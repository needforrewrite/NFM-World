using System.Text;

namespace NFMWorld.LuaSourceGenerator;

public class LuaTypeStubsGenerator(LuaVisibleType type, DiscoveredKind kind)
{
    public string GenerateCode()
    {
        var sb = new StringBuilder();

        var luaName = type.LuaName;
        var isStruct = type.IsStruct && !type.Type.IsPrimitive && !type.Type.IsEnum;
        var isArray = type.IsArray;

        // Generate class instance annotation
        var baseTypes = type.Type.GetInterfaces()
            .Where(i => i != type.BaseType && i != typeof(IDisposable))
            .Select(t => $"{t.GetSafeTypeName()}Instance");

        if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            var baseTypeName = type.BaseType.GetSafeTypeName();
            baseTypes = baseTypes.Prepend($"{baseTypeName}Instance");
        }

        if (baseTypes.Any())
        {
            sb.AppendLine($"---@class {luaName}Instance : {string.Join(", ", baseTypes)}");
        }
        else
        {
            sb.AppendLine($"---@class {luaName}Instance");
        }

        if (type.IsVisibleToLua)
        {
            // Generate field annotations for properties and fields
            foreach (var prop in type.InstanceProperties)
            {
                var propLuaName = prop.LuaName;
                var propType = prop.PropertyType.GetLuaStubTypeName();
                if (prop.HasGetter && prop is LuaVisibleIndexer)
                {
                    sb.AppendLine($"---@field {propLuaName} {propType}");
                }
            }

            if (type.InstanceIndexer is { } indexer)
            {
                var propType = indexer.PropertyType.GetLuaStubTypeName();
                var indexParameters = indexer.IndexParameters;
                if (indexParameters.Length == 1)
                {
                    sb.AppendLine($"---@field [{indexParameters[0].ParameterType.GetLuaStubTypeName()}] {propType}");
                }
                else
                {
                    var indexTypes = string.Join(", ",
                        indexParameters.Select(p => p.ParameterType.GetLuaStubTypeName()));
                    sb.AppendLine($"---@field [[{indexTypes}]] {propType}");
                }
            }

            foreach (var field in type.InstanceFields)
            {
                var fieldLuaName = field.LuaName;
                var fieldType = field.FieldType.GetLuaStubTypeName();
                sb.AppendLine($"---@field {fieldLuaName} {fieldType}");
            }

            if (isArray)
            {
                var elementType = type.ElementType!;
                var elementTypeName = elementType.GetLuaStubTypeName();
                sb.AppendLine($"---@field [integer] {elementTypeName}");
            }
        }

        sb.AppendLine($"{luaName}Instance = {{}}");
        sb.AppendLine($"{luaName} = {{}}");
        sb.AppendLine();

        // Generate class annotation
        if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            var baseTypeName = type.BaseType.GetSafeTypeName();
            sb.AppendLine($"---@class (exact) {luaName} : {baseTypeName}");
        }
        else
        {
            sb.AppendLine($"---@class (exact) {luaName}");
        }

        if (type.IsVisibleToLua)
        {
            // Generate static property accessor stubs
            foreach (var prop in type.StaticProperties)
            {
                var propLuaName = prop.LuaName;
                var propType = prop.PropertyType.GetLuaStubTypeName();

                if (prop.HasGetter)
                {
                    sb.AppendLine($"---@field {propLuaName} {propType}");
                }
            }

            if (type.StaticIndexer is { } staticIndexer)
            {
                var propType = staticIndexer.PropertyType.GetLuaStubTypeName();
                var indexParameters = staticIndexer.IndexParameters;
                if (indexParameters.Length == 1)
                {
                    sb.AppendLine($"---@field [{indexParameters[0].ParameterType.GetLuaStubTypeName()}] {propType}");
                }
                else
                {
                    var indexTypes = string.Join(", ",
                        indexParameters.Select(p => p.ParameterType.GetLuaStubTypeName()));
                    sb.AppendLine($"---@field [[{indexTypes}]] {propType}");
                }
            }

            // Generate constructor stub
            if (!isArray)
            {
                var constructors = type.Constructors;

                if (constructors.Length > 0 || isStruct)
                {
                    if (isStruct)
                    {
                        sb.AppendLine($"---Creates a new {luaName}");
                        sb.AppendLine($"---@return {luaName}Instance");
                        sb.AppendLine($"function {luaName}.new() end");
                        sb.AppendLine();
                    }

                    foreach (var ctor in constructors)
                    {
                        var parameters = ctor.Parameters;

                        sb.AppendLine($"---Creates a new {luaName}");
                        foreach (var param in parameters)
                        {
                            var paramType = param.ParameterType.GetLuaStubTypeName();
                            var paramName = param.Name ?? $"param{param.Position}";
                            var optional = param.ParameterType.IsNullable() ? "?" : "";
                            sb.AppendLine($"---@param {paramName}{optional} {paramType}");
                        }

                        sb.AppendLine($"---@return {type.Type.GetLuaStubTypeName()}");

                        var paramNames = string.Join(", ", parameters.Select(p => p.Name ?? $"param{p.Position}"));
                        sb.AppendLine($"function {luaName}.new({paramNames}) end");
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                // Array constructor - generate based on rank
                var rank = type.ArrayRank!.Value;
                sb.AppendLine($"---Creates a new {luaName}");

                if (rank == 1)
                {
                    sb.AppendLine($"---@param length integer");
                }
                else
                {
                    for (int i = 0; i < rank; i++)
                    {
                        sb.AppendLine($"---@param dim{i} integer");
                    }
                }

                sb.AppendLine($"---@return {type.Type.GetLuaStubTypeName()}");

                if (rank == 1)
                {
                    sb.AppendLine($"function {luaName}.new(length) end");
                }
                else
                {
                    var paramList = string.Join(", ", Enumerable.Range(0, rank).Select(i => $"dim{i}"));
                    sb.AppendLine($"function {luaName}.new({paramList}) end");
                }

                sb.AppendLine();
            }

            // Generate static method stubs
            var staticMethods = type.StaticMethods;

            foreach (var method in staticMethods)
            {
                var luaMethodName = method.LuaName;
                var parameters = method.Parameters;

                foreach (var param in parameters)
                {
                    var paramType = param.ParameterType.GetLuaStubTypeName();
                    var paramName = param.Name ?? $"param{param.Position}";
                    var optional = param.ParameterType.IsNullable() ? "?" : "";
                    sb.AppendLine($"---@param {paramName}{optional} {paramType}");
                }

                if (method.ReturnType != typeof(void))
                {
                    var returnType = method.ReturnType.GetLuaStubTypeName();
                    sb.AppendLine($"---@return {returnType}");
                }

                var paramNames = string.Join(", ", parameters.Select(p => p.Name ?? $"param{p.Position}"));
                sb.AppendLine($"function {luaName}.{luaMethodName}({paramNames}) end");
                sb.AppendLine();
            }

            // Generate instance method stubs (including extension methods)
            var instanceMethods = type.InstanceMethods;

            foreach (var method in instanceMethods)
            {
                var luaMethodName = method.LuaName;
                var isExtension = method.IsExtensionMethod;
                var parameters = method.Parameters;

                // For extension methods, skip the first parameter (this)
                var paramsToDocument = isExtension ? parameters.Skip(1).ToArray() : parameters;

                sb.AppendLine($"---@param self {type.Type.GetLuaStubTypeName()}");
                foreach (var param in paramsToDocument)
                {
                    var paramType = param.ParameterType.GetLuaStubTypeName();
                    var paramName = param.Name ?? $"param{param.Position}";
                    var optional = param.ParameterType.IsNullable() ? "?" : "";
                    sb.AppendLine($"---@param {paramName}{optional} {paramType}");
                }

                if (method.ReturnType != typeof(void))
                {
                    var returnType = method.ReturnType.GetLuaStubTypeName();
                    sb.AppendLine($"---@return {returnType}");
                }

                var paramNames = string.Join(", ", paramsToDocument.Select(p => p.Name ?? $"param{p.Position}"));
                sb.AppendLine($"function {luaName}Instance:{luaMethodName}({paramNames}) end");
                sb.AppendLine();
            }

            // Generate event stubs (instance events)
            var instanceEvents = type.InstanceEvents;

            foreach (var evt in instanceEvents)
            {
                var eventName = evt.Name;
                var delegateType = evt.EventHandlerType!;
                var invokeMethod = delegateType.GetMethod("Invoke")!;
                var parameters = invokeMethod.GetParameters();

                // Generate add stub
                sb.AppendLine($"---@param self {type.Type.GetLuaStubTypeName()}");
                sb.AppendLine($"---@param callback fun(");
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramType = param.ParameterType.GetLuaStubTypeName();
                    var paramName = param.Name ?? $"arg{i}";
                    var separator = i < parameters.Length - 1 ? ", " : "";
                    sb.AppendLine($"---    {paramName}: {paramType}{separator}");
                }

                sb.AppendLine($"---)");
                sb.AppendLine($"function {luaName}Instance:add_{eventName}(callback) end");
                sb.AppendLine();

                // Generate remove stub
                sb.AppendLine($"---@param self {type.Type.GetLuaStubTypeName()}");
                sb.AppendLine($"function {luaName}Instance:remove_{eventName}() end");
                sb.AppendLine();
            }

            // Generate event stubs (static events)
            var staticEvents = type.StaticEvents;

            foreach (var evt in staticEvents)
            {
                var eventName = evt.Name;
                var delegateType = evt.EventHandlerType!;
                var invokeMethod = delegateType.GetMethod("Invoke")!;
                var parameters = invokeMethod.GetParameters();

                // Generate add stub
                sb.AppendLine($"---@param callback fun(");
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramType = param.ParameterType.GetLuaStubTypeName();
                    var paramName = param.Name ?? $"arg{i}";
                    var separator = i < parameters.Length - 1 ? ", " : "";
                    sb.AppendLine($"---    {paramName}: {paramType}{separator}");
                }

                sb.AppendLine($"---)");
                sb.AppendLine($"function {luaName}.add_{eventName}(callback) end");
                sb.AppendLine();

                // Generate remove stub
                sb.AppendLine($"function {luaName}.remove_{eventName}() end");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}