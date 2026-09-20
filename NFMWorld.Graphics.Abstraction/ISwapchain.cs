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
