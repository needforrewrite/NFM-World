// LLM maintained.
//
// The IGraphicsDevice implementation: the D3D11 device, its immediate context, and resource creation.
//
// Structurally this is the closest of the three backends to sokol's - a real device object, real
// handle-owning resources, a command buffer that could have been deferred - but the ownership model
// is COM's rather than a handle table's, so there is no registry here and no deletion queue. What
// the device owns outright is the device and the context; everything else is owned by whoever
// created it and released by D3D11Interop.Release when its Dispose runs.
//
// The device is not created here. It is handed in, because this backend is designed to share one with
// whatever D3D11 the host already has (FNA3D's driver, sokol's D3D11 backend, SDL's ImGui renderer) -
// two devices on one window would each have their own swapchain and neither would see the other's
// frames. See D3D11DeviceDescription.
using NFMWorld.Shaders;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace NFMWorld.Graphics.D3D11;

/// <summary>
/// How to bring up the Direct3D 11 device and the swapchain over a Win32 window.
/// </summary>
/// <remarks>
/// The window is an <c>HWND</c>, not an SDL handle, because DXGI has no other way to attach a
/// swapchain - <c>NFMWorld.Platform.SDL3.SdlWindow.NativeWindowHandle</c> is where the app gets it.
/// Keeping the conversion at the call site means this project takes no SDL dependency at all.
/// </remarks>
/// <param name="WindowHandle">The Win32 <c>HWND</c> the swapchain presents into.</param>
/// <param name="Width">Back buffer width in pixels, which is the window's client area rather than its outer size.</param>
/// <param name="Height">Back buffer height in pixels.</param>
/// <param name="MultiSampleCount">
/// Requested sample count for the back buffer. Zero and one both mean no multisampling; anything
/// above one is clamped to the highest count the device supports for the back buffer's format, and
/// the count actually allocated is what <see cref="ISwapchain.MultiSampleCount"/> then reports.
/// </param>
/// <param name="EnableDebugLayer">
/// Whether to ask for <c>D3D11_CREATE_DEVICE_DEBUG</c> and fail if it is unavailable. Deliberately
/// opt-in rather than automatic: the debug layer needs the Graphics Tools optional feature
/// installed, and a machine without it fails device creation outright rather than falling back -
/// which would turn a diagnostic into an inability to launch.
/// </param>
/// <param name="DepthStencilFormat">The depth-stencil format the swapchain's own automatic depth buffer is not used for; see <see cref="D3D11Swapchain"/>.</param>
public readonly record struct D3D11DeviceDescription(
    nint WindowHandle,
    int Width,
    int Height,
    int MultiSampleCount = 0,
    bool EnableDebugLayer = false,
    TextureFormat DepthStencilFormat = TextureFormat.Depth24Stencil8);

/// <summary>
/// A Direct3D 11 rendering device over a single <c>HWND</c>-owned swapchain.
/// </summary>
public sealed unsafe class D3D11GraphicsDevice : IGraphicsDevice, IDisposable
{
    /// <summary>
    /// The device. Accessed as a field rather than through a property by the resource types, which
    /// are all in this assembly and take it once to create their objects.
    /// </summary>
    internal ID3D11Device* Device;

    /// <summary>The immediate context - the one and only command buffer's target.</summary>
    internal ID3D11DeviceContext* Context;

    private readonly D3D11Swapchain _swapchain;
    private D3D11CommandBuffer? _activeCommandBuffer;
    private bool _disposed;

    public ISwapchain Swapchain => _swapchain;

    /// <summary>
    /// The swapchain, typed. The draw path needs the multisampled colour view it renders into and the
    /// depth view it clears, and neither is on <see cref="ISwapchain"/>.
    /// </summary>
    internal D3D11Swapchain D3d11Swapchain => _swapchain;

    /// <summary>
    /// False. D3D's framebuffer origin is the top left, and - unlike the GL backends' Y-flipped
    /// viewport - nothing here needs to pretend otherwise: the generated HLSL is the same source
    /// the two D3D11-capable backends already use, and the abstraction's conventions were written
    /// against FNA3D, which is D3D11 on Windows.
    /// </summary>
    public bool HasBottomLeftFramebufferOrigin => false;

    /// <summary>
    /// The feature level the device came up at.
    ///
    /// Reported by the smoke test and useful in a crash log; nothing in the draw path branches on it,
    /// because the shaders are all compiled for <c>vs_5_0</c>/<c>ps_5_0</c> and a device below
    /// feature level 11 would have failed creation rather than degraded.
    /// </summary>
    internal D3D_FEATURE_LEVEL FeatureLevel { get; }

    private D3D11GraphicsDevice(
        ID3D11Device* device, ID3D11DeviceContext* context, D3D_FEATURE_LEVEL featureLevel,
        nint windowHandle, int width, int height, int multiSampleCount, TextureFormat depthStencilFormat)
    {
        Device = device;
        Context = context;
        FeatureLevel = featureLevel;
        _swapchain = new D3D11Swapchain(this, windowHandle, width, height, multiSampleCount, depthStencilFormat);
    }

    /// <summary>
    /// Brings up a device, its immediate context, and a swapchain over <paramref name="windowHandle"/>.
    /// </summary>
    /// <remarks>
    /// The device is created on its own and the factory is created separately, rather than going
    /// through <c>D3D11CreateDeviceAndSwapChain</c>. Two reasons, and the second is the important one:
    /// the combined call cannot create a flip-model swapchain at all (it has no swap-effect parameter
    /// below <c>DXGI_SWAP_CHAIN_DESC</c>, whose flip-model arm is unusable), and creating the device
    /// first is what lets this backend probe the adapter for tearing support before it commits to a
    /// swapchain that may not allow it.
    ///
    /// <c>D3D11_CREATE_DEVICE_SINGLETHREADED</c> is deliberately not passed. It is a promise that the
    /// device will only ever be touched from one thread, and both it and its context would then be
    /// left with no internal synchronisation - a legal and slightly faster configuration for a
    /// backend that owns its device outright. This one does not: on the integration path the same
    /// device is what SDL's ImGui renderer draws through, and the promise would be broken the first
    /// time that renderer ran on a different thread.
    /// </remarks>
    public static D3D11GraphicsDevice Create(in D3D11DeviceDescription description)
    {
        if (description.WindowHandle == 0)
        {
            throw new ArgumentException(
                "A zero window handle cannot host a DXGI swapchain. On the SDL path this is " +
                "SdlWindow.NativeWindowHandle, which is zero only when SDL did not create the window " +
                "through the Win32 video driver.", nameof(description));
        }

        if (description.Width <= 0 || description.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description), $"A {description.Width}x{description.Height} back buffer cannot be created.");
        }

        if (!OperatingSystem.IsWindows())
        {
            // Not a capability check - the whole backend is Win32-only, since TerraFX.Interop.Windows
            // is P/Invoking user32 and d3d11 directly. Failing here with that sentence beats an
            // EntryPointNotFoundException from inside the loader.
            throw new PlatformNotSupportedException(
                "This backend binds Direct3D 11 and DXGI through TerraFX.Interop.Windows and only runs on Windows.");
        }

        var flags = description.EnableDebugLayer ? (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_DEBUG : 0u;

        // Ordered most-capable first and passed as an array rather than a single value: D3D11CreateDevice
        // takes the first entry the hardware can honour and reports back which one that was, and a
        // machine whose driver tops out at 10_1 should get a working device rather than a failure.
        // 11_0 is the floor the rest of this backend assumes - 11_1 exists but is not required by
        // anything here, so it is offered first and is optional.
        var levels = stackalloc D3D_FEATURE_LEVEL[]
        {
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
        };

        ID3D11Device* device = null;
        ID3D11DeviceContext* context = null;
        D3D_FEATURE_LEVEL level;

        D3D11Interop.Check(
            DirectX.D3D11CreateDevice(
                null,
                D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
                // Only consulted by D3D_DRIVER_TYPE_SOFTWARE, which is not requested above.
                HMODULE.NULL,
                flags,
                levels,
                (uint)4,
                D3D11Interop.SdkVersion,
                &device,
                &level,
                &context),
            description.EnableDebugLayer
                ? "D3D11CreateDevice (hardware, 11.1-10.0, debug layer)"
                : "D3D11CreateDevice (hardware, 11.1-10.0)",
            description.EnableDebugLayer
                ? "The debug layer requires the Graphics Tools optional Windows feature; install it or " +
                  "create the device with EnableDebugLayer: false."
                : null);

        try
        {
            var result = new D3D11GraphicsDevice(
                device, context, level, description.WindowHandle,
                description.Width, description.Height, description.MultiSampleCount, description.DepthStencilFormat);
            return result;
        }
        catch
        {
            // The swapchain's constructor is the only thing that can fail here, and it does not take
            // ownership of either object until it succeeds.
            D3D11Interop.Release(ref context);
            D3D11Interop.Release(ref device);
            throw;
        }
    }

    public ICommandBuffer AcquireCommandBuffer()
    {
        if (_activeCommandBuffer is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(AcquireCommandBuffer)} was called again before the previous command buffer " +
                "was submitted - only one command buffer may be live at a time.");
        }

        var commandBuffer = new D3D11CommandBuffer(this);
        _activeCommandBuffer = commandBuffer;
        return commandBuffer;
    }

    /// <summary>
    /// Ends the frame.
    ///
    /// D3D11 is immediate-mode in the same sense GL is: <c>ID3D11DeviceContext</c> executes as it is
    /// called, so there is nothing recorded to replay and this has nothing to do but invalidate the
    /// command buffer's per-frame state and hand its constant buffer back for reuse. Presenting is
    /// the caller's next call (<see cref="D3D11Swapchain.Present"/>), not this one - the abstraction
    /// splits them and the app relies on that split to run sokol's frame boundary in between.
    ///
    /// The command buffer's objects are pooled rather than recreated per frame, because a frame's
    /// worth of them is several hundred COM object creations otherwise, and because a buffer's
    /// contents are stale-but-unused rather than wrong. See <see cref="D3D11CommandBuffer.Reset"/>.
    /// </summary>
    public void Submit(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not D3D11CommandBuffer d3dCommandBuffer)
        {
            throw new ArgumentException(
                $"{nameof(Submit)} needs a command buffer this backend created.", nameof(commandBuffer));
        }

        if (!ReferenceEquals(d3dCommandBuffer, _activeCommandBuffer))
        {
            throw new InvalidOperationException(
                "This command buffer is not the live one - it was either already submitted or never acquired.");
        }

        d3dCommandBuffer.Reset();
        _activeCommandBuffer = null;
    }

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        if (desc.SizeInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A buffer of {desc.SizeInBytes} bytes cannot be allocated.");

        if (initialData.Length > desc.SizeInBytes)
        {
            throw new ArgumentException(
                $"Initial data holds {initialData.Length} bytes but the buffer is {desc.SizeInBytes} bytes.",
                nameof(initialData));
        }

        // A dynamic buffer is written through Map(WRITE_DISCARD), which is only legal on a
        // DYNAMIC-usage resource, so a caller can never upload into a non-dynamic buffer after
        // creation. That is a real restriction of this API and not one this backend invents - but a
        // non-dynamic buffer declared without contents is still constructible, and is how the two
        // deferred-upload renderers in this tree build their buffers before the first UpdateBuffer.
        return new D3D11Buffer(this, desc, initialData);
    }

    public ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A {desc.Width}x{desc.Height} texture cannot be allocated.");

        if (desc.MipMapped && !initialData.IsEmpty)
        {
            throw new NotSupportedException(
                "A mipmapped texture cannot be created with initial data: D3D11 has no mip-chain " +
                "initialiser, so every level past 0 would be undefined. Create it single-level, or " +
                "create it empty and fill it through the command buffer.");
        }

        return new D3D11Texture(this, desc, initialData);
    }

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A {desc.Width}x{desc.Height} render target cannot be allocated.");

        return new D3D11RenderTarget(this, desc);
    }

    public ISampler CreateSampler(SamplerDesc desc) => new D3D11Sampler(this, desc);

    public IPipelineState CreatePipeline(PipelineDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc.VertexShader);
        ArgumentNullException.ThrowIfNull(desc.PixelShader);

        var vertex = RequireModule(desc.VertexShader, "VertexShader");
        var pixel = RequireModule(desc.PixelShader, "PixelShader");

        // The two halves of a pipeline are usually the same module - the app passes each bundle's
        // module twice - but unlike GL there is no requirement that they be, because D3D11 creates
        // the two stages as independent objects and the reflection describes the merged block the
        // shader compiler emits for both. Taking the vertex module's reflection for SetUniform is a
        // consequence of that merge, not of the two being the same object.
        return new D3D11PipelineState(this, vertex, pixel, desc);
    }

    private static D3D11ShaderModule RequireModule(IShaderModule module, string name)
    {
        if (module is not D3D11ShaderModule d3dModule)
        {
            throw new ArgumentException(
                $"{name} must be a shader module this backend created - see {nameof(LoadShaderModule)}.", nameof(module));
        }

        return d3dModule;
    }

    /// <inheritdoc cref="IGraphicsDevice.CreateShaderModule"/>
    IShaderModule IGraphicsDevice.CreateShaderModule(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection) =>
        LoadShaderModule(this, vertex, pixel, reflection);

    /// <summary>
    /// Compiles a generated bundle's HLSL into the two stage objects a pipeline binds.
    ///
    /// The HLSL and nothing else: the bundle carries SPIR-V, MSL and three GLSL flavours for the
    /// other backends, and the HLSL is the form <c>fxc</c> consumes directly. It is the same source
    /// the sokol backend's D3D11 build compiles, which is why the semantic-name contract in
    /// <see cref="D3D11PipelineState"/> is a property of these bundles rather than of this backend.
    /// </summary>
    public static IShaderModule LoadShaderModule(
        D3D11GraphicsDevice device, ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        ArgumentNullException.ThrowIfNull(pixel);
        ArgumentNullException.ThrowIfNull(reflection);

        if (string.IsNullOrWhiteSpace(vertex.Hlsl) || string.IsNullOrWhiteSpace(pixel.Hlsl))
        {
            throw new ArgumentException(
                "The bundle carries no HLSL for one or both stages. This backend compiles " +
                "ShaderStageSources.Hlsl; a bundle generated before that field existed cannot be used.",
                nameof(vertex));
        }

        return new D3D11ShaderModule(device, vertex, pixel, reflection);
    }

    /// <summary>
    /// Reads pixels back off the GPU through a staging resource.
    /// </summary>
    /// <remarks>
    /// D3D11 has no direct readback, so the path is three steps: a <c>STAGING</c> resource in the same
    /// format and size as the source, a <c>CopySubresourceRegion</c> of the requested rectangle into
    /// it, and a <c>Map(READ)</c> over the result. The copy is what makes this a full GPU stall, which
    /// is why the abstraction documents it as editor/export-only.
    ///
    /// The rows are <em>not</em> flipped. D3D11's texture origin and its row order agree, so bytes
    /// uploaded through <c>UpdateTexture</c> come back in the order they were written - the same
    /// round-trip property the GL backend preserves for the same reason.
    ///
    /// The staged copy is taken at the texture's own row pitch, which is generally wider than the
    /// row's meaningful bytes, so each row is copied out separately rather than as one block. For a
    /// region narrower than the texture that padding is the difference between a correct image and a
    /// sheared one.
    /// </remarks>
    public void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (texture is not D3D11Texture d3dTexture)
            throw new ArgumentException($"{nameof(ReadTexture)} needs a texture this backend created.", nameof(texture));

        if (d3dTexture.Format.IsDepthFormat())
        {
            throw new NotSupportedException(
                $"{d3dTexture.Format} has no colour storage to read back. Read the render target's " +
                $"{nameof(IRenderTarget.ColorTexture)} instead - that is what the abstraction documents.");
        }

        if (d3dTexture.SampleCount > 1)
        {
            throw new NotSupportedException(
                $"A {d3dTexture.SampleCount}x multisampled texture cannot be copied into a single-sampled " +
                "staging resource. Resolve it into a single-sampled target first.");
        }

        if (d3dTexture.MipMapped)
        {
            throw new NotSupportedException(
                "This backend allocates its mip chain but only ever reads level 0, and the staging " +
                "resource below is sized for a single level. Read a single-level texture.");
        }

        if (level != 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Only level 0 is readable through this backend.");

        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > d3dTexture.Width || y + height > d3dTexture.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x),
                $"The {width}x{height} region at ({x}, {y}) does not fit the {d3dTexture.Width}x{d3dTexture.Height} texture.");
        }

        var rowBytes = width * BytesPerPixel(d3dTexture.Format);
        var required = rowBytes * height;
        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"Destination holds {destination.Length} bytes; {required} are required for {width}x{height} " +
                $"of {d3dTexture.Format}.", nameof(destination));
        }

        var stagingDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = d3dTexture.Format.ToDxgiFormat(),
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = D3D11Flags.CpuAccessRead,
            MiscFlags = 0,
        };

        ID3D11Texture2D* staging = null;
        D3D11Interop.Check(
            Device->CreateTexture2D(&stagingDesc, null, &staging),
            $"CreateTexture2D (staging, {width}x{height} {d3dTexture.Format})");

        try
        {
            // The source box is in the texture's own coordinates and the destination is the staging
            // resource's origin, so the copy lands as a correctly aligned top-left rectangle.
            var box = new D3D11_BOX
            {
                left = (uint)x,
                top = (uint)y,
                front = 0,
                right = (uint)(x + width),
                bottom = (uint)(y + height),
                back = 1,
            };

            Context->CopySubresourceRegion(
                (ID3D11Resource*)staging, 0, 0, 0, 0, (ID3D11Resource*)d3dTexture.Texture, 0, &box);

            D3D11_MAPPED_SUBRESOURCE mapped;
            D3D11Interop.Check(
                Context->Map((ID3D11Resource*)staging, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped),
                "Map (texture readback)");

            try
            {
                for (var row = 0; row < height; row++)
                {
                    var source = (byte*)mapped.pData + (nuint)row * mapped.RowPitch;
                    new ReadOnlySpan<byte>(source, rowBytes).CopyTo(destination[(row * rowBytes)..]);
                }
            }
            finally
            {
                Context->Unmap((ID3D11Resource*)staging, 0);
            }
        }
        finally
        {
            D3D11Interop.Release(ref staging);
        }
    }

    /// <summary>
    /// The format's bytes per texel, for the readback arithmetic.
    ///
    /// Block-compressed formats have no whole number here, so they are rejected by name rather than
    /// given a rounded-up answer that would produce a plausible-looking, wrong copy.
    /// </summary>
    private static int BytesPerPixel(TextureFormat format)
    {
        if (format.IsBlockCompressed())
        {
            throw new NotSupportedException(
                $"{format} is block-compressed, so its rows are measured in 4x4 blocks rather than " +
                "texels and the tightly packed readback contract has no meaning for it. Decompress it " +
                "through a draw instead.");
        }

        return format switch
        {
            TextureFormat.Rgba8 or TextureFormat.Bgra8 => 4,
            TextureFormat.R8 => 1,
            TextureFormat.Single => 4,
            TextureFormat.Rgba32f => 16,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    /// <summary>
    /// Whether the device can honour a request for this sample count on this format, and what count
    /// it would really allocate. Consulted by the swapchain, which is the only thing here that
    /// multisamples.
    /// </summary>
    internal int ClampSampleCount(DXGI_FORMAT format, int requested)
    {
        if (requested <= 1)
            return 1;

        uint qualityLevels;
        if (Device->CheckMultisampleQualityLevels(format, (uint)requested, &qualityLevels) < 0 || qualityLevels == 0)
            return 1;

        return requested;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // The command buffer holds a constant buffer and a depth-stencil object directly, so it is
        // disposed rather than only dropped - the device created them out of its own Device, and
        // releasing the device with them still referenced is what the debug layer reports as a leak.
        _activeCommandBuffer?.Dispose();
        _activeCommandBuffer = null;

        // The swapchain next: it holds views over the back buffers, and a view outliving the
        // resource it points into is a dangling reference the debug layer reports at device teardown.
        _swapchain.Dispose();
        D3D11Interop.Release(ref Context);
        D3D11Interop.Release(ref Device);
    }
}
