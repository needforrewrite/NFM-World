namespace NFMWorld.Graphics;

public interface ISwapchain
{
    /// <summary>Width of the drawable the device renders into, in pixels. This is what render
    /// targets and viewports default to, so it must track the OS window - see <see cref="Resize"/>.</summary>
    int Width { get; }

    /// <summary>Height of the drawable the device renders into, in pixels.</summary>
    int Height { get; }

    /// <summary>
    /// Reallocates the drawable to the given pixel size. Call after the OS window changes size
    /// (including a fullscreen/borderless toggle); <see cref="Width"/>/<see cref="Height"/> then
    /// report what the backend actually allocated, which the caller should re-read rather than
    /// assume. Sizes of zero or less are ignored - a minimized window keeps its last drawable.
    /// </summary>
    void Resize(int width, int height);

    void Present();
}
