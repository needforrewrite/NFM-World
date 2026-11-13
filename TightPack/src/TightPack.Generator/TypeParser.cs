using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TightPack.Generator;

/// <summary>
/// Turns a <c>[TightPackable]</c> symbol into a <see cref="TypeModel"/>, including the proof that a
/// struct has no padding bytes (which is what permits blitting it).
/// </summary>
internal static class TypeParser
{
    private const string Namespace = "TightPack";

    /// <summary>
    /// Fully qualified without C# keyword aliases, so metadata lookups see <c>System.Int32</c>
    /// rather than <c>int</c>.
    /// </summary>
    private static readonly SymbolDisplayFormat FullyQualified =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
            & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

    // Scalars the writer has a dedicated method for: (metadata name, method suffix, wire size, alignment).
    private static readonly Dictionary<string, (string Method, int Size, int Align)> Scalars = new()
    {
        ["System.Boolean"] = ("Boolean", 1, 1),
        ["System.Byte"] = ("Byte", 1, 1),
        ["System.SByte"] = ("SByte", 1, 1),
        ["System.Char"] = ("Char", 2, 2),
        ["System.Int16"] = ("Int16", 2, 2),
        ["System.UInt16"] = ("UInt16", 2, 2),
        ["System.Int32"] = ("Int32", 4, 4),
        ["System.UInt32"] = ("UInt32", 4, 4),
        ["System.Int64"] = ("Int64", 8, 8),
        ["System.UInt64"] = ("UInt64", 8, 8),
        ["System.Single"] = ("Single", 4, 4),
        ["System.Double"] = ("Double", 8, 8),
        ["System.Half"] = ("Half", 2, 2),
        ["System.Int128"] = ("Int128", 16, 16),
        ["System.UInt128"] = ("UInt128", 16, 16),
        ["System.Decimal"] = ("Decimal", 16, 4),
        ["System.Guid"] = ("Guid", 16, 4),
        ["System.DateTime"] = ("DateTime", 8, 8),
        ["System.DateTimeOffset"] = ("DateTimeOffset", 12, 8),
        ["System.TimeSpan"] = ("TimeSpan", 8, 8),
        ["System.DateOnly"] = ("DateOnly", 4, 4),
        ["System.TimeOnly"] = ("TimeOnly", 8, 8),
        ["System.Text.Rune"] = ("Rune", 4, 4),
    };

    public static bool IsScalar(string metadataName) => Scalars.ContainsKey(metadataName);

    // Non-generic types that a formatter in the provider handles. Naming them here lets a member use
    // one without opting in through [TightPackAllowSerialize].
    private static readonly Dictionary<string, string> SimpleProviderTypes = new()
    {
        ["System.Collections.BitArray"] = "BitArrayFormatter",
        ["System.Globalization.CultureInfo"] = "CultureInfoFormatter",
        ["System.Numerics.BigInteger"] = "BigIntegerFormatter",
        ["System.Numerics.Quaternion"] = "QuaternionFormatter",
        ["System.Numerics.Vector2"] = "Vector2Formatter",
        ["System.Numerics.Vector3"] = "Vector3Formatter",
        ["System.Numerics.Vector4"] = "Vector4Formatter",
        ["System.Text.StringBuilder"] = "StringBuilderFormatter",
        ["System.TimeZoneInfo"] = "TimeZoneInfoFormatter",
        ["System.Type"] = "TypeFormatter",
        ["System.Uri"] = "UriFormatter",
        ["System.Version"] = "VersionFormatter",
    };

    public static TypeModel? Parse(
        INamedTypeSymbol symbol,
        Compilation compilation,
        DiagnosticSink context,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        Location? location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
        {
            return null;
        }

        AttributeData attribute = symbol.GetAttributes().First(a =>
            a.AttributeClass?.ToDisplayString() == Namespace + ".TightPackableAttribute");

        GenerateKind kind = ReadGenerateKind(attribute, compilation);
        if (kind == GenerateKind.NoGenerate)
        {
            return null;
        }

        // A value type has no identity to track, so reference tracking would only add boxing.
        if (kind == GenerateKind.CircularReference && symbol.IsValueType)
        {
            kind = GenerateKind.VersionTolerant;
        }

        // Union attributes are what make a type a union, so the enum value is optional.
        if (kind == GenerateKind.Object && HasUnionAttribute(symbol))
        {
            kind = GenerateKind.Union;
        }

        if (!IsPartial(symbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NotPartial, location, symbol.Name));
            return null;
        }

        var containers = new List<string>();
        for (INamedTypeSymbol? outer = symbol.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            if (!IsPartial(outer))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Diagnostics.NoPartialContainers, location, symbol.Name, outer.Name)
                );
                return null;
            }

            containers.Insert(0, BuildContainerDeclaration(outer));
        }

        var model = new TypeModel
        {
            TypeName = symbol.ToDisplayString(FullyQualified),
            Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : symbol.ContainingNamespace.ToDisplayString(),
            Containers = containers,
            Kind = kind,
            IsValueType = symbol.IsValueType,
            HintName = BuildHintName(symbol),
            Declaration = BuildDeclaration(symbol),
            SimpleName = symbol.Name,
            HasTypeParameters = symbol.TypeParameters.Length != 0,
        };

        if (kind == GenerateKind.Union)
        {
            if (!ParseUnion(symbol, model, compilation, context, location))
            {
                return null;
            }

            return model;
        }

        if (!ParseMembers(symbol, model, compilation, context, location, cancellationToken))
        {
            return null;
        }

        if (model.Members.Count >= TightPackWire.MaxMembers)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.TooManyMembers,
                location,
                symbol.Name,
                model.Members.Count,
                TightPackWire.MaxMembers
            )
            );
            return null;
        }

        ParseCallbacks(symbol, model);

        if (kind == GenerateKind.Collection)
        {
            if (!ParseCollection(symbol, model, compilation, context, location))
            {
                return null;
            }

            return model;
        }

        // Blitting writes the struct's whole image, so every non-static field has to reach the
        // payload: an excluded or ignored one would leave it uninitialised, and a member list that
        // disagrees with the memory layout (a property, a reordered field) would write it to the
        // wrong offset. Private fields are fine as long as they are included.
        //
        // Proved before the constructor check because a blitted value is rebuilt from its image,
        // which can also produce a readonly field with no assignment and no constructor.
        if (kind == GenerateKind.Object
            && symbol.IsValueType
            && IsBlitShaped(symbol, model)
            && LayoutProver.TryProve(symbol, compilation, cancellationToken, out int size))
        {
            model.Blittable = true;
            model.BlittableSize = size;

            // A callback would have to run after the image write, and the generated deserialize path
            // returns the blitted value as-is. Refusing keeps the shape honest rather than dropping them.
            if (model.OnSerializing.Count != 0 || model.OnSerialized.Count != 0
                || model.OnDeserializing.Count != 0 || model.OnDeserialized.Count != 0)
            {
                model.Blittable = false;
                model.BlittableSize = 0;
            }
        }

        if (!model.Blittable
            && !ParseConstructor(symbol, model, kind, compilation, context, location))
        {
            return null;
        }

        return model;
    }

    private static bool ParseUnion(
        INamedTypeSymbol symbol,
        TypeModel model,
        Compilation compilation,
        DiagnosticSink context,
        Location location
    )
    {
        var tags = new HashSet<ushort>();
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != Namespace + ".TightPackUnionAttribute")
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length != 2
                || attribute.ConstructorArguments[0].Value is not ushort tag
                || attribute.ConstructorArguments[1].Value is not INamedTypeSymbol caseType)
            {
                continue;
            }

            if (!tags.Add(tag))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Diagnostics.DuplicateUnionTag, location, symbol.Name, tag)
                );
                return false;
            }

            if (!IsPackable(caseType, compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnionCaseNotPackable,
                    location,
                    symbol.Name,
                    caseType.ToDisplayString()
                )
                );
                return false;
            }

            model.UnionCases.Add(new UnionCaseModel
            {
                Tag = tag,
                TypeName = caseType.ToDisplayString(FullyQualified),
            });
        }

        model.UnionCases.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        return true;
    }

    private static bool ParseCollection(
        INamedTypeSymbol symbol,
        TypeModel model,
        Compilation compilation,
        DiagnosticSink context,
        Location location
    )
    {
        INamedTypeSymbol? enumerable = symbol.AllInterfaces
            .FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

        if (enumerable is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CollectionShape, location, symbol.Name));
            return false;
        }

        ITypeSymbol element = enumerable.TypeArguments[0];
        var member = new MemberModel
        {
            Name = "Item",
            TypeName = element.ToDisplayString(FullyQualified),
            CanAssign = true,
        };

        if (!Classify(element, member, compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.UnsupportedMember,
                location,
                symbol.Name,
                "Item",
                element.ToDisplayString()
            )
            );
            return false;
        }

        model.Members.Add(member);
        return true;
    }

    private static bool ParseMembers(
        INamedTypeSymbol symbol,
        TypeModel model,
        Compilation compilation,
        DiagnosticSink context,
        Location location,
        CancellationToken cancellationToken
    )
    {
        var ordered = new List<(int Layer, int Order, int DeclIndex, MemberModel Member)>();

        // Members inherited from a base class are part of the payload, so they are collected first:
        // the base type's declaration order carries into every derived type.
        var hierarchy = new List<INamedTypeSymbol>();
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.BaseType)
        {
            if (current.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType
                or SpecialType.System_Enum)
            {
                break;
            }

            hierarchy.Insert(0, current);
        }

        for (int layer = 0; layer < hierarchy.Count; layer++)
            foreach (ISymbol member in hierarchy[layer].GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (member.IsStatic || member.IsImplicitlyDeclared || member is not (IFieldSymbol or IPropertySymbol))
                {
                    continue;
                }

                if (hierarchy[layer].IsRecord && member.Name == "EqualityContract")
                {
                    continue;
                }

                if (HasAttribute(member, "TightPackIgnoreAttribute"))
                {
                    continue;
                }

                bool included = HasAttribute(member, "TightPackIncludeAttribute");
                if (!included && member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                string typeName;
                bool canAssign;
                bool canAssignDirect;

                if (member is IFieldSymbol field)
                {
                    if (field.IsConst)
                    {
                        continue;
                    }

                    if (field.RefKind != RefKind.None)
                    {
                        continue;
                    }

                    typeName = field.Type.ToDisplayString(FullyQualified);
                    canAssign = !field.IsReadOnly;
                    canAssignDirect = canAssign;
                }
                else
                {
                    var property = (IPropertySymbol)member;
                    if (property.IsIndexer || property.RefKind != RefKind.None)
                    {
                        continue;
                    }

                    if (!included && property.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    // A getter-only property has nowhere to put a deserialized value, so it is only
                    // serialized when a constructor parameter can supply it or the author opts in.
                    if (property.SetMethod is null && !included && !HasConstructorParameter(symbol, property.Name))
                    {
                        continue;
                    }

                    typeName = property.Type.ToDisplayString(FullyQualified);
                    canAssign = property.SetMethod is not null;
                    canAssignDirect = property.SetMethod is { IsInitOnly: false };
                }

                ITypeSymbol memberType = member switch
            {
                IFieldSymbol f => f.Type,
                IPropertySymbol p => p.Type,
                _ => throw new InvalidOperationException(),
            };

                var memberModel = new MemberModel
            {
                Name = member.Name,
                TypeName = typeName,
                CanAssign = canAssign,
                CanAssignDirect = canAssignDirect,
                IsField = member is IFieldSymbol,
                IsRequired = member is IFieldSymbol { IsRequired: true }
                    or IPropertySymbol { IsRequired: true },
            };
                if (!Classify(memberType, memberModel, compilation, member))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedMember,
                        location,
                        symbol.Name,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                    );
                    return false;
                }

                if (memberModel.WriteKind == MemberWriteKind.Formatter)
                {
                    AddDependentFormatters(model, memberType, compilation);
                }

                int order = GetOrder(member) ?? int.MaxValue;
                memberModel.Order = order;
                int declarationIndex = GetDeclarationIndex(member);
                ordered.Add((layer, order, declarationIndex, memberModel));
            }

        // Base members come before derived ones; within one declaration, an explicit order wins and
        // declaration position breaks ties. Declaration position alone would interleave them by file.
        ordered.Sort(static (a, b) =>
            a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer)
            : a.Order != b.Order ? a.Order.CompareTo(b.Order)
            : a.DeclIndex.CompareTo(b.DeclIndex));

        foreach (var (_, _, _, member) in ordered)
        {
            model.Members.Add(member);
        }

        return true;
    }

    /// <summary>
    /// Classifies each element of a tuple member. Fails when an element is itself only
    /// provider-resolved, since then the inline path would be no cheaper.
    /// </summary>
    private static bool TryInlineTuple(INamedTypeSymbol tuple, MemberModel model, Compilation compilation)
    {
        var elements = new List<MemberModel>();
        foreach (IFieldSymbol element in tuple.TupleElements)
        {
            var elementModel = new MemberModel
            {
                Name = element.Name,
                TypeName = element.Type.ToDisplayString(FullyQualified),
                CanAssign = true,
                CanAssignDirect = true,
            };

            if (!Classify(element.Type, elementModel, compilation))
            {
                return false;
            }

            // An element that still needs a formatter adds a static field per element for no gain
            // over the tuple formatter, so only the direct-write kinds are worth inlining.
            if (elementModel.WriteKind is MemberWriteKind.Formatter)
            {
                return false;
            }

            elements.Add(elementModel);
        }

        model.WriteKind = MemberWriteKind.Tuple;
        model.TupleElements = elements;
        return true;
    }

    private static bool Classify(
        ITypeSymbol type,
        MemberModel model,
        Compilation compilation,
        ISymbol? owner = null
    )
    {
        string metadataName = MetaName(type);

        if (type is ITypeParameterSymbol)
        {
            model.WriteKind = MemberWriteKind.Formatter;
            return true;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            model.WriteKind = MemberWriteKind.Enum;
            model.Method = "";
            return true;
        }

        if (Scalars.TryGetValue(metadataName, out var scalar))
        {
            model.WriteKind = MemberWriteKind.Scalar;
            model.Method = scalar.Method;
            return true;
        }

        if (metadataName == "System.String")
        {
            model.WriteKind = MemberWriteKind.String;
            return true;
        }

        if (owner is not null && TryGetCustomFormatter(owner, out INamedTypeSymbol? formatterType))
        {
            model.WriteKind = MemberWriteKind.Formatter;
            model.FormatterExpression = InstantiateFormatter(formatterType!);
            return true;
        }

        // A tuple's elements are classified individually so the emitter writes them straight to the
        // writer. Going through ValueTupleFormatter instead resolves an element formatter per
        // element, which measured ~2.7ns of the ~6.4ns an element cost.
        if (type is INamedTypeSymbol { IsTupleType: true, Arity: >= 1 and <= 8 } tuple
            && TryInlineTuple(tuple, model, compilation))
        {
            return true;
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                model.WriteKind = MemberWriteKind.Formatter;
                model.FormatterExpression = BuildFormatterExpression(type);
                return true;
            }

            if (IsPackable(named, compilation))
            {
                model.WriteKind = MemberWriteKind.Packable;
                return true;
            }

            if (IsProviderResolved(named))
            {
                model.WriteKind = MemberWriteKind.Formatter;
                model.FormatterExpression = BuildFormatterExpression(type);
                return true;
            }
        }

        if (type is IArrayTypeSymbol)
        {
            model.WriteKind = MemberWriteKind.Formatter;
            model.FormatterExpression = BuildFormatterExpression(type);
            return true;
        }

        // Opt-in runtime resolution: the member type may get a formatter from the provider. The
        // attribute works per member, and on the declaring type for types built from assemblies that
        // do not reference TightPack and so cannot carry [TightPackable].
        if (owner is not null
            && (HasAttribute(owner, "TightPackAllowSerializeAttribute")
                || (owner.ContainingType is { } container
                    && HasAttribute(container, "TightPackAllowSerializeAttribute"))))
        {
            model.WriteKind = MemberWriteKind.Formatter;
            return true;
        }

        return false;
    }

    /// <summary>
    /// <c>System.*</c> name without the C# keyword aliases, so <c>int</c> is <c>System.Int32</c>.
    /// </summary>
    internal static string MetaName(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualified).Replace("global::", "");

    /// <summary>
    /// Types whose value is written through a formatter from the provider rather than by generated
    /// code. Every entry here must also be named in <see cref="BuildFormatterExpression"/>.
    /// </summary>
    private static bool IsProviderResolved(INamedTypeSymbol type)
    {
        if (type.IsTupleType)
        {
            return type.Arity is >= 1 and <= 8;
        }

        if (!type.IsGenericType)
        {
            return SimpleProviderTypes.ContainsKey(MetaName(type));
        }

        string name = MetaName(type.OriginalDefinition);
        return name is "System.Collections.Generic.List<T>"
            or "System.Collections.Generic.Stack<T>"
            or "System.Collections.Generic.Queue<T>"
            or "System.Collections.Generic.LinkedList<T>"
            or "System.Collections.Generic.SortedSet<T>"
            or "System.Collections.Generic.PriorityQueue<TElement, TPriority>"
            or "System.Collections.Generic.SortedDictionary<TKey, TValue>"
            or "System.Collections.Generic.SortedList<TKey, TValue>"
            or "System.Collections.Generic.ISet<T>"
            or "System.Collections.Generic.IReadOnlySet<T>"
            or "System.Collections.ObjectModel.Collection<T>"
            or "System.Collections.ObjectModel.ObservableCollection<T>"
            or "System.Collections.ObjectModel.ReadOnlyCollection<T>"
            or "System.Collections.ObjectModel.ReadOnlyObservableCollection<T>"
            or "System.Collections.Concurrent.ConcurrentQueue<T>"
            or "System.Collections.Concurrent.ConcurrentStack<T>"
            or "System.Collections.Concurrent.ConcurrentBag<T>"
            or "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>"
            or "System.Collections.Concurrent.BlockingCollection<T>"
            or "System.ArraySegment<T>"
            or "System.Collections.Generic.IList<T>"
            or "System.Collections.Generic.IEnumerable<T>"
            or "System.Collections.Generic.ICollection<T>"
            or "System.Collections.Generic.IReadOnlyList<T>"
            or "System.Collections.Generic.IReadOnlyCollection<T>"
            or "System.Collections.Generic.HashSet<T>"
            or "System.Collections.Generic.Dictionary<TKey, TValue>"
            or "System.Collections.Generic.IDictionary<TKey, TValue>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            or "System.Collections.Generic.KeyValuePair<TKey, TValue>"
            or "System.Collections.BitArray"
            or "System.Collections.Immutable.ImmutableArray<T>"
            or "System.Memory<T>"
            or "System.ReadOnlyMemory<T>"
            or "System.Runtime.CompilerServices.InlineArray2<T>"
            or "System.Runtime.CompilerServices.InlineArray3<T>"
            or "System.Runtime.CompilerServices.InlineArray4<T>"
            or "System.Runtime.CompilerServices.InlineArray5<T>"
            or "System.Runtime.CompilerServices.InlineArray6<T>"
            or "System.Runtime.CompilerServices.InlineArray7<T>"
            or "System.Runtime.CompilerServices.InlineArray8<T>"
            or "System.Runtime.CompilerServices.InlineArray9<T>"
            or "System.Runtime.CompilerServices.InlineArray10<T>"
            or "System.Runtime.CompilerServices.InlineArray11<T>"
            or "System.Runtime.CompilerServices.InlineArray12<T>"
            or "System.Runtime.CompilerServices.InlineArray13<T>"
            or "System.Runtime.CompilerServices.InlineArray14<T>"
            or "System.Runtime.CompilerServices.InlineArray15<T>"
            or "System.Runtime.CompilerServices.InlineArray16<T>"
            or "System.ValueTuple<T1>"
            or "System.ValueTuple<T1, T2>"
            or "System.ValueTuple<T1, T2, T3>"
            or "System.ValueTuple<T1, T2, T3, T4>"
            or "System.ValueTuple<T1, T2, T3, T4, T5>"
            or "System.ValueTuple<T1, T2, T3, T4, T5, T6>"
            or "System.ValueTuple<T1, T2, T3, T4, T5, T6, T7>"
            or "System.ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>";
    }

    /// <summary>
    /// The expression that builds the formatter for <paramref name="type"/>, or null when there is
    /// none to name. Generated code registers each of these from a static initializer, which keeps
    /// formatter construction out of reflection and therefore out of the AOT reachability problem.
    /// </summary>
    private static string? BuildFormatterExpression(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return array.Rank switch
            {
                1 => Formatter("ArrayFormatter", array.ElementType),
                2 => Formatter("TwoDimensionalArrayFormatter", array.ElementType),
                3 => Formatter("ThreeDimensionalArrayFormatter", array.ElementType),
                4 => Formatter("FourDimensionalArrayFormatter", array.ElementType),
                _ => null,
            };
        }

        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        // Tuples are matched by shape, not by name: a tuple symbol displays as "(int A, float B)".
        if (named.IsTupleType)
        {
            return Formatter("ValueTupleFormatter", named.TupleElements.Select(e => e.Type).ToArray());
        }

        if (!named.IsGenericType && SimpleProviderTypes.TryGetValue(MetaName(named), out string? simple))
        {
            return $"new global::TightPack.Formatters.{simple}()";
        }

        INamedTypeSymbol underlying = named.TupleUnderlyingType ?? named;

        if (underlying.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return Formatter("NullableFormatter", underlying.TypeArguments[0]);
        }

        ITypeSymbol[] arguments = underlying.TypeArguments.ToArray();
        return MetaName(underlying.OriginalDefinition) switch
        {
            "System.Collections.Generic.List<T>" => Formatter("ListFormatter", arguments),
            "System.Collections.Generic.Dictionary<TKey, TValue>" => Formatter("DictionaryFormatter", arguments),
            "System.Collections.Generic.HashSet<T>" => Formatter("HashSetFormatter", arguments),
            "System.Collections.Generic.KeyValuePair<TKey, TValue>" => Formatter("KeyValuePairFormatter", arguments),
            "System.Collections.Generic.IEnumerable<T>" => Formatter("EnumerableFormatter", arguments),
            "System.Collections.Generic.IList<T>" => Formatter("InterfaceListFormatter", arguments),
            "System.Collections.Generic.IReadOnlyList<T>" => Formatter("IReadOnlyListFormatter", arguments),
            "System.Collections.Generic.ICollection<T>" => Formatter("InterfaceCollectionFormatter", arguments),
            "System.Collections.Generic.IReadOnlyCollection<T>" => Formatter("IReadOnlyCollectionFormatter", arguments),
            "System.Collections.Generic.IDictionary<TKey, TValue>" => Formatter("InterfaceDictionaryFormatter", arguments),
            "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>" => Formatter("IReadOnlyDictionaryFormatter", arguments),
            "System.Collections.Generic.Stack<T>" => Formatter("StackFormatter", arguments),
            "System.Collections.Generic.Queue<T>" => Formatter("QueueFormatter", arguments),
            "System.Collections.Generic.LinkedList<T>" => Formatter("LinkedListFormatter", arguments),
            "System.Collections.Generic.SortedSet<T>" => Formatter("SortedSetFormatter", arguments),
            "System.Collections.Generic.ISet<T>" => Formatter("InterfaceSetFormatter", arguments),
            "System.Collections.Generic.IReadOnlySet<T>" => Formatter("IReadOnlySetFormatter", arguments),
            "System.Collections.Generic.PriorityQueue<TElement, TPriority>" => Formatter("PriorityQueueFormatter", arguments),
            "System.Collections.Generic.SortedDictionary<TKey, TValue>" => Formatter("SortedDictionaryFormatter", arguments),
            "System.Collections.Generic.SortedList<TKey, TValue>" => Formatter("SortedListFormatter", arguments),
            "System.Collections.ObjectModel.Collection<T>" => Formatter("CollectionFormatter", arguments),
            "System.Collections.ObjectModel.ObservableCollection<T>" => Formatter("ObservableCollectionFormatter", arguments),
            "System.Collections.ObjectModel.ReadOnlyCollection<T>" => Formatter("ReadOnlyCollectionFormatter", arguments),
            "System.Collections.ObjectModel.ReadOnlyObservableCollection<T>" => Formatter(
                "ReadOnlyObservableCollectionFormatter",
                arguments
            ),
            "System.Collections.Concurrent.ConcurrentQueue<T>" => Formatter("ConcurrentQueueFormatter", arguments),
            "System.Collections.Concurrent.ConcurrentStack<T>" => Formatter("ConcurrentStackFormatter", arguments),
            "System.Collections.Concurrent.ConcurrentBag<T>" => Formatter("ConcurrentBagFormatter", arguments),
            "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>" => Formatter(
                "ConcurrentDictionaryFormatter",
                arguments
            ),
            "System.Collections.Concurrent.BlockingCollection<T>" => Formatter("BlockingCollectionFormatter", arguments),
            "System.ArraySegment<T>" => Formatter("ArraySegmentFormatter", arguments),
            "System.Collections.BitArray" => "new global::TightPack.Formatters.BitArrayFormatter()",
            "System.Collections.Immutable.ImmutableArray<T>" => Formatter("ImmutableArrayFormatter", arguments),
            "System.Memory<T>" => IsByte(arguments[0])
                ? "new global::TightPack.Formatters.MemoryByteFormatter()"
                : Formatter("MemoryFormatter", arguments),
            "System.ReadOnlyMemory<T>" => IsByte(arguments[0])
                ? "new global::TightPack.Formatters.ReadOnlyMemoryByteFormatter()"
                : Formatter("ReadOnlyMemoryFormatter", arguments),
            "System.Runtime.CompilerServices.InlineArray2<T>" => Formatter("InlineArray2Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray3<T>" => Formatter("InlineArray3Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray4<T>" => Formatter("InlineArray4Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray5<T>" => Formatter("InlineArray5Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray6<T>" => Formatter("InlineArray6Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray7<T>" => Formatter("InlineArray7Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray8<T>" => Formatter("InlineArray8Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray9<T>" => Formatter("InlineArray9Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray10<T>" => Formatter("InlineArray10Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray11<T>" => Formatter("InlineArray11Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray12<T>" => Formatter("InlineArray12Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray13<T>" => Formatter("InlineArray13Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray14<T>" => Formatter("InlineArray14Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray15<T>" => Formatter("InlineArray15Formatter", arguments),
            "System.Runtime.CompilerServices.InlineArray16<T>" => Formatter("InlineArray16Formatter", arguments),
            "System.ValueTuple<T1>" => Formatter("ValueTupleFormatter", arguments),
            "System.ValueTuple<T1, T2>" => Formatter("ValueTupleFormatter", arguments),
            "System.ValueTuple<T1, T2, T3>" => Formatter("ValueTupleFormatter", arguments),
            "System.ValueTuple<T1, T2, T3, T4>" => Formatter("ValueTupleFormatter", arguments),
            "System.ValueTuple<T1, T2, T3, T4, T5>" => Formatter("ValueTupleFormatter", arguments),
            "System.ValueTuple<T1, T2, T3, T4, T5, T6>" => Formatter("ValueTupleFormatter", arguments),
            "System.ValueTuple<T1, T2, T3, T4, T5, T6, T7>" => Formatter("ValueTupleFormatter", arguments),
            "System.ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>" => Formatter("ValueTupleFormatter", arguments),
            _ => null,
        };
    }

    private static bool IsByte(ITypeSymbol type) => MetaName(type) == "System.Byte";

    private static string Formatter(string name, ITypeSymbol argument) => Formatter(name, new[] { argument });

    private static string Formatter(string name, ITypeSymbol[] arguments) =>
        $"new global::TightPack.Formatters.{name}<" +
        $"{string.Join(", ", arguments.Select(a => a.ToDisplayString(FullyQualified)))}>()";

    /// <summary>
    /// Records the formatters a wrapper formatter will look up at runtime, so this type registers
    /// them itself. A packable type with no generated member of its own - one reached only through
    /// <c>Nullable&lt;T&gt;</c>, a memory type or a collection - has nothing else to register it, and
    /// the provider's remaining source is a reflection-built formatter the AOT compiler cannot see.
    /// </summary>
    private static void AddDependentFormatters(
        TypeModel model,
        ITypeSymbol type,
        Compilation compilation
    )
    {
        foreach (ITypeSymbol argument in DependentArguments(type))
        {
            if (argument is INamedTypeSymbol named && IsPackable(named, compilation))
            {
                model.DependentFormatters.Add(
                    "global::TightPack.TightPackFormatterProvider.Register(" +
                    $"new global::TightPack.TightPackableFormatter<{named.ToDisplayString(FullyQualified)}>());"
                );
            }
        }
    }

    /// <summary>
    /// The element types a wrapper formatter resolves through the provider. A packable value's own
    /// generated code already registers it, so only wrappers whose argument has none qualify.
    /// </summary>
    private static IEnumerable<ITypeSymbol> DependentArguments(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            if (array.Rank is >= 1 and <= 4)
            {
                yield return array.ElementType;
            }

            yield break;
        }

        if (type is not INamedTypeSymbol named || !named.IsGenericType)
        {
            yield break;
        }

        INamedTypeSymbol underlying = named.TupleUnderlyingType ?? named;
        string definition = MetaName(underlying.OriginalDefinition);

        bool resolvedAtRuntime = definition
            is "System.Nullable<T>"
            or "System.ReadOnlyMemory<T>"
            or "System.Memory<T>"
            or "System.Collections.Generic.List<T>"
            or "System.Collections.Generic.Stack<T>"
            or "System.Collections.Generic.Queue<T>"
            or "System.Collections.Generic.LinkedList<T>"
            or "System.Collections.Generic.SortedSet<T>"
            or "System.Collections.Generic.ISet<T>"
            or "System.Collections.Generic.IReadOnlySet<T>"
            or "System.Collections.Generic.PriorityQueue<TElement, TPriority>"
            or "System.Collections.Generic.SortedDictionary<TKey, TValue>"
            or "System.Collections.Generic.SortedList<TKey, TValue>"
            or "System.Collections.ObjectModel.Collection<T>"
            or "System.Collections.ObjectModel.ObservableCollection<T>"
            or "System.Collections.ObjectModel.ReadOnlyCollection<T>"
            or "System.Collections.ObjectModel.ReadOnlyObservableCollection<T>"
            or "System.Collections.Concurrent.ConcurrentQueue<T>"
            or "System.Collections.Concurrent.ConcurrentStack<T>"
            or "System.Collections.Concurrent.ConcurrentBag<T>"
            or "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>"
            or "System.Collections.Concurrent.BlockingCollection<T>"
            or "System.ArraySegment<T>"
            or "System.Collections.Generic.Dictionary<TKey, TValue>"
            or "System.Collections.Generic.HashSet<T>"
            or "System.Collections.Generic.KeyValuePair<TKey, TValue>"
            or "System.Collections.Generic.IEnumerable<T>"
            or "System.Collections.Generic.IList<T>"
            or "System.Collections.Generic.IReadOnlyList<T>"
            or "System.Collections.Generic.ICollection<T>"
            or "System.Collections.Generic.IReadOnlyCollection<T>"
            or "System.Collections.Generic.IDictionary<TKey, TValue>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            or "System.Collections.Immutable.ImmutableArray<T>"
            or "System.Runtime.CompilerServices.InlineArray2<T>"
            or "System.Runtime.CompilerServices.InlineArray3<T>"
            or "System.Runtime.CompilerServices.InlineArray4<T>"
            or "System.Runtime.CompilerServices.InlineArray5<T>"
            or "System.Runtime.CompilerServices.InlineArray6<T>"
            or "System.Runtime.CompilerServices.InlineArray7<T>"
            or "System.Runtime.CompilerServices.InlineArray8<T>"
            or "System.Runtime.CompilerServices.InlineArray9<T>"
            or "System.Runtime.CompilerServices.InlineArray10<T>"
            or "System.Runtime.CompilerServices.InlineArray11<T>"
            or "System.Runtime.CompilerServices.InlineArray12<T>"
            or "System.Runtime.CompilerServices.InlineArray13<T>"
            or "System.Runtime.CompilerServices.InlineArray14<T>"
            or "System.Runtime.CompilerServices.InlineArray15<T>"
            or "System.Runtime.CompilerServices.InlineArray16<T>";

        if (!resolvedAtRuntime)
        {
            yield break;
        }

        foreach (ITypeSymbol argument in underlying.TypeArguments)
        {
            yield return argument is INamedTypeSymbol { IsTupleType: true } tuple
                ? tuple.TupleUnderlyingType ?? argument
                : argument;
        }
    }

    private static bool ParseConstructor(
        INamedTypeSymbol symbol,
        TypeModel model,
        GenerateKind kind,
        Compilation compilation,
        DiagnosticSink context,
        Location location
    )
    {
        if (!symbol.IsValueType && kind == GenerateKind.CircularReference)
        {
            // Access is irrelevant here: the generated code is a member of the same type, so a
            // private parameterless constructor is just as usable.
            bool hasParameterless = symbol.InstanceConstructors.Any(c => c.Parameters.Length == 0);

            if (!hasParameterless)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Diagnostics.NoParameterlessConstructor, location, symbol.Name)
                );
                return false;
            }
        }

        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < model.Members.Count; i++)
        {
            byName[model.Members[i].Name] = i;
        }

        IMethodSymbol? chosen = symbol.InstanceConstructors
            .FirstOrDefault(c => HasAttribute(c, "TightPackConstructorAttribute"));

        if (chosen is null)
        {
            IMethodSymbol? parameterless = symbol.InstanceConstructors.FirstOrDefault(
                c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
            );

            // A parameterless constructor plus members that accept a plain assignment is the
            // simplest rebuild path; anything else needs a matching constructor.
            if (parameterless is not null && model.Members.TrueForAll(m => m.CanAssignDirect))
            {
                return true;
            }

            // Prefer the widest constructor whose parameters all name a member of the same type;
            // members it does not cover are assigned through the initializer afterwards.
            chosen = symbol.InstanceConstructors
                .Where(c => !c.IsImplicitlyDeclared && c.Parameters.Length != 0)
                .Where(c => ParametersBind(c, model.Members))
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();
        }

        if (chosen is null)
        {
            // Without a constructor the generator builds the value through an object initializer,
            // which is enough for a value type or a type with a parameterless constructor - and for
            // an init-only member, since the initializer runs before the constructor returns.
            bool hasParameterless = symbol.IsValueType || symbol.InstanceConstructors.Any(
                c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
            );

            if (hasParameterless && model.Members.TrueForAll(m => m.CanAssign))
            {
                return true;
            }

            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NoUsableConstructor, location, symbol.Name));
            return false;
        }

        var ctor = new CtorModel();
        foreach (IParameterSymbol parameter in chosen.Parameters)
        {
            ctor.ParameterTypes.Add(parameter.Type.ToDisplayString(FullyQualified));
            ctor.ParameterMembers.Add(byName.TryGetValue(parameter.Name, out int index) ? index : -1);
        }

        model.Ctor = ctor;
        return true;
    }

    private static bool ParametersBind(IMethodSymbol ctor, List<MemberModel> members)
    {
        foreach (IParameterSymbol parameter in ctor.Parameters)
        {
            MemberModel? match = null;
            foreach (MemberModel member in members)
            {
                if (string.Equals(member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
                {
                    match = member;
                    break;
                }
            }

            if (match is null || match.TypeName != parameter.Type.ToDisplayString(FullyQualified))
            {
                return false;
            }
        }

        return true;
    }

    private static void ParseCallbacks(INamedTypeSymbol symbol, TypeModel model)
    {
        foreach (ISymbol member in symbol.GetMembers())
        {
            if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary, Parameters.Length: 0 })
            {
                continue;
            }

            if (HasAttribute(member, "TightPackOnSerializingAttribute"))
            {
                model.OnSerializing.Add(member.Name);
            }
            else if (HasAttribute(member, "TightPackOnSerializedAttribute"))
            {
                model.OnSerialized.Add(member.Name);
            }
            else if (HasAttribute(member, "TightPackOnDeserializingAttribute"))
            {
                model.OnDeserializing.Add(member.Name);
            }
            else if (HasAttribute(member, "TightPackOnDeserializedAttribute"))
            {
                model.OnDeserialized.Add(member.Name);
            }
        }
    }

    private static GenerateKind ReadGenerateKind(AttributeData attribute, Compilation compilation)
    {
        if (attribute.ConstructorArguments.Length == 0)
        {
            return GenerateKind.Object;
        }

        TypedConstant first = attribute.ConstructorArguments[0];
        if (first.Type?.ToDisplayString() != Namespace + ".GenerateType")
        {
            return GenerateKind.Object;
        }

        return (int)first.Value!switch
        {
            1 => GenerateKind.VersionTolerant,
            2 => GenerateKind.CircularReference,
            3 => GenerateKind.Collection,
            4 => GenerateKind.NoGenerate,
            5 => GenerateKind.Union,
            _ => GenerateKind.Object,
        };
    }

    private static bool IsPackable(INamedTypeSymbol type, Compilation compilation)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() == Namespace + ".TightPackableAttribute"))
            {
                return ReadGenerateKind(
                    current.GetAttributes().First(a =>
                        a.AttributeClass?.ToDisplayString() == Namespace + ".TightPackableAttribute"),
                    compilation
                ) != GenerateKind.NoGenerate;
            }
        }

        return type.AllInterfaces.Any(i =>
            i.OriginalDefinition.ToDisplayString() == Namespace + ".ITightPackable<TSelf>");
    }

    private static bool HasConstructorParameter(INamedTypeSymbol symbol, string name)
    {
        foreach (IMethodSymbol ctor in symbol.InstanceConstructors)
        {
            if (ctor.Parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartial(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.All(r =>
            r.GetSyntax() is TypeDeclarationSyntax { Modifiers: var modifiers }
            && modifiers.Any(SyntaxKind.PartialKeyword)
        );

    /// <summary>
    /// True when the serialized members are exactly the struct's instance fields, in declaration
    /// order, with no property standing in for a field.
    /// </summary>
    private static bool IsBlitShaped(INamedTypeSymbol symbol, TypeModel model)
    {
        var fields = new List<IFieldSymbol>();
        for (ITypeSymbol? current = symbol; current is not null; current = current.BaseType)
        {
            foreach (IFieldSymbol field in current.GetMembers().OfType<IFieldSymbol>())
            {
                if (!field.IsStatic && !field.IsConst && field.RefKind == RefKind.None)
                {
                    fields.Add(field);
                }
            }
        }

        if (fields.Count == 0 || fields.Count != model.Members.Count)
        {
            return false;
        }

        for (int i = 0; i < fields.Count; i++)
        {
            if (!model.Members[i].IsField || model.Members[i].Name != fields[i].Name)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == Namespace + "." + attributeName);

    private static bool HasUnionAttribute(ISymbol symbol) =>
        HasAttribute(symbol, "TightPackUnionAttribute");

    /// <summary>Reads the formatter type named by <c>[TightPackCustomFormatter]</c>, if any.</summary>
    private static bool TryGetCustomFormatter(
        ISymbol owner,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out INamedTypeSymbol? formatterType
    )
    {
        foreach (AttributeData attribute in owner.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == Namespace + ".TightPackCustomFormatterAttribute"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is INamedTypeSymbol named)
            {
                formatterType = named;
                return true;
            }
        }

        formatterType = null;
        return false;
    }

    /// <summary>
    /// Builds the formatter expression for a hand-written formatter: its static <c>Default</c> when
    /// there is one, else a parameterless constructor.
    /// </summary>
    private static string InstantiateFormatter(INamedTypeSymbol formatterType)
    {
        string name = formatterType.ToDisplayString(FullyQualified);
        bool hasDefault = formatterType.GetMembers("Default")
            .OfType<IFieldSymbol>()
            .Any(f => f.IsStatic && f.Type.Equals(formatterType, SymbolEqualityComparer.Default));

        return hasDefault ? $"{name}.Default" : $"new {name}()";
    }

    private static int? GetOrder(ISymbol member)
    {
        foreach (AttributeData attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == Namespace + ".TightPackOrderAttribute"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is int order)
            {
                return order;
            }
        }

        return null;
    }

    private static int GetDeclarationIndex(ISymbol member)
    {
        Location? location = member.Locations.FirstOrDefault(l => l.IsInSource);
        return location?.SourceSpan.Start ?? int.MaxValue;
    }

    private static string BuildDeclaration(INamedTypeSymbol symbol)
    {
        var parts = new List<string>();

        if (symbol.IsReadOnly)
        {
            parts.Add("readonly");
        }

        if (symbol.IsRefLikeType)
        {
            parts.Add("ref");
        }

        if (symbol.IsAbstract && symbol.TypeKind == TypeKind.Class)
        {
            parts.Add("abstract");
        }

        // Only a class declaration may carry "sealed"; emitting it on a container breaks the
        // partial declaration, and a nested struct is never sealed.
        if (symbol.IsSealed && symbol.TypeKind == TypeKind.Class && !symbol.IsRecord)
        {
            parts.Add("sealed");
        }

        parts.Add("partial");

        string keyword = symbol.TypeKind switch
        {
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => "class",
        };

        parts.Add(symbol.IsRecord ? "record " + keyword : keyword);
        parts.Add(symbol.Name);

        if (symbol.TypeParameters.Length != 0)
        {
            parts.Add("<" + string.Join(", ", symbol.TypeParameters.Select(p => p.Name)) + ">");
        }

        return string.Join(" ", parts);
    }

    private static string BuildContainerDeclaration(INamedTypeSymbol type) => BuildDeclaration(type);

    private static string BuildHintName(INamedTypeSymbol symbol)
    {
        var parts = new List<string>();
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            parts.Insert(0, current.Name);
        }

        string name = string.Join(".", parts);
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return builder.ToString();
    }
}

/// <summary>Wire constants the generator has to agree with <c>TightPackCode</c> on.</summary>
internal static class TightPackWire
{
    /// <summary>One byte holds the member count, minus the null and reference sentinels.</summary>
    public const int MaxMembers = 249;
}
