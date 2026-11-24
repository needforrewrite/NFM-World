// LLM maintained.
//
// Translates the abstraction's enums into WebGPU's, and answers the two size questions every
// transfer path asks (how many bytes a row occupies, how many bytes a pixel is).
//
// The tables are explicit rather than cast for the reason the other backends' mapping files give:
// the two vocabularies differ in both directions, and the one place they silently disagree -
// blend operations - is checked rather than translated.

using Wg = Maxine.WebGPU;
using BlendFactor = NFMWorld.Graphics.BlendFactor;
using BlendOperation = NFMWorld.Graphics.BlendOperation;
using ColorWriteMask = NFMWorld.Graphics.ColorWriteMask;
using CompareFunction = NFMWorld.Graphics.CompareFunction;
using CullMode = NFMWorld.Graphics.CullMode;
using PrimitiveTopology = NFMWorld.Graphics.PrimitiveTopology;
using StencilOperation = NFMWorld.Graphics.StencilOperation;
using TextureAddressMode = NFMWorld.Graphics.TextureAddressMode;
using TextureFilter = NFMWorld.Graphics.TextureFilter;
using TextureFormat = NFMWorld.Graphics.TextureFormat;
using VertexAttributeFormat = NFMWorld.Shaders.VertexAttributeFormat;

namespace NFMWorld.Graphics.WebGPU;

internal static class WebGpuMapping
{
    /// <summary>
    /// The texture format. A block-compressed texture needs <c>texture-compression-bc</c>, which the
    /// device requests unconditionally - every RAD model in this tree carries DXT data.
    /// </summary>
    public static Wg.TextureFormat ToTextureFormat(this TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 => Wg.TextureFormat.Rgba8Unorm,
        TextureFormat.Bgra8 => Wg.TextureFormat.Bgra8Unorm,
        TextureFormat.R8 => Wg.TextureFormat.R8Unorm,
        // Renderable and sampleable; filtering needs float32-filterable, which the device requests.
        TextureFormat.Single => Wg.TextureFormat.R32float,
        TextureFormat.Rgba32f => Wg.TextureFormat.Rgba32float,
        // WebGPU has no combined depth-stencil that is also sampleable, and none of this tree's
        // shaders sample a depth texture - only the shadow cascades write one.
        TextureFormat.Depth24Stencil8 => Wg.TextureFormat.Depth24PlusStencil8,
        TextureFormat.Dxt1 => Wg.TextureFormat.BC1RgbaUnorm,
        TextureFormat.Dxt3 => Wg.TextureFormat.BC2RgbaUnorm,
        TextureFormat.Dxt5 => Wg.TextureFormat.BC3RgbaUnorm,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static bool IsDepthFormat(this TextureFormat format) => format == TextureFormat.Depth24Stencil8;

    public static bool IsBlockCompressed(this TextureFormat format) =>
        format is TextureFormat.Dxt1 or TextureFormat.Dxt3 or TextureFormat.Dxt5;

    /// <summary>Texel rows one 4x4 block covers, i.e. how many image rows a block-compressed row is.</summary>
    public static int TexelBlockHeight(this TextureFormat format) => format.IsBlockCompressed() ? 4 : 1;

    /// <summary>The bytes one *row* of the format occupies at the given width.</summary>
    public static int RowBytes(this TextureFormat format, int width) => format switch
    {
        TextureFormat.R8 => width,
        TextureFormat.Rgba8 or TextureFormat.Bgra8 => width * 4,
        TextureFormat.Single => width * 4,
        TextureFormat.Rgba32f => width * 16,
        TextureFormat.Depth24Stencil8 => width * 4,
        TextureFormat.Dxt1 => (width + 3) / 4 * 8,
        TextureFormat.Dxt3 or TextureFormat.Dxt5 => (width + 3) / 4 * 16,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>The number of block rows an image of the given height has.</summary>
    public static int RowCount(this TextureFormat format, int height)
    {
        var block = format.TexelBlockHeight();
        return (height + block - 1) / block;
    }

    /// <summary>Bytes per pixel, for the formats whose rows are contiguous texels. Block-compressed is 0.</summary>
    public static int BytesPerPixel(this TextureFormat format) => format switch
    {
        TextureFormat.R8 => 1,
        TextureFormat.Rgba8 or TextureFormat.Bgra8 => 4,
        TextureFormat.Single => 4,
        TextureFormat.Rgba32f => 16,
        TextureFormat.Depth24Stencil8 => 4,
        _ => 0,
    };

    public static Wg.VertexFormat ToVertexFormat(this VertexAttributeFormat format) => format switch
    {
        VertexAttributeFormat.Float1 => Wg.VertexFormat.Float32,
        VertexAttributeFormat.Float2 => Wg.VertexFormat.Float32x2,
        VertexAttributeFormat.Float3 => Wg.VertexFormat.Float32x3,
        VertexAttributeFormat.Float4 => Wg.VertexFormat.Float32x4,
        VertexAttributeFormat.Byte4Normalized => Wg.VertexFormat.Unorm8x4,
        VertexAttributeFormat.Short4Normalized => Wg.VertexFormat.Snorm16x4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static Wg.PrimitiveTopology ToTopology(this PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => Wg.PrimitiveTopology.TriangleList,
        PrimitiveTopology.TriangleStrip => Wg.PrimitiveTopology.TriangleStrip,
        PrimitiveTopology.LineList => Wg.PrimitiveTopology.LineList,
        PrimitiveTopology.LineStrip => Wg.PrimitiveTopology.LineStrip,
        PrimitiveTopology.PointList => Wg.PrimitiveTopology.PointList,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    public static Wg.CullMode ToCullMode(this CullMode mode) => mode switch
    {
        CullMode.None => Wg.CullMode.None,
        CullMode.Front => Wg.CullMode.Front,
        CullMode.Back => Wg.CullMode.Back,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static Wg.CompareFunction ToCompareFunction(this CompareFunction function) => function switch
    {
        CompareFunction.Always => Wg.CompareFunction.Always,
        CompareFunction.Never => Wg.CompareFunction.Never,
        CompareFunction.Less => Wg.CompareFunction.Less,
        CompareFunction.LessEqual => Wg.CompareFunction.LessEqual,
        CompareFunction.Equal => Wg.CompareFunction.Equal,
        CompareFunction.NotEqual => Wg.CompareFunction.NotEqual,
        CompareFunction.GreaterEqual => Wg.CompareFunction.GreaterEqual,
        CompareFunction.Greater => Wg.CompareFunction.Greater,
        _ => throw new ArgumentOutOfRangeException(nameof(function), function, null),
    };

    public static Wg.BlendFactor ToBlendFactor(this BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => Wg.BlendFactor.Zero,
        BlendFactor.One => Wg.BlendFactor.One,
        BlendFactor.SourceAlpha => Wg.BlendFactor.SrcAlpha,
        BlendFactor.InverseSourceAlpha => Wg.BlendFactor.OneMinusSrcAlpha,
        BlendFactor.DestinationAlpha => Wg.BlendFactor.DstAlpha,
        BlendFactor.InverseDestinationAlpha => Wg.BlendFactor.OneMinusDstAlpha,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
    };

    public static Wg.BlendOperation ToBlendOperation(this BlendOperation operation) => operation switch
    {
        BlendOperation.Add => Wg.BlendOperation.Add,
        BlendOperation.Subtract => Wg.BlendOperation.Subtract,
        BlendOperation.ReverseSubtract => Wg.BlendOperation.ReverseSubtract,
        // WebGPU's Min/Max blend equations are not part of the core spec any GPU exposes through a
        // blend state; there are no callers, and translating them to Add would silently change a
        // draw's result rather than fail it.
        BlendOperation.Min or BlendOperation.Max => throw new NotSupportedException(
            $"WebGPU has no {operation} blend equation; express it in the shader instead."
        ),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };

    public static Wg.ColorWriteMask ToColorWriteMask(this ColorWriteMask mask) => (Wg.ColorWriteMask)(int)mask;

    /// <summary>
    /// The blend state WebGPU takes, as two components. The alpha half is spelled out from the
    /// abstraction's own alpha factors rather than reused from the colour half: the two are only
    /// equal for the premultiplied presets, and a caller setting them apart expects that.
    /// </summary>
    public static Wg.BlendState ToBlendState(this BlendStateDesc desc) => new()
    {
        Color = new Wg.BlendComponent
        {
            Operation = desc.ColorOperation.ToBlendOperation(),
            SrcFactor = desc.SourceColor.ToBlendFactor(),
            DstFactor = desc.DestinationColor.ToBlendFactor(),
        },
        Alpha = new Wg.BlendComponent
        {
            Operation = desc.AlphaOperation.ToBlendOperation(),
            SrcFactor = desc.SourceAlpha.ToBlendFactor(),
            DstFactor = desc.DestinationAlpha.ToBlendFactor(),
        },
    };

    public static Wg.AddressMode ToAddressMode(this TextureAddressMode mode) => mode switch
    {
        TextureAddressMode.Wrap => Wg.AddressMode.Repeat,
        TextureAddressMode.Clamp => Wg.AddressMode.ClampToEdge,
        TextureAddressMode.Mirror => Wg.AddressMode.MirrorRepeat,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static Wg.FilterMode ToFilterMode(this TextureFilter filter) => filter switch
    {
        TextureFilter.Point => Wg.FilterMode.Nearest,
        TextureFilter.Linear => Wg.FilterMode.Linear,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    public static Wg.StencilOperation ToStencilOperation(this StencilOperation operation) => operation switch
    {
        StencilOperation.Keep => Wg.StencilOperation.Keep,
        StencilOperation.Zero => Wg.StencilOperation.Zero,
        StencilOperation.Replace => Wg.StencilOperation.Replace,
        // The abstraction names these after GL's "increment and saturate at the type's maximum";
        // WebGPU spells the same behaviour Clamp.
        StencilOperation.IncrementSaturate => Wg.StencilOperation.IncrementClamp,
        StencilOperation.DecrementSaturate => Wg.StencilOperation.DecrementClamp,
        StencilOperation.Increment => Wg.StencilOperation.IncrementWrap,
        StencilOperation.Decrement => Wg.StencilOperation.DecrementWrap,
        StencilOperation.Invert => Wg.StencilOperation.Invert,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };
}
