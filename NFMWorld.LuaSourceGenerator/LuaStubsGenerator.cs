// using System.Text;
// using Microsoft.CodeAnalysis;
//
// namespace NFMWorld.LuaSourceGenerator;
//
// /// <summary>
// /// Generates Lua Language Server (LuaLS) annotation stubs for [LuaVisible] types.
// /// Outputs ---@class, ---@field, ---@param, ---@return annotations for IDE autocomplete.
// /// </summary>
// internal sealed class LuaStubsGenerator(LuaTypeMetadata type, Compilation compilation, Dictionary<string, string> luaVisibleNameMap)
// {
//     private readonly Dictionary<string, string> _luaVisibleNameMap = luaVisibleNameMap;
//     public string GenerateCode()
//     {
//         var sb = new StringBuilder();
//         var luaName = type.LuaName;
//
//         // Instance annotation (for objects created via .new())
//         GenerateInstanceClass(sb);
//
//         // Static/class annotation (for TypeTable access)
//         GenerateClassAnnotation(sb);
//
//         return sb.ToString();
//     }
//
//     private void GenerateInstanceClass(StringBuilder sb)
//     {
//         var luaName = type.LuaName;
//
//         // Build base type list for ---@class, filtering self-references and ILuaUserData
//         var baseTypes = new List<string>();
//         if (type.BaseTypeFullName != null)
//         {
//             var baseStub = StubTypeName(type.BaseTypeFullName);
//             if (baseStub != luaName)
//                 baseTypes.Add($"{baseStub}Instance");
//         }
//         foreach (var iface in type.InterfaceFullNames)
//         {
//             var ifaceStub = StubTypeName(iface);
//             if (ifaceStub != luaName && ifaceStub != "ILuaUserData")
//                 baseTypes.Add($"{ifaceStub}Instance");
//         }
//
//         if (baseTypes.Count > 0)
//             sb.AppendLine($"---@class {luaName}Instance : {string.Join(", ", baseTypes)}");
//         else
//             sb.AppendLine($"---@class {luaName}Instance");
//
//         // Fields and properties
//         foreach (var prop in type.InstanceProperties.Where(p => p.HasGetter))
//             sb.AppendLine($"---@field {prop.LuaName} {ToLuaTypeName(prop.PropertyType)}");
//
//         foreach (var field in type.InstanceFields)
//             sb.AppendLine($"---@field {field.LuaName} {ToLuaTypeName(field.FieldType)}");
//
//         sb.AppendLine($"{luaName}Instance = {{}}");
//         sb.AppendLine();
//     }
//
//     private void GenerateClassAnnotation(StringBuilder sb)
//     {
//         var luaName = type.LuaName;
//
//         // Class annotation with base type (filter self-references)
//         if (type.BaseTypeFullName != null)
//         {
//             var baseStub = StubTypeName(type.BaseTypeFullName);
//             if (baseStub != luaName)
//                 sb.AppendLine($"---@class (exact) {luaName} : {baseStub}");
//             else
//                 sb.AppendLine($"---@class (exact) {luaName}");
//         }
//         else
//             sb.AppendLine($"---@class (exact) {luaName}");
//
//         // Static properties and fields
//         foreach (var prop in type.StaticProperties.Where(p => p.HasGetter))
//             sb.AppendLine($"---@field {prop.LuaName} {ToLuaTypeName(prop.PropertyType)}");
//
//         foreach (var field in type.StaticFields)
//             sb.AppendLine($"---@field {field.LuaName} {ToLuaTypeName(field.FieldType)}");
//
//         sb.AppendLine();
//         sb.AppendLine($"{luaName} = {{}}");
//
//         // Constructors
//         GenerateConstructorStubs(sb);
//
//         // Static methods
//         foreach (var m in type.StaticMethods)
//         {
//             sb.AppendLine();
//             foreach (var p in m.Parameters)
//                 sb.AppendLine($"---@param {ParamName(p)} {ToLuaTypeName(p.TypeName)}");
//             if (m.ReturnTypeName != "void")
//                 sb.AppendLine($"---@return {ToLuaTypeName(m.ReturnTypeName)}");
//             var paramStr = string.Join(", ", m.Parameters.Select(ParamName));
//             sb.AppendLine($"function {luaName}.{m.FullLuaName}({paramStr}) end");
//         }
//
//         // Instance methods
//         foreach (var m in type.InstanceMethods)
//         {
//             sb.AppendLine();
//             sb.AppendLine($"---@param self {luaName}Instance");
//             // For extension methods, skip first param (this)
//             var docParams = m.IsExtension ? m.Parameters.Skip(1).ToArray() : m.Parameters;
//             foreach (var p in docParams)
//                 sb.AppendLine($"---@param {ParamName(p)} {ToLuaTypeName(p.TypeName)}");
//             if (m.ReturnTypeName != "void")
//                 sb.AppendLine($"---@return {ToLuaTypeName(m.ReturnTypeName)}");
//             var paramStr = string.Join(", ", docParams.Select(ParamName));
//             sb.AppendLine($"function {luaName}Instance:{m.FullLuaName}({paramStr}) end");
//         }
//
//         // Instance events
//         foreach (var evt in type.InstanceEvents)
//         {
//             var sig = GetEventDelegateSignature(evt);
//             sb.AppendLine();
//             sb.AppendLine($"---@param self {luaName}Instance");
//             sb.AppendLine($"---@param callback fun({sig})");
//             sb.AppendLine($"function {luaName}Instance:add_{evt.LuaName}(callback) end");
//             sb.AppendLine();
//             sb.AppendLine($"---@param self {luaName}Instance");
//             sb.AppendLine($"function {luaName}Instance:remove_{evt.LuaName}() end");
//         }
//
//         // Static events
//         foreach (var evt in type.StaticEvents)
//         {
//             var sig = GetEventDelegateSignature(evt);
//             sb.AppendLine();
//             sb.AppendLine($"---@param callback fun({sig})");
//             sb.AppendLine($"function {luaName}.add_{evt.LuaName}(callback) end");
//             sb.AppendLine();
//             sb.AppendLine($"function {luaName}.remove_{evt.LuaName}() end");
//         }
//     }
//
//     private void GenerateConstructorStubs(StringBuilder sb)
//     {
//         var luaName = type.LuaName;
//
//         if (type.IsStatic || type.IsInterface) return;
//
//         if (type.Constructors.Length == 0)
//         {
//             // Default parameterless constructor for classes/structs
//             sb.AppendLine();
//             sb.AppendLine($"---Creates a new {luaName}");
//             sb.AppendLine($"---@return {luaName}Instance");
//             sb.AppendLine($"function {luaName}.new() end");
//             return;
//         }
//
//         foreach (var ctor in type.Constructors)
//         {
//             sb.AppendLine();
//             sb.AppendLine($"---Creates a new {luaName}");
//             foreach (var p in ctor.Parameters)
//                 sb.AppendLine($"---@param {ParamName(p)} {ToLuaTypeName(p.TypeName)}");
//             sb.AppendLine($"---@return {luaName}Instance");
//             var paramStr = string.Join(", ", ctor.Parameters.Select(ParamName));
//             sb.AppendLine($"function {luaName}.{ctor.FullLuaNew}({paramStr}) end");
//         }
//     }
//
//     private string GetEventDelegateSignature(LuaEventMetadata evt)
//     {
//         // Try to resolve the delegate type and get its Invoke parameters
//         var handlerType = compilation.GetTypeByMetadataName(evt.HandlerType);
//         if (handlerType == null)
//         {
//             // Fallback: try to get the type from the event symbol's original type
//             return "...";
//         }
//
//         var invoke = handlerType.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();
//         if (invoke == null) return "...";
//
//         var paramStrs = new List<string>();
//         for (int i = 0; i < invoke.Parameters.Length; i++)
//         {
//             var p = invoke.Parameters[i];
//             var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
//             paramStrs.Add($"{ParamName(p)}: {ToLuaTypeName(typeName)}");
//         }
//         return string.Join(", ", paramStrs);
//     }
//
//     // ==================================================================
//     // Type name conversion helpers
//     // ==================================================================
//
//     private string ToLuaTypeName(string t)
//     {
//         if (t.EndsWith("?")) return $"{ToLuaTypeName(t.Substring(0, t.Length - 1))}|nil";
//         // Handle 1D arrays: int[] → { [integer]: number }
//         if (t.EndsWith("[]") && !t.EndsWith("[,]") && !t.Contains("["))
//         {
//             // t.EndsWith("[]") but check it's a simple 1D array, not multi-dim
//             var elemType = t.Substring(0, t.Length - 2);
//             return $"{{ [integer]: {ToLuaTypeName(elemType)} }}";
//         }
//         return t switch
//         {
//             "int" or "long" or "float" or "double" or "byte" or "sbyte"
//                 or "short" or "ushort" or "uint" or "ulong" or "decimal" => "number",
//             "bool" => "boolean",
//             "string" => "string",
//             "void" => "nil",
//             "object" => "any",
//             // Map Lua-CSharp base types to native Lua types
//             "Lua.LuaTable" => "table",
//             "Lua.LuaFunction" => "function",
//             "Lua.LuaValue" => "any",
//             _ => IsFixedMathType(t) ? FixedMathToLuaName(t) : $"{StubTypeName(t)}Instance"
//         };
//     }
//
//     private static string FixedMathToLuaName(string t)
//     {
//         var baseT = t.Contains('<') ? t.Substring(0, t.IndexOf('<')) : t;
//         // Handle namespace-qualified names (FixedMathSharp.Fixed64) and bare names (Fixed64)
//         if (baseT == "Fixed64" || baseT.EndsWith(".Fixed64")) return "fixed64";
//         if (baseT == "Vector3d" || baseT.EndsWith(".Vector3d")) return "fixed64vector3";
//         if (baseT == "f64AngleSingle" || baseT.EndsWith(".f64AngleSingle")) return "f64angle";
//         if (baseT == "f64Euler" || baseT.EndsWith(".f64Euler")) return "f64euler";
//         return ExtractSimpleName(baseT);
//     }
//
//     private static bool IsFixedMathType(string t)
//     {
//         var baseT = t.Contains('<') ? t.Substring(0, t.IndexOf('<')) : t;
//         return baseT is "Fixed64" or "Vector3d" or "f64AngleSingle" or "f64Euler" or "Fixed4x4"
//             || baseT.EndsWith(".Fixed64") || baseT.EndsWith(".Vector3d")
//             || baseT.EndsWith(".f64AngleSingle") || baseT.EndsWith(".f64Euler")
//             || baseT.EndsWith(".Fixed4x4");
//     }
//
//     /// <summary>Short Lua-friendly type name for stub annotations. Uses LuaName for [LuaVisible] types, sanitized full name otherwise.</summary>
//     private string StubTypeName(string fullName)
//     {
//         // Check if this is a [LuaVisible] type — use its LuaName
//         if (_luaVisibleNameMap.TryGetValue(fullName, out var luaName))
//             return luaName;
//         // Handle tuples
//         if (fullName.StartsWith("(")) return "ValueTuple";
//         // Handle arrays: int[,,] → intArray, int[] → intArray
//         if (fullName.EndsWith("]"))
//         {
//             var bracketIdx = fullName.IndexOf('[');
//             var elemName = bracketIdx >= 0 ? StubTypeName(fullName.Substring(0, bracketIdx)) : fullName;
//             return elemName + "Array";
//         }
//         // For non-LuaVisible types, use sanitized full name (matching external type stub names)
//         var sanitized = SanitizeForStub(fullName);
//         return sanitized;
//     }
//
//     /// <summary>Sanitize a full type name to match external stub file naming convention.</summary>
//     private static string SanitizeForStub(string fullName)
//     {
//         // Include generic args: UnlimitedArray<string> → UnlimitedArray_string_
//         var name = fullName.Contains('<') ? fullName.Replace('<', '_').Replace('>', '_').Replace(", ", "_").Replace(",", "_") : fullName;
//         name = name.Replace("global::", "").Replace(".", "_").Replace("[]", "Array");
//         return name;
//     }
//
//     private static string ExtractSimpleName(string fullName)
//     {
//         var lastDot = fullName.LastIndexOf('.');
//         return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
//     }
//
//     private static string ParamName(LuaParameterMetadata p) =>
//         p.Name ?? $"arg{p.Name.GetHashCode() & 0xFFFF}";
//
//     private static string ParamName(IParameterSymbol p) =>
//         p.Name ?? $"arg{p.Ordinal}";
// }
