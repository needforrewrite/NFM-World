using Microsoft.CodeAnalysis;

namespace TightPack.Generator;

internal static class Diagnostics
{
    private const string Category = "TightPack";

    public static readonly DiagnosticDescriptor NotPartial = new(
        "TPK0001",
        "Type must be partial",
        "'{0}' is marked [TightPackable] but is not declared partial, so no serialization can be generated for it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoUsableConstructor = new(
        "TPK0002",
        "No usable constructor",
        "'{0}' has no parameterless constructor and no constructor whose parameters match its serialized members; add [TightPackConstructor]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TooManyMembers = new(
        "TPK0003",
        "Too many members",
        "'{0}' declares {1} serialized members but the wire format allows at most {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnsupportedMember = new(
        "TPK0004",
        "Member type cannot be serialized",
        "'{0}.{1}' has type '{2}', which is not a primitive, a [TightPackable] type, or a known collection",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateUnionTag = new(
        "TPK0005",
        "Duplicate union tag",
        "'{0}' declares tag {1} more than once",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnionCaseNotPackable = new(
        "TPK0006",
        "Union case is not packable",
        "'{0}' lists '{1}' as a union case, but '{1}' is not [TightPackable]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CollectionShape = new(
        "TPK0007",
        "Collection type is not supported",
        "'{0}' is marked GenerateType.Collection but has no element type, Count property or Add method the generator can use",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoParameterlessConstructor = new(
        "TPK0008",
        "Circular reference requires a parameterless constructor",
        "'{0}' uses GenerateType.CircularReference, which has to construct the object before reading its members; add a parameterless constructor",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoPartialContainers = new(
        "TPK0009",
        "Containing types must be partial",
        "'{0}' is nested inside '{1}', which (or an outermost enclosing type) is not declared partial",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
