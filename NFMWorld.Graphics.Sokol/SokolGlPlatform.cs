using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// <see cref="ISokolPlatform"/> for sokol's GLCORE backend, on a GL context SDL created and owns.
///
/// Much smaller than <see cref="SokolD3D11Platform"/>, and the reason is structural rather than a
/// matter of taste: the context is SDL's, and SDL also owns the drawable behind it. So there is no
/// device to create, no swapchain to build, no views to re-create on resize and nothing here to
/// release - only the four things sokol genuinely cannot do for itself: say which formats it will
/// see (<see cref="CreateEnvironment"/>), name the framebuffer to render into
/// (<see cref="AcquireSwapchain"/>), show the frame (<see cref="Present"/>), and follow the vsync
/// setting.
///
/// <para>
/// <b>The context must already be current when sokol is initialized</b>, and there is no way to make
/// that a compile-time requirement - so it is stated here instead. <c>_sg_gl_setup_backend</c>
/// (<c>sokol_gfx.h:11101</c>) takes no description at all; unlike the D3D11/Metal/Vulkan/WGPU arms,
/// GL has no <c>sg_gl_environment</c> for a caller to hand a device through (<c>sg_environment</c> is
/// <c>defaults</c> plus those four, verified in <c>SharpSokol/src/SharpSokol/Native/Gfx.cs</c>). It
/// immediately queries <c>GL_MAJOR_VERSION</c>/<c>GL_MINOR_VERSION</c> to fill in its capability
/// table (<c>_sg_gl_init_caps_glcore</c>, <c>:10494-10520</c>). Against no context those return
/// zeros, so sokol concludes it is running on GL 0.0 and quietly disables
/// <c>draw_base_instance</c> (needs 4.2), <c>gl_texture_views</c> (4.3) and dual-source blending
/// (3.3). None of that throws; it just stops working. Initializing sokol before the context is
/// current is therefore not a subtly wrong configuration, it is a silently degraded one.
/// </para>
///
/// <para>
/// Note what is <em>not</em> here: any GL function lookup. On Windows <c>_SOKOL_USE_WIN32_GL_LOADER</c>
/// is defined under <c>SOKOL_GLCORE</c> (<c>sokol_gfx.h:6009</c>) and sokol opens
/// <c>opengl32.dll</c> and resolves entry points through <c>wglGetProcAddress</c> itself
/// (<c>:9731-9747</c>, called from <c>:11108</c>). Nothing in this class needs to know a single GL
/// symbol, which is why there is no <c>SDL_GL_GetProcAddress</c> plumbing on this path the way the
/// older <c>GlGraphicsDevice</c> path needs it.
/// </para>
///
/// <para>
/// <b>Depth and stencil belong to SDL here, not to sokol.</b> A GLCORE swapchain pass binds
/// <c>swapchain->gl.framebuffer</c> and nothing else (<c>:12146-12159</c>): there is no attachment
/// list to fill in, so <c>depth_format</c> only tells sokol which depth <em>state</em> is available
/// for pipeline validation. The actual buffers are whatever the window's pixel format has, which is
/// why the caller must ask SDL for a stencil before creating the window - <c>SDL_GL_STENCIL_SIZE 8</c>,
/// the same thing the ANGLE path does and for the same reason.
/// </para>
///
/// <para>
/// SDL is reached through delegates rather than by referencing it. This assembly is the sokol
/// backend and knows nothing about windows; <c>WorldGame</c> owns SDL and passes in the two calls
/// this needs, exactly as it already does for <c>GlGraphicsDevice.Create</c>
/// (<c>WorldGame.cs:234-236</c>). So there is no project reference from here to
/// <c>NFMWorld.Platform.SDL3</c>, and no SDL type in a signature.
/// </para>
/// </summary>
public sealed class SokolGlPlatform : ISokolPlatform
{
    /// <summary>
    /// Shows the frame: <c>SDL_GL_SwapWindow</c> over the window this platform was built for.
    ///
    /// A delegate rather than a call because presenting is a windowing-library operation, not a GL
    /// one, and this assembly deliberately holds no windowing reference. The caller supplies the
    /// same kind of callback it already supplies to <c>GlGraphicsDevice.Create</c>.
    /// </summary>
    private readonly Action _present;

    /// <summary>
    /// Sets the swap interval: <c>SDL_GL_SetSwapInterval</c>.
    ///
    /// Separate from <see cref="_present"/> because GL's interval is sticky context state rather
    /// than a per-present argument the way DXGI's sync interval is, so it is pushed only when
    /// <see cref="VSync"/> actually changes rather than on every frame.
    /// </summary>
    private readonly Action<int> _setSwapInterval;

    private int _width;
    private int _height;
    private bool _vsync = true;
    private int _appliedVsync = -1;
    private bool _disposed;

    /// <summary>
    /// Always <see cref="IntPtr.Zero"/>: this platform owns no native handle. The window exists, but
    /// SDL holds it and nothing here needs it - presenting goes through <see cref="_present"/>.
    /// </summary>
    public IntPtr NativeHandle => IntPtr.Zero;

    public int Width => _width;

    public int Height => _height;

    /// <summary>
    /// True. A GL context is current on exactly one thread at a time, and this one is SDL's, is not
    /// shared (no <c>SDL_GL_SHARE_WITH_CURRENT_CONTEXT</c> was requested at creation) and is made
    /// current by whoever created the window. sokol's GL backend assumes the context it was set up
    /// against is the one it draws through, with no rebinding of its own.
    /// </summary>
    public bool? SingleThreadedLifetime => true;

    /// <summary>
    /// Whether <see cref="Present"/> waits for vblank.
    ///
    /// Read per present, applied only on change - see <see cref="_setSwapInterval"/>.
    /// </summary>
    public bool VSync
    {
        get => _vsync;
        set => _vsync = value;
    }

    /// <summary>
    /// Wraps a GL context SDL has already created and made current, over the window it belongs to.
    /// </summary>
    /// <param name="width">The initial drawable size, in pixels.</param>
    /// <param name="height">The initial drawable size, in pixels.</param>
    /// <param name="present">
    /// Shows the frame. The caller's <c>SDL_GL_SwapWindow(Window.Handle)</c>.
    /// </param>
    /// <param name="setSwapInterval">
    /// Sets the swap interval. The caller's <c>SDL_GL_SetSwapInterval</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">On either delegate being null.</exception>
    /// <exception cref="InvalidOperationException">
    /// On a non-positive drawable size. Unlike D3D11 there is no object to create, so this is the
    /// only construction-time check there can be - but it is worth making, because a zero-sized GL
    /// drawable renders nothing and reports no error.
    /// </exception>
    public SokolGlPlatform(int width, int height, Action present, Action<int> setSwapInterval)
    {
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(setSwapInterval);

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException(
                $"{nameof(SokolGlPlatform)} needs a positive drawable size, got {width}x{height}.");

        _present = present;
        _setSwapInterval = setSwapInterval;
        _width = width;
        _height = height;
    }

    public sg_environment CreateEnvironment(int width, int height, int sampleCount)
    {
        if (width > 0 && height > 0)
        {
            _width = width;
            _height = height;
        }

        return new sg_environment
        {
            defaults = new sg_environment_defaults
            {
                // Matches the window's format, and this is not merely decorative even on a swapchain
                // backend: `defaults` is also the default pixel_format and sample_count for every
                // sg_image created without one (sokol_gfx.h:596-604), so it governs this app's
                // offscreen render targets and textures too. RGBA8 rather than the D3D11 path's BGRA8
                // because that is what a GL window is.
                color_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,

                // The app's shadow-cascade and NanoVG passes both need a stencil, and the window must
                // have been created with one (see the class remarks) - this only has to agree with it.
                // DEPTH_STENCIL rather than DEPTH so those pipelines, which declare a stencil
                // operation, validate against it.
                depth_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,

                // Always one. SokolSwapchain.MultiSampleCount is fixed at one, and sokol asserts a
                // swapchain pass carries no resolve view unless this is greater (sokol_gfx.h:24803).
                sample_count = 1,
            },

            // No gl member exists to fill in - see the class remarks. GL's environment arm is
            // deliberately empty upstream because the context is already current by the time sokol
            // looks, unlike D3D11/Vulkan/Metal where the caller must hand over a device.
        };
    }

    public sg_swapchain AcquireSwapchain()
    {
        // A minimized window has no drawable, and sokol's documented way to skip a frame is an
        // invalid swapchain (sokol_gfx.h:550-554) rather than a zero-sized pass: reporting the last
        // known size would have sokol set a viewport over a drawable that no longer matches it.
        if (_width <= 0 || _height <= 0)
            return new sg_swapchain { invalid = 1 };

        return new sg_swapchain
        {
            width = _width,
            height = _height,
            sample_count = 1,
            color_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            depth_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,

            gl = new sg_gl_swapchain
            {
                // **Zero means the default framebuffer**, and that is the only correct value here.
                // sokol's GLCORE swapchain branch binds exactly this and nothing else
                // (`glBindFramebuffer(GL_FRAMEBUFFER, swapchain->gl.framebuffer)`,
                // sokol_gfx.h:12158) - unlike its D3D11/Vulkan arms there is no attachment list, so
                // the colour and depth-stencil buffers are the window's own. A non-zero value would
                // have to name a framebuffer object this class had created and attached views to,
                // which is the *offscreen* path (the `else` branch at :12146), not a swapchain pass.
                framebuffer = 0,
            },
        };
    }

    /// <summary>
    /// Records the new size and nothing else.
    ///
    /// The drawable is the window's, so GL and SDL have already resized it by the time the OS reports
    /// the change; there is no object here to reallocate. That differs from
    /// <see cref="SokolD3D11Platform.Resize"/>, which genuinely rebuilds a DXGI swapchain - but it is
    /// still a real implementation of the contract rather than the sokol_app no-op: the size recorded
    /// here is what <see cref="AcquireSwapchain"/> reports for sokol's viewport and scissor setup, so
    /// getting it right is what makes a resize land correctly.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Applies the vsync setting if it changed, then shows the frame.
    ///
    /// Must be called after <c>sg_commit</c>: nothing in sokol_gfx presents on its own
    /// (<c>_sg_gl_commit</c> is empty, <c>sokol_gfx.h:12282</c>), so this is the only thing that shows
    /// a frame on this backend.
    /// </summary>
    public void Present()
    {
        ThrowIfDisposed();

        var interval = _vsync ? 1 : 0;
        if (interval != _appliedVsync)
        {
            // Only on a real change: this is a round trip to the driver, and GL's interval is sticky
            // state rather than a per-present argument the way DXGI's sync interval is.
            _setSwapInterval(interval);
            _appliedVsync = interval;
        }

        _present();
    }

    /// <summary>
    /// Does not destroy the GL context or the window: neither belongs to this class. The caller
    /// created the context and destroys it after the device is gone, because the device's GL objects
    /// live inside it.
    /// </summary>
    public void Dispose() => _disposed = true;

    /// <summary>
    /// Rejects use after disposal, so a late present names the cause rather than swapping a window
    /// that has already been destroyed - which on Win32 is undefined behaviour rather than an error.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SokolGlPlatform));
    }
}
