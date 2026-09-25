// LLM maintained.
//
// The swapchain: the default framebuffer of a context the host application owns and made current.
//
// There is only one path here, unlike the ANGLE backend's two. There the backend could also bring
// up its own EGL pbuffer and so genuinely reallocate the drawable on Resize; desktop GL has no such
// ownerless surface, so the host always owns the window and this only tracks its size. That is the
// same shape the sokol backend has (SokolSwapchain cannot reallocate under sokol_app).
namespace NFMWorld.Graphics.DesktopGL;

/// <summary>
/// The default framebuffer, i.e. GL framebuffer object 0.
///
/// Nothing is wrapped in an FBO: GL's default framebuffer is whatever the host's window is, so the
/// swapchain's whole job is to report the drawable's size and to present.
/// </summary>
internal sealed class GlSwapchain : ISwapchain
{
    /// <summary>
    /// How to present, or null when nothing does.
    ///
    /// This exists because the swap is not ours to make: the host owns both the window and its
    /// drawable, so presentation is the host's (<c>SDL_GL_SwapWindow</c>) and this class has no
    /// handle to it. Without this the device would render correctly into a drawable that is never
    /// shown - the failure looks like a black window and no error.
    /// </summary>
    private readonly Action? _present;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>
    /// The count the <em>window</em> was actually created with, as read back from SDL after the
    /// context was made.
    ///
    /// Zero unless the host asked SDL for multisampling before building the window. Both halves are
    /// deliberate: the value is the allocation and not the request, because a driver may clamp or
    /// refuse it and reporting the request would claim MSAA that is not there; and it is passed in
    /// rather than queried here, because this class has no GL calls of its own and the count is a
    /// property of the context the host owns.
    /// </summary>
    public int MultiSampleCount { get; }

    /// <summary>
    /// True: the count is baked into the context.
    ///
    /// A window's pixel format is fixed when its GL context is created, so nothing <see cref="Resize"/>
    /// can do would change the sample count - the change needs a new window and context, which is a
    /// restart. Reporting this rather than accepting the request is what stops the settings screen
    /// from looking like it worked when it cannot have.
    /// </summary>
    public bool MultiSampleChangeRequiresRestart => true;

    /// <summary>
    /// The last count <em>requested</em>, which is deliberately not <see cref="MultiSampleCount"/>.
    ///
    /// When the hardware clamps a request, comparing future requests against the clamped allocation
    /// would report a difference forever and rebuild the drawable every frame - the trap
    /// <c>WorldGame.EnsureSwapchainMatchesWindow</c> and <c>FNA3DSwapchain</c> both document.
    /// Requests are compared against requests; only the allocation is reported back to callers.
    /// </summary>
    private int _requestedMultiSampleCount;

    internal GlSwapchain(int width, int height, Action? present = null, int multiSampleCount = 0)
    {
        _present = present;
        Width = width;
        Height = height;
        MultiSampleCount = multiSampleCount;
        _requestedMultiSampleCount = multiSampleCount;
    }

    /// <summary>
    /// Records the new size, and the new multisample request.
    ///
    /// This cannot resize the drawable - the window is the host's, and the host has already resized
    /// it - but the size is still tracked, because reporting a stale one would leave every viewport
    /// and render target sized for the old drawable. Zero or less is ignored, which matters because
    /// a minimized window reports 0x0.
    ///
    /// The sample count is recorded but not applied, for the reason
    /// <see cref="MultiSampleChangeRequiresRestart"/> gives. Recording it is still necessary:
    /// <c>WorldGame</c> compares against what was last requested, so without this the same request
    /// would be re-issued every frame.
    /// </summary>
    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        if (width <= 0 || height <= 0)
            return;

        // See ISwapchain.Resize: 0 means "leave the count alone", and 1 is the UI's "MSAA 1x",
        // which means off.
        _requestedMultiSampleCount = multiSampleCount switch
        {
            0 => _requestedMultiSampleCount,
            1 => 0,
            _ => multiSampleCount,
        };

        Width = width;
        Height = height;
    }

    /// <summary>
    /// Presents by calling the host's swap callback.
    ///
    /// Null-checked rather than asserted: a host that owns the window but supplies no callback is a
    /// legitimate configuration (an offscreen host that reads its own framebuffer), and it simply
    /// means there is nothing to present.
    /// </summary>
    public void Present() => _present?.Invoke();
}
