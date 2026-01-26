using System.Reflection;
using System.Runtime.CompilerServices;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator;

public record LuaVisibleProperty(PropertyInfo Property, bool IsExtensionProperty)
{
    public string LuaName => Property.GetCustomAttribute<LuaNameAttribute>()?.Name ?? Property.Name.ToCamelCase();
    public Type PropertyType => Property.PropertyType;
    public bool HasGetter => Property.GetGetMethod() is {} getMethod &&
                             getMethod.GetCustomAttribute<LuaHiddenAttribute>() is null &&
                             getMethod.IsPublic;
    public bool HasSetter => Property.GetSetMethod() is {} setMethod &&
                             setMethod.GetCustomAttribute<LuaHiddenAttribute>() is null &&
                             setMethod.IsPublic;
    public Type? DeclaringType => Property.DeclaringType;
    public string Name => Property.Name;
}

public record LuaVisibleIndexer(PropertyInfo Property) : LuaVisibleProperty(Property, false)
{
    public ParameterInfo[] IndexParameters => Property.GetIndexParameters();
}

public record LuaVisibleMethod(MethodBase Method, bool IsExtensionMethod)
{
    public string LuaName => Method.GetCustomAttribute<LuaNameAttribute>()?.Name ?? Method.Name.ToCamelCase();
    public ParameterInfo[] Parameters => Method.GetParameters();
    public Type ReturnType => Method.ReturnType;
    public virtual string BindingName => Method.IsStatic
        ? $"{DeclaringType?.GetGenericTypeLuaName()}_staticmethod_{Method.Name}"
        : $"{DeclaringType?.GetGenericTypeLuaName()}_method_{Method.Name}";

    public string Name => Method.Name;
    public Type? DeclaringType => Method.DeclaringType;
    public bool IsReadOnlyStructMethod => DeclaringType?.IsValueType == true &&
                                           Method.GetCustomAttribute<IsReadOnlyAttribute>() is not null;
}

public record LuaVisibleField(FieldInfo Field)
{
    public string LuaName => Field.GetCustomAttribute<LuaNameAttribute>()?.Name ?? Field.Name.ToCamelCase();
    public Type FieldType => Field.FieldType;
    public Type? DeclaringType => Field.DeclaringType;
    public string Name => Field.Name;
    public bool IsReadOnly => Field.IsInitOnly;
}

public record LuaVisibleConstructor(ConstructorInfo Constructor) : LuaVisibleMethod(Constructor, false)
{
    public override string BindingName => $"{Constructor.DeclaringType?.GetGenericTypeLuaName()}_new";
}

public record LuaVisibleEvent(EventInfo Event)
{
    public string LuaName => Event.GetCustomAttribute<LuaNameAttribute>()?.Name ?? Event.Name.ToCamelCase();
    public Type EventHandlerType => Event.EventHandlerType!;
    public MethodInfo InvokeMethod => EventHandlerType.GetMethod("Invoke")!;
    public Type[] ParameterTypes => InvokeMethod.GetParameters().Select(p => p.ParameterType).ToArray();
    public Type ReturnType => InvokeMethod.ReturnType;
    public Type? DeclaringType => Event.DeclaringType;
}

public record LuaVisibleOperator(MethodBase Method) : LuaVisibleMethod(Method, false)
{
    public override string BindingName => $"{DeclaringType?.GetGenericTypeLuaName()}_{Method.Name}";
}

public class LuaVisibleType
{
    public bool IsVisibleToLua { get; }
    public string LuaName { get; }
    
    public LuaVisibleMethod[] DeclaredInstanceMethods { get; }
    public LuaVisibleMethod[] InstanceMethods { get; }
    public LuaVisibleField[] InstanceFields { get; }
    public LuaVisibleProperty[] InstanceProperties { get; }
    public LuaVisibleIndexer? InstanceIndexer { get; set; }
    public LuaVisibleEvent[] InstanceEvents { get; }
    
    public LuaVisibleMethod[] StaticMethods { get; }
    public LuaVisibleField[] StaticFields { get; }
    public LuaVisibleProperty[] StaticProperties { get; }
    public LuaVisibleIndexer? StaticIndexer { get; set; }
    public LuaVisibleEvent[] StaticEvents { get; }
    public LuaVisibleConstructor[] Constructors { get; }
    public LuaVisibleOperator[] Operators { get; }

    public Assembly OriginAssembly { get; }
    public Type Type { get; init; }
    public string FullName => Type.FullName ?? Type.Name;
    public bool IsStruct => Type.IsValueType;
    public bool IsNullable => Type.IsNullable();
    public Type? BaseType => Type.BaseType;
    public IEnumerable<Type> InterfaceTypes => Type.GetInterfaces();
    public string MetatableName { get; }
    public bool IsArray => Type.IsArray;
    public int? ArrayRank => Type.IsArray ? Type.GetArrayRank() : null;
    public Type? ElementType => Type.GetElementType();

    public LuaVisibleType(Assembly originAssembly, Type type)
    {
        OriginAssembly = originAssembly;
        Type = type;
        IsVisibleToLua = type.GetCustomAttribute<LuaVisibleAttribute>() is not null
                         || originAssembly.GetCustomAttributes<AssemblyLuaVisibleAttribute>().Any(e => e.Type == type);
        LuaName = type.GetCustomAttribute<LuaVisibleAttribute>()?.Name
                  ?? type.GetCustomAttribute<LuaNameAttribute>()?.Name
                  ?? type.GetGenericTypeLuaName();
        MetatableName = $"MT_{Type.GetSafeTypeName()}";
        DeclaredInstanceMethods = GetDeclaredInstanceMethods(type)
            .Where(m => m.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateMethod(m))
            .Select(m => new LuaVisibleMethod(m, false))
            .ToArray();
        InstanceMethods = IsVisibleToLua
            ? GetMethodsInTypeAndInterfaces(type)
                .Where(m => m.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateMethod(m))
                .Concat(GetExtensionMethods(originAssembly, type))
                .Select(m => new LuaVisibleMethod(m, IsExtensionMethod(m)))
                .ToArray()
            : [];
        InstanceFields = IsVisibleToLua
            ? GetFieldsInTypeAndBaseTypes(type)
                .Where(f => f.GetCustomAttribute<LuaHiddenAttribute>() is null)
                .Select(f => new LuaVisibleField(f))
                .ToArray()
            : [];
        InstanceProperties = IsVisibleToLua
            ? GetPropertiesInTypeAndInterfaces(type)
                .Where(p => p.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateProperty(p))
                .Concat(GetExtensionProperties(originAssembly, type))
                .Select(p => new LuaVisibleProperty(p, IsExtensionMethod(p.GetMethod ?? p.SetMethod!)))
                .ToArray()
            : [];
        InstanceIndexer = IsVisibleToLua
            ? GetPropertiesInTypeAndInterfaces(type)
                .Where(p => p.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateIndexer(p))
                .Select(p => new LuaVisibleIndexer(p))
                .FirstOrDefault()
            : null;
        InstanceEvents = IsVisibleToLua
            ? type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(e => e.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateEvent(e))
                .Select(e => new LuaVisibleEvent(e))
                .ToArray()
            : [];
        StaticMethods = IsVisibleToLua
            ? type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateMethod(m))
                .Select(m => new LuaVisibleMethod(m, false))
                .ToArray()
            : [];
        StaticFields = IsVisibleToLua
            ? type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.GetCustomAttribute<LuaHiddenAttribute>() is null)
                .Select(f => new LuaVisibleField(f))
                .ToArray()
            : [];
        StaticProperties = IsVisibleToLua
            ? type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateProperty(p))
                .Select(p => new LuaVisibleProperty(p, false))
                .ToArray()
            : [];
        StaticIndexer = IsVisibleToLua
            ? type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateIndexer(p))
                .Select(p => new LuaVisibleIndexer(p))
                .FirstOrDefault()
            : null;
        StaticEvents = IsVisibleToLua
            ? type.GetEvents(BindingFlags.Public | BindingFlags.Static)
                .Where(e => e.GetCustomAttribute<LuaHiddenAttribute>() is null && IsCandidateEvent(e))
                .Select(e => new LuaVisibleEvent(e))
                .ToArray()
            : [];
        Constructors = IsVisibleToLua
            ? type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(c => c.GetCustomAttribute<LuaHiddenAttribute>() is null)
                .Select(c => new LuaVisibleConstructor(c))
                .ToArray()
            : [];
        Operators = IsVisibleToLua
            ? type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.IsSpecialName && (m.Name.StartsWith("op_")) &&
                            m.GetCustomAttribute<LuaHiddenAttribute>() is null &&
                            IsCandidateOperator(m))
                .Select(m => new LuaVisibleOperator(m))
                .ToArray()
            : [];
    }

    private static IEnumerable<MethodInfo> GetExtensionMethods(Assembly originAssembly, Type type)
    {
        var extensionMethods = ((Assembly[])[originAssembly, type.Assembly])
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsStaticClass() && t.IsPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m =>
            {
                if (!IsExtensionMethod(m))
                    return false;

                var firstParamType = m.GetParameters()[0].ParameterType;
                if (firstParamType.IsGenericParameter)
                    return false;

                if (m.GetCustomAttribute<LuaHiddenAttribute>() is not null)
                    return false;
                
                if (!IsCandidateMethod(m))
                    return false;

                if (firstParamType.IsAssignableFrom(type))
                    return true;
                
                if (type.IsGenericType && firstParamType.IsGenericTypeDefinition)
                {
                    return type.GetGenericTypeDefinition() == firstParamType;
                }

                return false;
            });

        return extensionMethods;
    }
    
    private static IEnumerable<PropertyInfo> GetExtensionProperties(Assembly originAssembly, Type type)
    {
        var extensionProperties = ((Assembly[])[originAssembly, type.Assembly])
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsStaticClass() && t.IsPublic)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static))
            .Where(p =>
            {
                var getMethod = p.GetGetMethod();
                if (getMethod == null || !IsExtensionMethod(getMethod))
                    return false;

                var firstParamType = getMethod.GetParameters()[0].ParameterType;
                if (firstParamType.IsGenericParameter)
                    return false;

                if (p.GetCustomAttribute<LuaHiddenAttribute>() is not null)
                    return false;
                
                if (!IsCandidateProperty(p))
                    return false;

                if (firstParamType.IsAssignableFrom(type))
                    return true;

                if (type.IsGenericType && firstParamType.IsGenericTypeDefinition)
                {
                    return type.GetGenericTypeDefinition() == firstParamType;
                }

                return false;
            });

        return extensionProperties;
    }

    private static bool IsCandidateProperty(PropertyInfo propertyInfo)
    {
        var getMethod = propertyInfo.GetGetMethod();
        var setMethod = propertyInfo.GetSetMethod();

        if (getMethod != null && !IsCandidateGetterSetter(getMethod))
            return false;

        if (setMethod != null && !IsCandidateGetterSetter(setMethod))
            return false;
        
        if (!IsCandidateType(propertyInfo.PropertyType))
            return false;
        
        if (propertyInfo.GetIndexParameters().Length > 0)
            return false;

        return true;
    }

    private static bool IsCandidateGetterSetter(MethodInfo methodInfo)
    {
        if (methodInfo.GetParameters().Any(p => !IsCandidateType(p.ParameterType)))
            return false;
        
        if (!IsCandidateType(methodInfo.ReturnType))
            return false;
        
        // Exclude generic methods
        if (methodInfo.IsGenericMethod)
            return false;
        
        return true;
    }

    private static bool IsCandidateIndexer(PropertyInfo propertyInfo)
    {
        var getMethod = propertyInfo.GetGetMethod();
        var setMethod = propertyInfo.GetSetMethod();

        if (getMethod != null && !IsCandidateMethod(getMethod))
            return false;

        if (setMethod != null && !IsCandidateMethod(setMethod))
            return false;
        
        if (!IsCandidateType(propertyInfo.PropertyType))
            return false;
        
        if (propertyInfo.GetIndexParameters().Length == 0)
            return false;
        
        if (propertyInfo.GetIndexParameters().Any(p => !IsCandidateType(p.ParameterType)))
            return false;
        
        if (propertyInfo.GetIndexParameters().Any(p => p.ParameterType != typeof(int) && p.ParameterType != typeof(long) && p.ParameterType != typeof(string)))
            return false;

        return true;
    }

    public static bool IsCandidateType(Type type)
    {
        // Exclude ref structs
        if (type.IsRefStruct())
            return false;
        
        // Exclude pointers
        if (type.IsPointer)
            return false;
        
        // Exclude by ref types
        if (type.IsByRef)
            return false;
        
        // Exclude types that contain not-implemented static abstract members
        if (type.IsInterfaceWithStaticAbstractMethods())
            return false;
        
        return true;
    }

    private static bool IsCandidateMethod(MethodInfo methodInfo)
    {
        // Exclude property getters/setters and event adders/removers
        if (methodInfo.IsSpecialName)
            return false;

        if (methodInfo.GetParameters().Any(p => !IsCandidateType(p.ParameterType)))
            return false;
        
        if (!IsCandidateType(methodInfo.ReturnType))
            return false;
        
        // Exclude generic methods
        if (methodInfo.IsGenericMethod)
            return false;
        
        // Exclude compiler-generated methods
        if (IsCompilerMethod(methodInfo))
            return false;
        
        return true;
    }
    
    private static bool IsCandidateOperator(MethodInfo methodInfo)
    {
        if (methodInfo.GetParameters().Any(p => !IsCandidateType(p.ParameterType)))
            return false;
        
        if (!IsCandidateType(methodInfo.ReturnType))
            return false;
        
        // Exclude generic methods
        if (methodInfo.IsGenericMethod)
            return false;
        
        // Exclude compiler-generated methods
        if (IsCompilerMethod(methodInfo))
            return false;
        
        if (methodInfo.Name.GetLuaMetamethodName() == null)
            return false;
        
        return true;
    }
    
    private static bool IsCandidateEvent(EventInfo eventInfo)
    {
        var eventHandlerType = eventInfo.EventHandlerType;
        if (eventHandlerType == null)
            return false;

        // Exclude events with non-candidate event handler types
        if (!IsCandidateType(eventHandlerType))
            return false;
        
        // Exclude generic event handler types
        if (eventHandlerType.IsGenericType)
            return false;
        
        // Exclude events with delegates that have non-candidate methods
        var invokeMethod = eventHandlerType.GetMethod("Invoke");
        if (invokeMethod == null || !IsCandidateMethod(invokeMethod))
            return false;

        return true;
    }

    /// <summary>
    /// Check if a method is an extension method (static method with first parameter marked with ExtensionAttribute).
    /// </summary>
    private static bool IsExtensionMethod(MethodInfo method)
    {
        if (!method.IsStatic || method.GetParameters().Length == 0)
            return false;

        var firstParam = method.GetParameters()[0];
        var hasExtensionAttr = firstParam.GetCustomAttributesData()
            .Any(a => a.AttributeType.Name == "ExtensionAttribute" ||
                      a.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");

        // Also check if the method has the ExtensionAttribute directly (some compilers add it to the method too)
        if (!hasExtensionAttr)
        {
            hasExtensionAttr = method.GetCustomAttributesData()
                .Any(a => a.AttributeType.Name == "ExtensionAttribute" ||
                          a.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");
        }

        return hasExtensionAttr;
    }
    
    private static bool IsCompilerMethod(MethodInfo methodInfo)
    {
        return methodInfo.GetCustomAttributes()
                   .Any(attr => attr.GetType().FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
               || methodInfo.Name.Contains('<') && methodInfo.Name.Contains('>');
    }

    private static IEnumerable<MethodInfo> GetDeclaredInstanceMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);
    }

    private static IEnumerable<MethodInfo> GetMethodsInTypeAndInterfaces(Type type)
    {
        // Use a HashSet to track unique method signatures (method name + parameters)
        var seen = new HashSet<string>();

        var startingMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .Where(m => !m.IsSpecialName);

        foreach (var method in startingMethods)
        {
            var signature = GetMethodSignature(method);
            if (seen.Add(signature))
            {
                yield return method;
            }
        }

        if (type.IsArray)
        {
            yield break;
        }

        var interfaces = type.GetInterfaces();
        foreach (var iface in interfaces)
        {
            if (iface.IsInterfaceWithStaticAbstractMethods())
            {
                // Skip interfaces with static abstract methods, as we cannot generate implementation parts for them
                continue;
            }
            
            if (!type.IsInterface)
            {
                var map = type.GetInterfaceMap(iface);
                var interfaceMethods = map.InterfaceMethods;
                foreach (var method in interfaceMethods)
                {
                    var signature = GetMethodSignature(method);
                    if (!method.Name.Contains('.') && seen.Add(signature))
                    {
                        yield return method;
                    }
                }
            }
            else
            {
                var interfaceMethods = iface.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName);
                foreach (var method in interfaceMethods)
                {
                    var signature = GetMethodSignature(method);
                    if (seen.Add(signature))
                    {
                        yield return method;
                    }
                }
            }
        }
    }

    private static string GetMethodSignature(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var paramTypes = string.Join(",", parameters.Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
        return $"{method.Name}({paramTypes})";
    }

    private static IEnumerable<FieldInfo> GetFieldsInTypeAndBaseTypes(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
    }

    private static IEnumerable<PropertyInfo> GetPropertiesInTypeAndInterfaces(Type type)
    {
        var dict = new HashSet<string>();

        var startingProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        foreach (var prop in startingProperties)
        {
            if (dict.Add(prop.Name))
            {
                yield return prop;
            }
        }

        var interfaces = type.GetInterfaces();
        foreach (var iface in interfaces)
        {
            var interfaceProperties = iface.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in interfaceProperties)
            {
                if (dict.Add(prop.Name))
                {
                    yield return prop;
                }
            }
        }
    }
}

[Flags]
public enum DiscoveredKind
{
    BaseType = 1,
    Interface = 2,
    Property = 4,
    Field = 8,
    MethodReturnType = 16,
    MethodParameter = 32,
    ConstructorParameter = 64,
    EventReturnType = 128,
    ArrayElementType = 256,
    EventParameterType = 512,
    LuaVisible = 1024
}