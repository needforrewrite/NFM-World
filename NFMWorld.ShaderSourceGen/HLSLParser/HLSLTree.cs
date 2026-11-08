// Ported from HLSLTree.h / HLSLTree.cpp (M4 / Unknown Worlds Entertainment)

using System;
using System.Collections.Generic;

// ReSharper disable InconsistentNaming

namespace NFMWorld.ShaderSourceGen;

// ─── Enums ──────────────────────────────────────────────────────────────────

public enum HLSLNodeType : byte
{
    Root,
    Declaration,
    Struct,
    StructField,
    Buffer,
    BufferField,
    Function,
    Argument,
    ExpressionStatement,
    Expression,
    ReturnStatement,
    DiscardStatement,
    BreakStatement,
    ContinueStatement,
    IfStatement,
    ForStatement,
    BlockStatement,
    UnaryExpression,
    BinaryExpression,
    ConditionalExpression,
    CastingExpression,
    LiteralExpression,
    IdentifierExpression,
    ConstructorExpression,
    MemberAccess,
    ArrayAccess,
    FunctionCall,
    StateAssignment,
    SamplerState,
    Pass,
    Technique,
    Attribute,
    Pipeline,
    Stage,
}

public enum HLSLTypeDimension : byte
{
    None,
    Scalar,
    Vector2,
    Vector3,
    Vector4,
    Matrix3x3,
    Matrix4x4,
}

public enum HLSLBaseType : byte
{
    Unknown,
    Void,
    Float,
    Float2,
    Float3,
    Float4,
    Float3x3,
    Float4x4,
    Half,
    Half2,
    Half3,
    Half4,
    Half3x3,
    Half4x4,
    Bool,
    Int,
    Int2,
    Int3,
    Int4,
    Uint,
    Uint2,
    Uint3,
    Uint4,
    Texture,
    Sampler,
    Sampler2D,
    SamplerCube,
    UserDefined,
    Expression,
    Auto,
}

public static class HLSLBaseType_Ext
{
    public const HLSLBaseType Count = HLSLBaseType.Auto + 1;
    public const HLSLBaseType FirstInteger = HLSLBaseType.Bool;
    public const HLSLBaseType FirstNumeric = HLSLBaseType.Float;
    public const HLSLBaseType NumericCount = (HLSLBaseType)(LastNumeric - FirstNumeric + 1);
    public const HLSLBaseType LastInteger = HLSLBaseType.Uint4;
    public const HLSLBaseType LastNumeric = HLSLBaseType.Uint4;
}

public enum HLSLBinaryOp : sbyte
{
    And,
    Or,
    Add,
    Sub,
    Mul,
    Div,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,
    Equal,
    NotEqual,
    BitAnd,
    BitOr,
    BitXor,
    Assign,
    AddAssign,
    SubAssign,
    MulAssign,
    DivAssign,
}

public enum HLSLUnaryOp : byte
{
    Negative, // -x
    Positive, // +x
    Not, // !x
    PreIncrement, // ++x
    PreDecrement, // --x
    PostIncrement, // x++
    PostDecrement, // x--
    BitNot, // ~x
}

public enum HLSLArgumentModifier : byte
{
    None,
    In,
    Out,
    Inout,
    Uniform,
    Const,
}

[Flags]
public enum HLSLTypeFlags
{
    None = 0,
    Const = 0x01,
    Static = 0x02,
    Input = 0x100,
    Output = 0x200,
    Linear = 0x10000,
    Centroid = 0x20000,
    NoInterpolation = 0x40000,
    NoPerspective = 0x80000,
    Sample = 0x100000,
    NoPromote = 0x200000,
}

public enum HLSLAttributeType : byte
{
    Unknown,
    Unroll,
    Branch,
    Flatten,
    NoFastMath,
}

public enum HLSLAddressSpace : byte
{
    Undefined,
    Constant,
    Device,
    Thread,
    Shared,
}

// ─── Helper functions ───────────────────────────────────────────────────────

public static class HLSLTypeHelpers
{
    extension(HLSLBaseType baseType)
    {
        public bool IsSamplerType() =>
            baseType is HLSLBaseType.Sampler or HLSLBaseType.Sampler2D
                or HLSLBaseType.SamplerCube;

        public bool IsMatrixType() =>
            baseType is HLSLBaseType.Float3x3 or HLSLBaseType.Float4x4 or
                HLSLBaseType.Half3x3 or HLSLBaseType.Half4x4;

        public bool IsScalarType() =>
            baseType is HLSLBaseType.Float or HLSLBaseType.Half or HLSLBaseType.Bool or HLSLBaseType.Int or HLSLBaseType.Uint;

        public bool IsVectorType() =>
            baseType is HLSLBaseType.Float2 or HLSLBaseType.Float3 or HLSLBaseType.Float4 or
                HLSLBaseType.Half2 or HLSLBaseType.Half3 or HLSLBaseType.Half4 or
                HLSLBaseType.Int2 or HLSLBaseType.Int3 or HLSLBaseType.Int4 or
                HLSLBaseType.Uint2 or HLSLBaseType.Uint3 or HLSLBaseType.Uint4;
    }

    extension(HLSLType t)
    {
        public bool IsVectorType() => t.BaseType.IsVectorType();
        public bool IsScalarType() => t.BaseType.IsScalarType();
        public bool IsSamplerType() => t.BaseType.IsSamplerType();
    }

    extension(HLSLBinaryOp op)
    {
        public bool IsCompareOp() =>
            op is HLSLBinaryOp.Less or HLSLBinaryOp.Greater or HLSLBinaryOp.LessEqual
                or HLSLBinaryOp.GreaterEqual or HLSLBinaryOp.Equal or HLSLBinaryOp.NotEqual;

        public bool IsArithmeticOp() =>
            op is HLSLBinaryOp.Add or HLSLBinaryOp.Sub or HLSLBinaryOp.Mul or HLSLBinaryOp.Div;

        public bool IsLogicOp() =>
            op is HLSLBinaryOp.And or HLSLBinaryOp.Or;

        public bool IsAssignOp() =>
            op is HLSLBinaryOp.Assign or HLSLBinaryOp.AddAssign or HLSLBinaryOp.SubAssign
                or HLSLBinaryOp.MulAssign or HLSLBinaryOp.DivAssign;
    }
}

// ─── Types ──────────────────────────────────────────────────────────────────

public class HLSLType
{
    public HLSLBaseType BaseType;
    public HLSLBaseType SamplerType = HLSLBaseType.Float;
    public string? TypeName;
    public bool Array;
    public HLSLExpression? ArraySize;
    public int Flags;
    public HLSLAddressSpace AddressSpace = HLSLAddressSpace.Undefined;

    public HLSLType()
    {
    }

    public HLSLType(HLSLBaseType baseType)
    {
        BaseType = baseType;
    }

    public HLSLType Clone()
    {
        return new HLSLType
        {
            BaseType = BaseType,
            SamplerType = SamplerType,
            TypeName = TypeName,
            Array = Array,
            ArraySize = ArraySize,
            Flags = Flags,
            AddressSpace = AddressSpace,
        };
    }
}

// ─── AST Nodes ──────────────────────────────────────────────────────────────

public abstract class HLSLNode
{
    public HLSLNodeType NodeType;
    public string? FileName;
    public int Line;

    public static readonly Dictionary<Type, (HLSLNodeType Type, Func<HLSLNode> Ctor)> NodeTypes =
        new()
        {
            [typeof(HLSLRoot)] = (HLSLRoot.SType, static () => new HLSLRoot()),
            [typeof(HLSLAttribute)] = (HLSLAttribute.SType, static () => new HLSLAttribute()),
            [typeof(HLSLDeclaration)] = (HLSLDeclaration.SType, static () => new HLSLDeclaration()),
            [typeof(HLSLStruct)] = (HLSLStruct.SType, static () => new HLSLStruct()),
            [typeof(HLSLStructField)] = (HLSLStructField.SType, static () => new HLSLStructField()),
            [typeof(HLSLBuffer)] = (HLSLBuffer.SType, static () => new HLSLBuffer()),
            [typeof(HLSLFunction)] = (HLSLFunction.SType, static () => new HLSLFunction()),
            [typeof(HLSLArgument)] = (HLSLArgument.SType, static () => new HLSLArgument()),
            [typeof(HLSLExpressionStatement)] = (HLSLExpressionStatement.SType, static () => new HLSLExpressionStatement()),
            [typeof(HLSLExpression)] = (HLSLExpression.SType, static () => new HLSLExpression()),
            [typeof(HLSLReturnStatement)] = (HLSLReturnStatement.SType, static () => new HLSLReturnStatement()),
            [typeof(HLSLDiscardStatement)] = (HLSLDiscardStatement.SType, static () => new HLSLDiscardStatement()),
            [typeof(HLSLBreakStatement)] = (HLSLBreakStatement.SType, static () => new HLSLBreakStatement()),
            [typeof(HLSLContinueStatement)] = (HLSLContinueStatement.SType, static () => new HLSLContinueStatement()),
            [typeof(HLSLIfStatement)] = (HLSLIfStatement.SType, static () => new HLSLIfStatement()),
            [typeof(HLSLForStatement)] = (HLSLForStatement.SType, static () => new HLSLForStatement()),
            [typeof(HLSLBlockStatement)] = (HLSLBlockStatement.SType, static () => new HLSLBlockStatement()),
            [typeof(HLSLUnaryExpression)] = (HLSLUnaryExpression.SType, static () => new HLSLUnaryExpression()),
            [typeof(HLSLBinaryExpression)] = (HLSLBinaryExpression.SType, static () => new HLSLBinaryExpression()),
            [typeof(HLSLConditionalExpression)] = (HLSLConditionalExpression.SType, static () => new HLSLConditionalExpression()),
            [typeof(HLSLCastingExpression)] = (HLSLCastingExpression.SType, static () => new HLSLCastingExpression()),
            [typeof(HLSLLiteralExpression)] = (HLSLLiteralExpression.SType, static () => new HLSLLiteralExpression()),
            [typeof(HLSLIdentifierExpression)] = (HLSLIdentifierExpression.SType, static () => new HLSLIdentifierExpression()),
            [typeof(HLSLConstructorExpression)] = (HLSLConstructorExpression.SType, static () => new HLSLConstructorExpression()),
            [typeof(HLSLMemberAccess)] = (HLSLMemberAccess.SType, static () => new HLSLMemberAccess()),
            [typeof(HLSLArrayAccess)] = (HLSLArrayAccess.SType, static () => new HLSLArrayAccess()),
            [typeof(HLSLFunctionCall)] = (HLSLFunctionCall.SType, static () => new HLSLFunctionCall()),
            [typeof(HLSLStateAssignment)] = (HLSLStateAssignment.SType, static () => new HLSLStateAssignment()),
            [typeof(HLSLSamplerState)] = (HLSLSamplerState.SType, static () => new HLSLSamplerState()),
            [typeof(HLSLPass)] = (HLSLPass.SType, static () => new HLSLPass()),
            [typeof(HLSLTechnique)] = (HLSLTechnique.SType, static () => new HLSLTechnique()),
            [typeof(HLSLAttribute)] = (HLSLAttribute.SType, static () => new HLSLAttribute()),
            [typeof(HLSLPipeline)] = (HLSLPipeline.SType, static () => new HLSLPipeline()),
            [typeof(HLSLStage)] = (HLSLStage.SType, static () => new HLSLStage()),
        };
}

public class HLSLRoot : HLSLNode
{
    public const HLSLNodeType SType = HLSLNodeType.Root;
    public HLSLStatement? Statement;

    public IEnumerable<HLSLStatement> Statements()
    {
        var statement = Statement;
        while (statement != null)
        {
            yield return statement;
            statement = statement.NextStatement;
        }
    }
}

public abstract class HLSLStatement : HLSLNode
{
    public HLSLStatement? NextStatement;
    public HLSLAttribute? Attributes;
    public bool Hidden;
}

public class HLSLAttribute : HLSLNode
{
    public const HLSLNodeType SType = HLSLNodeType.Attribute;
    public HLSLAttributeType AttributeType = HLSLAttributeType.Unknown;
    public HLSLExpression? Argument;
    public HLSLAttribute? NextAttribute;
}

public class HLSLDeclaration : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.Declaration;
    public string? Name;
    public HLSLType Type = new();
    public string? RegisterName;
    public string? Semantic;
    public HLSLDeclaration? NextDeclaration;
    public HLSLExpression? Assignment;
    public HLSLBuffer? Buffer;
}

public class HLSLStruct : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.Struct;
    public string? Name;
    public HLSLStructField? Field;
}

public class HLSLStructField : HLSLNode
{
    public const HLSLNodeType SType = HLSLNodeType.StructField;
    public string? Name;
    public HLSLType Type = new();
    public string? Semantic;
    public string? SvSemantic;
    public HLSLStructField? NextField;
    public bool Hidden;
}

public class HLSLBuffer : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.Buffer;
    public string? Name;
    public string? RegisterName;
    public HLSLDeclaration? Field;
}

public class HLSLFunction : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.Function;
    public string? Name;
    public HLSLType ReturnType = new();
    public string? Semantic;
    public string? SvSemantic;
    public int NumArguments;
    public int NumOutputArguments;
    public HLSLArgument? Argument;
    public HLSLStatement? Statement;
    public HLSLFunction? Forward;
}

public class HLSLArgument : HLSLNode
{
    public const HLSLNodeType SType = HLSLNodeType.Argument;
    public string? Name;
    public HLSLArgumentModifier Modifier = HLSLArgumentModifier.None;
    public HLSLType Type = new();
    public string? Semantic;
    public string? SvSemantic;
    public HLSLExpression? DefaultValue;
    public HLSLArgument? NextArgument;
    public bool Hidden;
}

// ─── Statements ─────────────────────────────────────────────────────────────

public class HLSLExpressionStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.ExpressionStatement;
    public HLSLExpression? Expression;
}

public class HLSLReturnStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.ReturnStatement;
    public HLSLExpression? Expression;
}

public class HLSLDiscardStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.DiscardStatement;
}

public class HLSLBreakStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.BreakStatement;
}

public class HLSLContinueStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.ContinueStatement;
}

public class HLSLIfStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.IfStatement;
    public HLSLExpression? Condition;
    public HLSLStatement? Statement;
    public HLSLStatement? ElseStatement;
    public bool IsStatic;
}

public class HLSLForStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.ForStatement;
    public HLSLDeclaration? Initialization;
    public HLSLExpression? Condition;
    public HLSLExpression? Increment;
    public HLSLStatement? Statement;
}

public class HLSLBlockStatement : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.BlockStatement;
    public HLSLStatement? Statement;
}

// ─── Expressions ────────────────────────────────────────────────────────────

public class HLSLExpression : HLSLNode
{
    public const HLSLNodeType SType = HLSLNodeType.Expression;
    public HLSLType ExpressionType = new();
    public HLSLExpression? NextExpression;
}

public class HLSLUnaryExpression : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.UnaryExpression;
    public HLSLUnaryOp UnaryOp;
    public HLSLExpression? Expression;
}

public class HLSLBinaryExpression : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.BinaryExpression;
    public HLSLBinaryOp BinaryOp;
    public HLSLExpression? Expression1;
    public HLSLExpression? Expression2;
}

public class HLSLConditionalExpression : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.ConditionalExpression;
    public HLSLExpression? Condition;
    public HLSLExpression? TrueExpression;
    public HLSLExpression? FalseExpression;
}

public class HLSLCastingExpression : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.CastingExpression;
    public HLSLType Type = new();
    public HLSLExpression? Expression;
}

public class HLSLLiteralExpression : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.LiteralExpression;
    public HLSLBaseType Type;
    public bool BValue;
    public float FValue;
    public int IValue;
}

public class HLSLIdentifierExpression : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.IdentifierExpression;
    public string? Name;
    public bool Global;
}

public class HLSLConstructorExpression : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.ConstructorExpression;
    public HLSLType Type = new();
    public HLSLExpression? Argument;
}

public class HLSLMemberAccess : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.MemberAccess;
    public HLSLExpression? Object;
    public string? Field;
    public bool Swizzle;
}

public class HLSLArrayAccess : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.ArrayAccess;
    public HLSLExpression? Array;
    public HLSLExpression? Index;
}

public class HLSLFunctionCall : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.FunctionCall;
    public HLSLFunction? Function;
    public HLSLExpression? Argument;
    public int NumArguments;
}

public class HLSLStateAssignment : HLSLNode
{
    public const HLSLNodeType SType = HLSLNodeType.StateAssignment;
    public string? StateName;
    public int D3dRenderState;
    public int IValue;
    public float FValue;
    public string? SValue;
    public HLSLStateAssignment? NextStateAssignment;
}

public class HLSLSamplerState : HLSLExpression
{
    public new const HLSLNodeType SType = HLSLNodeType.SamplerState;
    public int NumStateAssignments;
    public HLSLStateAssignment? StateAssignments;
}

public class HLSLPass : HLSLNode
{
    public const HLSLNodeType SType = HLSLNodeType.Pass;
    public string? Name;
    public int NumStateAssignments;
    public HLSLStateAssignment? StateAssignments;
    public HLSLPass? NextPass;
}

public class HLSLTechnique : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.Technique;
    public string? Name;
    public int NumPasses;
    public HLSLPass? Passes;
}

public class HLSLPipeline : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.Pipeline;
    public string? Name;
    public int NumStateAssignments;
    public HLSLStateAssignment? StateAssignments;
}

public class HLSLStage : HLSLStatement
{
    public const HLSLNodeType SType = HLSLNodeType.Stage;
    public string? Name;
    public HLSLStatement? Statement;
    public HLSLDeclaration? Inputs;
    public HLSLDeclaration? Outputs;
}

// ─── Tree ───────────────────────────────────────────────────────────────────

public class HLSLTree
{
    private readonly HashSet<string> _stringPool = new(StringComparer.Ordinal);
    private readonly HLSLRoot _root;

    public HLSLTree()
    {
        _root = AddNode<HLSLRoot>(null, 1);
    }

    public HLSLRoot Root => _root;

    public static T AddNode<T>(string? fileName, int line) where T : HLSLNode
    {
        if (HLSLNode.NodeTypes.TryGetValue(typeof(T), out var nodeInfo))
        {
            var node = nodeInfo.Ctor();
            node.FileName = fileName;
            node.Line = line;
            node.NodeType = nodeInfo.Type;
            return (T)node;
        }

        throw new InvalidOperationException($"Type {typeof(T)} is not a valid {nameof(HLSLNode)} type.");
    }

    public HLSLFunction? FindFunction(string name)
    {
        var statement = _root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Function && statement is HLSLFunction fn && fn.Name == name)
                return fn;
            statement = statement.NextStatement;
        }

        return null;
    }

    public HLSLDeclaration? FindGlobalDeclaration(string name, out HLSLBuffer? bufferOut)
    {
        bufferOut = null;
        var statement = _root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Declaration && statement is HLSLDeclaration decl && decl.Name == name)
                return decl;
            if (statement.NodeType == HLSLNodeType.Buffer && statement is HLSLBuffer buffer)
            {
                var field = buffer.Field;
                while (field != null)
                {
                    if (field.Name == name)
                    {
                        bufferOut = buffer;
                        return field;
                    }

                    field = field.NextStatement as HLSLDeclaration;
                }
            }

            statement = statement.NextStatement;
        }

        return null;
    }

    public HLSLDeclaration? FindGlobalDeclaration(string name)
    {
        return FindGlobalDeclaration(name, out _);
    }

    public HLSLStruct? FindGlobalStruct(string name)
    {
        var statement = _root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Struct && statement is HLSLStruct s && s.Name == name)
                return s;
            statement = statement.NextStatement;
        }

        return null;
    }

    public HLSLTechnique? FindTechnique(string name)
    {
        var statement = _root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Technique && statement is HLSLTechnique t && t.Name == name)
                return t;
            statement = statement.NextStatement;
        }

        return null;
    }

    public HLSLPipeline? FindFirstPipeline() => FindNextPipeline(null);

    public HLSLPipeline? FindNextPipeline(HLSLPipeline? current)
    {
        var statement = current != null ? (HLSLStatement)current : _root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Pipeline && statement is HLSLPipeline p)
                return p;
            statement = statement.NextStatement;
        }

        return null;
    }

    public HLSLPipeline? FindPipeline(string name)
    {
        var statement = _root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Pipeline && statement is HLSLPipeline p && p.Name == name)
                return p;
            statement = statement.NextStatement;
        }

        return null;
    }

    public HLSLBuffer? FindBuffer(string name)
    {
        var statement = _root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Buffer && statement is HLSLBuffer b && b.Name == name)
                return b;
            statement = statement.NextStatement;
        }

        return null;
    }

    public bool GetExpressionValue(HLSLExpression? expression, out int value)
    {
        value = 0;
        if (expression == null) return false;

        if ((expression.ExpressionType.Flags & (int)HLSLTypeFlags.Const) == 0)
            return false;
        if (expression.ExpressionType.BaseType != HLSLBaseType.Int && expression.ExpressionType.BaseType != HLSLBaseType.Bool)
            return false;
        if (expression.ExpressionType.Array)
            return false;

        if (expression.NodeType == HLSLNodeType.BinaryExpression && expression is HLSLBinaryExpression bin)
        {
            if (!GetExpressionValue(bin.Expression1, out var v1) || !GetExpressionValue(bin.Expression2, out var v2))
                return false;
            switch (bin.BinaryOp)
            {
                case HLSLBinaryOp.And:
                    value = (v1 != 0 && v2 != 0) ? 1 : 0;
                    return true;
                case HLSLBinaryOp.Or:
                    value = (v1 != 0 || v2 != 0) ? 1 : 0;
                    return true;
                case HLSLBinaryOp.Add:
                    value = v1 + v2;
                    return true;
                case HLSLBinaryOp.Sub:
                    value = v1 - v2;
                    return true;
                case HLSLBinaryOp.Mul:
                    value = v1 * v2;
                    return true;
                case HLSLBinaryOp.Div:
                    value = v1 / v2;
                    return true;
                case HLSLBinaryOp.Less:
                    value = v1 < v2 ? 1 : 0;
                    return true;
                case HLSLBinaryOp.Greater:
                    value = v1 > v2 ? 1 : 0;
                    return true;
                case HLSLBinaryOp.LessEqual:
                    value = v1 <= v2 ? 1 : 0;
                    return true;
                case HLSLBinaryOp.GreaterEqual:
                    value = v1 >= v2 ? 1 : 0;
                    return true;
                case HLSLBinaryOp.Equal:
                    value = v1 == v2 ? 1 : 0;
                    return true;
                case HLSLBinaryOp.NotEqual:
                    value = v1 != v2 ? 1 : 0;
                    return true;
                case HLSLBinaryOp.BitAnd:
                    value = v1 & v2;
                    return true;
                case HLSLBinaryOp.BitOr:
                    value = v1 | v2;
                    return true;
                case HLSLBinaryOp.BitXor:
                    value = v1 ^ v2;
                    return true;
                default: return false;
            }
        }

        if (expression.NodeType == HLSLNodeType.UnaryExpression && expression is HLSLUnaryExpression un)
        {
            if (!GetExpressionValue(un.Expression, out value))
                return false;
            switch (un.UnaryOp)
            {
                case HLSLUnaryOp.Negative:
                    value = -value;
                    return true;
                case HLSLUnaryOp.Positive: return true;
                case HLSLUnaryOp.Not:
                    value = value == 0 ? 1 : 0;
                    return true;
                case HLSLUnaryOp.BitNot:
                    value = ~value;
                    return true;
                default: return false;
            }
        }

        if (expression.NodeType == HLSLNodeType.IdentifierExpression && expression is HLSLIdentifierExpression id)
        {
            var decl = FindGlobalDeclaration(id.Name!);
            if (decl == null || (decl.Type.Flags & (int)HLSLTypeFlags.Const) == 0)
                return false;
            return GetExpressionValue(decl.Assignment, out value);
        }

        if (expression.NodeType == HLSLNodeType.LiteralExpression && expression is HLSLLiteralExpression lit)
        {
            if (lit.ExpressionType.BaseType == HLSLBaseType.Int)
            {
                value = lit.IValue;
                return true;
            }

            if (lit.ExpressionType.BaseType == HLSLBaseType.Bool)
            {
                value = lit.BValue ? 1 : 0;
                return true;
            }
        }

        return false;
    }

    public bool NeedsFunction(string name)
    {
        var visitor = new NeedsFunctionVisitor { Name = name };
        visitor.VisitRoot(_root);
        return visitor.Result;
    }

    private class NeedsFunctionVisitor : HLSLTreeVisitor
    {
        public string? Name;
        public bool Result;

        public override void VisitTopLevelStatement(HLSLStatement node)
        {
            if (!node.Hidden) base.VisitTopLevelStatement(node);
        }

        public override void VisitFunctionCall(HLSLFunctionCall node)
        {
            Result = Result || node.Function?.Name == Name;
            base.VisitFunctionCall(node);
        }
    }
}

// ─── Static lookup tables ───────────────────────────────────────────────────

public static class HLSLTypeTables
{
    public static readonly HLSLTypeDimension[] BaseTypeDimension = new HLSLTypeDimension[(int)HLSLBaseType_Ext.Count]
    {
        HLSLTypeDimension.None, // Unknown
        HLSLTypeDimension.None, // Void
        HLSLTypeDimension.Scalar, // Float
        HLSLTypeDimension.Vector2, // Float2
        HLSLTypeDimension.Vector3, // Float3
        HLSLTypeDimension.Vector4, // Float4
        HLSLTypeDimension.Matrix3x3, // Float3x3
        HLSLTypeDimension.Matrix4x4, // Float4x4
        HLSLTypeDimension.Scalar, // Half
        HLSLTypeDimension.Vector2, // Half2
        HLSLTypeDimension.Vector3, // Half3
        HLSLTypeDimension.Vector4, // Half4
        HLSLTypeDimension.Matrix3x3, // Half3x3
        HLSLTypeDimension.Matrix4x4, // Half4x4
        HLSLTypeDimension.Scalar, // Bool
        HLSLTypeDimension.Scalar, // Int
        HLSLTypeDimension.Vector2, // Int2
        HLSLTypeDimension.Vector3, // Int3
        HLSLTypeDimension.Vector4, // Int4
        HLSLTypeDimension.Scalar, // Uint
        HLSLTypeDimension.Vector2, // Uint2
        HLSLTypeDimension.Vector3, // Uint3
        HLSLTypeDimension.Vector4, // Uint4
        HLSLTypeDimension.None, // Texture
        HLSLTypeDimension.None, // Sampler
        HLSLTypeDimension.None, // Sampler2D
        HLSLTypeDimension.None, // Sampler3D
        HLSLTypeDimension.None, // SamplerCube
        HLSLTypeDimension.None, // UserDefined
        HLSLTypeDimension.None, // Auto
    };

    public static readonly HLSLBaseType[] ScalarBaseType = new HLSLBaseType[(int)HLSLBaseType_Ext.Count]
    {
        HLSLBaseType.Unknown, // Unknown
        HLSLBaseType.Void, // Void
        HLSLBaseType.Float, // Float
        HLSLBaseType.Float, // Float2
        HLSLBaseType.Float, // Float3
        HLSLBaseType.Float, // Float4
        HLSLBaseType.Float, // Float3x3
        HLSLBaseType.Float, // Float4x4
        HLSLBaseType.Half, // Half
        HLSLBaseType.Half, // Half2
        HLSLBaseType.Half, // Half3
        HLSLBaseType.Half, // Half4
        HLSLBaseType.Half, // Half3x3
        HLSLBaseType.Half, // Half4x4
        HLSLBaseType.Bool, // Bool
        HLSLBaseType.Int, // Int
        HLSLBaseType.Int, // Int2
        HLSLBaseType.Int, // Int3
        HLSLBaseType.Int, // Int4
        HLSLBaseType.Uint, // Uint
        HLSLBaseType.Uint, // Uint2
        HLSLBaseType.Uint, // Uint3
        HLSLBaseType.Uint, // Uint4
        HLSLBaseType.Unknown, // Texture
        HLSLBaseType.Unknown, // Sampler
        HLSLBaseType.Unknown, // Sampler2D
        HLSLBaseType.Unknown, // Sampler3D
        HLSLBaseType.Unknown, // SamplerCube
        HLSLBaseType.Unknown, // UserDefined
        HLSLBaseType.Unknown, // Auto
    };
}

// ─── Visitor ────────────────────────────────────────────────────────────────

public class HLSLTreeVisitor
{
    public virtual void VisitType(HLSLType type)
    {
    }

    public virtual void VisitRoot(HLSLRoot root)
    {
        var statement = root.Statement;
        while (statement != null)
        {
            VisitTopLevelStatement(statement);
            statement = statement.NextStatement;
        }
    }

    public virtual void VisitTopLevelStatement(HLSLStatement node)
    {
        switch (node.NodeType)
        {
            case HLSLNodeType.Declaration: VisitDeclaration((HLSLDeclaration)node); break;
            case HLSLNodeType.Struct: VisitStruct((HLSLStruct)node); break;
            case HLSLNodeType.Buffer: VisitBuffer((HLSLBuffer)node); break;
            case HLSLNodeType.Function: VisitFunction((HLSLFunction)node); break;
            case HLSLNodeType.Technique: VisitTechnique((HLSLTechnique)node); break;
            case HLSLNodeType.Pipeline: VisitPipeline((HLSLPipeline)node); break;
        }
    }

    public virtual void VisitStatements(HLSLStatement? statement)
    {
        while (statement != null)
        {
            VisitStatement(statement);
            statement = statement.NextStatement;
        }
    }

    public virtual void VisitStatement(HLSLStatement node)
    {
        switch (node.NodeType)
        {
            case HLSLNodeType.Declaration: VisitDeclaration((HLSLDeclaration)node); break;
            case HLSLNodeType.ExpressionStatement: VisitExpressionStatement((HLSLExpressionStatement)node); break;
            case HLSLNodeType.ReturnStatement: VisitReturnStatement((HLSLReturnStatement)node); break;
            case HLSLNodeType.DiscardStatement: VisitDiscardStatement((HLSLDiscardStatement)node); break;
            case HLSLNodeType.BreakStatement: VisitBreakStatement((HLSLBreakStatement)node); break;
            case HLSLNodeType.ContinueStatement: VisitContinueStatement((HLSLContinueStatement)node); break;
            case HLSLNodeType.IfStatement: VisitIfStatement((HLSLIfStatement)node); break;
            case HLSLNodeType.ForStatement: VisitForStatement((HLSLForStatement)node); break;
            case HLSLNodeType.BlockStatement: VisitBlockStatement((HLSLBlockStatement)node); break;
        }
    }

    public virtual void VisitDeclaration(HLSLDeclaration node)
    {
        VisitType(node.Type);
        if (node.Assignment != null) VisitExpression(node.Assignment);
        if (node.NextDeclaration != null) VisitDeclaration(node.NextDeclaration);
    }

    public virtual void VisitStruct(HLSLStruct node)
    {
        var field = node.Field;
        while (field != null)
        {
            VisitStructField(field);
            field = field.NextField;
        }
    }

    public virtual void VisitStructField(HLSLStructField node) => VisitType(node.Type);

    public virtual void VisitBuffer(HLSLBuffer node)
    {
        var field = node.Field;
        while (field != null)
        {
            VisitDeclaration(field);
            field = field.NextStatement as HLSLDeclaration;
        }
    }

    public virtual void VisitFunction(HLSLFunction node)
    {
        VisitType(node.ReturnType);
        var arg = node.Argument;
        while (arg != null)
        {
            VisitArgument(arg);
            arg = arg.NextArgument;
        }

        VisitStatements(node.Statement);
    }

    public virtual void VisitArgument(HLSLArgument node)
    {
        VisitType(node.Type);
        if (node.DefaultValue != null) VisitExpression(node.DefaultValue);
    }

    public virtual void VisitExpressionStatement(HLSLExpressionStatement node)
    {
        if (node.Expression != null) VisitExpression(node.Expression);
    }

    public virtual void VisitExpression(HLSLExpression node)
    {
        VisitType(node.ExpressionType);
        switch (node.NodeType)
        {
            case HLSLNodeType.UnaryExpression: VisitUnaryExpression((HLSLUnaryExpression)node); break;
            case HLSLNodeType.BinaryExpression: VisitBinaryExpression((HLSLBinaryExpression)node); break;
            case HLSLNodeType.ConditionalExpression: VisitConditionalExpression((HLSLConditionalExpression)node); break;
            case HLSLNodeType.CastingExpression: VisitCastingExpression((HLSLCastingExpression)node); break;
            case HLSLNodeType.LiteralExpression: VisitLiteralExpression((HLSLLiteralExpression)node); break;
            case HLSLNodeType.IdentifierExpression: VisitIdentifierExpression((HLSLIdentifierExpression)node); break;
            case HLSLNodeType.ConstructorExpression: VisitConstructorExpression((HLSLConstructorExpression)node); break;
            case HLSLNodeType.MemberAccess: VisitMemberAccess((HLSLMemberAccess)node); break;
            case HLSLNodeType.ArrayAccess: VisitArrayAccess((HLSLArrayAccess)node); break;
            case HLSLNodeType.FunctionCall: VisitFunctionCall((HLSLFunctionCall)node); break;
            case HLSLNodeType.SamplerState: VisitSamplerState((HLSLSamplerState)node); break;
        }
    }

    public virtual void VisitReturnStatement(HLSLReturnStatement node)
    {
        if (node.Expression != null) VisitExpression(node.Expression);
    }

    public virtual void VisitDiscardStatement(HLSLDiscardStatement node)
    {
    }

    public virtual void VisitBreakStatement(HLSLBreakStatement node)
    {
    }

    public virtual void VisitContinueStatement(HLSLContinueStatement node)
    {
    }

    public virtual void VisitIfStatement(HLSLIfStatement node)
    {
        if (node.Condition != null) VisitExpression(node.Condition);
        VisitStatements(node.Statement);
        if (node.ElseStatement != null) VisitStatements(node.ElseStatement);
    }

    public virtual void VisitForStatement(HLSLForStatement node)
    {
        if (node.Initialization != null) VisitDeclaration(node.Initialization);
        if (node.Condition != null) VisitExpression(node.Condition);
        if (node.Increment != null) VisitExpression(node.Increment);
        VisitStatements(node.Statement);
    }

    public virtual void VisitBlockStatement(HLSLBlockStatement node) => VisitStatements(node.Statement);

    public virtual void VisitUnaryExpression(HLSLUnaryExpression node)
    {
        if (node.Expression != null) VisitExpression(node.Expression);
    }

    public virtual void VisitBinaryExpression(HLSLBinaryExpression node)
    {
        if (node.Expression1 != null) VisitExpression(node.Expression1);
        if (node.Expression2 != null) VisitExpression(node.Expression2);
    }

    public virtual void VisitConditionalExpression(HLSLConditionalExpression node)
    {
        if (node.Condition != null) VisitExpression(node.Condition);
        if (node.FalseExpression != null) VisitExpression(node.FalseExpression);
        if (node.TrueExpression != null) VisitExpression(node.TrueExpression);
    }

    public virtual void VisitCastingExpression(HLSLCastingExpression node)
    {
        VisitType(node.Type);
        if (node.Expression != null) VisitExpression(node.Expression);
    }

    public virtual void VisitLiteralExpression(HLSLLiteralExpression node)
    {
    }

    public virtual void VisitIdentifierExpression(HLSLIdentifierExpression node)
    {
    }

    public virtual void VisitConstructorExpression(HLSLConstructorExpression node)
    {
        var arg = node.Argument;
        while (arg != null)
        {
            VisitExpression(arg);
            arg = arg.NextExpression;
        }
    }

    public virtual void VisitMemberAccess(HLSLMemberAccess node)
    {
        if (node.Object != null) VisitExpression(node.Object);
    }

    public virtual void VisitArrayAccess(HLSLArrayAccess node)
    {
        if (node.Array != null) VisitExpression(node.Array);
        if (node.Index != null) VisitExpression(node.Index);
    }

    public virtual void VisitFunctionCall(HLSLFunctionCall node)
    {
        var arg = node.Argument;
        while (arg != null)
        {
            VisitExpression(arg);
            arg = arg.NextExpression;
        }
    }

    public virtual void VisitStateAssignment(HLSLStateAssignment node)
    {
    }

    public virtual void VisitSamplerState(HLSLSamplerState node)
    {
        var sa = node.StateAssignments;
        while (sa != null)
        {
            VisitStateAssignment(sa);
            sa = sa.NextStateAssignment;
        }
    }

    public virtual void VisitPass(HLSLPass node)
    {
        var sa = node.StateAssignments;
        while (sa != null)
        {
            VisitStateAssignment(sa);
            sa = sa.NextStateAssignment;
        }
    }

    public virtual void VisitTechnique(HLSLTechnique node)
    {
        var pass = node.Passes;
        while (pass != null)
        {
            VisitPass(pass);
            pass = pass.NextPass;
        }
    }

    public virtual void VisitPipeline(HLSLPipeline node)
    {
    }

    public virtual void VisitFunctions(HLSLRoot root)
    {
        var st = root.Statement;
        while (st != null)
        {
            if (st.NodeType == HLSLNodeType.Function) VisitFunction((HLSLFunction)st);
            st = st.NextStatement;
        }
    }

    public virtual void VisitParameters(HLSLRoot root)
    {
        var st = root.Statement;
        while (st != null)
        {
            if (st.NodeType == HLSLNodeType.Declaration) VisitDeclaration((HLSLDeclaration)st);
            st = st.NextStatement;
        }
    }
}

// ─── Tree operations ────────────────────────────────────────────────────────

public static class HLSLTreeOperations
{
    public static void PruneTree(HLSLTree tree, string entryName0, string? entryName1 = null)
    {
        var root = tree.Root;
        new ResetHiddenFlagVisitor().VisitRoot(root);

        var entry = tree.FindFunction(entryName0);
        if (entry != null) new MarkVisibleStatementsVisitor(tree).VisitFunction(entry);

        if (entryName1 != null)
        {
            entry = tree.FindFunction(entryName1);
            if (entry != null) new MarkVisibleStatementsVisitor(tree).VisitFunction(entry);
        }

        // Mark buffers visible if any field is visible
        var statement = root.Statement;
        while (statement != null)
        {
            if (statement.NodeType == HLSLNodeType.Buffer && statement is HLSLBuffer buffer)
            {
                var field = buffer.Field;
                while (field != null)
                {
                    if (!field.Hidden)
                    {
                        buffer.Hidden = false;
                        break;
                    }

                    field = field.NextStatement as HLSLDeclaration;
                }
            }

            statement = statement.NextStatement;
        }
    }

    public static void SortTree(HLSLTree tree)
    {
        var root = tree.Root;
        HLSLStatement? structs = null, lastStruct = null;
        HLSLStatement? constDecls = null, lastConstDecl = null;
        HLSLStatement? decls = null, lastDecl = null;
        HLSLStatement? functions = null, lastFunc = null;
        HLSLStatement? other = null, lastOther = null;

        var statement = root.Statement;
        while (statement != null)
        {
            var next = statement.NextStatement;
            statement.NextStatement = null;

            if (statement.NodeType == HLSLNodeType.Struct)
            {
                structs ??= statement;
                lastStruct?.NextStatement = statement;
                lastStruct = statement;
            }
            else if (statement.NodeType is HLSLNodeType.Declaration or HLSLNodeType.Buffer)
            {
                if (statement.NodeType == HLSLNodeType.Declaration
                    && (((HLSLDeclaration)statement).Type.Flags & (int)HLSLTypeFlags.Const) != 0)
                {
                    constDecls ??= statement;
                    lastConstDecl?.NextStatement = statement;
                    lastConstDecl = statement;
                }
                else
                {
                    decls ??= statement;
                    lastDecl?.NextStatement = statement;
                    lastDecl = statement;
                }
            }
            else if (statement.NodeType == HLSLNodeType.Function)
            {
                functions ??= statement;
                lastFunc?.NextStatement = statement;
                lastFunc = statement;
            }
            else
            {
                other ??= statement;
                lastOther?.NextStatement = statement;
                lastOther = statement;
            }

            statement = next;
        }

        HLSLStatement? first = structs, last = lastStruct;

        void Chain(HLSLStatement? head, HLSLStatement? tail)
        {
            if (head == null) return;
            if (first == null) first = head;
            else last!.NextStatement = head;

            last = tail;
        }

        Chain(constDecls, lastConstDecl);
        Chain(decls, lastDecl);
        Chain(functions, lastFunc);
        Chain(other, lastOther);

        root.Statement = first;
    }

    private class ResetHiddenFlagVisitor : HLSLTreeVisitor
    {
        public override void VisitTopLevelStatement(HLSLStatement statement)
        {
            statement.Hidden = true;
            if (statement.NodeType == HLSLNodeType.Buffer) VisitBuffer((HLSLBuffer)statement);
        }

        public override void VisitDeclaration(HLSLDeclaration node) => node.Hidden = true;
        public override void VisitArgument(HLSLArgument node) => node.Hidden = false;
    }

    private class MarkVisibleStatementsVisitor(HLSLTree tree) : HLSLTreeVisitor
    {
        public override void VisitFunction(HLSLFunction node)
        {
            node.Hidden = false;
            base.VisitFunction(node);
            if (node.Forward != null) VisitFunction(node.Forward);
        }

        public override void VisitFunctionCall(HLSLFunctionCall node)
        {
            base.VisitFunctionCall(node);
            if (node.Function is { Hidden: true })
                VisitFunction(node.Function);
        }

        public override void VisitIdentifierExpression(HLSLIdentifierExpression node)
        {
            base.VisitIdentifierExpression(node);
            if (node.Global)
            {
                var decl = tree.FindGlobalDeclaration(node.Name!);
                if (decl is { Hidden: true })
                {
                    decl.Hidden = false;
                    VisitDeclaration(decl);
                }
            }
        }

        public override void VisitType(HLSLType type)
        {
            if (type.BaseType == HLSLBaseType.UserDefined)
            {
                var s = tree.FindGlobalStruct(type.TypeName!);
                if (s != null)
                {
                    s.Hidden = false;
                    VisitStruct(s);
                }
            }
        }
    }
}
