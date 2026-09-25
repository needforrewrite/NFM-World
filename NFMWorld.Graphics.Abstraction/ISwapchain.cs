namespace NFMWorld.Graphics;

public interface ISwapchain
{
    /// <summary>Width of the drawable the device renders into, in pixels. This is what render
    /// targets and viewports default to, so it must track the OS window - see <see cref="Resize"/>.</summary>
    int Width { get; }

    /// <summary>Height of the drawable the device renders into, in pixels.</summary>
    int Height { get; }

    /// <summary>
    /// Multisample count the drawable was actually allocated with. A request passed to
    /// <see cref="Resize"/> may be reduced to what the device supports, so this is the value to
    /// display back to the user, not what was asked for.
    /// </summary>
    int MultiSampleCount { get; }

    /// <summary>
    /// Whether a change to the multisample count can only take effect on the next launch, rather
    /// than being applied by <see cref="Resize"/>.
    ///
    /// True where the count is a property of the <em>context</em> rather than the drawable. The two
    /// GL backends render into the host window's default framebuffer, and a window's pixel format -
    /// and therefore its sample count - is fixed when the GL context is created, so there is nothing
    /// a resize could do to change it. D3D11 and FNA3D rebuild the swapchain's buffers instead and
    /// can honour a new count mid-session, so they report false.
    ///
    /// Callers use this to decide whether to tell the user a restart is needed: a backend that
    /// cannot apply the change would otherwise accept the request and silently keep rendering with
    /// the old count, which reads as the setting being broken.
    /// </summary>
    bool MultiSampleChangeRequiresRestart { get; }

    /// <summary>
    /// Reallocates the drawable to the given pixel size and, when <paramref name="multiSampleCount"/>
    /// is greater than zero, to that multisample count - which is a backbuffer property and can only
    /// be changed by rebuilding it here, not by any per-draw pipeline state. Call after the OS window
    /// changes size (including a fullscreen/borderless toggle), or after the user picks a new MSAA
    /// setting. Passing zero for <paramref name="multiSampleCount"/> keeps the current count, so the
    /// existing callers that only want a resize are unaffected; pass one to turn MSAA off.
    /// <see cref="Width"/>/<see cref="Height"/>/<see cref="MultiSampleCount"/> then report what the
    /// backend actually allocated, which the caller should re-read rather than assume. Sizes of zero
    /// or less are ignored - a minimized window keeps its last drawable.
    /// </summary>
    void Resize(int width, int height, int multiSampleCount = 0);

    void Present();
}
