// LLM maintained.
//
// The swapchain: either the default framebuffer of an EGL pbuffer this backend owns, or the default
// framebuffer of a context the host application owns and made current (the SDL path).
//
// The two paths differ in exactly one capability, and it is the one the abstraction cares about:
// when this backend owns the EGL surface it can genuinely reallocate the drawable on Resize, and
// when the host owns the window the drawable is the host's to resize and this only tracks the new
// size. That is the same split the sokol backend has (SokolSwapchain cannot reallocate under
// sokol_app), except that here the owning case is the general one and the tracking case is the
// fallback.
namespace NFMWorld.Graphics.OpenGL;

/// <summary>
/// The default framebuffer, i.e. GL framebuffer object 0.
///
/// Nothing is wrapped in an FBO: GL's default framebuffer is whatever the EGL surface is, so the
/// swapchain's whole job is to report the drawable's size and to present.
/// </summary>
internal sealed class GlSwapchain : ISwapchain
{
    /// <summary>
    /// The EGL context this backend brought up, or null when the host owns the GL context.
    ///
    /// This is what decides whether <see cref="Resize"/> can reallocate: with a context the pbuffer
    /// is ours to rebuild; without one there is no EGL surface handle to rebuild.
    /// </summary>
    private readonly Egl.Context? _context;

    /// <summary>
    /// How to present when the host owns the GL context, or null when it does not need to be told.
    ///
    /// This exists because <c>eglSwapBuffers</c> is not the only way a frame reaches the screen. On
    /// the SDL path the host owns both the window and its surface, so the swap is SDL's
    /// (<c>SDL_GL_SwapWindow</c>) and this class has no handle to it. Without this the device would
    /// render correctly into a surface that is never shown - the failure looks like a black window
    /// and no error.
    /// </summary>
    private readonly Action? _present;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>
    /// The count the window (or the headless pbuffer) was actually created with, as read back from
    /// the windowing layer after the context was made.
    ///
    /// Zero unless multisampling was asked for before the window was built. On the headless path
    /// this is always zero: the pbuffer's EGL config is chosen by <see cref="Egl.Context.CreateHeadless"/>
    /// and requests no <c>EGL_SAMPLES</c>. As on the desktop path, the value is the allocation and
    /// not the request, because a driver may clamp or refuse it.
    /// </summary>
    public int MultiSampleCount { get; }

    /// <summary>
    /// True: the count is baked into the EGL config (or the context), and a surface rebuild does
    /// not touch it.
    ///
    /// <see cref="Resize"/> re-creates the pbuffer from the *same* config deliberately - EGL
    /// requires a surface to be created from the context's own config - so the sample count is one
    /// of the things that cannot change without a new context. That is a restart.
    /// </summary>
    public bool MultiSampleChangeRequiresRestart => true;

    /// <summary>
    /// The last count <em>requested</em>, which is deliberately not <see cref="MultiSampleCount"/>.
    ///
    /// See the desktop backend's copy of this field: comparing a request against a clamped
    /// allocation never converges, so requests are compared against requests.
    /// </summary>
    private int _requestedMultiSampleCount;

    internal GlSwapchain(Egl.Context? context, int width, int height, Action? present = null, int multiSampleCount = 0)
    {
        _context = context;
        _present = present;
        Width = width;
        Height = height;
        MultiSampleCount = multiSampleCount;
        _requestedMultiSampleCount = multiSampleCount;
    }

    /// <summary>
    /// Resizes the drawable.
    ///
    /// Zero or less is ignored, which matters because a minimized window reports 0x0 and a pbuffer
    /// of that size is not constructible. With no EGL context the size is still updated - the host
    /// has already resized its own window, and reporting a stale size would leave every viewport
    /// and render target sized for the old drawable.
    ///
    /// The sample count is recorded but not applied: it lives in the EGL config, which is reused
    /// across a pbuffer rebuild on purpose. See <see cref="MultiSampleChangeRequiresRestart"/>.
    /// </summary>
    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        if (width <= 0 || height <= 0)
            return;

        // See ISwapchain.Resize: 0 means "leave the count alone", and 1 is the UI's "MSAA 1x",
        // which means off. Resolved before the early-out so an unchanged request stays cheap.
        _requestedMultiSampleCount = multiSampleCount switch
        {
            0 => _requestedMultiSampleCount,
            1 => 0,
            _ => multiSampleCount,
        };

        if (width == Width && height == Height)
            return;

        _context?.ResizeSurface(width, height);
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Presents. With an EGL surface this is <c>eglSwapBuffers</c>; without one the host owns the
    /// window and presents it, which is what the callback added at construction is for. Both are
    /// null-checked rather than asserted: a host-owned context with no callback is a legitimate
    /// configuration (an offscreen host that reads its own framebuffer), and it simply means there is
    /// nothing to present.
    /// </summary>
    public void Present()
    {
        if (_context is not null)
            _context.SwapBuffers();
        else
            _present?.Invoke();
    }
}
