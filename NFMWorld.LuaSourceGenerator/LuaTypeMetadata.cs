using Microsoft.CodeAnalysis;

namespace NFMWorld.LuaSourceGenerator;

internal sealed class LuaTypeMetadata
{
    public INamedTypeSymbol Symbol { get; }
    public bool IsCandidate { get; }
    public string TypeName { get; }
    public string FullTypeName { get; }
    public string? Namespace { get; }
    public string LuaName { get; }
    public bool IsStatic { get; }
    public bool IsValueType { get; }
    public bool IsRecord { get; }
    public bool IsInterface { get; }
    public bool IsInlineArray { get; }
    public int? InlineArrayLength { get; }
    public string? InlineArrayElementType { get; }
    /// <summary>True if this type needs StructUserData wrapping (sealed BCL, generic, or doesn't implement ILuaUserData).</summary>
    public bool IsExternal { get; }
    /// <summary>Base type full name (without global::), or null if System.Object/ValueType.</summary>
    public string? BaseTypeFullName { get; }
    /// <summary>Implemented interface full names (without global::).</summary>
    public string[] InterfaceFullNames { get; }
    /// <summary>True if the type has required properties/fields (no constructor should be generated).</summary>
    public bool HasRequiredMembers { get; }

    public LuaMethodMetadata[] InstanceMethods { get; private set; }
    public LuaPropertyMetadata[] InstanceProperties { get; private set; }
    public LuaFieldMetadata[] InstanceFields { get; private set; }
    public LuaEventMetadata[] InstanceEvents { get; private set; }
    public LuaMethodMetadata[] Operators { get; }
    public LuaMethodMetadata[] StaticMethods { get; }
    public LuaPropertyMetadata[] StaticProperties { get; }
    public LuaFieldMetadata[] StaticFields { get; }
    public LuaEventMetadata[] StaticEvents { get; }
    public LuaConstructorMetadata[] Constructors { get; }

    public LuaTypeMetadata(INamedTypeSymbol symbol, SymbolReferences references, Compilation compilation)
    {
        Symbol = symbol;
        TypeName = symbol.Name;
        FullTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
        Namespace = symbol.ContainingNamespace?.ToDisplayString();
        IsStatic = symbol.IsStatic;
        IsValueType = symbol.IsValueType;
        IsRecord = symbol.IsRecord;
        IsInterface = symbol.TypeKind == TypeKind.Interface;

        var luaVisibleAttr = GetAttr(symbol, references.LuaVisibleAttribute);
        LuaName = luaVisibleAttr?.ConstructorArguments.FirstOrDefault().Value as string
            ?? luaVisibleAttr?.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Name").Value.Value as string
            ?? TypeName;
        var luaNameAttr = GetAttr(symbol, references.LuaNameAttribute);
        if (luaNameAttr?.ConstructorArguments.FirstOrDefault().Value is string nameOverride)
            LuaName = nameOverride;

        var inlineAttr = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "InlineArrayAttribute");
        if (inlineAttr != null)
        {
            IsInlineArray = true;
            InlineArrayLength = (int)(inlineAttr.ConstructorArguments.FirstOrDefault().Value ?? 0);
            var firstField = symbol.GetMembers().OfType<IFieldSymbol>().FirstOrDefault();
            InlineArrayElementType = firstField?.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        var isArray = symbol.TypeKind == TypeKind.Array;
        var isRefStruct = symbol.IsValueType && symbol.IsRefLikeType;
        var isConstructedGeneric = symbol.IsGenericType && symbol.TypeArguments.Length > 0
            && !symbol.IsDefinition; // List<int>, not List<>
        var isOpenGeneric = symbol.IsGenericType && symbol.IsDefinition; // List<>, GenericWrapper<>
        IsExternal = (symbol.IsSealed && !IsStatic && !IsValueType) || isConstructedGeneric
            || FullTypeName.StartsWith("System.");
        // Static classes are candidates too (they get a type table only)
        IsCandidate = !isArray && !isRefStruct && !IsExternal && !isOpenGeneric
            && FullTypeName != "System.Object";

        // Base type and interfaces for stub generators
        var bt = symbol.BaseType;
        if (bt != null && bt.SpecialType != SpecialType.System_Object && bt.SpecialType != SpecialType.System_ValueType)
            BaseTypeFullName = bt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
        InterfaceFullNames = symbol.AllInterfaces
            .Where(i => i.DeclaredAccessibility == Accessibility.Public)
            .Select(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""))
            .ToArray();

        var hiddenAttr = references.LuaHiddenAttribute;
        var members = symbol.GetMembers();

        InstanceMethods = CollectMethods(members, hiddenAttr, isStatic: false, symbol);
        Operators = members.OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.UserDefinedOperator && !HasAttr(m, hiddenAttr))
            .Select(m => new LuaMethodMetadata(m, isExtension: false)).ToArray();
        InstanceProperties = CollectProperties(members, hiddenAttr, isStatic: false);
        InstanceFields = CollectFields(members, hiddenAttr, isStatic: false);
        InstanceEvents = CollectEvents(members, hiddenAttr, isStatic: false);
        StaticMethods = CollectMethods(members, hiddenAttr, isStatic: true, symbol);
        StaticProperties = CollectProperties(members, hiddenAttr, isStatic: true);
        StaticFields = CollectFields(members, hiddenAttr, isStatic: true);
        StaticEvents = CollectEvents(members, hiddenAttr, isStatic: true);
        Constructors = (IsStatic || HasAnyRequiredMembers(symbol))
            ? System.Array.Empty<LuaConstructorMetadata>()
            : CollectConstructors(symbol, hiddenAttr);
        HasRequiredMembers = HasAnyRequiredMembers(symbol);

        // For interfaces, also collect members inherited from base interfaces
        if (IsInterface)
            CollectInheritedInterfaceMembers(symbol, hiddenAttr, references);

        // Assign overload suffixes to constructors (all are "new" overloads)
        if (Constructors.Length > 1)
        {
            var first = true;
            foreach (var c in Constructors)
            {
                if (first) { first = false; continue; }
                c.OverloadSuffix = "_" + string.Join("_", c.Parameters.Select(p => ParamSuffix(p)));
            }
        }
    }

    /// <summary>Collect members inherited from base interfaces (for interface types only).</summary>
    private void CollectInheritedInterfaceMembers(INamedTypeSymbol symbol, INamedTypeSymbol? hiddenAttr, SymbolReferences references)
    {
        var luaVisibleAttr = references.LuaVisibleAttribute;
        var seenLuaNames = new HashSet<string>(
            InstanceProperties.Select(p => p.LuaName)
            .Concat(InstanceFields.Select(f => f.LuaName))
            .Concat(InstanceMethods.Select(m => m.FullLuaName))
            .Concat(InstanceEvents.Select(e => e.LuaName))
        );

        var inheritedProps = new List<LuaPropertyMetadata>();
        var inheritedFields = new List<LuaFieldMetadata>();
        var inheritedMethods = new List<LuaMethodMetadata>();
        var inheritedEvents = new List<LuaEventMetadata>();

        // Walk all base interfaces recursively
        WalkBaseInterfaces(symbol, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));

        void WalkBaseInterfaces(INamedTypeSymbol iface, HashSet<INamedTypeSymbol> visited)
        {
            foreach (var baseIface in iface.Interfaces)
            {
                if (!visited.Add(baseIface)) continue;
                CollectFromInterface(baseIface);
                WalkBaseInterfaces(baseIface, visited);
            }
        }

        void CollectFromInterface(INamedTypeSymbol baseIface)
        {
            var baseMembers = baseIface.GetMembers();
            var isBaseLuaVisible = HasAttr(baseIface, luaVisibleAttr);

            foreach (var m in baseMembers.OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared && !HasAttr(m, hiddenAttr))
                .Where(m => !m.Parameters.Any(p => p.RefKind != RefKind.None))
                .Where(m => !m.Parameters.Any(p => p.Type.IsRefLikeType))
                .Where(m => !m.ReturnType.IsRefLikeType))
            {
                var meta = new LuaMethodMetadata(m, isExtension: false, owningType: symbol);
                if (seenLuaNames.Add(meta.FullLuaName))
                    inheritedMethods.Add(meta);
            }

            foreach (var p in baseMembers.OfType<IPropertySymbol>()
                .Where(p => !p.IsIndexer && !p.IsImplicitlyDeclared && !HasAttr(p, hiddenAttr))
                .Where(p => !p.IsStatic))
            {
                var meta = new LuaPropertyMetadata(p);
                if (seenLuaNames.Add(meta.LuaName))
                    inheritedProps.Add(meta);
            }

            foreach (var f in baseMembers.OfType<IFieldSymbol>()
                .Where(f => !f.IsImplicitlyDeclared && !HasAttr(f, hiddenAttr) && !f.IsStatic))
            {
                var meta = new LuaFieldMetadata(f);
                if (seenLuaNames.Add(meta.LuaName))
                    inheritedFields.Add(meta);
            }

            foreach (var e in baseMembers.OfType<IEventSymbol>()
                .Where(ev => !ev.IsImplicitlyDeclared && !HasAttr(ev, hiddenAttr) && !ev.IsStatic))
            {
                var meta = new LuaEventMetadata(e);
                if (seenLuaNames.Add(meta.LuaName))
                    inheritedEvents.Add(meta);
            }
        }

        // Merge inherited members into the type's own lists
        InstanceProperties = [.. InstanceProperties, .. inheritedProps];
        InstanceFields = [.. InstanceFields, .. inheritedFields];
        InstanceMethods = [.. InstanceMethods, .. inheritedMethods];
        InstanceEvents = [.. InstanceEvents, .. inheritedEvents];

        // Reassign overload suffixes for methods (new inherited methods may create overload groups)
        foreach (var group in InstanceMethods.GroupBy(m => m.LuaName).Where(g => g.Count() > 1))
        {
            var first = true;
            foreach (var m in group)
            {
                if (first) { first = false; continue; }
                // Only add suffix if not already set (own overloads already have suffixes from CollectMethods)
                if (m.OverloadSuffix.Length == 0)
                    m.OverloadSuffix = "_" + string.Join("_", m.Parameters.Select(p => ParamSuffix(p)));
            }
        }
    }

    private static bool HasAnyRequiredMembers(INamedTypeSymbol symbol)
    {
        return symbol.GetMembers().Any(m =>
            (m is IPropertySymbol p && p.IsRequired) ||
            (m is IFieldSymbol f && f.IsRequired));
    }

    private LuaMethodMetadata[] CollectMethods(System.Collections.Immutable.ImmutableArray<ISymbol> members, INamedTypeSymbol? hiddenAttr, bool isStatic, INamedTypeSymbol? owningType = null)
    {
        var methods = members.OfType<IMethodSymbol>()
            .Where(m => m.IsStatic == isStatic && m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared && !HasAttr(m, hiddenAttr))
            .Where(m => !m.Parameters.Any(p => p.RefKind != RefKind.None)) // skip ref/out/in parameters
            .Where(m => !m.Parameters.Any(p => p.Type.IsRefLikeType)) // skip Span/ReadOnlySpan params (ref structs)
            .Where(m => !m.ReturnType.IsRefLikeType) // skip methods returning ref structs
            .Select(m => new LuaMethodMetadata(m, isExtension: m.IsExtensionMethod, owningType)).ToArray();

        // Assign overload suffixes: for groups with >1 method sharing the same LuaName,
        // the first keeps the base name (no suffix), subsequent get a parameter-type-based suffix.
        foreach (var group in methods.GroupBy(m => m.LuaName).Where(g => g.Count() > 1))
        {
            var first = true;
            foreach (var m in group)
            {
                if (first) { first = false; continue; }
                m.OverloadSuffix = "_" + string.Join("_", m.Parameters.Select(p => ParamSuffix(p)));
            }
        }

        return methods;
    }

    /// <summary>Short Lua-friendly type name for overload suffix generation.</summary>
    private static string ParamSuffix(LuaParameterMetadata p)
    {
        var typeName = p.Type;
        // Detect arrays: "int[]" → "intArray", "float[,]" → "floatArray"
        var isArray = typeName.EndsWith("]");
        // Detect nullable: strip ? for suffix purposes, append "n" marker
        var isNullable = typeName.EndsWith("?") && !isArray;
        var cleanType = isNullable ? typeName.Substring(0, typeName.Length - 1) : typeName;
        // For StructUserData-wrapped types, unwrap to short type name
        var baseSuffix = p.NeedsStructUserData
            ? CamelCase(ShortTypeName(cleanType))
            : ParamSuffixFromTypeName(cleanType);
        if (isNullable) baseSuffix += "n";
        return isArray ? baseSuffix + "Array" : baseSuffix;
    }

    /// <summary>Short type suffix for a C# type name string (handles primitives, nullables, generics).</summary>
    private static string ParamSuffixFromTypeName(string typeName)
    {
        // Map primitives to short names
        var simple = typeName switch
        {
            "int" => "int", "long" => "long", "float" => "flt", "double" => "dbl",
            "bool" => "bool", "string" => "str", "byte" => "byte", "sbyte" => "sbyte",
            "short" => "short", "ushort" => "ushort", "uint" => "uint", "ulong" => "ulong",
            "decimal" => "dec", "char" => "char", "object" => "obj",
            _ => null
        };
        if (simple != null) return simple;
        // For nullable types, unwrap: "int?" → "intn" (nullable)
        if (typeName.EndsWith("?")) return ParamSuffixFromTypeName(typeName.Substring(0, typeName.Length - 1)) + "n";
        // Fallback: use the short type name
        return CamelCase(ShortTypeName(typeName));
    }

    private static string CamelCase(string n) => n.Length > 0 ? char.ToLowerInvariant(n[0]) + n[1..] : n;

    private static string ShortTypeName(string fullName)
    {
        // Handle tuples: "(bool, bool)" → "Tuple"
        if (fullName.StartsWith("("))
        {
            // Count commas to differentiate: "(bool,bool)" vs "(bool,bool,bool)"
            var commas = fullName.Count(c => c == ',');
            return $"Tuple{commas + 1}";
        }
        // Strip generic args for the base name: "System.Collections.Generic.List<int>" → "List"
        var name = fullName.Contains('<') ? fullName.Substring(0, fullName.IndexOf('<')) : fullName;
        // Take last segment after '.'
        var lastDot = name.LastIndexOf('.');
        var shortName = lastDot >= 0 ? name.Substring(lastDot + 1) : name;
        // Sanitize: replace invalid C# identifier chars for use in function names
        return shortName.Replace("[", "").Replace("]", "").Replace(",", "_").Replace("*", "Ptr");
    }

    private LuaPropertyMetadata[] CollectProperties(System.Collections.Immutable.ImmutableArray<ISymbol> members, INamedTypeSymbol? hiddenAttr, bool isStatic)
    {
        return members.OfType<IPropertySymbol>()
            .Where(p => p.IsStatic == isStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && !HasAttr(p, hiddenAttr))
            .Select(p => new LuaPropertyMetadata(p)).ToArray();
    }

    private LuaFieldMetadata[] CollectFields(System.Collections.Immutable.ImmutableArray<ISymbol> members, INamedTypeSymbol? hiddenAttr, bool isStatic)
    {
        return members.OfType<IFieldSymbol>()
            .Where(f => f.IsStatic == isStatic && !f.IsImplicitlyDeclared && !HasAttr(f, hiddenAttr))
            .Select(f => new LuaFieldMetadata(f)).ToArray();
    }

    private LuaEventMetadata[] CollectEvents(System.Collections.Immutable.ImmutableArray<ISymbol> members, INamedTypeSymbol? hiddenAttr, bool isStatic)
    {
        return members.OfType<IEventSymbol>()
            .Where(e => e.IsStatic == isStatic && !e.IsImplicitlyDeclared && !HasAttr(e, hiddenAttr))
            .Select(e => new LuaEventMetadata(e)).ToArray();
    }

    private static bool HasAttr(ISymbol s, INamedTypeSymbol? attr)
        => attr != null && s.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr));

    private static AttributeData? GetAttr(ISymbol s, INamedTypeSymbol? attr)
        => attr != null ? s.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr)) : null;

    private static LuaConstructorMetadata[] CollectConstructors(INamedTypeSymbol symbol, INamedTypeSymbol? hiddenAttr)
    {
        return symbol.Constructors
            .Where(c => !c.IsImplicitlyDeclared && !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public && !HasAttr(c, hiddenAttr))
            .Select(c => new LuaConstructorMetadata(c)).ToArray();
    }
}

internal sealed class LuaConstructorMetadata
{
    public LuaParameterMetadata[] Parameters { get; }
    /// <summary>Suffix for overload disambiguation (e.g. "_int_string"). Empty for first/only constructor.</summary>
    public string OverloadSuffix { get; set; } = "";
    /// <summary>Full Lua-visible name: "new" + overload suffix.</summary>
    public string FullLuaNew => OverloadSuffix.Length > 0 ? "new" + OverloadSuffix : "new";
    public LuaConstructorMetadata(IMethodSymbol c)
    {
        Parameters = c.Parameters.Select(p => new LuaParameterMetadata(p)).ToArray();
    }
}

internal sealed class LuaMethodMetadata
{
    public IMethodSymbol Symbol { get; }
    public string Name { get; }
    public string LuaName { get; }
    public bool IsExtension { get; }
    public string? DeclaringType { get; }
    public string ReturnType { get; }
    public bool IsNullableReturnType { get; }
    public LuaParameterMetadata[] Parameters { get; }
    /// <summary>Full type name of the type that actually implements this method (base class or interface).</summary>
    public string? ImplementationSourceType { get; }
    /// <summary>Full type name of the type being code-generated (for IsInherited comparison).</summary>
    private string? GeneratingTypeFullName { get; }
    /// <summary>True if this method is declared on a different type than the one being generated (virtual override, interface impl, or interface inheritance).</summary>
    public bool IsInherited => ImplementationSourceType != null && GeneratingTypeFullName != null
        && ImplementationSourceType != GeneratingTypeFullName;
    /// <summary>Suffix for overload disambiguation (e.g. "_int", "_string_int"). Empty for non-overloaded or first overload.</summary>
    public string OverloadSuffix { get; set; } = "";
    /// <summary>Full Lua-visible name including overload suffix.</summary>
    public string FullLuaName => OverloadSuffix.Length > 0 ? LuaName + OverloadSuffix : LuaName;

    public LuaMethodMetadata(IMethodSymbol s, bool isExtension, INamedTypeSymbol? owningType = null)
    {
        Symbol = s;
        Name = s.Name;
        IsExtension = isExtension;
        DeclaringType = s.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?.Replace("global::", "");
        GeneratingTypeFullName = owningType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?.Replace("global::", "");
        ReturnType = s.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
        IsNullableReturnType = s.ReturnType.IsReferenceType && s.ReturnType.NullableAnnotation == NullableAnnotation.Annotated;
        var attr = s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "LuaNameAttribute");
        LuaName = attr?.ConstructorArguments.FirstOrDefault().Value as string ?? Camel(Name);
        Parameters = s.Parameters.Select(p => new LuaParameterMetadata(p)).ToArray();

        // Determine the actual implementation source (for method deduplication)
        if (owningType != null && !SymbolEqualityComparer.Default.Equals(s.ContainingType, owningType))
        {
            // This method is declared on a different type — check if it's an override or interface impl
            if (s.IsOverride && s.OverriddenMethod != null)
            {
                ImplementationSourceType = s.OverriddenMethod.ContainingType
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", "");
            }
            else
            {
                // Interface implementation — find which interface declares it
                foreach (var iface in owningType.AllInterfaces)
                {
                    var impl = owningType.FindImplementationForInterfaceMember(
                        iface.GetMembers().OfType<IMethodSymbol>()
                            .FirstOrDefault(im => im.Name == s.Name && im.Parameters.Length == s.Parameters.Length));
                    if (impl != null && SymbolEqualityComparer.Default.Equals(impl, s))
                    {
                        ImplementationSourceType = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                            .Replace("global::", "");
                        break;
                    }
                }
            }
        }
        ImplementationSourceType ??= DeclaringType;
    }

    private static string Camel(string n) => n.Length > 0 ? char.ToLowerInvariant(n[0]) + n[1..] : n;
}

internal sealed class LuaParameterMetadata(IParameterSymbol p)
{
    public string Name { get; } = p.Name;
    public string Type { get; } = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        .Replace("global::", "");
    public bool IsNullableReferenceType { get; } = p.Type.IsReferenceType && p.Type.NullableAnnotation == NullableAnnotation.Annotated;
    /// <summary>True if this type needs StructUserData wrapping (not ILuaUserData, not primitive, not FixedMath).</summary>
    public bool NeedsStructUserData => !IsPrimitiveOrSpecial(Type);

    private static bool IsPrimitiveOrSpecial(string t) => t switch
    {
        "int" or "long" or "float" or "double" or "bool" or "string" or "object" or "void"
            or "byte" or "sbyte" or "short" or "ushort" or "uint" or "ulong" or "decimal"
            => true,
        // Nullable value types are marshalled as "value or nil", not StructUserData
        "int?" or "long?" or "float?" or "double?" or "bool?"
            or "byte?" or "sbyte?" or "short?" or "ushort?" or "uint?" or "ulong?" or "decimal?" or "char?"
            => true,
        _ => IsFixedMathBaseType(t) || IsSpanType(t)
    };

    private static bool IsSpanType(string t)
    {
        var baseT = t.Contains('<') ? t.Substring(0, t.IndexOf('<')) : t;
        return baseT is "System.Span" or "Span" or "System.ReadOnlySpan" or "ReadOnlySpan";
    }

    private static bool IsFixedMathBaseType(string t)
    {
        var clean = t.EndsWith("?") ? t.Substring(0, t.Length - 1) : t;
        var baseT = clean.Contains('<') ? clean.Substring(0, clean.IndexOf('<')) : clean;
        return baseT is "Fixed64" or "Vector3d" or "f64AngleSingle" or "Fixed4x4"
            || baseT.EndsWith(".Fixed64") || baseT.EndsWith(".Vector3d")
            || baseT.EndsWith(".f64AngleSingle")
            || baseT.EndsWith(".Fixed4x4");
    }
}

internal sealed class LuaPropertyMetadata(IPropertySymbol s)
{
    public string Name { get; } = s.Name;
    public string PropertyType { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
    public bool HasGetter { get; } = s.GetMethod != null;
    public bool HasSetter { get; } = s.SetMethod != null && !s.SetMethod.IsInitOnly;
    public bool IsNullableReferenceType { get; } = s.Type.IsReferenceType && s.Type.NullableAnnotation == NullableAnnotation.Annotated;
    public string LuaName { get; } = s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "LuaNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}

internal sealed class LuaFieldMetadata(IFieldSymbol s)
{
    public string Name { get; } = s.Name;
    public string FieldType { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
    public bool IsReadOnly { get; } = s.IsReadOnly || s.IsConst;
    public bool IsNullableReferenceType { get; } = s.Type.IsReferenceType && s.Type.NullableAnnotation == NullableAnnotation.Annotated;
    public string LuaName { get; } = s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "LuaNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}

internal sealed class LuaEventMetadata(IEventSymbol s)
{
    public string Name { get; } = s.Name;
    public string HandlerType { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
    public string LuaName { get; } = s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "LuaNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}
