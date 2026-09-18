// Ported from the native ABI declared in FNA/lib/FNA3D/include/FNA3D.h (FNA3D is a plain C
// library with a stable C ABI; these enums mirror it 1:1 and carry no dependency on
// Microsoft.Xna.Framework, unlike FNA's own C# FNA3D.cs wrapper).
namespace NFMWorld.Graphics.FNA3D.Native;

public enum FNA3D_PresentInterval
{
    Default,
    One,
    Two,
    Immediate,
}

public enum FNA3D_DisplayOrientation
{
    Default,
    LandscapeLeft,
    LandscapeRight,
    Portrait,
}

public enum FNA3D_RenderTargetUsage
{
    DiscardContents,
    PreserveContents,
    PlatformContents,
}

[Flags]
public enum FNA3D_ClearOptions
{
    Target = 1,
    DepthBuffer = 2,
    Stencil = 4,
}

public enum FNA3D_PrimitiveType
{
    TriangleList,
    TriangleStrip,
    LineList,
    LineStrip,
    PointListExt,
}

public enum FNA3D_IndexElementSize
{
    Bits16,
    Bits32,
}

public enum FNA3D_SurfaceFormat
{
    Color,
    Bgr565,
    Bgra5551,
    Bgra4444,
    Dxt1,
    Dxt3,
    Dxt5,
    NormalizedByte2,
    NormalizedByte4,
    Rgba1010102,
    Rg32,
    Rgba64,
    Alpha8,
    Single,
    Vector2,
    Vector4,
    HalfSingle,
    HalfVector2,
    HalfVector4,
    HdrBlendable,
    ColorBgraExt,
    ColorSrgbExt,
    Dxt5SrgbExt,
    Bc7Ext,
    Bc7SrgbExt,
    ByteExt,
    UShortExt,
}

public enum FNA3D_DepthFormat
{
    None,
    D16,
    D24,
    D24S8,
}

public enum FNA3D_CubeMapFace
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ,
}

public enum FNA3D_BufferUsage
{
    None,
    WriteOnly,
}

public enum FNA3D_SetDataOptions
{
    None,
    Discard,
    NoOverwrite,
}

public enum FNA3D_Blend
{
    One,
    Zero,
    SourceColor,
    InverseSourceColor,
    SourceAlpha,
    InverseSourceAlpha,
    DestinationColor,
    InverseDestinationColor,
    DestinationAlpha,
    InverseDestinationAlpha,
    BlendFactor,
    InverseBlendFactor,
    SourceAlphaSaturation,
}

public enum FNA3D_BlendFunction
{
    Add,
    Subtract,
    ReverseSubtract,
    Max,
    Min,
}

[Flags]
public enum FNA3D_ColorWriteChannels
{
    None = 0,
    Red = 1,
    Green = 2,
    Blue = 4,
    Alpha = 8,
    All = 15,
}

public enum FNA3D_StencilOperation
{
    Keep,
    Zero,
    Replace,
    Increment,
    Decrement,
    IncrementSaturation,
    DecrementSaturation,
    Invert,
}

public enum FNA3D_CompareFunction
{
    Always,
    Never,
    Less,
    LessEqual,
    Equal,
    GreaterEqual,
    Greater,
    NotEqual,
}

public enum FNA3D_CullMode
{
    None,
    CullClockwiseFace,
    CullCounterClockwiseFace,
}

public enum FNA3D_FillMode
{
    Solid,
    Wireframe,
}

public enum FNA3D_TextureAddressMode
{
    Wrap,
    Clamp,
    Mirror,
}

public enum FNA3D_TextureFilter
{
    Linear,
    Point,
    Anisotropic,
    LinearMipPoint,
    PointMipLinear,
    MinLinearMagPointMipLinear,
    MinLinearMagPointMipPoint,
    MinPointMagLinearMipLinear,
    MinPointMagLinearMipPoint,
}

public enum FNA3D_VertexElementFormat
{
    Single,
    Vector2,
    Vector3,
    Vector4,
    Color,
    Byte4,
    Short2,
    Short4,
    NormalizedShort2,
    NormalizedShort4,
    HalfVector2,
    HalfVector4,
}

public enum FNA3D_VertexElementUsage
{
    Position,
    Color,
    TextureCoordinate,
    Normal,
    Binormal,
    Tangent,
    BlendIndices,
    BlendWeight,
    Depth,
    Fog,
    PointSize,
    Sample,
    TesselateFactor,
}
