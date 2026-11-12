using System.Collections.Generic;

namespace TightPack.Generator;

internal enum GenerateKind
{
    Object,
    VersionTolerant,
    CircularReference,
    Collection,
    NoGenerate,
    Union,
}

internal enum MemberWriteKind
{
    /// <summary>A fixed-width primitive written with a dedicated writer method.</summary>
    Scalar,

    /// <summary>A primitive-shaped value the writer writes through the enum methods.</summary>
    Enum,

    /// <summary>UTF-8 string with a length prefix.</summary>
    String,

    /// <summary>A generated <c>ITightPackable</c> type.</summary>
    Packable,

    /// <summary>Anything resolved through <c>TightPackFormatterProvider</c>.</summary>
    Formatter,

    /// <summary>A tuple whose elements are written individually; see <see cref="MemberModel.TupleElements"/>.</summary>
    Tuple,
}

internal sealed class MemberModel
{
    public string Name = "";

    /// <summary>Fully qualified type name, e.g. <c>global::System.Int32</c>.</summary>
    public string TypeName = "";

    public MemberWriteKind WriteKind;

    /// <summary>Writer/reader method suffix, e.g. <c>Int32</c> for <c>WriteInt32</c>.</summary>
    public string Method = "";

    /// <summary>
    /// For a <see cref="MemberWriteKind.Formatter"/> member, the expression that constructs its
    /// formatter, so generated code can hold the instance in a static field instead of looking it
    /// up per access. Null when no concrete formatter can be named.
    /// </summary>
    public string? FormatterExpression;

    /// <summary>The static field holding <see cref="FormatterExpression"/>, assigned by the emitter.</summary>
    public string? FormatterField;

    public int Order = -1;

    /// <summary>True when the member is assignable from an object initializer (set or init).</summary>
    public bool CanAssign;

    /// <summary>True when the member is assignable outside an initializer (field or <c>set</c>).</summary>
    public bool CanAssignDirect;

    /// <summary>True for a field, false for a property.</summary>
    public bool IsField;

    /// <summary>
    /// The member is <c>required</c>, so the value has to be built through an object initializer:
    /// a parameterless constructor followed by assignments does not compile.
    /// </summary>
    public bool IsRequired;

    /// <summary>
    /// For a tuple member, its elements in order: the emitter writes them individually, which
    /// reaches a scalar writer directly instead of a provider lookup and a virtual call per element.
    /// Null for every other member.
    /// </summary>
    public List<MemberModel>? TupleElements;
}

internal sealed class CtorModel
{
    /// <summary>Constructor parameter type names, in declaration order.</summary>
    public List<string> ParameterTypes = new();

    /// <summary>Member index bound to each parameter, or -1.</summary>
    public List<int> ParameterMembers = new();
}

internal sealed class TypeModel
{
    public string HintName = "";
    public string TypeName = "";

    /// <summary>The declared name without type parameters, used to name the static constructor.</summary>
    public string SimpleName = "";

    public string Namespace = "";

    /// <summary>Enclosing type declarations, outermost first, each already fully qualified.</summary>
    public List<string> Containers = new();

    public GenerateKind Kind;
    public bool IsValueType;

    /// <summary>The partial declaration to emit, e.g. <c>readonly partial struct Foo</c>.</summary>
    public string Declaration = "";

    /// <summary>
    /// A type parameter member is written through the formatter provider; the generated member
    /// declares the generic constraint, so <c>T</c> is legal there but not in a static method.
    /// </summary>
    public bool HasTypeParameters;

    /// <summary>True when the type can be written as one raw memory block.</summary>
    public bool Blittable;

    public List<MemberModel> Members = new();
    public CtorModel? Ctor;

    public int BlittableSize;

    public List<UnionCaseModel> UnionCases = new();

    /// <summary>
    /// Registrations this type emits for formatters its wrapper members look up at runtime, so those
    /// types need no reflection-built formatter of their own.
    /// </summary>
    public List<string> DependentFormatters = new();

    public List<string> OnSerializing = new();
    public List<string> OnSerialized = new();
    public List<string> OnDeserializing = new();
    public List<string> OnDeserialized = new();

    public bool HasSerializationCallbacks =>
        OnSerializing.Count != 0 || OnSerialized.Count != 0 ||
        OnDeserializing.Count != 0 || OnDeserialized.Count != 0;
}

internal sealed class UnionCaseModel
{
    public ushort Tag;
    public string TypeName = "";
}
