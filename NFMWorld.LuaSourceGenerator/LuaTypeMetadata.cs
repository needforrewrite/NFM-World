using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace NFMWorld.LuaSourceGenerator;

internal class BaseLuaTypeMetadata
{
    public AssemblyIdentity? Assembly { get; }
    public string LuaName { get; }
    public string TypeName { get; }
    public string FullTypeName { get; }
    public string? Namespace { get; }
    public bool IsStatic { get; }
    public bool IsValueType { get; }
    public bool IsRecord { get; }
    public bool IsInterface { get; }
    public bool IsCandidate { get; }
    public bool HasLuaVisibleAttr { get; }
    public bool IsImplicitlyLuaVisible { get => field || HasLuaVisibleAttr; set; }
    
    public bool IsILuaUserData { get; }
    public bool IsReferenceType { get; }
    public bool IsNullableReferenceType { get; }
    public bool IsNullableValueType { get; }

    public SpecialType SpecialType { get; }
    public bool IsFixed64 { get; }
    public bool IsFixed64AngleSingle { get; }
    public bool IsFixed64Euler { get; }
    public bool IsFixed64Vector3 { get; }
    
    public bool IsArray { get; }
    public bool IsRefStruct { get; }
    public bool IsConstructedGeneric { get; }
    public bool IsOpenGeneric { get; }
    public bool IsEnum { get; }

    public string SanitizedTypeName { get; }

    public bool IsBuiltIn => SpecialType is
                                 SpecialType.System_Boolean or
                                 SpecialType.System_Char or
                                 SpecialType.System_SByte or
                                 SpecialType.System_Byte or
                                 SpecialType.System_Int16 or
                                 SpecialType.System_UInt16 or
                                 SpecialType.System_Int32 or
                                 SpecialType.System_UInt32 or
                                 SpecialType.System_Int64 or
                                 SpecialType.System_UInt64 or
                                 SpecialType.System_Decimal or
                                 SpecialType.System_Single or
                                 SpecialType.System_Double or
                                 SpecialType.System_String or
                                 SpecialType.System_Void ||
                             IsFixed64 ||
                             IsFixed64AngleSingle ||
                             IsFixed64Euler ||
                             IsFixed64Vector3 ||
                             NullableUnderlyingType?.IsBuiltIn == true;
    
    public BaseLuaTypeMetadata? NullableUnderlyingType { get; }
    
    public static SymbolDisplayFormat FullyQualifiedNoGlobalNoNamespaceFormat { get; } =
        new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public BaseLuaTypeMetadata(ITypeSymbol symbol, SymbolReferences references)
    {
        Assembly = symbol.ContainingAssembly?.Identity;
        TypeName = symbol.Name;
        FullTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = symbol.ContainingNamespace?.ToDisplayString();
        IsStatic = symbol.IsStatic;
        IsValueType = symbol.IsValueType;
        IsRecord = symbol.IsRecord;
        IsInterface = symbol.TypeKind == TypeKind.Interface;
        SpecialType = symbol.SpecialType;
        IsFixed64 = SymbolEqualityComparer.Default.Equals(symbol, references.Fixed64);
        IsFixed64AngleSingle = SymbolEqualityComparer.Default.Equals(symbol, references.Fixed64AngleSingle);
        IsFixed64Euler = SymbolEqualityComparer.Default.Equals(symbol, references.Fixed64Euler);
        IsFixed64Vector3 = SymbolEqualityComparer.Default.Equals(symbol, references.Fixed64Vector3);

        var luaVisibleAttr = GetAttr(symbol, references.LuaVisibleAttribute);
        var hasLuaVisibleAttr = luaVisibleAttr != null && symbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, luaVisibleAttr.AttributeClass));
        HasLuaVisibleAttr = hasLuaVisibleAttr;

        var sanitizedTypeName = (Namespace != null ? Namespace + "." : "") + SanitizeLongTypeName(symbol.ToDisplayString(FullyQualifiedNoGlobalNoNamespaceFormat));
        LuaName = luaVisibleAttr?.ConstructorArguments.FirstOrDefault().Value as string
                  ?? luaVisibleAttr?.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Name").Value.Value as string
                  ?? (HasLuaVisibleAttr ? TypeName : sanitizedTypeName); // TODO decide if we should have short names for non-LuaVisible types
        var luaNameAttr = GetAttr(symbol, references.LuaNameAttribute);
        if (luaNameAttr?.ConstructorArguments.FirstOrDefault().Value is string nameOverride)
            LuaName = nameOverride;

        IsArray = symbol.TypeKind == TypeKind.Array;
        IsRefStruct = symbol is { IsValueType: true, IsRefLikeType: true };
        IsConstructedGeneric = symbol is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: > 0, IsDefinition: false }; // List<int>, not List<>
        IsOpenGeneric = symbol is INamedTypeSymbol { IsGenericType: true, IsDefinition: true }; // List<>, GenericWrapper<>
        IsCandidate = !IsRefStruct && !IsOpenGeneric && !IsBuiltIn && !IsNullableValueType;

        IsILuaUserData = symbol.AllInterfaces.Any(t => SymbolEqualityComparer.Default.Equals(t, references.ILuaUserData));

        IsReferenceType = symbol.IsReferenceType;
        IsNullableReferenceType = symbol.IsReferenceType && symbol.NullableAnnotation == NullableAnnotation.Annotated;
        if (symbol is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            IsNullableValueType = true;
            NullableUnderlyingType = new BaseLuaTypeMetadata(namedType.TypeArguments[0], references);
        }
        IsEnum = symbol is INamedTypeSymbol { EnumUnderlyingType: not null };
        SanitizedTypeName = sanitizedTypeName;
    }

    private static string SanitizeLongTypeName(string fullTypeName)
    {
        // Map primitives to short names
        if (fullTypeName is
            "int" or "long" or "float" or "double" or
            "bool" or "string" or "byte" or "sbyte" or
            "short" or "ushort" or "uint" or "ulong" or
            "decimal" or "char" or "object")
            return fullTypeName;

        return Regex.Replace(fullTypeName, @"\[,*\]", match => "Array" + (match.Value.Count(c => c == ',') is var v and >= 1 ? $"{v+1}" : ""))
                .Replace("<", "_")
                .Replace(">", "")
                .Replace(".", "_")
                .Replace("?", "n")
                .Replace("(", "ValueTuple_")
                .Replace(", ", "_")
                .Replace(")", "_")
                .Replace("[", "")
                .Replace("]", "")
                .Replace(",", "_")
                .Replace(" ", "_")
                .Replace("*", "Ptr")
                .Replace("global::", "")
                .Replace("@", "_")
                .TrimEnd('_');
    }

    protected static bool HasAttr(ISymbol s, INamedTypeSymbol? attr)
        => attr != null && s.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr));

    protected static AttributeData? GetAttr(ISymbol s, INamedTypeSymbol? attr)
        => attr != null ? s.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr)) : null;
}

internal sealed class LuaTypeMetadata : BaseLuaTypeMetadata
{
    public bool IsInlineArray { get; }
    public int? InlineArrayLength { get; }
    public string? InlineArrayElementType { get; }
    
    public bool IsIEnumerable { get; }
    public BaseLuaTypeMetadata? IEnumerableType { get; }
    public bool IsIEnumerableOfKeyValuePair { get; }
    public BaseLuaTypeMetadata? IEnumerableKeyType { get; }
    public BaseLuaTypeMetadata? IEnumerableValueType { get; }

    /// <summary>Base type full name (with global::), or null if System.Object/ValueType.</summary>
    public string? BaseTypeFullName => BaseType?.FullTypeName;

    public BaseLuaTypeMetadata? BaseType { get; }

    /// <summary>Implemented interface full names (with global::).</summary>
    public string[] InterfaceFullNames { get; }
    
    /// <summary>True if the type has required properties/fields (no constructor should be generated).</summary>
    public bool HasRequiredMembers { get; }

    public BaseLuaTypeMetadata? EnumUnderlyingType { get; }

    public LuaMethodMetadata[] InstanceMethods { get; }
    public LuaPropertyMetadata[] InstanceProperties { get; }
    public LuaIndexerMetadata[] InstanceIndexers { get; }
    public LuaFieldMetadata[] InstanceFields { get; }
    public LuaOperatorMetadata[] Operators { get; }
    public LuaMethodMetadata[] StaticMethods { get; }
    public LuaPropertyMetadata[] StaticProperties { get; }
    public LuaFieldMetadata[] StaticFields { get; }
    public LuaConstructorMetadata[] Constructors { get; }
    public LuaEnumMemberMetadata[] EnumMembers { get; }

    // Instance methods dispatch through __index too, so method-only types
    // (e.g. interfaces or calculators) still need an __index metamethod.
    public bool HasIndex => InstanceFields.Length > 0 || InstanceProperties.Length > 0 || InstanceIndexers.Length > 0 || InstanceMethods.Length > 0; // todo check if any readable members exist
    public bool HasNewIndex => InstanceFields.Length > 0 || InstanceProperties.Length > 0 || InstanceIndexers.Length > 0; // todo check if any writable members exist

    public bool HasStaticIndex => StaticFields.Length > 0 || StaticProperties.Length > 0; // todo check if any readable members exist
    public bool HasStaticNewIndex => StaticFields.Length > 0 || StaticProperties.Length > 0; // todo check if any writable members exist

    public bool HasPairsAndIPairs => IsArray || IsInlineArray || IsIEnumerable;

    public bool HasLength { get; }
    public bool HasCount { get; }
    public bool HasLengthOrCount => HasLength || IsInlineArray || HasCount || IsIEnumerable;
    
    public static SymbolDisplayFormat FullyQualifiedFormatWithoutTypeParameters { get; } =
        new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.None,
            miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static ITypeSymbol? GetEnumerableElementType(ITypeSymbol type, SymbolReferences references)
    {
        // Case 1: the type itself is IEnumerable<T>
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.Equals(references.IEnumerableT, SymbolEqualityComparer.Default))
        {
            return namedType.TypeArguments.FirstOrDefault();
        }

        // Case 2: type implements IEnumerable<T> (possibly through an interface chain,
        // base class, or is itself an array like T[])
        var allInterfaces = type.AllInterfaces;

        var match = type is INamedTypeSymbol nt && nt.OriginalDefinition.Equals(references.IEnumerableT, SymbolEqualityComparer.Default)
            ? nt
            : allInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.Equals(references.IEnumerableT, SymbolEqualityComparer.Default));

        if (match is not null)
            return match.TypeArguments.FirstOrDefault();

        // Case 3: arrays (string[], int[], etc.) implement IEnumerable<T> implicitly per spec
        // but Roslyn's AllInterfaces sometimes doesn't surface it cleanly depending on symbol source;
        // handle explicitly for safety.
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        return null;
    }
    
    private static bool TryGetKeyValuePairTypes(
        ITypeSymbol type,
        SymbolReferences references,
        [NotNullWhen(true)] out ITypeSymbol? keyType,
        [NotNullWhen(true)] out ITypeSymbol? valueType)
    {
        keyType = null;
        valueType = null;

        if (type is not INamedTypeSymbol namedType)
            return false;

        var kvpDefinition = references.KeyValuePair;

        if (kvpDefinition is null)
            return false; // shouldn't happen unless corlib refs are broken

        if (!namedType.OriginalDefinition.Equals(kvpDefinition, SymbolEqualityComparer.Default))
            return false;

        keyType = namedType.TypeArguments[0];
        valueType = namedType.TypeArguments[1];
        return true;
    }
    
    public LuaTypeMetadata(ITypeSymbol symbol, SymbolReferences references) : base(symbol, references)
    {
        var luaVisibleAttr = GetAttr(symbol, references.LuaVisibleAttribute);

        var inlineAttr = symbol.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, references.InlineArrayAttribute));
        if (inlineAttr != null)
        {
            IsInlineArray = true;
            InlineArrayLength = (int)(inlineAttr.ConstructorArguments.FirstOrDefault().Value ?? 0);
            var firstField = symbol.GetMembers().OfType<IFieldSymbol>().FirstOrDefault();
            InlineArrayElementType = firstField?.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        if (GetEnumerableElementType(symbol, references) is { } elementType)
        {
            IsIEnumerable = true;
            IEnumerableType = new BaseLuaTypeMetadata(elementType, references);

            if (TryGetKeyValuePairTypes(elementType, references, out var kvpKey, out var kvpValue))
            {
                IsIEnumerableOfKeyValuePair = true;
                IEnumerableKeyType = new BaseLuaTypeMetadata(kvpKey, references);
                IEnumerableValueType = new BaseLuaTypeMetadata(kvpValue, references);
            }
        }

        // Base type and interfaces for stub generators
        var bt = symbol.BaseType;
        if (bt != null && bt.SpecialType != SpecialType.System_Object && bt.SpecialType != SpecialType.System_ValueType)
            BaseType = new BaseLuaTypeMetadata(bt, references);
        InterfaceFullNames = symbol.AllInterfaces
            .Where(i => i.DeclaredAccessibility == Accessibility.Public)
            .Select(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToArray();

        var members = symbol.GetMembers();

        InstanceMethods = CollectMethods(members, references, isStatic: false, symbol);
        Operators = CollectOperators(references, members);
        InstanceProperties = CollectProperties(members, references, isStatic: false);
        InstanceIndexers = CollectInstanceIndexers(members, references);
        InstanceFields = CollectFields(members, references, isStatic: false);
        StaticMethods = CollectMethods(members, references, isStatic: true, symbol);
        StaticProperties = CollectProperties(members, references, isStatic: true);
        StaticFields = CollectFields(members, references, isStatic: true);
        Constructors = IsStatic || HasAnyRequiredMembers(symbol) || symbol is not INamedTypeSymbol ints ? [] : CollectConstructors(ints, references);
        HasRequiredMembers = HasAnyRequiredMembers(symbol);
        EnumMembers = symbol is INamedTypeSymbol { EnumUnderlyingType: not null } ? CollectEnumMembers(members, references) : [];
        EnumUnderlyingType = symbol is INamedTypeSymbol { EnumUnderlyingType: {} enumUnderlying } ? new BaseLuaTypeMetadata(enumUnderlying, references) : null;

        // For interfaces, also collect members inherited from base interfaces
        if (IsInterface)
        {
            var seenLuaNames = new HashSet<string>(
                InstanceProperties.Select(p => p.LuaName)
                    .Concat(InstanceFields.Select(f => f.LuaName))
                    .Concat(InstanceMethods.Select(m => m.LuaName))
            );

            var seenIndexerKeys = new HashSet<string>(
                InstanceIndexers.Select(i => i.Name + ":" + i.KeyTypeName)
            );

            var inheritedProps = new List<LuaPropertyMetadata>();
            var inheritedFields = new List<LuaFieldMetadata>();
            var inheritedMethods = new List<LuaMethodMetadata>();
            var inheritedIndexers = new List<LuaIndexerMetadata>();

            // Walk all base interfaces recursively
            WalkBaseInterfaces(symbol, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));

            void WalkBaseInterfaces(ITypeSymbol iface, HashSet<INamedTypeSymbol> visited)
            {
                foreach (var baseIface in iface.Interfaces)
                {
                    if (!visited.Add(baseIface)) continue;
                    CollectFromInterface(baseIface);
                    WalkBaseInterfaces(baseIface, visited);
                }
            }

            void CollectFromInterface(ITypeSymbol baseIface)
            {
                var baseMembers = baseIface.GetMembers();
                var isBaseLuaVisible = HasAttr(baseIface, luaVisibleAttr?.AttributeClass);

                // TODO should we?
                // if (!isBaseLuaVisible) return;

                foreach (var m in baseMembers.OfType<IMethodSymbol>()
                             .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared && !HasAttr(m, references.LuaHiddenAttribute))
                             .Where(m => !m.Parameters.Any(p => p.RefKind != RefKind.None))
                             .Where(m => !m.Parameters.Any(p => p.Type.IsRefLikeType))
                             .Where(m => !m.ReturnType.IsRefLikeType))
                {
                    var meta = new LuaMethodMetadata(m, references, owningType: symbol);
                    if (seenLuaNames.Add(meta.LuaName))
                        inheritedMethods.Add(meta);
                }

                foreach (var p in baseMembers.OfType<IPropertySymbol>()
                             .Where(p => !p.IsIndexer && !p.IsImplicitlyDeclared && !HasAttr(p, references.LuaHiddenAttribute))
                             .Where(p => !p.IsStatic))
                {
                    var meta = new LuaPropertyMetadata(p, references);
                    if (seenLuaNames.Add(meta.LuaName))
                        inheritedProps.Add(meta);
                }

                foreach (var f in baseMembers.OfType<IFieldSymbol>()
                             .Where(f => !f.IsImplicitlyDeclared && !HasAttr(f, references.LuaHiddenAttribute) && !f.IsStatic))
                {
                    var meta = new LuaFieldMetadata(f, references);
                    if (seenLuaNames.Add(meta.LuaName))
                        inheritedFields.Add(meta);
                }

                foreach (var idx in baseMembers.OfType<IPropertySymbol>()
                             .Where(p => p.IsIndexer && !p.IsStatic && p.Parameters.Length == 1 && !HasAttr(p, references.LuaHiddenAttribute))
                             .Where(p => GetResultantVisibility(p) == SymbolVisibility.Public))
                {
                    var meta = new LuaIndexerMetadata(idx, references);
                    if (seenIndexerKeys.Add(meta.Name + ":" + meta.KeyTypeName))
                        inheritedIndexers.Add(meta);
                }
            }

            // Merge inherited members into the type's own lists
            InstanceProperties = [.. InstanceProperties, .. inheritedProps];
            InstanceIndexers = [.. InstanceIndexers, .. inheritedIndexers];
            InstanceFields = [.. InstanceFields, .. inheritedFields];
            InstanceMethods = [.. InstanceMethods, .. inheritedMethods];
        }

        HasLength = IsArray || symbol
            .GetMembers("Length")
            .Any(s => s is IPropertySymbol prop && prop.Type.SpecialType is SpecialType.System_Int32);
        HasCount = symbol
            .GetMembers("Count")
            .Any(s => s is IPropertySymbol prop && prop.Type.SpecialType is SpecialType.System_Int32);
    }

    private static LuaEnumMemberMetadata[] CollectEnumMembers(ImmutableArray<ISymbol> members, SymbolReferences references)
    {
        return members.OfType<IFieldSymbol>()
            .Where(f => f.ConstantValue != null)
            .Select(m => new LuaEnumMemberMetadata(m, references))
            .ToArray();
    }

    private static bool HasAnyRequiredMembers(ITypeSymbol symbol)
    {
        return symbol.GetMembers().Any(m =>
            m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true });
    }

    private static LuaOperatorMetadata[] CollectOperators(SymbolReferences references, ImmutableArray<ISymbol> members)
    {
        return members.OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.UserDefinedOperator && !HasAttr(m, references.LuaHiddenAttribute))
            .Where(m => GetResultantVisibility(m) == SymbolVisibility.Public)
            .Where(m => IsLuaOperatorMethodName(m.Name))
            .Select(m => new LuaOperatorMetadata(m, references))
            .ToArray();
    }

    private static bool IsLuaOperatorMethodName(string name)
    {
        return name is
            "op_Addition" or
            "op_Subtraction" or
            "op_Division" or
            "op_UnaryNegation" or
            "op_Equality" or
            "op_LessThan" or
            "op_LessThanOrEqual" or
            "op_Modulus";
    }

    private static LuaMethodMetadata[] CollectMethods(ImmutableArray<ISymbol> members, SymbolReferences references, bool isStatic, ITypeSymbol? owningType = null)
    {
        var methods = members.OfType<IMethodSymbol>()
            .Where(m => m.IsStatic == isStatic && m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared && !HasAttr(m, references.LuaHiddenAttribute))
            .Where(m => GetResultantVisibility(m) == SymbolVisibility.Public)
            .Where(m => !m.Parameters.Any(p => p.RefKind != RefKind.None)) // skip ref/out/in parameters
            .Where(m => !m.Parameters.Any(p => p.Type.IsRefLikeType)) // skip Span/ReadOnlySpan params (ref structs)
            .Where(m => !m.ReturnType.IsRefLikeType) // skip methods returning ref structs
            .Select(m => new LuaMethodMetadata(m, references, owningType)).ToArray();

        return methods;
    }
    
    // Copied from https://github.com/dotnet/roslyn/blob/d2ff1d83e8fde6165531ad83f0e5b1ae95908289/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Extensions/ISymbolExtensions.cs#L28-L73
    private static SymbolVisibility GetResultantVisibility(ISymbol symbol)
    {
        // Start by assuming it's visible.
        var visibility = SymbolVisibility.Public;
        switch (symbol.Kind)
        {
            case SymbolKind.Alias:
                // Aliases are uber private.  They're only visible in the same file that they
                // were declared in.
                return SymbolVisibility.Private;
            case SymbolKind.Parameter:
                // Parameters are only as visible as their containing symbol
                return GetResultantVisibility(symbol.ContainingSymbol);
            case SymbolKind.TypeParameter:
                // Type Parameters are private.
                return SymbolVisibility.Private;
        }

        while (symbol is not null && symbol.Kind != SymbolKind.Namespace)
        {
            switch (symbol.DeclaredAccessibility)
            {
                // If we see anything private, then the symbol is private.
                case Accessibility.NotApplicable:
                case Accessibility.Private:
                    return SymbolVisibility.Private;
                // If we see anything internal, then knock it down from public to
                // internal.
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    visibility = SymbolVisibility.Internal;
                    break;
                // For anything else (Public, Protected, ProtectedOrInternal), the
                // symbol stays at the level we've gotten so far.
            }

            symbol = symbol.ContainingSymbol;
        }

        return visibility;
    }

    enum SymbolVisibility
    {
        Public,
        Internal,
        Private,
    }

    /// <summary>Short Lua-friendly type name for overload suffix generation.</summary>
    private static string ParamSuffix(LuaParameterMetadata p)
    {
        // Map primitives to short names
        var simple = p.TypeName switch
        {
            "int" => "int", "long" => "long", "float" => "flt", "double" => "dbl",
            "bool" => "bool", "string" => "str", "byte" => "byte", "sbyte" => "sbyte",
            "short" => "short", "ushort" => "ushort", "uint" => "uint", "ulong" => "ulong",
            "decimal" => "dec", "char" => "char", "object" => "obj",
            _ => null
        };
        if (simple != null) return simple;

        var fullName = p.TypeName;
        
        // Strip generic args for the base name: "System.Collections.Generic.List<int>" → "List"
        var name = fullName.Contains('<') ? fullName[..fullName.IndexOf('<')] : fullName;
        // Take last segment after '.'
        var lastDot = name.LastIndexOf('.');
        var shortName = lastDot >= 0 ? name[(lastDot + 1)..] : name;
        
        // Edge cases:
        // int?
        // (bool Up, bool Down)
        // int[,]
        // int[]
        // int*
        return CamelCase(
            Regex.Replace(shortName, @"\[,*\]", match => "Array" + (match.Value.Count(c => c == ',') is var v and >= 1 ? $"{v+1}" : ""))
            .Replace("?", "n")
            .Replace("(", "ValueTuple_")
            .Replace(")", "_")
            .Replace(", ", "_")
            .Replace("[", "")
            .Replace("]", "")
            .Replace(",", "_")
            .Replace(" ", "_")
            .Replace("*", "Ptr")
            .TrimEnd('_')
        );

        static string CamelCase(string n) => n.Length > 0 ? char.ToLowerInvariant(n[0]) + n[1..] : n;
    }

    private static LuaPropertyMetadata[] CollectProperties(ImmutableArray<ISymbol> members, SymbolReferences references, bool isStatic)
    {
        return members.OfType<IPropertySymbol>()
            .Where(p => p.IsStatic == isStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && !HasAttr(p, references.LuaHiddenAttribute))
            .Where(m => GetResultantVisibility(m) == SymbolVisibility.Public)
            .Select(p => new LuaPropertyMetadata(p, references))
            .ToArray();
    }

    /// <summary>Collects public single-parameter instance indexers (e.g. <c>this[int index]</c>).</summary>
    private static LuaIndexerMetadata[] CollectInstanceIndexers(ImmutableArray<ISymbol> members, SymbolReferences references)
    {
        return members.OfType<IPropertySymbol>()
            .Where(p => p.IsIndexer && !p.IsStatic && p.Parameters.Length == 1 && !HasAttr(p, references.LuaHiddenAttribute))
            .Where(p => GetResultantVisibility(p) == SymbolVisibility.Public)
            .Select(p => new LuaIndexerMetadata(p, references))
            .ToArray();
    }

    private static LuaFieldMetadata[] CollectFields(ImmutableArray<ISymbol> members, SymbolReferences references, bool isStatic)
    {
        return members.OfType<IFieldSymbol>()
            .Where(f => f.IsStatic == isStatic && !f.IsImplicitlyDeclared && !HasAttr(f, references.LuaHiddenAttribute))
            .Where(m => GetResultantVisibility(m) == SymbolVisibility.Public)
            .Select(f => new LuaFieldMetadata(f, references))
            .ToArray();
    }

    private static LuaConstructorMetadata[] CollectConstructors(INamedTypeSymbol symbol, SymbolReferences references)
    {
        return symbol.InstanceConstructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public && !HasAttr(c, references.LuaHiddenAttribute))
            .Where(m => GetResultantVisibility(m) == SymbolVisibility.Public)
            .Select(c => new LuaConstructorMetadata(c, references))
            .ToArray();
    }
}

internal class LuaOperatorMetadata(IMethodSymbol s, SymbolReferences references) : LuaMethodMetadata(s, references)
{
    public bool IsUnary => Parameters.Length == 1;

    public string Operator => Name switch
    {
        "op_Addition" => "+",
        "op_Subtraction" => "-",
        "op_Multiply" => "*",
        "op_Division" => "/",
        "op_UnaryNegation" => "-",
        "op_Equality" => "==",
        "op_LessThan" => "<",
        "op_LessThanOrEqual" => "<=",
        "op_Modulus" => "%",
        _ => throw new ArgumentOutOfRangeException(nameof(Name), Name, "Unsupported operator")
    };

    public string MetamethodName => Name switch
    {
        "op_Addition" => Metamethods.Add,
        "op_Subtraction" => Metamethods.Sub,
        "op_Multiply" => Metamethods.Mul,
        "op_Division" => Metamethods.Div,
        "op_UnaryNegation" => Metamethods.Unm,
        "op_Equality" => Metamethods.Eq,
        "op_LessThan" => Metamethods.Lt,
        "op_LessThanOrEqual" => Metamethods.Le,
        "op_Modulus" => Metamethods.Mod,
        _ => throw new ArgumentOutOfRangeException(nameof(Name), Name, "Unsupported operator")
    };
}

internal sealed class LuaConstructorMetadata : LuaMethodMetadata
{
    public LuaConstructorMetadata(IMethodSymbol c, SymbolReferences references) : base(c, references)
    {
        IsInstanceConstructor = true;
    }
}

internal class LuaMethodMetadata
{
    public string Name { get; }
    public string LuaName { get; }
    public string? DeclaringTypeName { get; }
    public string ReturnTypeName { get; }
    public bool IsNullableReturnType { get; }
    public LuaParameterMetadata[] Parameters { get; }
    /// <summary>Full type name of the type that actually implements this method (base class or interface).</summary>
    public string? ImplementationSourceType { get; }
    /// <summary>Full type name of the type being code-generated (for IsInherited comparison).</summary>
    private string? GeneratingTypeFullName { get; }
    /// <summary>True if this method is declared on a different type than the one being generated (virtual override, interface impl, or interface inheritance).</summary>
    public bool IsInherited => ImplementationSourceType != null && GeneratingTypeFullName != null
        && ImplementationSourceType != GeneratingTypeFullName;

    public BaseLuaTypeMetadata? DeclaringType { get; }
    public BaseLuaTypeMetadata ReturnType { get; }

    public bool IsStatic { get; }
    public bool IsVoid { get; }
    public bool IsInstanceConstructor { get; protected set; }

    public long OverloadPriority { get; }

    public LuaMethodMetadata(IMethodSymbol s, SymbolReferences references, ITypeSymbol? owningType = null)
    {
        Name = s.Name;
        DeclaringTypeName = s.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        GeneratingTypeFullName = owningType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        ReturnTypeName = s.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        IsNullableReturnType = s.ReturnType.IsReferenceType && s.ReturnType.NullableAnnotation == NullableAnnotation.Annotated;
        var attr = s.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(references.LuaNameAttribute, a.AttributeClass));
        LuaName = attr?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.MethodKind == MethodKind.Constructor ? "new" : Camel(Name));
        Parameters = s.Parameters.Select(p => new LuaParameterMetadata(p, references)).ToArray();
        ReturnType = new BaseLuaTypeMetadata(s.ReturnType, references);
        DeclaringType = s.ContainingType != null ? new BaseLuaTypeMetadata(s.ContainingType, references) : null;
        IsStatic = s.IsStatic;
        IsVoid = s.ReturnsVoid;
        IsInstanceConstructor = false;
        OverloadPriority = s.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(references.LuaOverloadPriorityAttribute, a.AttributeClass))
            ?.ConstructorArguments.FirstOrDefault().Value as long? ?? 1;

        // Determine the actual implementation source (for method deduplication)
        if (owningType != null && !SymbolEqualityComparer.Default.Equals(s.ContainingType, owningType))
        {
            // This method is declared on a different type — check if it's an override or interface impl
            if (s.IsOverride && s.OverriddenMethod != null)
            {
                ImplementationSourceType = s.OverriddenMethod.ContainingType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else
            {
                // Interface implementation — find which interface declares it
                foreach (var iface in owningType.AllInterfaces)
                {
                    var interfaceMember = iface.GetMembers().OfType<IMethodSymbol>()
                        .FirstOrDefault(im => im.Name == s.Name && im.Parameters.Length == s.Parameters.Length);
                    var impl = interfaceMember != null ? owningType.FindImplementationForInterfaceMember(interfaceMember) : null;
                    if (impl != null && SymbolEqualityComparer.Default.Equals(impl, s))
                    {
                        ImplementationSourceType = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        break;
                    }
                }
            }
        }
        ImplementationSourceType ??= DeclaringTypeName;
    }

    private static string Camel(string n) => n.Length > 0 ? char.ToLowerInvariant(n[0]) + n[1..] : n;
}

internal sealed class LuaParameterMetadata(IParameterSymbol p, SymbolReferences references)
{
    public string Name { get; } = p.Name;
    public string TypeName { get; } = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public bool IsNullableReferenceType { get; } = p.Type.IsReferenceType && p.Type.NullableAnnotation == NullableAnnotation.Annotated;
    public bool IsNullableValueType { get; } = p.Type.Name.EndsWith("?");

    public BaseLuaTypeMetadata Type { get; } = new BaseLuaTypeMetadata(p.Type, references);
}

internal class LuaPropertyMetadata(IPropertySymbol s, SymbolReferences references)
{
    public string Name { get; } = s.Name;
    public string PropertyTypeName { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public BaseLuaTypeMetadata PropertyType { get; } = new BaseLuaTypeMetadata(s.Type, references);
    public bool HasGetter { get; } = s.GetMethod != null;
    public bool HasSetter { get; } = s.SetMethod != null && !s.SetMethod.IsInitOnly;
    public bool IsNullableReferenceType { get; } = s.Type.IsReferenceType && s.Type.NullableAnnotation == NullableAnnotation.Annotated;
    public bool IsNullableValueType { get; } = s.Type.Name.EndsWith("?");
    public string LuaName { get; } = s
        .GetAttributes()
        .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, references.LuaNameAttribute))
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}

internal sealed class LuaIndexerMetadata(IPropertySymbol s, SymbolReferences references) : LuaPropertyMetadata(s, references)
{
    /// <summary>The single index key parameter.</summary>
    public LuaParameterMetadata Key { get; } = new(s.Parameters[0], references);
    public string KeyTypeName { get; } = s.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}

internal sealed class LuaFieldMetadata(IFieldSymbol s, SymbolReferences references)
{
    public string Name { get; } = s.Name;
    public string FieldTypeName { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public BaseLuaTypeMetadata FieldType { get; } = new BaseLuaTypeMetadata(s.Type, references);
    public bool IsReadOnly { get; } = s.IsReadOnly || s.IsConst;
    public bool IsNullableReferenceType { get; } = s.Type.IsReferenceType && s.Type.NullableAnnotation == NullableAnnotation.Annotated;
    public bool IsNullableValueType { get; } = s.Type.Name.EndsWith("?");
    public string LuaName { get; } = s
        .GetAttributes()
        .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, references.LuaNameAttribute))
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}

internal sealed class LuaEnumMemberMetadata(IFieldSymbol s, SymbolReferences references)
{
    public string Name { get; } = s.Name;
    public string FieldTypeName { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public BaseLuaTypeMetadata FieldType { get; } = new BaseLuaTypeMetadata(s.Type, references);
    public string LuaName { get; } = s
        .GetAttributes()
        .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, references.LuaNameAttribute))
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
    public object? ConstantValue { get; } = s.ConstantValue;
}
