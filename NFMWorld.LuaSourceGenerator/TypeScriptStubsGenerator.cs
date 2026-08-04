using System.Text;
using Microsoft.CodeAnalysis;

namespace NFMWorld.LuaSourceGenerator;

/// <summary>
/// Generates TypeScript definition stubs for [LuaVisible] types.
/// Outputs declare class/interface definitions for the CEF-based frontend.
/// </summary>
internal sealed class TypeScriptStubsGenerator(LuaTypeMetadata type, Compilation compilation, Dictionary<string, string> luaVisibleNameMap)
{
    private readonly Dictionary<string, string> _luaVisibleNameMap = luaVisibleNameMap;
    public string GenerateCode()
    {
        var sb = new StringBuilder();
        var luaName = type.LuaName;

        var isInterface = type.IsInterface;
        sb.AppendLine($"declare {(isInterface ? "interface" : "class")} {luaName}");

        // Build extends/implements chain
        var bases = new List<string>();
        if (type.BaseTypeFullName != null)
            bases.Add(StubTypeName(type.BaseTypeFullName));
        bases.AddRange(type.InterfaceFullNames.Select(StubTypeName));

        if (bases.Count > 0)
        {
            if (!isInterface && type.BaseTypeFullName != null)
            {
                sb.AppendLine($"    extends {StubTypeName(type.BaseTypeFullName)}");
                var ifaces = type.InterfaceFullNames.Select(StubTypeName).ToList();
                if (ifaces.Count > 0)
                    sb.AppendLine($"    implements {string.Join(", ", ifaces)}");
            }
            else if (isInterface)
            {
                sb.AppendLine($"    extends {string.Join(", ", bases)}");
            }
            else
            {
                sb.AppendLine($"    implements {string.Join(", ", bases)}");
            }
        }

        sb.AppendLine("{");

        // Instance properties
        foreach (var prop in type.InstanceProperties.Where(p => p.HasGetter))
            sb.AppendLine($"    {(prop.HasSetter ? "" : "readonly ")}{prop.LuaName}: {ToTSTypeName(prop.PropertyType)};");

        // Instance fields
        foreach (var field in type.InstanceFields)
            sb.AppendLine($"    {(!field.IsReadOnly ? "" : "readonly ")}{field.LuaName}: {ToTSTypeName(field.FieldType)};");

        // Instance methods
        foreach (var m in type.InstanceMethods)
        {
            var docParams = m.IsExtension ? m.Parameters.Skip(1).ToArray() : m.Parameters;
            var paramStr = string.Join(", ", docParams.Select(p => $"{TsParamName(p)}: {ToTSTypeName(p.Type)}"));
            sb.AppendLine($"    {m.FullLuaName}({paramStr}): {ToTSTypeName(m.ReturnType)};");
        }

        // Instance events
        foreach (var evt in type.InstanceEvents)
        {
            var sig = GetEventDelegateSignature(evt);
            sb.AppendLine($"    add_{evt.LuaName}(callback: ({sig}) => void): void;");
            sb.AppendLine($"    remove_{evt.LuaName}(callback: ({sig}) => void): void;");
        }

        // Static properties
        foreach (var prop in type.StaticProperties.Where(p => p.HasGetter))
            sb.AppendLine($"    static {(prop.HasSetter ? "" : "readonly ")}{prop.LuaName}: {ToTSTypeName(prop.PropertyType)};");

        // Static fields
        foreach (var field in type.StaticFields)
            sb.AppendLine($"    static {(!field.IsReadOnly ? "" : "readonly ")}{field.LuaName}: {ToTSTypeName(field.FieldType)};");

        // Static methods
        foreach (var m in type.StaticMethods)
        {
            var paramStr = string.Join(", ", m.Parameters.Select(p => $"{TsParamName(p)}: {ToTSTypeName(p.Type)}"));
            sb.AppendLine($"    static {m.FullLuaName}({paramStr}): {ToTSTypeName(m.ReturnType)};");
        }

        // Static events
        foreach (var evt in type.StaticEvents)
        {
            var sig = GetEventDelegateSignature(evt);
            sb.AppendLine($"    static add_{evt.LuaName}(callback: ({sig}) => void): void;");
            sb.AppendLine($"    static remove_{evt.LuaName}(callback: ({sig}) => void): void;");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GetEventDelegateSignature(LuaEventMetadata evt)
    {
        var handlerType = compilation.GetTypeByMetadataName(evt.HandlerType);
        if (handlerType == null) return "...";
        var invoke = handlerType.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();
        if (invoke == null) return "...";
        return string.Join(", ", invoke.Parameters.Select(p =>
            $"{TsParamName(p)}: {ToTSTypeName(p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""))}"));
    }

    // ==================================================================
    // Type name conversion helpers
    // ==================================================================

    private string ToTSTypeName(string t)
    {
        if (t.EndsWith("?")) return $"{ToTSTypeName(t.Substring(0, t.Length - 1))} | null";
        return t switch
        {
            "int" or "long" or "float" or "double" or "byte" or "sbyte"
                or "short" or "ushort" or "uint" or "ulong" or "decimal" => "number",
            "bool" => "boolean",
            "string" => "string",
            "void" => "void",
            "object" => "any",
            _ => IsFixedMathType(t) ? FixedMathToTSName(t) : StubTypeName(t)
        };
    }

    private string FixedMathToTSName(string t)
    {
        var baseT = t.Contains('<') ? t.Substring(0, t.IndexOf('<')) : t;
        if (baseT == "Fixed64" || baseT.EndsWith(".Fixed64")) return "fixed64";
        if (baseT == "Vector3d" || baseT.EndsWith(".Vector3d")) return "fixed64vector3";
        if (baseT == "f64AngleSingle" || baseT.EndsWith(".f64AngleSingle")) return "fixed64angle";
        if (baseT == "f64Euler" || baseT.EndsWith(".f64Euler")) return "fixed64euler";
        return StubTypeName(baseT);
    }

    private static bool IsFixedMathType(string t)
    {
        var baseT = t.Contains('<') ? t.Substring(0, t.IndexOf('<')) : t;
        return baseT is "Fixed64" or "Vector3d" or "f64AngleSingle" or "f64Euler" or "Fixed4x4"
            || baseT.EndsWith(".Fixed64") || baseT.EndsWith(".Vector3d")
            || baseT.EndsWith(".f64AngleSingle") || baseT.EndsWith(".f64Euler")
            || baseT.EndsWith(".Fixed4x4");
    }

    /// <summary>TypeScript-friendly type name for stub declarations. Uses LuaName for [LuaVisible] types, sanitized full name otherwise.</summary>
    private string StubTypeName(string fullName)
    {
        // Check if this is a [LuaVisible] type — use its LuaName
        if (_luaVisibleNameMap.TryGetValue(fullName, out var luaName))
            return luaName;
        // Handle tuples
        if (fullName.StartsWith("(")) return "ValueTuple";
        // Handle arrays: int[,,] → intArray, int[] → intArray
        if (fullName.EndsWith("]"))
        {
            var bracketIdx = fullName.IndexOf('[');
            var elemName = bracketIdx >= 0 ? StubTypeName(fullName.Substring(0, bracketIdx)) : fullName;
            return elemName + "Array";
        }
        // For non-LuaVisible types, use sanitized full name (matching external type stub names)
        var sanitized = SanitizeForStub(fullName);
        return sanitized;
    }

    /// <summary>Sanitize a full type name to match external stub file naming convention.</summary>
    private static string SanitizeForStub(string fullName)
    {
        // Include generic args: UnlimitedArray<string> → UnlimitedArray_string
        var name = fullName.Contains('<') ? fullName.Replace('<', '_').Replace('>', '_').Replace(", ", "_").Replace(",", "_") : fullName;
        name = name.Replace("global::", "").Replace(".", "_").Replace("[]", "Array");
        return name;
    }

    private static string TsParamName(LuaParameterMetadata p) =>
        p.Name ?? "arg";

    private static string TsParamName(IParameterSymbol p) =>
        p.Name ?? $"arg{p.Ordinal}";
}
