using System.Reflection;
using System.Runtime.CompilerServices;

namespace NFMWorld.LuaSourceGenerator;

internal class LuaBindingNewIndexGenerator(
    LuaVisibleType type,
    IReadOnlyList<LuaVisibleField> fields,
    IReadOnlyList<LuaVisibleProperty> properties,
    LuaVisibleIndexer? indexer,
    bool isStatic,
    int indentLevel = 0)
{
    public string BindingName => $"{type.Type.GetGenericTypeLuaName()}_{(isStatic ? "static_newindex" : "newindex")}";
    
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
                        if (prop.HasSetter)
                        {
                            sb.AppendLine($"case \"{prop.LuaName}\":");
                            using (sb.Block())
                            {
                                AppendRead(sb, "value", prop.PropertyType, 3);
                                if (isStatic)
                                {
                                    sb.AppendLine($"{prop.DeclaringType!.GetFullTypeName()}.{prop.Name} = value;");
                                }
                                else
                                {
                                    sb.AppendLine($"obj.{prop.Name} = value;");
                                    if (type.IsStruct && !prop.IsExtensionProperty) sb.AppendLine("UpdateStruct(L, 1, obj);");
                                }
                                sb.AppendLine("return 0;");
                            }
                        }
                    }

                    // Fields
                    foreach (var field in fields)
                    {
                        sb.AppendLine($"case \"{field.LuaName}\":");
                        using (sb.Block())
                        {
                            AppendRead(sb, "value", field.FieldType, 3);
                            if (isStatic)
                            {
                                sb.AppendLine($"{field.DeclaringType!.GetFullTypeName()}.{field.Name} = value;");
                            }
                            else
                            {
                                sb.AppendLine($"obj.{field.Name} = value;");
                                if (type.IsStruct) sb.AppendLine("UpdateStruct(L, 1, obj);");
                            }
                            sb.AppendLine("return 0;");
                        }
                    }

                    // Default case
                    sb.AppendLine("default:");
                    using (sb.Block())
                    {
                        sb.AppendLine("return 0;");
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

                AppendRead(sb, "value", elementType, 3);
                sb.AppendLine("obj[index] = value;");
                if (type.IsStruct) sb.AppendLine("UpdateStruct(L, 1, obj);");
                sb.AppendLine("return 0;");
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

                AppendRead(sb, "value", elementType, 3);
                sb.AppendLine($"obj[{string.Join(", ", Enumerable.Range(0, rank).Select(i => $"index{i}"))}] = value;");
                if (type.IsStruct) sb.AppendLine("UpdateStruct(L, 1, obj);");
                sb.AppendLine("return 0;");
            }
            sb.AppendLine();
        }
    }

    private static void AppendRead(IndentedStringBuilder sb, string varName, Type type, int stackIndex)
    {
        var fullTypeName = type.GetFullTypeName();

        if (type.IsNullable())
        {
            // Nullable value type (int?, float?, etc.)
            var underlyingType = Nullable.GetUnderlyingType(type)!;
            var underlyingTypeName = underlyingType.GetFullTypeName();
            sb.AppendLine($"{fullTypeName} {varName} = ToObjectNullable<{underlyingTypeName}>(L, {stackIndex})!;");
        }
        else
        {
            sb.AppendLine($"{fullTypeName} {varName} = ToObject<{fullTypeName}>(L, {stackIndex})!;");
        }
    }
}