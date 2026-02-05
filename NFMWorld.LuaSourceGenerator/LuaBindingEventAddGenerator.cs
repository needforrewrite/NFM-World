using System.Reflection;

namespace NFMWorld.LuaSourceGenerator;

internal class LuaBindingEventAddGenerator(LuaVisibleType type, LuaVisibleEvent @event, bool isStatic, int indentLevel = 0)
{
    public string GenerateCode()
    {
        var sb = new IndentedStringBuilder(indentLevel);

        var bindingName = @event.BindingName;
        sb.AppendLine($"[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]");
        sb.AppendLine($"private static int {bindingName}_add(lua_State L)");
        using (sb.Block())
        {
            sb.AppendLine("string? errorMsg = null;");
            sb.AppendLine("try");
            using (sb.Block())
            {
                if (!isStatic)
                {
                    sb.AppendLine($"var self = GetObjectFromStack<{type.Type.GetFullTypeName()}>(L, 1);");
                    if (!type.IsStruct)
                    {
                        sb.AppendLine("if (self == null)");
                        using (sb.Block())
                        {
                            sb.AppendLine($"errorMsg = \"Expected {type.Type.GetFullTypeName()} as first argument to {bindingName}_add\";");
                            sb.AppendLine("goto fail;");
                        }
                    }
                }

                if (isStatic)
                {
                    // For static events, handle both dot and colon syntax
                    // Colon syntax: table:add_Event(func) - function is at index 2
                    // Dot syntax: table.add_Event(func) - function is at index 1
                    sb.AppendLine("var funcIdx = lua_type(L, 1) == LUA_TFUNCTION ? 1 : 2;");
                    sb.AppendLine("if (lua_type(L, funcIdx) != LUA_TFUNCTION)");
                    using (sb.Block())
                    {
                        sb.AppendLine($"errorMsg = \"Expected function as listener to {bindingName}_add\";");
                        sb.AppendLine("goto fail;");
                    }
                }
                else
                {
                    sb.AppendLine("if (lua_type(L, 2) != LUA_TFUNCTION)");
                    using (sb.Block())
                    {
                        sb.AppendLine($"errorMsg = \"Expected function as listener to {bindingName}_add\";");
                        sb.AppendLine("goto fail;");
                    }
                }

                sb.AppendLine();

                // Convert Lua function to delegate

                // Subscribe to event
                if (isStatic)
                {
                    sb.AppendLine($"var listener = CreateEventDelegate<{@event.EventHandlerType.GetFullTypeName()}>(L, funcIdx, listener => {type.Type.GetFullTypeName()}.{@event.Name} -= listener);");
                    sb.AppendLine();
                    sb.AppendLine($"{type.Type.GetFullTypeName()}.{@event.Name} += listener;");
                }
                else
                {
                    sb.AppendLine($"var listener = CreateEventDelegate<{@event.EventHandlerType.GetFullTypeName()}>(L, 2, listener => self.{@event.Name} -= listener);");
                    sb.AppendLine();
                    sb.AppendLine($"self.{@event.Name} += listener;");
                    if (type.IsStruct)
                    {
                        sb.AppendLine("UpdateStruct(L, 1, self);");
                    }
                }

                sb.AppendLine("return 0;");
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
}