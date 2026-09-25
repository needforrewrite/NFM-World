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
    /// Always zero.
    ///
    /// MSAA count is a property of the framebuffer configuration the context was created with, and
    /// the host's SDL window is asked for whatever SDL_GL_SetAttribute specified - not something
    /// this backend sets or can report. Reporting zero is the honest answer rather than a count the
    /// window may not have. MSAA here is an off-screen concern: a multisampled renderbuffer plus a
    /// resolve blit.
    /// </summary>
    public int MultiSampleCount => 0;

    internal GlSwapchain(int width, int height, Action? present = null)
    {
        _present = present;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Records the new size.
    ///
    /// This cannot resize the drawable - the window is the host's, and the host has already resized
    /// it - but the size is still tracked, because reporting a stale one would leave every viewport
    /// and render target sized for the old drawable. Zero or less is ignored, which matters because
    /// a minimized window reports 0x0.
    /// </summary>
    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        if (width <= 0 || height <= 0)
            return;

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
