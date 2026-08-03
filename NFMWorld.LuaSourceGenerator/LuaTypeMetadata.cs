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
    public bool IsInterface { get; }
    public bool IsInlineArray { get; }
    public int? InlineArrayLength { get; }
    public string? InlineArrayElementType { get; }
    /// <summary>True if this type needs StructUserData wrapping (sealed BCL, generic, or doesn't implement ILuaUserData).</summary>
    public bool IsExternal { get; }

    public LuaMethodMetadata[] InstanceMethods { get; }
    public LuaPropertyMetadata[] InstanceProperties { get; }
    public LuaFieldMetadata[] InstanceFields { get; }
    public LuaEventMetadata[] InstanceEvents { get; }
    public LuaMethodMetadata[] Operators { get; }

    public LuaTypeMetadata(INamedTypeSymbol symbol, SymbolReferences references, Compilation compilation)
    {
        Symbol = symbol;
        TypeName = symbol.Name;
        FullTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
        Namespace = symbol.ContainingNamespace?.ToDisplayString();
        IsStatic = symbol.IsStatic;
        IsValueType = symbol.IsValueType;
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
        IsExternal = symbol.IsSealed || isConstructedGeneric
            || FullTypeName.StartsWith("System.");
        IsCandidate = !isArray && !isRefStruct && !IsStatic && !IsExternal && !isOpenGeneric
            && FullTypeName != "System.Object";

        var hiddenAttr = references.LuaHiddenAttribute;
        var members = symbol.GetMembers();

        InstanceMethods = CollectMethods(members, hiddenAttr, isStatic: false, symbol);
        Operators = members.OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.UserDefinedOperator && !HasAttr(m, hiddenAttr))
            .Select(m => new LuaMethodMetadata(m, isExtension: false)).ToArray();
        InstanceProperties = CollectProperties(members, hiddenAttr, isStatic: false);
        InstanceFields = CollectFields(members, hiddenAttr, isStatic: false);
        InstanceEvents = CollectEvents(members, hiddenAttr, isStatic: false);
    }

    private LuaMethodMetadata[] CollectMethods(System.Collections.Immutable.ImmutableArray<ISymbol> members, INamedTypeSymbol? hiddenAttr, bool isStatic, INamedTypeSymbol? owningType = null)
    {
        return members.OfType<IMethodSymbol>()
            .Where(m => m.IsStatic == isStatic && m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared && !HasAttr(m, hiddenAttr))
            .Where(m => !m.Parameters.Any(p => p.RefKind != RefKind.None)) // skip ref/out/in parameters
            .Select(m => new LuaMethodMetadata(m, isExtension: m.IsExtensionMethod, owningType)).ToArray();
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
}

internal sealed class LuaMethodMetadata
{
    public IMethodSymbol Symbol { get; }
    public string Name { get; }
    public string LuaName { get; }
    public bool IsExtension { get; }
    public string? DeclaringType { get; }
    public string ReturnType { get; }
    public LuaParameterMetadata[] Parameters { get; }
    /// <summary>Full type name of the type that actually implements this method (base class or interface).</summary>
    public string? ImplementationSourceType { get; }
    /// <summary>True if this method is declared on a different type than the current one (virtual override or interface impl).</summary>
    public bool IsInherited => ImplementationSourceType != null && ImplementationSourceType != DeclaringType;

    public LuaMethodMetadata(IMethodSymbol s, bool isExtension, INamedTypeSymbol? owningType = null)
    {
        Symbol = s;
        Name = s.Name;
        IsExtension = isExtension;
        DeclaringType = s.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?.Replace("global::", "");
        ReturnType = s.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
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
    public string Type { get; } = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        .Replace("global::", "");
    /// <summary>True if this type needs StructUserData wrapping (not ILuaUserData, not primitive, not FixedMath).</summary>
    public bool NeedsStructUserData => !IsPrimitiveOrSpecial(Type);
    
    private static bool IsPrimitiveOrSpecial(string t) => t switch
    {
        "int" or "long" or "float" or "double" or "bool" or "string" or "object" or "void"
            or "byte" or "sbyte" or "short" or "ushort" or "uint" or "ulong" or "decimal"
            => true,
        _ => t.Contains("Fixed64") || t.Contains("Vector3d") || t.Contains("f64AngleSingle") || t.Contains("f64Euler")
    };
}

internal sealed class LuaPropertyMetadata(IPropertySymbol s)
{
    public string Name { get; } = s.Name;
    public string PropertyType { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public bool HasGetter { get; } = s.GetMethod != null;
    public bool HasSetter { get; } = s.SetMethod != null;
    public string LuaName { get; } = s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "LuaNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}

internal sealed class LuaFieldMetadata(IFieldSymbol s)
{
    public string Name { get; } = s.Name;
    public string FieldType { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public bool IsReadOnly { get; } = s.IsReadOnly;
    public string LuaName { get; } = s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "LuaNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}

internal sealed class LuaEventMetadata(IEventSymbol s)
{
    public string Name { get; } = s.Name;
    public string HandlerType { get; } = s.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public string LuaName { get; } = s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "LuaNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string ?? (s.Name.Length > 0 ? char.ToLowerInvariant(s.Name[0]) + s.Name[1..] : s.Name);
}
