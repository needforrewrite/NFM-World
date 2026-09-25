using System.Runtime.InteropServices;
using SharpSokol.Native;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// <see cref="ISokolPlatform"/> for D3D11 on a window this class does not own.
///
/// The window comes in as a bare <c>HWND</c>, so SDL keeps every part of the window (input, ImGui's
/// backend, the GL path's context) and this class only borrows the surface to create a device and a
/// swapchain against. That is the whole reason the D3D11 backend of sokol_gfx can be hosted this
/// way: <c>sg_d3d11_environment</c> is <c>{ device, device_context }</c> and nothing else
/// (<c>sokol_gfx.h:5361-5364</c>, asserted at <c>:13880-13892</c>) - no <c>HWND</c>, no DXGI factory,
/// no swapchain - so the swapchain is ours to build and ours to present.
///
/// Silk.NET is the only dependency here, and it is a <em>raw</em> D3D11/DXGI binding rather than a
/// windowing layer: <see cref="D3D11.GetApi(DXSwapchainProvider,bool)"/> exists only to hand back a
/// function-pointer table for <c>d3d11.dll</c>, and no Silk window is ever created. (Its
/// <c>INativeWindowSource</c> overload is avoided deliberately - it would pull in Silk's windowing
/// abstraction for a window it does not own.)
///
/// The interface this implements returns SharpSokol's <c>sg_environment</c>/<c>sg_swapchain</c> and
/// so names <c>SharpSokol.Native</c> in its signature. That is intentional and contained: the seam
/// is one assembly boundary away from its only caller (<see cref="SokolGraphicsDevice.Create"/>),
/// and hiding it behind a second set of mirror structs would cost more than it bought - the two
/// structs are sokol's public ABI, not an implementation detail.
/// </summary>
public sealed unsafe class SokolD3D11Platform : ISokolPlatform
{
    /// <summary>
    /// The colour format of the backbuffer, and the one sokol is told is in use.
    ///
    /// BGRA8 rather than RGBA8 because that is what a Win32 swapchain is expected to be
    /// (the <c>CreateDeviceBgraSupport</c> flag below is what makes it legal): the compositor can
    /// then take the buffer without a per-present swizzle.
    ///
    /// At a sample count of one this value is never actually read - the only two uses of a
    /// swapchain pass's <c>color_format</c> are inside a <c>resolve_view</c> branch that is skipped
    /// entirely and asserts <c>sample_count &gt; 1</c> if it is entered (<c>sokol_gfx.h:14928-14939</c>).
    /// It is still set truthfully, because the same field is a <em>validated</em> part of the
    /// offscreen-image defaults and because a future MSAA path makes it load-bearing.
    /// </summary>
    private const Format BackbufferFormat = Format.FormatB8G8R8A8Unorm;

    private const int BufferCount = 2;

    /// <summary>
    /// Whether the swapchain was created with a flip-model effect, which decides the buffer count
    /// <see cref="Resize"/> has to hand back to <c>ResizeBuffers</c>.
    ///
    /// The flip model is what sokol_app uses on Windows 10 and later
    /// (<c>sokol_app.h:8925-8931</c>), and it is the right choice here for the same reason: it
    /// avoids the compositor's per-present copy. But it cannot be selected unconditionally, because
    /// the flip model is genuinely unavailable on Windows 7 and on basic-display-adapter sessions,
    /// where <c>CreateDeviceAndSwapChain</c> then fails outright rather than falling back. So the
    /// flip model is attempted first and the blt model is the fallback - the same progression, in
    /// the other order, as sokol_app's version check.
    /// </summary>
    private bool _flipModel;

    /// <summary>True: the device is created with <c>CreateDeviceFlag.Singlethreaded</c> - see
    /// <see cref="SingleThreadedLifetime"/>.</summary>
    public bool? SingleThreadedLifetime => true;

    /// <summary>
    /// Direct3D 11.0, deliberately - not 11.1. Feature level is a hardware floor, and 11.1 buys
    /// nothing here while excluding Windows 7-era drivers. This is the same level FNA3D and ANGLE
    /// will already have negotiated on any machine running the GL path.
    /// </summary>
    private static readonly D3DFeatureLevel[] FeatureLevels =
    [
        D3DFeatureLevel.Level111,
        D3DFeatureLevel.Level110,
        D3DFeatureLevel.Level100,
    ];

    private ID3D11Device* _device;
    private ID3D11DeviceContext* _context;
    private IDXGISwapChain* _swapchain;
    private ID3D11RenderTargetView* _renderTargetView;
    private ID3D11DepthStencilView* _depthStencilView;
    private ID3D11Texture2D* _depthStencilTexture;

    private int _width;
    private int _height;
    private bool _disposed;

    /// <summary>The <c>HWND</c> this was constructed with, kept only to answer
    /// <see cref="NativeHandle"/> - nothing here uses it after the swapchain exists.</summary>
    private readonly IntPtr _windowHandle;

    public int Width => _width;

    public int Height => _height;

    public IntPtr NativeHandle => _windowHandle;

    /// <summary>
    /// Whether <see cref="Present"/> blocks on vblank.
    ///
    /// Unlike the FNA3D backend, whose present interval is fixed at device creation, a DXGI
    /// swapchain's interval is a per-present argument and so can genuinely follow the setting.
    /// </summary>
    /// <inheritdoc cref="ISokolPlatform.VSync"/>
    public bool VSync { get; set; } = true;

    /// <summary>
    /// Creates the device, swapchain and the colour/depth views over <paramref name="windowHandle"/>.
    /// </summary>
    /// <param name="windowHandle">
    /// The <c>HWND</c> of a window the caller owns and keeps alive for this object's lifetime. Not
    /// owned here - <see cref="Dispose"/> never destroys it.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// On a zero handle, a non-positive size, or any D3D11/DXGI failure. Every one of those is fatal
    /// to the backend, and failing at construction with the HRESULT is far more useful than a
    /// zeroed-out device that produces a black frame later.
    /// </exception>
    public SokolD3D11Platform(IntPtr windowHandle, int width, int height)
    {
        if (windowHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                "SokolD3D11Platform needs a native window handle. On Windows this is the HWND from " +
                "SdlWindow.NativeWindowHandle; a zero value means the window was not created through " +
                "the Win32 video driver.");

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException(
                $"SokolD3D11Platform needs a positive drawable size, got {width}x{height}.");

        _windowHandle = windowHandle;
        _width = width;
        _height = height;

        // DXSwapchainProvider only selects the DLL entry points to bind; the window it names is
        // never touched, and the Win32 provider is the one that matches the HWND we pass below.
        // The overload is marked obsolete in favour of one that takes Silk's own window abstraction,
        // which is precisely what this class exists to avoid - hence the suppression rather than the
        // INativeWindowSource call.
#pragma warning disable CS0618
        var d3d11 = D3D11.GetApi(DXSwapchainProvider.Win32, false);
#pragma warning restore CS0618

        IDXGISwapChain* swapchain = null;
        ID3D11Device* device = null;
        ID3D11DeviceContext* context = null;
        var featureLevel = D3DFeatureLevel.Level100;

        var swapchainDesc = new SwapChainDesc
        {
            BufferDesc = new ModeDesc
            {
                Width = (uint)width,
                Height = (uint)height,
                RefreshRate = new Rational(0, 1),
                Format = BackbufferFormat,
            },
            SampleDesc = new SampleDesc { Count = 1, Quality = 0 },
            BufferUsage = 32, // DXGI_USAGE_RENDER_TARGET_OUTPUT
            BufferCount = BufferCount,
            OutputWindow = windowHandle,
            Windowed = true,
            SwapEffect = SwapEffect.FlipDiscard,

            // FrameLatencyWaitableObject is not set: the game's frame pacing is the timer's job, and
            // nothing here waits on the handle this flag would create.
            Flags = 0,
        };

        fixed (D3DFeatureLevel* levels = FeatureLevels)
        {
            // Flags matter more than they look. BgraSupport is required for the BGRA8 backbuffer
            // above. Singlethreaded matches what sokol_app does unconditionally
            // (sokol_app.h:8936) and is what makes SingleThreadedLifetime's answer true; it is a
            // promise that only the creating thread will touch the context, which is why
            // SokolGraphicsDevice.EnsureRenderingThread exists to enforce it. Debug is opt-in only,
            // since the debug layer changes behaviour enough to invalidate the measurements this
            // backend exists to take.
            var flags = CreateDeviceFlag.BgraSupport | CreateDeviceFlag.Singlethreaded;
            if (DebugLayer) flags |= CreateDeviceFlag.Debug;

            var hr = CreateDeviceAndSwapChain(d3d11, levels, swapchainDesc, (uint)flags,
                out swapchain, out device, out context, out featureLevel, out _flipModel);
            if (hr < 0)
                throw new InvalidOperationException(
                    $"D3D11CreateDeviceAndSwapChain failed with 0x{hr:X8} (flip model: {_flipModel}, " +
                    $"{featureLevel} negotiated). If the hardware device was rejected, this is the " +
                    "point at which a WARP fallback would belong.");
        }

        _device = device;
        _context = context;
        _swapchain = swapchain;

        CreateSizeDependentViews();
    }

    /// <summary>
    /// Creates the device and swapchain, trying the flip model first and the blt model second.
    ///
    /// The retry is not defensive padding: <c>DXGI_SWAP_EFFECT_FLIP_DISCARD</c> is unavailable on
    /// Windows 7 and in basic-display-adapter sessions, and there the failure is a hard
    /// <c>DXGI_ERROR_INVALID_CALL</c> from <c>D3D11CreateDeviceAndSwapChain</c> rather than a silent
    /// downgrade. Only the swap effect and the buffer count differ between the attempts, so the two
    /// descriptors are built from one method.
    /// </summary>
    private static int CreateDeviceAndSwapChain(
        D3D11 d3d11,
        D3DFeatureLevel* levels,
        SwapChainDesc desc,
        uint flags,
        out IDXGISwapChain* swapchain,
        out ID3D11Device* device,
        out ID3D11DeviceContext* context,
        out D3DFeatureLevel featureLevel,
        out bool flipModel)
    {
        // Locals rather than the out parameters directly: an out parameter is not an addressable
        // expression, so &swapchain and friends would not compile.
        IDXGISwapChain* sc = null;
        ID3D11Device* dev = null;
        ID3D11DeviceContext* ctx = null;
        var level = D3DFeatureLevel.Level100;
        var localDesc = desc;

        flipModel = true;

        var hr = d3d11.CreateDeviceAndSwapChain(
            (IDXGIAdapter*)null, D3DDriverType.Hardware, IntPtr.Zero, flags,
            levels, (uint)FeatureLevels.Length, D3D11SdkVersion,
            &localDesc, &sc, &dev, &level, ref ctx);

        if (hr < 0)
        {
            // Blt model fallback: DXGI_SWAP_EFFECT_DISCARD with a single buffer, the one combination
            // every DXGI implementation supports. Anything the first attempt allocated is released
            // first - a failed call can still have handed back objects.
            flipModel = false;
            if (sc != null) { sc->Release(); sc = null; }
            if (dev != null) { dev->Release(); dev = null; }
            if (ctx != null) { ctx->Release(); ctx = null; }

            localDesc.SwapEffect = SwapEffect.Discard;
            localDesc.BufferCount = 1;
            level = D3DFeatureLevel.Level100;

            hr = d3d11.CreateDeviceAndSwapChain(
                (IDXGIAdapter*)null, D3DDriverType.Hardware, IntPtr.Zero, flags,
                levels, (uint)FeatureLevels.Length, D3D11SdkVersion,
                &localDesc, &sc, &dev, &level, ref ctx);

            if (hr < 0)
            {
                // Both attempts failed; report the flip-model attempt in the caller's message, since
                // that is the one whose failure is worth diagnosing.
                flipModel = true;
                if (sc != null) { sc->Release(); sc = null; }
                if (dev != null) { dev->Release(); dev = null; }
                if (ctx != null) { ctx->Release(); ctx = null; }
            }
        }

        swapchain = sc;
        device = dev;
        context = ctx;
        featureLevel = level;
        return hr;
    }

    /// <summary>
    /// Whether to ask for the D3D11 debug layer. Opt in via <c>NFMWORLD_D3D11_DEBUG=1</c>: the
    /// layer is only installed with the Graphics Tools feature, it slows every call, and it is
    /// mutually exclusive with the performance comparison this backend is being wired up to make.
    /// </summary>
    private static bool DebugLayer =>
        Environment.GetEnvironmentVariable("NFMWORLD_D3D11_DEBUG") is "1" or "true";

    /// <summary>
    /// <c>D3D11_SDK_VERSION</c>. Silk.NET 2.22 does not emit a named constant for it (there is no
    /// <c>*SdkVersion</c> type anywhere in its Direct3D11/Core assemblies), and the value is fixed
    /// by the ABI rather than by the SDK installed - it is the version D3D11CreateDevice* expects
    /// in this parameter, 7, and has been since Windows 8. Passing anything else fails the call.
    /// </summary>
    private const uint D3D11SdkVersion = 7;

    /// <summary>
    /// Creates the render-target and depth-stencil views over the current backbuffer.
    ///
    /// Called at construction and again after every <see cref="Resize"/>: <c>ResizeBuffers</c>
    /// invalidates every view that referred to the old buffers, so there is no way to keep them.
    /// </summary>
    private void CreateSizeDependentViews()
    {
        ID3D11Texture2D* backbuffer = null;
        // Taken into a local because the IID overload takes a pointer and a static readonly field
        // cannot have its address taken.
        var texture2DGuid = ID3D11Texture2D.Guid;

        // Both swap effects take buffer 0 here. For the blt model that is not in doubt. For the flip
        // model, the story that buffers 0..n-2 are "the queue" and n-1 is the backbuffer is not a
        // DXGI rule - GetBuffer(0) on a flip-model swapchain returns the current back buffer, and
        // which physical buffer that names changes as presents rotate the swapchain. Nothing here
        // calls GetBuffer per frame, so this view is created once and must be re-created whenever the
        // buffers are replaced (a resize does that - see Resize).
        const uint backBufferIndex = 0;
        var hr = _swapchain->GetBuffer(backBufferIndex, &texture2DGuid, (void**)&backbuffer);
        if (hr < 0)
            throw new InvalidOperationException($"IDXGISwapChain::GetBuffer({backBufferIndex}) failed with 0x{hr:X8}.");

        try
        {
            // No RenderTargetViewDesc: a null description means "the whole resource, in the
            // resource's own format", which is exactly right now that the backbuffer is created
            // non-typeless (implicitly, straight into BGRA8).
            ID3D11RenderTargetView* rtv = null;
            hr = _device->CreateRenderTargetView((ID3D11Resource*)backbuffer, null, &rtv);
            if (hr < 0)
                throw new InvalidOperationException($"ID3D11Device::CreateRenderTargetView failed with 0x{hr:X8}.");
            _renderTargetView = rtv;
        }
        finally
        {
            // Released immediately: the view holds its own reference, and GetBuffer's is ours to
            // drop. Otherwise every resize across a session's life leaks a backbuffer.
            backbuffer->Release();
        }

        var depthDesc = new Texture2DDesc
        {
            Width = (uint)_width,
            Height = (uint)_height,
            MipLevels = 1,
            ArraySize = 1,

            // Typeless, matching exactly what sokol creates for this format rather than something
            // merely compatible: _sg_d3d11_base_pixel_format maps SG_PIXELFORMAT_DEPTH_STENCIL to
            // DXGI_FORMAT_R32G8X24_TYPELESS (sokol_gfx.h:13554), and its DSV to D32_FLOAT_S8X24_UINT
            // (:13594). D24S8 appears nowhere in sokol's D3D11 path, and pairing a
            // R24_UNORM_X8_TYPELESS resource with that DSV is rejected with E_INVALIDARG - the view
            // format has to be a member of the resource's typeless family.
            Format = Format.FormatR32G8X24Typeless,
            SampleDesc = new SampleDesc { Count = 1, Quality = 0 },
            Usage = Silk.NET.Direct3D11.Usage.Default,
            BindFlags = (uint)BindFlag.DepthStencil,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* depthTexture = null;
        hr = _device->CreateTexture2D(&depthDesc, null, &depthTexture);
        if (hr < 0)
            throw new InvalidOperationException($"ID3D11Device::CreateTexture2D (depth) failed with 0x{hr:X8}.");
        _depthStencilTexture = depthTexture;

        // The description is explicit here, unlike the render target's, because the resource above is
        // typeless and a typeless format cannot be used as a view format - the view is where the
        // concrete format is pinned down, and a null description has nothing to pin it with.
        var dsvDesc = new DepthStencilViewDesc
        {
            Format = Format.FormatD32FloatS8X24Uint,

            // DEPTH_STENCIL, the default: this view exposes depth and stencil together, which is what
            // the app's shadow-cascade and NanoVG passes want.
            ViewDimension = DsvDimension.Texture2D,
        };

        ID3D11DepthStencilView* dsv = null;
        hr = _device->CreateDepthStencilView((ID3D11Resource*)depthTexture, &dsvDesc, &dsv);
        if (hr < 0)
            throw new InvalidOperationException($"ID3D11Device::CreateDepthStencilView failed with 0x{hr:X8}.");
        _depthStencilView = dsv;
    }

    /// <summary>Releases the two views and the depth texture they were built over.</summary>
    private void ReleaseSizeDependentViews()
    {
        if (_renderTargetView != null)
        {
            _renderTargetView->Release();
            _renderTargetView = null;
        }

        if (_depthStencilView != null)
        {
            _depthStencilView->Release();
            _depthStencilView = null;
        }

        if (_depthStencilTexture != null)
        {
            _depthStencilTexture->Release();
            _depthStencilTexture = null;
        }
    }

    public sg_environment CreateEnvironment(int width, int height, int sampleCount)
    {
        return new sg_environment
        {
            defaults = new sg_environment_defaults
            {
                // BGRA8 is not a formality here the way it is on the swapchain: this is also the
                // default pixel format and sample count for every sg_image created without one
                // (sokol_gfx.h:596-604, :6608-6613), so it governs this app's offscreen render
                // targets and textures too.
                color_format = sg_pixel_format.SG_PIXELFORMAT_BGRA8,

                // The app's shadow-cascade and NanoVG passes need a stencil, and this is what
                // sokol_app would have supplied by default.
                depth_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,

                // Always one: the swapchain is created non-MSAA (SampleDesc.Count = 1 above), and
                // sokol asserts that a swapchain pass's resolve_view is absent iff this is one
                // (sokol_gfx.h:24803-24805). Higher values need a real MSAA backbuffer plus a
                // separate resolve target - see ISokolPlatform.Resize's note.
                sample_count = 1,
            },
            d3d11 = new sg_d3d11_environment
            {
                device = _device,
                device_context = _context,
            },
        };
    }

    public sg_swapchain AcquireSwapchain()
    {
        // A minimized window has no drawable, and sokol's documented way to skip a frame is an
        // invalid swapchain (sokol_gfx.h:550-554) rather than a zero-sized pass. Reporting the
        // last known size here instead would have sokol set a viewport over a backbuffer that no
        // longer matches it.
        if (_width <= 0 || _height <= 0)
            return new sg_swapchain { invalid = 1 };

        return new sg_swapchain
        {
            width = _width,
            height = _height,
            sample_count = 1,
            color_format = sg_pixel_format.SG_PIXELFORMAT_BGRA8,
            depth_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,

            // The swapchain views are handed over verbatim - sokol casts them and calls
            // OMSetRenderTargets with no pooling and no AddRef (sokol_gfx.h:14840-14849) - so
            // these must stay valid for as long as any pass might name them, which is why they
            // live until Resize or Dispose.
            d3d11 = new sg_d3d11_swapchain
            {
                render_view = _renderTargetView,

                // Must be null: sokol rejects a resolve view on a non-MSAA swapchain
                // (sokol_gfx.h:24803-24805), and the resolve branch it guards asserts
                // sample_count > 1 (sokol_gfx.h:14930).
                resolve_view = null,
                depth_stencil_view = _depthStencilView,
            },
        };
    }

    public void Resize(int width, int height)
    {
        // A minimized window reports 0x0 and has no drawable; ISwapchain's contract is that this
        // keeps the last one rather than reallocating to nothing.
        if (width <= 0 || height <= 0) return;
        if (width == _width && height == _height) return;

        // The buffer count and format must match what the swapchain was created with, or DXGI
        // rejects the call - which is why the count follows the model chosen at creation. Only the
        // size is being changed here.
        var hr = _swapchain->ResizeBuffers(
            _flipModel ? BufferCount : 1u, (uint)width, (uint)height, BackbufferFormat, 0);
        if (hr < 0)
            throw new InvalidOperationException($"IDXGISwapChain::ResizeBuffers({width}x{height}) failed with 0x{hr:X8}.");

        _width = width;
        _height = height;

        // Order matters: release first, or the old views keep the old buffers alive and DXGI
        // refuses to resize with outstanding references.
        ReleaseSizeDependentViews();
        CreateSizeDependentViews();
    }

    public void Present()
    {
        // Called after sg_commit(). Nothing in sokol_gfx presents on its own (_sg_d3d11_commit is
        // an empty function, sokol_gfx.h:15217), and nothing here has the backbuffer bound any more
        // either - _sg_d3d11_end_pass finishes with ClearState.
        var interval = VSync ? 1u : 0u;
        var hr = _swapchain->Present(interval, 0);
        if (hr < 0)
            throw new InvalidOperationException($"IDXGISwapChain::Present(syncInterval: {interval}) failed with 0x{hr:X8}.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ReleaseSizeDependentViews();

        // Released in reverse creation order. The context has to go after nothing in particular -
        // D3D11 refcounts each object independently - but the order is kept consistent with
        // creation so a leaked reference is easier to attribute.
        if (_swapchain != null)
        {
            _swapchain->Release();
            _swapchain = null;
        }

        if (_context != null)
        {
            _context->Release();
            _context = null;
        }

        if (_device != null)
        {
            _device->Release();
            _device = null;
        }
    }
}
