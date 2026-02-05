namespace nfm_world_library.Lua;

/// <summary>
/// Marks a type to be exposed to Lua via the source generator.
/// The type will be available as a global variable in Lua with the same name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
public sealed class LuaVisibleAttribute : Attribute
{
    /// <summary>
    /// Optional custom name to use in Lua. If not specified, the type name is used.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Marks a method, property, or field to be hidden from Lua even if the containing type is [LuaVisible].
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Constructor | AttributeTargets.Event)]
public sealed class LuaHiddenAttribute : Attribute;

/// <summary>
/// Marks a method or property with a custom Lua name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class LuaNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// Marks an assembly to indicate that type T should be considered Lua visible.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AssemblyLuaVisibleAttribute<T>() : AssemblyLuaVisibleAttribute(typeof(T));

/// <summary>
/// Marks an assembly to indicate that the specified type should be considered Lua visible.
/// </summary>
/// <param name="type"></param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class AssemblyLuaVisibleAttribute(Type type) : Attribute
{
    public Type Type { get; } = type;
    
    public AssemblyLuaVisibleAttribute(string typeName) : this(Type.GetType(typeName) ?? throw new ArgumentException($"Type '{typeName}' not found."))
    { 
    }
}
