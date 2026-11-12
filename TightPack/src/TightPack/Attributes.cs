namespace TightPack;

/// <summary>
/// Marks a type for generated <see cref="ITightPackable{T}"/> serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited =
    false)]
public sealed class TightPackableAttribute : Attribute
{
    public GenerateType GenerateType { get; }
    public SerializeLayout SerializeLayout { get; }

    public TightPackableAttribute(GenerateType generateType = GenerateType.Object)
    {
        GenerateType = generateType;
        SerializeLayout = generateType is GenerateType.VersionTolerant or GenerateType.CircularReference
            ? SerializeLayout.Explicit
            : SerializeLayout.Sequential;
    }

    public TightPackableAttribute(SerializeLayout serializeLayout)
    {
        GenerateType = GenerateType.Object;
        SerializeLayout = serializeLayout;
    }

    public TightPackableAttribute(GenerateType generateType, SerializeLayout serializeLayout)
    {
        GenerateType = generateType;
        SerializeLayout = serializeLayout;
    }
}

public enum GenerateType
{
    /// <summary>Fixed member order; a member count header catches schema drift.</summary>
    Object,

    /// <summary>Append-only member evolution; the reader skips members it does not know.</summary>
    VersionTolerant,

    /// <summary>Version tolerant, with object identity preserved (cycles allowed).</summary>
    CircularReference,

    /// <summary>The type itself is a collection; a formatter is generated for it.</summary>
    Collection,

    /// <summary>Do not generate anything (a hand-written formatter exists).</summary>
    NoGenerate,

    /// <summary>Abstract class or interface dispatching to tagged implementations.</summary>
    Union,
}

public enum SerializeLayout
{
    /// <summary>Members are written in declaration (or order-attribute) order.</summary>
    Sequential,

    /// <summary>Members carry their own length so a reader can skip unknown ones.</summary>
    Explicit,
}

/// <summary>
/// Dispatches an abstract class or interface to a tagged implementation. Every derived type
/// listed here must be <see cref="TightPackableAttribute">TightPackable</see> and a reference type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class TightPackUnionAttribute : Attribute
{
    public ushort Tag { get; }
    public Type Type { get; }

    public TightPackUnionAttribute(ushort tag, Type type)
    {
        Tag = tag;
        Type = type;
    }
}

/// <summary>Overrides the implicit (declaration) order of a member.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class TightPackOrderAttribute : Attribute
{
    public int Order { get; }

    public TightPackOrderAttribute(int order)
    {
        Order = order;
    }
}

/// <summary>Excludes a member that would otherwise be serialized.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class TightPackIgnoreAttribute : Attribute;

/// <summary>Includes a non-public member that would otherwise be excluded.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class TightPackIncludeAttribute : Attribute;

/// <summary>Selects the constructor used to rebuild the value. Required when there is more than one.</summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class TightPackConstructorAttribute : Attribute;

/// <summary>
/// Allows a member whose type has no known formatter to be resolved through the provider at runtime.
/// Applied to the declaring type, it covers every member of that type — the escape hatch for types
/// built from members defined in assemblies that do not reference TightPack.
/// </summary>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = false)]
public sealed class TightPackAllowSerializeAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TightPackOnSerializingAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TightPackOnSerializedAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TightPackOnDeserializingAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TightPackOnDeserializedAttribute : Attribute;

/// <summary>
/// Replaces the member's formatter. The formatter type must derive from
/// <see cref="TightPackFormatter{T}"/> for the member type, and is instantiated through its
/// static <c>Default</c> member when one exists, else through its parameterless constructor.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class TightPackCustomFormatterAttribute : Attribute
{
    public Type FormatterType { get; }

    public TightPackCustomFormatterAttribute(Type formatterType)
    {
        ArgumentNullException.ThrowIfNull(formatterType);
        FormatterType = formatterType;
    }
}
