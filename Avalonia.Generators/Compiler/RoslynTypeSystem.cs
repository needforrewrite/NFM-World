using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XamlX.TypeSystem;

namespace Avalonia.Generators.Compiler;

internal class RoslynTypeSystem : IXamlTypeSystem
{
    private readonly List<IXamlAssembly> _assemblies = new();
    private readonly ConcurrentDictionary<string, IXamlType?> _typeCache = new();

    public RoslynTypeSystem(Compilation compilation)
    {
        _assemblies.Add(new RoslynAssembly(compilation.Assembly));

        var assemblySymbols = compilation
            .References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .Select(assembly => new RoslynAssembly(assembly))
            .ToList();

        _assemblies.AddRange(assemblySymbols);
    }

    public IEnumerable<IXamlAssembly> Assemblies => _assemblies;

    public IXamlAssembly? FindAssembly(string name) =>
        Assemblies
            .FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    [UnconditionalSuppressMessage("Trimming", "IL2092", Justification = TrimmingMessages.Roslyn)]
    public IXamlType? FindType(string name) =>
        _typeCache.GetOrAdd(name, _ => _assemblies
            .Select(assembly => assembly.FindType(name))
            .FirstOrDefault(type => type != null));

    [UnconditionalSuppressMessage("Trimming", "IL2092", Justification = TrimmingMessages.Roslyn)]
    public IXamlType? FindType(string name, string assembly) =>
        _assemblies
            .Select(assemblyInstance => assemblyInstance.FindType(name))
            .FirstOrDefault(type => type != null);

    internal static IXamlType WrapType(ITypeSymbol symbol, RoslynAssembly assembly)
    {
        if (symbol is IArrayTypeSymbol arrayType)
            return new RoslynArrayType(arrayType, assembly);
        if (symbol is INamedTypeSymbol namedType)
            return new RoslynType(namedType, assembly);
        return XamlPseudoType.Unknown;
    }
}

internal class RoslynAssembly : IXamlAssembly
{
    private readonly IAssemblySymbol _symbol;

    public RoslynAssembly(IAssemblySymbol symbol) => _symbol = symbol;

    public bool Equals(IXamlAssembly other) =>
        other is RoslynAssembly roslynAssembly &&
        SymbolEqualityComparer.Default.Equals(_symbol, roslynAssembly._symbol);

    public string Name => _symbol.Name;

    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes =>
        _symbol.GetAttributes()
            .Select(data => new RoslynAttribute(data, this))
            .ToList();

    public IXamlType? FindType(string fullName)
    {
        var type = _symbol.GetTypeByMetadataName(fullName);
        return type is null ? null : new RoslynType(type, this);
    }
}

internal class RoslynAttribute : IXamlCustomAttribute
{
    private readonly AttributeData _data;
    private readonly RoslynAssembly _assembly;

    public RoslynAttribute(AttributeData data, RoslynAssembly assembly)
    {
        _data = data;
        _assembly = assembly;
    }

    public bool Equals(IXamlCustomAttribute other) =>
        other is RoslynAttribute attribute &&
        _data == attribute._data;

    public IXamlType Type => new RoslynType(_data.AttributeClass!, _assembly);

    public List<object?> Parameters =>
        _data.ConstructorArguments
            .Select(argument => argument.Kind == TypedConstantKind.Type && argument.Value is INamedTypeSymbol nts
                ? (object?)new RoslynType(nts, _assembly)
                : argument.Value)
            .ToList();

    public Dictionary<string, object?> Properties =>
        _data.NamedArguments.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Value);
}

internal class RoslynType : IXamlType
{
    private static readonly SymbolDisplayFormat NamespaceFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    private static readonly SymbolDisplayFormat MetadataNameFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    private readonly RoslynAssembly _assembly;
    private readonly INamedTypeSymbol _symbol;

    public RoslynType(INamedTypeSymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlType other) =>
        other is RoslynType roslynType &&
        SymbolEqualityComparer.Default.Equals(_symbol, roslynType._symbol);

    public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(_symbol);

    public object Id => SymbolEqualityComparer.Default.GetHashCode(_symbol);

    public string Name => _symbol.MetadataName;

    public string? Namespace
    {
        get
        {
            var ns = _symbol.ContainingNamespace;
            return ns is { IsGlobalNamespace: false } ? ns.ToDisplayString() : null;
        }
    }

    public string FullName
    {
        get
        {
            if (_symbol.ContainingType != null)
            {
                var declaring = new RoslynType(_symbol.ContainingType, _assembly);
                return declaring.FullName + "+" + _symbol.MetadataName;
            }
            return Namespace != null ? $"{Namespace}.{_symbol.MetadataName}" : _symbol.MetadataName;
        }
    }

    public IXamlAssembly Assembly => _assembly;

    public bool IsPublic => _symbol.DeclaredAccessibility == Accessibility.Public;

    public bool IsNestedPrivate => _symbol.DeclaredAccessibility == Accessibility.Private;

    public IXamlType? DeclaringType =>
        _symbol.ContainingType is { } containingType ? new RoslynType(containingType, _assembly) : null;

    public IReadOnlyList<IXamlProperty> Properties =>
        _symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsIndexer)
            .Select(property => (IXamlProperty)new RoslynProperty(property, _assembly))
            .ToList();

    public IReadOnlyList<IXamlEventInfo> Events =>
        _symbol.GetMembers()
            .OfType<IEventSymbol>()
            .Select(e => (IXamlEventInfo)new RoslynEvent(e, _assembly))
            .ToList();

    public IReadOnlyList<IXamlField> Fields =>
        _symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Select(f => (IXamlField)new RoslynField(f, _assembly))
            .ToList();

    public IReadOnlyList<IXamlMethod> Methods =>
        _symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation
                or MethodKind.PropertyGet or MethodKind.PropertySet
                or MethodKind.EventAdd or MethodKind.EventRemove)
            .Select(m => (IXamlMethod)new RoslynMethod(m, _assembly))
            .ToList();

    public IReadOnlyList<IXamlConstructor> Constructors =>
        _symbol.InstanceConstructors
            .Select(method => (IXamlConstructor)new RoslynConstructor(method, _assembly))
            .ToList();

    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes =>
        _symbol.GetAttributes()
            .Where(a => a.AttributeClass != null)
            .Select(a => (IXamlCustomAttribute)new RoslynAttribute(a, _assembly))
            .ToList();

    public IReadOnlyList<IXamlType> GenericArguments =>
        _symbol.TypeArguments
            .Select(ta => RoslynTypeSystem.WrapType(ta, _assembly))
            .ToList();

    public bool IsAssignableFrom(IXamlType type)
    {
        if (Equals(type))
            return true;
        if (type is not RoslynType rt)
            return false;
        var conversion = CSharpCompilation.Create("dummy").ClassifyCommonConversion(rt._symbol, _symbol);
        if (conversion.IsImplicit || conversion.IsIdentity)
            return true;
        // Fallback: walk hierarchy
        if (!_symbol.IsValueType)
        {
            for (var bt = rt._symbol.BaseType; bt != null; bt = bt.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(bt, _symbol))
                    return true;
            }
            foreach (var iface in rt._symbol.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, _symbol))
                    return true;
            }
        }
        return false;
    }

    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments)
    {
        if (_symbol.TypeParameters.Length == 0 || typeArguments.Count != _symbol.TypeParameters.Length)
            return this;
        var roslynArgs = typeArguments.Select(ta =>
        {
            if (ta is RoslynType rt) return rt._symbol;
            if (ta is RoslynArrayType rat) return rat.Symbol;
            return (ITypeSymbol)_symbol.TypeParameters[0]; // fallback
        }).ToArray();
        var constructed = _symbol.OriginalDefinition.Construct(roslynArgs);
        return new RoslynType(constructed, _assembly);
    }

    public IXamlType? GenericTypeDefinition =>
        _symbol.IsGenericType && !SymbolEqualityComparer.Default.Equals(_symbol, _symbol.OriginalDefinition)
            ? new RoslynType(_symbol.OriginalDefinition, _assembly)
            : null;

    public bool IsArray => false;

    public IXamlType? ArrayElementType => null;

    public IXamlType MakeArrayType(int dimensions) => new SyntheticArrayType(this);

    public IXamlType? BaseType =>
        _symbol.BaseType is { } baseType ? new RoslynType(baseType, _assembly) : null;

    public bool IsValueType => _symbol.IsValueType;

    public bool IsEnum => _symbol.TypeKind == TypeKind.Enum;

    public IReadOnlyList<IXamlType> Interfaces =>
        _symbol.AllInterfaces
            .Select(i => (IXamlType)new RoslynType(i, _assembly))
            .ToList();

    public bool IsInterface => _symbol.TypeKind == TypeKind.Interface;

    public IXamlType GetEnumUnderlyingType()
    {
        if (_symbol.EnumUnderlyingType is { } ut)
            return new RoslynType(ut, _assembly);
        throw new InvalidOperationException($"Type {FullName} is not an enum.");
    }

    public IReadOnlyList<IXamlType> GenericParameters =>
        _symbol.TypeParameters
            .Select(tp => (IXamlType)new RoslynTypeParameter(tp, _assembly))
            .ToList();

    public bool IsFunctionPointer => _symbol.TypeKind == TypeKind.FunctionPointer;

    internal INamedTypeSymbol Symbol => _symbol;
}

internal class RoslynArrayType : IXamlType
{
    private readonly IArrayTypeSymbol _symbol;
    private readonly RoslynAssembly _assembly;

    public RoslynArrayType(IArrayTypeSymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlType other) =>
        other is RoslynArrayType rat &&
        SymbolEqualityComparer.Default.Equals(_symbol, rat._symbol);

    public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(_symbol);

    public object Id => SymbolEqualityComparer.Default.GetHashCode(_symbol);
    public string Name => _symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    public string? Namespace => null;
    public string FullName => RoslynTypeSystem.WrapType(_symbol.ElementType, _assembly).FullName + "[]";
    public bool IsPublic => true;
    public bool IsNestedPrivate => false;
    public IXamlAssembly? Assembly => _assembly;
    public IReadOnlyList<IXamlProperty> Properties => Array.Empty<IXamlProperty>();
    public IReadOnlyList<IXamlEventInfo> Events => Array.Empty<IXamlEventInfo>();
    public IReadOnlyList<IXamlField> Fields => Array.Empty<IXamlField>();
    public IReadOnlyList<IXamlMethod> Methods => Array.Empty<IXamlMethod>();
    public IReadOnlyList<IXamlConstructor> Constructors => Array.Empty<IXamlConstructor>();
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();
    public IReadOnlyList<IXamlType> GenericArguments => Array.Empty<IXamlType>();
    public bool IsAssignableFrom(IXamlType type) => Equals(type);
    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments) => throw new NotSupportedException();
    public IXamlType? GenericTypeDefinition => null;
    public bool IsArray => true;
    public IXamlType? ArrayElementType => RoslynTypeSystem.WrapType(_symbol.ElementType, _assembly);
    public IXamlType MakeArrayType(int dimensions) => throw new NotSupportedException();
    public IXamlType? BaseType => null;
    public IXamlType? DeclaringType => null;
    public bool IsValueType => false;
    public bool IsEnum => false;
    public IReadOnlyList<IXamlType> Interfaces => Array.Empty<IXamlType>();
    public bool IsInterface => false;
    public IXamlType GetEnumUnderlyingType() => throw new InvalidOperationException();
    public IReadOnlyList<IXamlType> GenericParameters => Array.Empty<IXamlType>();
    public bool IsFunctionPointer => false;

    internal IArrayTypeSymbol Symbol => _symbol;
}

internal class RoslynTypeParameter : IXamlType
{
    private readonly ITypeParameterSymbol _symbol;
    private readonly RoslynAssembly _assembly;

    public RoslynTypeParameter(ITypeParameterSymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlType other) =>
        other is RoslynTypeParameter rtp &&
        SymbolEqualityComparer.Default.Equals(_symbol, rtp._symbol);

    public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(_symbol);

    public object Id => SymbolEqualityComparer.Default.GetHashCode(_symbol);
    public string Name => _symbol.Name;
    public string? Namespace => null;
    public string FullName => _symbol.Name;
    public bool IsPublic => true;
    public bool IsNestedPrivate => false;
    public IXamlAssembly? Assembly => _assembly;
    public IReadOnlyList<IXamlProperty> Properties => Array.Empty<IXamlProperty>();
    public IReadOnlyList<IXamlEventInfo> Events => Array.Empty<IXamlEventInfo>();
    public IReadOnlyList<IXamlField> Fields => Array.Empty<IXamlField>();
    public IReadOnlyList<IXamlMethod> Methods => Array.Empty<IXamlMethod>();
    public IReadOnlyList<IXamlConstructor> Constructors => Array.Empty<IXamlConstructor>();
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();
    public IReadOnlyList<IXamlType> GenericArguments => Array.Empty<IXamlType>();
    public bool IsAssignableFrom(IXamlType type) => Equals(type);
    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments) => throw new NotSupportedException();
    public IXamlType? GenericTypeDefinition => null;
    public bool IsArray => false;
    public IXamlType? ArrayElementType => null;
    public IXamlType MakeArrayType(int dimensions) => throw new NotSupportedException();
    public IXamlType? BaseType => null;
    public IXamlType? DeclaringType => null;
    public bool IsValueType => _symbol.HasValueTypeConstraint;
    public bool IsEnum => false;
    public IReadOnlyList<IXamlType> Interfaces => Array.Empty<IXamlType>();
    public bool IsInterface => false;
    public IXamlType GetEnumUnderlyingType() => throw new InvalidOperationException();
    public IReadOnlyList<IXamlType> GenericParameters => Array.Empty<IXamlType>();
    public bool IsFunctionPointer => false;

    internal ITypeParameterSymbol Symbol => _symbol;
}

/// <summary>
/// A synthetic array type created by MakeArrayType when we don't have
/// access to the Roslyn compilation to create a proper IArrayTypeSymbol.
/// </summary>
internal class SyntheticArrayType : IXamlType
{
    private readonly IXamlType _elementType;

    public SyntheticArrayType(IXamlType elementType)
    {
        _elementType = elementType;
    }

    public bool Equals(IXamlType other) =>
        other is SyntheticArrayType sat && _elementType.Equals(sat._elementType);

    public override int GetHashCode() => _elementType.GetHashCode() ^ 0x1234;

    public object Id => ("ArrayOf", _elementType.Id);
    public string Name => _elementType.Name + "[]";
    public string? Namespace => _elementType.Namespace;
    public string FullName => _elementType.FullName + "[]";
    public bool IsPublic => true;
    public bool IsNestedPrivate => false;
    public IXamlAssembly? Assembly => _elementType.Assembly;
    public IReadOnlyList<IXamlProperty> Properties => Array.Empty<IXamlProperty>();
    public IReadOnlyList<IXamlEventInfo> Events => Array.Empty<IXamlEventInfo>();
    public IReadOnlyList<IXamlField> Fields => Array.Empty<IXamlField>();
    public IReadOnlyList<IXamlMethod> Methods => Array.Empty<IXamlMethod>();
    public IReadOnlyList<IXamlConstructor> Constructors => Array.Empty<IXamlConstructor>();
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();
    public IReadOnlyList<IXamlType> GenericArguments => Array.Empty<IXamlType>();
    public bool IsAssignableFrom(IXamlType type) => Equals(type);
    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments) => throw new NotSupportedException();
    public IXamlType? GenericTypeDefinition => null;
    public bool IsArray => true;
    public IXamlType? ArrayElementType => _elementType;
    public IXamlType MakeArrayType(int dimensions) => throw new NotSupportedException();
    public IXamlType? BaseType => null;
    public IXamlType? DeclaringType => null;
    public bool IsValueType => false;
    public bool IsEnum => false;
    public IReadOnlyList<IXamlType> Interfaces => Array.Empty<IXamlType>();
    public bool IsInterface => false;
    public IXamlType GetEnumUnderlyingType() => throw new InvalidOperationException();
    public IReadOnlyList<IXamlType> GenericParameters => Array.Empty<IXamlType>();
    public bool IsFunctionPointer => false;
}

internal class RoslynConstructor : IXamlConstructor
{
    private readonly IMethodSymbol _symbol;
    private readonly RoslynAssembly _assembly;

    public RoslynConstructor(IMethodSymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlConstructor other) =>
        other is RoslynConstructor roslynConstructor &&
        SymbolEqualityComparer.Default.Equals(_symbol, roslynConstructor._symbol);

    public bool IsPublic => _symbol.DeclaredAccessibility == Accessibility.Public;

    public bool IsStatic => _symbol.IsStatic;

    public IReadOnlyList<IXamlType> Parameters =>
        _symbol.Parameters
            .Select(p => RoslynTypeSystem.WrapType(p.Type, _assembly))
            .ToList();

    public string Name => _symbol.MetadataName;

    public IXamlType DeclaringType => new RoslynType(_symbol.ContainingType, _assembly);

    public IXamlParameterInfo GetParameterInfo(int index) => new RoslynParameter(_assembly, _symbol.Parameters[index]);
}

internal class RoslynProperty : IXamlProperty
{
    private readonly IPropertySymbol _symbol;
    private readonly RoslynAssembly _assembly;

    public RoslynProperty(IPropertySymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlProperty other) =>
        other is RoslynProperty roslynProperty &&
        SymbolEqualityComparer.Default.Equals(_symbol, roslynProperty._symbol);

    public string Name => _symbol.Name;

    public IXamlType DeclaringType => new RoslynType(_symbol.ContainingType, _assembly);

    public IXamlType PropertyType => RoslynTypeSystem.WrapType(_symbol.Type, _assembly);

    public IXamlMethod? Getter => _symbol.GetMethod == null ? null : new RoslynMethod(_symbol.GetMethod, _assembly);

    public IXamlMethod? Setter => _symbol.SetMethod == null ? null : new RoslynMethod(_symbol.SetMethod, _assembly);

    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes =>
        _symbol.GetAttributes()
            .Where(a => a.AttributeClass != null)
            .Select(a => (IXamlCustomAttribute)new RoslynAttribute(a, _assembly))
            .ToList();

    public IReadOnlyList<IXamlType> IndexerParameters =>
        _symbol.Parameters
            .Select(p => RoslynTypeSystem.WrapType(p.Type, _assembly))
            .ToList();
}

internal class RoslynField : IXamlField
{
    private readonly IFieldSymbol _symbol;
    private readonly RoslynAssembly _assembly;

    public RoslynField(IFieldSymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlField other) =>
        other is RoslynField rf &&
        SymbolEqualityComparer.Default.Equals(_symbol, rf._symbol);

    public string Name => _symbol.Name;
    public IXamlType DeclaringType => new RoslynType(_symbol.ContainingType, _assembly);
    public IXamlType FieldType => RoslynTypeSystem.WrapType(_symbol.Type, _assembly);
    public bool IsPublic => _symbol.DeclaredAccessibility == Accessibility.Public;
    public bool IsStatic => _symbol.IsStatic;
    public bool IsLiteral => _symbol.HasConstantValue;

    public object GetLiteralValue() => _symbol.ConstantValue!;

    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes =>
        _symbol.GetAttributes()
            .Where(a => a.AttributeClass != null)
            .Select(a => (IXamlCustomAttribute)new RoslynAttribute(a, _assembly))
            .ToList();
}

internal class RoslynEvent : IXamlEventInfo
{
    private readonly IEventSymbol _symbol;
    private readonly RoslynAssembly _assembly;

    public RoslynEvent(IEventSymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlEventInfo other) =>
        other is RoslynEvent re &&
        SymbolEqualityComparer.Default.Equals(_symbol, re._symbol);

    public string Name => _symbol.Name;
    public IXamlType DeclaringType => new RoslynType(_symbol.ContainingType, _assembly);
    public IXamlMethod? Add => _symbol.AddMethod != null ? new RoslynMethod(_symbol.AddMethod, _assembly) : null;
}

internal class RoslynParameter : IXamlParameterInfo
{
    private readonly RoslynAssembly _assembly;
    private readonly IParameterSymbol _symbol;

    public RoslynParameter(RoslynAssembly assembly, IParameterSymbol symbol)
    {
        _assembly = assembly;
        _symbol = symbol;
    }

    public string Name => _symbol.Name;
    public IXamlType ParameterType => RoslynTypeSystem.WrapType(_symbol.Type, _assembly);

    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes =>
        _symbol.GetAttributes()
            .Where(a => a.AttributeClass != null)
            .Select(a => (IXamlCustomAttribute)new RoslynAttribute(a, _assembly))
            .ToList();
}

internal class RoslynMethod : IXamlMethod
{
    private readonly IMethodSymbol _symbol;
    private readonly RoslynAssembly _assembly;

    public RoslynMethod(IMethodSymbol symbol, RoslynAssembly assembly)
    {
        _symbol = symbol;
        _assembly = assembly;
    }

    public bool Equals(IXamlMethod other) =>
        other is RoslynMethod roslynMethod &&
        SymbolEqualityComparer.Default.Equals(roslynMethod._symbol, _symbol);

    public string Name => _symbol.Name;

    public bool IsPublic => _symbol.DeclaredAccessibility == Accessibility.Public;

    public bool IsPrivate => _symbol.DeclaredAccessibility == Accessibility.Private;

    public bool IsFamily => _symbol.DeclaredAccessibility == Accessibility.Protected;

    public bool IsStatic => _symbol.IsStatic;

    public bool ContainsGenericParameters => _symbol.TypeParameters.Length > 0 && !_symbol.TypeArguments.All(ta => ta is INamedTypeSymbol);

    public bool IsGenericMethod => _symbol.IsGenericMethod;

    public bool IsGenericMethodDefinition => _symbol.IsDefinition && _symbol.IsGenericMethod;

    public IReadOnlyList<IXamlType> GenericParameters =>
        _symbol.TypeParameters
            .Select(tp => (IXamlType)new RoslynTypeParameter(tp, _assembly))
            .ToList();

    public IReadOnlyList<IXamlType> GenericArguments =>
        _symbol.TypeArguments
            .Select(ta => RoslynTypeSystem.WrapType(ta, _assembly))
            .ToList();

    public IXamlType ReturnType => RoslynTypeSystem.WrapType(_symbol.ReturnType, _assembly);

    public IReadOnlyList<IXamlType> Parameters =>
        _symbol.Parameters
            .Select(p => RoslynTypeSystem.WrapType(p.Type, _assembly))
            .ToList();

    public IXamlType DeclaringType => new RoslynType(_symbol.ContainingType, _assembly);

    public IXamlMethod MakeGenericMethod(IReadOnlyList<IXamlType> typeArguments)
    {
        var roslynArgs = typeArguments.Select(ta =>
        {
            if (ta is RoslynType rt) return (ITypeSymbol)rt.Symbol;
            if (ta is RoslynArrayType rat) return (ITypeSymbol)rat.Symbol;
            if (ta is RoslynTypeParameter rtp) return (ITypeSymbol)rtp.Symbol;
            throw new InvalidOperationException($"Cannot convert {ta.GetType().Name} to Roslyn type symbol.");
        }).ToArray();
        return new RoslynMethod(_symbol.Construct(roslynArgs), _assembly);
    }

    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes =>
        _symbol.GetAttributes()
            .Where(a => a.AttributeClass != null)
            .Select(a => (IXamlCustomAttribute)new RoslynAttribute(a, _assembly))
            .ToList();

    public IXamlParameterInfo GetParameterInfo(int index) => new RoslynParameter(_assembly, _symbol.Parameters[index]);

    internal IMethodSymbol Symbol => _symbol;
}
