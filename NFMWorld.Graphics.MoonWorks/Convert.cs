using System.Runtime.CompilerServices;
using Core = NFMWorld.Graphics.Core;
using MW = MoonWorks.Graphics;

namespace NFMWorld.Graphics.MoonWorks;

/// <summary>
/// Converts between Core abstraction enums/structs and MoonWorks types.
/// Most enums share identical integer layouts and can be cast directly.
/// TextureFormat requires explicit mapping due to differing value assignments.
/// </summary>
internal static class Convert
{
    // ── TextureFormat (different int values, needs explicit mapping) ──

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.TextureFormat ToMW(Core.TextureFormat f) => f switch
    {
        Core.TextureFormat.R8Unorm => MW.TextureFormat.R8Unorm,
        Core.TextureFormat.R8G8B8A8Unorm => MW.TextureFormat.R8G8B8A8Unorm,
        Core.TextureFormat.B8G8R8A8Unorm => MW.TextureFormat.B8G8R8A8Unorm,
        Core.TextureFormat.R8G8B8A8UnormSrgb => MW.TextureFormat.R8G8B8A8UnormSRGB,
        Core.TextureFormat.B8G8R8A8UnormSrgb => MW.TextureFormat.B8G8R8A8UnormSRGB,
        Core.TextureFormat.R16G16B16A16Float => MW.TextureFormat.R16G16B16A16Float,
        Core.TextureFormat.R32Float => MW.TextureFormat.R32Float,
        Core.TextureFormat.R32G32Float => MW.TextureFormat.R32G32Float,
        Core.TextureFormat.R32G32B32A32Float => MW.TextureFormat.R32G32B32A32Float,
        Core.TextureFormat.D16Unorm => MW.TextureFormat.D16Unorm,
        Core.TextureFormat.D24Unorm => MW.TextureFormat.D24Unorm,
        Core.TextureFormat.D32Float => MW.TextureFormat.D32Float,
        Core.TextureFormat.D24UnormS8Uint => MW.TextureFormat.D24UnormS8Uint,
        Core.TextureFormat.D32FloatS8Uint => MW.TextureFormat.D32FloatS8Uint,
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, null)
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Core.TextureFormat ToCore(MW.TextureFormat f) => f switch
    {
        MW.TextureFormat.R8Unorm => Core.TextureFormat.R8Unorm,
        MW.TextureFormat.R8G8B8A8Unorm => Core.TextureFormat.R8G8B8A8Unorm,
        MW.TextureFormat.B8G8R8A8Unorm => Core.TextureFormat.B8G8R8A8Unorm,
        MW.TextureFormat.R8G8B8A8UnormSRGB => Core.TextureFormat.R8G8B8A8UnormSrgb,
        MW.TextureFormat.B8G8R8A8UnormSRGB => Core.TextureFormat.B8G8R8A8UnormSrgb,
        MW.TextureFormat.R16G16B16A16Float => Core.TextureFormat.R16G16B16A16Float,
        MW.TextureFormat.R32Float => Core.TextureFormat.R32Float,
        MW.TextureFormat.R32G32Float => Core.TextureFormat.R32G32Float,
        MW.TextureFormat.R32G32B32A32Float => Core.TextureFormat.R32G32B32A32Float,
        MW.TextureFormat.D16Unorm => Core.TextureFormat.D16Unorm,
        MW.TextureFormat.D24Unorm => Core.TextureFormat.D24Unorm,
        MW.TextureFormat.D32Float => Core.TextureFormat.D32Float,
        MW.TextureFormat.D24UnormS8Uint => Core.TextureFormat.D24UnormS8Uint,
        MW.TextureFormat.D32FloatS8Uint => Core.TextureFormat.D32FloatS8Uint,
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, null)
    };

    // ── Enums with matching integer layouts — cast directly ──

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.TextureUsageFlags ToMW(Core.TextureUsageFlags f) => (MW.TextureUsageFlags)(uint)f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.BufferUsageFlags ToMW(Core.BufferUsageFlags f) => (MW.BufferUsageFlags)(uint)f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.IndexElementSize ToMW(Core.IndexElementSize v) => (MW.IndexElementSize)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.PrimitiveType ToMW(Core.PrimitiveType v) => (MW.PrimitiveType)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.CullMode ToMW(Core.CullMode v) => (MW.CullMode)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.FrontFace ToMW(Core.FrontFace v) => (MW.FrontFace)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.FillMode ToMW(Core.FillMode v) => (MW.FillMode)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.CompareOp ToMW(Core.CompareOp v) => (MW.CompareOp)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.StencilOp ToMW(Core.StencilOp v) => (MW.StencilOp)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.BlendFactor ToMW(Core.BlendFactor v) => (MW.BlendFactor)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.BlendOp ToMW(Core.BlendOp v) => (MW.BlendOp)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.ColorComponentFlags ToMW(Core.ColorComponentFlags v) => (MW.ColorComponentFlags)(byte)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.LoadOp ToMW(Core.LoadOp v) => (MW.LoadOp)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.StoreOp ToMW(Core.StoreOp v) => (MW.StoreOp)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.SampleCount ToMW(Core.SampleCount v) => (MW.SampleCount)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.Filter ToMW(Core.Filter v) => (MW.Filter)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.SamplerMipmapMode ToMW(Core.SamplerMipmapMode v) => (MW.SamplerMipmapMode)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.SamplerAddressMode ToMW(Core.SamplerAddressMode v) => (MW.SamplerAddressMode)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.ShaderStage ToMW(Core.ShaderStage v) => (MW.ShaderStage)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.VertexElementFormat ToMW(Core.VertexElementFormat v) => (MW.VertexElementFormat)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.VertexInputRate ToMW(Core.VertexInputRate v) => (MW.VertexInputRate)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.PresentMode ToMW(Core.PresentMode v) => (MW.PresentMode)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.SwapchainComposition ToMW(Core.SwapchainComposition v) => (MW.SwapchainComposition)(int)v;

    // ── Composite structs ────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.StencilOpState ToMW(Core.StencilOpState s) => new()
    {
        FailOp = ToMW(s.FailOp),
        PassOp = ToMW(s.PassOp),
        DepthFailOp = ToMW(s.DepthFailOp),
        CompareOp = ToMW(s.CompareOp),
    };

    public static MW.DepthStencilState ToMW(Core.DepthStencilState s) => new()
    {
        CompareOp = ToMW(s.CompareOp),
        BackStencilState = ToMW(s.BackStencilState),
        FrontStencilState = ToMW(s.FrontStencilState),
        CompareMask = s.CompareMask,
        WriteMask = s.WriteMask,
        EnableDepthTest = s.EnableDepthTest,
        EnableDepthWrite = s.EnableDepthWrite,
        EnableStencilTest = s.EnableStencilTest,
    };

    public static MW.RasterizerState ToMW(Core.RasterizerState s) => new()
    {
        FillMode = ToMW(s.FillMode),
        CullMode = ToMW(s.CullMode),
        FrontFace = ToMW(s.FrontFace),
        DepthBiasConstantFactor = s.DepthBiasConstantFactor,
        DepthBiasClamp = s.DepthBiasClamp,
        DepthBiasSlopFactor = s.DepthBiasSlopeFactor,
        EnableDepthBias = s.EnableDepthBias,
        EnableDepthClip = s.EnableDepthClip,
    };

    public static MW.MultisampleState ToMW(Core.MultisampleState s) => new()
    {
        SampleCount = ToMW(s.SampleCount),
        SampleMask = s.SampleMask,
        EnableMask = s.EnableMask,
    };

    public static MW.ColorTargetBlendState ToMW(Core.ColorTargetBlendState s) => new()
    {
        SrcColorBlendFactor = ToMW(s.SrcColorBlendFactor),
        DstColorBlendFactor = ToMW(s.DstColorBlendFactor),
        ColorBlendOp = ToMW(s.ColorBlendOp),
        SrcAlphaBlendFactor = ToMW(s.SrcAlphaBlendFactor),
        DstAlphaBlendFactor = ToMW(s.DstAlphaBlendFactor),
        AlphaBlendOp = ToMW(s.AlphaBlendOp),
        ColorWriteMask = ToMW(s.ColorWriteMask),
        EnableBlend = s.EnableBlend,
        EnableColorWriteMask = s.EnableColorWriteMask,
    };

    public static MW.SamplerCreateInfo ToMW(Core.SamplerCreateInfo s) => new()
    {
        MinFilter = ToMW(s.MinFilter),
        MagFilter = ToMW(s.MagFilter),
        MipmapMode = ToMW(s.MipmapMode),
        AddressModeU = ToMW(s.AddressModeU),
        AddressModeV = ToMW(s.AddressModeV),
        AddressModeW = ToMW(s.AddressModeW),
        MipLodBias = s.MipLodBias,
        MaxAnisotropy = s.MaxAnisotropy,
        CompareOp = ToMW(s.CompareOp),
        MinLod = s.MinLod,
        MaxLod = s.MaxLod,
        EnableAnisotropy = s.EnableAnisotropy,
        EnableCompare = s.EnableCompare,
    };

    // ── Unwrap helpers ───────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.Texture Unwrap(Core.ITexture texture) =>
        ((MwTexture)texture).Inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.Buffer Unwrap(Core.IBuffer buffer) =>
        ((MwBuffer)buffer).Inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.Shader Unwrap(Core.IShader shader) =>
        ((MwShader)shader).Inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.Sampler Unwrap(Core.ISampler sampler) =>
        ((MwSampler)sampler).Inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.GraphicsPipeline Unwrap(Core.IGraphicsPipeline pipeline) =>
        ((MwGraphicsPipeline)pipeline).Inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static global::MoonWorks.Window Unwrap(Core.IWindow window) =>
        ((MwWindow)window).Inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MW.Fence Unwrap(Core.IFence fence) =>
        ((MwFence)fence).Inner;
}
