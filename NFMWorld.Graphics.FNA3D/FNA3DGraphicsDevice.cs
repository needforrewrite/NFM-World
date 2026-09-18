using NFMWorld.Graphics.FNA3D.Native;

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
            depthStencilFormat = FNA3D_DepthFormat.D24S8,
            presentationInterval = vsync ? FNA3D_PresentInterval.One : FNA3D_PresentInterval.Immediate,
            displayOrientation = FNA3D_DisplayOrientation.Default,
            renderTargetUsage = FNA3D_RenderTargetUsage.DiscardContents,
        };

        var device = FNA3DNative.FNA3D_CreateDevice(ref parameters, (byte)(debugMode ? 1 : 0));
        if (device == IntPtr.Zero)
            throw new InvalidOperationException("FNA3D_CreateDevice returned null - check that the FNA3D native library and a compatible graphics driver are available.");

        return new FNA3DGraphicsDevice(device, new FNA3DSwapchain(device, backBufferWidth, backBufferHeight));
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
        var dynamic = Mapping.ToDynamicFlag(desc.Usage);
        var usage = Mapping.ToNativeBufferUsage(desc.Usage);
        var handle = desc.Kind == BufferKind.Vertex
            ? FNA3DNative.FNA3D_GenVertexBuffer(_device, dynamic, usage, desc.SizeInBytes)
            : FNA3DNative.FNA3D_GenIndexBuffer(_device, dynamic, usage, desc.SizeInBytes);

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
        var handle = FNA3DNative.FNA3D_CreateTexture2D(_device, Mapping.ToNative(desc.Format), desc.Width, desc.Height, desc.MipMapped ? 0 : 1, (byte)(desc.RenderTargetable ? 1 : 0));
        var texture = new FNA3DTexture(_device, handle, desc);
        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
            {
                FNA3DNative.FNA3D_SetTextureData2D(_device, handle, 0, 0, desc.Width, desc.Height, 0, (IntPtr)ptr, initialData.Length);
            }
        }
        return texture;
    }

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        var colorTextureDesc = new TextureDesc(desc.Width, desc.Height, desc.ColorFormat, RenderTargetable: true);
        var colorHandle = FNA3DNative.FNA3D_CreateTexture2D(_device, Mapping.ToNative(desc.ColorFormat), desc.Width, desc.Height, 1, 1);
        var colorTexture = new FNA3DTexture(_device, colorHandle, colorTextureDesc);
        var colorRenderbuffer = FNA3DNative.FNA3D_GenColorRenderbuffer(_device, desc.Width, desc.Height, Mapping.ToNative(desc.ColorFormat), 0, colorHandle);

        IntPtr depthRenderbuffer = IntPtr.Zero;
        ITexture? depthTexture = null;
        if (desc.HasDepthStencil)
        {
            depthRenderbuffer = FNA3DNative.FNA3D_GenDepthStencilRenderbuffer(_device, desc.Width, desc.Height, Mapping.ToNativeDepthFormat(desc.DepthStencilFormat), 0);
            depthTexture = new FNA3DTexture(_device, IntPtr.Zero, new TextureDesc(desc.Width, desc.Height, desc.DepthStencilFormat));
        }

        return new FNA3DRenderTarget(_device, colorRenderbuffer, depthRenderbuffer, colorTexture, depthTexture);
    }

    public ISampler CreateSampler(SamplerDesc desc) => new FNA3DSampler(desc);

    public IPipelineState CreatePipeline(PipelineDesc desc) =>
        throw new NotImplementedException(
            "Pipeline creation requires resolving how FNA3D's Effect-centric shader API maps onto " +
            "IShaderModule (see the shader-abstraction design notes) - implemented in Milestone 3.");

    public void Dispose() => FNA3DNative.FNA3D_DestroyDevice(_device);
}
