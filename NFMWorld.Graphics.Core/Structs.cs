using System.Numerics;
using System.Runtime.InteropServices;

namespace NFMWorld.Graphics.Core;

// ── Viewport / Scissor ───────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public struct Viewport
{
    public float X;
    public float Y;
    public float W;
    public float H;
    public float MinDepth;
    public float MaxDepth;

    public Viewport(float w, float h)
    {
        X = 0; Y = 0;
        W = w; H = h;
        MinDepth = 0; MaxDepth = 1;
    }

    public Viewport(float x, float y, float w, float h)
    {
        X = x; Y = y;
        W = w; H = h;
        MinDepth = 0; MaxDepth = 1;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Rect(int x, int y, int w, int h)
{
    public int X = x;
    public int Y = y;
    public int W = w;
    public int H = h;

    public Rect(int w, int h) : this(0, 0, w, h)
    {
    }
}

// ── Color ────────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public struct Color(byte r, byte g, byte b, byte a = 255)
{
    public byte R = r, G = g, B = b, A = a;

    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Transparent = new(0, 0, 0, 0);
}

// ── Render State Structs ─────────────────────────────────────────

public struct StencilOpState
{
    public StencilOp FailOp;
    public StencilOp PassOp;
    public StencilOp DepthFailOp;
    public CompareOp CompareOp;
}

public struct DepthStencilState
{
    public CompareOp CompareOp;
    public StencilOpState BackStencilState;
    public StencilOpState FrontStencilState;
    public byte CompareMask;
    public byte WriteMask;
    public bool EnableDepthTest;
    public bool EnableDepthWrite;
    public bool EnableStencilTest;

    public static readonly DepthStencilState Disable = new()
    {
        CompareOp = Core.CompareOp.Invalid,
        EnableDepthTest = false,
        EnableDepthWrite = false,
        EnableStencilTest = false,
    };
}

public struct RasterizerState
{
    public FillMode FillMode;
    public CullMode CullMode;
    public FrontFace FrontFace;
    public float DepthBiasConstantFactor;
    public float DepthBiasClamp;
    public float DepthBiasSlopeFactor;
    public bool EnableDepthBias;
    public bool EnableDepthClip;

    public static RasterizerState CCW_CullBack => new()
    {
        FillMode = FillMode.Fill,
        CullMode = CullMode.Back,
        FrontFace = FrontFace.CounterClockwise,
    };

    public static RasterizerState CCW_CullNone => new()
    {
        FillMode = FillMode.Fill,
        CullMode = CullMode.None,
        FrontFace = FrontFace.CounterClockwise,
    };

    public static RasterizerState CCW_CullFront => new()
    {
        FillMode = FillMode.Fill,
        CullMode = CullMode.Front,
        FrontFace = FrontFace.CounterClockwise,
    };

    public static RasterizerState CCW_Wireframe => new()
    {
        FillMode = FillMode.Line,
        CullMode = CullMode.None,
        FrontFace = FrontFace.CounterClockwise,
    };
}

public struct MultisampleState
{
    public SampleCount SampleCount;
    public uint SampleMask;
    public bool EnableMask;

    public static readonly MultisampleState None = new()
    {
        SampleCount = SampleCount.One,
    };
}

public struct ColorTargetBlendState
{
    public BlendFactor SrcColorBlendFactor;
    public BlendFactor DstColorBlendFactor;
    public BlendOp ColorBlendOp;
    public BlendFactor SrcAlphaBlendFactor;
    public BlendFactor DstAlphaBlendFactor;
    public BlendOp AlphaBlendOp;
    public ColorComponentFlags ColorWriteMask;
    public bool EnableBlend;
    public bool EnableColorWriteMask;

    public static readonly ColorTargetBlendState NoBlend = new()
    {
        ColorWriteMask = ColorComponentFlags.All,
    };

    public static readonly ColorTargetBlendState Opaque = new()
    {
        ColorWriteMask = ColorComponentFlags.All,
    };

    public static readonly ColorTargetBlendState NoWrite = new()
    {
        EnableColorWriteMask = true,
        ColorWriteMask = ColorComponentFlags.None,
    };

    public static readonly ColorTargetBlendState Additive = new()
    {
        EnableBlend = true,
        SrcColorBlendFactor = BlendFactor.SrcAlpha,
        DstColorBlendFactor = BlendFactor.One,
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = BlendFactor.SrcAlpha,
        DstAlphaBlendFactor = BlendFactor.One,
        AlphaBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.All,
    };

    public static readonly ColorTargetBlendState PremultipliedAlphaBlend = new()
    {
        EnableBlend = true,
        SrcColorBlendFactor = BlendFactor.One,
        DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = BlendFactor.One,
        DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
        AlphaBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.All,
    };

    public static readonly ColorTargetBlendState NonPremultipliedAlphaBlend = new()
    {
        EnableBlend = true,
        SrcColorBlendFactor = BlendFactor.SrcAlpha,
        DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = BlendFactor.SrcAlpha,
        DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
        AlphaBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.All,
    };
}

// ── Sampler State ────────────────────────────────────────────────

public struct SamplerCreateInfo
{
    public Filter MinFilter;
    public Filter MagFilter;
    public SamplerMipmapMode MipmapMode;
    public SamplerAddressMode AddressModeU;
    public SamplerAddressMode AddressModeV;
    public SamplerAddressMode AddressModeW;
    public float MipLodBias;
    public float MaxAnisotropy;
    public CompareOp CompareOp;
    public float MinLod;
    public float MaxLod;
    public bool EnableAnisotropy;
    public bool EnableCompare;

    public static readonly SamplerCreateInfo PointClamp = new()
    {
        MinFilter = Filter.Nearest,
        MagFilter = Filter.Nearest,
        MipmapMode = SamplerMipmapMode.Nearest,
        AddressModeU = SamplerAddressMode.ClampToEdge,
        AddressModeV = SamplerAddressMode.ClampToEdge,
        AddressModeW = SamplerAddressMode.ClampToEdge,
    };

    public static readonly SamplerCreateInfo PointWrap = new()
    {
        MinFilter = Filter.Nearest,
        MagFilter = Filter.Nearest,
        MipmapMode = SamplerMipmapMode.Nearest,
        AddressModeU = SamplerAddressMode.Repeat,
        AddressModeV = SamplerAddressMode.Repeat,
        AddressModeW = SamplerAddressMode.Repeat,
    };

    public static readonly SamplerCreateInfo LinearClamp = new()
    {
        MinFilter = Filter.Linear,
        MagFilter = Filter.Linear,
        MipmapMode = SamplerMipmapMode.Linear,
        AddressModeU = SamplerAddressMode.ClampToEdge,
        AddressModeV = SamplerAddressMode.ClampToEdge,
        AddressModeW = SamplerAddressMode.ClampToEdge,
    };

    public static readonly SamplerCreateInfo LinearWrap = new()
    {
        MinFilter = Filter.Linear,
        MagFilter = Filter.Linear,
        MipmapMode = SamplerMipmapMode.Linear,
        AddressModeU = SamplerAddressMode.Repeat,
        AddressModeV = SamplerAddressMode.Repeat,
        AddressModeW = SamplerAddressMode.Repeat,
    };

    public static readonly SamplerCreateInfo AnisotropicClamp = new()
    {
        MinFilter = Filter.Linear,
        MagFilter = Filter.Linear,
        MipmapMode = SamplerMipmapMode.Linear,
        AddressModeU = SamplerAddressMode.ClampToEdge,
        AddressModeV = SamplerAddressMode.ClampToEdge,
        AddressModeW = SamplerAddressMode.ClampToEdge,
        EnableAnisotropy = true,
        MaxAnisotropy = 4,
    };

    public static readonly SamplerCreateInfo AnisotropicWrap = new()
    {
        MinFilter = Filter.Linear,
        MagFilter = Filter.Linear,
        MipmapMode = SamplerMipmapMode.Linear,
        AddressModeU = SamplerAddressMode.Repeat,
        AddressModeV = SamplerAddressMode.Repeat,
        AddressModeW = SamplerAddressMode.Repeat,
        EnableAnisotropy = true,
        MaxAnisotropy = 4,
    };
}

// ── Pipeline Creation Structs ────────────────────────────────────

public struct ColorTargetDescription
{
    public TextureFormat Format;
    public ColorTargetBlendState BlendState;
}

public struct GraphicsPipelineTargetInfo
{
    public ColorTargetDescription[] ColorTargetDescriptions;
    public TextureFormat DepthStencilFormat;
    public bool HasDepthStencilTarget;
}

public struct VertexBufferDescription
{
    public uint Slot;
    public uint Pitch;
    public VertexInputRate InputRate;
    public uint InstanceStepRate;
}

public struct VertexAttribute
{
    public uint Location;
    public uint BufferSlot;
    public VertexElementFormat Format;
    public uint Offset;
}

public struct VertexInputState
{
    public VertexBufferDescription[] VertexBufferDescriptions;
    public VertexAttribute[] VertexAttributes;

    public static readonly VertexInputState Empty = new()
    {
        VertexBufferDescriptions = [],
        VertexAttributes = [],
    };
}

public struct GraphicsPipelineCreateInfo
{
    public IShader VertexShader;
    public IShader FragmentShader;
    public VertexInputState VertexInputState;
    public PrimitiveType PrimitiveType;
    public RasterizerState RasterizerState;
    public MultisampleState MultisampleState;
    public DepthStencilState DepthStencilState;
    public GraphicsPipelineTargetInfo TargetInfo;
    public string? Name;
}

// ── Render Pass Structs ──────────────────────────────────────────

public struct ColorTargetInfo
{
    public ITexture Texture;
    public uint MipLevel;
    public uint LayerOrDepthPlane;
    public Color ClearColor;
    public LoadOp LoadOp;
    public StoreOp StoreOp;
    public bool Cycle;
}

public struct DepthStencilTargetInfo
{
    public ITexture Texture;
    public float ClearDepth;
    public LoadOp LoadOp;
    public StoreOp StoreOp;
    public LoadOp StencilLoadOp;
    public StoreOp StencilStoreOp;
    public bool Cycle;
    public byte ClearStencil;
}

// ── Buffer / Texture Binding ─────────────────────────────────────

public struct BufferBinding(IBuffer buffer, uint offset = 0)
{
    public IBuffer Buffer = buffer;
    public uint Offset = offset;
}

public struct TextureSamplerBinding(ITexture texture, ISampler sampler)
{
    public ITexture Texture = texture;
    public ISampler Sampler = sampler;
}
