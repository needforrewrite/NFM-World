using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace TightPack.Generator;

/// <summary>
/// Proves that a struct occupies exactly its fields' bytes, so blitting it cannot write or read
/// padding. The layout is simulated twice, at a 4-byte and an 8-byte maximum alignment (as x86 and
/// x64 hosts effectively do), and the struct only qualifies when neither run inserts padding and both
/// produce the same total. An explicit <c>Pack</c> lowers that cap for the struct it is declared on;
/// it does not reach the structs nested inside it. Anything the prover cannot model is refused,
/// because a false positive would put uninitialised memory on the wire.
/// </summary>
internal static class LayoutProver
{
    private const int MaxDepth = 32;

    /// <summary>
    /// Types whose image is their serialized bytes on every host. <c>System.DateTime</c> is absent on
    /// purpose: the writer uses <c>ToBinary</c>, which is not the raw image of a local time.
    /// </summary>
    private static readonly Dictionary<string, (int Size, int Align)> Primitives = new()
    {
        ["System.Boolean"] = (1, 1),
        ["System.Byte"] = (1, 1),
        ["System.SByte"] = (1, 1),
        ["System.Char"] = (2, 2),
        ["System.Int16"] = (2, 2),
        ["System.UInt16"] = (2, 2),
        ["System.Int32"] = (4, 4),
        ["System.UInt32"] = (4, 4),
        ["System.Int64"] = (8, 8),
        ["System.UInt64"] = (8, 8),
        ["System.Single"] = (4, 4),
        ["System.Double"] = (8, 8),
        ["System.Half"] = (2, 2),
        ["System.Int128"] = (16, 16),
        ["System.UInt128"] = (16, 16),
        ["System.Decimal"] = (16, 4),
        ["System.Guid"] = (16, 4),
        ["System.TimeSpan"] = (8, 8),
        ["System.DateOnly"] = (4, 4),
        ["System.TimeOnly"] = (8, 8),
        ["System.Text.Rune"] = (4, 4),
    };

    /// <summary>
    /// Returns true when <paramref name="type"/> is a struct whose in-memory image is exactly its
    /// serialized bytes. <paramref name="size"/> is that image's size.
    /// </summary>
    public static bool TryProve(INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken, out int size)
    {
        size = 0;
        if (!type.IsValueType || type.SpecialType == SpecialType.System_Void || type.IsRefLikeType)
        {
            return false;
        }

        var visited = new HashSet<string>();
        if (!TryLayout(type, 4, compilation, cancellationToken, visited, 0, out int narrowSize, out _)
            || !TryLayout(type, 8, compilation, cancellationToken, visited, 0, out int wideSize, out _))
        {
            return false;
        }

        if (narrowSize != wideSize)
        {
            return false;
        }

        size = narrowSize;
        return true;
    }

    /// <summary>
    /// Lays out <paramref name="type"/> on a host that aligns a field to at most
    /// <paramref name="hostAlign"/> bytes. A <c>Pack</c> declared on a struct lowers the cap for its
    /// own fields, but is not passed down: a nested struct keeps its own layout and size.
    /// </summary>
    private static bool TryLayout(
        ITypeSymbol type,
        int hostAlign,
        Compilation compilation,
        CancellationToken cancellationToken,
        HashSet<string> visited,
        int depth,
        out int size,
        out int align
    )
    {
        size = 0;
        align = 1;

        if (depth > MaxDepth)
        {
            return false;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return type is INamedTypeSymbol { EnumUnderlyingType: { } underlying }
                   && TryLayout(underlying, hostAlign, compilation, cancellationToken, visited, depth + 1, out size, out align);
        }

        if (Primitives.TryGetValue(TypeParser.MetaName(type), out var primitive))
        {
            size = primitive.Size;
            align = System.Math.Min(primitive.Align, hostAlign);
            return true;
        }

        if (type is not INamedTypeSymbol named || !named.IsValueType)
        {
            return false;
        }

        // Nullable<T> has a bool flag plus an aligned value; even when it happens to fit it is not a
        // type we want a raw image of, and the generator never needs one.
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return false;
        }

        // [InlineArray(n)] is n copies of its single field, whatever that field is called.
        if (TryGetInlineArrayLength(named, out int length))
        {
            if (named.TypeArguments.Length != 1
                || !TryLayout(
                    named.TypeArguments[0],
                    hostAlign,
                    compilation,
                    cancellationToken,
                    visited,
                    depth + 1,
                    out int elementSize,
                    out int elementAlign
                ))
            {
                return false;
            }

            size = elementSize * length;
            align = elementAlign;
            return true;
        }

        if (!TryGetPack(named, out int pack))
        {
            return false;
        }

        List<IFieldSymbol> fields = CollectFields(named);
        if (fields.Count == 0 || !visited.Add(type.ToDisplayString()))
        {
            return false;
        }

        try
        {
            return Simulate(
                fields,
                pack > 0 ? System.Math.Min(pack, hostAlign) : hostAlign,
                hostAlign,
                compilation,
                cancellationToken,
                visited,
                depth,
                out size,
                out align
            );
        }
        finally
        {
            visited.Remove(type.ToDisplayString());
        }
    }

    /// <summary>
    /// Places each field at the next offset that is a multiple of its alignment, capped at
    /// <paramref name="maxAlign"/>, and refuses the struct if that skips a byte.
    /// </summary>
    private static bool Simulate(
        List<IFieldSymbol> fields,
        int maxAlign,
        int hostAlign,
        Compilation compilation,
        CancellationToken cancellationToken,
        HashSet<string> visited,
        int depth,
        out int total,
        out int structAlign
    )
    {
        total = 0;
        structAlign = 1;
        int offset = 0;

        foreach (IFieldSymbol field in fields)
        {
            if (!TryLayout(
                field.Type,
                hostAlign,
                compilation,
                cancellationToken,
                visited,
                depth + 1,
                out int size,
                out int align
            ))
            {
                return false;
            }

            align = System.Math.Min(align, maxAlign);

            // A field that does not start where the previous one ended is padded.
            if (offset % align != 0)
            {
                return false;
            }

            offset += size;
            if (align > structAlign)
            {
                structAlign = align;
            }
        }

        // A tail that is not a whole number of alignments is padded too.
        if (offset % structAlign != 0)
        {
            return false;
        }

        total = offset;
        return true;
    }

    private static List<IFieldSymbol> CollectFields(INamedTypeSymbol type)
    {
        var fields = new List<IFieldSymbol>();
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (IFieldSymbol field in current.GetMembers().OfType<IFieldSymbol>())
            {
                if (!field.IsStatic && !field.IsConst && field.RefKind == RefKind.None)
                {
                    fields.Add(field);
                }
            }
        }

        return fields;
    }

    /// <summary>
    /// Reads the struct's alignment cap. Returns false when the layout is anything but sequential,
    /// because an explicit or automatic layout makes the field order no evidence of the image.
    /// </summary>
    private static bool TryGetPack(INamedTypeSymbol type, out int pack)
    {
        pack = 0;
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.InteropServices.StructLayoutAttribute")
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is int layout
                && layout != 0)
            {
                return false;
            }

            foreach (var named in attribute.NamedArguments)
            {
                // An explicit Size overrides the computed layout, which pads the tail.
                if (named.Key == "Size" && named.Value.Value is int size && size > 0)
                {
                    return false;
                }

                if (named.Key == "Pack" && named.Value.Value is int value && value > 0)
                {
                    pack = value;
                }
            }
        }

        return true;
    }

    private static bool TryGetInlineArrayLength(INamedTypeSymbol type, out int length)
    {
        length = 0;
        foreach (AttributeData attribute in type.OriginalDefinition.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InlineArrayAttribute"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is int value
                && value > 0)
            {
                length = value;
                return true;
            }
        }

        return false;
    }
}
