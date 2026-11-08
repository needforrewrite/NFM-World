namespace NFMWorld.PluginSystem;

[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Enum
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Property
    | AttributeTargets.Field
    | AttributeTargets.Constructor,
    Inherited = false
)]
public class PluginVisibleAttribute(Visible visible = Visible.Self) : Attribute
{
    public Visible Visible { get; set; } = visible;
}

/// <summary>
/// Names a type outside the host (a vendor dependency) that plugins may reach, and how
/// much of it. <see cref="Visible.Self"/> exposes only the type and the members it marks
/// itself - typically none - so use <see cref="Visible.IncludingAllMembersOfType"/> to
/// expose its public members.
/// </summary>
/// <typeparam name="T"></typeparam>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class VendorPluginVisibleAttribute<T>(Visible visible = Visible.Self) : Attribute
{
    public Visible Visible { get; set; } = visible;
}

/// <summary>
/// Names a type outside the host that plugins may reach, and how much of it. The
/// non-generic form is for a type that cannot be used as a generic argument, such as a
/// static class.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class VendorPluginVisibleAttribute(Type type, Visible visible = Visible.Self) : Attribute
{
    public Type Type { get; } = type;
    public Visible Visible { get; set; } = visible;
}

public enum Visible
{
    /// <summary>
    /// Make this class, struct, enum, interface, method, property, or field visible to the plugin system.
    /// On a type this exposes the type and the members that carry their own marker; it
    /// does not expose the type's other members, private or otherwise.
    /// </summary>
    Self,

    /// <summary>
    /// When applied on a type, make its public members and its public or protected
    /// constructors visible to the plugin system without each needing a marker of its
    /// own. Everything else stays hidden.
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
