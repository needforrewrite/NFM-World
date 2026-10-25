// LLM maintained.
//
// The swapchain: a DXGI flip-model swapchain over the app's HWND, plus the depth buffer that goes
// with it.
//
// Two things here are not obvious from the abstraction's ISwapchain surface and are the reason this
// file is longer than its GL counterpart:
//
//   - The depth-stencil buffer is the swapchain's. The app binds "no target" to mean the back buffer
//     and then clears colour *and* depth on it (WorldGame.Draw does exactly that), but a DXGI back
//     buffer has no depth attachment and D3D11 has no default depth buffer - so one is allocated and
//     owned here, and rebound whenever the back buffers are rebuilt.
//
//   - Multisampling is a faux pass. A DXGI swapchain cannot be multisampled in the flip model, so
//     the sample count above one means rendering into a separate multisampled texture and resolving
//     it into the back buffer at present time. That is the same structure FNA3D's D3D11 driver uses,
//     and it is why MultiSampleCount here is a real, per-draw property rather than a window flag.
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace NFMWorld.Graphics.D3D11;

internal sealed unsafe class D3D11Swapchain : ISwapchain
{
    /// <summary>
    /// Buffers in the swapchain. Two, which is the flip model's minimum and the only value that
    /// does not add a frame of latency for nothing.
    /// </summary>
    private const uint BufferCount = 2;

    /// <summary>
    /// The back buffer format. Fixed rather than configurable: the app's window has no alpha and the
    /// abstraction exposes no format for the swapchain, and B8G8R8A8 is the one flip-model format
    /// every DXGI implementation is required to support.
    /// </summary>
    private const DXGI_FORMAT BackBufferFormat = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;

    private readonly D3D11GraphicsDevice _device;
    private readonly TextureFormat _depthFormat;

    private IDXGISwapChain* _swapChain;
    private IDXGIFactory2* _factory;

    /// <summary>
    /// The swapchain's own back buffer and the view over it. Both are replaced on every resize, which
    /// is the only thing that invalidates them - <c>GetBuffer(0)</c> returns the same surface across
    /// presents, so caching the pair here is correct.
    /// </summary>
    private ID3D11Texture2D* _backBuffer;
    private ID3D11RenderTargetView* _backBufferView;

    /// <summary>
    /// The multisampled texture everything is actually drawn into when <see cref="MultiSampleCount"/>
    /// is above one, and the view over it. Null when it is one.
    /// </summary>
    private ID3D11Texture2D* _multisampledTexture;
    private ID3D11RenderTargetView* _multisampledView;

    private ID3D11Texture2D* _depthTexture;
    private ID3D11DepthStencilView* _depthView;

    private readonly bool _tearingSupported;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>
    /// The sample count the multisampled texture was actually allocated with.
    ///
    /// Defaults to one rather than zero, and that default is load-bearing: <see cref="CreateBuffers"/>
    /// reads this to size the depth buffer's sample descriptor, and it runs from the constructor before
    /// the real count is assigned. <c>DXGI_SAMPLE_DESC</c> accepts a count of one or a real count, but
    /// zero is not a legal value and D3D11 rejects the whole texture description with E_INVALIDARG -
    /// which reads as nothing more than "CreateTexture2D failed".
    /// </summary>
    public int MultiSampleCount { get; private set; } = 1;

    /// <summary>
    /// False: a new count is a new multisampled texture plus a viewport-sized depth buffer, both of
    /// which <see cref="Resize"/> builds. Nothing about this backend's sample count is baked into a
    /// window or a context, unlike the GL backends' pixel format.
    /// </summary>
    public bool MultiSampleChangeRequiresRestart => false;

    /// <summary>
    /// The last count <em>requested</em>, which is deliberately not <see cref="MultiSampleCount"/>.
    ///
    /// The same trap the GL backends document: comparing a request against a clamped allocation never
    /// converges when the driver clamps, and <c>WorldGame.EnsureSwapchainMatchesWindow</c> compares
    /// against the last request for exactly this reason - so a request that gets clamped must still
    /// be recorded as the request, not as what it resolved to.
    /// </summary>
    private int _requestedMultiSampleCount;

    /// <summary>The texture the current render target view covers: the multisampled one, or the back buffer.</summary>
    internal ID3D11Texture2D* ColorTarget => _multisampledTexture is null ? _backBuffer : _multisampledTexture;

    /// <summary>
    /// The swapchain's own back buffer, which is what a present hands to the compositor.
    ///
    /// Distinct from <see cref="ColorTarget"/> whenever multisampling is on: the resolve into this
    /// texture is the last step of <see cref="Present"/>, so this is the surface to read to see what
    /// actually reaches the screen. Exposed for the smoke test's present check, which reads the clear
    /// colour back off it - a raw pointer rather than a wrapped <c>D3D11Texture</c>, because that type
    /// creates the resource it wraps and this one belongs to DXGI.
    /// </summary>
    internal ID3D11Texture2D* BackBuffer => _backBuffer;

    internal ID3D11RenderTargetView* ColorView => _multisampledView is null ? _backBufferView : _multisampledView;

    internal ID3D11DepthStencilView* DepthView => _depthView;

    internal D3D11Swapchain(
        D3D11GraphicsDevice device, nint windowHandle, int width, int height, int requestedMultiSampleCount,
        TextureFormat depthFormat)
    {
        _device = device;
        _depthFormat = depthFormat;
        _requestedMultiSampleCount = requestedMultiSampleCount;

        // The size and the sample count are recorded before anything is created, because CreateBuffers
        // reads both of them to size the depth buffer and the multisampled colour texture - and it runs
        // from inside the call below, not after it.
        Width = width;
        Height = height;
        MultiSampleCount = device.ClampSampleCount(BackBufferFormat, requestedMultiSampleCount);

        // The device's own DXGI factory, rather than one created for the purpose: a swapchain has to
        // be created by the factory that owns the adapter the device is on, and going through the
        // device is the only way to be sure of that.
        _factory = GetFactoryFor(device.Device);
        _tearingSupported = QueryTearingSupport(_factory);

        CreateSwapChain(windowHandle, width, height);
    }

    /// <summary>
    /// The <c>IDXGIFactory2</c> the device was created from.
    /// </summary>
    /// <remarks>
    /// Asked of the device rather than created fresh, so the swapchain is guaranteed to be on the
    /// same adapter as the device - a factory from <c>CreateDXGIFactory1</c> picks its own adapter,
    /// and on a machine with two GPUs that is a silent way to get a swapchain the device cannot
    /// present to.
    /// </remarks>
    private static IDXGIFactory2* GetFactoryFor(ID3D11Device* device)
    {
        IDXGIDevice* dxgiDevice = null;
        IDXGIAdapter* adapter = null;
        IDXGIFactory1* factory1 = null;
        IDXGIFactory2* factory2 = null;

        try
        {
            // The IID is materialised into a local first. __uuidof's result is a wrapper whose pointer
            // conversion is a real operator on a temporary, and taking its address in the argument list
            // leaves a pointer into storage the compiler is free not to keep alive across the call.
            var dxgiDeviceIid = D3D11Interop.Iid<IDXGIDevice>();
            D3D11Interop.Check(
                device->QueryInterface(&dxgiDeviceIid, (void**)&dxgiDevice), "ID3D11Device -> IDXGIDevice");

            D3D11Interop.Check(dxgiDevice->GetAdapter(&adapter), "IDXGIDevice::GetAdapter");

            var factory1Iid = D3D11Interop.Iid<IDXGIFactory1>();
            D3D11Interop.Check(
                adapter->GetParent(&factory1Iid, (void**)&factory1), "IDXGIAdapter -> IDXGIFactory1");

            // IDXGIFactory2 for CreateSwapChainForHwnd, which is the only entry point that can ask for
            // a flip-model swap effect. Every Windows 8 and later factory has it.
            var factory2Iid = D3D11Interop.Iid<IDXGIFactory2>();
            D3D11Interop.Check(
                factory1->QueryInterface(&factory2Iid, (void**)&factory2),
                "IDXGIFactory1 -> IDXGIFactory2",
                "A factory without IDXGIFactory2 predates Windows 8, so no flip-model swapchain is available.");

            return factory2;
        }
        catch
        {
            D3D11Interop.Release(ref factory2);
            throw;
        }
        finally
        {
            D3D11Interop.Release(ref factory1);
            D3D11Interop.Release(ref adapter);
            D3D11Interop.Release(ref dxgiDevice);
        }
    }

    /// <summary>
    /// Whether the adapter allows a tearing present. Absent support, requesting
    /// <c>ALLOW_TEARING</c> at swapchain creation fails outright, so this is a precondition rather
    /// than an optimisation - and a failure to answer is treated as "no" rather than thrown, since a
    /// factory old enough not to know the question is also old enough not to support tearing.
    /// </summary>
    private static bool QueryTearingSupport(IDXGIFactory2* factory)
    {
        IDXGIFactory5* factory5 = null;
        try
        {
            var iid = D3D11Interop.Iid<IDXGIFactory5>();
            if (factory->QueryInterface(&iid, (void**)&factory5) < 0 || factory5 is null)
                return false;

            BOOL supported = false;
            if (factory5->CheckFeatureSupport(
                    DXGI_FEATURE.DXGI_FEATURE_PRESENT_ALLOW_TEARING, &supported, (uint)sizeof(BOOL)) < 0)
            {
                return false;
            }

            return supported;
        }
        finally
        {
            D3D11Interop.Release(ref factory5);
        }
    }

    /// <summary>
    /// Creates the swapchain and everything that hangs off it.
    ///
    /// The flip model is requested first and a plain blt-model swapchain is the fallback, because the
    /// flip model is what makes the tearing present and the low-latency path possible at all, but it
    /// is also pickier: an old driver, or a window whose format DXGI will not flip, fails
    /// <c>CreateSwapChainForHwnd</c> rather than degrading. Falling back to <c>DISCARD</c> is what
    /// FNA3D's driver effectively does by never asking for flip in the first place.
    /// </summary>
    private void CreateSwapChain(nint windowHandle, int width, int height)
    {
        var hwnd = (HWND)windowHandle;

        IDXGISwapChain1* swapChain1 = null;
        var created = false;

        if (_factory is not null)
        {
            var desc = new DXGI_SWAP_CHAIN_DESC1
            {
                Width = (uint)width,
                Height = (uint)height,
                Format = BackBufferFormat,
                Stereo = false,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                BufferUsage = D3D11Interop.UsageRenderTargetOutput,
                BufferCount = BufferCount,
                // STRETCH rather than NONE: NONE makes DXGI letterbox with black bars when the
                // back buffer size and the client area disagree, which happens for the frame or two
                // between a window resize and this backend's Resize call.
                Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH,
                SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_DISCARD,
                // UNSPECIFIED rather than IGNORE: IGNORE is the documented requirement for a
                // composition swapchain and is rejected for an HWND one on some drivers.
                AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_UNSPECIFIED,
                Flags = _tearingSupported ? D3D11Interop.SwapChainFlagAllowTearing : 0u,
            };

            var hr = _factory->CreateSwapChainForHwnd(
                (IUnknown*)_device.Device, hwnd, &desc, null, null, &swapChain1);

            if (hr >= 0)
            {
                created = true;
            }
            else
            {
                D3D11Interop.Release(ref swapChain1);

                // The flip-model attempt failed, so tearing is off the table by definition - the flag
                // is only legal on a flip-model swapchain, and this fallback is a blt-model one.
                var fallback = new DXGI_SWAP_CHAIN_DESC
                {
                    BufferDesc = new DXGI_MODE_DESC
                    {
                        Width = (uint)width,
                        Height = (uint)height,
                        RefreshRate = new DXGI_RATIONAL { Numerator = 0, Denominator = 1 },
                        Format = BackBufferFormat,
                        ScanlineOrdering = DXGI_MODE_SCANLINE_ORDER.DXGI_MODE_SCANLINE_ORDER_UNSPECIFIED,
                        Scaling = DXGI_MODE_SCALING.DXGI_MODE_SCALING_UNSPECIFIED,
                    },
                    SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                    BufferUsage = D3D11Interop.UsageRenderTargetOutput,
                    BufferCount = BufferCount,
                    OutputWindow = hwnd,
                    Windowed = true,
                    SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_DISCARD,
                    Flags = 0,
                };

                IDXGISwapChain* bltChain = null;
                D3D11Interop.Check(
                    _factory->CreateSwapChain((IUnknown*)_device.Device, &fallback, &bltChain),
                    "IDXGIFactory1::CreateSwapChain (flip model rejected; blt model)",
                    "The flip-model swapchain failed, which usually means the driver is too old for " +
                    "DXGI_SWAP_EFFECT_FLIP_DISCARD. Tearing support and the low-latency present are " +
                    "unavailable on the fallback.");

                swapChain1 = (IDXGISwapChain1*)bltChain;
                created = true;
            }
        }

        if (!created)
        {
            throw new InvalidOperationException(
                "No DXGI adapter could be reached from this device, so no swapchain can be created. " +
                "This backend requires the device to have come from a real adapter - see " +
                "D3D11Swapchain.GetFactoryFor.");
        }

        _swapChain = (IDXGISwapChain*)swapChain1;

        // Only NO_WINDOW_CHANGES, which is what FNA3D passes: it stops DXGI from registering its own
        // resize and mode handling behind the app's back, while leaving Alt+Enter and Print Screen to
        // DXGI. Registering the full set would have DXGI resizing a window whose size the app is
        // tracking itself.
        if (_factory is not null)
            _factory->MakeWindowAssociation(hwnd, D3D11Interop.MwaNoWindowChanges);

        CreateBuffers();
    }

    /// <summary>
    /// Acquires the back buffer, builds the views over it, and allocates the multisampled and
    /// depth-stencil textures that go with it.
    ///
    /// Called at construction and again after every <c>ResizeBuffers</c>, which is the only thing
    /// that invalidates any of the five objects.
    /// </summary>
    private void CreateBuffers()
    {
        var backBufferIid = D3D11Interop.Iid<ID3D11Texture2D>();
        ID3D11Texture2D* backBuffer = null;
        D3D11Interop.Check(
            _swapChain->GetBuffer(0, &backBufferIid, (void**)&backBuffer), "IDXGISwapChain::GetBuffer(0)");
        _backBuffer = backBuffer;

        ID3D11RenderTargetView* backBufferView = null;
        D3D11Interop.Check(
            _device.Device->CreateRenderTargetView((ID3D11Resource*)backBuffer, null, &backBufferView),
            "CreateRenderTargetView (back buffer)");
        _backBufferView = backBufferView;

        if (MultiSampleCount > 1)
        {
            var multisampledDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)Width,
                Height = (uint)Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = BackBufferFormat,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = (uint)MultiSampleCount, Quality = 0 },
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                // The resolve reads it, so it has to be bindable as a shader resource - and D3D11
                // requires the source of a resolve to be a render target, which the first flag gives.
                BindFlags = D3D11Flags.BindRenderTarget | D3D11Flags.BindShaderResource,
                CPUAccessFlags = 0,
                MiscFlags = 0,
            };

            ID3D11Texture2D* multisampled = null;
            D3D11Interop.Check(
                _device.Device->CreateTexture2D(&multisampledDesc, null, &multisampled),
                $"CreateTexture2D (multisampled back buffer, {Width}x{Height}, {MultiSampleCount}x)");
            _multisampledTexture = multisampled;

            ID3D11RenderTargetView* multisampledView = null;
            D3D11Interop.Check(
                _device.Device->CreateRenderTargetView((ID3D11Resource*)multisampled, null, &multisampledView),
                "CreateRenderTargetView (multisampled back buffer)");
            _multisampledView = multisampledView;
        }

        // The swapchain owns the depth buffer because the app's "no target" binding has to have one:
        // WorldGame.Draw clears depth and stencil against the back buffer every frame, and a DXGI
        // back buffer has no depth attachment. The sample count has to match the colour target's or
        // D3D11 rejects the render target set as a whole.
        var depthDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)Width,
            Height = (uint)Height,
            MipLevels = 1,
            ArraySize = 1,
            // Typeless, so the depth-stencil view below can pick the concrete format - the same split
            // D3D11RenderTarget makes for off-screen targets, and required for the same reason.
            Format = _depthFormat.ToDxgiFormat(),
            SampleDesc = new DXGI_SAMPLE_DESC { Count = (uint)MultiSampleCount, Quality = 0 },
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11Flags.BindDepthStencil,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* depth = null;
        D3D11Interop.Check(
            _device.Device->CreateTexture2D(&depthDesc, null, &depth),
            $"CreateTexture2D (depth-stencil, {Width}x{Height}, {MultiSampleCount}x)");
        _depthTexture = depth;

        var depthViewDesc = new D3D11_DEPTH_STENCIL_VIEW_DESC
        {
            Format = _depthFormat.ToDepthStencilViewFormat(),
            ViewDimension = MultiSampleCount > 1
                ? D3D11_DSV_DIMENSION.D3D11_DSV_DIMENSION_TEXTURE2DMS
                : D3D11_DSV_DIMENSION.D3D11_DSV_DIMENSION_TEXTURE2D,
            Flags = 0,
        };
        depthViewDesc.Anonymous.Texture2D.MipSlice = 0;

        ID3D11DepthStencilView* depthView = null;
        D3D11Interop.Check(
            _device.Device->CreateDepthStencilView((ID3D11Resource*)depth, &depthViewDesc, &depthView),
            "CreateDepthStencilView (swapchain depth buffer)");
        _depthView = depthView;
    }

    /// <summary>
    /// Reallocates the back buffers.
    ///
    /// Zero or less is ignored, so a minimized window keeps its last drawable - the same rule the GL
    /// backends follow, and the reason is the same: a zero-sized back buffer is not constructible and
    /// the size the app reports would then be wrong for every viewport and render target laid out
    /// from it.
    /// </summary>
    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        if (width <= 0 || height <= 0)
            return;

        // The abstraction's convention, shared with the GL backends: 0 means "leave the count alone"
        // and 1 is the UI's "off". Resolved before the early-out so an unchanged request is cheap.
        var requested = multiSampleCount switch
        {
            0 => _requestedMultiSampleCount,
            1 => 0,
            _ => multiSampleCount,
        };

        var clamped = _device.ClampSampleCount(BackBufferFormat, requested);

        if (width == Width && height == Height && clamped == MultiSampleCount)
        {
            // The size and count both stand, but the request may still have changed - a clamped count
            // has to be recorded so the caller's comparison against it converges. See
            // _requestedMultiSampleCount.
            _requestedMultiSampleCount = requested;
            return;
        }

        _requestedMultiSampleCount = requested;

        // Everything over the back buffers must go before ResizeBuffers: DXGI refuses to resize a
        // swapchain whose buffers still have outstanding references, and the views and the acquired
        // texture are exactly that.
        ReleaseBuffers();

        D3D11Interop.Check(
            _swapChain->ResizeBuffers(
                BufferCount, (uint)width, (uint)height, BackBufferFormat,
                _tearingSupported ? D3D11Interop.SwapChainFlagAllowTearing : 0u),
            $"IDXGISwapChain::ResizeBuffers({width}x{height})");

        Width = width;
        Height = height;
        MultiSampleCount = clamped;
        CreateBuffers();
    }

    private void ReleaseBuffers()
    {
        D3D11Interop.Release(ref _depthView);
        D3D11Interop.Release(ref _depthTexture);
        D3D11Interop.Release(ref _multisampledView);
        D3D11Interop.Release(ref _multisampledTexture);
        D3D11Interop.Release(ref _backBufferView);
        D3D11Interop.Release(ref _backBuffer);
    }

    /// <inheritdoc cref="ISwapchain.VSync"/>
    /// <remarks>
    /// Stored and read by <see cref="Present"/>, which is what the present interval is an argument to.
    /// </remarks>
    public bool VSync { get; set; } = true;

    /// <summary>
    /// Presents.
    ///
    /// A multisampled draw is resolved into the back buffer first, because the multisampled texture
    /// is not part of the swapchain at all - it is this backend's private surface, and the resolve is
    /// what makes the frame visible.
    ///
    /// Both the sync interval and the tearing flag are decided together and the pair is constrained:
    /// <c>DXGI_PRESENT_ALLOW_TEARING</c> is only accepted with a sync interval of zero, and a flip
    /// present with a non-zero interval is what actually waits for vblank. So the choice is made here
    /// from the app's vsync setting and nothing else - see <see cref="Present"/>'s caller,
    /// <c>WorldGame.SyncBackendVSync</c>, which is where that setting arrives.
    ///
    /// The consequence worth stating, because it is the reverse of what the setting's name suggests:
    /// <em>turning vsync off is what buys the tearing flag</em>, not the other way round. A vsynced
    /// present passes interval 1 and no flag and therefore always waits for vblank; only the
    /// unsynchronized present is allowed to tear, and only when the swapchain was created with the
    /// flag. So <see cref="VSync"/> false on a tearing-capable swapchain is a genuine uncapped
    /// present, while on one that is not it is merely an unsynchronized blt.
    /// </summary>
    public void Present()
    {
        var context = _device.Context;

        if (_multisampledTexture is not null && _backBuffer is not null)
        {
            // Legal into a swapchain back buffer because the destination is a DEFAULT-usage,
            // single-sampled resource and the source is multisampled - the two conditions the API
            // states, both satisfied above.
            context->ResolveSubresource(
                (ID3D11Resource*)_backBuffer, 0, (ID3D11Resource*)_multisampledTexture, 0, BackBufferFormat);
        }

        // The interval and the flag move together: ALLOW_TEARING is rejected outright with a non-zero
        // interval, so the flag can only be set on the branch that already passes zero.
        var interval = VSync ? 1u : 0u;
        var flags = VSync || !_tearingSupported ? 0u : D3D11Interop.PresentAllowTearing;

        D3D11Interop.Check(_swapChain->Present(interval, flags), "IDXGISwapChain::Present");
    }

    /// <summary>
    /// Whether this swapchain accepts <c>DXGI_PRESENT_ALLOW_TEARING</c>, which is what a caller needs
    /// to know to decide between a tearing present and a synchronized one.
    /// </summary>
    internal bool AllowsTearing => _tearingSupported;

    public void Dispose()
    {
        ReleaseBuffers();
        D3D11Interop.Release(ref _swapChain);
        D3D11Interop.Release(ref _factory);
    }
}
