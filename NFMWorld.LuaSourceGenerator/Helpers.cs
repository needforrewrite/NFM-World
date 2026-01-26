using System.Reflection;

namespace NFMWorld.LuaSourceGenerator;

public static class Helpers
{
    extension(Type type)
    {
        /// <summary>
        /// Check if a type is a ref struct (cannot be marshalled to Lua).
        /// </summary>
        public bool IsRefStruct()
        {
            return type is { IsValueType: true, IsByRefLike: true };
        }

        /// <summary>
        /// Check if a type is a static class (sealed + abstract + class).
        /// </summary>
        public bool IsStaticClass()
        {
            return type is { IsClass: true, IsAbstract: true, IsSealed: true };
        }

        public string GetGenericTypeLuaName()
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var rank = type.GetArrayRank();
                if (rank == 1)
                {
                    return $"ArrayOf{GetSimpleTypeName(elementType)}";
                }
                else
                {
                    return $"ArrayOf{GetSimpleTypeName(elementType)}{rank}D";
                }
            }

            // Handle nested types (like List<int>.Enumerator)
            if (type.IsNested && type.DeclaringType != null)
            {
                // For nested types of generic types, we need to reconstruct the parent's generic arguments
                // E.g., List<int>.Enumerator has DeclaringType = List`1, but we want "List_Int32"
                if (type.DeclaringType.IsGenericType)
                {
                    // Get the generic arguments that apply to the declaring type
                    // For List<int>.Enumerator, the Enumerator shares the same generic argument as List
                    var declaringTypeArgs = type.GetGenericArguments();
                    var declaringTypeArgCount = type.DeclaringType.GetGenericArguments().Length;

                    // Build the declaring type name with its generic arguments
                    var declaringBaseName = type.DeclaringType.Name.Split('`')[0];
                    if (declaringTypeArgCount > 0 && declaringTypeArgs.Length >= declaringTypeArgCount)
                    {
                        var declaringArgNames = string.Join("_", declaringTypeArgs.Take(declaringTypeArgCount).Select(GetSimpleTypeName));
                        var declaringName = $"{declaringBaseName}_{declaringArgNames}";
                        return $"{declaringName}_{type.Name}";
                    }
                    else
                    {
                        return $"{declaringBaseName}_{type.Name}";
                    }
                }
                else
                {
                    var declaringName = GetGenericTypeLuaName(type.DeclaringType);
                    return $"{declaringName}_{type.Name}";
                }
            }

            if (!type.IsGenericType)
                return type.Name;

            var baseName = type.Name.Split('`')[0];
            var genericArgs = type.GetGenericArguments();
            var argNames = string.Join("_", genericArgs.Select(GetSimpleTypeName));
            return $"{baseName}_{argNames}";
        }

        private string GetSimpleTypeName()
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var rank = type.GetArrayRank();
                return rank == 1 ? $"{GetSimpleTypeName(elementType)}Array" : $"{GetSimpleTypeName(elementType)}{rank}DArray";
            }
            if (type.IsGenericType)
            {
                return GetGenericTypeLuaName(type);
            }
            return type.Name;
        }

        public string GetFullTypeName()
        {
            // Handle in/out/ref types
            if (type.IsByRef)
            {
                var elementType = type.GetElementType()!;
                return GetFullTypeName(elementType);
            }

            // Handle array types
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var rank = type.GetArrayRank();
                var brackets = rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]";
                return GetFullTypeName(elementType) + brackets;
            }

            // Handle nullable types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(type)!;
                return GetFullTypeName(underlyingType) + "?";
            }

            // Handle generic types
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();

                // Get the name, handling nested types
                var name = genericTypeDef.FullName ?? genericTypeDef.Name;

                // For nested types like List<T>.Enumerator, the name includes +
                // We need to handle the outer generic type first
                if (type.IsNested && type.DeclaringType != null && type.DeclaringType.IsGenericType)
                {
                    // For nested types of constructed generics (e.g., List<int>.Enumerator),
                    // the DeclaringType is the generic definition (List`1), but the nested type
                    // itself has the resolved generic arguments from the constructed parent.
                    // We need to reconstruct the declaring type with those arguments.
                    var allGenericArgs = type.GetGenericArguments();
                    var declaringTypeArgCount = type.DeclaringType.GetGenericArguments().Length;

                    // Get the declaring type's base name and namespace
                    var declaringNamespace = type.DeclaringType.Namespace;
                    var declaringBaseName = type.DeclaringType.Name.Split('`')[0];

                    // Build the full declaring type name with generic arguments
                    string declaringTypeName;
                    if (declaringTypeArgCount > 0 && allGenericArgs.Length >= declaringTypeArgCount)
                    {
                        var declaringTypeArgs = allGenericArgs.Take(declaringTypeArgCount).ToArray();
                        var argNames = string.Join(", ", declaringTypeArgs.Select(GetFullTypeName));
                        declaringTypeName = $"{declaringNamespace}.{declaringBaseName}<{argNames}>";
                    }
                    else
                    {
                        declaringTypeName = $"{declaringNamespace}.{declaringBaseName}";
                    }

                    // Get just the nested type's name (after the +)
                    var nestedName = type.Name;
                    var tickIndex = nestedName.IndexOf('`');
                    if (tickIndex > 0)
                    {
                        nestedName = nestedName.Substring(0, tickIndex);
                    }

                    // If the nested type itself has generic arguments beyond the declaring type's, add them
                    var nestedGenericArgs = allGenericArgs.Skip(declaringTypeArgCount).ToArray();
                    if (nestedGenericArgs.Length > 0)
                    {
                        var nestedArgNames = string.Join(", ", nestedGenericArgs.Select(GetFullTypeName));
                        return $"{declaringTypeName}.{nestedName}<{nestedArgNames}>";
                    }
                    else
                    {
                        return $"{declaringTypeName}.{nestedName}";
                    }
                }

                // Remove the `1, `2, etc. from the name
                var tickIndex2 = name.IndexOf('`');
                if (tickIndex2 > 0)
                {
                    name = name.Substring(0, tickIndex2);
                }

                var genericArgs = type.GetGenericArguments();
                var argNames2 = string.Join(", ", genericArgs.Select(GetFullTypeName));
                return $"{name}<{argNames2}>";
            }

            // Handle nested types (non-generic)
            if (type.IsNested && type.DeclaringType != null)
            {
                return GetFullTypeName(type.DeclaringType) + "." + type.Name;
            }

            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(void)) return "void";
            if (type == typeof(object)) return "object";
            return type.FullName ?? type.Name;
        }

        public bool IsNullable()
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public bool IsInterfaceWithStaticAbstractMethods()
        {
            return type.IsInterface && type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(m => m is { IsStatic: true, IsAbstract: true });
        }
        
        public string GetSafeTypeName()
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var rank = type.GetArrayRank();
                var suffix = rank == 1 ? "Array" : $"Array{rank}D";
                return GetSafeTypeName(elementType) + suffix;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                // Handle nested generic types like List<int>.Enumerator
                if (type.IsNested && type.DeclaringType != null && type.DeclaringType.IsGenericType)
                {
                    // For nested types of constructed generics (e.g., List<int>.Enumerator),
                    // the DeclaringType is the generic definition (List`1), but the nested type
                    // itself has the resolved generic arguments from the constructed parent.
                    // We need to build the declaring type name using those arguments.
                    var allGenericArgs = type.GetGenericArguments();
                    var declaringTypeArgCount = type.DeclaringType.GetGenericArguments().Length;
                    var declaringBaseName = type.DeclaringType.Name.Split('`')[0];
                    var nestedName = type.Name.Split('`')[0];

                    // Get generic args that belong to the declaring type
                    var declaringTypeArgs = allGenericArgs.Take(declaringTypeArgCount).ToArray();

                    // Get generic args that belong to this nested type only
                    var nestedGenericArgs = allGenericArgs.Skip(declaringTypeArgCount).ToArray();

                    // Build the declaring type name with its generic arguments
                    string declaringName;
                    if (declaringTypeArgs.Length > 0)
                    {
                        var declaringArgNames = string.Join("_", declaringTypeArgs.Select(GetSafeTypeName));
                        declaringName = $"{declaringBaseName}_{declaringArgNames}";
                    }
                    else
                    {
                        declaringName = declaringBaseName;
                    }

                    // Build the full name
                    if (nestedGenericArgs.Length > 0)
                    {
                        var nestedArgNames = string.Join("_", nestedGenericArgs.Select(GetSafeTypeName));
                        return $"{declaringName}_{nestedName}_{nestedArgNames}";
                    }
                    else
                    {
                        return $"{declaringName}_{nestedName}";
                    }
                }

                var baseName = type.Name.Split('`')[0];
                var genericArgs = type.GetGenericArguments();
                var argNames2 = string.Join("_", genericArgs.Select(GetSafeTypeName));
                return $"{baseName}_{argNames2}".Replace(".", "_").Replace("+", "_");
            }

            // Handle nested non-generic types
            if (type.IsNested && type.DeclaringType != null)
            {
                return GetSafeTypeName(type.DeclaringType) + "_" + type.Name;
            }

            return type.Name.Replace(".", "_").Replace("+", "_").Replace("`", "_");
        }
    }

    extension(string name)
    {
        public string ToCamelCase()
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        public string? GetLuaMetamethodName() => name switch
        {
            "op_Addition" => "__add",
            "op_Subtraction" => "__sub",
            "op_Multiply" => "__mul",
            "op_Division" => "__div",
            "op_Modulus" => "__mod",
            "op_UnaryNegation" => "__unm",
            "op_Equality" => "__eq",
            "op_LessThan" => "__lt",
            "op_LessThanOrEqual" => "__le",
            "op_GreaterThan" => "__gt",
            "op_GreaterThanOrEqual" => "__ge",
            _ => null
        };

        public string? GetOperatorSymbol() => name switch
        {
            "op_Addition" => "+",
            "op_Subtraction" => "-",
            "op_Multiply" => "*",
            "op_Division" => "/",
            "op_Modulus" => "%",
            "op_UnaryNegation" => "-",
            "op_Equality" => "==",
            "op_LessThan" => "<",
            "op_LessThanOrEqual" => "<=",
            "op_GreaterThan" => ">",
            "op_GreaterThanOrEqual" => ">=",
            _ => null
        };
    }

    extension(MethodBase methodBase)
    {
        public Type ReturnType =>
            methodBase switch
            {
                MethodInfo mi => mi.ReturnType,
                ConstructorInfo => typeof(void),
                _ => throw new InvalidOperationException("Unknown MethodBase type for ReturnType.")
            };
    }

    extension(IndentedStringBuilder sb)
    {
        public void GeneratePushValue(Type valueType, string valueExpression)
        {
            if (valueType.IsNullable())
            {
                sb.AppendLine($"PushNullableValue(L, {valueExpression});");
            }
            else
            {
                sb.AppendLine($"PushValue(L, {valueExpression});");
            }
        }
    }
}