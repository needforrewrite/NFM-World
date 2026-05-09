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
    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments) => new ConstructedCSharpType(this, typeArguments);
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

        var methodCtx = new CSharpMethodContext(returnType, isStatic, false, argNames, argsList.ToArray());
        var emitter = new CSharpEmitter(_typeSystem, methodCtx);
        var method = new CSharpMethodBuilder(this, returnType, argsList, name, visibility, isStatic, isInterfaceImpl, argNames, emitter, overrideMethod);
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

        var methodCtx = new CSharpMethodContext(null, isStatic, true, argNames, args);
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

        // Methods - skip methods that are already emitted as property getters/setters
        var propMethods = new HashSet<object>();
        foreach (var prop in _properties)
        {
            if (prop.Getter is CSharpMethodBuilder gm2) propMethods.Add(gm2);
            if (prop.Setter is CSharpMethodBuilder sm2) propMethods.Add(sm2);
        }
        foreach (var method in _methods)
        {
            if (propMethods.Contains(method))
                continue;
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

        // Methods - skip methods that are already emitted as property getters/setters
        var propertyMethods = new HashSet<object>();
        foreach (var prop in _properties)
        {
            if (prop.Getter is CSharpMethodBuilder gm) propertyMethods.Add(gm);
            if (prop.Setter is CSharpMethodBuilder sm) propertyMethods.Add(sm);
        }
        foreach (var method in _methods)
        {
            if (propertyMethods.Contains(method))
                continue;
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
        var isExplicitImpl = method.Name.Contains('.');
        var vis = isExplicitImpl ? "" : method.MethodVisibility switch
        {
            XamlVisibility.Public => "public ",
            XamlVisibility.Assembly => "internal ",
            XamlVisibility.Private => "private ",
            _ => "private "
        };
        var staticMod = method.IsStatic ? "static " : "";
        var retType = CSharpFormatting.FormatType(method.ReturnType);
        var args = string.Join(", ", method.Parameters.Select((p, i) => $"{CSharpFormatting.FormatType(p)} {method.ArgNames[i]}"));

        // For explicit interface implementations, format the interface part properly
        // to handle generic interfaces (replace backtick metadata names with C# generic syntax)
        var methodName = FormatMethodName(method);

        sb.AppendLine($"{indent}{vis}{staticMod}{retType} {methodName}({args})");
        sb.AppendLine($"{indent}{{");

        var bodyIndent = indent + "    ";
        sb.Append(method.Emitter.GenerateLocalDeclarations());
        foreach (var stmt in method.Emitter.Statements)
            sb.AppendLine($"{bodyIndent}{stmt}");

        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Formats a method name, converting explicit interface implementation names
    /// from metadata format (using backtick) to C# generic syntax.
    /// </summary>
    private static string FormatMethodName(CSharpMethodBuilder method)
    {
        if (!method.Name.Contains('.'))
            return method.Name;

        // Explicit interface implementation: use the override method's declaring type to get proper formatting
        if (method.OverrideMethod?.DeclaringType is { } interfaceType)
        {
            var lastDot = method.Name.LastIndexOf('.');
            var simpleName = method.Name.Substring(lastDot + 1);
            var formattedInterface = CSharpFormatting.FormatType(interfaceType);
            // Remove "global::" prefix since explicit implementations don't use it
            if (formattedInterface.StartsWith("global::"))
                formattedInterface = formattedInterface.Substring("global::".Length);
            return $"{formattedInterface}.{simpleName}";
        }

        // Fallback: strip backtick arity from the name (loses generic args but at least compiles)
        return StripBacktickArity(method.Name);
    }

    /// <summary>
    /// Formats a property name, handling explicit interface implementation names.
    /// </summary>
    private static string FormatPropertyName(CSharpPropertyInfo prop)
    {
        if (!prop.Name.Contains('.'))
            return prop.Name;

        // Try to get the interface type from the getter or setter's override method
        var overrideMethod = (prop.Getter as CSharpMethodBuilder)?.OverrideMethod
                          ?? (prop.Setter as CSharpMethodBuilder)?.OverrideMethod;
        if (overrideMethod?.DeclaringType is { } interfaceType)
        {
            var lastDot = prop.Name.LastIndexOf('.');
            var simpleName = prop.Name.Substring(lastDot + 1);
            var formattedInterface = CSharpFormatting.FormatType(interfaceType);
            if (formattedInterface.StartsWith("global::"))
                formattedInterface = formattedInterface.Substring("global::".Length);
            return $"{formattedInterface}.{simpleName}";
        }

        return StripBacktickArity(prop.Name);
    }

    private static string StripBacktickArity(string name)
    {
        // Remove backtick+digits patterns like `1, `2 etc.
        var result = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] == '`')
            {
                // Skip backtick and following digits
                i++;
                while (i < name.Length && char.IsDigit(name[i]))
                    i++;
                i--; // Will be incremented by for loop
            }
            else
            {
                result.Append(name[i]);
            }
        }
        return result.ToString();
    }

    private void GenerateProperty(StringBuilder sb, CSharpPropertyInfo prop, string indent)
    {
        var typeName = CSharpFormatting.FormatType(prop.PropertyType);
        var isExplicitImpl = prop.Name.Contains('.');
        var vis = isExplicitImpl ? "" : "public ";
        var propName = FormatPropertyName(prop);

        // Check if getter/setter have method bodies (CSharpMethodBuilder with statements)
        var getterMethod = prop.Getter as CSharpMethodBuilder;
        var setterMethod = prop.Setter as CSharpMethodBuilder;
        var hasGetterBody = getterMethod?.Emitter.Statements.Count > 0;
        var hasSetterBody = setterMethod?.Emitter.Statements.Count > 0;

        if (hasGetterBody || hasSetterBody)
        {
            sb.AppendLine($"{indent}{vis}{typeName} {propName}");
            sb.AppendLine($"{indent}{{");
            var bodyIndent = indent + "    ";
            var stmtIndent = indent + "        ";

            if (hasGetterBody)
            {
                sb.AppendLine($"{bodyIndent}get");
                sb.AppendLine($"{bodyIndent}{{");
                sb.Append(getterMethod!.Emitter.GenerateLocalDeclarations());
                foreach (var stmt in getterMethod.Emitter.Statements)
                    sb.AppendLine($"{stmtIndent}{stmt}");
                sb.AppendLine($"{bodyIndent}}}");
            }
            else if (prop.Getter != null)
            {
                sb.AppendLine($"{bodyIndent}get;");
            }

            if (hasSetterBody)
            {
                sb.AppendLine($"{bodyIndent}set");
                sb.AppendLine($"{bodyIndent}{{");
                sb.Append(setterMethod!.Emitter.GenerateLocalDeclarations());
                // In property setters, replace arg0 references with 'value'
                foreach (var stmt in setterMethod.Emitter.Statements)
                    sb.AppendLine($"{stmtIndent}{ReplaceSetterArg(stmt)}");
                sb.AppendLine($"{bodyIndent}}}");
            }
            else if (prop.Setter != null)
            {
                sb.AppendLine($"{bodyIndent}set;");
            }

            sb.AppendLine($"{indent}}}");
        }
        else
        {
            sb.Append($"{indent}{vis}{typeName} {propName} {{ ");
            if (prop.Getter != null) sb.Append("get; ");
            if (prop.Setter != null) sb.Append("set; ");
            sb.AppendLine("}");
        }
    }

    /// <summary>
    /// In a property setter body emitted from IL, the first parameter (arg0 for an instance method's
    /// second arg) maps to the implicit 'value' parameter in C# property setters.
    /// </summary>
    private static string ReplaceSetterArg(string statement)
    {
        // The setter's CSharpMethodContext has argNames = ["arg0"], and for a non-static method,
        // GetArgName(1) returns argNames[0] = "arg0". In C# property setters, this is 'value'.
        return statement.Replace("arg0", "value");
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
        bool isStatic, bool isInterfaceImpl, string[] argNames, CSharpEmitter emitter,
        IXamlMethod? overrideMethod = null)
    {
        _declaringType = declaringType;
        ReturnType = returnType;
        Parameters = parameters;
        Name = name;
        MethodVisibility = visibility;
        IsStatic = isStatic;
        IsInterfaceImpl = isInterfaceImpl;
        OverrideMethod = overrideMethod;
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
    public bool IsInterfaceImpl { get; }
    public IXamlMethod? OverrideMethod { get; }
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

/// <summary>
/// Represents a constructed generic type (e.g. Context&lt;SomeNode&gt;) created from a CSharpTypeBuilder
/// generic definition. Forwards Fields/Methods/Constructors from the definition.
/// </summary>
internal class ConstructedCSharpType : IXamlType
{
    private readonly CSharpTypeBuilder _definition;
    private readonly IReadOnlyList<IXamlType> _typeArguments;

    public IReadOnlyList<IXamlConstructor> Constructors { get; }

    public ConstructedCSharpType(CSharpTypeBuilder definition, IReadOnlyList<IXamlType> typeArguments)
    {
        _definition = definition;
        _typeArguments = typeArguments;
        // Wrap constructors so they report this constructed type as DeclaringType
        Constructors = definition.Constructors
            .Select(c => (IXamlConstructor)new ConstructedCtorWrapper(this, c))
            .ToList();
    }

    public object Id { get; } = Guid.NewGuid();
    public string Name => _definition.Name;
    public string? Namespace => _definition.Namespace;

    public string FullName
    {
        get
        {
            var baseName = ((IXamlType)_definition).FullName;
            return baseName + "<" + string.Join(", ", _typeArguments.Select(t => t.FullName)) + ">";
        }
    }

    public bool IsPublic => _definition.IsPublic;
    public bool IsNestedPrivate => _definition.IsNestedPrivate;
    public IXamlAssembly? Assembly => _definition.Assembly;
    public IReadOnlyList<IXamlProperty> Properties => _definition.Properties;
    public IReadOnlyList<IXamlEventInfo> Events => _definition.Events;
    public IReadOnlyList<IXamlField> Fields => _definition.Fields;
    public IReadOnlyList<IXamlMethod> Methods => _definition.Methods;
    public IReadOnlyList<IXamlCustomAttribute> CustomAttributes => _definition.CustomAttributes;
    public IReadOnlyList<IXamlType> GenericArguments => _typeArguments;
    public IXamlType? GenericTypeDefinition => _definition;
    public bool IsArray => false;
    public IXamlType? ArrayElementType => null;
    public IXamlType? BaseType => _definition.BaseType;
    public IXamlType? DeclaringType => _definition.DeclaringType;
    public bool IsValueType => false;
    public bool IsEnum => false;
    public IReadOnlyList<IXamlType> Interfaces => _definition.Interfaces;
    public bool IsInterface => false;
    public IReadOnlyList<IXamlType> GenericParameters => Array.Empty<IXamlType>();
    public bool IsFunctionPointer => false;

    public bool IsAssignableFrom(IXamlType type) => type.Equals(this) || _definition.IsAssignableFrom(type);
    public IXamlType MakeGenericType(IReadOnlyList<IXamlType> typeArguments) => throw new NotSupportedException();
    public IXamlType MakeArrayType(int dimensions) => throw new NotSupportedException();
    public IXamlType GetEnumUnderlyingType() => throw new NotSupportedException();
    public bool Equals(IXamlType? other)
    {
        if (other is ConstructedCSharpType c)
            return ReferenceEquals(_definition, c._definition) &&
                   _typeArguments.Count == c._typeArguments.Count &&
                   _typeArguments.Zip(c._typeArguments, (a, b) => a.Equals(b)).All(x => x);
        return false;
    }
}

internal class ConstructedCtorWrapper : IXamlConstructor
{
    private readonly IXamlConstructor _inner;

    public ConstructedCtorWrapper(IXamlType declaringType, IXamlConstructor inner)
    {
        DeclaringType = declaringType;
        _inner = inner;
    }

    public IXamlType DeclaringType { get; }
    public string Name => _inner.Name;
    public bool IsPublic => _inner.IsPublic;
    public bool IsStatic => _inner.IsStatic;
    public IReadOnlyList<IXamlType> Parameters => _inner.Parameters;
    public IXamlParameterInfo GetParameterInfo(int index) => _inner.GetParameterInfo(index);
    public bool Equals(IXamlConstructor? other) => _inner.Equals(other is ConstructedCtorWrapper w ? w._inner : other);
}

#endregion