namespace nfm_world_library.Lua;

/// <summary>
/// Marks a type to be exposed to Lua via the source generator.
/// The type will be available as a global variable in Lua with the same name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
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
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Constructor)]
public sealed class LuaHiddenAttribute : Attribute;

/// <summary>
/// Marks a method or property with a custom Lua name.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class LuaNameAttribute : Attribute
{
    public string Name { get; }

    public LuaNameAttribute(string name)
    {
        Name = name;
    }
}
