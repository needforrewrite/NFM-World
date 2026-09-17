// Ported from HLSLParser.h / HLSLParser.cpp (M4 / Unknown Worlds Entertainment)

using System;
using System.Collections.Generic;

// ReSharper disable InconsistentNaming

namespace NFMWorld.ShaderSourceGen;

public ref struct HLSLParser(ReadOnlySpan<char> fileName, ReadOnlySpan<char> buffer, int length)
{
    // ── Data tables ─────────────────────────────────────────────────────────

    private enum NumericType { Float, Half, Bool, Int, Uint, Count, NaN }

    private readonly record struct BaseTypeDescription(
        string TypeName,
        NumericType NumericType,
        int NumComponents,
        int NumDimensions,
        int Height,
        int BinaryOpRank);

    private readonly record struct EffectStateValue(string? Name, int Value);

    private readonly record struct EffectState(string Name, int D3drs, EffectStateValue[]? Values);

    private enum CompareFunctionsResult : byte { FunctionsEqual, Function1Better, Function2Better }

    // ── Intrinsics ──────────────────────────────────────────────────────────

    private struct Intrinsic
    {
        public readonly HLSLFunction Function;
        public readonly HLSLArgument[] Arguments;

        public Intrinsic(string name, HLSLBaseType returnType, params ReadOnlySpan<HLSLBaseType> argTypes)
        {
            Function = new HLSLFunction { Name = name, ReturnType = new HLSLType(returnType), NumArguments = argTypes.Length };
            Arguments = new HLSLArgument[argTypes.Length];
            for (var i = 0; i < argTypes.Length; i++)
            {
                Arguments[i] = new HLSLArgument { Type = new HLSLType(argTypes[i]) { Flags = (int)HLSLTypeFlags.Const } };
                if (i > 0) Arguments[i - 1].NextArgument = Arguments[i];
            }
            if (argTypes.Length > 0) Function.Argument = Arguments[0];
        }
    }

    private static Intrinsic[] BuildIntrinsics()
    {
        var list = new List<Intrinsic>();

        void Float1(string n)
        {
            list.Add(new Intrinsic(n, HLSLBaseType.Float, HLSLBaseType.Float));
            list.Add(new Intrinsic(n, HLSLBaseType.Float2, HLSLBaseType.Float2));
            list.Add(new Intrinsic(n, HLSLBaseType.Float3, HLSLBaseType.Float3));
            list.Add(new Intrinsic(n, HLSLBaseType.Float4, HLSLBaseType.Float4));
            list.Add(new Intrinsic(n, HLSLBaseType.Half, HLSLBaseType.Half));
            list.Add(new Intrinsic(n, HLSLBaseType.Half2, HLSLBaseType.Half2));
            list.Add(new Intrinsic(n, HLSLBaseType.Half3, HLSLBaseType.Half3));
            list.Add(new Intrinsic(n, HLSLBaseType.Half4, HLSLBaseType.Half4));
        }

        void Float2(string n)
        {
            list.Add(new Intrinsic(n, HLSLBaseType.Float, HLSLBaseType.Float, HLSLBaseType.Float));
            list.Add(new Intrinsic(n, HLSLBaseType.Float2, HLSLBaseType.Float2, HLSLBaseType.Float2));
            list.Add(new Intrinsic(n, HLSLBaseType.Float3, HLSLBaseType.Float3, HLSLBaseType.Float3));
            list.Add(new Intrinsic(n, HLSLBaseType.Float4, HLSLBaseType.Float4, HLSLBaseType.Float4));
            list.Add(new Intrinsic(n, HLSLBaseType.Half, HLSLBaseType.Half, HLSLBaseType.Half));
            list.Add(new Intrinsic(n, HLSLBaseType.Half2, HLSLBaseType.Half2, HLSLBaseType.Half2));
            list.Add(new Intrinsic(n, HLSLBaseType.Half3, HLSLBaseType.Half3, HLSLBaseType.Half3));
            list.Add(new Intrinsic(n, HLSLBaseType.Half4, HLSLBaseType.Half4, HLSLBaseType.Half4));
        }

        void Float3(string n)
        {
            list.Add(new Intrinsic(n, HLSLBaseType.Float, HLSLBaseType.Float, HLSLBaseType.Float, HLSLBaseType.Float));
            list.Add(new Intrinsic(n, HLSLBaseType.Float2, HLSLBaseType.Float2, HLSLBaseType.Float2, HLSLBaseType.Float2));
            list.Add(new Intrinsic(n, HLSLBaseType.Float3, HLSLBaseType.Float3, HLSLBaseType.Float3, HLSLBaseType.Float3));
            list.Add(new Intrinsic(n, HLSLBaseType.Float4, HLSLBaseType.Float4, HLSLBaseType.Float4, HLSLBaseType.Float4));
            list.Add(new Intrinsic(n, HLSLBaseType.Half, HLSLBaseType.Half, HLSLBaseType.Half, HLSLBaseType.Half));
            list.Add(new Intrinsic(n, HLSLBaseType.Half2, HLSLBaseType.Half2, HLSLBaseType.Half2, HLSLBaseType.Half2));
            list.Add(new Intrinsic(n, HLSLBaseType.Half3, HLSLBaseType.Half3, HLSLBaseType.Half3, HLSLBaseType.Half3));
            list.Add(new Intrinsic(n, HLSLBaseType.Half4, HLSLBaseType.Half4, HLSLBaseType.Half4, HLSLBaseType.Half4));
        }

        void Intrinsic(string name, HLSLBaseType returnType, params ReadOnlySpan<HLSLBaseType> argTypes)
        {
            list.Add(new Intrinsic(name, returnType, argTypes));
        }

        Float1("abs");
        Float2("atan2");
        Float3("clamp");
        Float1("cos");
        
        Float3("lerp");
        Float3("smoothstep");
        
        Float1("floor");
        Float1("ceil");
        Float1("frac");
        
        Float2("fmod");

        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Float);
        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Float);
        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Float);
        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Float);
        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Half);
        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Half2);
        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Half3);
        Intrinsic("clip", HLSLBaseType.Void, HLSLBaseType.Half4);

        Intrinsic("dot", HLSLBaseType.Float, HLSLBaseType.Float, HLSLBaseType.Float);
        Intrinsic("dot", HLSLBaseType.Float, HLSLBaseType.Float2, HLSLBaseType.Float2);
        Intrinsic("dot", HLSLBaseType.Float, HLSLBaseType.Float3, HLSLBaseType.Float3);
        Intrinsic("dot", HLSLBaseType.Float, HLSLBaseType.Float4, HLSLBaseType.Float4);
        Intrinsic("dot", HLSLBaseType.Half, HLSLBaseType.Half, HLSLBaseType.Half);
        Intrinsic("dot", HLSLBaseType.Half, HLSLBaseType.Half2, HLSLBaseType.Half2);
        Intrinsic("dot", HLSLBaseType.Half, HLSLBaseType.Half3, HLSLBaseType.Half3);
        Intrinsic("dot", HLSLBaseType.Half, HLSLBaseType.Half4, HLSLBaseType.Half4);
        
        Intrinsic("cross", HLSLBaseType.Float3, HLSLBaseType.Float3, HLSLBaseType.Float3);

        Intrinsic("length", HLSLBaseType.Float, HLSLBaseType.Float);
        Intrinsic("length", HLSLBaseType.Float, HLSLBaseType.Float2);
        Intrinsic("length", HLSLBaseType.Float, HLSLBaseType.Float3);
        Intrinsic("length", HLSLBaseType.Float, HLSLBaseType.Float4);
        Intrinsic("length", HLSLBaseType.Half, HLSLBaseType.Half);
        Intrinsic("length", HLSLBaseType.Half, HLSLBaseType.Half2);
        Intrinsic("length", HLSLBaseType.Half, HLSLBaseType.Half3);
        Intrinsic("length", HLSLBaseType.Half, HLSLBaseType.Half4);

        Float2("max");
        Float2("min");
        
        Float2("mul");
        Intrinsic("mul", HLSLBaseType.Float3, HLSLBaseType.Float3, HLSLBaseType.Float3x3);
        Intrinsic("mul", HLSLBaseType.Float4, HLSLBaseType.Float4, HLSLBaseType.Float4x4);

        Intrinsic("transpose", HLSLBaseType.Float3x3, HLSLBaseType.Float3x3);
        Intrinsic("transpose", HLSLBaseType.Float4x4, HLSLBaseType.Float4x4);

        Float1("normalize");
        Float2("pow");
        Float1("saturate");
        Float1("sin");
        Float1("sqrt");
        Float1("rsqrt");
        Float1("rcp");
        Float1("ddx");
        Float1("ddy");
        
        Float1("sign");
        Float2("step");
        Float2("reflect");

        Intrinsic("tex2D", HLSLBaseType.Float4, HLSLBaseType.Sampler2D, HLSLBaseType.Float2);
        Intrinsic("tex2Dproj", HLSLBaseType.Float4, HLSLBaseType.Sampler2D, HLSLBaseType.Float4);
        Intrinsic("tex2Dlod", HLSLBaseType.Float4, HLSLBaseType.Sampler2D, HLSLBaseType.Float4);
        Intrinsic("texCUBE", HLSLBaseType.Float4, HLSLBaseType.SamplerCube, HLSLBaseType.Float3);
        Intrinsic("texCUBEbias", HLSLBaseType.Float4, HLSLBaseType.SamplerCube, HLSLBaseType.Float4);

        // sincos
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Float, HLSLBaseType.Float, HLSLBaseType.Float);
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Float2, HLSLBaseType.Float, HLSLBaseType.Float2);
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Float3, HLSLBaseType.Float, HLSLBaseType.Float3);
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Float4, HLSLBaseType.Float, HLSLBaseType.Float4);
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Half, HLSLBaseType.Half, HLSLBaseType.Half);
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Half2, HLSLBaseType.Half2, HLSLBaseType.Half2);
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Half3, HLSLBaseType.Half3, HLSLBaseType.Half3);
        Intrinsic("sincos", HLSLBaseType.Void, HLSLBaseType.Half4, HLSLBaseType.Half4, HLSLBaseType.Half4);

        Float1("exp2");
        
        return list.ToArray();
    }

    private static readonly Intrinsic[] _intrinsics = BuildIntrinsics();

    private static ReadOnlySpan<int> _binaryOpPriority => [2, 1, 8, 8, 9, 9, 7, 7, 7, 7, 6, 6, 5, 3, 4];
    private const int ConditionalOpPriority = 1;

    private static readonly BaseTypeDescription[] _baseTypeDescriptions =
    [
        new("unknown type", NumericType.NaN, 0, 0, 0, -1),
        new("void", NumericType.NaN, 0, 0, 0, -1),
        new("float", NumericType.Float, 1, 0, 1, 0),
        new("float2", NumericType.Float, 2, 1, 1, 0),
        new("float3", NumericType.Float, 3, 1, 1, 0),
        new("float4", NumericType.Float, 4, 1, 1, 0),
        new("float3x3", NumericType.Float, 3, 2, 3, 0),
        new("float4x4", NumericType.Float, 4, 2, 4, 0),
        new("half", NumericType.Half, 1, 0, 1, 1),
        new("half2", NumericType.Half, 2, 1, 1, 1),
        new("half3", NumericType.Half, 3, 1, 1, 1),
        new("half4", NumericType.Half, 4, 1, 1, 1),
        new("half3x3", NumericType.Half, 3, 2, 3, 1),
        new("half4x4", NumericType.Half, 4, 2, 4, 1),
        new("bool", NumericType.Bool, 1, 0, 1, 4),
        new("int", NumericType.Int, 1, 0, 1, 3),
        new("int2", NumericType.Int, 2, 1, 1, 3),
        new("int3", NumericType.Int, 3, 1, 1, 3),
        new("int4", NumericType.Int, 4, 1, 1, 3),
        new("uint", NumericType.Uint, 1, 0, 1, 2),
        new("uint2", NumericType.Uint, 2, 1, 1, 2),
        new("uint3", NumericType.Uint, 3, 1, 1, 2),
        new("uint4", NumericType.Uint, 4, 1, 1, 2),
        new("texture", NumericType.NaN, 1, 0, 0, -1),
        new("sampler", NumericType.NaN, 1, 0, 0, -1),
        new("sampler2D", NumericType.NaN, 1, 0, 0, -1),
        new("samplerCUBE", NumericType.NaN, 1, 0, 0, -1),
        new("user defined", NumericType.NaN, 1, 0, 0, -1),
        new("expression", NumericType.NaN, 1, 0, 0, -1)
    ];

    private static readonly int[,] _numberTypeRank = new int[(int)NumericType.Count, (int)NumericType.Count]
    {
        { 0, 4, 4, 4, 4 }, // Float
        { 1, 0, 4, 4, 4 }, // Half
        { 5, 5, 0, 5, 5 }, // Bool
        { 5, 5, 4, 0, 3 }, // Int
        { 5, 5, 4, 2, 0 }, // Uint
    };

    // ── Effect / sampler / pipeline states ──────────────────────────────────

    private static readonly EffectStateValue[] BooleanValues = [new("False", 0), new("True", 1)];
    private static readonly EffectStateValue[] IntegerValues = [];
    private static readonly EffectStateValue[] FloatValues = [];

    private static readonly EffectStateValue[] TextureFilteringValues =
    [
        new("None", 0),
        new("Point", 1),
        new("Linear", 2),
        new("Anisotropic", 3)
    ];
    private static readonly EffectStateValue[] TextureAddressingValues =
    [
        new("Wrap", 1),
        new("Mirror", 2),
        new("Clamp", 3),
        new("Border", 4),
        new("MirrorOnce", 5)
    ];

    private static readonly EffectStateValue[] CullValues =
    [
        new("None", 1),
        new("CW", 2),
        new("CCW", 3)
    ];
        
    private static readonly EffectStateValue[] CmpValues =
    [
        new("Never", 1),
        new("Less", 2),
        new("Equal", 3),
        new("LessEqual", 4),
        new("Greater", 5),
        new("NotEqual", 6),
        new("GreaterEqual", 7),
        new("Always", 8)
    ];

    private static readonly EffectStateValue[] BlendValues =
    [
        new("Zero", 1),
        new("One", 2),
        new("SrcColor", 3),
        new("InvSrcColor", 4),
        new("SrcAlpha", 5),
        new("InvSrcAlpha", 6),
        new("DestAlpha", 7),
        new("InvDestAlpha", 8),
        new("DestColor", 9),
        new("InvDestColor", 10),
        new("SrcAlphaSat", 11),
        new("BothSrcAlpha", 12),
        new("BothInvSrcAlpha", 13),
        new("BlendFactor", 14),
        new("InvBlendFactor", 15),
        new("SrcColor2", 16),
        new("InvSrcColor2", 17)
    ];

    private static readonly EffectStateValue[] BlendOpValues =
    [
        new("Add", 1),
        new("Subtract", 2),
        new("RevSubtract", 3),
        new("Min", 4),
        new("Max", 5)
    ];

    private static readonly EffectStateValue[] FillModeValues =
    [
        new("Point", 1),
        new("Wireframe", 2),
        new("Solid", 3)
    ];
    
    private static readonly EffectStateValue[] StencilOpValues =
    [
        new("Keep", 1),
        new("Zero", 2),
        new("Replace", 3),
        new("IncrSat", 4),
        new("DecrSat", 5),
        new("Invert", 6),
        new("Incr", 7),
        new("Decr", 8)
    ];
    
    private static readonly EffectStateValue[] ColorMaskValues =
    [
        new("False", 0),
        new("Red", 1),
        new("Green", 2),
        new("Blue", 4),
        new("Alpha", 8),
        new("X", 1),
        new("Y", 2),
        new("Z", 4),
        new("W", 8)
    ];

    private static readonly EffectState[] SamplerStates =
    [
        new("AddressU", 1, TextureAddressingValues),
        new("AddressV", 2, TextureAddressingValues),
        new("AddressW", 3, TextureAddressingValues),
        new("MagFilter", 5, TextureFilteringValues),
        new("MinFilter", 6, TextureFilteringValues),
        new("MipFilter", 7, TextureFilteringValues),
        new("MipMapLodBias", 8, FloatValues),
        new("MaxMipLevel", 9, IntegerValues),
        new("MaxAnisotropy", 10, IntegerValues),
        new("sRGBTexture", 11, BooleanValues),
        new("Texture", 12, null),
        new("Filter", 13, TextureFilteringValues),
    ];

    private static readonly EffectState[] EffectStates =
    [
        new("VertexShader", 0, null),
        new("PixelShader", 0, null),
        new("AlphaBlendEnable", 27, BooleanValues),
        new("SrcBlend", 19, BlendValues),
        new("DestBlend", 20, BlendValues),
        new("BlendOp", 171, BlendOpValues),
        new("SeparateAlphaBlendEanble", 206, BooleanValues),
        new("SrcBlendAlpha", 207, BlendValues),
        new("DestBlendAlpha", 208, BlendValues),
        new("BlendOpAlpha", 209, BlendOpValues),
        new("AlphaTestEnable", 15, BooleanValues),
        new("AlphaRef", 24, IntegerValues),
        new("AlphaFunc", 25, CmpValues),
        new("CullMode", 22, CullValues),
        new("ZEnable", 7, BooleanValues),
        new("ZWriteEnable", 14, BooleanValues),
        new("ZFunc", 23, CmpValues),
        new("StencilEnable", 52, BooleanValues),
        new("StencilFail", 53, StencilOpValues),
        new("StencilZFail", 54, StencilOpValues),
        new("StencilPass", 55, StencilOpValues),
        new("StencilFunc", 56, CmpValues),
        new("StencilRef", 57, IntegerValues),
        new("StencilMask", 58, IntegerValues),
        new("StencilWriteMask", 59, IntegerValues),
        new("TwoSidedStencilMode", 185, BooleanValues),
        new("CCW_StencilFail", 186, StencilOpValues),
        new("CCW_StencilZFail", 187, StencilOpValues),
        new("CCW_StencilPass", 188, StencilOpValues),
        new("CCW_StencilFunc", 189, CmpValues),
        new("ColorWriteEnable", 168, ColorMaskValues),
        new("FillMode", 8, FillModeValues),
        new("MultisampleAlias", 161, BooleanValues),
        new("MultisampleMask", 162, IntegerValues),
        new("ScissorTestEnable", 174, BooleanValues),
        new("SlopeScaleDepthBias", 175, FloatValues),
        new("DepthBias", 195, FloatValues)
    ];

    private static readonly EffectStateValue[] WitnessCullModeValues =
    [
        new("None", 0),
        new("Back", 1),
        new("Front", 2)
    ];
    
    private static readonly EffectStateValue[] WitnessFillModeValues = [
        new("Solid", 0),
        new("Wireframe", 1)
    ];
    
    private static readonly EffectStateValue[] WitnessBlendModeValues = [
        new("Disabled", 0),
        new("AlphaBlend", 1),
        new("Add", 2),
        new("Mixed", 3),
        new("Multiply", 4),
        new("Multiply2", 5)
    ];
    
    private static readonly EffectStateValue[] WitnessDepthFuncValues = [
        new("LessEqual", 0),
        new("Less", 1),
        new("Equal", 2),
        new("Greater", 3),
        new("Always", 4)
    ];
    
    private static readonly EffectStateValue[] WitnessStencilModeValues = [
        new("Disabled", 0),
        new("Set", 1),
        new("Test", 2)
    ];

    private static readonly EffectState[] PipelineStates =
    [
        new("VertexShader", 0, null), new("PixelShader", 0, null),
        new("DepthWrite", 0, BooleanValues), new("DepthEnable", 0, BooleanValues), new("DepthFunc", 0, WitnessDepthFuncValues), new("StencilMode", 0, WitnessStencilModeValues),
        new("CullMode", 0, WitnessCullModeValues), new("FillMode", 0, WitnessFillModeValues), new("MultisampleEnable", 0, BooleanValues), new("PolygonOffset", 0, BooleanValues),
        new("BlendMode", 0, WitnessBlendModeValues), new("ColorWrite", 0, BooleanValues), new("AlphaWrite", 0, BooleanValues), new("AlphaTest", 0, BooleanValues)
    ];

    // ── Instance state ──────────────────────────────────────────────────────

    private HLSLTokenizer _tokenizer = new(fileName, buffer, length);
    private readonly List<HLSLStruct> _userTypes = [];
    private readonly List<Variable> _variables = [];
    private readonly List<HLSLFunction> _functions = [];
    private int _numGlobals;
    private HLSLTree _tree;
    private bool _allowUndeclaredIdentifiers;

    private struct Variable
    {
        public string? Name;
        public HLSLType Type;
    }

    // ── Constructor ─────────────────────────────────────────────────────────

    public HLSLParser(ReadOnlySpan<char> fileName, ReadOnlySpan<char> buffer)
        : this(fileName, buffer, buffer.Length) { }

    // ── Public API ──────────────────────────────────────────────────────────

    public bool Parse(HLSLTree tree)
    {
        _tree = tree;
        var root = _tree.Root;
        HLSLStatement? lastStatement = null;

        while (!Accept((int)HLSLToken.EndOfStream))
        {
            HLSLStatement? statement = null;
            if (!ParseTopLevel(ref statement)) return false;
            if (statement != null)
            {
                if (lastStatement == null) root.Statement = statement;
                else lastStatement.NextStatement = statement;
                lastStatement = statement;
                while (lastStatement.NextStatement != null) lastStatement = lastStatement.NextStatement;
            }
        }
        return true;
    }

    // ── Token helpers ───────────────────────────────────────────────────────

    private bool Accept(int token)
    {
        if (_tokenizer.Token == token) { _tokenizer.Next(); return true; }
        return false;
    }

    private bool Accept(string token)
    {
        if (_tokenizer.Token == (int)HLSLToken.Identifier && _tokenizer.Identifier == token)
        { _tokenizer.Next(); return true; }
        return false;
    }

    private bool Expect(int token)
    {
        if (!Accept(token))
        {
            var want = HLSLTokenizer.GetTokenName(token);
            var near = _tokenizer.TokenName;
            return _tokenizer.Error($"Syntax error: expected '{want}' near '{near}'");
        }
        return true;
    }

    private bool Expect(string token)
    {
        if (!Accept(token))
        {
            var near = _tokenizer.TokenName;
            return _tokenizer.Error($"Syntax error: expected '{token}' near '{near}'");
        }
        return true;
    }

    private bool AcceptIdentifier(out string? identifier)
    {
        identifier = null;
        if (_tokenizer.Token == (int)HLSLToken.Identifier)
        {
            identifier = string.FromSpan(_tokenizer.Identifier);
            _tokenizer.Next();
            return true;
        }
        return false;
    }

    private bool ExpectIdentifier(out string identifier)
    {
        if (!AcceptIdentifier(out var id))
        {
            var near = _tokenizer.TokenName;
            identifier = "";
            return _tokenizer.Error($"Syntax error: expected identifier near '{near}'");
        }
        identifier = id!;
        return true;
    }

    private bool AcceptFloat(out float value) { value = 0; if (_tokenizer.Token == (int)HLSLToken.FloatLiteral) { value = _tokenizer.Float; _tokenizer.Next(); return true; } return false; }
    private bool AcceptHalf(out float value) { value = 0; if (_tokenizer.Token == (int)HLSLToken.HalfLiteral) { value = _tokenizer.Float; _tokenizer.Next(); return true; } return false; }
    private bool AcceptInt(out int value) { value = 0; if (_tokenizer.Token == (int)HLSLToken.IntLiteral) { value = _tokenizer.Int; _tokenizer.Next(); return true; } return false; }

    private string FileName => string.FromSpan(_tokenizer.FileName);
    private int LineNumber => _tokenizer.LineNumber;

    // ── Type / modifier parsing ─────────────────────────────────────────────

    private bool AcceptTypeModifier(ref int flags)
    {
        if (Accept((int)HLSLToken.Const)) { flags |= (int)HLSLTypeFlags.Const; return true; }
        if (Accept((int)HLSLToken.Static)) { flags |= (int)HLSLTypeFlags.Static; return true; }
        if (Accept((int)HLSLToken.Uniform)) return true;
        if (Accept((int)HLSLToken.Inline)) return true;
        return false;
    }

    private bool AcceptInterpolationModifier(ref int flags)
    {
        if (Accept("linear")) { flags |= (int)HLSLTypeFlags.Linear; return true; }
        if (Accept("centroid")) { flags |= (int)HLSLTypeFlags.Centroid; return true; }
        if (Accept("nointerpolation")) { flags |= (int)HLSLTypeFlags.NoInterpolation; return true; }
        if (Accept("noperspective")) { flags |= (int)HLSLTypeFlags.NoPerspective; return true; }
        if (Accept("sample")) { flags |= (int)HLSLTypeFlags.Sample; return true; }
        return false;
    }

    private bool AcceptType(bool allowVoid, HLSLType type)
    {
        type.Flags = 0;
        while (AcceptTypeModifier(ref type.Flags) || AcceptInterpolationModifier(ref type.Flags)) { }

        var token = _tokenizer.Token;
        type.BaseType = HLSLBaseType.Void;

        type.BaseType = token switch
        {
            (int)HLSLToken.Float => HLSLBaseType.Float,
            (int)HLSLToken.Float2 => HLSLBaseType.Float2,
            (int)HLSLToken.Float3 => HLSLBaseType.Float3,
            (int)HLSLToken.Float4 => HLSLBaseType.Float4,
            (int)HLSLToken.Float3x3 => HLSLBaseType.Float3x3,
            (int)HLSLToken.Float4x4 => HLSLBaseType.Float4x4,
            (int)HLSLToken.Half => HLSLBaseType.Half,
            (int)HLSLToken.Half2 => HLSLBaseType.Half2,
            (int)HLSLToken.Half3 => HLSLBaseType.Half3,
            (int)HLSLToken.Half4 => HLSLBaseType.Half4,
            (int)HLSLToken.Half3x3 => HLSLBaseType.Half3x3,
            (int)HLSLToken.Half4x4 => HLSLBaseType.Half4x4,
            (int)HLSLToken.Bool => HLSLBaseType.Bool,
            (int)HLSLToken.Int => HLSLBaseType.Int,
            (int)HLSLToken.Int2 => HLSLBaseType.Int2,
            (int)HLSLToken.Int3 => HLSLBaseType.Int3,
            (int)HLSLToken.Int4 => HLSLBaseType.Int4,
            (int)HLSLToken.Uint => HLSLBaseType.Uint,
            (int)HLSLToken.Uint2 => HLSLBaseType.Uint2,
            (int)HLSLToken.Uint3 => HLSLBaseType.Uint3,
            (int)HLSLToken.Uint4 => HLSLBaseType.Uint4,
            (int)HLSLToken.Texture => HLSLBaseType.Texture,
            (int)HLSLToken.Sampler => HLSLBaseType.Sampler2D,
            (int)HLSLToken.Sampler2D => HLSLBaseType.Sampler2D,
            (int)HLSLToken.SamplerCube => HLSLBaseType.SamplerCube,
            _ => HLSLBaseType.Void,
        };

        if (type.BaseType != HLSLBaseType.Void)
        {
            _tokenizer.Next();
            if (type.BaseType.IsSamplerType())
            {
                if (Accept('<'))
                {
                    var st = _tokenizer.Token;
                    if (st == (int)HLSLToken.Float) type.SamplerType = HLSLBaseType.Float;
                    else if (st == (int)HLSLToken.Half) type.SamplerType = HLSLBaseType.Half;
                    else return _tokenizer.Error("Expected half or float.");
                    _tokenizer.Next();
                    if (!Expect('>')) return false;
                }
            }
            return true;
        }
        if (allowVoid && Accept((int)HLSLToken.Void)) { type.BaseType = HLSLBaseType.Void; return true; }
        if (token == (int)HLSLToken.Identifier)
        {
            var identifier = string.FromSpan(_tokenizer.Identifier);
            if (FindUserDefinedType(identifier) != null)
            {
                _tokenizer.Next();
                type.BaseType = HLSLBaseType.UserDefined;
                type.TypeName = identifier;
                return true;
            }
        }
        return false;
    }

    private bool ExpectType(bool allowVoid, HLSLType type)
    {
        if (!AcceptType(allowVoid, type)) { _tokenizer.Error("Expected type"); return false; }
        return true;
    }

    private bool AcceptDeclaration(bool allowUnsizedArray, HLSLType type, out string name)
    {
        name = "";
        if (!AcceptType(false, type)) return false;
        if (!ExpectIdentifier(out name)) return false;
        if (Accept('['))
        {
            type.Array = true;
            if (Accept(']') && allowUnsizedArray) return true;
            HLSLExpression? arraySize = null;
            if (!ParseExpression(ref arraySize) || !Expect(']')) return false;
            type.ArraySize = arraySize;
        }
        return true;
    }

    private bool ExpectDeclaration(bool allowUnsizedArray, HLSLType type, out string name)
    {
        if (!AcceptDeclaration(allowUnsizedArray, type, out name))
        { _tokenizer.Error("Expected declaration"); return false; }
        return true;
    }

    // ── Top-level parsing ───────────────────────────────────────────────────

    private bool ParseTopLevel(ref HLSLStatement? statement)
    {
        HLSLAttribute? attributes = null;
        ParseAttributeBlock(ref attributes);

        var line = LineNumber;
        var fileName = FileName;
        HLSLType type = new();
        var doesNotExpectSemicolon = false;

        if (Accept((int)HLSLToken.Struct))
        {
            if (!ExpectIdentifier(out var structName)) return false;
            if (FindUserDefinedType(structName) != null) { _tokenizer.Error($"struct {structName} already defined"); return false; }
            if (!Expect('{')) return false;

            var structure = HLSLTree.AddNode<HLSLStruct>(fileName, line);
            structure.Name = structName;
            _userTypes.Add(structure);

            HLSLStructField? lastField = null;
            while (!Accept('}'))
            {
                if (CheckForUnexpectedEndOfStream('}')) return false;
                HLSLStructField? field = null;
                if (!ParseFieldDeclaration(ref field)) return false;
                if (lastField == null) structure.Field = field;
                else lastField!.NextField = field;
                lastField = field;
            }
            statement = structure;
        }
        else if (Accept((int)HLSLToken.CBuffer) || Accept((int)HLSLToken.TBuffer))
        {
            var buffer = HLSLTree.AddNode<HLSLBuffer>(fileName, line);
            AcceptIdentifier(out var bufName);
            buffer.Name = bufName;

            if (Accept(':'))
            {
                if (!Expect((int)HLSLToken.Register) || !Expect('(') || !ExpectIdentifier(out var regName) || !Expect(')')) return false;
                buffer.RegisterName = regName;
            }
            if (!Expect('{')) return false;

            HLSLDeclaration? lastField = null;
            while (!Accept('}'))
            {
                if (CheckForUnexpectedEndOfStream('}')) return false;
                HLSLDeclaration? field = null;
                if (!ParseDeclaration(ref field)) { _tokenizer.Error("Expected variable declaration"); return false; }
                DeclareVariable(field!.Name!, field.Type);
                field.Buffer = buffer;
                if (buffer.Field == null) buffer.Field = field;
                else lastField!.NextStatement = field;
                lastField = field;
                if (!Expect(';')) return false;
            }
            statement = buffer;
        }
        else if (AcceptType(true, type))
        {
            if (!ExpectIdentifier(out var globalName)) return false;

            if (Accept('('))
            {
                var function = HLSLTree.AddNode<HLSLFunction>(fileName, line);
                function.Name = globalName;
                function.ReturnType.BaseType = type.BaseType;
                function.ReturnType.TypeName = type.TypeName;
                function.Attributes = attributes;

                BeginScope();
                if (!ParseArgumentList(function)) return false;

                var declaration = FindFunction(function);

                if (Accept(';'))
                {
                    if (declaration == null) { _functions.Add(function); statement = function; }
                    EndScope();
                    return true;
                }
                if (Accept(':') && !ExpectIdentifier(out var sem)) return false;
                else if (_tokenizer.Token != '{') function.Semantic = string.FromSpan(_tokenizer.Identifier);

                if (declaration != null)
                {
                    if (declaration.Forward != null || declaration.Statement != null) return _tokenizer.Error("Duplicate function definition");
                    declaration.Forward = function;
                }
                else _functions.Add(function);

                if (!Expect('{') || !ParseBlock(ref function.Statement, function.ReturnType)) return false;
                EndScope();
                statement = function;
                return true;
            }
            else
            {
                var decl = HLSLTree.AddNode<HLSLDeclaration>(fileName, line);
                decl.Name = globalName;
                decl.Type = type;

                if (Accept('['))
                {
                    if (!Accept(']'))
                    {
                        HLSLExpression? arrSize = null;
                        if (!ParseExpression(ref arrSize) || !Expect(']')) return false;
                        decl.Type.ArraySize = arrSize;
                    }
                    decl.Type.Array = true;
                }
                if (Accept(':'))
                {
                    if (AcceptIdentifier(out var semOrReg)) decl.Semantic = semOrReg;
                    else if (!Expect((int)HLSLToken.Register) || !Expect('(') || !ExpectIdentifier(out var regN) || !Expect(')')) return false;
                    else decl.RegisterName = regN;
                }
                DeclareVariable(globalName, decl.Type);
                if (!ParseDeclarationAssignment(decl)) return false;
                statement = decl;
            }
        }
        else if (ParseTechnique(ref statement)) doesNotExpectSemicolon = true;
        else if (ParsePipeline(ref statement)) doesNotExpectSemicolon = true;
        else if (ParseStage(ref statement)) doesNotExpectSemicolon = true;

        if (statement != null) statement.Attributes = attributes;
        return doesNotExpectSemicolon || Expect(';');
    }

    // ── Block / statement parsing ───────────────────────────────────────────

    private bool ParseStatementOrBlock(ref HLSLStatement? firstStatement, HLSLType returnType, bool scoped = true)
    {
        if (scoped) BeginScope();
        if (Accept('{')) { if (!ParseBlock(ref firstStatement, returnType)) return false; }
        else { if (!ParseStatement(ref firstStatement, returnType)) return false; }
        if (scoped) EndScope();
        return true;
    }

    private bool ParseBlock(ref HLSLStatement? firstStatement, HLSLType returnType)
    {
        HLSLStatement? lastStatement = null;
        while (!Accept('}'))
        {
            if (CheckForUnexpectedEndOfStream('}')) return false;
            HLSLStatement? statement = null;
            if (!ParseStatement(ref statement, returnType)) return false;
            if (statement != null)
            {
                if (firstStatement == null) firstStatement = statement;
                else lastStatement!.NextStatement = statement;
                lastStatement = statement;
                while (lastStatement.NextStatement != null) lastStatement = lastStatement.NextStatement;
            }
        }
        return true;
    }

    private bool ParseStatement(ref HLSLStatement? statement, HLSLType returnType)
    {
        var fileName = FileName;
        var line = LineNumber;

        if (Accept(';')) return true;

        HLSLAttribute? attributes = null;
        ParseAttributeBlock(ref attributes);

        if (Accept((int)HLSLToken.If))
        {
            var ifSt = HLSLTree.AddNode<HLSLIfStatement>(fileName, line);
            ifSt.Attributes = attributes;
            HLSLExpression? cond = null;
            if (!Expect('(') || !ParseExpression(ref cond) || !Expect(')')) return false;
            ifSt.Condition = cond;
            statement = ifSt;
            if (!ParseStatementOrBlock(ref ifSt.Statement, returnType)) return false;
            if (Accept((int)HLSLToken.Else)) return ParseStatementOrBlock(ref ifSt.ElseStatement, returnType);
            return true;
        }
        if (Accept((int)HLSLToken.For))
        {
            var forSt = HLSLTree.AddNode<HLSLForStatement>(fileName, line);
            forSt.Attributes = attributes;
            if (!Expect('(')) return false;
            BeginScope();
            HLSLDeclaration? init = null;
            if (!ParseDeclaration(ref init)) return false;
            forSt.Initialization = init;
            if (!Expect(';')) return false;
            HLSLExpression? cond = null;
            ParseExpression(ref cond);
            forSt.Condition = cond;
            if (!Expect(';')) return false;
            HLSLExpression? inc = null;
            ParseExpression(ref inc);
            forSt.Increment = inc;
            if (!Expect(')')) return false;
            statement = forSt;
            if (!ParseStatementOrBlock(ref forSt.Statement, returnType)) return false;
            EndScope();
            return true;
        }
        if (Accept('{'))
        {
            var block = HLSLTree.AddNode<HLSLBlockStatement>(fileName, line);
            statement = block;
            BeginScope();
            var ok = ParseBlock(ref block.Statement, returnType);
            EndScope();
            return ok;
        }
        if (Accept((int)HLSLToken.Discard)) { statement = HLSLTree.AddNode<HLSLDiscardStatement>(fileName, line); return Expect(';'); }
        if (Accept((int)HLSLToken.Break)) { statement = HLSLTree.AddNode<HLSLBreakStatement>(fileName, line); return Expect(';'); }
        if (Accept((int)HLSLToken.Continue)) { statement = HLSLTree.AddNode<HLSLContinueStatement>(fileName, line); return Expect(';'); }
        if (Accept((int)HLSLToken.Return))
        {
            var ret = HLSLTree.AddNode<HLSLReturnStatement>(fileName, line);
            HLSLExpression? expr = null;
            if (!Accept(';') && !ParseExpression(ref expr)) return false;
            ret.Expression = expr;
            var voidType = new HLSLType(HLSLBaseType.Void);
            if (!CheckTypeCast(ret.Expression != null ? ret.Expression.ExpressionType : voidType, returnType)) return false;
            statement = ret;
            return Expect(';');
        }

        HLSLDeclaration? decl = null;
        HLSLExpression? expression = null;
        if (ParseDeclaration(ref decl))
        {
            statement = decl;
        }
        else if (ParseExpression(ref expression))
        {
            var exprSt = HLSLTree.AddNode<HLSLExpressionStatement>(fileName, line);
            exprSt.Expression = expression;
            statement = exprSt;
        }
        return Expect(';');
    }

    private bool ParseDeclaration(ref HLSLDeclaration? declaration)
    {
        var fileName = FileName;
        var line = LineNumber;
        HLSLType type = new();
        if (!AcceptType(false, type)) return false;

        HLSLDeclaration? first = null, last = null;
        do
        {
            if (!ExpectIdentifier(out var name)) return false;
            if (Accept('['))
            {
                type.Array = true;
                if (Accept(']')) return true;
                HLSLExpression? arrSize = null;
                if (!ParseExpression(ref arrSize) || !Expect(']')) return false;
                type.ArraySize = arrSize;
            }
            var decl = HLSLTree.AddNode<HLSLDeclaration>(fileName, line);
            decl.Type = type;
            decl.Name = name;
            DeclareVariable(decl.Name, decl.Type);
            if (!ParseDeclarationAssignment(decl)) return false;
            if (first == null) first = decl;
            if (last != null) last.NextDeclaration = decl;
            last = decl;
        } while (Accept(','));

        declaration = first;
        return true;
    }

    private bool ParseDeclarationAssignment(HLSLDeclaration declaration)
    {
        if (Accept('='))
        {
            if (declaration.Type.Array)
            {
                var numValues = 0;
                HLSLExpression? initExpr = null;
                if (!Expect('{') || !ParseExpressionList('}', true, ref initExpr, out numValues)) return false;
                declaration.Assignment = initExpr;
            }
            else if (declaration.Type.BaseType.IsSamplerType())
            {
                HLSLExpression? samplerExpr = null;
                if (!ParseSamplerState(ref samplerExpr)) return false;
                declaration.Assignment = samplerExpr;
            }
            else
            {
                HLSLExpression? expr = null;
                if (!ParseExpression(ref expr)) return false;
                declaration.Assignment = expr;
            }
        }
        return true;
    }

    private bool ParseFieldDeclaration(ref HLSLStructField? field)
    {
        field = HLSLTree.AddNode<HLSLStructField>(FileName, LineNumber);
        if (!ExpectDeclaration(false, field.Type, out var name)) return false;
        field.Name = name;
        if (Accept(':')) { if (!ExpectIdentifier(out var sem)) return false; field.Semantic = sem; }
        return Expect(';');
    }

    // ── Expression parsing ──────────────────────────────────────────────────

    private bool ParseExpression(ref HLSLExpression? expression)
    {
        if (!ParseBinaryExpression(0, ref expression)) return false;

        if (AcceptAssign(out var assignOp))
        {
            HLSLExpression? expr2 = null;
            if (!ParseExpression(ref expr2)) return false;
            var bin = HLSLTree.AddNode<HLSLBinaryExpression>(expression!.FileName!, expression.Line);
            bin.BinaryOp = assignOp;
            bin.Expression1 = expression;
            bin.Expression2 = expr2;
            bin.ExpressionType = expression.ExpressionType;
            if (!CheckTypeCast(expr2!.ExpressionType, expression.ExpressionType)) return false;
            expression = bin;
        }
        return true;
    }

    private bool AcceptBinaryOperator(int priority, out HLSLBinaryOp binaryOp)
    {
        binaryOp = default;
        var token = _tokenizer.Token;
        binaryOp = token switch
        {
            (int)HLSLToken.AndAnd => HLSLBinaryOp.And,
            (int)HLSLToken.BarBar => HLSLBinaryOp.Or,
            '+' => HLSLBinaryOp.Add, '-' => HLSLBinaryOp.Sub, '*' => HLSLBinaryOp.Mul, '/' => HLSLBinaryOp.Div,
            '<' => HLSLBinaryOp.Less, '>' => HLSLBinaryOp.Greater,
            (int)HLSLToken.LessEqual => HLSLBinaryOp.LessEqual, (int)HLSLToken.GreaterEqual => HLSLBinaryOp.GreaterEqual,
            (int)HLSLToken.EqualEqual => HLSLBinaryOp.Equal, (int)HLSLToken.NotEqual => HLSLBinaryOp.NotEqual,
            '&' => HLSLBinaryOp.BitAnd, '|' => HLSLBinaryOp.BitOr, '^' => HLSLBinaryOp.BitXor,
            _ => (HLSLBinaryOp)(-1),
        };
        if ((int)binaryOp == -1) return false;
        if (_binaryOpPriority[(int)binaryOp] > priority) { _tokenizer.Next(); return true; }
        return false;
    }

    private bool AcceptUnaryOperator(bool pre, out HLSLUnaryOp unaryOp)
    {
        unaryOp = default;
        var token = _tokenizer.Token;
        if (token == (int)HLSLToken.PlusPlus) unaryOp = pre ? HLSLUnaryOp.PreIncrement : HLSLUnaryOp.PostIncrement;
        else if (token == (int)HLSLToken.MinusMinus) unaryOp = pre ? HLSLUnaryOp.PreDecrement : HLSLUnaryOp.PostDecrement;
        else if (pre && token == '-') unaryOp = HLSLUnaryOp.Negative;
        else if (pre && token == '+') unaryOp = HLSLUnaryOp.Positive;
        else if (pre && token == '!') unaryOp = HLSLUnaryOp.Not;
        else if (pre && token == '~') unaryOp = HLSLUnaryOp.BitNot;
        else return false;
        _tokenizer.Next();
        return true;
    }

    private bool AcceptAssign(out HLSLBinaryOp op)
    {
        op = default;
        if (Accept('=')) op = HLSLBinaryOp.Assign;
        else if (Accept((int)HLSLToken.PlusEqual)) op = HLSLBinaryOp.AddAssign;
        else if (Accept((int)HLSLToken.MinusEqual)) op = HLSLBinaryOp.SubAssign;
        else if (Accept((int)HLSLToken.TimesEqual)) op = HLSLBinaryOp.MulAssign;
        else if (Accept((int)HLSLToken.DivideEqual)) op = HLSLBinaryOp.DivAssign;
        else return false;
        return true;
    }

    private bool ParseBinaryExpression(int priority, ref HLSLExpression? expression)
    {
        var fileName = FileName;
        var line = LineNumber;
        var needsEndParen = false;
        if (!ParseTerminalExpression(ref expression, ref needsEndParen)) return false;
        if (needsEndParen) priority = 0;

        while (true)
        {
            if (AcceptBinaryOperator(priority, out var binaryOp))
            {
                HLSLExpression? expr2 = null;
                if (!ParseBinaryExpression(_binaryOpPriority[(int)binaryOp], ref expr2)) return false;
                var bin = HLSLTree.AddNode<HLSLBinaryExpression>(fileName, line);
                bin.BinaryOp = binaryOp;
                bin.Expression1 = expression;
                bin.Expression2 = expr2;
                if (!GetBinaryOpResultType(binaryOp, expression!.ExpressionType, expr2!.ExpressionType, bin.ExpressionType))
                {
                    return _tokenizer.Error($"binary '{GetBinaryOpName(binaryOp)}' : no global operator found which takes types '{GetTypeName(expression.ExpressionType)}' and '{GetTypeName(expr2.ExpressionType)}'");
                }
                bin.ExpressionType.Flags = (expression.ExpressionType.Flags | expr2.ExpressionType.Flags) & (int)HLSLTypeFlags.Const;
                expression = bin;
            }
            else if (ConditionalOpPriority > priority && Accept('?'))
            {
                var cond = HLSLTree.AddNode<HLSLConditionalExpression>(fileName, line);
                cond.Condition = expression;
                HLSLExpression? e1 = null, e2 = null;
                if (!ParseBinaryExpression(ConditionalOpPriority, ref e1) || !Expect(':') || !ParseBinaryExpression(ConditionalOpPriority, ref e2)) return false;
                if (GetTypeCastRank(_tree, e1!.ExpressionType, e2!.ExpressionType) == -1) return _tokenizer.Error("':' no possible conversion");
                cond.TrueExpression = e1; cond.FalseExpression = e2; cond.ExpressionType = e1.ExpressionType;
                expression = cond;
            }
            else break;

            if (needsEndParen) { if (!Expect(')')) return false; needsEndParen = false; }
        }
        return !needsEndParen || Expect(')');
    }

    private bool ParsePartialConstructor(ref HLSLExpression? expression, HLSLBaseType type, string? typeName)
    {
        var fileName = FileName; var line = LineNumber;
        var ctor = HLSLTree.AddNode<HLSLConstructorExpression>(fileName, line);
        ctor.Type.BaseType = type; ctor.Type.TypeName = typeName;
        var numArgs = 0;
        HLSLExpression? args = null;
        if (!ParseExpressionList(')', false, ref args, out numArgs)) return false;
        ctor.Argument = args;
        ctor.ExpressionType = ctor.Type;
        ctor.ExpressionType.Flags = (int)HLSLTypeFlags.Const;
        expression = ctor;
        return true;
    }

    private bool ParseTerminalExpression(ref HLSLExpression? expression, ref bool needsEndParen)
    {
        var fileName = FileName; var line = LineNumber;
        needsEndParen = false;

        if (AcceptUnaryOperator(true, out var unaryOp))
        {
            var un = HLSLTree.AddNode<HLSLUnaryExpression>(fileName, line);
            un.UnaryOp = unaryOp;
            HLSLExpression? inner = null;
            if (!ParseTerminalExpression(ref inner, ref needsEndParen)) return false;
            un.Expression = inner;
            if (unaryOp == HLSLUnaryOp.Not)
            {
                un.ExpressionType = new HLSLType(HLSLBaseType.Bool);
                un.ExpressionType.Flags = inner!.ExpressionType.Flags & (int)HLSLTypeFlags.Const;
            }
            else un.ExpressionType = inner!.ExpressionType;
            expression = un;
            return true;
        }

        if (Accept('('))
        {
            HLSLType castType = new();
            if (AcceptType(false, castType))
            {
                if (Accept('(')) { needsEndParen = true; return ParsePartialConstructor(ref expression, castType.BaseType, castType.TypeName); }
                var cast = HLSLTree.AddNode<HLSLCastingExpression>(fileName, line);
                cast.Type = castType;
                cast.ExpressionType = castType;
                HLSLExpression? inner = null;
                if (!Expect(')') || !ParseExpression(ref inner)) return false;
                cast.Expression = inner;
                expression = cast;
                return true;
            }
            if (!ParseExpression(ref expression) || !Expect(')')) return false;
        }
        else
        {
            if (AcceptFloat(out var fv))
            {
                var lit = HLSLTree.AddNode<HLSLLiteralExpression>(fileName, line);
                lit.Type = HLSLBaseType.Float; lit.FValue = fv;
                lit.ExpressionType = new HLSLType(HLSLBaseType.Float) { Flags = (int)HLSLTypeFlags.Const };
                expression = lit; return true;
            }
            if (AcceptHalf(out var hv))
            {
                var lit = HLSLTree.AddNode<HLSLLiteralExpression>(fileName, line);
                lit.Type = HLSLBaseType.Half; lit.FValue = hv;
                lit.ExpressionType = new HLSLType(HLSLBaseType.Half) { Flags = (int)HLSLTypeFlags.Const };
                expression = lit; return true;
            }
            if (AcceptInt(out var iv))
            {
                var lit = HLSLTree.AddNode<HLSLLiteralExpression>(fileName, line);
                lit.Type = HLSLBaseType.Int; lit.IValue = iv;
                lit.ExpressionType = new HLSLType(HLSLBaseType.Int) { Flags = (int)HLSLTypeFlags.Const };
                expression = lit; return true;
            }
            if (Accept((int)HLSLToken.True))
            {
                var lit = HLSLTree.AddNode<HLSLLiteralExpression>(fileName, line);
                lit.Type = HLSLBaseType.Bool; lit.BValue = true;
                lit.ExpressionType = new HLSLType(HLSLBaseType.Bool) { Flags = (int)HLSLTypeFlags.Const };
                expression = lit; return true;
            }
            if (Accept((int)HLSLToken.False))
            {
                var lit = HLSLTree.AddNode<HLSLLiteralExpression>(fileName, line);
                lit.Type = HLSLBaseType.Bool; lit.BValue = false;
                lit.ExpressionType = new HLSLType(HLSLBaseType.Bool) { Flags = (int)HLSLTypeFlags.Const };
                expression = lit; return true;
            }

            HLSLType ctorType = new();
            if (AcceptType(false, ctorType))
            {
                Expect('(');
                if (!ParsePartialConstructor(ref expression, ctorType.BaseType, ctorType.TypeName)) return false;
            }
            else
            {
                var ident = HLSLTree.AddNode<HLSLIdentifierExpression>(fileName, line);
                if (!ExpectIdentifier(out var idName)) return false;
                ident.Name = idName;

                var identType = FindVariable(ident.Name, out var isGlobal);
                if (identType != null) { ident.ExpressionType = identType; ident.Global = isGlobal; }
                else if (GetIsFunction(ident.Name)) ident.Global = true;
                else if (_allowUndeclaredIdentifiers)
                {
                    var lit = HLSLTree.AddNode<HLSLLiteralExpression>(fileName, line);
                    lit.BValue = false; lit.Type = HLSLBaseType.Bool;
                    lit.ExpressionType = new HLSLType(HLSLBaseType.Bool) { Flags = (int)HLSLTypeFlags.Const };
                    expression = lit;
                    return true;
                }
                else return _tokenizer.Error($"Undeclared identifier '{ident.Name}'");
                expression = ident;
            }
        }

        var done = false;
        while (!done)
        {
            done = true;
            while (AcceptUnaryOperator(false, out var postOp))
            {
                var un = HLSLTree.AddNode<HLSLUnaryExpression>(fileName, line);
                un.UnaryOp = postOp; un.Expression = expression; un.ExpressionType = expression!.ExpressionType;
                expression = un; done = false;
            }
            while (Accept('.'))
            {
                var ma = HLSLTree.AddNode<HLSLMemberAccess>(fileName, line);
                ma.Object = expression;
                if (!ExpectIdentifier(out var fld)) return false;
                ma.Field = fld;
                if (!GetMemberType(expression!.ExpressionType, ma)) return _tokenizer.Error($"Couldn't access '{fld}'");
                expression = ma; done = false;
            }
            while (Accept('['))
            {
                var aa = HLSLTree.AddNode<HLSLArrayAccess>(fileName, line);
                aa.Array = expression;
                HLSLExpression? idx = null;
                if (!ParseExpression(ref idx) || !Expect(']')) return false;
                aa.Index = idx;
                if (expression!.ExpressionType.Array)
                {
                    aa.ExpressionType = expression.ExpressionType.Clone();
                    aa.ExpressionType.Array = false; aa.ExpressionType.ArraySize = null;
                }
                else
                {
                    aa.ExpressionType.BaseType = expression.ExpressionType.BaseType switch
                    {
                        HLSLBaseType.Float2 or HLSLBaseType.Float3 or HLSLBaseType.Float4 => HLSLBaseType.Float,
                        HLSLBaseType.Float3x3 => HLSLBaseType.Float3,
                        HLSLBaseType.Float4x4 => HLSLBaseType.Float4,
                        HLSLBaseType.Half2 or HLSLBaseType.Half3 or HLSLBaseType.Half4 => HLSLBaseType.Half,
                        HLSLBaseType.Half3x3 => HLSLBaseType.Half3,
                        HLSLBaseType.Half4x4 => HLSLBaseType.Half4,
                        HLSLBaseType.Int2 or HLSLBaseType.Int3 or HLSLBaseType.Int4 => HLSLBaseType.Int,
                        HLSLBaseType.Uint2 or HLSLBaseType.Uint3 or HLSLBaseType.Uint4 => HLSLBaseType.Uint,
                        _ => HLSLBaseType.Unknown,
                    };
                    if (aa.ExpressionType.BaseType == HLSLBaseType.Unknown) return _tokenizer.Error("array, matrix, vector, or indexable object type expected");
                }
                expression = aa; done = false;
            }
            if (Accept('('))
            {
                var fc = HLSLTree.AddNode<HLSLFunctionCall>(fileName, line);
                HLSLExpression? args = null;
                if (!ParseExpressionList(')', false, ref args, out fc.NumArguments)) return false;
                fc.Argument = args;
                if (expression!.NodeType != HLSLNodeType.IdentifierExpression) return _tokenizer.Error("Expected function identifier");
                var identExpr = (HLSLIdentifierExpression)expression;
                var fn = MatchFunctionCall(fc, identExpr.Name!);
                if (fn == null) return false;
                fc.Function = fn; fc.ExpressionType = fn.ReturnType;
                expression = fc; done = false;
            }
        }
        return true;
    }

    private bool ParseExpressionList(int endToken, bool allowEmptyEnd, ref HLSLExpression? first, out int numExpressions)
    {
        numExpressions = 0;
        HLSLExpression? last = null;
        while (!Accept(endToken))
        {
            if (CheckForUnexpectedEndOfStream(endToken)) return false;
            if (numExpressions > 0 && !Expect(',')) return false;
            if (allowEmptyEnd && Accept(endToken)) break;
            HLSLExpression? expr = null;
            if (!ParseExpression(ref expr)) return false;
            if (first == null) first = expr;
            else last!.NextExpression = expr;
            last = expr; numExpressions++;
        }
        return true;
    }

    private bool ParseArgumentList(HLSLFunction function)
    {
        var fileName = FileName; var line = LineNumber;
        HLSLArgument? lastArg = null;
        function.NumArguments = 0;
        function.NumOutputArguments = 0;

        while (!Accept(')'))
        {
            if (CheckForUnexpectedEndOfStream(')')) return false;
            if (function.NumArguments > 0 && !Expect(',')) return false;

            var arg = HLSLTree.AddNode<HLSLArgument>(fileName, line);
            if (Accept((int)HLSLToken.Uniform)) arg.Modifier = HLSLArgumentModifier.Uniform;
            else if (Accept((int)HLSLToken.In)) arg.Modifier = HLSLArgumentModifier.In;
            else if (Accept((int)HLSLToken.Out)) arg.Modifier = HLSLArgumentModifier.Out;
            else if (Accept((int)HLSLToken.InOut)) arg.Modifier = HLSLArgumentModifier.Inout;
            else if (Accept((int)HLSLToken.Const)) arg.Modifier = HLSLArgumentModifier.Const;

            if (!ExpectDeclaration(true, arg.Type, out var argName)) return false;
            arg.Name = argName;
            DeclareVariable(arg.Name, arg.Type);

            var sem = "";
            if (Accept(':') && !ExpectIdentifier(out sem)) return false;
            arg.Semantic = sem;

            HLSLExpression? defVal = null;
            if (Accept('=') && !ParseExpression(ref defVal)) return false;
            arg.DefaultValue = defVal;

            if (lastArg != null) lastArg.NextArgument = arg;
            else function.Argument = arg;
            lastArg = arg;
            function.NumArguments++;
            if (arg.Modifier is HLSLArgumentModifier.Out or HLSLArgumentModifier.Inout) function.NumOutputArguments++;
        }
        return true;
    }

    // ── Effect / technique / pipeline / stage ────────────────────────────────

    private bool ParseSamplerState(ref HLSLExpression? expression)
    {
        if (!Expect((int)HLSLToken.SamplerState)) return false;
        var fileName = FileName; var line = LineNumber;
        var ss = HLSLTree.AddNode<HLSLSamplerState>(fileName, line);
        if (!Expect('{')) return false;
        HLSLStateAssignment? last = null;
        while (!Accept('}'))
        {
            if (CheckForUnexpectedEndOfStream('}')) return false;
            HLSLStateAssignment? sa = null;
            if (!ParseStateAssignment(ref sa, true, false)) return false;
            if (last == null) ss.StateAssignments = sa; else last.NextStateAssignment = sa;
            last = sa; ss.NumStateAssignments++;
        }
        expression = ss;
        return true;
    }

    private bool ParseTechnique(ref HLSLStatement? statement)
    {
        if (!Accept((int)HLSLToken.Technique)) return false;
        if (!ExpectIdentifier(out var name)) return false;
        if (!Expect('{')) return false;
        var tech = HLSLTree.AddNode<HLSLTechnique>(FileName, LineNumber);
        tech.Name = name;
        HLSLPass? lastPass = null;
        while (!Accept('}'))
        {
            if (CheckForUnexpectedEndOfStream('}')) return false;
            HLSLPass? pass = null;
            if (!ParsePass(ref pass)) return false;
            if (lastPass == null) tech.Passes = pass; else lastPass.NextPass = pass;
            lastPass = pass; tech.NumPasses++;
        }
        statement = tech;
        return true;
    }

    private bool ParsePass(ref HLSLPass? pass)
    {
        if (!Accept((int)HLSLToken.Pass)) return false;
        AcceptIdentifier(out var passName);
        if (!Expect('{')) return false;
        var fileName = FileName; var line = LineNumber;
        pass = HLSLTree.AddNode<HLSLPass>(fileName, line);
        pass.Name = passName;
        HLSLStateAssignment? last = null;
        while (!Accept('}'))
        {
            if (CheckForUnexpectedEndOfStream('}')) return false;
            HLSLStateAssignment? sa = null;
            if (!ParseStateAssignment(ref sa, false, false)) return false;
            if (last == null) pass.StateAssignments = sa; else last.NextStateAssignment = sa;
            last = sa; pass.NumStateAssignments++;
        }
        return true;
    }

    private bool ParsePipeline(ref HLSLStatement? statement)
    {
        if (!Accept("pipeline")) return false;
        AcceptIdentifier(out var pipelineName);
        if (!Expect('{')) return false;
        var pipeline = HLSLTree.AddNode<HLSLPipeline>(FileName, LineNumber);
        pipeline.Name = pipelineName;
        HLSLStateAssignment? last = null;
        while (!Accept('}'))
        {
            if (CheckForUnexpectedEndOfStream('}')) return false;
            HLSLStateAssignment? sa = null;
            if (!ParseStateAssignment(ref sa, false, true)) return false;
            if (last == null) pipeline.StateAssignments = sa; else last.NextStateAssignment = sa;
            last = sa; pipeline.NumStateAssignments++;
        }
        statement = pipeline;
        return true;
    }

    private bool ParseStage(ref HLSLStatement? statement)
    {
        if (!Accept("stage")) return false;
        if (!ExpectIdentifier(out var stageName)) return false;
        if (!Expect('{')) return false;
        var stage = HLSLTree.AddNode<HLSLStage>(FileName, LineNumber);
        stage.Name = stageName;
        BeginScope();
        var voidType = new HLSLType(HLSLBaseType.Void);
        if (!Expect('{') || !ParseBlock(ref stage.Statement, voidType)) return false;
        EndScope();
        statement = stage;
        return true;
    }

    private bool ParseStateAssignment(ref HLSLStateAssignment? stateAssignment, bool isSamplerState, bool isPipeline)
    {
        var fileName = FileName; var line = LineNumber;
        stateAssignment = HLSLTree.AddNode<HLSLStateAssignment>(fileName, line);
        if (!ParseStateName(isSamplerState, isPipeline, out var stateName, out var state)) return false;
        stateAssignment.StateName = state.Name;
        stateAssignment.D3dRenderState = state.D3drs;
        if (!Expect('=')) return false;
        if (!ParseStateValue(state, stateAssignment)) return false;
        return Expect(';');
    }

    private bool ParseStateName(bool isSamplerState, bool isPipeline, out string name, out EffectState state)
    {
        name = ""; state = default;
        // State names may coincide with reserved words (e.g. "Texture" is both a keyword and a sampler state)
        ReadOnlySpan<char> ident;
        if (_tokenizer.Token == (int)HLSLToken.Identifier)
            ident = _tokenizer.Identifier;
        else if (_tokenizer.Token == (int)HLSLToken.Texture)
            ident = "texture";
        else return _tokenizer.Error("Syntax error: expected identifier");
        var found = GetEffectState(ident, isSamplerState, isPipeline);
        if (found == null) { _tokenizer.Error($"Syntax error: unexpected identifier '{ident}'"); return false; }
        state = found.Value; name = state.Name;
        _tokenizer.Next();
        return true;
    }

    private bool ParseStateValue(EffectState state, HLSLStateAssignment sa)
    {
        var isColorMask = state.Values == ColorMaskValues;
        var isInteger = state.Values == IntegerValues;
        var isFloat = state.Values == FloatValues;
        var isBool = state.Values == BooleanValues;

        if (state.Values == null)
        {
            // Skip compile statement
            while (_tokenizer.Token != ';') _tokenizer.Next();
            return true;
        }
        if (isInteger) { if (!AcceptInt(out var v)) { _tokenizer.Error("Expected integer"); return false; } sa.IValue = v; return true; }
        if (isFloat) { if (!AcceptFloat(out var v)) { _tokenizer.Error("Expected float"); return false; } sa.FValue = v; return true; }
        if (isBool)
        {
            var sv = GetStateValue(_tokenizer.Identifier, state.Values);
            if (sv != null) { sa.IValue = sv.Value.Value; _tokenizer.Next(); return true; }
            if (AcceptInt(out var v)) { sa.IValue = v != 0 ? 1 : 0; return true; }
            _tokenizer.Error("Expected bool"); return false;
        }
        if (isColorMask) { if (!ParseColorMask(out var mask)) { _tokenizer.Error("Expected color mask"); return false; } sa.IValue = mask; return true; }

        var stateVal = GetStateValue(_tokenizer.Identifier, state.Values);
        if (stateVal == null) { _tokenizer.Error($"Unexpected value '{_tokenizer.Identifier}' for state '{state.Name}'"); return false; }
        sa.IValue = stateVal.Value.Value;
        _tokenizer.Next();
        return true;
    }

    private bool ParseColorMask(out int mask)
    {
        mask = 0;
        do
        {
            if (_tokenizer.Token == (int)HLSLToken.IntLiteral) mask |= _tokenizer.Int;
            else if (_tokenizer.Token == (int)HLSLToken.Identifier)
            {
                foreach (var sv in ColorMaskValues) { if (sv.Name != null && sv.Name.Equals(_tokenizer.Identifier, StringComparison.OrdinalIgnoreCase)) { mask |= sv.Value; break; } }
            }
            else return false;
            _tokenizer.Next();
        } while (Accept('|'));
        return true;
    }

    private bool ParseAttributeList(ref HLSLAttribute? firstAttribute)
    {
        var fileName = FileName; var line = LineNumber;
        var last = firstAttribute;
        do
        {
            if (!ExpectIdentifier(out var id)) return false;
            var attr = HLSLTree.AddNode<HLSLAttribute>(fileName, line);
            if (id == "unroll") attr.AttributeType = HLSLAttributeType.Unroll;
            else if (id == "flatten") attr.AttributeType = HLSLAttributeType.Flatten;
            else if (id == "branch") attr.AttributeType = HLSLAttributeType.Branch;
            else if (id == "nofastmath") attr.AttributeType = HLSLAttributeType.NoFastMath;
            if (firstAttribute == null) firstAttribute = attr;
            else last!.NextAttribute = attr;
            last = attr;
        } while (Accept(','));
        return true;
    }

    private bool ParseAttributeBlock(ref HLSLAttribute? attribute)
    {
        if (!Accept('[')) return false;
        ParseAttributeList(ref attribute);
        if (!Expect(']')) return false;
        ParseAttributeBlock(ref attribute);
        return true;
    }

    // ── Scope / variable management ─────────────────────────────────────────

    private void BeginScope() => _variables.Add(new Variable { Name = null });

    private void EndScope()
    {
        var i = _variables.Count - 1;
        while (_variables[i].Name != null) { i--; }
        _variables.RemoveRange(i, _variables.Count - i);
    }

    private void DeclareVariable(string name, HLSLType type)
    {
        if (_variables.Count == _numGlobals) _numGlobals++;
        _variables.Add(new Variable { Name = name, Type = type });
    }

    private HLSLType? FindVariable(string name, out bool global)
    {
        global = false;
        for (var i = _variables.Count - 1; i >= 0; i--)
        {
            if (_variables[i].Name == name) { global = i < _numGlobals; return _variables[i].Type; }
        }
        return null;
    }

    private HLSLFunction? FindFunction(string name)
    {
        foreach (var fn in _functions) if (fn.Name == name) return fn;
        return null;
    }

    private HLSLFunction? FindFunction(HLSLFunction fun)
    {
        foreach (var fn in _functions)
        {
            if (fn.Name == fun.Name && AreTypesEqual(fn.ReturnType, fun.ReturnType) && AreArgumentListsEqual(fn.Argument, fun.Argument))
                return fn;
        }
        return null;
    }

    private HLSLStruct? FindUserDefinedType(string name)
    {
        foreach (var t in _userTypes) if (t.Name == name) return t;
        return null;
    }

    private bool GetIsFunction(string name)
    {
        foreach (var fn in _functions) if (fn.Name == name) return true;
        foreach (var intr in _intrinsics) if (intr.Function.Name == name) return true;
        return false;
    }

    private bool CheckForUnexpectedEndOfStream(int endToken)
    {
        if (Accept((int)HLSLToken.EndOfStream))
        {
            _tokenizer.Error($"Unexpected end of file while looking for '{HLSLTokenizer.GetTokenName(endToken)}'");
            return true;
        }
        return false;
    }

    private bool CheckTypeCast(HLSLType srcType, HLSLType dstType)
    {
        if (GetTypeCastRank(_tree, srcType, dstType) == -1)
        {
            _tokenizer.Error($"Cannot implicitly convert from '{GetTypeName(srcType)}' to '{GetTypeName(dstType)}'");
            return false;
        }
        return true;
    }

    // ── Type ranking / matching ─────────────────────────────────────────────

    private static string GetTypeName(HLSLType type) =>
        type.BaseType == HLSLBaseType.UserDefined ? type.TypeName ?? "user defined" : _baseTypeDescriptions[(int)type.BaseType].TypeName;

    private static string GetBinaryOpName(HLSLBinaryOp op) => op switch
    {
        HLSLBinaryOp.And => "&&", HLSLBinaryOp.Or => "||",
        HLSLBinaryOp.Add => "+", HLSLBinaryOp.Sub => "-", HLSLBinaryOp.Mul => "*", HLSLBinaryOp.Div => "/",
        HLSLBinaryOp.Less => "<", HLSLBinaryOp.Greater => ">", HLSLBinaryOp.LessEqual => "<=", HLSLBinaryOp.GreaterEqual => ">=",
        HLSLBinaryOp.Equal => "==", HLSLBinaryOp.NotEqual => "!=",
        HLSLBinaryOp.BitAnd => "&", HLSLBinaryOp.BitOr => "|", HLSLBinaryOp.BitXor => "^",
        HLSLBinaryOp.Assign => "=", HLSLBinaryOp.AddAssign => "+=", HLSLBinaryOp.SubAssign => "-=",
        HLSLBinaryOp.MulAssign => "*=", HLSLBinaryOp.DivAssign => "/=",
        _ => "???",
    };

    private static int GetTypeCastRank(HLSLTree tree, HLSLType srcType, HLSLType dstType)
    {
        if (srcType.Array != dstType.Array) return -1;
        if (srcType.Array)
        {
            tree.GetExpressionValue(srcType.ArraySize, out var srcSize);
            tree.GetExpressionValue(dstType.ArraySize, out var dstSize);
            if (srcSize != dstSize) return -1;
        }
        if (srcType.BaseType == HLSLBaseType.UserDefined && dstType.BaseType == HLSLBaseType.UserDefined)
            return srcType.TypeName == dstType.TypeName ? 0 : -1;
        if (srcType.BaseType == dstType.BaseType)
        {
            if (srcType.BaseType.IsSamplerType()) return srcType.SamplerType == dstType.SamplerType ? 0 : -1;
            return 0;
        }

        ref readonly var srcDesc = ref _baseTypeDescriptions[(int)srcType.BaseType];
        ref readonly var dstDesc = ref _baseTypeDescriptions[(int)dstType.BaseType];
        if (srcDesc.NumericType == NumericType.NaN || dstDesc.NumericType == NumericType.NaN) return -1;

        var result = _numberTypeRank[(int)srcDesc.NumericType, (int)dstDesc.NumericType] << 1;
        if (srcDesc.NumDimensions == 0 && dstDesc.NumDimensions > 0) result |= 1;
        else if ((srcDesc.NumDimensions == dstDesc.NumDimensions && (srcDesc.NumComponents > dstDesc.NumComponents || srcDesc.Height > dstDesc.Height)) ||
                 (srcDesc.NumDimensions > 0 && dstDesc.NumDimensions == 0)) result |= (1 << 4);
        else if (srcDesc.NumDimensions != dstDesc.NumDimensions || srcDesc.NumComponents != dstDesc.NumComponents || srcDesc.Height != dstDesc.Height) return -1;
        return result;
    }

    private static bool AreTypesEqual(HLSLType a, HLSLType b)
    {
        if (a.BaseType == HLSLBaseType.UserDefined && b.BaseType == HLSLBaseType.UserDefined)
            return a.TypeName == b.TypeName;
        return a.BaseType == b.BaseType;
    }

    private static bool AreArgumentListsEqual(HLSLArgument? a, HLSLArgument? b)
    {
        while (a != null && b != null)
        {
            if (!AreTypesEqual(a.Type, b.Type) || a.Modifier != b.Modifier) return false;
            a = a.NextArgument; b = b.NextArgument;
        }
        return a == null && b == null;
    }

    private static bool GetBinaryOpResultType(HLSLBinaryOp op, HLSLType type1, HLSLType type2, HLSLType result)
    {
        if (type1.BaseType < HLSLBaseType_Ext.FirstNumeric || type1.BaseType > HLSLBaseType_Ext.LastNumeric || type1.Array ||
            type2.BaseType < HLSLBaseType_Ext.FirstNumeric || type2.BaseType > HLSLBaseType_Ext.LastNumeric || type2.Array)
            return false;

        if (op is HLSLBinaryOp.BitAnd or HLSLBinaryOp.BitOr or HLSLBinaryOp.BitXor)
            if (type1.BaseType < HLSLBaseType_Ext.FirstInteger || type1.BaseType > HLSLBaseType_Ext.LastInteger) return false;

        switch (op)
        {
            case HLSLBinaryOp.And: case HLSLBinaryOp.Or: case HLSLBinaryOp.Less: case HLSLBinaryOp.Greater:
            case HLSLBinaryOp.LessEqual: case HLSLBinaryOp.GreaterEqual: case HLSLBinaryOp.Equal: case HLSLBinaryOp.NotEqual:
                var numComp = Math.Max(_baseTypeDescriptions[(int)type1.BaseType].NumComponents, _baseTypeDescriptions[(int)type2.BaseType].NumComponents);
                result.BaseType = (HLSLBaseType)((int)HLSLBaseType.Bool + numComp - 1);
                break;
            default:
                // Use the lookup table (simplified: just pick the higher type)
                result.BaseType = type1.BaseType >= type2.BaseType ? type1.BaseType : type2.BaseType;
                break;
        }
        result.TypeName = null; result.Array = false; result.ArraySize = null;
        result.Flags = (type1.Flags & type2.Flags) & (int)HLSLTypeFlags.Const;
        return result.BaseType != HLSLBaseType.Unknown;
    }

    private static bool GetFunctionCallCastRanks(HLSLTree tree, HLSLFunctionCall call, HLSLFunction function, int[] rankBuffer)
    {
        if (function.NumArguments < call.NumArguments) return false;
        var expr = call.Argument; var arg = function.Argument;
        for (var i = 0; i < call.NumArguments; i++)
        {
            var rank = GetTypeCastRank(tree, expr!.ExpressionType, arg!.Type);
            if (rank == -1) return false;
            rankBuffer[i] = rank;
            arg = arg.NextArgument; expr = expr.NextExpression;
        }
        for (var i = call.NumArguments; i < function.NumArguments; i++)
        {
            if (arg?.DefaultValue == null) return false;
            arg = arg?.NextArgument;
        }
        return true;
    }

    private HLSLFunction? MatchFunctionCall(HLSLFunctionCall call, string name)
    {
        HLSLFunction? matched = null;
        var numMatched = 0;
        var nameMatches = false;

        foreach (var fn in _functions) MatchFunctionCall_TryMatch(call, fn, fn.Name == name, ref nameMatches, ref matched, ref numMatched);
        foreach (var intr in _intrinsics) MatchFunctionCall_TryMatch(call, intr.Function, intr.Function.Name == name, ref nameMatches, ref matched, ref numMatched);

        if (matched != null && numMatched > 1) { _tokenizer.Error($"'{name}' {numMatched} overloads have similar conversions"); return null; }
        if (matched == null)
        {
            if (nameMatches) _tokenizer.Error($"'{name}' no overloaded function matched all arguments");
            else _tokenizer.Error($"Undeclared identifier '{name}'");
        }
        return matched;
    }

    private void MatchFunctionCall_TryMatch(HLSLFunctionCall call, HLSLFunction fn, bool nameEqual, ref bool nameMatches, ref HLSLFunction? matched, ref int numMatched)
    {
        if (!nameEqual) return;
        nameMatches = true;
        var result = CompareFunctions(call, fn, matched);
        if (result == CompareFunctionsResult.Function1Better) { matched = fn; numMatched = 1; }
        else if (result == CompareFunctionsResult.FunctionsEqual) numMatched++;
    }

    private CompareFunctionsResult CompareFunctions(HLSLFunctionCall call, HLSLFunction fn1, HLSLFunction? fn2)
    {
        int[] r1 = new int[call.NumArguments], r2 = new int[call.NumArguments];
        var v1 = GetFunctionCallCastRanks(_tree, call, fn1, r1);
        var v2 = fn2 != null && GetFunctionCallCastRanks(_tree, call, fn2, r2);
        if (!(v1 && v2)) { if (v1) return CompareFunctionsResult.Function1Better; if (v2) return CompareFunctionsResult.Function2Better; return CompareFunctionsResult.FunctionsEqual; }
        Array.Sort(r1); Array.Reverse(r1);
        Array.Sort(r2); Array.Reverse(r2);
        for (var i = 0; i < call.NumArguments; i++)
        {
            if (r1[i] < r2[i]) return CompareFunctionsResult.Function1Better;
            if (r2[i] < r1[i]) return CompareFunctionsResult.Function2Better;
        }
        return CompareFunctionsResult.FunctionsEqual;
    }

    private bool GetMemberType(HLSLType objectType, HLSLMemberAccess memberAccess)
    {
        var field = memberAccess.Field!;

        if (objectType.BaseType == HLSLBaseType.UserDefined)
        {
            var structure = FindUserDefinedType(objectType.TypeName!);
            if (structure == null) return false;
            var f = structure.Field;
            while (f != null) { if (f.Name == field) { memberAccess.ExpressionType = f.Type; return true; } f = f.NextField; }
            return false;
        }

        ref readonly var desc = ref _baseTypeDescriptions[(int)objectType.BaseType];
        if (desc.NumericType == NumericType.NaN) return false;

        var swizzleLength = 0;
        if (desc.NumDimensions <= 1)
        {
            foreach (var c in field)
            {
                if (c is not ('x' or 'y' or 'z' or 'w' or 'r' or 'g' or 'b' or 'a'))
                {
                    _tokenizer.Error($"Invalid swizzle '{field}'");
                    return false;
                }
                swizzleLength++;
            }
        }
        else
        {
            var n = 0;
            while (n < field.Length && field[n] == '_')
            {
                n++;
                var b = 1;
                if (n < field.Length && field[n] == 'm') { b = 0; n++; }
                if (n + 1 >= field.Length || !char.IsDigit(field[n]) || !char.IsDigit(field[n + 1])) return false;
                int r = (field[n] - '0') - b, c1 = (field[n + 1] - '0') - b;
                if (r >= desc.Height || c1 >= desc.NumComponents) return false;
                swizzleLength++; n += 2;
            }
            if (n != field.Length) return false;
        }
        if (swizzleLength is 0 or > 4) return _tokenizer.Error($"Invalid swizzle '{field}'");

        ReadOnlySpan<HLSLBaseType> types = desc.NumericType switch
        {
            NumericType.Float => [HLSLBaseType.Float, HLSLBaseType.Float2, HLSLBaseType.Float3, HLSLBaseType.Float4],
            NumericType.Half => [HLSLBaseType.Half, HLSLBaseType.Half2, HLSLBaseType.Half3, HLSLBaseType.Half4],
            NumericType.Int => [HLSLBaseType.Int, HLSLBaseType.Int2, HLSLBaseType.Int3, HLSLBaseType.Int4],
            NumericType.Uint => [HLSLBaseType.Uint, HLSLBaseType.Uint2, HLSLBaseType.Uint3, HLSLBaseType.Uint4],
            NumericType.Bool => [HLSLBaseType.Bool],
            _ => throw new InvalidOperationException(),
        };
        memberAccess.ExpressionType.BaseType = types[swizzleLength - 1];
        memberAccess.Swizzle = true;
        return true;
    }

    private static EffectState? GetEffectState(ReadOnlySpan<char> name, bool isSamplerState, bool isPipeline)
    {
        var states = isPipeline ? PipelineStates : isSamplerState ? SamplerStates : EffectStates;
        foreach (var s in states)
            if (s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    private static EffectStateValue? GetStateValue(ReadOnlySpan<char> name, EffectStateValue[] values)
    {
        foreach (var v in values)
        {
            if (v.Name == null) break;
            if (v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return v;
        }
        return null;
    }
}
