using NFMWorld.Shaders;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>Translates NFMWorld.Graphics abstraction enums to/from FNA3D's native enums.</summary>
internal static class Mapping
{
    public static FNA3D_SurfaceFormat ToNative(TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 => FNA3D_SurfaceFormat.Color,
        TextureFormat.Bgra8 => FNA3D_SurfaceFormat.ColorBgraEXT,
        TextureFormat.R8 => FNA3D_SurfaceFormat.Alpha8,
        TextureFormat.Depth24Stencil8 => throw new ArgumentException("Depth24Stencil8 is a depth format, not a texture surface format; use ToNativeDepthFormat instead.", nameof(format)),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static FNA3D_DepthFormat ToNativeDepthFormat(TextureFormat format) => format switch
    {
        TextureFormat.Depth24Stencil8 => FNA3D_DepthFormat.Depth24Stencil8,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Not a depth format."),
    };

    public static FNA3D_BufferUsage ToNativeBufferUsage(BufferUsage usage) => usage switch
    {
        BufferUsage.Immutable => FNA3D_BufferUsage.WriteOnly,
        BufferUsage.Dynamic => FNA3D_BufferUsage.WriteOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null),
    };

    public static byte ToDynamicFlag(BufferUsage usage) => usage == BufferUsage.Dynamic ? (byte)1 : (byte)0;

    public static FNA3D_IndexElementSize ToNative(IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => FNA3D_IndexElementSize.SixteenBits,
        IndexFormat.UInt32 => FNA3D_IndexElementSize.ThirtyTwoBits,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static FNA3D_PrimitiveType ToNative(PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => FNA3D_PrimitiveType.TriangleList,
        PrimitiveTopology.TriangleStrip => FNA3D_PrimitiveType.TriangleStrip,
        PrimitiveTopology.LineList => FNA3D_PrimitiveType.LineList,
        PrimitiveTopology.LineStrip => FNA3D_PrimitiveType.LineStrip,
        PrimitiveTopology.PointList => FNA3D_PrimitiveType.PointListEXT,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    public static FNA3D_ClearOptions ToNative(ClearOptions options)
    {
        FNA3D_ClearOptions result = 0;
        if (options.HasFlag(ClearOptions.Color)) result |= FNA3D_ClearOptions.Target;
        if (options.HasFlag(ClearOptions.Depth)) result |= FNA3D_ClearOptions.DepthBuffer;
        if (options.HasFlag(ClearOptions.Stencil)) result |= FNA3D_ClearOptions.Stencil;
        return result;
    }

    public static FNA3D_TextureFilter ToNative(TextureFilter filter) => filter switch
    {
        TextureFilter.Point => FNA3D_TextureFilter.Point,
        TextureFilter.Linear => FNA3D_TextureFilter.Linear,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    public static FNA3D_TextureAddressMode ToNative(TextureAddressMode mode) => mode switch
    {
        TextureAddressMode.Wrap => FNA3D_TextureAddressMode.Wrap,
        TextureAddressMode.Clamp => FNA3D_TextureAddressMode.Clamp,
        TextureAddressMode.Mirror => FNA3D_TextureAddressMode.Mirror,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static FNA3D_Blend ToNative(BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => FNA3D_Blend.Zero,
        BlendFactor.One => FNA3D_Blend.One,
        BlendFactor.SourceAlpha => FNA3D_Blend.SourceAlpha,
        BlendFactor.InverseSourceAlpha => FNA3D_Blend.InverseSourceAlpha,
        BlendFactor.DestinationAlpha => FNA3D_Blend.DestinationAlpha,
        BlendFactor.InverseDestinationAlpha => FNA3D_Blend.InverseDestinationAlpha,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
    };

    public static FNA3D_BlendFunction ToNative(BlendOperation op) => op switch
    {
        BlendOperation.Add => FNA3D_BlendFunction.Add,
        BlendOperation.Subtract => FNA3D_BlendFunction.Subtract,
        BlendOperation.ReverseSubtract => FNA3D_BlendFunction.ReverseSubtract,
        BlendOperation.Min => FNA3D_BlendFunction.Min,
        BlendOperation.Max => FNA3D_BlendFunction.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static FNA3D_CompareFunction ToNative(CompareFunction fn) => fn switch
    {
        CompareFunction.Always => FNA3D_CompareFunction.Always,
        CompareFunction.Never => FNA3D_CompareFunction.Never,
        CompareFunction.Less => FNA3D_CompareFunction.Less,
        CompareFunction.LessEqual => FNA3D_CompareFunction.LessEqual,
        CompareFunction.Equal => FNA3D_CompareFunction.Equal,
        CompareFunction.NotEqual => FNA3D_CompareFunction.NotEqual,
        CompareFunction.GreaterEqual => FNA3D_CompareFunction.GreaterEqual,
        CompareFunction.Greater => FNA3D_CompareFunction.Greater,
        _ => throw new ArgumentOutOfRangeException(nameof(fn), fn, null),
    };

    public static FNA3D_CullMode ToNative(CullMode mode) => mode switch
    {
        CullMode.None => FNA3D_CullMode.None,
        CullMode.Front => FNA3D_CullMode.CullClockwiseFace,
        CullMode.Back => FNA3D_CullMode.CullCounterClockwiseFace,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static FNA3D_FillMode ToNative(FillMode mode) => mode switch
    {
        FillMode.Solid => FNA3D_FillMode.Solid,
        FillMode.Wireframe => FNA3D_FillMode.WireFrame,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static FNA3D_BlendState ToNative(BlendStateDesc desc) => new()
    {
        colorSourceBlend = ToNative(desc.SourceColor),
        colorDestinationBlend = ToNative(desc.DestinationColor),
        colorBlendFunction = ToNative(desc.ColorOperation),
        alphaSourceBlend = ToNative(desc.SourceAlpha),
        alphaDestinationBlend = ToNative(desc.DestinationAlpha),
        alphaBlendFunction = ToNative(desc.AlphaOperation),
        colorWriteEnable = FNA3D_ColorWriteChannels.All,
        colorWriteEnable1 = FNA3D_ColorWriteChannels.All,
        colorWriteEnable2 = FNA3D_ColorWriteChannels.All,
        colorWriteEnable3 = FNA3D_ColorWriteChannels.All,
        blendFactor = new FNA3D_Color { R = 255, G = 255, B = 255, A = 255 },
        multiSampleMask = -1,
    };

    public static FNA3D_DepthStencilState ToNative(DepthStencilStateDesc desc) => new()
    {
        depthBufferEnable = (byte)(desc.DepthTestEnabled ? 1 : 0),
        depthBufferWriteEnable = (byte)(desc.DepthWriteEnabled ? 1 : 0),
        depthBufferFunction = ToNative(desc.DepthCompare),
        stencilFunction = FNA3D_CompareFunction.Always,
        ccwStencilFunction = FNA3D_CompareFunction.Always,
        stencilPass = FNA3D_StencilOperation.Keep,
        stencilFail = FNA3D_StencilOperation.Keep,
        stencilDepthBufferFail = FNA3D_StencilOperation.Keep,
        ccwStencilPass = FNA3D_StencilOperation.Keep,
        ccwStencilFail = FNA3D_StencilOperation.Keep,
        ccwStencilDepthBufferFail = FNA3D_StencilOperation.Keep,
        stencilMask = -1,
        stencilWriteMask = -1,
    };

    public static FNA3D_RasterizerState ToNative(RasterizerStateDesc desc) => new()
    {
        cullMode = ToNative(desc.CullMode),
        fillMode = ToNative(desc.FillMode),
    };

    public static FNA3D_VertexElementFormat ToNative(VertexAttributeFormat format) => format switch
    {
        VertexAttributeFormat.Float1 => FNA3D_VertexElementFormat.Single,
        VertexAttributeFormat.Float2 => FNA3D_VertexElementFormat.Vector2,
        VertexAttributeFormat.Float3 => FNA3D_VertexElementFormat.Vector3,
        VertexAttributeFormat.Float4 => FNA3D_VertexElementFormat.Vector4,
        VertexAttributeFormat.Byte4Normalized => FNA3D_VertexElementFormat.Color,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Maps an HLSL semantic name (e.g. "POSITION", "TEXCOORD") to FNA3D's vertex-element usage -
    /// any trailing digits (the usage index, e.g. the "1" in "TEXCOORD1") belong in
    /// <see cref="VertexAttributeDesc.Slot"/> instead and are ignored here.
    /// </summary>
    public static FNA3D_VertexElementUsage ToNativeVertexUsage(string semantic) => semantic.TrimEnd("0123456789".ToCharArray()).ToUpperInvariant() switch
    {
        "POSITION" or "SV_POSITION" => FNA3D_VertexElementUsage.Position,
        "COLOR" => FNA3D_VertexElementUsage.Color,
        "TEXCOORD" => FNA3D_VertexElementUsage.TextureCoordinate,
        "NORMAL" => FNA3D_VertexElementUsage.Normal,
        "BINORMAL" => FNA3D_VertexElementUsage.Binormal,
        "TANGENT" => FNA3D_VertexElementUsage.Tangent,
        "BLENDINDICES" => FNA3D_VertexElementUsage.BlendIndices,
        "BLENDWEIGHT" => FNA3D_VertexElementUsage.BlendWeight,
        "DEPTH" => FNA3D_VertexElementUsage.Depth,
        "FOG" => FNA3D_VertexElementUsage.Fog,
        "PSIZE" => FNA3D_VertexElementUsage.PointSize,
        _ => throw new ArgumentOutOfRangeException(nameof(semantic), semantic, "Unrecognized HLSL semantic."),
    };
}
