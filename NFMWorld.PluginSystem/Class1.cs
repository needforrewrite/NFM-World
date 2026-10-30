namespace NFMWorld.PluginSystem;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface |
    AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Constructor, Inherited = false)]
public class PluginVisibleAttribute(Visible visible = Visible.Self) : Attribute
{
    public Visible Visible { get; set; } = visible;
}

/// <summary>
/// Marks a type as globally visible to the plugin system. This means that the type (and optionally all its members)
/// will be visible to plugins.
/// </summary>
/// <typeparam name="T"></typeparam>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class PublicVisibleAttribute<T>(Visible visible = Visible.Self) : Attribute
{
    public Visible Visible { get; set; } = visible;
}

public enum Visible
{
    /// <summary>
    /// Make this class, struct, enum, interface, method, property, or field visible to the plugin system.
    /// </summary>
    Self,
    
    /// <summary>
    /// When applied on a type, make itself and all members of that type visible to the plugin system.
    /// </summary>
    IncludingAllMembersOfType,
    
    /// <summary>
    /// When applied on an open generic type, make any generic instantiation of the type visible to the plugin system.
    /// </summary>
    IncludingAnyGenericInstantiation,
    
    /// <summary>
    /// When applied on an open generic type, make any generic instantiation of the type, and all members of those
    /// instantiations, visible to the plugin system.
    /// </summary>
    IncludingAnyGenericInstantiationAndAllMembersOfType,
}