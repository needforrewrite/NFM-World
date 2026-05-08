namespace NFMWorld.Graphics.Core;

// ── Texture / Surface ────────────────────────────────────────────

public enum TextureFormat
{
    R8Unorm,
    R8G8B8A8Unorm,
    B8G8R8A8Unorm,
    R8G8B8A8UnormSrgb,
    B8G8R8A8UnormSrgb,
    R16G16B16A16Float,
    R32Float,
    R32G32Float,
    R32G32B32A32Float,
    D16Unorm,
    D24Unorm,
    D32Float,
    D24UnormS8Uint,
    D32FloatS8Uint,
}

[Flags]
public enum TextureUsageFlags
{
    Sampler = 0x01,
    ColorTarget = 0x02,
    DepthStencilTarget = 0x04,
    GraphicsStorageRead = 0x08,
    ComputeStorageRead = 0x10,
    ComputeStorageWrite = 0x20,
}

// ── Buffers ──────────────────────────────────────────────────────

[Flags]
public enum BufferUsageFlags
{
    Vertex = 0x01,
    Index = 0x02,
    Indirect = 0x04,
    GraphicsStorageRead = 0x08,
    ComputeStorageRead = 0x10,
    ComputeStorageWrite = 0x20,
}

public enum IndexElementSize
{
    Sixteen,
    ThirtyTwo,
}

// ── Pipeline / Render State ──────────────────────────────────────

public enum PrimitiveType
{
    TriangleList,
    TriangleStrip,
    LineList,
    LineStrip,
    PointList,
}

public enum CullMode
{
    None,
    Front,
    Back,
}

public enum FrontFace
{
    CounterClockwise,
    Clockwise,
}

public enum FillMode
{
    Fill,
    Line,
}

public enum CompareOp
{
    Invalid,
    Never,
    Less,
    Equal,
    LessOrEqual,
    Greater,
    NotEqual,
    GreaterOrEqual,
    Always,
}

public enum StencilOp
{
    Invalid,
    Keep,
    Zero,
    Replace,
    IncrementAndClamp,
    DecrementAndClamp,
    Invert,
    IncrementAndWrap,
    DecrementAndWrap,
}

public enum BlendFactor
{
    Invalid,
    Zero,
    One,
    SrcColor,
    OneMinusSrcColor,
    DstColor,
    OneMinusDstColor,
    SrcAlpha,
    OneMinusSrcAlpha,
    DstAlpha,
    OneMinusDstAlpha,
    ConstantColor,
    OneMinusConstantColor,
    SrcAlphaSaturate,
}

public enum BlendOp
{
    Invalid,
    Add,
    Subtract,
    ReverseSubtract,
    Min,
    Max,
}

[Flags]
public enum ColorComponentFlags : byte
{
    None = 0,
    R = 0x01,
    G = 0x02,
    B = 0x04,
    A = 0x08,
    All = R | G | B | A,
}

public enum LoadOp
{
    Load,
    Clear,
    DontCare,
}

public enum StoreOp
{
    Store,
    DontCare,
    Resolve,
    StoreAndResolve,
}

public enum SampleCount
{
    One,
    Two,
    Four,
    Eight,
}

// ── Sampler ──────────────────────────────────────────────────────

public enum Filter
{
    Nearest,
    Linear,
}

public enum SamplerMipmapMode
{
    Nearest,
    Linear,
}

public enum SamplerAddressMode
{
    Repeat,
    MirroredRepeat,
    ClampToEdge,
}

// ── Shader ───────────────────────────────────────────────────────

public enum ShaderStage
{
    Vertex,
    Fragment,
}

// ── Vertex Input ─────────────────────────────────────────────────

public enum VertexInputRate
{
    Vertex,
    Instance,
}

public enum VertexElementFormat
{
    Invalid,
    Int,
    Int2,
    Int3,
    Int4,
    Uint,
    Uint2,
    Uint3,
    Uint4,
    Float,
    Float2,
    Float3,
    Float4,
    Byte2,
    Byte4,
    Ubyte2,
    Ubyte4,
    Byte2Norm,
    Byte4Norm,
    Ubyte2Norm,
    Ubyte4Norm,
    Short2,
    Short4,
    Ushort2,
    Ushort4,
    Short2Norm,
    Short4Norm,
    Ushort2Norm,
    Ushort4Norm,
    Half2,
    Half4,
}

// ── Presentation ─────────────────────────────────────────────────

public enum PresentMode
{
    VSync,
    Immediate,
    Mailbox,
}

public enum SwapchainComposition
{
    SDR,
    SDRLinear,
    HDRExtendedLinear,
    HDR10ST2084,
}
