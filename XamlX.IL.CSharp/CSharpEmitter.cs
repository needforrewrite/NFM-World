using System;
using System.Collections.Generic;
using System.Text;
using XamlX.IL;
using XamlX.TypeSystem;
using SreOpCode = System.Reflection.Emit.OpCode;
using SreOpCodes = System.Reflection.Emit.OpCodes;

namespace XamlX.CSharp;

/// <summary>
/// An IXamlILEmitter implementation that translates IL opcodes to C# source code statements.
/// The emitter maintains a virtual evaluation stack to track types and produce valid C# expressions.
/// </summary>
#if !XAMLX_INTERNAL
public
#endif
class CSharpEmitter : IXamlILEmitter
{
    private readonly CSharpMethodContext _method;
    private readonly List<string> _statements = new();
    private readonly Stack<CSharpExpression> _evalStack = new();
    private readonly List<CSharpLocal> _locals = new();
    private readonly Dictionary<CSharpLabel, string> _labelNames = new();
    private int _labelCounter;
    private int _localCounter;
    private int _tempCounter;

    public IXamlTypeSystem TypeSystem { get; }
    public XamlLocalsPool LocalsPool { get; }

    public CSharpEmitter(IXamlTypeSystem typeSystem, CSharpMethodContext method)
    {
        TypeSystem = typeSystem;
        _method = method;
        LocalsPool = new XamlLocalsPool(t => DefineLocal(t));
    }

    /// <summary>
    /// Gets the generated C# statements for this method body.
    /// </summary>
    public IReadOnlyList<string> Statements => _statements;

    private void Push(string expr, IXamlType? type = null)
        => _evalStack.Push(new CSharpExpression(expr, type));

    private CSharpExpression Pop()
        => _evalStack.Count > 0 ? _evalStack.Pop() : new CSharpExpression("default", null);

    private string PopExpr() => Pop().Expression;

    private void Emit(string statement) => _statements.Add(statement);

    private string FormatType(IXamlType type) => CSharpFormatting.FormatType(type);

    #region IXamlILEmitter Implementation

    public IXamlILEmitter Emit(SreOpCode code)
    {
        if (code == SreOpCodes.Nop) { /* skip */ }
        else if (code == SreOpCodes.Ret)
        {
            if (_method.ReturnType != null && _method.ReturnType.FullName != "System.Void")
            {
                var val = PopExpr();
                Emit($"return {val};");
            }
            else
            {
                Emit("return;");
            }
        }
        else if (code == SreOpCodes.Dup)
        {
            var val = Pop();
            var temp = AllocTemp();
            Emit($"var {temp} = {val.Expression};");
            Push(temp, val.Type);
            Push(temp, val.Type);
        }
        else if (code == SreOpCodes.Pop)
        {
            var val = PopExpr();
            // Only emit if the expression might have side effects
            if (!IsSimpleExpression(val))
                Emit($"_ = {val};");
        }
        else if (code == SreOpCodes.Throw)
        {
            var val = PopExpr();
            Emit($"throw (global::System.Exception){val};");
        }
        else if (code == SreOpCodes.Ldnull) Push("null");
        else if (code == SreOpCodes.Ldc_I4_0) Push("0");
        else if (code == SreOpCodes.Ldc_I4_1) Push("1");
        else if (code == SreOpCodes.Ldc_I4_2) Push("2");
        else if (code == SreOpCodes.Ldc_I4_3) Push("3");
        else if (code == SreOpCodes.Ldc_I4_4) Push("4");
        else if (code == SreOpCodes.Ldc_I4_5) Push("5");
        else if (code == SreOpCodes.Ldc_I4_6) Push("6");
        else if (code == SreOpCodes.Ldc_I4_7) Push("7");
        else if (code == SreOpCodes.Ldc_I4_8) Push("8");
        else if (code == SreOpCodes.Ldc_I4_M1) Push("-1");
        else if (code == SreOpCodes.Ldarg_0) Push(_method.GetArgName(0));
        else if (code == SreOpCodes.Ldarg_1) Push(_method.GetArgName(1));
        else if (code == SreOpCodes.Ldarg_2) Push(_method.GetArgName(2));
        else if (code == SreOpCodes.Ldarg_3) Push(_method.GetArgName(3));
        else if (code == SreOpCodes.Ldloc_0) Push(GetLocalName(0));
        else if (code == SreOpCodes.Ldloc_1) Push(GetLocalName(1));
        else if (code == SreOpCodes.Ldloc_2) Push(GetLocalName(2));
        else if (code == SreOpCodes.Ldloc_3) Push(GetLocalName(3));
        else if (code == SreOpCodes.Stloc_0) Emit($"{GetLocalName(0)} = {PopExpr()};");
        else if (code == SreOpCodes.Stloc_1) Emit($"{GetLocalName(1)} = {PopExpr()};");
        else if (code == SreOpCodes.Stloc_2) Emit($"{GetLocalName(2)} = {PopExpr()};");
        else if (code == SreOpCodes.Stloc_3) Emit($"{GetLocalName(3)} = {PopExpr()};");
        else if (code == SreOpCodes.Ldelem_Ref)
        {
            var index = PopExpr();
            var array = PopExpr();
            Push($"{array}[{index}]");
        }
        else if (code == SreOpCodes.Stelem_Ref)
        {
            var value = PopExpr();
            var index = PopExpr();
            var array = PopExpr();
            Emit($"{array}[{index}] = {value};");
        }
        else if (code == SreOpCodes.Ldlen)
        {
            var array = PopExpr();
            Push($"{array}.Length");
        }
        else if (code == SreOpCodes.Add)
        {
            var right = PopExpr();
            var left = PopExpr();
            Push($"({left} + {right})");
        }
        else if (code == SreOpCodes.Sub)
        {
            var right = PopExpr();
            var left = PopExpr();
            Push($"({left} - {right})");
        }
        else
        {
            Emit($"// TODO: Unhandled opcode: {code.Name}");
        }

        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, IXamlField field)
    {
        var fieldRef = $"{FormatType(field.DeclaringType)}.{field.Name}";

        if (code == SreOpCodes.Ldfld)
        {
            var obj = PopExpr();
            Push($"{obj}.{field.Name}", field.FieldType);
        }
        else if (code == SreOpCodes.Ldsfld)
        {
            Push(fieldRef, field.FieldType);
        }
        else if (code == SreOpCodes.Stfld)
        {
            var value = PopExpr();
            var obj = PopExpr();
            Emit($"{obj}.{field.Name} = {value};");
        }
        else if (code == SreOpCodes.Stsfld)
        {
            var value = PopExpr();
            Emit($"{fieldRef} = {value};");
        }
        else
        {
            Emit($"// TODO: Unhandled field opcode: {code.Name} {fieldRef}");
        }

        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, IXamlMethod method)
    {
        if (code == SreOpCodes.Call || code == SreOpCodes.Callvirt)
        {
            EmitMethodCall(method, isVirtual: code == SreOpCodes.Callvirt);
        }
        else if (code == SreOpCodes.Ldtoken)
        {
            Push($"typeof({FormatType(method.ReturnType)}).GetMethod(\"{method.Name}\").MethodHandle");
        }
        else if (code == SreOpCodes.Ldftn)
        {
            // Push a method reference - the consumer will typically use it with a delegate constructor
            var obj = method.IsStatic ? FormatType(method.DeclaringType) : PopExpr();
            Push($"{obj}.{method.Name}");
        }
        else
        {
            Emit($"// TODO: Unhandled method opcode: {code.Name} {method.Name}");
        }

        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, IXamlConstructor ctor)
    {
        if (code == SreOpCodes.Newobj)
        {
            var args = new string[ctor.Parameters.Count];
            for (var i = args.Length - 1; i >= 0; i--)
                args[i] = PopExpr();

            Push($"new {FormatType(ctor.DeclaringType)}({string.Join(", ", args)})", ctor.DeclaringType);
        }
        else if (code == SreOpCodes.Call)
        {
            // Base constructor call
            var args = new string[ctor.Parameters.Count];
            for (var i = args.Length - 1; i >= 0; i--)
                args[i] = PopExpr();

            var obj = PopExpr(); // 'this'
            Emit($"// base ctor call: {FormatType(ctor.DeclaringType)}({string.Join(", ", args)})");
        }
        else
        {
            Emit($"// TODO: Unhandled ctor opcode: {code.Name}");
        }

        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, string arg)
    {
        if (code == SreOpCodes.Ldstr)
            Push(CSharpFormatting.FormatStringLiteral(arg));
        else
            Emit($"// TODO: Unhandled string opcode: {code.Name} \"{arg}\"");
        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, int arg)
    {
        if (code == SreOpCodes.Ldc_I4 || code == SreOpCodes.Ldc_I4_S)
            Push(arg.ToString());
        else if (code == SreOpCodes.Ldarg || code == SreOpCodes.Ldarg_S)
            Push(_method.GetArgName(arg));
        else if (code == SreOpCodes.Ldloc || code == SreOpCodes.Ldloc_S)
            Push(GetLocalName(arg));
        else if (code == SreOpCodes.Stloc || code == SreOpCodes.Stloc_S)
            Emit($"{GetLocalName(arg)} = {PopExpr()};");
        else
            Emit($"// TODO: Unhandled int opcode: {code.Name} {arg}");
        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, long arg)
    {
        if (code == SreOpCodes.Ldc_I8)
            Push($"{arg}L");
        else
            Emit($"// TODO: Unhandled long opcode: {code.Name} {arg}");
        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, sbyte arg)
    {
        if (code == SreOpCodes.Ldc_I4_S)
            Push(((int)arg).ToString());
        else
            Emit($"// TODO: Unhandled sbyte opcode: {code.Name} {arg}");
        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, byte arg)
    {
        if (code == SreOpCodes.Ldarg_S)
            Push(_method.GetArgName(arg));
        else
            Emit($"// TODO: Unhandled byte opcode: {code.Name} {arg}");
        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, IXamlType type)
    {
        var typeName = FormatType(type);

        if (code == SreOpCodes.Castclass)
        {
            var val = PopExpr();
            Push($"(({typeName}){val})", type);
        }
        else if (code == SreOpCodes.Isinst)
        {
            var val = PopExpr();
            Push($"({val} as {typeName})", type);
        }
        else if (code == SreOpCodes.Box)
        {
            var val = PopExpr();
            Push($"(object){val}");
        }
        else if (code == SreOpCodes.Unbox_Any)
        {
            var val = PopExpr();
            Push($"(({typeName}){val})", type);
        }
        else if (code == SreOpCodes.Unbox)
        {
            // Unbox leaves a managed pointer; in C# we just cast
            var val = PopExpr();
            Push($"(({typeName}){val})", type);
        }
        else if (code == SreOpCodes.Newarr)
        {
            var length = PopExpr();
            Push($"new {typeName}[{length}]");
        }
        else if (code == SreOpCodes.Ldtoken)
        {
            Push($"typeof({typeName})");
        }
        else if (code == SreOpCodes.Initobj)
        {
            var addr = PopExpr();
            Emit($"{addr} = default({typeName});");
        }
        else if (code == SreOpCodes.Ldelem)
        {
            var index = PopExpr();
            var array = PopExpr();
            Push($"{array}[{index}]");
        }
        else if (code == SreOpCodes.Stelem)
        {
            var value = PopExpr();
            var index = PopExpr();
            var array = PopExpr();
            Emit($"{array}[{index}] = {value};");
        }
        else
        {
            Emit($"// TODO: Unhandled type opcode: {code.Name} {typeName}");
        }

        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, float arg)
    {
        if (code == SreOpCodes.Ldc_R4)
            Push($"{arg}f");
        else
            Emit($"// TODO: Unhandled float opcode: {code.Name} {arg}");
        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, double arg)
    {
        if (code == SreOpCodes.Ldc_R8)
            Push($"{arg}d");
        else
            Emit($"// TODO: Unhandled double opcode: {code.Name} {arg}");
        return this;
    }

    public IXamlLabel DefineLabel()
    {
        var label = new CSharpLabel($"label_{_labelCounter++}");
        return label;
    }

    public IXamlILEmitter MarkLabel(IXamlLabel label)
    {
        var csl = (CSharpLabel)label;
        Emit($"{csl.Name}:;");
        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, IXamlLabel label)
    {
        var csl = (CSharpLabel)label;

        if (code == SreOpCodes.Br || code == SreOpCodes.Br_S)
        {
            FlushStack();
            Emit($"goto {csl.Name};");
        }
        else if (code == SreOpCodes.Brfalse || code == SreOpCodes.Brfalse_S)
        {
            var val = PopExpr();
            Emit($"if (!({val} is true or not (null or 0))) goto {csl.Name};");
        }
        else if (code == SreOpCodes.Brtrue || code == SreOpCodes.Brtrue_S)
        {
            var val = PopExpr();
            Emit($"if ({val} is true or not (null or 0)) goto {csl.Name};");
        }
        else if (code == SreOpCodes.Beq || code == SreOpCodes.Beq_S)
        {
            var right = PopExpr();
            var left = PopExpr();
            Emit($"if ({left} == {right}) goto {csl.Name};");
        }
        else if (code == SreOpCodes.Blt || code == SreOpCodes.Blt_S)
        {
            var right = PopExpr();
            var left = PopExpr();
            Emit($"if ({left} < {right}) goto {csl.Name};");
        }
        else if (code == SreOpCodes.Ble || code == SreOpCodes.Ble_S)
        {
            var right = PopExpr();
            var left = PopExpr();
            Emit($"if ({left} <= {right}) goto {csl.Name};");
        }
        else if (code == SreOpCodes.Bgt || code == SreOpCodes.Bgt_S)
        {
            var right = PopExpr();
            var left = PopExpr();
            Emit($"if ({left} > {right}) goto {csl.Name};");
        }
        else if (code == SreOpCodes.Bge || code == SreOpCodes.Bge_S)
        {
            var right = PopExpr();
            var left = PopExpr();
            Emit($"if ({left} >= {right}) goto {csl.Name};");
        }
        else
        {
            Emit($"// TODO: Unhandled label opcode: {code.Name} {csl.Name}");
        }

        return this;
    }

    public IXamlILEmitter Emit(SreOpCode code, IXamlLocal local)
    {
        var csl = (CSharpLocal)local;

        if (code == SreOpCodes.Ldloc || code == SreOpCodes.Ldloc_S)
            Push(csl.Name, csl.Type);
        else if (code == SreOpCodes.Stloc || code == SreOpCodes.Stloc_S)
            Emit($"{csl.Name} = {PopExpr()};");
        else if (code == SreOpCodes.Ldloca || code == SreOpCodes.Ldloca_S)
            Push($"ref {csl.Name}", csl.Type);
        else
            Emit($"// TODO: Unhandled local opcode: {code.Name} {csl.Name}");

        return this;
    }

    public void InsertSequencePoint(IFileSource file, int line, int position)
    {
        Emit($"// {file.FilePath}:{line}:{position}");
    }

    public IXamlLocal DefineLocal(IXamlType type)
    {
        var local = new CSharpLocal($"__local_{_localCounter}", _localCounter, type);
        _locals.Add(local);
        _localCounter++;
        return local;
    }

    #endregion

    #region Private Helpers

    private void EmitMethodCall(IXamlMethod method, bool isVirtual)
    {
        var args = new string[method.Parameters.Count];
        for (var i = args.Length - 1; i >= 0; i--)
            args[i] = PopExpr();

        // Special case: Type.GetTypeFromHandle(typeof(X)) → typeof(X)
        // In IL, Ldtoken pushes RuntimeTypeHandle and GetTypeFromHandle converts to Type.
        // Our Ldtoken already produces typeof(X) which is a Type, so skip the conversion.
        if (method.IsStatic && method.Name == "GetTypeFromHandle" &&
            method.DeclaringType.FullName == "System.Type" &&
            args.Length == 1 && args[0].StartsWith("typeof("))
        {
            Push(args[0], method.ReturnType);
            return;
        }

        string call;

        if (method.IsStatic)
        {
            call = $"{FormatType(method.DeclaringType)}.{method.Name}({string.Join(", ", args)})";
        }
        else
        {
            var obj = PopExpr();
            call = $"{obj}.{method.Name}({string.Join(", ", args)})";
        }

        if (method.ReturnType.FullName == "System.Void")
        {
            Emit($"{call};");
        }
        else
        {
            Push(call, method.ReturnType);
        }
    }

    private string GetLocalName(int index)
    {
        if (index < _locals.Count)
            return _locals[index].Name;
        return $"__local_{index}";
    }

    private string AllocTemp() => $"__tmp_{_tempCounter++}";

    private void FlushStack()
    {
        // Assign any pending stack values to temps to preserve ordering across gotos
        var pending = new List<CSharpExpression>();
        while (_evalStack.Count > 0)
            pending.Add(_evalStack.Pop());

        pending.Reverse();
        var newExprs = new List<CSharpExpression>();
        foreach (var expr in pending)
        {
            if (!IsSimpleExpression(expr.Expression))
            {
                var temp = AllocTemp();
                Emit($"var {temp} = {expr.Expression};");
                newExprs.Add(new CSharpExpression(temp, expr.Type));
            }
            else
            {
                newExprs.Add(expr);
            }
        }

        foreach (var expr in newExprs)
            _evalStack.Push(expr);
    }

    private static bool IsSimpleExpression(string expr)
    {
        return expr == "null" || expr == "default" || expr == "this"
               || (expr.Length <= 20 && !expr.Contains('(') && !expr.Contains('['));
    }

    /// <summary>
    /// Generates the local variable declarations for the start of the method body.
    /// </summary>
    public string GenerateLocalDeclarations()
    {
        var sb = new StringBuilder();
        foreach (var local in _locals)
        {
            sb.AppendLine($"    {FormatType(local.Type)} {local.Name} = default;");
        }
        return sb.ToString();
    }

    #endregion
}

internal struct CSharpExpression
{
    public string Expression;
    public IXamlType? Type;

    public CSharpExpression(string expression, IXamlType? type)
    {
        Expression = expression;
        Type = type;
    }
}

internal class CSharpLocal : IXamlILLocal
{
    public string Name { get; }
    public int Index { get; }
    public IXamlType Type { get; }

    public CSharpLocal(string name, int index, IXamlType type)
    {
        Name = name;
        Index = index;
        Type = type;
    }
}

internal class CSharpLabel : IXamlLabel
{
    public string Name { get; }

    public CSharpLabel(string name)
    {
        Name = name;
    }
}

#if !XAMLX_INTERNAL
public
#endif
class CSharpMethodContext
{
    public IXamlType? ReturnType { get; }
    public bool IsStatic { get; }
    public bool IsConstructor { get; }
    private readonly string[] _argNames;

    public CSharpMethodContext(IXamlType? returnType, bool isStatic, bool isConstructor, string[] argNames)
    {
        ReturnType = returnType;
        IsStatic = isStatic;
        IsConstructor = isConstructor;
        _argNames = argNames;
    }

    public string GetArgName(int index)
    {
        if (!IsStatic && index == 0)
            return "this";
        var adjustedIndex = IsStatic ? index : index - 1;
        if (adjustedIndex >= 0 && adjustedIndex < _argNames.Length)
            return _argNames[adjustedIndex];
        return $"__arg_{index}";
    }
}