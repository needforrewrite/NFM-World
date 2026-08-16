using System.Text;
using Microsoft.CodeAnalysis;
using WorldXaml.Generator.Common;

namespace NFMWorld.LuaSourceGenerator;

/// <summary>
/// Generates Lua Language Server (LuaLS) annotation stubs for [LuaVisible] types.
/// Outputs ---@class, ---@field, ---@param, ---@return annotations for IDE autocomplete.
/// </summary>
internal sealed class LuaStubsGenerator(BaseLuaTypeMetadata type)
{
    public string GenerateCode()
    {
        var sb = new IndentedStringBuilder();

        // Instance annotation (for objects created via .new())
        GenerateInstanceClass(sb);

        // Static/class annotation (for TypeTable access)
        GenerateClassAnnotation(sb);

        return sb.ToString();
    }

    private void GenerateInstanceClass(IndentedStringBuilder sb)
    {
        var luaName = type.LuaName;

        // Build base type list for ---@class, filtering self-references and ILuaUserData
        var baseTypes = new List<string>();
        if (type is LuaTypeMetadata { BaseType: { } baseType })
        {
            var baseStub = baseType.LuaName;
            if (baseStub != luaName)
                baseTypes.Add(baseStub);
        }

        if (type is LuaTypeMetadata { Interfaces: { } intfs })
        {
            foreach (var iface in intfs)
            {
                var ifaceStub = iface.LuaName;
                if (ifaceStub != luaName && ifaceStub != "Lua.ILuaUserData")
                    baseTypes.Add(ifaceStub);
            }
        }

        if (baseTypes.Count > 0)
            sb.AppendLine($"---@class {luaName} : {string.Join(", ", baseTypes)}");
        else
            sb.AppendLine($"---@class {luaName}");

        if (type is LuaTypeMetadata luaTypeMetadata)
        {
            // Fields and properties
            foreach (var prop in luaTypeMetadata.InstanceProperties.Where(p => p.HasGetter))
                sb.AppendLine($"---@field {prop.LuaName} {ToLuaTypeName(prop.PropertyType)}");

            foreach (var field in luaTypeMetadata.InstanceFields)
                sb.AppendLine($"---@field {field.LuaName} {ToLuaTypeName(field.FieldType)}");

            // Instance methods
            foreach (var m in luaTypeMetadata.InstanceMethods)
            {
                sb.Append($"---@field {m.LuaName} fun(self: {luaName}");
                foreach (var (p, idx) in m.Parameters.Select((e, idx) => (e, idx)))
                {
                    sb.Append($", {ParamName(p, idx)}: {ToLuaTypeName(p.Type)}");
                }

                sb.Append(")");
                if (!m.IsVoid)
                {
                    sb.Append($": {ToLuaTypeName(m.ReturnType)}");
                }

                sb.AppendLine();
            }
        }

        sb.AppendLine();
    }

    private void GenerateClassAnnotation(IndentedStringBuilder sb)
    {
        var luaName = type.LuaName;

        sb.AppendLine($"{luaName} = {{}}");

        sb.AppendLine();

        if (type is LuaTypeMetadata luaTypeMetadata)
        {
            // Static properties and fields
            foreach (var prop in luaTypeMetadata.StaticProperties.Where(p => p.HasGetter))
            {
                sb.AppendLine($"---@type {ToLuaTypeName(prop.PropertyType)}");
                sb.AppendLine($"{luaName}.{prop.LuaName} = nil");
            }

            foreach (var field in luaTypeMetadata.StaticFields)
            {
                sb.AppendLine($"---@type {ToLuaTypeName(field.FieldType)}");
                sb.AppendLine($"{luaName}.{field.LuaName} = nil");
            }

            // Constructors
            GenerateConstructorStubs(sb, luaTypeMetadata);

            // Static methods
            foreach (var m in luaTypeMetadata.StaticMethods)
            {
                sb.AppendLine();
                foreach (var (p, idx) in m.Parameters.Select((e, idx) => (e, idx)))
                    sb.AppendLine($"---@param {ParamName(p, idx)} {ToLuaTypeName(p.Type)}");
                if (m.ReturnTypeName != "void")
                    sb.AppendLine($"---@return {ToLuaTypeName(m.ReturnType)}");
                var paramStr = string.Join(", ", m.Parameters.Select(ParamName));
                sb.AppendLine($"function {luaName}.{m.LuaName}({paramStr}) end");
            }
        }
    }

    private static void GenerateConstructorStubs(IndentedStringBuilder sb, LuaTypeMetadata luaTypeMetadata)
    {
        var luaName = luaTypeMetadata.LuaName;

        if (luaTypeMetadata.IsStatic || luaTypeMetadata.IsInterface) return;

        if (luaTypeMetadata.Constructors.Length == 0)
        {
            // Default parameterless constructor for classes/structs
            sb.AppendLine();
            sb.AppendLine($"---Creates a new {luaName}");
            sb.AppendLine($"---@return {luaName}");
            sb.AppendLine($"function {luaName}.new() end");
            return;
        }

        foreach (var ctor in luaTypeMetadata.Constructors)
        {
            sb.AppendLine();
            sb.AppendLine($"---Creates a new {luaName}");
            foreach (var (p, idx) in ctor.Parameters.Select((e, idx) => (e, idx)))
                sb.AppendLine($"---@param {ParamName(p, idx)} {ToLuaTypeName(p.Type)}");
            sb.AppendLine($"---@return {luaName}");
            var paramStr = string.Join(", ", ctor.Parameters.Select(ParamName));
            sb.AppendLine($"function {luaName}.new({paramStr}) end");
        }
    }

    // ==================================================================
    // Type name conversion helpers
    // ==================================================================

    private static string ToLuaTypeName(BaseLuaTypeMetadata t)
    {
        if (t.SpecialType is SpecialType.System_Boolean) return "boolean";
        if (t.SpecialType is SpecialType.System_Char) return "integer";
        if (t.SpecialType is SpecialType.System_SByte) return "integer";
        if (t.SpecialType is SpecialType.System_Byte) return "integer";
        if (t.SpecialType is SpecialType.System_Int16) return "integer";
        if (t.SpecialType is SpecialType.System_UInt16) return "integer";
        if (t.SpecialType is SpecialType.System_Int32) return "integer";
        if (t.SpecialType is SpecialType.System_UInt32) return "integer";
        if (t.SpecialType is SpecialType.System_Int64) return "integer";
        if (t.SpecialType is SpecialType.System_UInt64) return "integer";
        if (t.SpecialType is SpecialType.System_Decimal) return "number";
        if (t.SpecialType is SpecialType.System_Single) return "number";
        if (t.SpecialType is SpecialType.System_Double) return "number";
        if (t.SpecialType is SpecialType.System_String) return "string";
        if (t.SpecialType is SpecialType.System_Void) return "void";
        if (t.IsFixed64) return "fixed64";
        if (t.IsFixed64AngleSingle) return "f64angle";
        if (t.IsFixed64Euler) return "f64euler";
        if (t.IsFixed64Vector3) return "fixed64vector3";
        
        if (t.IsNullableValueType) return $"{ToLuaTypeName(t.NullableUnderlyingType!)}|nil";

        var suff = t.IsNullableReferenceType ? "|nil" : "";

        if (t.IsArray)
        {
            return $"{{ [integer]: {ToLuaTypeName(t.IEnumerableType)} }}{suff}";
        }
        if (t.IsInlineArray)
        {
            return $"{{ [integer]: {ToLuaTypeName(t.InlineArrayElementType)} }}{suff}";
        }

        if (t.FullTypeName == "global::Lua.LuaTable") return $"table{suff}";
        if (t.FullTypeName == "global::Lua.LuaFunction") return $"function{suff}";
        if (t.FullTypeName == "global::Lua.LuaValue") return $"any{suff}";

        return t.LuaName + suff;
    }

    private static string ParamName(LuaParameterMetadata p, int idx) =>
        p.Name ?? $"arg{idx}";
}
