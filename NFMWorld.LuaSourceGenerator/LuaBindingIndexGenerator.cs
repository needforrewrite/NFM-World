using System.Reflection;
using System.Runtime.CompilerServices;

namespace NFMWorld.LuaSourceGenerator;

internal class LuaBindingIndexGenerator(
    LuaVisibleType type,
    IReadOnlyList<LuaVisibleField> fields,
    IReadOnlyList<LuaVisibleProperty> properties,
    LuaVisibleIndexer? indexer,
    IReadOnlyList<LuaVisibleMethod> methods,
    bool isStatic,
    int indentLevel = 0)
{
    public string BindingName => $"{type.Type.GetGenericTypeLuaName()}_{(isStatic ? "static_index" : "index")}";
    
    public string GenerateCode()
    {
        var sb = new IndentedStringBuilder(indentLevel);

        sb.AppendLine($"[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]");
        sb.AppendLine($"private static int {BindingName}(lua_State L)");
        using (sb.Block())
        {
            sb.AppendLine("string? errorMsg = null;");
            sb.AppendLine("try");
            using (sb.Block())
            {
                if (!isStatic)
                {
                    if (type.IsStruct)
                    {
                        sb.AppendLine($"var obj = GetObjectFromStack<{type.Type.GetFullTypeName()}>(L, 1);");
                    }
                    else
                    {
                        sb.AppendLine($"var obj = GetObjectFromStack<{type.Type.GetFullTypeName()}>(L, 1);");
                        sb.AppendLine("if (obj == null)");
                        using (sb.Block())
                        {
                            sb.AppendLine($"errorMsg = \"Attempt to index a nil value as if it were type {type.FullName}\";");
                            sb.AppendLine("goto fail;");
                        }
                    }
                }
                
                // Check for array or indexer access first (when key is number or table)
                var isArray = type.IsArray && !isStatic;

                var isInlineArray = type.IsInlineArray && !isStatic;
                if (isArray || indexer != null || isInlineArray)
                {
                    AppendArrayIndexing(isInlineArray, isArray, sb);
                    sb.AppendLine();
                }
                
                // String key for named properties/fields/methods
                sb.AppendLine("var key = lua_tostring(L, 2);");
                sb.AppendLine("if (key == null) { lua_pushnil(L); return 1; }");
                sb.AppendLine();

                sb.AppendLine("switch (key)");
                using (sb.Block())
                {
                    // Properties
                    foreach (var prop in properties)
                    {
                        if (prop.HasGetter)
                        {
                            sb.AppendLine($"case \"{prop.LuaName}\":");
                            using (sb.Block())
                            {
                                if (isStatic)
                                {
                                    sb.GeneratePushValue(prop.PropertyType, $"{prop.DeclaringType!.GetFullTypeName()}.{prop.Name}");
                                }
                                else
                                {
                                    AppendPushValueWithParentTracking(
                                        sb,
                                        containingTypeExpression: $"(({prop.DeclaringType!.GetFullTypeName()})obj)",
                                        memberExpression: prop.Name,
                                        type: prop.PropertyType,
                                        parentType: type.Type,
                                        isReadOnly: !prop.HasSetter
                                    );
                                }

                                sb.AppendLine("return 1;");
                            }
                        }
                    }

                    // Fields
                    foreach (var field in fields)
                    {
                        sb.AppendLine($"case \"{field.LuaName}\":");
                        using (sb.Block())
                        {
                            if (isStatic)
                            {
                                sb.GeneratePushValue(field.FieldType, $"{type.Type.GetFullTypeName()}.{field.Name}");
                            }
                            else
                            {
                                AppendPushValueWithParentTracking(
                                    sb,
                                    containingTypeExpression: $"(({type.Type.GetFullTypeName()})obj)",
                                    memberExpression: field.Name,
                                    type: field.FieldType,
                                    parentType: type.Type,
                                    isReadOnly: field.IsReadOnly
                                );
                            }

                            sb.AppendLine("return 1;");
                        }
                    }
                    
                    // Methods
                    foreach (var method in methods.DistinctBy(method => method.LuaName))
                    {
                        sb.AppendLine($"case \"{method.LuaName}\":");
                        using (sb.Block())
                        {
                            sb.AppendLine($"lua_pushcfunction(L, &{method.BindingName});");
                            sb.AppendLine("return 1;");
                        }
                    }

                    // Default case
                    sb.AppendLine("default:");
                    using (sb.Block())
                    {
                        sb.AppendLine("lua_pushnil(L);");
                        sb.AppendLine("return 1;");
                    }
                }
            }
            sb.AppendLine(
                """
                catch (System.Exception ex)
                {
                    errorMsg = FormatException(ex);
                }
                fail:
                if (errorMsg != null)
                {
                    return luaL_error(L, errorMsg);
                }
                return Unreachable();
                """
            );
        }

        return sb.ToString();
    }

    private static void AppendPushValueWithParentTracking(IndentedStringBuilder sb, string containingTypeExpression, string memberExpression, Type type, Type parentType, bool isReadOnly)
    {
        // Only use parent tracking for value types (structs)
        if (type is { IsValueType: true, IsPrimitive: false, IsEnum: false } && !type.IsNullable())
        {
            var typeName = type.GetFullTypeName();
            var metatable = type.GetSafeTypeName();

            // For structs, we need to get the ID from userdata on the stack
            using (sb.Block())
            {
                sb.AppendLine("var ptr = lua_touserdata(L, 1);");
                sb.AppendLine("if (ptr != null)");
                using (sb.Block())
                {
                    sb.AppendLine("var parentId = *(int*)ptr;");
                    if (parentType.IsValueType)
                    {
                        sb.AppendLine($"PushStructWithParent(L, {containingTypeExpression}.{memberExpression}, \"MT_{metatable}\", parentId, static (obj, value) => {{ System.Diagnostics.Debug.WriteLine($\"Attempted to assign value of struct {{obj}} ({{obj.GetType()}}) member '{memberExpression}' to {{value}} but Lua only owns a temporary value and there is no way to track it to its parent. Nothing will be set.\"); }});");
                    }
                    else if (isReadOnly)
                    {
                        sb.AppendLine($"PushStructWithParent(L, {containingTypeExpression}.{memberExpression}, \"MT_{metatable}\", parentId, static (obj, value) => {{ System.Diagnostics.Debug.WriteLine($\"Attempted to assign value of struct {{obj}} ({{obj.GetType()}}) member '{memberExpression}' to {{value}} but the field is read-only. Nothing will be set.\"); }});");
                    }
                    else
                    {
                        sb.AppendLine($"PushStructWithParent(L, {containingTypeExpression}.{memberExpression}, \"MT_{metatable}\", parentId, static (obj, value) => {containingTypeExpression}.{memberExpression} = ({typeName})value);");
                    }
                }
            }
        }
        else
        {
            sb.GeneratePushValue(type, $"{containingTypeExpression}.{memberExpression}");
        }
    }

    private void AppendArrayIndexing(bool isInlineArray, bool isArray, IndentedStringBuilder sb)
    {
        var inlineArrayLength = isInlineArray ? type.InlineArrayLength : null;
        var inlineArrayElementType = isInlineArray ? type.InlineArrayElementType : null;

        Type elementType;
        if (isArray)
        {
            elementType = type.ElementType!;
        }
        else if (isInlineArray)
        {
            elementType = inlineArrayElementType!;
        }
        else
        {
            elementType = indexer!.PropertyType;
        }
                    
        int rank = isArray ? type.ArrayRank!.Value : indexer?.IndexParameters.Length ?? 1;

        if (rank == 1)
        {
            Type parameterType;
            if (isArray || isInlineArray)
            {
                parameterType = typeof(int);
            }
            else
            {
                parameterType = indexer!.IndexParameters[0].ParameterType;
            }

            sb.AppendLine("// Check if key is a number (array/indexer access)");
            sb.AppendLine("if (lua_type(L, 2) == LUA_TNUMBER)");
            using (sb.Block())
            {
                if (parameterType == typeof(int))
                {
                    sb.AppendLine("var index = (int)lua_tointeger(L, 2) - 1; // Convert from 1-indexed to 0-indexed");
                }
                else if (parameterType == typeof(long))
                {
                    sb.AppendLine("var index = (long)(lua_tointeger(L, 2) - 1); // Convert from 1-indexed to 0-indexed");
                }
                else if (parameterType == typeof(string))
                {
                    sb.AppendLine("var index = lua_tostring(L, 2);");
                }
                else
                {
                    throw new InvalidOperationException("Unsupported indexer parameter type for single-parameter indexer.");
                }

                if (inlineArrayLength is { } length)
                {
                    sb.AppendLine($"if (index < 0 || index >= {length})");
                    using (sb.Block())
                    {
                        sb.AppendLine($"errorMsg = $\"Index {{index + 1}} out of bounds for inline array of length {length}.\";");
                        sb.AppendLine("goto fail;");
                    }
                }

                sb.AppendLine("var element = obj[index];");
                sb.GeneratePushValue(elementType, "element");
                sb.AppendLine("return 1;");
            }

            sb.AppendLine();
        }

        // Handle table-based indexing for multi-dimensional arrays and multi-parameter indexers
        if (rank > 1)
        {
            sb.AppendLine("// Check if key is a table (multi-dimensional array/indexer access)");
            sb.AppendLine("if (lua_type(L, 2) == LUA_TTABLE)");
            using (sb.Block())
            {
                for (int i = 0; i < rank; i++)
                {
                    sb.AppendLine($"lua_rawgeti(L, 2, {i + 1});");
                    var parameterType = isArray || isInlineArray ? typeof(int) : indexer!.IndexParameters[i].ParameterType;
                    if (parameterType == typeof(int))
                    {
                        sb.AppendLine($"var index{i} = (int)lua_tointeger(L, -1) - 1; // Convert from 1-indexed to 0-indexed");
                    }
                    else if (parameterType == typeof(long))
                    {
                        sb.AppendLine($"var index{i} = (long)(lua_tointeger(L, -1) - 1); // Convert from 1-indexed to 0-indexed");
                    }
                    else if (parameterType == typeof(string))
                    {
                        sb.AppendLine($"var index{i} = lua_tostring(L, -1);");
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported indexer parameter type for multi-parameter indexer.");
                    }
                    sb.AppendLine("lua_pop(L, 1);");
                }

                sb.AppendLine($"var element = obj[{string.Join(", ", Enumerable.Range(0, rank).Select(i => $"index{i}"))}];");
                sb.GeneratePushValue(elementType, "element");
                sb.AppendLine("return 1;");
            }
            sb.AppendLine();
        }
    }
}