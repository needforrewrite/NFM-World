using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// The window-system and 3D-API glue sokol_gfx deliberately does not provide.
///
/// This seam exists because of an explicit division of responsibility in sokol_gfx itself
/// (<c>sokol_gfx.h:101-113</c>):
///
/// <code>
///     sokol_gfx DOES NOT:
///     - create a window, swapchain or the 3D-API context/device, you must do this
///       before sokol_gfx is initialized, and pass any required information
///       (like 3D device pointers) to the sokol_gfx initialization call
///     - present the rendered frame, how this is done exactly usually depends
///       on how the window and 3D-API context/device was created
/// </code>
///
/// Upstream pairs sokol_gfx with sokol_app, whose <c>sglue_environment()</c>/<c>sglue_swapchain()</c>
/// fill both halves in. That is not an option here: sokol_app owns the window and the main loop, and
/// this application's window, input, ImGui backend and NanoVG plumbing are all SDL3's. So the two
/// halves are supplied here instead, per platform, and sokol_app is never initialized.
///
/// The corresponding half built by hand is not novel - it is what a platform whose window comes from
/// somewhere else must do, and there are two worked examples in this repository:
/// <c>Sokol.NET/examples/IOSSokolApp/MetalView.cs</c> and
/// <c>Sokol.NET/examples/AndroidSokolApp/OpenGLView.cs</c>, both of which hand-build an
/// <c>sg_environment</c> with the comment "Cannot use sapp_* functions as Sokol App is not
/// initialized".
///
/// <see cref="SokolD3D11Platform"/> is the only implementation today. Metal (SharpMetal), Vulkan and
/// GL are the intended follow-ons, and the split below is drawn so that adding one does not reshape
/// anything: a platform supplies its own 3D-API objects in <see cref="CreateEnvironment"/>, its own
/// per-frame drawable in <see cref="AcquireSwapchain"/>, and its own present in
/// <see cref="Present"/>. What it does *not* do is anything about sokol's own resource model, which
/// is why this interface mentions no <c>sg_image</c>/<c>sg_pipeline</c>/<c>sg_buffer</c>.
///
/// Implementations are created before <c>sg_setup</c> (the environment they hand back is
/// <c>sg_setup</c>'s input) and disposed after <c>sg_shutdown</c>.
/// </summary>
/// <remarks>
/// Public rather than internal only because it appears in the public signature of
/// <see cref="SokolGraphicsDevice.Create"/>; nothing outside this assembly is expected to implement
/// or reference it. Making <c>Create</c> internal instead would not help - the caller is
/// <c>nfm-world</c>, a different assembly, and it needs <c>Create</c>.
/// </remarks>
public interface ISokolPlatform : IDisposable
{
    /// <summary>
    /// The drawable's current width in pixels, as the platform last allocated it.
    ///
    /// A property rather than part of <see cref="AcquireSwapchain"/>'s return value because
    /// <c>SokolSwapchain</c> needs to answer <c>ISwapchain.Width</c> outside a pass, and because the
    /// value is the platform's record of what it actually allocated - not of what was asked for.
    /// A platform with no drawable yet reports 0.
    /// </summary>
    int Width { get; }

    /// <summary>The drawable's current height in pixels. See <see cref="Width"/>.</summary>
    int Height { get; }

    /// <summary>
    /// Whether sokol must not be touched from a thread other than the one that created this
    /// platform, or <c>null</c> when either is fine.
    ///
    /// Consulted by <see cref="SokolGraphicsDevice.EnsureRenderingThread"/> on the first
    /// <c>sg_*</c> call: when this is true and the calling thread differs from the creating thread,
    /// that method throws rather than letting the backend corrupt itself silently.
    ///
    /// It exists because of D3D11's immediate context, which is not thread-safe by default - a
    /// device created with <c>CreateDeviceFlag.Singlethreaded</c> (the flag name is D3D11's
    /// backwards spelling of "only use this from one thread") has no internal locking at all, and
    /// two threads issuing draws through it produces undefined ordering and torn state rather than
    /// an error. A platform that knows its device was created that way reports true here so the
    /// mistake is a clear exception at the first call instead of a corrupted frame much later.
    ///
    /// A platform that creates its device with internal thread protection, or an API that allows
    /// multi-threaded recording, can return <c>null</c> and accept calls from anywhere.
    /// </summary>
    bool? SingleThreadedLifetime { get; }

    /// <summary>
    /// The 3D-API objects and default formats <c>sg_setup</c> needs, built against the surface the
    /// platform owns.
    ///
    /// Called once, before <c>sg_setup</c>. For D3D11 the two required fields are
    /// <c>d3d11.device</c> and <c>d3d11.device_context</c> - sokol asserts on exactly those and
    /// nothing else (<c>sokol_gfx.h:13880-13892</c>): no <c>HWND</c>, no DXGI factory, no swapchain.
    /// Getting this narrow set is what makes hosting sokol on somebody else's window possible at all.
    ///
    /// <c>defaults</c> is not optional in practice even when every swapchain pass fills its own
    /// format fields: it is also the default <c>pixel_format</c> and <c>sample_count</c> for every
    /// <c>sg_image</c> created without one (<c>sokol_gfx.h:596-604</c>), so a wrong value here
    /// silently affects offscreen images rather than failing.
    /// </summary>
    sg_environment CreateEnvironment(int width, int height, int sampleCount);

    /// <summary>
    /// The drawable for the frame about to be rendered.
    ///
    /// Called once per frame, while no pass is open, and re-read rather than cached because the
    /// views change on resize - which is sokol_app's own rule for <c>sapp_acquire_swapchain</c>
    /// ("must be called exactly once per frame", <c>sokol_app.h:1935</c>).
    ///
    /// Two fields must be truthful even though sokol cannot check them: <c>width</c>/<c>height</c>
    /// are mandatory and the only values that matter for the viewport and scissor sokol sets on the
    /// caller's behalf (<c>sokol_gfx.h:24777-24778</c>, <c>:14855-14865</c>), and <c>color_format</c>
    /// is used to perform the MSAA resolve, which sokol does itself (<c>:14926-14943</c>).
    ///
    /// A minimized or otherwise unrenderable window should return a swapchain with
    /// <c>invalid = true</c> and every other member zeroed, which is the documented way to skip a
    /// frame (<c>sokol_gfx.h:550-554</c>) rather than to render into a zero-sized target.
    /// </summary>
    sg_swapchain AcquireSwapchain();

    /// <summary>
    /// Reallocates the drawable after the OS window changed size.
    ///
    /// Whether this can genuinely reallocate is the one capability the abstraction cares about, and
    /// it is what distinguishes this platform from the sokol_app path: sokol_app creates the
    /// swapchain and rebuilds it itself, exposing it only through <c>sglue_swapchain()</c> with no
    /// way to ask for a size (<c>SokolSwapchain</c>'s original doc comment). A platform that owns
    /// its own swapchain can honour the request. Sizes of zero or less must be ignored - a minimized
    /// window reports 0x0 and has no drawable.
    /// </summary>
    void Resize(int width, int height);

    /// <summary>
    /// Shows the frame that was just committed.
    ///
    /// Called after <c>sg_commit()</c>, and this ordering is load-bearing rather than stylistic:
    /// nothing in sokol_gfx presents on its own (<c>_sg_d3d11_commit</c> is an empty function,
    /// <c>sokol_gfx.h:15217</c>), and sokol_app presents *after* its frame callback returns
    /// (<c>sokol_app.h:9909-9919</c>). Committing first is also mechanically safe because
    /// <c>_sg_d3d11_end_pass</c> finishes with <c>ClearState</c>, so by the time this runs nothing
    /// has the backbuffer bound any more.
    /// </summary>
    void Present();
}
