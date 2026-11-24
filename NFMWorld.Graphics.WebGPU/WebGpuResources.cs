// LLM maintained.
//
// The GPU objects: buffers, textures (with the view every binding and attachment needs), samplers,
// render targets and the compiled shader module.
//
// WebGPU separates a texture from the view used to bind it, where D3D11's shader resource view and
// render target view are also separate objects but GL has neither. That difference is why every
// texture here owns one view covering its whole single mip level: a combined depth-stencil view, a
// sampled view and a render attachment are all this one object in every case this tree produces.

using NFMWorld.Shaders;
using Wg = Maxine.WebGPU;
using WgpuBuffer = Maxine.WebGPU.Buffer;

namespace NFMWorld.Graphics.WebGPU;

internal sealed unsafe class WebGpuBuffer : IBuffer
{
    private readonly WebGpuGraphicsDevice _device;

    internal WgpuBuffer* Handle;

    public BufferKind Kind { get; }
    public BufferUsage Usage { get; }
    public int SizeInBytes { get; }
    internal IndexFormat IndexFormat { get; }
    internal Wg.BufferUsage GpuUsage { get; }

    /// <summary>
    /// The allocation's size, rounded up to WebGPU's copy unit. Copies are in 4-byte units, so an
    /// update that fills a buffer whose logical size is not a multiple of 4 - an index buffer of an
    /// odd number of 16-bit indices - has to be allowed to write its last partial word.
    /// </summary>
    internal ulong GpuSize { get; }

    internal WebGpuBuffer(WebGpuGraphicsDevice device, BufferDesc desc, ReadOnlySpan<byte> initialData)
    {
        _device = device;
        Kind = desc.Kind;
        Usage = desc.Usage;
        SizeInBytes = desc.SizeInBytes;
        IndexFormat = desc.IndexFormat;
        GpuSize = WebGpuInterop.Align((ulong)desc.SizeInBytes, 4);

        GpuUsage = desc.Kind == BufferKind.Vertex ? Wg.BufferUsage.Vertex : Wg.BufferUsage.Index;
        // CopyDst because every update travels through a staging copy or a queue write; CopySrc so a
        // buffer can be read back and so a caller may copy one into another.
        GpuUsage |= Wg.BufferUsage.CopyDst | Wg.BufferUsage.CopySrc;

        var descriptor = new Wg.BufferDescriptor
        {
            Usage = GpuUsage,
            Size = GpuSize,
            MappedAtCreation = 0,
        };

        Handle = _device.Api.DeviceCreateBuffer(_device.Device, in descriptor);
        _device.CheckCreated(Handle is not null, $"buffer ({desc.Kind}, {desc.SizeInBytes} byte(s))");

        if (!initialData.IsEmpty)
            _device.WriteBuffer(Handle, 0, initialData);
    }

    internal int IndexSize => IndexFormat == IndexFormat.UInt32 ? 4 : 2;

    public void Dispose()
    {
        if (Handle is null)
            return;

        // Deferred: a buffer can be replaced while the frame being recorded still references it
        // (ImmediateBatch3D reallocates its vertex buffer mid-frame). See WebGpuGraphicsDevice.DeferDestroy.
        var handle = Handle;
        Handle = null;
        _device.DeferDestroy(() =>
            {
                _device.Api.BufferDestroy(handle);
                _device.Api.BufferRelease(handle);
            }
        );
    }
}

internal sealed unsafe class WebGpuTexture : ITexture
{
    private readonly WebGpuGraphicsDevice _device;

    internal Wg.Texture* Handle;
    internal Wg.TextureView* View;

    public int Width { get; }
    public int Height { get; }
    public TextureFormat Format { get; }

    /// <summary>Sample count, which is above one only for the swapchain's multisampled colour buffer.</summary>
    internal int SampleCount { get; }

    internal bool MipMapped { get; }

    /// <summary>The format as WebGPU names it, kept because a pipeline variant keys on it.</summary>
    internal Wg.TextureFormat GpuFormat { get; }

    internal WebGpuTexture(
        WebGpuGraphicsDevice device,
        int width,
        int height,
        TextureFormat format,
        bool renderTargetable,
        bool mipMapped,
        int sampleCount = 1
    )
    {
        _device = device;
        Width = width;
        Height = height;
        Format = format;
        SampleCount = sampleCount;
        MipMapped = mipMapped;
        GpuFormat = format.ToTextureFormat();

        var usage = Wg.TextureUsage.TextureBinding | Wg.TextureUsage.CopyDst | Wg.TextureUsage.CopySrc;
        if (renderTargetable)
            usage |= Wg.TextureUsage.RenderAttachment;

        var descriptor = new Wg.TextureDescriptor
        {
            Usage = usage,
            Dimension = Wg.TextureDimension.Dimension2D,
            Size = new Wg.Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 },
            Format = GpuFormat,
            // No mip generation: WebGPU has no blit, and the one caller that asks for a mip chain
            // (the font atlas) samples level 0 only.
            MipLevelCount = 1,
            SampleCount = (uint)sampleCount,
        };

        Handle = device.Api.DeviceCreateTexture(device.Device, in descriptor);
        device.CheckCreated(Handle is not null, $"texture ({width}x{height} {format}, {sampleCount}x)");

        View = CreateView(device, Handle, GpuFormat);
    }

    private static Wg.TextureView* CreateView(WebGpuGraphicsDevice device, Wg.Texture* texture, Wg.TextureFormat format)
    {
        var descriptor = new Wg.TextureViewDescriptor
        {
            Format = format,
            Dimension = Wg.TextureViewDimension.Dimension2D,
            BaseMipLevel = 0,
            MipLevelCount = 1,
            BaseArrayLayer = 0,
            ArrayLayerCount = 1,
            Aspect = Wg.TextureAspect.All,
        };

        var view = device.Api.TextureCreateView(texture, in descriptor);
        device.CheckCreated(view is not null, "texture view");
        return view;
    }

    public void Dispose()
    {
        if (View is not null)
        {
            // Deferred for the same reason a bind group's release is: a pass records the view's id
            // for its attachment and resolves it when the pass ends.
            var view = View;
            View = null;
            _device.DeferDestroy(() => _device.Api.TextureViewRelease(view));
        }

        if (Handle is null)
            return;

        // Deferred for the same reason as a buffer's: the whole-frame command buffer still holds a
        // reference to it, and wgpu treats destroy as invalidating every later submission.
        var handle = Handle;
        Handle = null;
        _device.DeferDestroy(() =>
            {
                _device.Api.TextureDestroy(handle);
                _device.Api.TextureRelease(handle);
            }
        );
    }
}

internal sealed unsafe class WebGpuSampler : ISampler
{
    private readonly WebGpuGraphicsDevice _device;

    internal Wg.Sampler* Handle;

    public SamplerDesc Desc { get; }

    internal WebGpuSampler(WebGpuGraphicsDevice device, SamplerDesc desc)
    {
        _device = device;
        Desc = desc;

        var descriptor = new Wg.SamplerDescriptor
        {
            AddressModeU = desc.AddressU.ToAddressMode(),
            AddressModeV = desc.AddressV.ToAddressMode(),
            AddressModeW = desc.AddressU.ToAddressMode(),
            MagFilter = desc.Filter.ToFilterMode(),
            MinFilter = desc.Filter.ToFilterMode(),
            MipmapFilter = Wg.MipmapFilterMode.Nearest,
            LodMinClamp = 0,
            LodMaxClamp = 1,
            Compare = Wg.CompareFunction.Undefined,
            MaxAnisotropy = 1,
        };

        Handle = device.Api.DeviceCreateSampler(device.Device, in descriptor);
        device.CheckCreated(Handle is not null, "sampler");
    }

    /// <summary>Whether a binding layout may declare this sampler Filtering, which point sampling may not.</summary>
    internal bool IsFiltering => Desc.Filter == TextureFilter.Linear;

    public void Dispose()
    {
        if (Handle is null)
            return;

        _device.Api.SamplerRelease(Handle);
        Handle = null;
    }
}

internal sealed unsafe class WebGpuRenderTarget : IRenderTarget
{
    internal WebGpuTexture Color { get; }
    internal WebGpuTexture? Depth { get; }

    public ITexture ColorTexture => Color;
    public ITexture? DepthStencilTexture => Depth;

    internal WebGpuRenderTarget(WebGpuGraphicsDevice device, RenderTargetDesc desc)
    {
        Color = new WebGpuTexture(device, desc.Width, desc.Height, desc.ColorFormat, renderTargetable: true, mipMapped: false);

        if (desc.HasDepthStencil)
            Depth = new WebGpuTexture(
                device,
                desc.Width,
                desc.Height,
                desc.DepthStencilFormat,
                renderTargetable: true,
                mipMapped: false
            );
    }

    public void Dispose()
    {
        Color.Dispose();
        Depth?.Dispose();
    }
}

/// <summary>
/// A program's two WGSL modules plus the reflection and the entry-point names WebGPU has to be told
/// explicitly.
///
/// One module covers both stages for the same reason the GL backend's does: the reflection describes
/// the merged result, and <see cref="IShaderModule.Stage"/> reports
/// <see cref="ShaderStage.Vertex"/> because the abstraction has no "whole program" stage.
/// </summary>
internal sealed unsafe class WebGpuShaderModule : IShaderModule
{
    private readonly WebGpuGraphicsDevice _device;

    internal Wg.ShaderModule* VertexModule;
    internal Wg.ShaderModule* PixelModule;

    internal string VertexEntry { get; }
    internal string PixelEntry { get; }

    internal ShaderReflection Reflection { get; }

    public ShaderStage Stage => ShaderStage.Vertex;

    /// <summary>Empty: this backend consumes WGSL source, not a byte blob.</summary>
    public ReadOnlyMemory<byte> Bytecode => ReadOnlyMemory<byte>.Empty;

    internal WebGpuShaderModule(
        WebGpuGraphicsDevice device,
        ShaderStageSources vertex,
        ShaderStageSources pixel,
        ShaderReflection reflection
    )
    {
        _device = device;
        Reflection = reflection;
        VertexEntry = vertex.EntryPoint;
        PixelEntry = pixel.EntryPoint;

        VertexModule = CreateModule(device, vertex, "vertex");
        PixelModule = WgslFor(device, pixel).Length > 0 ? CreateModule(device, pixel, "pixel") : null;
    }

    /// <summary>
    /// The WGSL flavour this target compiles. They differ only in whether tint's
    /// derivative-uniformity directive is present: desktop naga rejects it, the browser's Tint
    /// requires it on the pixel stages that sample under divergent control flow.
    /// </summary>
    private static string WgslFor(WebGpuGraphicsDevice device, ShaderStageSources sources) =>
        device.IsBrowser ? sources.WgslBrowser : sources.Wgsl;

    private static Wg.ShaderModule* CreateModule(WebGpuGraphicsDevice device, ShaderStageSources sources, string stage)
    {
        var wgsl = WgslFor(device, sources);

        if (wgsl.Length == 0)
        {
            throw new NotSupportedException(
                $"This build's shader bundles carry no WGSL for the {stage} stage"
                + (device.IsBrowser ? " in the browser's flavour" : "")
                + ", so the WebGPU backend cannot create the pipeline. Rebuild with the tint package "
                + "restored (see NFMWorld.ShaderCompiler/WgslCompiler.cs)."
            );
        }

        using var code = new WebGpuInterop.Utf8(wgsl);
        var wgslDescriptor = new Wg.ShaderSourceWGSL
        {
            Chain = new Wg.ChainedStruct { SType = Wg.SType.ShaderSourceWgsl },
            Code = code.View,
        };

        var descriptor = new Wg.ShaderModuleDescriptor
        {
            NextInChain = (Wg.ChainedStruct*)&wgslDescriptor,
        };

        var module = device.Api.DeviceCreateShaderModule(device.Device, in descriptor);
        device.CheckCreated(module is not null, $"{stage} shader module");
        return module;
    }

    public void Dispose()
    {
        if (VertexModule is not null)
        {
            _device.Api.ShaderModuleRelease(VertexModule);
            VertexModule = null;
        }

        if (PixelModule is not null)
        {
            _device.Api.ShaderModuleRelease(PixelModule);
            PixelModule = null;
        }
    }
}
