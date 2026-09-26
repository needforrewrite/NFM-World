// LLM maintained.
//
// Translates NFMWorld.Graphics' abstraction enums into D3D11's.
//
// Kept separate from the device, the pipeline and the command buffer for the same reason both other
// backends keep their own mapping table: the two vocabularies differ in both directions, so every
// entry here is an explicit decision rather than a cast, and having them in one place is what stops
// a caller's translation from drifting from the command buffer's.
//
// Two of the tables are not one-to-one and are worth reading before trusting any single line:
// D3D11_COMPARISON_FUNC runs in the opposite numeric order to the abstraction's, and the stencil
// ops' "saturating" and "wrapping" names are swapped relative to GL's.
using NFMWorld.Shaders;
using TerraFX.Interop.DirectX;

namespace NFMWorld.Graphics.D3D11;

internal static class D3D11Mapping
{
    /// <summary>
    /// The texture format. Every one of these is a *typed* format: the resource is created in it and
    /// the view over it repeats it, which is only possible because no caller ever needs to alias the
    /// same texture as two formats.
    ///
    /// The exception is the depth-stencil pair, and it is not an exception in the API so much as in
    /// this file: D3D11 will not let a typed depth resource be created at all in a form a depth view
    /// accepts, so <c>Depth24Stencil8</c> maps to a *typeless* resource format here and to the
    /// concrete view format in <see cref="ToDepthStencilViewFormat"/>. The two must stay together -
    /// a view whose format is not in the resource's typeless family is rejected with E_INVALIDARG,
    /// which is a resource-creation failure rather than a draw-time one, but only if the two tables
    /// disagree.
    ///
    /// <c>Single</c> is R32_FLOAT rather than R32_TYPELESS for the same reason it is R32F on the GL
    /// backends: <c>Enums.Single</c> is documented as a colour-renderable, readable shadow format, and
    /// a single typed format gives that without a second table entry to keep in step.
    /// </summary>
    public static DXGI_FORMAT ToDxgiFormat(this TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 => DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
        TextureFormat.Bgra8 => DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
        TextureFormat.R8 => DXGI_FORMAT.DXGI_FORMAT_R8_UNORM,
        TextureFormat.Single => DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT,
        // Deliberately the float format rather than _UINT: Enums.Rgba32f documents the data as
        // "four 32-bit floats per texel, unnormalized", so a shader that samples it must see floats.
        // The two forms are the same size and layout, and only this one keeps the declared type.
        TextureFormat.Rgba32f => DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT,
        // Typeless - see the summary. The DXGI name is R24G8 (depth 24, stencil 8) even though D3D11
        // calls the matching view format D24_UNORM_S8_UINT; the naming mismatch is the SDK's, not a
        // transcription error here.
        TextureFormat.Depth24Stencil8 => DXGI_FORMAT.DXGI_FORMAT_R24G8_TYPELESS,
        // A BC1/2/3 block is 8 or 16 bytes covering 4x4 texels, so the "bytes per pixel" of any of
        // them is fractional and only the block size is meaningful - see BytesPerBlock.
        TextureFormat.Dxt1 => DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM,
        TextureFormat.Dxt3 => DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM,
        TextureFormat.Dxt5 => DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// The format a depth-stencil *view* over the resource takes. See
    /// <see cref="ToDxgiFormat"/> for why the resource's own format differs.
    /// </summary>
    public static DXGI_FORMAT ToDepthStencilViewFormat(this TextureFormat format) => format switch
    {
        TextureFormat.Depth24Stencil8 => DXGI_FORMAT.DXGI_FORMAT_D24_UNORM_S8_UINT,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>Whether the format belongs on the output-merger's depth-stencil slot rather than its render-target one.</summary>
    public static bool IsDepthFormat(this TextureFormat format) => format == TextureFormat.Depth24Stencil8;

    /// <summary>Whether the format is block-compressed, i.e. uploaded in 4x4 blocks rather than rows.</summary>
    public static bool IsBlockCompressed(this TextureFormat format) =>
        format is TextureFormat.Dxt1 or TextureFormat.Dxt3 or TextureFormat.Dxt5;

    /// <summary>
    /// The bytes one *row* of this format occupies at the given width - the number
    /// <c>ReadTexture</c> packs to and <c>UpdateTexture</c> unpacks from.
    ///
    /// A block-compressed row is measured in blocks, so its byte count is a quarter of the texel
    /// count times the block size. That is not a rounding convenience: 4x4 DXT1 is 8 bytes for 16
    /// texels, and no whole number of bytes per texel describes it.
    /// </summary>
    public static int RowBytes(this TextureFormat format, int width) => format switch
    {
        TextureFormat.Rgba8 or TextureFormat.Bgra8 => width * 4,
        TextureFormat.R8 => width,
        TextureFormat.Single => width * 4,
        TextureFormat.Rgba32f => width * 16,
        TextureFormat.Dxt1 => Math.Max(1, (width + 3) / 4) * 8,
        TextureFormat.Dxt3 or TextureFormat.Dxt5 => Math.Max(1, (width + 3) / 4) * 16,
        // Not an upload or readback size - a depth-stencil target has no colour bytes - but
        // ReadTexture rejects it before this is ever reached, and the resource's own stride is what
        // matters for the arithmetic that is done.
        TextureFormat.Depth24Stencil8 => width * 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static D3D11_USAGE ToUsage(this BufferUsage usage) => usage switch
    {
        // An immutable buffer is created with its contents and never written again, so DEFAULT is
        // the right storage: it is GPU-local and the driver is free to place it optimally.
        BufferUsage.Immutable => D3D11_USAGE.D3D11_USAGE_DEFAULT,
        // A dynamic buffer is written through Map(WRITE_DISCARD), which needs DYNAMIC + CPU write.
        // This is the one usage D3D11 offers for that, and it is the reason a dynamic buffer cannot
        // also be a render target or a shader resource - not a limitation this backend imposes.
        BufferUsage.Dynamic => D3D11_USAGE.D3D11_USAGE_DYNAMIC,
        _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null),
    };

    public static DXGI_FORMAT ToDxgiFormat(this IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => DXGI_FORMAT.DXGI_FORMAT_R16_UINT,
        IndexFormat.UInt32 => DXGI_FORMAT.DXGI_FORMAT_R32_UINT,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static D3D_PRIMITIVE_TOPOLOGY ToTopology(this PrimitiveTopology topology) => topology switch
    {
        // The D3D11_ aliases rather than the D3D_PRIMITIVE_ ones: they are the same values, and the
        // 11-suffixed names are what the API's own header documents for an 11.0 device.
        PrimitiveTopology.TriangleList => D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST,
        PrimitiveTopology.TriangleStrip => D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP,
        PrimitiveTopology.LineList => D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_LINELIST,
        PrimitiveTopology.LineStrip => D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP,
        PrimitiveTopology.PointList => D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_POINTLIST,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    public static D3D11_BLEND ToBlend(this BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => D3D11_BLEND.D3D11_BLEND_ZERO,
        BlendFactor.One => D3D11_BLEND.D3D11_BLEND_ONE,
        BlendFactor.SourceAlpha => D3D11_BLEND.D3D11_BLEND_SRC_ALPHA,
        BlendFactor.InverseSourceAlpha => D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
        BlendFactor.DestinationAlpha => D3D11_BLEND.D3D11_BLEND_DEST_ALPHA,
        BlendFactor.InverseDestinationAlpha => D3D11_BLEND.D3D11_BLEND_INV_DEST_ALPHA,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
    };

    public static D3D11_BLEND_OP ToBlendOp(this BlendOperation op) => op switch
    {
        BlendOperation.Add => D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
        // D3D11 names the operand order the same way OpenGL's glBlendEquation does, so there is no
        // swap here the way there would be against the old D3D9 SRC/DEST naming.
        BlendOperation.Subtract => D3D11_BLEND_OP.D3D11_BLEND_OP_SUBTRACT,
        BlendOperation.ReverseSubtract => D3D11_BLEND_OP.D3D11_BLEND_OP_REV_SUBTRACT,
        BlendOperation.Min => D3D11_BLEND_OP.D3D11_BLEND_OP_MIN,
        BlendOperation.Max => D3D11_BLEND_OP.D3D11_BLEND_OP_MAX,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    /// <summary>
    /// The comparison function, shared by the depth test and the three stencil tests.
    ///
    /// D3D11 orders these by "how permissive" - NEVER is 1 and ALWAYS is 8 - while the abstraction
    /// orders them alphabetically. So this is one of the tables where nothing can be inferred from
    /// position and every row is load-bearing.
    /// </summary>
    public static D3D11_COMPARISON_FUNC ToComparison(this CompareFunction fn) => fn switch
    {
        CompareFunction.Never => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
        CompareFunction.Less => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_LESS,
        CompareFunction.Equal => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_EQUAL,
        CompareFunction.LessEqual => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_LESS_EQUAL,
        CompareFunction.Greater => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_GREATER,
        CompareFunction.NotEqual => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NOT_EQUAL,
        CompareFunction.GreaterEqual => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_GREATER_EQUAL,
        CompareFunction.Always => D3D11_COMPARISON_FUNC.D3D11_COMPARISON_ALWAYS,
        _ => throw new ArgumentOutOfRangeException(nameof(fn), fn, null),
    };

    /// <summary>
    /// The stencil operation.
    ///
    /// The saturating/wrapping pair is the trap, and it is the mirror image of the one the GL
    /// backends document: <c>StencilOperation.Increment</c> is the *wrapping* one and maps to
    /// <c>INCR</c>, while <c>IncrementSaturate</c> maps to <c>INCR_SAT</c>. Reading "Increment"
    /// as the clamped op - which GLES's <c>GL_INCR</c> naming invites - turns NanoVG's two-sided
    /// stencil fill into one that pins at 255 and never unwinds.
    /// </summary>
    public static D3D11_STENCIL_OP ToStencilOp(this StencilOperation op) => op switch
    {
        StencilOperation.Keep => D3D11_STENCIL_OP.D3D11_STENCIL_OP_KEEP,
        StencilOperation.Zero => D3D11_STENCIL_OP.D3D11_STENCIL_OP_ZERO,
        StencilOperation.Replace => D3D11_STENCIL_OP.D3D11_STENCIL_OP_REPLACE,
        StencilOperation.Increment => D3D11_STENCIL_OP.D3D11_STENCIL_OP_INCR,
        StencilOperation.Decrement => D3D11_STENCIL_OP.D3D11_STENCIL_OP_DECR,
        StencilOperation.IncrementSaturate => D3D11_STENCIL_OP.D3D11_STENCIL_OP_INCR_SAT,
        StencilOperation.DecrementSaturate => D3D11_STENCIL_OP.D3D11_STENCIL_OP_DECR_SAT,
        StencilOperation.Invert => D3D11_STENCIL_OP.D3D11_STENCIL_OP_INVERT,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static D3D11_CULL_MODE ToCullMode(this CullMode mode) => mode switch
    {
        CullMode.None => D3D11_CULL_MODE.D3D11_CULL_NONE,
        CullMode.Front => D3D11_CULL_MODE.D3D11_CULL_FRONT,
        CullMode.Back => D3D11_CULL_MODE.D3D11_CULL_BACK,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static D3D11_FILL_MODE ToFillMode(this FillMode mode) => mode switch
    {
        FillMode.Solid => D3D11_FILL_MODE.D3D11_FILL_SOLID,
        FillMode.Wireframe => D3D11_FILL_MODE.D3D11_FILL_WIREFRAME,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static byte ToWriteMask(this ColorWriteMask mask) => (byte)(
        (mask.HasFlag(ColorWriteMask.Red) ? D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_RED : 0) |
        (mask.HasFlag(ColorWriteMask.Green) ? D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_GREEN : 0) |
        (mask.HasFlag(ColorWriteMask.Blue) ? D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_BLUE : 0) |
        (mask.HasFlag(ColorWriteMask.Alpha) ? D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALPHA : 0));

    /// <summary>
    /// The sampler's filter.
    ///
    /// Every combination has minification, magnification and mip selection set together, and the
    /// interesting half is the mip one. A texture with a single level sampled with a mipmapping
    /// filter reads as black on D3D11 exactly as it does on GL, so where GL's backend resolves this
    /// at draw time from the bound texture's completeness, this one resolves it at pipeline
    /// creation from whether the format is one that ever carries a chain. No caller in this app
    /// creates a mipmapped texture, so the answer is <c>POINT</c>/<c>LINEAR</c> for mips in every
    /// case that exists; the choice is written out rather than hardcoded so that a future
    /// <c>TextureDesc.MipMapped</c> has somewhere to land.
    /// </summary>
    public static D3D11_FILTER ToFilter(this TextureFilter filter) => filter switch
    {
        TextureFilter.Point => D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_POINT,
        TextureFilter.Linear => D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    public static D3D11_TEXTURE_ADDRESS_MODE ToAddressMode(this TextureAddressMode mode) => mode switch
    {
        TextureAddressMode.Wrap => D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_WRAP,
        TextureAddressMode.Clamp => D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
        // D3D11's MIRROR is GL's MIRRORED_REPEAT and GL's MIRROR_CLAMP_TO_LONG, i.e. the repeating
        // one; MIRROR_ONCE is the clamp-to-edge variant. The abstraction means the repeating one.
        TextureAddressMode.Mirror => D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_MIRROR,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>
    /// A vertex attribute's format.
    ///
    /// The normalized flag is folded into the format name rather than carried beside it, which is
    /// what D3D11's <c>DXGI_FORMAT</c> is: <c>R8G8B8A8_UNORM</c> is the normalized four-byte colour
    /// and <c>R8G8B8A8_UINT</c> the raw one. Since the abstraction's
    /// <see cref="VertexAttributeFormat"/> only has the normalized forms, every entry that could be
    /// either lands on the UNORM/SNORM one.
    /// </summary>
    public static DXGI_FORMAT ToDxgiFormat(this VertexAttributeFormat format) => format switch
    {
        VertexAttributeFormat.Float1 => DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT,
        VertexAttributeFormat.Float2 => DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
        VertexAttributeFormat.Float3 => DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT,
        VertexAttributeFormat.Float4 => DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT,
        // XNA's packed colour, and the one entry here that is not a float: four unsigned bytes the
        // hardware scales to 0..1 on the way to the shader.
        VertexAttributeFormat.Byte4Normalized => DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
        // Signed 16-bit lanes scaled to [-1, 1], matching the GL backends' Short4Normalized.
        VertexAttributeFormat.Short4Normalized => DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_SNORM,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// The abstraction's 0..255 stencil masks widened to a byte. The abstraction models them as
    /// <c>int</c> (XNA did), and its callers pass 0xff or <c>int.MaxValue</c> meaning "all bits" -
    /// D3D11's field is eight bits wide, so the widening is a truncation and both cases land on 0xff.
    /// </summary>
    public static byte ToStencilMask(this int mask) => (byte)mask;

    /// <summary>
    /// The GLSL type name for a uniform, used only by the smoke test's reflection dump - the same
    /// helper the GL backends carry, spelled in D3D11's vocabulary so a dump from either can be read
    /// side by side.
    /// </summary>
    public static string Describe(this UniformType type) => type switch
    {
        UniformType.Float => "float",
        UniformType.Vector2 => "float2",
        UniformType.Vector3 => "float3",
        UniformType.Vector4 => "float4",
        UniformType.Matrix4x4 => "float4x4",
        UniformType.Int => "int",
        _ => "?",
    };
}
