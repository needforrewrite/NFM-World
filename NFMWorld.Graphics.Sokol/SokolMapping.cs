using NFMWorld.Shaders;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// Translates NFMWorld.Graphics abstraction enums into SharpSokol's <c>sg_*</c> enums.
///
/// The SharpSokol binding keeps sokol's C names verbatim (<c>SG_PIXELFORMAT_RGBA8</c>), so
/// every mapping here is an explicit translation rather than an identity cast - which is the
/// point, since the two vocabularies differ in both directions (sokol has no notion of
/// "Bgra8 render target" vs "Bgra8 texture", and the abstraction has no notion of sokol's
/// internal formats).
/// </summary>
internal static class SokolMapping
{
    /// <summary>
    /// Maps a texture format to sokol's pixel format, which encodes <em>both</em> the channel
    /// layout and the intended use. <paramref name="renderTargetable"/> selects the depth form
    /// for depth formats and is otherwise ignored - sokol infers attachment capability from the
    /// image's <c>usage</c> flags at creation, not from the pixel format.
    /// </summary>
    public static sg_pixel_format ToNative(this TextureFormat format, bool renderTargetable = false) => format switch
    {
        TextureFormat.Rgba8 => sg_pixel_format.SG_PIXELFORMAT_RGBA8,
        TextureFormat.Bgra8 => sg_pixel_format.SG_PIXELFORMAT_BGRA8,
        TextureFormat.R8 => sg_pixel_format.SG_PIXELFORMAT_R8,
        // Single is the shadow-cascade depth format. sokol's SG_PIXELFORMAT_DEPTH is the
        // 32-bit float depth form, and SG_PIXELFORMAT_R32F is the plain float colour form -
        // R32F was used before sokol_gfx gained a dedicated DEPTH format, so either works,
        // but DEPTH is the one the backends know to attach as a depth-stencil view.
        TextureFormat.Single => renderTargetable
            ? sg_pixel_format.SG_PIXELFORMAT_DEPTH
            : sg_pixel_format.SG_PIXELFORMAT_R32F,
        TextureFormat.Depth24Stencil8 => sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,
        // sokol's BCn names line up with DXTn (BC1=DXT1, BC2=DXT3, BC3=DXT5).
        TextureFormat.Dxt1 => sg_pixel_format.SG_PIXELFORMAT_BC1_RGBA,
        TextureFormat.Dxt3 => sg_pixel_format.SG_PIXELFORMAT_BC2_RGBA,
        TextureFormat.Dxt5 => sg_pixel_format.SG_PIXELFORMAT_BC3_RGBA,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static sg_index_type ToNative(this IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => sg_index_type.SG_INDEXTYPE_UINT16,
        IndexFormat.UInt32 => sg_index_type.SG_INDEXTYPE_UINT32,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static sg_primitive_type ToNative(this PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
        PrimitiveTopology.TriangleStrip => sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLE_STRIP,
        PrimitiveTopology.LineList => sg_primitive_type.SG_PRIMITIVETYPE_LINES,
        PrimitiveTopology.LineStrip => sg_primitive_type.SG_PRIMITIVETYPE_LINE_STRIP,
        PrimitiveTopology.PointList => sg_primitive_type.SG_PRIMITIVETYPE_POINTS,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    public static sg_filter ToNative(this TextureFilter filter) => filter switch
    {
        TextureFilter.Point => sg_filter.SG_FILTER_NEAREST,
        TextureFilter.Linear => sg_filter.SG_FILTER_LINEAR,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    public static sg_wrap ToNative(this TextureAddressMode mode) => mode switch
    {
        TextureAddressMode.Wrap => sg_wrap.SG_WRAP_REPEAT,
        TextureAddressMode.Clamp => sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
        TextureAddressMode.Mirror => sg_wrap.SG_WRAP_MIRRORED_REPEAT,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static sg_blend_factor ToNative(this BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => sg_blend_factor.SG_BLENDFACTOR_ZERO,
        BlendFactor.One => sg_blend_factor.SG_BLENDFACTOR_ONE,
        BlendFactor.SourceAlpha => sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
        BlendFactor.InverseSourceAlpha => sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
        BlendFactor.DestinationAlpha => sg_blend_factor.SG_BLENDFACTOR_DST_ALPHA,
        BlendFactor.InverseDestinationAlpha => sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_DST_ALPHA,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
    };

    public static sg_blend_op ToNative(this BlendOperation op) => op switch
    {
        BlendOperation.Add => sg_blend_op.SG_BLENDOP_ADD,
        BlendOperation.Subtract => sg_blend_op.SG_BLENDOP_SUBTRACT,
        BlendOperation.ReverseSubtract => sg_blend_op.SG_BLENDOP_REVERSE_SUBTRACT,
        BlendOperation.Min => sg_blend_op.SG_BLENDOP_MIN,
        BlendOperation.Max => sg_blend_op.SG_BLENDOP_MAX,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static sg_compare_func ToNative(this CompareFunction fn) => fn switch
    {
        CompareFunction.Always => sg_compare_func.SG_COMPAREFUNC_ALWAYS,
        CompareFunction.Never => sg_compare_func.SG_COMPAREFUNC_NEVER,
        CompareFunction.Less => sg_compare_func.SG_COMPAREFUNC_LESS,
        CompareFunction.LessEqual => sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
        CompareFunction.Equal => sg_compare_func.SG_COMPAREFUNC_EQUAL,
        CompareFunction.NotEqual => sg_compare_func.SG_COMPAREFUNC_NOT_EQUAL,
        CompareFunction.GreaterEqual => sg_compare_func.SG_COMPAREFUNC_GREATER_EQUAL,
        CompareFunction.Greater => sg_compare_func.SG_COMPAREFUNC_GREATER,
        _ => throw new ArgumentOutOfRangeException(nameof(fn), fn, null),
    };

    public static sg_stencil_op ToNative(this StencilOperation op) => op switch
    {
        StencilOperation.Keep => sg_stencil_op.SG_STENCILOP_KEEP,
        StencilOperation.Zero => sg_stencil_op.SG_STENCILOP_ZERO,
        StencilOperation.Replace => sg_stencil_op.SG_STENCILOP_REPLACE,
        StencilOperation.Increment => sg_stencil_op.SG_STENCILOP_INCR_WRAP,
        StencilOperation.Decrement => sg_stencil_op.SG_STENCILOP_DECR_WRAP,
        StencilOperation.IncrementSaturate => sg_stencil_op.SG_STENCILOP_INCR_CLAMP,
        StencilOperation.DecrementSaturate => sg_stencil_op.SG_STENCILOP_DECR_CLAMP,
        StencilOperation.Invert => sg_stencil_op.SG_STENCILOP_INVERT,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static sg_cull_mode ToNative(this CullMode mode) => mode switch
    {
        CullMode.None => sg_cull_mode.SG_CULLMODE_NONE,
        CullMode.Front => sg_cull_mode.SG_CULLMODE_FRONT,
        CullMode.Back => sg_cull_mode.SG_CULLMODE_BACK,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static sg_color_mask ToNative(this ColorWriteMask mask)
    {
        // sokol's masks are a 4-bit value packed into an enum whose NONE member is 0x10,
        // so building the bit pattern directly (rather than OR-ing members) is the only
        // correct approach - OR-ing SG_COLORMASK_R|SG_COLORMASK_G happens to work, but
        // SG_COLORMASK_NONE|SG_COLORMASK_R would not.
        var value = 0;
        if (mask.HasFlag(ColorWriteMask.Red)) value |= 0x1;
        if (mask.HasFlag(ColorWriteMask.Green)) value |= 0x2;
        if (mask.HasFlag(ColorWriteMask.Blue)) value |= 0x4;
        if (mask.HasFlag(ColorWriteMask.Alpha)) value |= 0x8;
        return (sg_color_mask)(value == 0 ? 0x10 : value);
    }

    public static sg_vertex_format ToNative(this VertexAttributeFormat format) => format switch
    {
        VertexAttributeFormat.Float1 => sg_vertex_format.SG_VERTEXFORMAT_FLOAT,
        VertexAttributeFormat.Float2 => sg_vertex_format.SG_VERTEXFORMAT_FLOAT2,
        VertexAttributeFormat.Float3 => sg_vertex_format.SG_VERTEXFORMAT_FLOAT3,
        VertexAttributeFormat.Float4 => sg_vertex_format.SG_VERTEXFORMAT_FLOAT4,
        // XNA/FNA's Color format is a packed 4-byte BGRA value normalized to 0..1 on read -
        // that is exactly sokol's UBYTE4N, not BYTE4N (which is signed).
        VertexAttributeFormat.Byte4Normalized => sg_vertex_format.SG_VERTEXFORMAT_UBYTE4N,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Strips any trailing digits from an HLSL semantic, yielding the bare name D3D11's
    /// <c>D3D11_INPUT_ELEMENT_DESC.SemanticName</c> takes (e.g. "TEXCOORD").
    ///
    /// The index is NOT parsed back out of the string: it lives in
    /// <see cref="VertexAttributeDesc.Slot"/> (FNA3D's <c>UsageIndex</c>), and callers pass the
    /// semantic bare - "TEXCOORD" with Slot 3, not "TEXCOORD3". Deriving the index from the name
    /// instead would collapse every TEXCOORD to index 0, so an instanced layout would declare six
    /// identical TEXCOORD0 elements and D3D11 would reject the input layout against a shader whose
    /// signature is actually TEXCOORD3..8.
    /// </summary>
    public static string SemanticName(string semantic)
    {
        var end = semantic.Length;
        while (end > 0 && char.IsAsciiDigit(semantic[end - 1])) end--;
        return semantic[..end];
    }
}
