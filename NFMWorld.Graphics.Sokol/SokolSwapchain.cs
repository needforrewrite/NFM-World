using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// The <see cref="ISwapchain"/> face of an <see cref="ISokolPlatform"/>, and the platform's owner.
///
/// One object rather than two, so that there is exactly one place a platform is created and exactly
/// one place it is disposed, and so <see cref="SokolGraphicsDevice.Run"/>'s frame loop can reach the
/// sokol_app-backed platform to refresh its cached size without a second field on the device.
///
/// This holds no drawable of its own, and that is the point: the platform owns the swapchain and
/// this only forwards. Under <see cref="SokolGraphicsDevice.Run"/> the platform was sokol_app, which
/// owned and rebuilt the swapchain itself - so <see cref="Resize"/> could not reallocate anything
/// and only re-read what sokol_app had already decided (<c>sapp_set_window_size</c> does not exist in
/// this sokol version, and the sample count is fixed once the frame loop starts). Under
/// <see cref="SokolGraphicsDevice.Create"/> the platform built the swapchain, so <see cref="Resize"/>
/// is a genuine request it honours.
///
/// The size still comes from the platform rather than being remembered here, because the platform is
/// what actually resized the drawable and it is the only party that knows whether the result matches
/// the request.
/// </summary>
internal sealed class SokolSwapchain(ISokolPlatform platform) : ISwapchain
{
    /// <summary>
    /// The platform this wraps. Exposed for two device-side uses: the <c>Run</c> frame loop's
    /// per-frame size refresh, and the read-only capability flags (<see cref="RequiresSingleThread"/>)
    /// that <see cref="SokolGraphicsDevice.EnsureRenderingThread"/> consults.
    /// </summary>
    internal ISokolPlatform Platform { get; } = platform;

    public int Width => Platform.Width;

    public int Height => Platform.Height;

    /// <summary>
    /// Always one - see <see cref="SokolGraphicsDevice"/>. A multisampled swapchain would need a
    /// resolve target sokol will not create, so a request for more is never honoured and this field
    /// is what tells the caller so.
    /// </summary>
    public int MultiSampleCount => 1;

    /// <summary>
    /// False: this swapchain's count never changes, so nothing about it can need a restart.
    ///
    /// Reporting false rather than true is correct here even though the count is as immovable as
    /// the GL backends' - what this property answers is whether a <em>user-visible setting change</em>
    /// can take effect, and sokol's answer is that it always already has: a request for more than
    /// one is refused at <see cref="Resize"/> and reported as 1 immediately, on the same frame. A
    /// restart would change nothing.
    /// </summary>
    public bool MultiSampleChangeRequiresRestart => false;

    /// <summary>
    /// Whether the platform's 3D API must be used from the thread that created it. Forwarded to
    /// <see cref="SokolGraphicsDevice.EnsureRenderingThread"/>, which is the only reader.
    /// </summary>
    internal bool RequiresSingleThread => Platform.SingleThreadedLifetime is true;

    /// <summary>
    /// The drawable for the frame about to be rendered, straight from the platform.
    ///
    /// Called through <see cref="SokolGraphicsDevice.AcquireSwapchain"/> once per pass rather than
    /// once per frame, which is safe because there is no way for the views to change mid-frame: the
    /// only thing that replaces them is a resize, and both resize paths
    /// (<see cref="WorldGame"/>'s handler and its per-frame check) run outside a command buffer.
    /// </summary>
    internal sg_swapchain Acquire() => Platform.AcquireSwapchain();

    /// <summary>
    /// Asks the platform to rebuild its drawable. On the <c>Create</c> path that is a real
    /// reallocation; on the <c>Run</c> path it is not, and <see cref="SokolGraphicsDevice.Run"/>'s
    /// platform documents why. Either way <see cref="MultiSampleCount"/> stays 1 - the size half of
    /// the request is honoured, the sample count half is not.
    /// </summary>
    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        if (width <= 0 || height <= 0) return;
        Platform.Resize(width, height);
    }

    /// <summary>
    /// Shows the frame. Under sokol_app this was a no-op, because sokol_app presents after its frame
    /// callback returns. On the <c>Create</c> path it is the only thing that presents anything:
    /// sokol_gfx never does (<c>_sg_d3d11_commit</c> is an empty function, <c>sokol_gfx.h:15217</c>),
    /// so this call is load-bearing rather than decorative. It must come after <c>sg_commit</c>.
    /// </summary>
    public void Present() => Platform.Present();

    /// <summary>
    /// Disposing the platform here rather than from the device is what keeps the two in step: the
    /// device nulls its reference to this object when it shuts down, and nothing else holds one.
    /// </summary>
    public void Dispose() => Platform.Dispose();
}
