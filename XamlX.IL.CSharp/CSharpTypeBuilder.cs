using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XamlX.IL;
using XamlX.TypeSystem;

namespace XamlX.CSharp;

/// <summary>
/// IXamlTypeBuilder that generates C# source code instead of IL/Cecil types.
/// </summary>
#if !XAMLX_INTERNAL
public
#endif
class CSharpTypeBuilder : IXamlTypeBuilder<IXamlILEmitter>
{
    private readonly IXamlTypeSystem _typeSystem;
    private readonly string _namespace;
    private readonly string _name;
    private readonly IXamlType? _baseType;
    private readonly XamlVisibility _visibility;
    private readonly List<CSharpFieldInfo> _fields = new();
    private readonly List<CSharpMethodBuilder> _methods = new();
    private readonly List<CSharpConstructorBuilder> _constructors = new();
    private readonly List<CSharpPropertyInfo> _properties = new();
    private readonly List<IXamlType> _interfaces = new();
    private readonly List<CSharpTypeBuilder> _nestedTypes = new();
    private readonly List<KeyValuePair<string, XamlGenericParameterConstraint>> _genericParams = new();
    private readonly List<CSharpGenericParameterType> _genericParamTypes = new();
    private readonly CSharpTypeBuilder? _parent;

    internal CSharpTypeBuilder(IXamlTypeSystem typeSystem, string namespaceName, string name,
        IXamlType? baseType, XamlVisibility visibility, CSharpTypeBuilder? parent = null)
    {
        _typeSystem = typeSystem;
        _namespace = namespaceName;
        _name = name;
        _baseType = baseType;
        _visibility = visibility;
        _parent = parent;
    }

    public string FullName
    {
        get
        {
            if (_parent != null)
                return $"{_parent.FullName}.{_name}";
            return string.IsNullOrEmpty(_namespace) ? _name : $"{_namespace}.{_name}";
        }
    }

    #region IXamlType Implementation (read-only reflection for the type being built)

    public object Id { get; } = Guid.NewGuid();
    public string Name => _name;
    public string? Namespace => _namespace;
    string IXamlType.FullName => FullName;
    public bool IsPublic => _visibility == XamlVisibility.Public;
    public bool IsNestedPrivate => _visibility == XamlVisibility.Private && _parent != null;
    public IXamlAssembly? Assembly => null;
    public IReadOnlyList<IXamlProperty> Properties => _properties;
    public IReadOnlyList<IXamlEventInfo> Events => Array.Empty<IXamlEventInfo>();
    public IReadOnlyList<IXamlField> Fields => _fields;
    public IReadOnlyList<IXamlMethod> Methods => _methods;
    public IReadOnlyList<IXamlConstructor> Constructors => _constructors;
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();
    public IReadOnlyList<IXamlType> GenericArguments => Array.Empty<IXamlType>();
    public IXamlType? GenericTypeDefinition => null;
    public bool IsArray => false;
    public IXamlType? ArrayElementType => null;
    public IXamlType? BaseType => _baseType;
    public IXamlType? DeclaringType => _parent;
    public bool IsValueType => false;
    public bool IsEnum => false;
    public IReadOnlyList<IXamlType> Interfaces => _interfaces;
    public bool IsInterface => false;
    public IReadOnlyList<IXamlType> GenericParameters => _genericParamTypes;
    public bool IsFunctionPointer => false;

    public bool IsAssignableFrom(IXamlType type) => type.Equals(this) || type.GetAllInterfaces().Any(i => i.Equals(this));
    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments) => throw new NotSupportedException();
    public IXamlType MakeArrayType(int dimensions) => throw new NotSupportedException();
    public IXamlType GetEnumUnderlyingType() => throw new NotSupportedException();
    public bool Equals(IXamlType? other) => ReferenceEquals(this, other);

    #endregion

    #region IXamlTypeBuilder Implementation

    public IXamlField DefineField(IXamlType type, string name, XamlVisibility visibility, bool isStatic)
    {
        var field = new CSharpFieldInfo(this, type, name, visibility, isStatic);
        _fields.Add(field);
        return field;
    }

    public void AddInterfaceImplementation(IXamlType type)
    {
        _interfaces.Add(type);
    }

    public IXamlMethodBuilder<IXamlILEmitter> DefineMethod(IXamlType returnType, IEnumerable<IXamlType> args,
        string name, XamlVisibility visibility, bool isStatic, bool isInterfaceImpl,
        IXamlMethod? overrideMethod = null)
    {
        var argsList = args.ToList();
        var argNames = new string[argsList.Count];
        for (var i = 0; i < argsList.Count; i++)
            argNames[i] = $"arg{i}";

        var methodCtx = new CSharpMethodContext(returnType, isStatic, false, argNames);
        var emitter = new CSharpEmitter(_typeSystem, methodCtx);
        var method = new CSharpMethodBuilder(this, returnType, argsList, name, visibility, isStatic, isInterfaceImpl, argNames, emitter);
        _methods.Add(method);
        return method;
    }

    public IXamlProperty DefineProperty(IXamlType propertyType, string name, IXamlMethod? setter, IXamlMethod? getter)
    {
        var prop = new CSharpPropertyInfo(this, propertyType, name, setter, getter);
        _properties.Add(prop);
        return prop;
    }

    public IXamlConstructorBuilder<IXamlILEmitter> DefineConstructor(bool isStatic, params IXamlType[] args)
    {
        var argNames = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
            argNames[i] = $"arg{i}";

        var methodCtx = new CSharpMethodContext(null, isStatic, true, argNames);
        var emitter = new CSharpEmitter(_typeSystem, methodCtx);
        var ctor = new CSharpConstructorBuilder(this, isStatic, args, argNames, emitter);
        _constructors.Add(ctor);
        return ctor;
    }

    public IXamlType CreateType() => this;

    public IXamlTypeBuilder<IXamlILEmitter> DefineSubType(IXamlType baseType, string name, XamlVisibility visibility)
    {
        var nested = new CSharpTypeBuilder(_typeSystem, "", name, baseType, visibility, this);
        _nestedTypes.Add(nested);
        return nested;
    }

    public IXamlTypeBuilder<IXamlILEmitter> DefineDelegateSubType(string name, XamlVisibility visibility,
        IXamlType returnType, IEnumerable<IXamlType> parameterTypes)
    {
        // For C# source, we output a delegate declaration
        var nested = new CSharpTypeBuilder(_typeSystem, "", name, null, visibility, this);
        _nestedTypes.Add(nested);
        return nested;
    }

    public void DefineGenericParameters(IReadOnlyList<KeyValuePair<string, XamlGenericParameterConstraint>> names)
    {
        _genericParams.AddRange(names);
        foreach (var kvp in names)
            _genericParamTypes.Add(new CSharpGenericParameterType(kvp.Key, this));
    }

    #endregion

    #region C# Source Generation

    /// <summary>
    /// Generates just the member declarations (fields, constructors, properties, methods, nested types)
    /// without the enclosing class/namespace wrapper. Used for embedding into existing partial classes.
    /// </summary>
    public void GenerateMembers(StringBuilder sb, string indent)
    {
        GenerateMembersBody(sb, indent);
    }

    private void GenerateMembersBody(StringBuilder sb, string indent)
    {
        // Fields
        foreach (var field in _fields)
        {
            var fieldVis = field.Visibility switch
            {
                XamlVisibility.Public => "public",
                XamlVisibility.Assembly => "internal",
                XamlVisibility.Private => "private",
                _ => "private"
            };
            var staticMod = field.IsStatic ? "static " : "";
            sb.AppendLine($"{indent}{fieldVis} {staticMod}{CSharpFormatting.FormatType(field.FieldType)} {field.Name};");
        }
        if (_fields.Count > 0) sb.AppendLine();

        // Constructors
        foreach (var ctor in _constructors)
        {
            GenerateConstructor(sb, ctor, indent);
            sb.AppendLine();
        }

        // Properties
        foreach (var prop in _properties)
        {
            GenerateProperty(sb, prop, indent);
            sb.AppendLine();
        }

        // Methods
        foreach (var method in _methods)
        {
            GenerateMethod(sb, method, indent);
            sb.AppendLine();
        }

        // Nested types
        foreach (var nested in _nestedTypes)
        {
            nested.GenerateTypeBody(sb, indent);
            sb.AppendLine();
        }
    }

    public string GenerateSource()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(_namespace))
        {
            sb.AppendLine($"namespace {_namespace}");
            sb.AppendLine("{");
            GenerateTypeBody(sb, "    ");
            sb.AppendLine("}");
        }
        else
        {
            GenerateTypeBody(sb, "");
        }

        return sb.ToString();
    }

    private void GenerateTypeBody(StringBuilder sb, string indent)
    {
        var vis = _visibility switch
        {
            XamlVisibility.Public => "public",
            XamlVisibility.Assembly => "internal",
            XamlVisibility.Private => "private",
            _ => "internal"
        };

        var genericSuffix = "";
        if (_genericParams.Count > 0)
            genericSuffix = "<" + string.Join(", ", _genericParams.Select(p => p.Key)) + ">";

        var baseClause = "";
        var bases = new List<string>();
        if (_baseType != null)
            bases.Add(CSharpFormatting.FormatType(_baseType));
        bases.AddRange(_interfaces.Select(CSharpFormatting.FormatType));
        if (bases.Count > 0)
            baseClause = " : " + string.Join(", ", bases);

        sb.AppendLine($"{indent}{vis} partial class {_name}{genericSuffix}{baseClause}");
        sb.AppendLine($"{indent}{{");

        var innerIndent = indent + "    ";

        // Fields
        foreach (var field in _fields)
        {
            var fieldVis = field.Visibility switch
            {
                XamlVisibility.Public => "public",
                XamlVisibility.Assembly => "internal",
                XamlVisibility.Private => "private",
                _ => "private"
            };
            var staticMod = field.IsStatic ? "static " : "";
            sb.AppendLine($"{innerIndent}{fieldVis} {staticMod}{CSharpFormatting.FormatType(field.FieldType)} {field.Name};");
        }

        if (_fields.Count > 0) sb.AppendLine();

        // Constructors
        foreach (var ctor in _constructors)
        {
            GenerateConstructor(sb, ctor, innerIndent);
            sb.AppendLine();
        }

        // Properties
        foreach (var prop in _properties)
        {
            GenerateProperty(sb, prop, innerIndent);
            sb.AppendLine();
        }

        // Methods
        foreach (var method in _methods)
        {
            GenerateMethod(sb, method, innerIndent);
            sb.AppendLine();
        }

        // Nested types
        foreach (var nested in _nestedTypes)
        {
            nested.GenerateTypeBody(sb, innerIndent);
            sb.AppendLine();
        }

        // Generic constraints
        foreach (var gp in _genericParams)
        {
            if (gp.Value.IsClass)
                sb.AppendLine($"{innerIndent}// where {gp.Key} : class");
        }

        sb.AppendLine($"{indent}}}");
    }

    private void GenerateConstructor(StringBuilder sb, CSharpConstructorBuilder ctor, string indent)
    {
        var staticMod = ctor.IsStatic ? "static " : "public ";
        var args = string.Join(", ", ctor.Parameters.Select((p, i) => $"{CSharpFormatting.FormatType(p)} {ctor.ArgNames[i]}"));

        sb.AppendLine($"{indent}{staticMod}{_name}({args})");
        sb.AppendLine($"{indent}{{");

        var bodyIndent = indent + "    ";
        sb.Append(ctor.Emitter.GenerateLocalDeclarations());
        foreach (var stmt in ctor.Emitter.Statements)
            sb.AppendLine($"{bodyIndent}{stmt}");

        sb.AppendLine($"{indent}}}");
    }

    private void GenerateMethod(StringBuilder sb, CSharpMethodBuilder method, string indent)
    {
        var vis = method.MethodVisibility switch
        {
            XamlVisibility.Public => "public",
            XamlVisibility.Assembly => "internal",
            XamlVisibility.Private => "private",
            _ => "private"
        };
        var staticMod = method.IsStatic ? "static " : "";
        var retType = CSharpFormatting.FormatType(method.ReturnType);
        var args = string.Join(", ", method.Parameters.Select((p, i) => $"{CSharpFormatting.FormatType(p)} {method.ArgNames[i]}"));

        sb.AppendLine($"{indent}{vis} {staticMod}{retType} {method.Name}({args})");
        sb.AppendLine($"{indent}{{");

        var bodyIndent = indent + "    ";
        sb.Append(method.Emitter.GenerateLocalDeclarations());
        foreach (var stmt in method.Emitter.Statements)
            sb.AppendLine($"{bodyIndent}{stmt}");

        sb.AppendLine($"{indent}}}");
    }

    private void GenerateProperty(StringBuilder sb, CSharpPropertyInfo prop, string indent)
    {
        var typeName = CSharpFormatting.FormatType(prop.PropertyType);
        sb.Append($"{indent}public {typeName} {prop.Name} {{ ");
        if (prop.Getter != null) sb.Append("get; ");
        if (prop.Setter != null) sb.Append("set; ");
        sb.AppendLine("}");
    }

    #endregion
}

#region Supporting Types

internal class CSharpFieldInfo : IXamlField
{
    private readonly CSharpTypeBuilder _declaringType;

    public CSharpFieldInfo(CSharpTypeBuilder declaringType, IXamlType fieldType, string name,
        XamlVisibility visibility, bool isStatic)
    {
        _declaringType = declaringType;
        FieldType = fieldType;
        Name = name;
        Visibility = visibility;
        IsStatic = isStatic;
    }

    public string Name { get; }
    public IXamlType DeclaringType => _declaringType;
    public IXamlType FieldType { get; }
    public bool IsPublic => Visibility == XamlVisibility.Public;
    public bool IsStatic { get; }
    public bool IsLiteral => false;
    public XamlVisibility Visibility { get; }
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();

    public object GetLiteralValue() => throw new NotSupportedException();
    public bool Equals(IXamlField? other) => ReferenceEquals(this, other);
}

internal class CSharpMethodBuilder : IXamlMethodBuilder<IXamlILEmitter>
{
    private readonly CSharpTypeBuilder _declaringType;

    public CSharpMethodBuilder(CSharpTypeBuilder declaringType, IXamlType returnType,
        List<IXamlType> parameters, string name, XamlVisibility visibility,
        bool isStatic, bool isInterfaceImpl, string[] argNames, CSharpEmitter emitter)
    {
        _declaringType = declaringType;
        ReturnType = returnType;
        Parameters = parameters;
        Name = name;
        MethodVisibility = visibility;
        IsStatic = isStatic;
        ArgNames = argNames;
        Emitter = emitter;
    }

    public string Name { get; }
    public IXamlType DeclaringType => _declaringType;
    public IXamlType ReturnType { get; }
    public IReadOnlyList<IXamlType> Parameters { get; }
    public XamlVisibility MethodVisibility { get; }
    public bool IsPublic => MethodVisibility == XamlVisibility.Public;
    public bool IsPrivate => MethodVisibility == XamlVisibility.Private;
    public bool IsFamily => false;
    public bool IsStatic { get; }
    public bool ContainsGenericParameters => false;
    public bool IsGenericMethod => false;
    public bool IsGenericMethodDefinition => false;
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();
    public IReadOnlyList<IXamlType> GenericParameters => Array.Empty<IXamlType>();
    public IReadOnlyList<IXamlType> GenericArguments => Array.Empty<IXamlType>();
    public string[] ArgNames { get; }
    public CSharpEmitter Emitter { get; }

    IXamlILEmitter IXamlMethodBuilder<IXamlILEmitter>.Generator => Emitter;

    public IXamlMethod MakeGenericMethod(IReadOnlyList<IXamlType> typeArguments) => throw new NotSupportedException();
    public IXamlParameterInfo GetParameterInfo(int index)
        => new AnonymousParameterInfo(Parameters[index], ArgNames[index]);
    public bool Equals(IXamlMethod? other) => ReferenceEquals(this, other);
}

internal class CSharpConstructorBuilder : IXamlConstructorBuilder<IXamlILEmitter>
{
    private readonly CSharpTypeBuilder _declaringType;

    public CSharpConstructorBuilder(CSharpTypeBuilder declaringType, bool isStatic,
        IXamlType[] parameters, string[] argNames, CSharpEmitter emitter)
    {
        _declaringType = declaringType;
        IsStatic = isStatic;
        Parameters = parameters;
        ArgNames = argNames;
        Emitter = emitter;
    }

    public string Name => IsStatic ? ".cctor" : ".ctor";
    public IXamlType DeclaringType => _declaringType;
    public bool IsPublic => !IsStatic;
    public bool IsStatic { get; }
    public IReadOnlyList<IXamlType> Parameters { get; }
    public string[] ArgNames { get; }
    public CSharpEmitter Emitter { get; }

    IXamlILEmitter IXamlConstructorBuilder<IXamlILEmitter>.Generator => Emitter;

    public IXamlParameterInfo GetParameterInfo(int index)
        => new AnonymousParameterInfo(Parameters[index], ArgNames[index]);
    public bool Equals(IXamlConstructor? other) => ReferenceEquals(this, other);
}

internal class CSharpPropertyInfo : IXamlProperty
{
    private readonly CSharpTypeBuilder _declaringType;

    public CSharpPropertyInfo(CSharpTypeBuilder declaringType, IXamlType propertyType, string name,
        IXamlMethod? setter, IXamlMethod? getter)
    {
        _declaringType = declaringType;
        PropertyType = propertyType;
        Name = name;
        Setter = setter;
        Getter = getter;
    }

    public string Name { get; }
    public IXamlType DeclaringType => _declaringType;
    public IXamlType PropertyType { get; }
    public IXamlMethod? Setter { get; }
    public IXamlMethod? Getter { get; }
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();
    public IReadOnlyList<IXamlType> IndexerParameters => Array.Empty<IXamlType>();
    public bool Equals(IXamlProperty? other) => ReferenceEquals(this, other);
}

internal class CSharpGenericParameterType : IXamlType
{
    private readonly CSharpTypeBuilder _declaringType;

    public CSharpGenericParameterType(string name, CSharpTypeBuilder declaringType)
    {
        Name = name;
        _declaringType = declaringType;
    }

    public object Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public string? Namespace => null;
    public string FullName => Name;
    public bool IsPublic => true;
    public bool IsNestedPrivate => false;
    public IXamlAssembly? Assembly => null;
    public IReadOnlyList<IXamlProperty> Properties => Array.Empty<IXamlProperty>();
    public IReadOnlyList<IXamlEventInfo> Events => Array.Empty<IXamlEventInfo>();
    public IReadOnlyList<IXamlField> Fields => Array.Empty<IXamlField>();
    public IReadOnlyList<IXamlMethod> Methods => Array.Empty<IXamlMethod>();
    public IReadOnlyList<IXamlConstructor> Constructors => Array.Empty<IXamlConstructor>();
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => Array.Empty<IXamlCustomAttribute>();
    public IReadOnlyList<IXamlType> GenericArguments => Array.Empty<IXamlType>();
    public IXamlType? GenericTypeDefinition => null;
    public bool IsArray => false;
    public IXamlType? ArrayElementType => null;
    public IXamlType? BaseType => null;
    public IXamlType? DeclaringType => _declaringType;
    public bool IsValueType => false;
    public bool IsEnum => false;
    public IReadOnlyList<IXamlType> Interfaces => Array.Empty<IXamlType>();
    public bool IsInterface => false;
    public IReadOnlyList<IXamlType> GenericParameters => Array.Empty<IXamlType>();
    public bool IsFunctionPointer => false;

    public bool IsAssignableFrom(IXamlType type) => Equals(type);
    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments) => throw new NotSupportedException();
    public IXamlType MakeArrayType(int dimensions) => throw new NotSupportedException();
    public IXamlType GetEnumUnderlyingType() => throw new NotSupportedException();
    public bool Equals(IXamlType? other) => ReferenceEquals(this, other);
}

#endregion