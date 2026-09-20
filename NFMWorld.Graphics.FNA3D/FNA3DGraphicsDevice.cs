using NFMWorld.Shaders;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// FNA3D-backed <see cref="IGraphicsDevice"/> - the first concrete backend, proving the
/// abstraction against FNA3D's native C ABI directly rather than wrapping FNA's own
/// <c>Microsoft.Xna.Framework.Graphics.GraphicsDevice</c>. Because FNA3D is itself an
/// immediate-mode device, <see cref="AcquireCommandBuffer"/>/<see cref="Submit"/> are cheap:
/// every <see cref="ICommandBuffer"/> call executes synchronously against the native device the
/// instant it's made (see <see cref="FNA3DCommandBuffer"/>), so there is nothing to defer or
/// replay here - only a future Vulkan/Metal backend would need real recording.
/// </summary>
public sealed class FNA3DGraphicsDevice : IGraphicsDevice, IDisposable
{
    private readonly IntPtr _device;
    private bool _commandBufferAcquired;

    public ISwapchain Swapchain { get; }

    /// <summary>
    /// The raw <c>FNA3D_Device*</c>. Deliberately not public - it's backend-specific, so exposing
    /// it on the interface would defeat the point of the abstraction. Available to the smoke test
    /// (see InternalsVisibleTo) for framebuffer readback verification.
    /// </summary>
    internal IntPtr Handle => _device;

    private FNA3DGraphicsDevice(IntPtr device, FNA3DSwapchain swapchain)
    {
        _device = device;
        Swapchain = swapchain;
    }

    /// <summary>
    /// Creates the FNA3D device against an existing native window handle (an SDL_Window*, once
    /// NFMWorld.Platform.SDL3 exists per Milestone 2 - any native window handle works today for
    /// standalone smoke-testing).
    /// </summary>
    public static FNA3DGraphicsDevice Create(IntPtr windowHandle, int backBufferWidth, int backBufferHeight, bool vsync = true, bool debugMode = false)
    {
        var parameters = new FNA3D_PresentationParameters
        {
            backBufferWidth = backBufferWidth,
            backBufferHeight = backBufferHeight,
            backBufferFormat = FNA3D_SurfaceFormat.Color,
            multiSampleCount = 0,
            deviceWindowHandle = windowHandle,
            isFullScreen = 0,
            depthStencilFormat = FNA3D_DepthFormat.Depth24Stencil8,
            presentationInterval = vsync ? FNA3D_PresentInterval.One : FNA3D_PresentInterval.Immediate,
            displayOrientation = FNA3D_DisplayOrientation.Default,
            renderTargetUsage = FNA3D_RenderTargetUsage.DiscardContents,
        };

        var device = FNA3D_CreateDevice(ref parameters, (byte)(debugMode ? 1 : 0));
        if (device == IntPtr.Zero)
            throw new InvalidOperationException("FNA3D_CreateDevice returned null - check that the FNA3D native library and a compatible graphics driver are available.");

        return new FNA3DGraphicsDevice(device, new FNA3DSwapchain(device, windowHandle, backBufferWidth, backBufferHeight));
    }

    public ICommandBuffer AcquireCommandBuffer()
    {
        if (_commandBufferAcquired)
            throw new InvalidOperationException($"{nameof(AcquireCommandBuffer)} was called again before the previous command buffer was submitted - only one command buffer may be live at a time.");
        _commandBufferAcquired = true;
        return new FNA3DCommandBuffer(_device);
    }

    public void Submit(ICommandBuffer commandBuffer)
    {
        // FNA3DCommandBuffer executes every call immediately, so there is nothing left to flush
        // here - this just releases the "one buffer at a time" guard.
        _commandBufferAcquired = false;
    }

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        var dynamic = desc.Usage.ToDynamicFlag();
        var usage = desc.Usage.ToNativeBufferUsage();
        var handle = desc.Kind == BufferKind.Vertex
            ? FNA3D_GenVertexBuffer(_device, dynamic, usage, desc.SizeInBytes)
            : FNA3D_GenIndexBuffer(_device, dynamic, usage, desc.SizeInBytes);

        var buffer = new FNA3DBuffer(_device, handle, desc);
        if (!initialData.IsEmpty)
        {
            var cb = new FNA3DCommandBuffer(_device);
            cb.UpdateBuffer(buffer, initialData);
        }
        return buffer;
    }

    public unsafe ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        var handle = FNA3D_CreateTexture2D(_device, desc.Format.ToNative(), desc.Width, desc.Height, desc.MipMapped ? 0 : 1, (byte)(desc.RenderTargetable ? 1 : 0));
        var texture = new FNA3DTexture(_device, handle, desc);
        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
            {
                FNA3D_SetTextureData2D(_device, handle, 0, 0, desc.Width, desc.Height, 0, (IntPtr)ptr, initialData.Length);
            }
        }
        return texture;
    }

    public unsafe void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
    {
        if (texture is not FNA3DTexture fna)
        {
            throw new ArgumentException(
                $"{nameof(ReadTexture)} needs a texture this backend created - got {texture.GetType().Name}.",
                nameof(texture));
        }
        if (fna.Handle == IntPtr.Zero)
        {
            throw new ArgumentException(
                "This texture has no native handle and cannot be read back: IRenderTarget.DepthStencilTexture is a " +
                "placeholder (FNA3D keeps depth-stencil data in a renderbuffer, not a texture object). Read the " +
                "target's ColorTexture instead.",
                nameof(texture));
        }

        fixed (byte* ptr = destination)
        {
            FNA3D_GetTextureData2D(_device, fna.Handle, x, y, width, height, level, (IntPtr)ptr, destination.Length);
        }
    }

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        var colorTextureDesc = new TextureDesc(desc.Width, desc.Height, desc.ColorFormat, RenderTargetable: true);
        var colorHandle = FNA3D_CreateTexture2D(_device, desc.ColorFormat.ToNative(), desc.Width, desc.Height, 1, 1);
        var colorTexture = new FNA3DTexture(_device, colorHandle, colorTextureDesc);

        // Only MSAA targets need a colour renderbuffer: FNA's own RenderTarget2D calls
        // FNA3D_GenColorRenderbuffer when MultiSampleCount > 0 and otherwise leaves
        // FNA3D_RenderTargetBinding.colorBuffer zero, letting FNA3D_SetRenderTargets bind the
        // texture itself. RenderTargetDesc carries no sample count, so today this is always the
        // null path - and it has to be, at least on the D3D11 driver: with a renderbuffer
        // generated at sample count 0, a cleared off-screen target's ColorTexture reads back as
        // all zeros through IGraphicsDevice.ReadTexture (measured in the smoke test, and anything
        // sampling that texture - e.g. the shadow cascades - then sees zeros), while the null path
        // round-trips the clear exactly. OpenGL and SDLGPU read back correctly either way.
        IntPtr colorRenderbuffer = IntPtr.Zero;

        IntPtr depthRenderbuffer = IntPtr.Zero;
        ITexture? depthTexture = null;
        if (desc.HasDepthStencil)
        {
            depthRenderbuffer = FNA3D_GenDepthStencilRenderbuffer(_device, desc.Width, desc.Height, desc.DepthStencilFormat.ToNativeDepthFormat(), 0);
            depthTexture = new FNA3DTexture(_device, IntPtr.Zero, new TextureDesc(desc.Width, desc.Height, desc.DepthStencilFormat));
        }

        return new FNA3DRenderTarget(_device, colorRenderbuffer, depthRenderbuffer, colorTexture, depthTexture);
    }

    public ISampler CreateSampler(SamplerDesc desc) => new FNA3DSampler(desc);

    /// <summary>
    /// Wraps a compiled D3D9 Effects Framework blob (.fxb, produced by fxc.exe) as an
    /// <see cref="IShaderModule"/> for this backend - see <see cref="FNA3DShaderModule"/>'s doc
    /// comments for why FNA3D represents a whole Effect as one shader module rather than separate
    /// vertex/pixel stages. Pass the returned module for BOTH <see cref="PipelineDesc.VertexShader"/>
    /// and <see cref="PipelineDesc.PixelShader"/> when calling <see cref="CreatePipeline"/>.
    /// </summary>
    public static IShaderModule LoadEffectModule(ReadOnlyMemory<byte> compiledEffectBytecode) =>
        new FNA3DShaderModule(ShaderStage.Vertex, compiledEffectBytecode);

    public IPipelineState CreatePipeline(PipelineDesc desc)
    {
        if (!ReferenceEquals(desc.VertexShader, desc.PixelShader))
        {
            throw new ArgumentException(
                "FNA3D has no way to submit a standalone vertex or pixel shader - only a whole " +
                "compiled Effect. Pass the same FNA3DShaderModule for both VertexShader and " +
                "PixelShader to signal that its Bytecode is a complete Effect blob.", nameof(desc));
        }

        var bytecode = desc.VertexShader.Bytecode.ToArray();
        FNA3D_CreateEffect(_device, bytecode, bytecode.Length, out var effect, out var effectData);
        if (effect == IntPtr.Zero)
            throw new InvalidOperationException("FNA3D_CreateEffect returned null - the compiled Effect blob failed to parse.");

        var pipeline = new FNA3DPipelineState(_device, desc, effect, effectData, desc.TechniqueName);
        FNA3D_SetEffectTechnique(_device, effect, pipeline.TechniquePointer);
        return pipeline;
    }

    public void Dispose() => FNA3D_DestroyDevice(_device);
}
