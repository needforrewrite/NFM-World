using NFMWorld.Shaders;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>Translates NFMWorld.Graphics abstraction enums to/from FNA3D's native enums.</summary>
internal static class Mapping
{
    public static FNA3D_SurfaceFormat ToNative(this TextureFormat format) => format switch
    {
        TextureFormat.Rgba8 => FNA3D_SurfaceFormat.Color,
        TextureFormat.Bgra8 => FNA3D_SurfaceFormat.ColorBgraEXT,
        TextureFormat.R8 => FNA3D_SurfaceFormat.Alpha8,
        TextureFormat.Dxt1 => FNA3D_SurfaceFormat.Dxt1,
        TextureFormat.Dxt3 => FNA3D_SurfaceFormat.Dxt3,
        TextureFormat.Dxt5 => FNA3D_SurfaceFormat.Dxt5,
        TextureFormat.Single => FNA3D_SurfaceFormat.Single,
        TextureFormat.Depth24Stencil8 => throw new ArgumentException("Depth24Stencil8 is a depth format, not a texture surface format; use ToNativeDepthFormat instead.", nameof(format)),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static FNA3D_DepthFormat ToNativeDepthFormat(this TextureFormat format) => format switch
    {
        TextureFormat.Depth24Stencil8 => FNA3D_DepthFormat.Depth24Stencil8,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Not a depth format."),
    };

    public static FNA3D_BufferUsage ToNativeBufferUsage(this BufferUsage usage) => usage switch
    {
        BufferUsage.Immutable => FNA3D_BufferUsage.WriteOnly,
        BufferUsage.Dynamic => FNA3D_BufferUsage.WriteOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null),
    };

    public static byte ToDynamicFlag(this BufferUsage usage) => usage == BufferUsage.Dynamic ? (byte)1 : (byte)0;

    public static FNA3D_IndexElementSize ToNative(this IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => FNA3D_IndexElementSize.SixteenBits,
        IndexFormat.UInt32 => FNA3D_IndexElementSize.ThirtyTwoBits,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static FNA3D_PrimitiveType ToNative(this PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => FNA3D_PrimitiveType.TriangleList,
        PrimitiveTopology.TriangleStrip => FNA3D_PrimitiveType.TriangleStrip,
        PrimitiveTopology.LineList => FNA3D_PrimitiveType.LineList,
        PrimitiveTopology.LineStrip => FNA3D_PrimitiveType.LineStrip,
        PrimitiveTopology.PointList => FNA3D_PrimitiveType.PointListEXT,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    public static FNA3D_ClearOptions ToNative(this ClearOptions options)
    {
        FNA3D_ClearOptions result = 0;
        if (options.HasFlag(ClearOptions.Color)) result |= FNA3D_ClearOptions.Target;
        if (options.HasFlag(ClearOptions.Depth)) result |= FNA3D_ClearOptions.DepthBuffer;
        if (options.HasFlag(ClearOptions.Stencil)) result |= FNA3D_ClearOptions.Stencil;
        return result;
    }

    public static FNA3D_TextureFilter ToNative(this TextureFilter filter) => filter switch
    {
        TextureFilter.Point => FNA3D_TextureFilter.Point,
        TextureFilter.Linear => FNA3D_TextureFilter.Linear,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    public static FNA3D_TextureAddressMode ToNative(this TextureAddressMode mode) => mode switch
    {
        TextureAddressMode.Wrap => FNA3D_TextureAddressMode.Wrap,
        TextureAddressMode.Clamp => FNA3D_TextureAddressMode.Clamp,
        TextureAddressMode.Mirror => FNA3D_TextureAddressMode.Mirror,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static FNA3D_Blend ToNative(this BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => FNA3D_Blend.Zero,
        BlendFactor.One => FNA3D_Blend.One,
        BlendFactor.SourceAlpha => FNA3D_Blend.SourceAlpha,
        BlendFactor.InverseSourceAlpha => FNA3D_Blend.InverseSourceAlpha,
        BlendFactor.DestinationAlpha => FNA3D_Blend.DestinationAlpha,
        BlendFactor.InverseDestinationAlpha => FNA3D_Blend.InverseDestinationAlpha,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
    };

    public static FNA3D_BlendFunction ToNative(this BlendOperation op) => op switch
    {
        BlendOperation.Add => FNA3D_BlendFunction.Add,
        BlendOperation.Subtract => FNA3D_BlendFunction.Subtract,
        BlendOperation.ReverseSubtract => FNA3D_BlendFunction.ReverseSubtract,
        BlendOperation.Min => FNA3D_BlendFunction.Min,
        BlendOperation.Max => FNA3D_BlendFunction.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static FNA3D_CompareFunction ToNative(this CompareFunction fn) => fn switch
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

    public static FNA3D_CullMode ToNative(this CullMode mode) => mode switch
    {
        CullMode.None => FNA3D_CullMode.None,
        CullMode.Front => FNA3D_CullMode.CullClockwiseFace,
        CullMode.Back => FNA3D_CullMode.CullCounterClockwiseFace,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static FNA3D_FillMode ToNative(this FillMode mode) => mode switch
    {
        FillMode.Solid => FNA3D_FillMode.Solid,
        FillMode.Wireframe => FNA3D_FillMode.WireFrame,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static FNA3D_ColorWriteChannels ToNative(this ColorWriteMask mask)
    {
        FNA3D_ColorWriteChannels result = 0;
        if (mask.HasFlag(ColorWriteMask.Red)) result |= FNA3D_ColorWriteChannels.Red;
        if (mask.HasFlag(ColorWriteMask.Green)) result |= FNA3D_ColorWriteChannels.Green;
        if (mask.HasFlag(ColorWriteMask.Blue)) result |= FNA3D_ColorWriteChannels.Blue;
        if (mask.HasFlag(ColorWriteMask.Alpha)) result |= FNA3D_ColorWriteChannels.Alpha;
        return result;
    }

    public static FNA3D_StencilOperation ToNative(this StencilOperation op) => op switch
    {
        StencilOperation.Keep => FNA3D_StencilOperation.Keep,
        StencilOperation.Zero => FNA3D_StencilOperation.Zero,
        StencilOperation.Replace => FNA3D_StencilOperation.Replace,
        StencilOperation.Increment => FNA3D_StencilOperation.Increment,
        StencilOperation.Decrement => FNA3D_StencilOperation.Decrement,
        StencilOperation.IncrementSaturate => FNA3D_StencilOperation.IncrementSaturation,
        StencilOperation.DecrementSaturate => FNA3D_StencilOperation.DecrementSaturation,
        StencilOperation.Invert => FNA3D_StencilOperation.Invert,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static FNA3D_BlendState ToNative(this BlendStateDesc desc) => new()
    {
        colorSourceBlend = desc.SourceColor.ToNative(),
        colorDestinationBlend = desc.DestinationColor.ToNative(),
        colorBlendFunction = desc.ColorOperation.ToNative(),
        alphaSourceBlend = desc.SourceAlpha.ToNative(),
        alphaDestinationBlend = desc.DestinationAlpha.ToNative(),
        alphaBlendFunction = desc.AlphaOperation.ToNative(),
        colorWriteEnable = desc.ColorWriteMask.ToNative(),
        colorWriteEnable1 = desc.ColorWriteMask.ToNative(),
        colorWriteEnable2 = desc.ColorWriteMask.ToNative(),
        colorWriteEnable3 = desc.ColorWriteMask.ToNative(),
        blendFactor = new FNA3D_Color(255, 255, 255, 255),
        multiSampleMask = -1,
    };

    public static FNA3D_DepthStencilState ToNative(this DepthStencilStateDesc desc) => new()
    {
        depthBufferEnable = (byte)(desc.DepthTestEnabled ? 1 : 0),
        depthBufferWriteEnable = (byte)(desc.DepthWriteEnabled ? 1 : 0),
        depthBufferFunction = desc.DepthCompare.ToNative(),
        stencilEnable = (byte)(desc.StencilTestEnabled ? 1 : 0),
        twoSidedStencilMode = (byte)(desc.TwoSidedStencil ? 1 : 0),
        stencilFunction = desc.StencilFunction.ToNative(),
        ccwStencilFunction = desc.CcwStencilFunction.ToNative(),
        stencilPass = desc.StencilPass.ToNative(),
        stencilFail = desc.StencilFail.ToNative(),
        stencilDepthBufferFail = desc.StencilDepthFail.ToNative(),
        ccwStencilPass = desc.CcwStencilPass.ToNative(),
        ccwStencilFail = desc.CcwStencilFail.ToNative(),
        ccwStencilDepthBufferFail = desc.CcwStencilDepthFail.ToNative(),
        stencilMask = desc.StencilReadMask,
        stencilWriteMask = desc.StencilWriteMask,
        referenceStencil = desc.ReferenceStencil,
    };

    public static FNA3D_RasterizerState ToNative(this RasterizerStateDesc desc) => new()
    {
        cullMode = desc.CullMode.ToNative(),
        fillMode = desc.FillMode.ToNative(),
        scissorTestEnable = (byte)(desc.ScissorTestEnabled ? 1 : 0),
    };

    public static FNA3D_SamplerState ToNative(this SamplerDesc desc) => new()
    {
        filter = desc.Filter.ToNative(),
        addressU = desc.AddressU.ToNative(),
        addressV = desc.AddressV.ToNative(),
        addressW = FNA3D_TextureAddressMode.Wrap,
        mipMapLevelOfDetailBias = 0f,
        maxAnisotropy = 4,
        maxMipLevel = 0,
    };

    public static FNA3D_VertexElementFormat ToNative(this VertexAttributeFormat format) => format switch
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
    public static FNA3D_VertexElementUsage ToNativeVertexUsage(string semantic) => semantic.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').ToUpperInvariant() switch
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
