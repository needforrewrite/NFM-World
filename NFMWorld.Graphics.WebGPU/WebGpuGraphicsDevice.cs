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
using Maxine.WebGPU.Extensions.Dawn;
using Maxine.WebGPU.Extensions.WGPU;
using Wg = Maxine.WebGPU;
using WgpuBuffer = Maxine.WebGPU.Buffer;
using WgpuDevice = Maxine.WebGPU.Device;
using WgpuSurface = Maxine.WebGPU.Surface;
using WgpuTexture = Maxine.WebGPU.Texture;

namespace NFMWorld.Graphics.WebGPU;

/// <summary>The windowing system a WebGPU surface is created over.</summary>
public enum WebGpuWindowKind
{
    /// <summary>No window: the device renders into an offscreen colour attachment.</summary>
    None,

    /// <summary>Win32. <see cref="WebGpuWindow.Handle"/> is an <c>HWND</c>.</summary>
    Win32,

    /// <summary>X11. <see cref="WebGpuWindow.Display"/> is a <c>Display*</c> and
    /// <see cref="WebGpuWindow.Handle"/> the window's XID.</summary>
    X11,

    /// <summary>Wayland. <see cref="WebGpuWindow.Display"/> is a <c>wl_display*</c> and
    /// <see cref="WebGpuWindow.Handle"/> a <c>wl_surface*</c>.</summary>
    Wayland,

    /// <summary>macOS. <see cref="WebGpuWindow.Handle"/> is a <c>CAMetalLayer*</c>.</summary>
    Metal,
}

/// <summary>
/// The native window a WebGPU surface presents into, tagged with the system that produced it.
/// </summary>
/// <remarks>
/// A record struct of untyped pointers rather than one handle, because each desktop platform wants a
/// different pair and this assembly references no windowing stack to ask. The caller is
/// <c>SdlWindow.NativeHandles</c> through <c>WorldGame</c>, which is the only place that knows both.
/// Fields that do not apply to <see cref="WebGpuWindow.Kind"/> are zero.
/// </remarks>
/// <param name="Kind">Which system the handles belong to.</param>
/// <param name="Handle">The <c>HWND</c>, X11 XID, <c>wl_surface*</c> or <c>CAMetalLayer*</c>.</param>
/// <param name="Display">The X11 <c>Display*</c> or Wayland <c>wl_display*</c> the window belongs to.</param>
public readonly record struct WebGpuWindow(WebGpuWindowKind Kind, nint Handle, nint Display = default)
{
    /// <summary>A device with no presentation surface, which is what a headless run wants.</summary>
    public static WebGpuWindow None => default;
}

/// <summary>
/// How to bring up the WebGPU device and the surface over a native window.
/// </summary>
/// <param name="Window">
/// The window to present into, as <c>NFMWorld.Platform.SDL3.SdlWindow.NativeHandles</c> reports it.
/// <see cref="WebGpuWindowKind.None"/> means no presentation surface at all: the device still
/// renders, into an offscreen colour attachment, which is what the smoke harness runs against on a
/// machine with no window.
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
    WebGpuWindow Window,
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

    /// <summary>
    /// wgpu-native's own extension surface; <see cref="Poll"/> is the only user. Null on the browser,
    /// where emdawnwebgpu does not export <c>wgpuDevicePoll</c> and there is nothing to poll.
    /// </summary>
    private readonly Wgpu? _ext;

    /// <summary>
    /// True when this device runs over emdawnwebgpu in a browser. It changes four things: no adapter
    /// object exists, the surface is created from a canvas selector rather than a window handle,
    /// there is no uncaptured-error callback, and a present is the surface texture simply being
    /// released.
    /// </summary>
    internal bool IsBrowser => _browser;

    private readonly bool _browser;

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

    /// <summary>
    /// Where wgpu's uncaptured errors land. It exists before the device does, because the callback is
    /// part of the descriptor that creates it, so anything reported in between is held until the
    /// constructor can record it.
    /// </summary>
    private sealed class ErrorRouter
    {
        internal WebGpuGraphicsDevice? Target;
        internal readonly List<string> Early = [];

        internal void Report(Wg.ErrorType type, Wg.StringView message)
        {
            var text = $"{type}: {WebGpuInterop.Read(message)}";

            lock (this)
            {
                if (Target is { } target)
                    target.ReportError(text);
                else
                    Early.Add(text);
            }
        }
    }

    private readonly ErrorRouter? _errorRouter;

    /// <summary>The error thunk the descriptor handed over, released with the device.</summary>
    private readonly Wg.PfnUncapturedErrorCallback _errorCallback;

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
    /// <summary>
    /// Drains the device's pending work.
    ///
    /// Most validation is reported when the work executes rather than when it is recorded, and a
    /// headless caller - the smoke harness above all - has no present to drive that. Not part of
    /// <see cref="NFMWorld.Graphics.Abstraction.IGraphicsDevice"/>: the abstraction has no notion of a
    /// queue that needs draining.
    /// </summary>
    internal void Poll(bool waitForWork)
    {
        if (_ext is null)

            return;

        _ext.DevicePoll(_device, waitForWork, null);
    }

    private void ReportError(string message)
    {
        if (_reportedErrors.Count < 64 && _reportedErrors.Add(message))
            GraphicsDiagnostics.Warning?.Invoke($"webgpu: {message}");
        _lastError = message;
    }

    /// <summary>
    /// Surface operations report through a status rather than an uncaptured error, so the swapchain
    /// needs its own way in. Same de-duplication: a surface that stays lost would log every frame.
    /// </summary>
    internal void ReportSurfaceError(string message) => ReportError($"surface: {message}");

    public ISwapchain Swapchain => _swapchain;

    internal WebGpuSwapchain SwapchainInternal => _swapchain;

    /// <summary>False: WebGPU's framebuffer origin is the top left, like D3D11's.</summary>
    public bool HasBottomLeftFramebufferOrigin => false;

    public bool SupportsBaseVertexDraw => true;

    /// <summary>False on the browser: emdawnwebgpu has no <c>wgpuBufferMapAsync</c>.</summary>
    public bool SupportsTextureReadback => !_browser;

    /// <summary>Human-readable adapter identity, for the boot log and the smoke harness.</summary>
    public string AdapterDescription { get; } = "";

    private WebGpuGraphicsDevice(
        Wg.WebGPU api,
        Wgpu? ext,
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
        ErrorRouter? errorRouter,
        Wg.PfnUncapturedErrorCallback errorCallback,
        bool browser,
        in WebGpuDeviceDescription description
    )
    {
        Api = api;
        _ext = ext;
        _browser = browser;
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
        _errorRouter = errorRouter;
        _errorCallback = errorCallback;

        // The callback is installed by the descriptor that created the device, so anything wgpu
        // reported before this point was held by the router.

        if (_errorRouter is { } router)
        {
            lock (router)
            {
                router.Target = this;
                foreach (var early in router.Early)
                    ReportError(early);
                router.Early.Clear();
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
        var ext = new Wgpu(api.Context);

        var instance = WebGpuInterop.CreateInstance(api);

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
        var adapterDone = false;
        var adapterCallback = Wg.PfnRequestAdapterCallback.From((Wg.RequestAdapterCallback)((status, candidate, message, _, _) =>
        {
            if (status == Wg.RequestAdapterStatus.Success)
                adapter = candidate;
            else
                adapterError = WebGpuInterop.Read(message) ?? status.ToString();
            adapterDone = true;
        })
        );

        // AllowProcessEvents, not WaitAnyOnly: this build's wgpuInstanceWaitAny aborts, so a
        // wait-any-only callback would never run. This mode runs it on the thread that calls
        // wgpuInstanceProcessEvents, which is the one waiting below, so the writes still need no
        // lock.
        api.InstanceRequestAdapter(instance2, &adapterOptions, new Wg.RequestAdapterCallbackInfo
        {
            Mode = Wg.CallbackMode.AllowProcessEvents,
            Callback = adapterCallback,
        });

        if (!WebGpuInterop.WaitFor(api, instance2, () => adapterDone, TimeSpan.FromSeconds(10)))
            adapterError ??= "the request timed out";

        if (adapter is null)
        {
            throw new InvalidOperationException(
                "wgpu-native found no usable adapter"
                + (adapterError is null ? "." : $": {adapterError}")
                + " On Windows this is a Vulkan or D3D12 driver problem - WebGPU is not available on a "
                + "machine with neither."
            );
        }

        var adapterInfo = default(Wg.AdapterInfo);

        if (api.AdapterGetInfo(adapter, &adapterInfo) != Wg.Status.Success)

            throw new InvalidOperationException("wgpu-native did not report the adapter's identity.");

        var adapterName = WebGpuInterop.Read(adapterInfo.Description)
                          ?? WebGpuInterop.Read(adapterInfo.Device)
                          ?? "unknown adapter";
        var backendType = adapterInfo.BackendType;
        api.AdapterInfoFreeMembers(adapterInfo);

        var limits = default(Wg.Limits);

        if (api.AdapterGetLimits(adapter, &limits) != Wg.Status.Success)

            throw new InvalidOperationException("wgpu-native did not report the adapter's limits.");

        // BC textures are how every RAD model carries its art, and float32 filtering is what the
        // MSDF atlas samples with. Both are optional features rather than core, so each is requested
        // only when the adapter advertises it - asking for one it lacks fails device creation.
        var wantsBc = api.AdapterHasFeature(adapter, Wg.FeatureName.TextureCompressionBC);
        var wantsFloat32Filterable = api.AdapterHasFeature(adapter, Wg.FeatureName.Float32filterable);

        var deviceFeatures = stackalloc Wg.FeatureName[2];
        var deviceFeatureCount = 0;
        if (wantsBc)
            deviceFeatures[deviceFeatureCount++] = Wg.FeatureName.TextureCompressionBC;
        if (wantsFloat32Filterable)
            deviceFeatures[deviceFeatureCount++] = Wg.FeatureName.Float32filterable;

        // The device's limits are the adapter's own, requested back: the documented way to ask for
        // everything it can do without naming each field. RequiredLimits is the struct itself, not
        // the old one-member wrapper.
        var errorRouter = description.EnableValidation ? new ErrorRouter() : null;
        var errorCallback = default(Wg.PfnUncapturedErrorCallback);

        Wg.UncapturedErrorCallbackInfo errorCallbackInfo = default;

        if (errorRouter is not null)
        {
            // An uncaptured error has no wait behind it, so the callback has to stay callable for the
            // device's whole life. SilkMarshal pins the delegate, and the device frees the thunk when
            // it releases the device.
            errorCallback = Wg.PfnUncapturedErrorCallback.From(
                (Wg.UncapturedErrorCallback)((_, type, message, _, _) => errorRouter.Report(type, message))
            );

            errorCallbackInfo = new Wg.UncapturedErrorCallbackInfo { Callback = errorCallback };
        }

        var deviceDescriptor = new Wg.DeviceDescriptor
        {
            RequiredFeatureCount = (nuint)deviceFeatureCount,
            RequiredFeatures = deviceFeatures,
            RequiredLimits = &limits,
            UncapturedErrorCallbackInfo = errorCallbackInfo,
        };

        WgpuDevice* device = null;
        string? deviceError = null;
        var deviceDone = false;
        var deviceCallback = Wg.PfnRequestDeviceCallback.From((Wg.RequestDeviceCallback)((status, created, message, _, _) =>
        {
            if (status == Wg.RequestDeviceStatus.Success)
                device = created;
            else
                deviceError = WebGpuInterop.Read(message) ?? status.ToString();
            deviceDone = true;
        })
        );

        api.AdapterRequestDevice(adapter, &deviceDescriptor, new Wg.RequestDeviceCallbackInfo
        {
            Mode = Wg.CallbackMode.AllowProcessEvents,
            Callback = deviceCallback,
        });

        if (!WebGpuInterop.WaitFor(api, instance2, () => deviceDone, TimeSpan.FromSeconds(20)))
            deviceError ??= "the request timed out";

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
        var surfaceFormat = surface2 is null
            ? Wg.TextureFormat.Bgra8Unorm
            : WebGpuSwapchain.PickSurfaceFormat(api, surface2, adapter, Wg.TextureFormat.Bgra8Unorm);

        GraphicsDiagnostics.Warning?.Invoke(
            $"webgpu: {adapterName} ({backendType}), surface {surfaceFormat}"
            + $", bc={(wantsBc ? "yes" : "no")}, float32-filterable={(wantsFloat32Filterable ? "yes" : "no")}"
            + $", uniform alignment {limits.MinUniformBufferOffsetAlignment}"
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
            limits.MinUniformBufferOffsetAlignment,
            wantsFloat32Filterable,
            wantsBc,
            errorRouter,
            errorCallback,
            browser: false,
            description
        );
    }

    /// <summary>
    /// Brings the backend up over emdawnwebgpu in a browser.
    ///
    /// Nothing is requested here. Adapter and device calls answer through futures completed from a
    /// JS microtask, which a synchronous wait never runs, so the page creates the device with
    /// <c>navigator.gpu</c> before the runtime starts and <see cref="WgpuNativeContext.AcquireDevice"/>
    /// hands it over. Two consequences follow from that: there is no adapter object (every query the
    /// desktop path makes of one it makes of the device, and the surface queries ignore the argument
    /// entirely), and there is no uncaptured-error callback to install - emdawnwebgpu has no such
    /// entry point, so <see cref="TakeError"/> stays empty and only <see cref="CheckCreated"/> carries
    /// a failure reason.
    /// </summary>
    /// <param name="context">
    /// The browser's WebGPU entry points, from <c>NFMWorld.Platform.WebGPU.WgpuNativeContext.Instance</c>.
    /// Passed in rather than resolved here because that trampoline table lives in the project that
    /// builds against the nightly SDK, which this one does not.
    /// </param>
    /// <param name="deviceHandle">
    /// The device the page created, from <c>WgpuNativeContext.AcquireDevice()</c>. Adopted, not
    /// requested - see the remarks.
    /// </param>
    /// <param name="width">Back buffer width in pixels.</param>
    /// <param name="height">Back buffer height in pixels.</param>
    /// <param name="multiSampleCount">Sample count for the offscreen colour attachment.</param>
    /// <param name="depthStencilFormat">The swapchain's automatic depth buffer's format.</param>
    public static WebGpuGraphicsDevice CreateForBrowser(
        INativeContext context,
        nint deviceHandle,
        int width,
        int height,
        int multiSampleCount = 0,
        TextureFormat depthStencilFormat = TextureFormat.Depth24Stencil8
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (width <= 0 || height <= 0)

            throw new ArgumentOutOfRangeException(nameof(width), $"A {width}x{height} back buffer cannot be created.");

        var api = new Wg.WebGPU(context);
        var instance = WebGpuInterop.CreateInstance(api);

        if (instance is null)

            throw new InvalidOperationException("emdawnwebgpu could not create an instance.");

        var device = (WgpuDevice*)deviceHandle;

        if (device is null)
        {
            api.InstanceRelease(instance);

            throw new InvalidOperationException(
                "The page did not hand over a WebGPU device. main.js requests one from navigator.gpu and "
                + "parks it on Module.preinitializedWebGPUDevice before the runtime starts."
            );
        }

        var queue = api.DeviceGetQueue(device);

        var adapterInfo = default(Wg.AdapterInfo);
        var hasInfo = api.DeviceGetAdapterInfo(device, &adapterInfo) == Wg.Status.Success;
        var adapterName = WebGpuInterop.Read(adapterInfo.Description)
                          ?? WebGpuInterop.Read(adapterInfo.Device)
                          ?? WebGpuInterop.Read(adapterInfo.Architecture)
                          ?? "browser device";
        if (hasInfo)
            api.AdapterInfoFreeMembers(adapterInfo);

        var limits = default(Wg.Limits);

        if (api.DeviceGetLimits(device, &limits) != Wg.Status.Success)

            throw new InvalidOperationException("emdawnwebgpu did not report the device's limits.");

        // BC textures are absent in every browser today, so that arm of the format mapping never
        // runs. Float32 filtering is not optional for us: the shadow cascade is a Single-format
        // texture, and a filterable float32 device is the only way to sample it.
        var hasBc = api.DeviceHasFeature(device, Wg.FeatureName.TextureCompressionBC);
        var hasFloat32Filterable = api.DeviceHasFeature(device, Wg.FeatureName.Float32filterable);

        // The surface is created after the device: wgpuInstanceCreateSurface is the JS library's own
        // function here, and it resolves the canvas's 'webgpu' context, which the page's device
        // request has already produced.
        var surface = CreateCanvasSurface(api, instance);

        var surfaceFormat = surface is null
            ? Wg.TextureFormat.Bgra8Unorm
            : WebGpuSwapchain.PickSurfaceFormat(api, surface, null, Wg.TextureFormat.Bgra8Unorm);

        GraphicsDiagnostics.Warning?.Invoke(
            $"webgpu: {adapterName} (browser), surface {surfaceFormat}"
            + $", bc={(hasBc ? "yes" : "no")}, float32-filterable={(hasFloat32Filterable ? "yes" : "no")}"
            + $", uniform alignment {limits.MinUniformBufferOffsetAlignment}"
        );

        return new WebGpuGraphicsDevice(
            api,
            ext: null,
            instance,
            adapter: null,
            device,
            queue,
            surface,
            surfaceFormat,
            adapterName,
            limits.MinUniformBufferOffsetAlignment,
            hasFloat32Filterable,
            hasBc,
            errorRouter: null,
            errorCallback: default,
            browser: true,
            new WebGpuDeviceDescription(
                WebGpuWindow.None,
                width,
                height,
                multiSampleCount,
                EnableValidation: false,
                depthStencilFormat
            )
        );
    }

    /// <summary>
    /// The surface over the page's canvas. Returns null when the canvas has no WebGPU context, in
    /// which case the device still renders, into an offscreen attachment.
    /// </summary>
    private static WgpuSurface* CreateCanvasSurface(Wg.WebGPU api, Wg.Instance* instance)
    {
        using var selector = new WebGpuInterop.Utf8("#canvas");
        var fromCanvas = new EmscriptenSurfaceSourceCanvasHTMLSelector
        {
            Chain = new Wg.ChainedStruct { SType = DawnSType.EmscriptenSurfaceSourceCanvasHTMLSelector },
            Selector = selector.View,
        };

        var descriptor = new Wg.SurfaceDescriptor
        {
            NextInChain = (Wg.ChainedStruct*)&fromCanvas,
        };

        return api.InstanceCreateSurface(instance, &descriptor);
    }

    private static WgpuSurface* CreateSurface(Wg.WebGPU api, Wg.Instance* instance, in WebGpuDeviceDescription description)
    {
        var window = description.Window;

        if (window.Kind == WebGpuWindowKind.None)

            return null;

        if (window.Handle == 0)
        {
            throw new ArgumentException(
                $"A {window.Kind} surface needs a window handle, and this one is zero. Either the "
                + "window was destroyed before the device was created, or the layer SDL was asked for "
                + "did not exist.",
                nameof(description)
            );
        }

        var surface = window.Kind switch
        {
            WebGpuWindowKind.Win32 => CreateHwndSurface(api, instance, window.Handle),
            WebGpuWindowKind.X11 => CreateXlibSurface(api, instance, window.Display, window.Handle),
            WebGpuWindowKind.Wayland => CreateWaylandSurface(api, instance, window.Display, window.Handle),
            WebGpuWindowKind.Metal => CreateMetalLayerSurface(api, instance, window.Handle),
            _ => throw new PlatformNotSupportedException($"No WebGPU surface source is defined for {window.Kind}."),
        };

        if (surface is null)
        {
            throw new InvalidOperationException(
                $"wgpu-native could not create a surface over the {window.Kind} window. A surface source "
                + "needs an instance built with that backend (wgpu-native exposes all of them) and "
                + "handles that are still valid - a destroyed window or a released layer fails here "
                + "rather than at present time."
            );
        }

        return surface;
    }

    private static WgpuSurface* CreateHwndSurface(Wg.WebGPU api, Wg.Instance* instance, nint hwnd)
    {
        // DXGI asks for the instance the window belongs to; every window this app creates lives in
        // the process image, which is what a null module name resolves to.
        var fromHwnd = new Wg.SurfaceSourceWindowsHWND
        {
            Chain = new Wg.ChainedStruct { SType = Wg.SType.SurfaceSourceWindowsHwnd },
            Hinstance = (void*)GetModuleHandleW(null),
            Hwnd = (void*)hwnd,
        };

        var descriptor = new Wg.SurfaceDescriptor { NextInChain = (Wg.ChainedStruct*)&fromHwnd };

        return api.InstanceCreateSurface(instance, &descriptor);
    }

    private static WgpuSurface* CreateXlibSurface(Wg.WebGPU api, Wg.Instance* instance, nint display, nint window)
    {
        var fromXlib = new Wg.SurfaceSourceXlibWindow
        {
            Chain = new Wg.ChainedStruct { SType = Wg.SType.SurfaceSourceXlibWindow },
            Display = (void*)display,
            Window = (ulong)window,
        };

        var descriptor = new Wg.SurfaceDescriptor { NextInChain = (Wg.ChainedStruct*)&fromXlib };

        return api.InstanceCreateSurface(instance, &descriptor);
    }

    private static WgpuSurface* CreateWaylandSurface(Wg.WebGPU api, Wg.Instance* instance, nint display, nint surface)
    {
        var fromWayland = new Wg.SurfaceSourceWaylandSurface
        {
            Chain = new Wg.ChainedStruct { SType = Wg.SType.SurfaceSourceWaylandSurface },
            Display = (void*)display,
            Surface = (void*)surface,
        };

        var descriptor = new Wg.SurfaceDescriptor { NextInChain = (Wg.ChainedStruct*)&fromWayland };

        return api.InstanceCreateSurface(instance, &descriptor);
    }

    private static WgpuSurface* CreateMetalLayerSurface(Wg.WebGPU api, Wg.Instance* instance, nint layer)
    {
        // The layer's drawable size is SDL's to maintain (it resizes the Metal view with the window),
        // so nothing here has to track the client area - the swapchain reads the texture's size.
        var fromLayer = new Wg.SurfaceSourceMetalLayer
        {
            Chain = new Wg.ChainedStruct { SType = Wg.SType.SurfaceSourceMetalLayer },
            Layer = (void*)layer,
        };

        var descriptor = new Wg.SurfaceDescriptor { NextInChain = (Wg.ChainedStruct*)&fromLayer };

        return api.InstanceCreateSurface(instance, &descriptor);
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

    /// <summary>
    /// Drops the frame's recording instead of submitting half a frame, and hands the acquired
    /// surface texture back so the next frame starts from a clean acquisition.
    /// </summary>
    public void AbandonCommandBuffer(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not WebGpuCommandBuffer webGpuCommandBuffer
            || !ReferenceEquals(webGpuCommandBuffer, _activeCommandBuffer))
        {
            return;
        }

        _activeCommandBuffer = null;
        webGpuCommandBuffer.Dispose();
        _swapchain.AbandonFrame();
        FlushPendingDestroys();
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

        var destination = new Wg.TexelCopyTextureInfo
        {
            Texture = texture.Handle,
            MipLevel = (uint)level,
            Origin = new Wg.Origin3D { X = (uint)x, Y = (uint)y, Z = 0 },
            Aspect = Wg.TextureAspect.All,
        };

        var layout = new Wg.TexelCopyBufferLayout
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

        if (_browser)
        {
            // A readback needs a MapRead buffer and a mapped-range callback. emdawnwebgpu implements
            // neither (wgpuBufferMapAsync returns a future, and its JS side has no mapAsync path at
            // all), so this fails at buffer creation rather than here unless it is refused.

            throw new NotSupportedException(
                "Reading a texture back is not available on the browser's WebGPU backend: it needs "
                + "wgpuBufferMapAsync, which emdawnwebgpu does not implement."
            );
        }

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

        if (x < 0
            || y < 0
            || width <= 0
            || height <= 0
            || x + width > webGpuTexture.Width
            || y + height > webGpuTexture.Height)
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

            var source = new Wg.TexelCopyTextureInfo
            {
                Texture = webGpuTexture.Handle,
                MipLevel = 0,
                Origin = new Wg.Origin3D { X = (uint)x, Y = (uint)y, Z = 0 },
                Aspect = Wg.TextureAspect.All,
            };

            var copyDestination = new Wg.TexelCopyBufferInfo
            {
                Buffer = buffer,
                Layout = new Wg.TexelCopyBufferLayout
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

            var status = Wg.MapAsyncStatus.Error;
            var mapped = false;
            var mapCallback = Wg.PfnBufferMapCallback.From(
                (Wg.BufferMapCallback)((result, _, _, _) =>
                {
                    status = result;
                    mapped = true;
                })
            );

            Api.BufferMapAsync(buffer, Wg.MapMode.Read, 0, (nuint)stagingSize, new Wg.BufferMapCallbackInfo
            {
                Mode = Wg.CallbackMode.AllowProcessEvents,
                Callback = mapCallback,
            });

            var mappedInTime = WebGpuInterop.WaitFor(Api, Instance, () => mapped, TimeSpan.FromSeconds(10));

            if (!mappedInTime || status != Wg.MapAsyncStatus.Success)
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

        // Null on the browser, where there is no adapter object at all - and the release is
        // unconditional, so passing null aborts inside the native library.

        if (Adapter is not null)
        {
            Api.AdapterRelease(Adapter);
            Adapter = null;
        }

        Api.InstanceRelease(Instance);
        Instance = null;

        // Frees the GCHandle SilkMarshal pinned for the error thunk. Doing it after the device is
        // released is what makes it safe: nothing can call the callback any more.
        if (_errorCallback.Handle != null)
            _errorCallback.Dispose();
    }
}
