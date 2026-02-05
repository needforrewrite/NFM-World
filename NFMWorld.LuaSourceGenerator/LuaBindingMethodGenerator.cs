using System.Reflection;

namespace NFMWorld.LuaSourceGenerator;

internal class LuaBindingMethodGenerator(LuaVisibleType type, IReadOnlyList<LuaVisibleMethod> overloads, bool isStatic, int indentLevel = 0)
{
    public string GenerateCode()
    {
        var sb = new IndentedStringBuilder(indentLevel);

        var bindingName = overloads[0].BindingName;
        sb.AppendLine($"[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]");
        sb.AppendLine($"private static int {bindingName}(lua_State L)");
        using (sb.Block())
        {
            // sb.AppendDirective("#if DEBUG");
            // sb.AppendLine(
            //     """
            //     var __top = lua_gettop(L);
            //     int CheckTop(int offset)
            //     {
            //         if (lua_gettop(L) != __top + offset)
            //         {
            //     	    luaL_error(L, "Error stack corrupted");
            //         }
            //     }
            //     """
            // );
            // sb.AppendDirective("#endif");
            sb.AppendLine("string? errorMsg = null;");
            sb.AppendLine("try");
            using (sb.Block())
            {
                if (isStatic)
                {
                    sb.AppendLine("var argCount = lua_gettop(L);");
                }
                else
                {
                    sb.AppendLine("var argCount = lua_gettop(L) - 1; // First arg is self");
                    sb.AppendLine();
                    sb.AppendLine($"var self = GetObjectFromStack<{type.Type.GetFullTypeName()}>(L, 1);");

                    if (!type.IsStruct)
                    {
                        sb.AppendLine("if (self == null)");
                        using (sb.Block())
                        {
                            sb.AppendLine($"errorMsg = \"Expected {type.Type.GetFullTypeName()} as first argument to {bindingName}\";");
                            sb.AppendLine("goto fail;");
                        }
                    }
                }

                sb.AppendLine();

                if (overloads.Count == 1)
                {
                    // Single overload: no need for overload resolution

                    // BUG: LuaJIT __unm passes two arguments instead of one
                    if (overloads[0].Name != "op_UnaryNegation")
                    {
                        sb.AppendLine($"if (argCount != {overloads[0].Parameters.Length})");
                        using (sb.Block())
                        {
                            sb.AppendLine($"errorMsg = \"Invalid argument count for {overloads[0].LuaName}, expected {overloads[0].Parameters.Length} arguments\";");
                            sb.AppendLine("goto fail;");
                        }
                    }

                    AppendMethodCall(sb, overloads[0]);
                }
                else
                {
                    var allOverloadsHaveDifferingParameterCounts = overloads
                        .Select(o => o.Parameters.Length)
                        .Distinct()
                        .Count() == overloads.Count;

                    if (allOverloadsHaveDifferingParameterCounts)
                    {
                        // Simple overload resolution by parameter count
                        foreach (var overload in overloads)
                        {
                            sb.AppendLine($"if (argCount == {overload.Parameters.Length})");
                            using (sb.Block())
                            {
                                AppendMethodCall(sb, overload);
                            }
                        }
                        
                        sb.AppendLine(overloads.Count == 1
                            ? $"errorMsg = \"Invalid argument count for {overloads[0].LuaName}, expected {overloads[0].Parameters.Length} arguments\";"
                            : $"errorMsg = \"Invalid argument count for {overloads[0].LuaName}, expected one of ({string.Join(", ", overloads.Select(overload => overload.Parameters.Length).Distinct())}) arguments\";");
                        sb.AppendLine("goto fail;");
                    }
                    else
                    {
                        // Complex overload resolution by parameter count and types
                        sb.AppendLine("// Multiple overloads with same argument count - find best match");
                        sb.AppendLine("int bestScore = -1;");
                        sb.AppendLine("int bestIndex = -1;");
                        sb.AppendLine();
                        
                        var parameterOffset = isStatic ? 0 : 1;

                        for (int methodIdx = 0; methodIdx < overloads.Count; methodIdx++)
                        {
                            var method = overloads[methodIdx];
                            var parameters = method.Parameters;

                            sb.AppendLine($"// Try overload {methodIdx}: {method.Name}({string.Join(", ", parameters.Select(p => p.ParameterType.GetFullTypeName()))})");
                            using (sb.Block())
                            {
                                sb.AppendLine("int score = 0;");

                                for (int i = 0; i < parameters.Length; i++)
                                {
                                    var paramTypeName = parameters[i].ParameterType.GetFullTypeName();
                                    sb.AppendLine($"int score{i} = ScoreParameterCompatibility<{paramTypeName}>(L, {i + 1 + parameterOffset});");
                                    sb.AppendLine($"if (score{i} < 0) goto next{methodIdx};");
                                    sb.AppendLine($"else score += score{i};");
                                }

                                sb.AppendLine($"if (score > bestScore)");
                                using (sb.Block())
                                {
                                    sb.AppendLine($"bestScore = score;");
                                    sb.AppendLine($"bestIndex = {methodIdx};");
                                }
                            }

                            sb.AppendLine($"next{methodIdx}:");
                            sb.AppendLine();
                        }


                        // Now invoke the best match
                        sb.AppendLine("switch (bestIndex)");
                        using (sb.Block())
                        {
                            for (int methodIdx = 0; methodIdx < overloads.Count; methodIdx++)
                            {
                                var method = overloads[methodIdx];

                                sb.AppendLine($"case {methodIdx}:");
                                using (sb.Indent())
                                {
                                    using (sb.Block())
                                    {
                                        AppendMethodCall(sb, method);
                                    }
                                }
                            }
                        }
                    }

                    sb.AppendLine($"errorMsg = \"Invalid arguments for {overloads[0].LuaName}\";");
                    sb.AppendLine("goto fail;");
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

    private void AppendMethodCall(IndentedStringBuilder sb, LuaVisibleMethod overload)
    {
        // Extract parameters
        for (int i = 0; i < (!overload.IsExtensionMethod ? overload.Parameters.Length : overload.Parameters.Length - 1); i++)
        {
            var param = overload.Parameters[i];
            AppendParameterRead(sb, param, i, isStatic ? 1 : 2);
        }

        sb.AppendLine();

        // Call method
        if (overload is LuaVisibleOperator)
        {
            // Operator method

            if (overload.Parameters.Length == 2)
            {
                // Use the actual operator syntax
                var opSymbol = overload.Name.GetOperatorSymbol();
                if (opSymbol != null)
                {
                    sb.AppendLine($"var result = arg0 {opSymbol} arg1;");
                }
                else
                {
                    sb.AppendLine($"var result = {overload.DeclaringType!.GetFullTypeName()}.{overload.Name}(arg0, arg1);");
                }

            }
            else if (overload.Parameters.Length == 1)
            {
                var opSymbol = overload.Name.GetOperatorSymbol();
                if (opSymbol != null)
                {
                    sb.AppendLine($"var result = {opSymbol}arg0;");
                }
                else
                {
                    sb.AppendLine($"var result = {overload.DeclaringType!.GetFullTypeName()}.{overload.Name}(arg0);");
                }
            }
            else
            {
                throw new InvalidOperationException("Operator overloads must have 1 or 2 parameters");
            }
            
            sb.GeneratePushValue(overload.ReturnType, "result");
            sb.AppendLine("return 1;");
        }
        else if (overload is LuaVisibleConstructor)
        {
            // Constructor method

            var argumentList = string.Join(", ", Enumerable.Range(0, overload.Parameters.Length)
                .Select(i => $"arg{i}"));

            if (type.IsArray)
            {
                foreach (var i in Enumerable.Range(0, overload.Parameters.Length))
                {
                    sb.AppendLine(
                        $$"""
                        if (arg{{i}} < 0)
                        {
                            errorMsg = "Array size must be non-negative.";
                            goto fail;
                        }
                        """);
                }
            }

            sb.AppendLine(type.IsArray
                ? $"var result = new {type.Type.GetElementType()!.GetFullTypeName()}[{argumentList}];"
                : $"var result = new {overload.DeclaringType!.GetFullTypeName()}({argumentList});");

            sb.GeneratePushValue(type.Type, "result");
            sb.AppendLine("return 1;");
        }
        else
        {
            // Regular method

            var argumentList = string.Join(", ", Enumerable.Range(0, (!overload.IsExtensionMethod ? overload.Parameters.Length : overload.Parameters.Length - 1))
                .Select(i => $"arg{i}"));
            if (overload.ReturnType == typeof(void))
            {
                if (isStatic)
                {
                    sb.AppendLine($"{overload.DeclaringType!.GetFullTypeName()}.{overload.Name}({argumentList});");
                }
                else
                {
                    if (overload.IsExtensionMethod)
                    {
                        sb.AppendLine(
                            $"{overload.DeclaringType!.GetFullTypeName()}.{overload.Name}(self, {argumentList});");
                        if (type.IsStruct && !overload.IsReadOnlyStructMethod)
                        {
                            sb.AppendLine($"UpdateStruct(L, 1, self); // Update self in case it was modified");
                        }
                    }
                    else
                    {
                        if (type.IsStruct && !overload.IsReadOnlyStructMethod)
                        {
                            sb.AppendLine(overload.DeclaringType != type.Type
                                ? $"{overload.DeclaringType!.GetFullTypeName()} selfMutable = self;"
                                : $"var selfMutable = self;");

                            sb.AppendLine($"selfMutable.{overload.Name}({argumentList});");
                            sb.AppendLine($"UpdateStruct(L, 1, selfMutable); // Update self in case it was modified");
                        }
                        else
                        {
                            sb.AppendLine(overload.DeclaringType != type.Type
                                ? $"(({overload.DeclaringType!.GetFullTypeName()})self).{overload.Name}({argumentList});"
                                : $"self.{overload.Name}({argumentList});");
                        }
                    }
                }

                sb.AppendLine("return 0;");
            }
            else
            {
                if (isStatic)
                {
                    sb.AppendLine($"var result = {overload.DeclaringType!.GetFullTypeName()}.{overload.Name}({argumentList});");
                }
                else
                {
                    if (overload.IsExtensionMethod)
                    {
                        sb.AppendLine(
                            $"var result = {overload.DeclaringType!.GetFullTypeName()}.{overload.Name}(self, {argumentList});");
                        if (type.IsStruct && !overload.IsReadOnlyStructMethod)
                        {
                            sb.AppendLine($"UpdateStruct(L, 1, self); // Update self in case it was modified");
                        }
                    }
                    else
                    {
                        if (type.IsStruct && !overload.IsReadOnlyStructMethod)
                        {
                            sb.AppendLine(overload.DeclaringType != type.Type
                                ? $"{overload.DeclaringType!.GetFullTypeName()} selfMutable = self;"
                                : $"var selfMutable = self;");

                            sb.AppendLine($"var result = selfMutable.{overload.Name}({argumentList});");
                            sb.AppendLine($"UpdateStruct(L, 1, selfMutable); // Update self in case it was modified");
                        }
                        else
                        {
                            sb.AppendLine(overload.DeclaringType != type.Type
                                ? $"var result = (({overload.DeclaringType!.GetFullTypeName()})self).{overload.Name}({argumentList});"
                                : $"var result = self.{overload.Name}({argumentList});");
                        }
                    }
                }

                sb.GeneratePushValue(overload.ReturnType, "result");
                sb.AppendLine("return 1;");
            }
        }
    }

    private void AppendParameterRead(IndentedStringBuilder sb, ParameterInfo param, int index, int stackOffset)
    {
        var varName = $"arg{index}";
        var stackIndex = index + stackOffset;
        var paramType = param.ParameterType;
        var fullTypeName = paramType.GetFullTypeName();

        if (paramType.IsNullable())
        {
            // Nullable value type (int?, float?, etc.)
            var underlyingType = Nullable.GetUnderlyingType(paramType)!;
            var underlyingTypeName = underlyingType.GetFullTypeName();
            sb.AppendLine($"{fullTypeName} {varName} = ToObjectNullable<{underlyingTypeName}>(L, {stackIndex})!;");
        }
        else
        {
            sb.AppendLine($"{fullTypeName} {varName} = ToObject<{fullTypeName}>(L, {stackIndex})!;");
        }
    }
}