using System.Text;

namespace NFMWorld.LuaSourceGenerator;

public class TypeScriptTypeStubsGenerator(LuaVisibleType type, DiscoveredKind kind)
{
    public string GenerateCode()
    {
        var sb = new IndentedStringBuilder();

        var luaName = type.LuaName;
        var isStruct = type.IsStruct && !type.Type.IsPrimitive && !type.Type.IsEnum;
        var isArray = type.IsArray;

        if (isArray)
        {
            // Generate array type
            var elementType = type.ElementType!;
            var rank = type.ArrayRank!.Value;
            var tsElementType = elementType.GetTypeScriptTypeName();

            sb.AppendLine($"declare class {luaName}");
            using (sb.Block())
            {
                if (rank == 1)
                {
                    sb.AppendLine("/** @customName new */");
                    sb.AppendLine($"static inst(length: number): {luaName};");
                }
                else
                {
                    sb.AppendLine("/** @customName new */");
                    var dims = string.Join(", ", Enumerable.Range(0, rank).Select(i => $"dim{i}: number"));
                    sb.AppendLine($"static inst({dims}): {luaName};");
                }

                if (rank == 1)
                {
                    sb.AppendLine($"[index: number]: {tsElementType};");
                    sb.AppendLine($"readonly length: number;");
                }
                else
                {
                    sb.AppendLine($"[index: [{string.Join(", ", Enumerable.Range(0, rank).Select(_ => "number"))}]]: {tsElementType};");
                    sb.AppendLine($"readonly length: number;");
                }
            }
            return sb.ToString();
        }

        // Generate class instance annotation
        var interfaces = type.Type.GetInterfaces()
            .Where(i => i != type.BaseType && i != typeof(IDisposable))
            .Select(t => t.GetSafeTypeName())
            .ToArray();
        
        sb.AppendLine($"declare {(type.Type.IsInterface ? "interface" : "class")} {luaName}");
        using (sb.Indent())
        {
            if (!type.Type.IsInterface)
            {
                if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
                {
                    sb.AppendLine("extends " + type.BaseType.GetSafeTypeName());
                    
                    if (interfaces.Length > 0)
                    {
                        sb.AppendLine(" implements " + string.Join(", ", interfaces));
                    }
                }
                else
                {
                    if (interfaces.Length > 0)
                    {
                        sb.AppendLine("implements " + string.Join(", ", interfaces));
                    }
                }
            }
            else
            {
                if (interfaces.Length > 0)
                {
                    sb.AppendLine("extends " + string.Join(", ", interfaces));
                }
            }
        }

        using (sb.Block())
        {
            if (type.IsVisibleToLua)
            {
                // Generate field annotations for properties and fields
                foreach (var prop in type.InstanceProperties)
                {
                    if (prop.HasGetter)
                    {
                        sb.AppendLine($"{(prop.HasSetter ? "" : "readonly ")}{prop.LuaName}: {prop.PropertyType.GetTypeScriptTypeName()};");
                    }
                }
                
                foreach (var field in type.InstanceFields)
                {
                    sb.AppendLine($"{(!field.IsReadOnly ? "" : "readonly ")}{field.LuaName}: {field.FieldType.GetTypeScriptTypeName()};");
                }
                
                if (type.InstanceIndexer is {} indexer)
                {
                    var indexParams = string.Join(", ", indexer.IndexParameters
                        .Select(p => $"{p.Name}: {p.ParameterType.GetTypeScriptTypeName()}"));
                    sb.AppendLine($"{(indexer.HasSetter ? "" : "readonly ")}[index: [{indexParams}]]: {indexer.PropertyType.GetTypeScriptTypeName()};");
                }
                
                // Generate field annotations for static properties and fields
                foreach (var prop in type.StaticProperties)
                {
                    if (prop.HasGetter)
                    {
                        sb.AppendLine($"static {(prop.HasSetter ? "" : "readonly ")}{prop.LuaName}: {prop.PropertyType.GetTypeScriptTypeName()};");
                    }
                }
                
                foreach (var field in type.StaticFields)
                {
                    sb.AppendLine($"static {(!field.IsReadOnly ? "" : "readonly ")}{field.LuaName}: {field.FieldType.GetTypeScriptTypeName()};");
                }
                
                if (type.StaticIndexer is {} staticIndexer)
                {
                    var indexParams = string.Join(", ", staticIndexer.IndexParameters
                        .Select(p => $"{p.Name}: {p.ParameterType.GetTypeScriptTypeName()}"));
                    sb.AppendLine($"static {(staticIndexer.HasSetter ? "" : "readonly ")}[index: [{indexParams}]]: {staticIndexer.PropertyType.GetTypeScriptTypeName()};");
                }
                
                // Methods
                
                foreach (var method in type.InstanceMethods)
                {
                    var luaMethodName = method.LuaName;
                    var isExtension = method.IsExtensionMethod;
                    var methodParams = method.Parameters;

                    // For extension methods, skip the first parameter (this)
                    var paramsToDocument = isExtension ? methodParams.Skip(1) : methodParams;

                    var parameters = paramsToDocument
                        .Select(p => $"{p.Name ?? "arg"}: {p.ParameterType.GetTypeScriptTypeName()}")
                        .ToList();
                    var paramStr = string.Join(", ", parameters);
                    var returnType = method.ReturnType.GetTypeScriptTypeName();
                    sb.AppendLine($"{luaMethodName}({paramStr}): {returnType};");
                }
                
                foreach (var method in type.StaticMethods)
                {
                    var luaMethodName = method.LuaName;
                    var isExtension = method.IsExtensionMethod;
                    var methodParams = method.Parameters;

                    // For extension methods, skip the first parameter (this)
                    var paramsToDocument = isExtension ? methodParams.Skip(1) : methodParams;

                    var parameters = paramsToDocument
                        .Select(p => $"{p.Name ?? "arg"}: {p.ParameterType.GetTypeScriptTypeName()}")
                        .ToArray();
                    var paramStr = string.Join(", ", parameters);
                    var returnType = method.ReturnType.GetTypeScriptTypeName();
                    sb.AppendLine($"static {luaMethodName}(this: void, {paramStr}): {returnType};");
                }
                
                // Events
                
                foreach (var evt in type.InstanceEvents)
                {
                    var eventName = evt.LuaName;
                    var parameters = evt.Parameters
                        .Select(p => $"{p.Name ?? "arg"}: {p.ParameterType.GetTypeScriptTypeName()}")
                        .ToArray();
                    var callbackType = $"({string.Join(", ", parameters)}) => {evt.ReturnType.GetTypeScriptTypeName()}";

                    sb.AppendLine($"add_{eventName}(callback: {callbackType}): void;");
                    sb.AppendLine($"remove_{eventName}(callback: {callbackType}): void;");
                }
                
                foreach (var evt in type.StaticEvents)
                {
                    var eventName = evt.LuaName;
                    var parameters = evt.Parameters
                        .Select(p => $"{p.Name ?? "arg"}: {p.ParameterType.GetTypeScriptTypeName()}")
                        .ToArray();
                    var callbackType = $"({string.Join(", ", parameters)}) => {evt.ReturnType.GetTypeScriptTypeName()}";

                    sb.AppendLine($"static add_{eventName}(this: void, callback: {callbackType}): void;");
                    sb.AppendLine($"static remove_{eventName}(this: void, callback: {callbackType}): void;");
                }
            }
        }

        return sb.ToString();
    }
}