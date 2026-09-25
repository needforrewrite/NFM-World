// LLM maintained.
//
// Translates NFMWorld.Graphics' abstraction enums into Silk.NET.OpenGL's GL enums.
//
// Kept separate from the device and command buffer for the same reason sokol's backend keeps its
// own mapping table: the two vocabularies differ in both directions, so every entry here is an
// explicit decision rather than a cast, and having them in one place is what stops a caller's
// translation from drifting from the command buffer's.
using Silk.NET.OpenGL;
using NFMWorld.Shaders;

namespace NFMWorld.Graphics.DesktopGL;

internal static class GlMapping
{
    /// <summary>
    /// The internal format a texture of this type is allocated with.
    ///
    /// The choice is not arbitrary for two of them:
    ///
    /// - <c>Rgba8</c> uses the sized <c>RGBA8</c> rather than the unsized <c>RGBA</c>, because an
    ///   unsized internal format is only legal for a texture with no immutable storage, and the
    ///   POC wants the format the driver actually allocated to be the one the abstraction named.
    /// - <c>Bgra8</c> maps to <c>RGBA8</c> storage with a <c>BGRA</c> upload format rather than to
    ///   a sized BGRA internal format, because <c>BGRA8</c> is not a core ES 3.0 sized format -
    ///   the channel order is a property of the data being uploaded, which
    ///   <see cref="GlMapping.ToUploadFormat"/> supplies. That split is what keeps a BMP-style
    ///   BGRA upload working without a CPU swizzle.
    ///
    /// <c>Dxt1/3/5</c> are genuinely unsupported: ES 3.0 core has no compressed texture formats at
    /// all (they arrive via <c>GL_EXT_texture_compression_*</c> or <c>GL_OES_compressed_ETC1_RGB8</c>),
    /// so a compressed upload is rejected loudly rather than silently uploaded as garbage.
    /// </summary>
    public static InternalFormat ToInternalFormat(this TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 => InternalFormat.Rgba8,
        TextureFormat.Bgra8 => InternalFormat.Rgba8,
        TextureFormat.R8 => InternalFormat.R8,
        // The shadow-cascade format. A 32-bit float colour texture is what the shadow pass
        // renders depth into, and R32F is core ES 3.0.
        TextureFormat.Single => InternalFormat.R32f,
        TextureFormat.Depth24Stencil8 => InternalFormat.Depth24Stencil8,
        TextureFormat.Dxt1 or TextureFormat.Dxt3 or TextureFormat.Dxt5 =>
            throw new NotSupportedException(
                $"'{format}' is a block-compressed format, and ES 3.0 core has no compressed " +
                "texture formats (they are extensions). This POC does not translate it."),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// The format the *source bytes* are in, for <c>glTexImage2D</c>/<c>glTexSubImage2D</c>/
    /// <c>glReadPixels</c>. Paired with <see cref="ToInternalFormat"/>, which describes the
    /// storage - the two are independent in GLES.
    /// </summary>
    public static PixelFormat ToUploadFormat(this TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 => PixelFormat.Rgba,
        // BGRA byte order on the way in; RGBA8 storage. See ToInternalFormat.
        TextureFormat.Bgra8 => PixelFormat.Bgra,
        TextureFormat.R8 => PixelFormat.Red,
        TextureFormat.Single => PixelFormat.Red,
        TextureFormat.Depth24Stencil8 => PixelFormat.DepthStencil,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>The upload element type matching <see cref="ToUploadFormat"/>.</summary>
    public static PixelType ToUploadType(this TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 or TextureFormat.Bgra8 or TextureFormat.R8 => PixelType.UnsignedByte,
        TextureFormat.Single => PixelType.Float,
        TextureFormat.Depth24Stencil8 => PixelType.UnsignedInt248,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Whether the format has a depth aspect, i.e. belongs on a framebuffer's
    /// <c>DEPTH_STENCIL_ATTACHMENT</c> point rather than its <c>COLOR_ATTACHMENT0</c>.
    /// </summary>
    public static bool IsDepthFormat(this TextureFormat format) =>
        format == TextureFormat.Depth24Stencil8;

    public static int BytesPerPixel(this TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 or TextureFormat.Bgra8 => 4,
        TextureFormat.R8 => 1,
        TextureFormat.Single => 4,
        // Not an upload size - a depth-stencil target has no colour bytes to read back - but
        // ReadTexture rejects it before this is ever used for arithmetic.
        TextureFormat.Depth24Stencil8 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static BufferTargetARB ToTarget(this BufferKind kind) => kind switch
    {
        BufferKind.Vertex => BufferTargetARB.ArrayBuffer,
        BufferKind.Index => BufferTargetARB.ElementArrayBuffer,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// The usage hint for a buffer. GLES has no immutable-storage requirement, so both usages map
    /// to a plain static/dynamic draw hint - the difference between them is that
    /// <see cref="BufferUsage.Dynamic"/> is the one <c>UpdateBuffer</c> accepts, which the
    /// command buffer enforces rather than the driver.
    /// </summary>
    public static BufferUsageARB ToUsage(this BufferUsage usage) => usage switch
    {
        BufferUsage.Immutable => BufferUsageARB.StaticDraw,
        BufferUsage.Dynamic => BufferUsageARB.DynamicDraw,
        _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null),
    };

    public static DrawElementsType ToIndexType(this IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => DrawElementsType.UnsignedShort,
        IndexFormat.UInt32 => DrawElementsType.UnsignedInt,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static PrimitiveType ToPrimitiveType(this PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => PrimitiveType.Triangles,
        PrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
        PrimitiveTopology.LineList => PrimitiveType.Lines,
        PrimitiveTopology.LineStrip => PrimitiveType.LineStrip,
        PrimitiveTopology.PointList => PrimitiveType.Points,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    public static GLEnum ToMinFilter(this TextureFilter filter, bool mipMapped) => (filter, mipMapped) switch
    {
        (TextureFilter.Point, false) => GLEnum.Nearest,
        (TextureFilter.Linear, false) => GLEnum.Linear,
        (TextureFilter.Point, true) => GLEnum.NearestMipmapNearest,
        (TextureFilter.Linear, true) => GLEnum.LinearMipmapLinear,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    public static GLEnum ToMagFilter(this TextureFilter filter) => filter switch
    {
        TextureFilter.Point => GLEnum.Nearest,
        TextureFilter.Linear => GLEnum.Linear,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    public static GLEnum ToWrap(this TextureAddressMode mode) => mode switch
    {
        TextureAddressMode.Wrap => GLEnum.Repeat,
        TextureAddressMode.Clamp => GLEnum.ClampToEdge,
        TextureAddressMode.Mirror => GLEnum.MirroredRepeat,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static BlendingFactor ToBlendFactor(this BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => BlendingFactor.Zero,
        BlendFactor.One => BlendingFactor.One,
        BlendFactor.SourceAlpha => BlendingFactor.SrcAlpha,
        BlendFactor.InverseSourceAlpha => BlendingFactor.OneMinusSrcAlpha,
        BlendFactor.DestinationAlpha => BlendingFactor.DstAlpha,
        BlendFactor.InverseDestinationAlpha => BlendingFactor.OneMinusDstAlpha,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
    };

    public static BlendEquationModeEXT ToBlendEquation(this BlendOperation op) => op switch
    {
        BlendOperation.Add => BlendEquationModeEXT.FuncAdd,
        BlendOperation.Subtract => BlendEquationModeEXT.FuncSubtract,
        BlendOperation.ReverseSubtract => BlendEquationModeEXT.FuncReverseSubtract,
        // MIN and MAX are core ES 3.0 blending equations, so they are not the extension-gated
        // case desktop GL's GL_EXT_blend_minmax would make them.
        BlendOperation.Min => BlendEquationModeEXT.Min,
        BlendOperation.Max => BlendEquationModeEXT.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    /// <summary>
    /// The depth comparison. GL's <c>glDepthFunc</c> takes a <c>GLenum</c> and its stencil functions
    /// take a <c>StencilFunction</c>; the two enums have the same members, so one mapping serves
    /// both and the call site casts to whichever it needs.
    /// </summary>
    public static DepthFunction ToDepthFunction(this CompareFunction fn) => fn switch
    {
        CompareFunction.Always => DepthFunction.Always,
        CompareFunction.Never => DepthFunction.Never,
        CompareFunction.Less => DepthFunction.Less,
        CompareFunction.LessEqual => DepthFunction.Lequal,
        CompareFunction.Equal => DepthFunction.Equal,
        CompareFunction.NotEqual => DepthFunction.Notequal,
        CompareFunction.GreaterEqual => DepthFunction.Gequal,
        CompareFunction.Greater => DepthFunction.Greater,
        _ => throw new ArgumentOutOfRangeException(nameof(fn), fn, null),
    };

    /// <summary>The same comparison as a <c>StencilFunction</c>, for <c>glStencilFuncSeparate</c>.</summary>
    public static StencilFunction ToStencilFunction(this CompareFunction fn) => fn switch
    {
        CompareFunction.Always => StencilFunction.Always,
        CompareFunction.Never => StencilFunction.Never,
        CompareFunction.Less => StencilFunction.Less,
        CompareFunction.LessEqual => StencilFunction.Lequal,
        CompareFunction.Equal => StencilFunction.Equal,
        CompareFunction.NotEqual => StencilFunction.Notequal,
        CompareFunction.GreaterEqual => StencilFunction.Gequal,
        CompareFunction.Greater => StencilFunction.Greater,
        _ => throw new ArgumentOutOfRangeException(nameof(fn), fn, null),
    };

    /// <summary>
    /// The stencil op. The saturating variants are worth spelling out: GLES names them
    /// <c>INCR</c>/<c>DECR</c> (clamped) while the wrapping ones are <c>INCR_WRAP</c>/<c>DECR_WRAP</c>,
    /// which is the opposite of D3D11's <c>INCR_SAT</c>/<c>INCR</c> spelling.
    /// </summary>
    public static StencilOp ToStencilOp(this StencilOperation op) => op switch
    {
        StencilOperation.Keep => StencilOp.Keep,
        StencilOperation.Zero => StencilOp.Zero,
        StencilOperation.Replace => StencilOp.Replace,
        StencilOperation.Increment => StencilOp.IncrWrap,
        StencilOperation.Decrement => StencilOp.DecrWrap,
        StencilOperation.IncrementSaturate => StencilOp.Incr,
        StencilOperation.DecrementSaturate => StencilOp.Decr,
        StencilOperation.Invert => StencilOp.Invert,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static GLEnum ToCullFaceMode(this CullMode mode) => mode switch
    {
        CullMode.Front => GLEnum.Front,
        CullMode.Back => GLEnum.Back,
        CullMode.None => throw new InvalidOperationException("CullMode.None is expressed by disabling GL_CULL_FACE, not by a face mode."),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static PolygonMode ToPolygonMode(this FillMode mode) => mode switch
    {
        FillMode.Solid => PolygonMode.Fill,
        // ES 3.0 core has no polygon mode at all - GL_LINE fill is one of the few desktop-only
        // features with no ES equivalent. Rejected loudly so a wireframe request cannot silently
        // render solid and look like a scene bug.
        FillMode.Wireframe => throw new NotSupportedException(
            "Wireframe fill has no ES 3.0 equivalent: glPolygonMode is desktop-GL only."),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>The four per-channel write enables, in GL's bit order.</summary>
    public static bool[] ToColorMask(this ColorWriteMask mask) =>
    [
        mask.HasFlag(ColorWriteMask.Red),
        mask.HasFlag(ColorWriteMask.Green),
        mask.HasFlag(ColorWriteMask.Blue),
        mask.HasFlag(ColorWriteMask.Alpha),
    ];

    /// <summary>
    /// A vertex attribute's component count, component type and normalized flag.
    ///
    /// <c>Byte4Normalized</c> is XNA's packed colour: four unsigned bytes read back as 0..1, which
    /// is exactly GL's <c>GL_UNSIGNED_BYTE</c> with <c>normalized = true</c>. The rest are plain
    /// floats, where the vector size alone decides the component count.
    ///
    /// <c>VertexAttribPointerType</c> rather than <c>VertexAttribType</c>: the two enums carry the
    /// same members, but only the former is what <c>glVertexAttribPointer</c> takes, and that is the
    /// only attribute-format call this backend can make on an ES 3.0 context - see GlPipelineState.
    /// </summary>
    public static (int Components, VertexAttribPointerType Type, bool Normalized) ToAttribFormat(this VertexAttributeFormat format) => format switch
    {
        VertexAttributeFormat.Float1 => (1, VertexAttribPointerType.Float, false),
        VertexAttributeFormat.Float2 => (2, VertexAttribPointerType.Float, false),
        VertexAttributeFormat.Float3 => (3, VertexAttribPointerType.Float, false),
        VertexAttributeFormat.Float4 => (4, VertexAttribPointerType.Float, false),
        VertexAttributeFormat.Byte4Normalized => (4, VertexAttribPointerType.UnsignedByte, true),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// The GLSL type name for a uniform, used only by the smoke test's reflection dump. The
    /// abstraction's <see cref="NFMWorld.Shaders.UniformType"/> is a coarse classification (matrix
    /// vs vector vs scalar), which is all a byte-offset upload needs.
    /// </summary>
    public static string Describe(this NFMWorld.Shaders.UniformType type) => type switch
    {
        NFMWorld.Shaders.UniformType.Float => "float",
        NFMWorld.Shaders.UniformType.Vector2 => "vec2",
        NFMWorld.Shaders.UniformType.Vector3 => "vec3",
        NFMWorld.Shaders.UniformType.Vector4 => "vec4",
        NFMWorld.Shaders.UniformType.Matrix4x4 => "mat4",
        NFMWorld.Shaders.UniformType.Int => "int",
        _ => "?",
    };
}
