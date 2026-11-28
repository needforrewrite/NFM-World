// LLM maintained.
//
// The presentation surface, and the attachments a pass into "no render target" draws against.
//
// Two things here are not obvious. WebGPU has no multisampled surface, so a multisampled back
// buffer is an offscreen colour texture whose pass resolves into the acquired surface texture - the
// same structure the D3D11 backend uses. And the surface texture is not ours: wgpu hands it over for
// one frame and reclaims it when it is released after present, so the swapchain keeps it and the
// view it made rather than wrapping it in a WebGpuTexture it would then try to destroy.

using Wg = Maxine.WebGPU;
using WgpuTexture = Maxine.WebGPU.Texture;

namespace NFMWorld.Graphics.WebGPU;

internal sealed unsafe class WebGpuSwapchain : ISwapchain
{
    private readonly WebGpuGraphicsDevice _device;
    private readonly Wg.Surface* _surface;
    private readonly TextureFormat _depthStencilFormat;

    private Wg.TextureFormat _format;
    private int _width;
    private int _height;
    private int _sampleCount;

    /// <summary>The multisampled colour buffer, or null when the surface is drawn single-sampled.</summary>
    private WebGpuTexture? _multisampled;

    /// <summary>The single-sampled colour buffer of a headless device, which stands in for the surface.</summary>
    private WebGpuTexture? _offscreen;

    private WebGpuTexture? _depth;

    private WgpuTexture* _surfaceTexture;
    private Wg.TextureView* _surfaceView;

    /// <summary>
    /// Whether a surface texture is currently held. A frame that never opened a pass into the
    /// surface has nothing to present, and presenting anyway is an error.
    /// </summary>
    private bool _frameAcquired;

    public int Width => _width;
    public int Height => _height;
    public int MultiSampleCount => _sampleCount;

    /// <summary>False: like D3D11, this backend rebuilds its attachments on demand.</summary>
    public bool MultiSampleChangeRequiresRestart => false;

    /// <summary>The view a pass draws into: the multisampled buffer when there is one.</summary>
    internal Wg.TextureView* ColorView
    {
        get
        {
            if (_multisampled is not null)

                return _multisampled.View;

            return _surfaceView is not null ? _surfaceView : _offscreen?.View;
        }
    }

    /// <summary>Where a multisampled pass resolves to, or null when nothing does.</summary>
    internal Wg.TextureView* ResolveView
    {
        get
        {
            if (_multisampled is null)

                return null;

            return _surfaceView is not null ? _surfaceView : _offscreen?.View;
        }
    }

    internal Wg.TextureView* DepthView => _depth?.View;

    /// <summary>The colour attachment's format, which a pipeline variant keys on.</summary>
    internal Wg.TextureFormat ColorFormat => _multisampled?.GpuFormat ?? _offscreen?.GpuFormat ?? _format;

    /// <summary>The depth attachment's format, or Undefined when the swapchain has none.</summary>
    internal Wg.TextureFormat DepthFormat => _depth?.GpuFormat ?? Wg.TextureFormat.Undefined;

    internal bool HasDepth => _depth is not null;

    /// <summary>The single-sampled colour buffer a headless device can read back.</summary>
    internal WebGpuTexture? BackBuffer => _offscreen;

    /// <summary>
    /// The present mode, re-applied by reconfiguring the surface: unlike a GL interval this is baked
    /// into the surface's configuration, so it cannot be changed by presenting differently.
    /// </summary>
    public bool VSync
    {
        get;
        set
        {
            if (field == value)

                return;
            field = value;
            Configure();
        }
    } = true;

    internal WebGpuSwapchain(
        WebGpuGraphicsDevice device,
        int width,
        int height,
        int multiSampleCount,
        TextureFormat depthStencilFormat
    )
    {
        _device = device;
        _surface = device.SurfacePointer;
        _depthStencilFormat = depthStencilFormat;
        _format = device.SurfaceFormat;
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _sampleCount = NormalizeSampleCount(multiSampleCount);

        Allocate();
    }

    /// <summary>
    /// Acquires the frame's surface texture. Called by the command buffer before the first pass that
    /// draws into the surface, so a command buffer that only touches render targets never waits on
    /// the compositor.
    /// </summary>
    internal bool BeginFrame()
    {
        if (_surface is null || _surfaceView is not null)

            return true;

        var surfaceTexture = default(Wg.SurfaceTexture);
        _device.Api.SurfaceGetCurrentTexture(_surface, &surfaceTexture);

        switch (surfaceTexture.Status)
        {
            // Suboptimal means the surface would rather be reconfigured but this frame is usable;
            // dropping it would show a stall for a change the next Configure picks up anyway.
            case Wg.SurfaceGetCurrentTextureStatus.SuccessOptimal:
            case Wg.SurfaceGetCurrentTextureStatus.SuccessSuboptimal:

                break;
            case Wg.SurfaceGetCurrentTextureStatus.Outdated:
            case Wg.SurfaceGetCurrentTextureStatus.Lost:
                // Reconfigure and skip this frame: presenting a stale size is worse than one missed
                // frame, and the next AcquireCommandBuffer tries again.
                if (surfaceTexture.Texture is not null)
                    _device.Api.TextureRelease(surfaceTexture.Texture);
                Configure();

                return false;
            default:

                // Timeout and Error. Both leave the texture's ownership with wgpu, so releasing it
                // here would be a double free; only the frame is lost.

                return false;
        }

        _surfaceTexture = surfaceTexture.Texture;
        var descriptor = new Wg.TextureViewDescriptor
        {
            Format = _format,
            Dimension = Wg.TextureViewDimension.Dimension2D,
            BaseMipLevel = 0,
            MipLevelCount = 1,
            BaseArrayLayer = 0,
            ArrayLayerCount = 1,
            Aspect = Wg.TextureAspect.All,
            // The view must be usable for what the passes do with it, which is drawing into it,
            // and sampling from it when it is the resolve source of a multisampled frame.
            Usage = Wg.TextureUsage.RenderAttachment,
        };

        _surfaceView = _device.Api.TextureCreateView(_surfaceTexture, &descriptor);
        _device.CheckCreated(_surfaceView is not null, "surface texture view");
        _frameAcquired = true;

        return true;
    }

    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        if (width <= 0 || height <= 0)

            return;

        var samples = multiSampleCount > 0 ? NormalizeSampleCount(multiSampleCount) : _sampleCount;

        if (width == _width && height == _height && samples == _sampleCount)

            return;

        _width = width;
        _height = height;
        _sampleCount = samples;

        // A configure is refused while a frame is still acquired, so hand the frame back first.
        DiscardFrame();
        ReleaseAttachments();
        Allocate();
    }

    /// <summary>
    /// Drops the frame's surface texture without presenting it. wgpu-native discards the acquisition
    /// when the texture goes with no present in between, which is what a resize or a teardown wants.
    /// </summary>
    private void DiscardFrame()
    {
        if (_surfaceView is not null)
        {
            _device.Api.TextureViewRelease(_surfaceView);
            _surfaceView = null;
        }

        if (_surfaceTexture is not null)
        {
            _device.Api.TextureRelease(_surfaceTexture);
            _surfaceTexture = null;
        }

        _frameAcquired = false;
    }

    /// <summary>Drops an acquired frame the device abandoned after a mid-frame failure.</summary>
    internal void AbandonFrame() => DiscardFrame();

    public void Present()
    {
        if (_surface is null || !_frameAcquired)

            return;

        _frameAcquired = false;

        if (_surfaceView is not null)
        {
            _device.Api.TextureViewRelease(_surfaceView);
            _surfaceView = null;
        }

        // Present before releasing the acquired texture, not after: wgpu-native's texture destructor
        // discards the frame when the surface has not been presented yet, so releasing first leaves
        // present with nothing to hand to the compositor.
        //
        // A browser has no present call at all: wgpuSurfacePresent aborts, and the canvas shows the
        // screen at the end of the animation frame that wrote it. Releasing the texture is the whole
        // of a present there.

        if (!_device.IsBrowser)
        {
            var status = _device.Api.SurfacePresent(_surface);
            if (status != Wg.Status.Success)
                _device.ReportSurfaceError($"presenting the frame failed ({status})");
        }

        if (_surfaceTexture is not null)
        {
            _device.Api.TextureRelease(_surfaceTexture);
            _surfaceTexture = null;
        }
    }

    private void Allocate()
    {
        if (_surface is null)
        {
            // Headless: the resolve target is a real texture, which is what the smoke harness reads.
            _offscreen = new WebGpuTexture(
                _device,
                _width,
                _height,
                TextureFormat.Bgra8,
                renderTargetable: true,
                mipMapped: false
            );
        }
        else
        {
            Configure();
        }

        if (_sampleCount > 1)
        {
            _multisampled = new WebGpuTexture(
                _device,
                _width,
                _height,
                _format == Wg.TextureFormat.Bgra8Unorm ? TextureFormat.Bgra8 : TextureFormat.Rgba8,
                renderTargetable: true,
                mipMapped: false,
                sampleCount: _sampleCount
            );
        }

        _depth = new WebGpuTexture(
            _device,
            _width,
            _height,
            _depthStencilFormat,
            renderTargetable: true,
            mipMapped: false,
            sampleCount: _sampleCount
        );
    }

    /// <summary>
    /// (Re)configures the surface at the current size. Called on allocation, on resize and after a
    /// lost or outdated acquisition.
    /// </summary>
    private void Configure()
    {
        if (_surface is null)

            return;

        var capabilities = default(Wg.SurfaceCapabilities);

        if (_device.Api.SurfaceGetCapabilities(_surface, _device.Adapter, &capabilities) != Wg.Status.Success)
        {
            // An unconfigured surface still needs a configuration; without capabilities the current
            // format is the only sane guess, so configure with it and let the next acquisition fail
            // loudly rather than silently reusing a stale size.
            ConfigureWith(_format, VSync ? Wg.PresentMode.Fifo : Wg.PresentMode.Immediate);

            return;
        }

        try
        {
            // The preferred format can be an sRGB variant, which no TextureFormat maps to. Choosing the
            // linear one from the same capability list keeps the multisampled buffer and the resolve
            // target on one format, which WebGPU requires of a pass.
            _format = ChooseFormat(capabilities, _format);

            var presentMode = VSync ? Wg.PresentMode.Fifo : Wg.PresentMode.Immediate;
            var supported = false;

            for (nuint i = 0; i < capabilities.PresentModeCount; i++)
            {
                if (capabilities.PresentModes[i] == presentMode)
                    supported = true;
            }

            if (!supported)
                presentMode = Wg.PresentMode.Fifo;

            ConfigureWith(_format, presentMode);
        }
        finally
        {
            _device.Api.SurfaceCapabilitiesFreeMembers(capabilities);
        }
    }

    private void ConfigureWith(Wg.TextureFormat format, Wg.PresentMode presentMode)
    {
        var configuration = new Wg.SurfaceConfiguration
        {
            Device = _device.Device,
            Format = format,
            Usage = Wg.TextureUsage.RenderAttachment,
            Width = (uint)_width,
            Height = (uint)_height,
            PresentMode = presentMode,
            AlphaMode = Wg.CompositeAlphaMode.Auto,
        };

        _device.Api.SurfaceConfigure(_surface, &configuration);
    }

    /// <summary>
    /// The surface format to configure and render with. Queried by <see cref="WebGpuGraphicsDevice.Create"/>
    /// before the swapchain exists, which is why it is static, and reused by <see cref="Configure"/>
    /// so the boot log and the configured surface can never disagree.
    /// </summary>
    internal static Wg.TextureFormat PickSurfaceFormat(
        Wg.WebGPU api,
        Wg.Surface* surface,
        Wg.Adapter* adapter,
        Wg.TextureFormat fallback
    )
    {
        var capabilities = default(Wg.SurfaceCapabilities);

        if (api.SurfaceGetCapabilities(surface, adapter, &capabilities) != Wg.Status.Success)

            return fallback;

        try
        {
            return ChooseFormat(capabilities, fallback);
        }
        finally
        {
            api.SurfaceCapabilitiesFreeMembers(capabilities);
        }
    }

    /// <summary>
    /// Picks the linear format the rest of the backend can match: Bgra8 first because RAD art is BGRA,
    /// then Rgba8, then whatever the surface lists, and only then the caller's guess.
    /// </summary>
    private static Wg.TextureFormat ChooseFormat(in Wg.SurfaceCapabilities capabilities, Wg.TextureFormat fallback)
    {
        if (HasFormat(capabilities, Wg.TextureFormat.Bgra8Unorm))

            return Wg.TextureFormat.Bgra8Unorm;
        if (HasFormat(capabilities, Wg.TextureFormat.Rgba8Unorm))

            return Wg.TextureFormat.Rgba8Unorm;

        return capabilities.FormatCount > 0 ? capabilities.Formats[0] : fallback;
    }

    private static bool HasFormat(in Wg.SurfaceCapabilities capabilities, Wg.TextureFormat format)
    {
        for (nuint i = 0; i < capabilities.FormatCount; i++)
        {
            if (capabilities.Formats[i] == format)

                return true;
        }

        return false;
    }

    private void ReleaseAttachments()
    {
        _multisampled?.Dispose();
        _multisampled = null;
        _depth?.Dispose();
        _depth = null;

        // A headless back buffer is resized by replacing it; a surface's is the compositor's.

        if (_offscreen is not null)
        {
            _offscreen.Dispose();
            _offscreen = null;
        }
    }

    /// <summary>
    /// WebGPU supports 1x and 4x in core. A requested 2 becomes 4 rather than being silently
    /// flattened to 1, because the caller asked for smoothing and 4 is what the API can give it.
    /// </summary>
    private static int NormalizeSampleCount(int requested) => requested switch
    {
        <= 1 => 1,
        <= 4 => 4,
        _ => 4,
    };

    public void Dispose()
    {
        DiscardFrame();
        ReleaseAttachments();
    }
}
