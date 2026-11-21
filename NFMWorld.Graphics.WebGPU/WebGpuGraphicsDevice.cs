// LLM maintained.
//
// The IGraphicsDevice implementation over wgpu-native through Silk.NET's bindings.
//
// Three things about WebGPU shape every other file in this assembly, so they are stated once here:
//
//   - The API is asynchronous by design. Adapter and device creation are callbacks, and wgpu-native
//     invokes them before it returns - so the waits below are not a busy poll, they are the
//     callback having already run.
//   - Pipeline state is baked, including the render target's formats and sample count. The
//     abstraction creates a pipeline without knowing what it will be drawn into, so
//     WebGpuPipelineState builds variants lazily per (format, sample count, depth, bindings).
//   - There is no default framebuffer. "Target null" means the swapchain's own colour attachment,
//     which this backend owns as an offscreen texture when multisampling, and which is acquired
//     once per frame rather than per draw.

using System.Runtime.InteropServices;
using NFMWorld.Shaders;
using Silk.NET.Core.Contexts;
using Silk.NET.WebGPU.Extensions.WGPU;
using Wg = Silk.NET.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using WgpuDevice = Silk.NET.WebGPU.Device;
using WgpuSurface = Silk.NET.WebGPU.Surface;
using WgpuTexture = Silk.NET.WebGPU.Texture;

namespace NFMWorld.Graphics.WebGPU;

/// <summary>
/// How to bring up the WebGPU device and the surface over a native window.
/// </summary>
/// <param name="WindowHandle">
/// The window's native handle - <c>SDL_PROP_WINDOW_WIN32_HWND_POINTER</c> through
/// <c>NFMWorld.Platform.SDL3.SdlWindow.NativeWindowHandle</c>. Zero means no presentation surface
/// at all: the device still renders, into an offscreen colour attachment, which is what the smoke
/// harness runs against on a machine with no window.
/// </param>
/// <param name="Width">Back buffer width in pixels.</param>
/// <param name="Height">Back buffer height in pixels.</param>
/// <param name="MultiSampleCount">
/// Requested sample count for the back buffer. WebGPU surfaces are single-sampled in core, so
/// anything above one renders into an offscreen multisampled colour attachment that is resolved
/// into the surface at present time - the same structure the D3D11 backend uses.
/// </param>
/// <param name="EnableValidation">
/// Whether an uncaptured-error callback is installed and reported to
/// <see cref="GraphicsDiagnostics.Warning"/>. On by default: wgpu-native's validation messages name
/// the exact descriptor field that is wrong, and without a callback they are only logged to stderr,
/// where a game launched from the desktop never sees them.
/// </param>
/// <param name="DepthStencilFormat">The depth-stencil format the swapchain's automatic depth buffer takes.</param>
public readonly record struct WebGpuDeviceDescription(
    nint WindowHandle,
    int Width,
    int Height,
    int MultiSampleCount = 0,
    bool EnableValidation = true,
    TextureFormat DepthStencilFormat = TextureFormat.Depth24Stencil8
);

/// <summary>
/// The WebGPU device, its queue, and everything created from them.
/// </summary>
public sealed unsafe class WebGpuGraphicsDevice : IGraphicsDevice
{
    private const int SurfaceWidthFallback = 1280;
    private const int SurfaceHeightFallback = 720;

    /// <summary>The API surface, shared by every object in this assembly.</summary>
    internal Wg.WebGPU Api { get; }

    /// <summary>wgpu-native's own extension, needed for <c>DevicePoll</c>.</summary>
    internal Wgpu Ext { get; }

    internal Wg.Instance* Instance;
    internal Wg.Adapter* Adapter;

    private WgpuDevice* _device;
    private Wg.Queue* _queue;

    private WgpuSurface* _surface;
    private Wg.TextureFormat _surfaceFormat = Wg.TextureFormat.Bgra8Unorm;

    private readonly WebGpuSwapchain _swapchain;

    /// <summary>The multisampled colour attachment the swapchain allocates, or its single-sample stand-in.</summary>
    internal Wg.TextureFormat SurfaceFormat => _surfaceFormat;

    internal WgpuDevice* Device => _device;
    internal Wg.Queue* QueueInternal => _queue;
    internal WgpuSurface* SurfacePointer => _surface;

    /// <summary>
    /// The alignment a uniform block's dynamic offset must respect. Queried from the adapter rather
    /// than assumed 256: it is the one limit every device reports differently, and a wrong value is
    /// a validation error per draw rather than a warning.
    /// </summary>
    internal ulong UniformAlignment { get; }

    internal bool Float32Filterable { get; }
    internal bool TextureCompressionBc { get; }

    /// <summary>Per-draw uniform blocks, written into per-frame chunks.</summary>
    internal WebGpuDynamicBuffer Uniforms { get; }

    /// <summary>Copy sources for buffer and texture uploads, which have to be ordered against draws.</summary>
    internal WebGpuDynamicBuffer Staging { get; }

    /// <summary>A 1x1 white texture, bound wherever a declared texture has none - see the bind group comment.</summary>
    internal WebGpuTexture DummyTexture { get; }

    internal WebGpuSampler DummySampler { get; }

    private WebGpuCommandBuffer? _activeCommandBuffer;

    /// <summary>
    /// Releases and destroys waiting for the submit that could still reference them.
    /// </summary>
    /// <remarks>
    /// Two hazards, both from the frame being one command buffer submitted at the end. wgpu's destroy
    /// is not a refcount drop: it invalidates the resource for every submission made after it, and a
    /// command buffer that recorded a draw against the resource and is submitted afterwards fails
    /// validation and panics the process. That happens for real - Apos' glyph atlas grows mid-frame,
    /// disposes the old atlas texture and keeps drawing, and ImmediateBatch3D replaces its vertex
    /// buffer the same way. The other is id vacancy: a pass stores a bind group's, texture view's and
    /// pipeline's id and resolves them when the pass *ends*, so releasing one an open pass already
    /// recorded panics on a vacant slot. Both are deferred to just after the next submit, when
    /// nothing can be recording and wgpu's own submission refcount keeps the memory valid.
    /// </remarks>
    private readonly List<Action> _pendingDestroys = [];

    internal void DeferDestroy(Action destroy) => _pendingDestroys.Add(destroy);

    private void FlushPendingDestroys()
    {
        if (_pendingDestroys.Count == 0)
            return;

        foreach (var destroy in _pendingDestroys)
            destroy();
        _pendingDestroys.Clear();
    }

    private readonly Wg.ErrorCallback? _errorCallback;
    private readonly Wg.PfnErrorCallback? _errorCallbackPointer;

    private readonly HashSet<string> _reportedErrors = [];
    private byte[] _copyPadding = new byte[64];
    private string? _lastError;

    /// <summary>The last validation error the device reported, if any. Cleared by reading it through <see cref="TakeError"/>.</summary>
    internal string? TakeError()
    {
        var error = _lastError;
        _lastError = null;
        return error;
    }

    /// <summary>
    /// Records an uncaptured error and reports it to the host's diagnostics sink, once per distinct
    /// message: a draw that fails validation fails it every frame, and a per-frame log line would
    /// drown the boot log it is there to explain.
    /// </summary>
    private void ReportError(string message)
    {
        if (_reportedErrors.Count < 64 && _reportedErrors.Add(message))
            GraphicsDiagnostics.Warning?.Invoke($"webgpu: {message}");
        _lastError = message;
    }

    public ISwapchain Swapchain => _swapchain;

    internal WebGpuSwapchain SwapchainInternal => _swapchain;

    /// <summary>False: WebGPU's framebuffer origin is the top left, like D3D11's.</summary>
    public bool HasBottomLeftFramebufferOrigin => false;

    public bool SupportsBaseVertexDraw => true;

    /// <summary>Human-readable adapter identity, for the boot log and the smoke harness.</summary>
    public string AdapterDescription { get; } = "";

    private WebGpuGraphicsDevice(
        Wg.WebGPU api,
        Wgpu ext,
        Wg.Instance* instance,
        Wg.Adapter* adapter,
        WgpuDevice* device,
        Wg.Queue* queue,
        WgpuSurface* surface,
        Wg.TextureFormat surfaceFormat,
        string adapterDescription,
        ulong uniformAlignment,
        bool float32Filterable,
        bool textureCompressionBc,
        in WebGpuDeviceDescription description
    )
    {
        Api = api;
        Ext = ext;
        Instance = instance;
        Adapter = adapter;
        _device = device;
        _queue = queue;
        _surface = surface;
        _surfaceFormat = surfaceFormat;
        AdapterDescription = adapterDescription;
        UniformAlignment = uniformAlignment;
        Float32Filterable = float32Filterable;
        TextureCompressionBc = textureCompressionBc;

        // Installed after the device exists because that is when wgpu-native starts validating.
        if (description.EnableValidation)
        {
            _errorCallback = (type, message, _) => ReportError($"{type}: {WebGpuInterop.Read(message)}");
            if (Wg.PfnErrorCallback.From(_errorCallback) is { } callbackPointer)
            {
                _errorCallbackPointer = callbackPointer;
                Api.DeviceSetUncapturedErrorCallback(_device, callbackPointer, null);
            }
        }

        Uniforms = new WebGpuDynamicBuffer(this, "uniform ring", Wg.BufferUsage.Uniform, uniformAlignment, 256 * 1024);
        Staging = new WebGpuDynamicBuffer(this, "staging ring", Wg.BufferUsage.None, 4, 4 * 1024 * 1024);

        DummyTexture = new WebGpuTexture(this, 1, 1, TextureFormat.Rgba8, renderTargetable: false, mipMapped: false);
        Span<byte> white = stackalloc byte[4] { 255, 255, 255, 255 };
        UploadTexture(DummyTexture, 0, 0, 1, 1, white);

        DummySampler = new WebGpuSampler(
            this,
            new SamplerDesc(TextureFilter.Point, TextureAddressMode.Clamp, TextureAddressMode.Clamp)
        );

        _swapchain = new WebGpuSwapchain(
            this,
            description.Width,
            description.Height,
            description.MultiSampleCount,
            description.DepthStencilFormat
        );
    }

    /// <summary>
    /// Brings up an adapter, a device and - when a window handle is given - the presentation surface.
    /// </summary>
    public static WebGpuGraphicsDevice Create(in WebGpuDeviceDescription description)
    {
        if (description.Width <= 0 || description.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description),
                $"A {description.Width}x{description.Height} back buffer cannot be created."
            );
        }

        var api = Wg.WebGPU.GetApi();
        var ext = new Wgpu(Wg.WebGPU.CreateDefaultContext(new[] { "wgpu_native" }));

        var instanceDescriptor = default(Wg.InstanceDescriptor);
        var instance = api.CreateInstance(&instanceDescriptor);
        if (instance is null)
            throw new InvalidOperationException("wgpu-native could not create an instance.");

        var surface = CreateSurface(api, instance, description);
        var instance2 = instance;
        var surface2 = surface;

        // The adapter must be told about the surface before it is chosen, or the one wgpu-native
        // picks can have no presentation support for it at all and every configure fails.
        var adapterOptions = new Wg.RequestAdapterOptions
        {
            CompatibleSurface = surface2,
            PowerPreference = Wg.PowerPreference.HighPerformance,
            BackendType = Wg.BackendType.Undefined,
        };

        Wg.Adapter* adapter = null;
        string? adapterError = null;
        using var adapterReady = new ManualResetEventSlim(false);
        var adapterCallback = Wg.PfnRequestAdapterCallback.From((status, candidate, message, _) =>
            {
                if (status == Wg.RequestAdapterStatus.Success)
                    adapter = candidate;
                else
                    adapterError = WebGpuInterop.Read(message) ?? status.ToString();
                adapterReady.Set();
            }
        );

        api.InstanceRequestAdapter(instance2, &adapterOptions, adapterCallback, null);
        adapterReady.Wait(TimeSpan.FromSeconds(10));

        if (adapter is null)
        {
            throw new InvalidOperationException(
                "wgpu-native found no usable adapter"
                + (adapterError is null ? "." : $": {adapterError}")
                + " On Windows this is a Vulkan or D3D12 driver problem - WebGPU is not available on a "
                + "machine with neither."
            );
        }

        var properties = default(Wg.AdapterProperties);
        api.AdapterGetProperties(adapter, &properties);
        var adapterName = WebGpuInterop.Read(properties.Name) ?? "unknown adapter";

        var supportedLimits = default(Wg.SupportedLimits);
        if (!api.AdapterGetLimits(adapter, &supportedLimits))
            throw new InvalidOperationException("wgpu-native did not report the adapter's limits.");

        // BC textures are how every RAD model carries its art, and float32 filtering is what the
        // MSDF atlas samples with. Both are optional features rather than core, so each is requested
        // only when the adapter advertises it - asking for one it lacks fails device creation.
        var wantsBc = api.AdapterHasFeature(adapter, Wg.FeatureName.TextureCompressionBC);
        var wantsFloat32Filterable = api.AdapterHasFeature(adapter, Wg.FeatureName.Float32filterable);

        var features = stackalloc Wg.FeatureName[2];
        var featureCount = 0;
        if (wantsBc)
            features[featureCount++] = Wg.FeatureName.TextureCompressionBC;
        if (wantsFloat32Filterable)
            features[featureCount++] = Wg.FeatureName.Float32filterable;

        // The adapter's own limits are requested back, which is the documented way to ask for
        // everything it can do without naming each field.
        var requiredLimits = new Wg.RequiredLimits { Limits = supportedLimits.Limits };

        var deviceDescriptor = new Wg.DeviceDescriptor
        {
            RequiredFeatureCount = (nuint)featureCount,
            RequiredFeatures = features,
            RequiredLimits = &requiredLimits,
        };

        WgpuDevice* device = null;
        string? deviceError = null;
        using var deviceReady = new ManualResetEventSlim(false);
        var deviceCallback = Wg.PfnRequestDeviceCallback.From((status, created, message, _) =>
            {
                if (status == Wg.RequestDeviceStatus.Success)
                    device = created;
                else
                    deviceError = WebGpuInterop.Read(message) ?? status.ToString();
                deviceReady.Set();
            }
        );

        api.AdapterRequestDevice(adapter, &deviceDescriptor, deviceCallback, null);
        deviceReady.Wait(TimeSpan.FromSeconds(20));

        if (device is null)
        {
            api.AdapterRelease(adapter);
            api.InstanceRelease(instance);
            throw new InvalidOperationException(
                "wgpu-native could not create a device"
                + (deviceError is null ? "." : $": {deviceError}")
            );
        }

        var queue = api.DeviceGetQueue(device);
        var surfaceFormat = surface2 is null ? Wg.TextureFormat.Bgra8Unorm : api.SurfaceGetPreferredFormat(surface2, adapter);

        GraphicsDiagnostics.Warning?.Invoke(
            $"webgpu: {adapterName} ({properties.BackendType}), surface {surfaceFormat}"
            + $", bc={(wantsBc ? "yes" : "no")}, float32-filterable={(wantsFloat32Filterable ? "yes" : "no")}"
            + $", uniform alignment {supportedLimits.Limits.MinUniformBufferOffsetAlignment}"
        );

        return new WebGpuGraphicsDevice(
            api,
            ext,
            instance,
            adapter,
            device,
            queue,
            surface2,
            surfaceFormat,
            adapterName,
            supportedLimits.Limits.MinUniformBufferOffsetAlignment,
            wantsFloat32Filterable,
            wantsBc,
            description
        );
    }

    private static WgpuSurface* CreateSurface(Wg.WebGPU api, Wg.Instance* instance, in WebGpuDeviceDescription description)
    {
        if (description.WindowHandle == 0)
            return null;

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The WebGPU backend creates its surface from a Win32 HWND. The SDL window only exposes "
                + "that pointer today, so on another platform render headless (pass WindowHandle: 0) or "
                + "add the platform's handle accessor first."
            );
        }

        var fromHwnd = new Wg.SurfaceDescriptorFromWindowsHWND
        {
            Chain = new Wg.ChainedStruct { SType = Wg.SType.SurfaceDescriptorFromWindowsHwnd },
            Hinstance = (void*)GetModuleHandleW(null),
            Hwnd = (void*)description.WindowHandle,
        };

        var descriptor = new Wg.SurfaceDescriptor
        {
            NextInChain = (Wg.ChainedStruct*)&fromHwnd,
        };

        var surface = api.InstanceCreateSurface(instance, &descriptor);
        if (surface is null)
            throw new InvalidOperationException("wgpu-native could not create a surface over the window handle.");

        return surface;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandleW(string? moduleName);

    /// <summary>
    /// Throws for a null handle a create call returned, with whatever the validation callback
    /// recorded alongside it. WebGPU reports a failed creation as null and the reason as a separate
    /// callback, so the two have to be joined here or the message is lost.
    /// </summary>
    internal void CheckCreated(bool created, string what, [System.Runtime.CompilerServices.CallerMemberName] string site = "")
    {
        if (created)
            return;

        throw new InvalidOperationException(
            $"WebGPU could not create the {what} ({site}).{(_lastError is null ? "" : "\n" + _lastError)}"
        );
    }

    public ICommandBuffer AcquireCommandBuffer()
    {
        if (_activeCommandBuffer is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(AcquireCommandBuffer)} was called again before the previous command buffer was "
                + "submitted - only one may be live at a time."
            );
        }

        var commandBuffer = new WebGpuCommandBuffer(this);
        _activeCommandBuffer = commandBuffer;
        return commandBuffer;
    }

    public void Submit(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not WebGpuCommandBuffer webGpuCommandBuffer)
        {
            throw new ArgumentException($"{nameof(Submit)} needs a command buffer this backend created.", nameof(commandBuffer));
        }

        if (!ReferenceEquals(webGpuCommandBuffer, _activeCommandBuffer))
        {
            throw new InvalidOperationException(
                "This command buffer is not the live one - it was either already submitted or never acquired."
            );
        }

        webGpuCommandBuffer.Submit();
        _activeCommandBuffer = null;
        FlushPendingDestroys();

        // Every write this frame was recorded against a chunk; the next frame starts filling them
        // again. The GPU may still be reading them, which is safe: wgpu keeps a reference on the
        // submitted command buffer, and a later write only ever lands behind it in queue order.
        Uniforms.Reset();
        Staging.Reset();
    }

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        if (desc.SizeInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A buffer of {desc.SizeInBytes} bytes cannot be allocated.");

        if (initialData.Length > desc.SizeInBytes)
        {
            throw new ArgumentException(
                $"Initial data holds {initialData.Length} bytes but the buffer is {desc.SizeInBytes} bytes.",
                nameof(initialData)
            );
        }

        return new WebGpuBuffer(this, desc, initialData);
    }

    public ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A {desc.Width}x{desc.Height} texture cannot be allocated.");

        var texture = new WebGpuTexture(this, desc.Width, desc.Height, desc.Format, desc.RenderTargetable, desc.MipMapped);

        if (!initialData.IsEmpty)
            UploadTexture(texture, 0, 0, desc.Width, desc.Height, initialData);

        return texture;
    }

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(desc),
                $"A {desc.Width}x{desc.Height} render target cannot be allocated."
            );

        return new WebGpuRenderTarget(this, desc);
    }

    public ISampler CreateSampler(SamplerDesc desc) => new WebGpuSampler(this, desc);

    public IShaderModule CreateShaderModule(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection) =>
        new WebGpuShaderModule(this, vertex, pixel, reflection);

    public IPipelineState CreatePipeline(PipelineDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc.VertexShader);
        ArgumentNullException.ThrowIfNull(desc.PixelShader);

        if (desc.VertexShader is not WebGpuShaderModule vertex)
            throw new ArgumentException($"{nameof(desc)}'s vertex shader was not created by this backend.", nameof(desc));

        var pixelModule = desc.PixelShader as WebGpuShaderModule;

        return new WebGpuPipelineState(this, vertex, pixelModule, desc);
    }

    /// <summary>
    /// Writes into a buffer immediately, in queue order. Used for creation-time data and for the
    /// uniform ring, where the write must precede the submission that reads it; a mid-frame update to
    /// a buffer that is also drawn from goes through <see cref="WebGpuCommandBuffer.UpdateBuffer"/>'s
    /// recorded copy instead.
    /// </summary>
    internal void WriteBuffer(WgpuBuffer* buffer, ulong offset, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        // Copies move whole 4-byte words, so an odd tail - three 16-bit indices, say - is written
        // with the padding <see cref="WebGpuBuffer.GpuSize"/> reserved for it. The padding is never
        // read: it always starts past the buffer's logical size.
        var length = WebGpuInterop.Align(data.Length, 4);
        if (length == data.Length)
        {
            fixed (byte* pointer = data)
                Api.QueueWriteBuffer(_queue, buffer, offset, pointer, (nuint)length);
            return;
        }

        if (_copyPadding.Length < length)
            _copyPadding = new byte[length];

        data.CopyTo(_copyPadding);
        _copyPadding.AsSpan(data.Length, length - data.Length).Clear();
        fixed (byte* pointer = _copyPadding)
            Api.QueueWriteBuffer(_queue, buffer, offset, pointer, (nuint)length);
    }

    /// <summary>
    /// Copies pixel data into a texture through the queue.
    ///
    /// WebGPU requires every row of a multi-row upload to start on a 256-byte boundary, so a tightly
    /// packed source is repacked here when its own row pitch does not already satisfy that. This is
    /// the one place the abstraction's "tightly packed rows" contract and WebGPU's pitch rule differ.
    /// </summary>
    internal void UploadTexture(
        WebGpuTexture texture,
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> data,
        int level = 0
    )
    {
        if (data.IsEmpty)
            return;

        var rowBytes = texture.Format.RowBytes(width);
        var rows = texture.Format.RowCount(height);
        var required = rowBytes * rows;

        if (data.Length < required)
        {
            throw new ArgumentException(
                $"A {width}x{height} {texture.Format} upload needs {required} byte(s); {data.Length} were given.",
                nameof(data)
            );
        }

        var pitch = WebGpuInterop.Align(rowBytes, rows > 1 ? 256 : 4);

        byte[]? padded = null;
        ReadOnlySpan<byte> source;
        if (pitch == rowBytes && data.Length == required)
        {
            source = data;
        }
        else
        {
            padded = new byte[pitch * rows];
            for (var row = 0; row < rows; row++)
                data.Slice(row * rowBytes, rowBytes).CopyTo(padded.AsSpan(row * pitch, rowBytes));
            source = padded;
        }

        var destination = new Wg.ImageCopyTexture
        {
            Texture = texture.Handle,
            MipLevel = (uint)level,
            Origin = new Wg.Origin3D { X = (uint)x, Y = (uint)y, Z = 0 },
            Aspect = Wg.TextureAspect.All,
        };

        var layout = new Wg.TextureDataLayout
        {
            Offset = 0,
            BytesPerRow = (uint)pitch,
            RowsPerImage = (uint)rows,
        };

        var extent = new Wg.Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };

        fixed (byte* pointer = source)
            Api.QueueWriteTexture(_queue, &destination, pointer, (nuint)source.Length, &layout, &extent);
    }

    public void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (texture is not WebGpuTexture webGpuTexture)
            throw new ArgumentException($"{nameof(ReadTexture)} needs a texture this backend created.", nameof(texture));

        if (webGpuTexture.Format.IsDepthFormat())
        {
            throw new NotSupportedException(
                $"{webGpuTexture.Format} has no colour storage to read back. Read the render target's "
                + $"{nameof(IRenderTarget.ColorTexture)} instead."
            );
        }

        if (webGpuTexture.SampleCount > 1)
        {
            throw new NotSupportedException(
                $"A {webGpuTexture.SampleCount}x multisampled texture cannot be copied into a single-sampled "
                + "staging buffer. Resolve it into a single-sampled target first."
            );
        }

        if (webGpuTexture.Format.IsBlockCompressed())
        {
            throw new NotSupportedException(
                $"{webGpuTexture.Format} is block-compressed, and copying a block row back to memory reads "
                + "whole 4x4 blocks rather than texels. Read an uncompressed target instead."
            );
        }

        if (level != 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Only level 0 is readable through this backend.");

        if (x < 0 || y < 0 || width <= 0 || height <= 0
            || x + width > webGpuTexture.Width || y + height > webGpuTexture.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"The {width}x{height} region at ({x}, {y}) does not fit the "
                + $"{webGpuTexture.Width}x{webGpuTexture.Height} texture."
            );
        }

        var rowBytes = webGpuTexture.Format.RowBytes(width);
        var required = rowBytes * height;
        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"Destination holds {destination.Length} bytes; {required} are required for {width}x{height} "
                + $"of {webGpuTexture.Format}.",
                nameof(destination)
            );
        }

        var pitch = WebGpuInterop.Align(rowBytes, 256);
        var stagingSize = pitch * height;

        var bufferDescriptor = new Wg.BufferDescriptor
        {
            Usage = Wg.BufferUsage.CopyDst | Wg.BufferUsage.MapRead,
            Size = (ulong)stagingSize,
        };

        var buffer = Api.DeviceCreateBuffer(_device, &bufferDescriptor);
        CheckCreated(buffer is not null, "readback buffer");

        try
        {
            var encoderDescriptor = default(Wg.CommandEncoderDescriptor);
            var encoder = Api.DeviceCreateCommandEncoder(_device, &encoderDescriptor);

            var source = new Wg.ImageCopyTexture
            {
                Texture = webGpuTexture.Handle,
                MipLevel = 0,
                Origin = new Wg.Origin3D { X = (uint)x, Y = (uint)y, Z = 0 },
                Aspect = Wg.TextureAspect.All,
            };

            var copyDestination = new Wg.ImageCopyBuffer
            {
                Buffer = buffer,
                Layout = new Wg.TextureDataLayout
                {
                    Offset = 0,
                    BytesPerRow = (uint)pitch,
                    RowsPerImage = (uint)height,
                },
            };

            var extent = new Wg.Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };
            Api.CommandEncoderCopyTextureToBuffer(encoder, &source, &copyDestination, &extent);

            var finishDescriptor = default(Wg.CommandBufferDescriptor);
            var commands = Api.CommandEncoderFinish(encoder, &finishDescriptor);
            Api.CommandEncoderRelease(encoder);

            Api.QueueSubmit(_queue, 1, &commands);
            Api.CommandBufferRelease(commands);

            using var mapped = new ManualResetEventSlim(false);
            var status = Wg.BufferMapAsyncStatus.Unknown;
            var mapCallback = Wg.PfnBufferMapCallback.From((result, _) =>
                {
                    status = result;
                    mapped.Set();
                }
            );

            Api.BufferMapAsync(buffer, Wg.MapMode.Read, 0, (nuint)stagingSize, mapCallback, null);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (!mapped.IsSet && DateTime.UtcNow < deadline)
            {
                // Null rather than a zeroed WrappedSubmissionIndex: wgpu-native treats a non-null
                // index with a null queue as "a different queue's submission" and panics in Rust,
                // which takes the process with it. Null polls everything submitted so far.
                Ext.DevicePoll(_device, 1, null);
            }

            if (!mapped.IsSet || status != Wg.BufferMapAsyncStatus.Success)
            {
                throw new InvalidOperationException(
                    $"Mapping the readback buffer failed ({status}){(_lastError is null ? "" : ": " + _lastError)}."
                );
            }

            var mappedRange = (byte*)Api.BufferGetConstMappedRange(buffer, 0, (nuint)stagingSize);
            if (mappedRange is null)
                throw new InvalidOperationException("wgpu-native returned no mapped range for the readback buffer.");

            for (var row = 0; row < height; row++)
                new ReadOnlySpan<byte>(mappedRange + row * (int)pitch, rowBytes)
                    .CopyTo(destination.Slice(row * rowBytes, rowBytes));

            Api.BufferUnmap(buffer);
        }
        finally
        {
            Api.BufferDestroy(buffer);
            Api.BufferRelease(buffer);
        }
    }

    public void Dispose()
    {
        if (Instance is null)
            return;

        FlushPendingDestroys();
        _swapchain.Dispose();
        DummyTexture.Dispose();
        DummySampler.Dispose();
        Uniforms.Dispose();
        Staging.Dispose();

        // Those disposes queue their own releases behind the submit, which will never come.
        FlushPendingDestroys();

        if (_queue is not null)
        {
            Api.QueueRelease(_queue);
            _queue = null;
        }

        Api.DeviceRelease(_device);
        _device = null;

        if (_surface is not null)
        {
            Api.SurfaceRelease(_surface);
            _surface = null;
        }

        Api.AdapterRelease(Adapter);
        Adapter = null;
        Api.InstanceRelease(Instance);
        Instance = null;
    }
}
