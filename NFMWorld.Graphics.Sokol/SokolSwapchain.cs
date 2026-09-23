using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// The sokol_app-owned drawable.
///
/// This is the one part of the abstraction that sokol cannot be a faithful backend for, and the
/// mismatch is structural rather than incidental: in FNA3D the device owns its backbuffer and
/// rebuilds it on demand (<c>FNA3D_ResetBackbuffer</c>), whereas here sokol_app creates the
/// swapchain and recreates it itself on resize, exposing it only through
/// <c>sglue_swapchain()</c>. There is no <c>sapp_set_window_size</c> in this sokol version and no
/// way to change the sample count after <c>sapp_run</c> has been entered.
///
/// So <see cref="Resize"/> cannot reallocate anything. All it can do - and all it does - is
/// re-read what sokol_app actually allocated, which is exactly what <see cref="ISwapchain.Resize"/>
/// tells callers to do instead of assuming their request was honored.
/// </summary>
internal sealed class SokolSwapchain : ISwapchain
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>
    /// The count sapp allocated the drawable with, taken from <c>sapp_sample_count()</c> - not the
    /// value passed to <see cref="SokolGraphicsDevice.Run"/>, which sapp may have reduced to what
    /// the device supports.
    /// </summary>
    public int MultiSampleCount { get; private set; }

    /// <summary>
    /// Re-reads the drawable's dimensions from sokol_app. Called once per frame by the loop in
    /// <see cref="SokolGraphicsDevice.Run"/> because sokol_app rebuilds the swapchain on a resize
    /// without telling us - and the OS may coalesce several resize events into one, so polling the
    /// size is more reliable than counting events.
    /// </summary>
    internal void Refresh()
    {
        if (App.isvalid() == 0) return;
        Width = App.width();
        Height = App.height();
        MultiSampleCount = App.sample_count();
    }

    /// <summary>
    /// Re-reads the drawable rather than resizing it - see the type remarks. The requested size and
    /// sample count are advisory only: sokol_app's drawable follows the OS window, and its sample
    /// count is fixed when the frame loop starts.
    /// </summary>
    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        // A minimized window reports 0x0 and there is no drawable to query; keep the last known
        // size, matching ISwapchain's "a minimized window keeps its last drawable" contract.
        if (width <= 0 || height <= 0) return;
        Refresh();
    }

    /// <summary>
    /// Nothing to do: sokol presents as part of <c>sg_commit()</c>, which
    /// <see cref="SokolCommandBuffer.Commit"/> issues at the end of the frame. Presenting here as
    /// well would commit a second time.
    /// </summary>
    public void Present()
    {
    }
}
