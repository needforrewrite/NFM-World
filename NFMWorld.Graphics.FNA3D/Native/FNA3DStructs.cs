using System.Runtime.InteropServices;

namespace NFMWorld.Graphics.FNA3D.Native;

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_Color
{
    public byte r, g, b, a;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_Rect
{
    public int x, y, w, h;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_Vec4
{
    public float x, y, z, w;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_Viewport
{
    public int x, y, w, h;
    public float minDepth, maxDepth;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_PresentationParameters
{
    public int backBufferWidth;
    public int backBufferHeight;
    public FNA3D_SurfaceFormat backBufferFormat;
    public int multiSampleCount;
    public IntPtr deviceWindowHandle;
    public byte isFullScreen;
    public FNA3D_DepthFormat depthStencilFormat;
    public FNA3D_PresentInterval presentationInterval;
    public FNA3D_DisplayOrientation displayOrientation;
    public FNA3D_RenderTargetUsage renderTargetUsage;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_BlendState
{
    public FNA3D_Blend colorSourceBlend;
    public FNA3D_Blend colorDestinationBlend;
    public FNA3D_BlendFunction colorBlendFunction;
    public FNA3D_Blend alphaSourceBlend;
    public FNA3D_Blend alphaDestinationBlend;
    public FNA3D_BlendFunction alphaBlendFunction;
    public FNA3D_ColorWriteChannels colorWriteEnable;
    public FNA3D_ColorWriteChannels colorWriteEnable1;
    public FNA3D_ColorWriteChannels colorWriteEnable2;
    public FNA3D_ColorWriteChannels colorWriteEnable3;
    public FNA3D_Color blendFactor;
    public int multiSampleMask;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_DepthStencilState
{
    public byte depthBufferEnable;
    public byte depthBufferWriteEnable;
    public FNA3D_CompareFunction depthBufferFunction;
    public byte stencilEnable;
    public int stencilMask;
    public int stencilWriteMask;
    public byte twoSidedStencilMode;
    public FNA3D_StencilOperation stencilFail;
    public FNA3D_StencilOperation stencilDepthBufferFail;
    public FNA3D_StencilOperation stencilPass;
    public FNA3D_CompareFunction stencilFunction;
    public FNA3D_StencilOperation ccwStencilFail;
    public FNA3D_StencilOperation ccwStencilDepthBufferFail;
    public FNA3D_StencilOperation ccwStencilPass;
    public FNA3D_CompareFunction ccwStencilFunction;
    public int referenceStencil;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_RasterizerState
{
    public FNA3D_FillMode fillMode;
    public FNA3D_CullMode cullMode;
    public float depthBias;
    public float slopeScaleDepthBias;
    public byte scissorTestEnable;
    public byte multiSampleAntiAlias;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_SamplerState
{
    public FNA3D_TextureFilter filter;
    public FNA3D_TextureAddressMode addressU;
    public FNA3D_TextureAddressMode addressV;
    public FNA3D_TextureAddressMode addressW;
    public float mipMapLevelOfDetailBias;
    public int maxAnisotropy;
    public int maxMipLevel;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_VertexElement
{
    public int offset;
    public FNA3D_VertexElementFormat vertexElementFormat;
    public FNA3D_VertexElementUsage vertexElementUsage;
    public int usageIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_VertexDeclaration
{
    public int vertexStride;
    public int elementCount;
    public IntPtr elements; /* FNA3D_VertexElement* */
}

[StructLayout(LayoutKind.Sequential)]
public struct FNA3D_VertexBufferBinding
{
    public IntPtr vertexBuffer; /* FNA3D_Buffer* */
    public FNA3D_VertexDeclaration vertexDeclaration;
    public int vertexOffset;
    public int instanceFrequency;
}

/// <summary>
/// Mirrors the native anonymous union (twod: width/height vs. cube: size/face) via explicit
/// field offsets - both branches start where <c>type</c> ends, matching FNA3D.h's layout.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct FNA3D_RenderTargetBinding
{
    public const byte TypeTwoD = 0;
    public const byte TypeCube = 1;

    [FieldOffset(0)] public byte type;

    [FieldOffset(4)] public int twod_width;
    [FieldOffset(8)] public int twod_height;

    [FieldOffset(4)] public int cube_size;
    [FieldOffset(8)] public FNA3D_CubeMapFace cube_face;

    [FieldOffset(12)] public int levelCount;
    [FieldOffset(16)] public int multiSampleCount;
    [FieldOffset(24)] public IntPtr texture;
    [FieldOffset(32)] public IntPtr colorBuffer;
}
