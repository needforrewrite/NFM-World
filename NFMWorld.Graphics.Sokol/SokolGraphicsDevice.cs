using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// sokol_gfx-backed <see cref="IGraphicsDevice"/>.
///
/// Unlike the FNA3D backend - which creates its own device against a window handle and owns that
/// window's swapchain - sokol_gfx does not own a window at all. Its D3D11 backend asserts on an
/// <c>ID3D11Device</c> injected through <c>sg_desc.environment</c>, and that device is produced by
/// sokol_app. So the lifetime here is inverted: <see cref="Run"/> drives sokol_app's main loop and
/// creates the device inside its init callback, handing this instance to the caller's frame
/// callback. That mirrors the intended C usage exactly (see sokol_glue.h's <c>sglue_environment</c>),
/// and it is the only arrangement the D3D11 backend accepts.
///
/// This is why the class is not constructed by the caller the way <c>FNA3DGraphicsDevice.Create</c>
/// is: there is no valid device object until sokol_app has initialized the 3D API.
/// </summary>
public sealed unsafe class SokolGraphicsDevice : IGraphicsDevice
{
    private SokolSwapchain? _swapchain;
    private SokolCommandBuffer? _activeCommandBuffer;
    private bool _shutdown;
    private int _renderingThreadId;

    /// <summary>
    /// Resources created with initial data whose mirror has not reached the GPU yet.
    ///
    /// They are registered here rather than on the command buffer because a resource is typically
    /// created <em>during</em> recording - after <see cref="AcquireCommandBuffer"/> has already
    /// returned, and holding no reference to the buffer it will be drawn through. The next
    /// <c>FlushUploads</c> drains this into its own list, so a resource created and drawn in the
    /// same frame still uploads before its first use.
    ///
    /// Without this, a dynamic buffer created with initial data is <em>never</em> uploaded: only
    /// <see cref="SokolCommandBuffer.UpdateBuffer"/> calls <c>Track</c>, and a caller that created
    /// the buffer with its contents has nothing left to write - so the GPU copy stays at the
    /// driver's undefined contents. That is a subtle failure rather than an obvious one, because
    /// any caller that <em>does</em> rewrite the buffer every frame uploads normally; only the
    /// resources left alone after creation break. <c>RenderQueue</c> is exactly that shape: it
    /// creates an instanced batch's buffer with its first frame's contents and then skips
    /// <c>UpdateBuffer</c> for as long as the batch is unchanged, so per-frame geometry (the
    /// player's car) drew correctly while static geometry (stage parts) did not.
    /// </summary>
    private readonly List<IPendingUpload> _createdUploads = [];

    /// <summary>Records a just-created resource whose contents are still only in its mirror.</summary>
    internal void TrackCreated(IPendingUpload resource)
    {
        if (!_createdUploads.Contains(resource)) _createdUploads.Add(resource);
    }

    /// <summary>
    /// Moves the created-but-unuploaded resources into <paramref name="destination"/>, which
    /// becomes responsible for flushing them.
    /// </summary>
    internal void DrainCreatedUploads(List<IPendingUpload> destination)
    {
        if (_createdUploads.Count == 0) return;

        foreach (var resource in _createdUploads)
            if (!destination.Contains(resource)) destination.Add(resource);

        _createdUploads.Clear();
    }

    /// <summary>
    /// The live device, set once <c>sg_setup</c> has succeeded. sokol_gfx is a global singleton
    /// (every <c>sg_*</c> call is a free function), so at most one device can exist per process -
    /// this is the handle the static entry points below check against.
    /// </summary>
    private static SokolGraphicsDevice? _current;

    public ISwapchain Swapchain =>
        _swapchain ?? throw new InvalidOperationException("The device has not been created yet.");

    /// <inheritdoc />
    /// <remarks>
    /// False for D3D11, which is what this backend's vendored sokol is built against. The GL and Metal
    /// builds of sokol would answer differently, which is precisely why this is a per-device answer
    /// and not a property of "sokol".
    /// </remarks>
    public bool HasBottomLeftFramebufferOrigin => false;

    /// <summary>
    /// The drawable for the frame about to be rendered. Re-read per pass by
    /// <see cref="SokolCommandBuffer"/> rather than cached, because a resize replaces the views.
    /// </summary>
    internal sg_swapchain AcquireSwapchain() => RequireSwapchain().Acquire();

    private SokolSwapchain RequireSwapchain() =>
        _swapchain ?? throw new InvalidOperationException("The device has not been created yet.");

    /// <summary>
    /// Refuses a <c>sg_*</c> call from a thread other than the one that created the device, when
    /// the platform says its API requires that.
    ///
    /// This exists because <c>ISokolPlatform.SingleThreadedLifetime</c> records a real property of
    /// the underlying API - D3D11's immediate context is not thread-safe unless the device asked for
    /// internal locking - and sokol's own guard for it (<c>SOKOL_ASSERT</c> against a recorded
    /// thread id) is compiled out of a release build. Without this, a second thread drawing through
    /// the same context produces undefined ordering and a corrupted frame, with nothing to point at
    /// the cause. The check is one integer comparison, and only until the first successful call.
    /// </summary>
    internal void EnsureRenderingThread()
    {
        if (_renderingThreadId == 0)
        {
            _renderingThreadId = Environment.CurrentManagedThreadId;
            return;
        }

        if (_renderingThreadId == Environment.CurrentManagedThreadId) return;

        if (_swapchain?.RequiresSingleThread is true)
            throw new InvalidOperationException(
                $"sokol_gfx is being used from thread {Environment.CurrentManagedThreadId}, but this " +
                $"device was created on thread {_renderingThreadId} and its platform reports that the " +
                "underlying 3D API must be used from one thread only (D3D11's immediate context is " +
                "not thread-safe unless the device opted into internal locking). All rendering must " +
                "happen on the creating thread.");
    }

    private SokolGraphicsDevice() { }

    /// <summary>
    /// Creates a device against a caller-supplied <see cref="ISokolPlatform"/>, without sokol_app.
    ///
    /// This is the counterpart of <see cref="Run"/>, and the two differ in who owns the window.
    /// <see cref="Run"/> hands the window and the main loop to sokol_app; this one takes a platform
    /// that already has a window of its own (this application's is SDL3's, because input, ImGui and
    /// NanoVG all run on top of it) and only borrows it to build a device. sokol_gfx is never told
    /// about the window at all - its D3D11 environment is a device and a context and nothing else
    /// (<c>sokol_gfx.h:5361-5364</c>).
    ///
    /// The caller drives the frame: render through <see cref="AcquireCommandBuffer"/> and
    /// <see cref="Submit"/>, then call <c>platform.Present()</c>. That last call is what actually
    /// puts the frame on screen, and it has to come after <c>sg_commit</c> - nothing in sokol_gfx
    /// presents on its own (<c>_sg_d3d11_commit</c> is empty, <c>sokol_gfx.h:15217</c>).
    ///
    /// <paramref name="platform"/> is owned by the returned device and disposed with it, which is
    /// why it is not also returned for the caller to keep.
    /// </summary>
    public static SokolGraphicsDevice Create(ISokolPlatform platform, int width, int height, ConfigureDesc? configure = null)
        => Setup(platform.CreateEnvironment(width, height, SampleCount), platform, ownsSokol: true, configure);

    /// <summary>
    /// The one place <c>sg_setup</c> is called, shared by both entry points so the pools, the
    /// logger and the singleton bookkeeping cannot drift apart between them.
    /// </summary>
    /// <param name="ownsSokol">
    /// Whether the returned device is responsible for <c>sg_shutdown</c>. True for
    /// <see cref="Create"/>, which called <c>sg_setup</c> itself and has no other teardown path.
    /// False for <see cref="Run"/>, where sokol_app's cleanup callback shuts sokol down as the frame
    /// loop ends - and where claiming ownership would mean <c>sg_shutdown</c> running twice, which
    /// sokol answers with an assertion rather than an error (it checks <c>_sg.valid</c> first).
    /// </param>
    private static SokolGraphicsDevice Setup(
        sg_environment environment, ISokolPlatform platform, bool ownsSokol, ConfigureDesc? configure)
    {
        if (_current is not null)
            throw new InvalidOperationException(
                "A sokol device already exists. sg_setup is process-global, so only one can be live at a time.");

        var desc = new sg_desc
        {
            environment = environment,

            // The pool defaults (sokol_gfx.h:6608-6613: 128 buffers, 128 images, 64 samplers,
            // 32 shaders, 64 pipelines, 256 views) are sized for a small sample application. This
            // app is neither small nor short-lived: several render targets with their own views and
            // many textures accumulate for the whole session, and a pool that fills up fails at
            // sg_make_* rather than growing. Exhaustion is the failure mode that would be hardest to
            // attribute from a distance, so the headroom is bought up front.
            //
            // Each must stay under _SG_MAX_POOL_SIZE = 65536 (sokol_gfx.h:6605-6607), and a
            // non-positive value means "use the default" rather than "unlimited".
            buffer_pool_size = 4096,
            image_pool_size = 2048,
            sampler_pool_size = 256,
            shader_pool_size = 512,
            pipeline_pool_size = 1024,
            view_pool_size = 4096,

            // 4 MB is the default and is plenty: this backend has one merged uniform block of
            // roughly a hundred floats per draw.
            uniform_buffer_size = 4 * 1024 * 1024,

            logger = new sg_logger
            {
                func = (delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void>)
                    &SokolLog.Func,
            },
        };

        configure?.Invoke(ref desc);
        Gfx.setup(&desc);

        // This is the thread the context lives on - for GLCORE it is the one SDL made the context
        // current on - and every sg_destroy_* has to come from here. See SokolLifetime.
        SokolLifetime.OwnedByCurrentThread();

        var device = new SokolGraphicsDevice
        {
            _swapchain = new SokolSwapchain(platform),
            _ownsSokol = ownsSokol,
        };

        _current = device;
        return device;
    }

    /// <summary>
    /// Initializes sokol_gfx and runs <paramref name="frame"/> once per frame until the window
    /// closes. <paramref name="init"/> runs after the 3D API exists, so it is the place to create
    /// resources; <paramref name="shutdown"/> runs as the loop ends.
    /// </summary>
    /// <param name="configure">
    /// Optional last chance to adjust <c>sg_desc</c> before <c>sg_setup</c> - used for the one
    /// thing a caller genuinely needs to control from outside: installing an
    /// <c>sg_logger</c>. sokol reports validation failures by <em>logging</em> rather than by
    /// failing a call (<c>_SG_VALIDATE</c> only sets an error and logs; see sokol_gfx.h:7779),
    /// and this backend logs nothing by default, so without a logger a validation failure is
    /// entirely silent. <see cref="SokolGraphicsDevice.Run"/> passes the environment first, so
    /// an implementation must leave it alone.
    /// </param>
    /// <summary>
    /// Adjusts an <c>sg_desc</c> in place - a <c>ref</c>-taking delegate rather than
    /// <c>Action&lt;sg_desc&gt;</c> because the struct is passed by value, so a mutation through a
    /// plain action would be discarded.
    /// </summary>
    public delegate void ConfigureDesc(ref sg_desc desc);

    public static void Run(
        string title,
        int width,
        int height,
        Action<SokolGraphicsDevice> init,
        Action<SokolGraphicsDevice> frame,
        Action<SokolGraphicsDevice>? shutdown = null,
        int multiSampleCount = 0,
        ConfigureDesc? configure = null)
    {
        var state = new RunState(init, frame, shutdown);

        var desc = new sapp_desc
        {
            width = width,
            height = height,
            sample_count = multiSampleCount,
            window_title = Utf8Z(title),
            // sokol_app defaults depth_format to DEPTH_STENCIL, which is what the shadow-cascade
            // and NanoVG pass paths need, so it is left alone here.
            init_userdata_cb = &RunState.InitThunk,
            frame_userdata_cb = &RunState.FrameThunk,
            cleanup_userdata_cb = &RunState.CleanupThunk,
            user_data = (void*)state.Pinned(),
        };

        state.Configure = configure;
        App.run(&desc);
    }

    /// <summary>
    /// The <see cref="ISokolPlatform"/> behind sokol_app, for the <see cref="Run"/> path only.
    ///
    /// <c>Run</c> predates the platform seam and did not go through it: it read the environment and
    /// swapchain straight out of <c>sglue_*</c>. Rather than either leaving that path holding a
    /// different shape or inventing a platform that would have to be created before
    /// <c>sapp_run</c> - impossible, because sokol_app creates the window and the 3D API inside the
    /// call - the seam is adapted to it here. Every member is a one-line forward to
    /// sokol_app, and the ones that cannot work under sokol_app say so.
    /// </summary>
    private sealed class SokolAppPlatform : ISokolPlatform
    {
        /// <summary>Rebuilt per frame by <see cref="Refresh"/>, because sokol_app recreates the
        /// swapchain on resize with no way to ask for a size - see <see cref="Resize"/>.</summary>
        private int _width;
        private int _height;

        public int Width => _width;

        public int Height => _height;

        /// <summary>
        /// Always zero: sokol_app created the window and owns it, and it is never handed out -
        /// there is no <c>sapp_</c> call for it and nothing that hosts sokol_app's window externally.
        /// Callers that need a real handle use <see cref="SokolD3D11Platform"/> or
        /// <see cref="SokolGlPlatform"/>, both of which were given one.
        /// </summary>
        public IntPtr NativeHandle => IntPtr.Zero;

        /// <summary>
        /// True: sokol_app's <c>_sapp_d3d11_create_device_and_swapchain</c> passes
        /// <c>D3D11_CREATE_DEVICE_SINGLETHREADED</c> unconditionally (<c>sokol_app.h:8936</c>), so
        /// the immediate context has no internal locking and must be used from one thread.
        /// </summary>
        public bool? SingleThreadedLifetime => true;

        public void Refresh()
        {
            if (App.isvalid() == 0) return;
            _width = App.width();
            _height = App.height();
        }

        public sg_environment CreateEnvironment(int width, int height, int sampleCount)
        {
            _width = width;
            _height = height;
            return Glue.environment();
        }

        public sg_swapchain AcquireSwapchain() => Glue.swapchain();

        public void Resize(int width, int height)
        {
            // Nothing to do, and nothing that could be done: sokol_app's drawable follows the OS
            // window, and this sokol version has no sapp_set_window_size. SokolSwapchain.Resize
            // documents why the request is advisory on this path.
        }

        /// <summary>
        /// Presenting is sokol_app's, so there is no interval for this platform to set: the swapchain
        /// belongs to sokol_app and it presents after its frame callback returns.
        /// </summary>
        public bool VSync { get; set; } = true;

        public void Present()
        {
            // sokol_app presents after its frame callback returns; nothing here presents.
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Copies <paramref name="value"/> into unmanaged memory as a NUL-terminated UTF-8 string.
    /// Used for the window title, which has to outlive the <c>sapp_desc</c> it is passed in: the
    /// allocation is intentionally never freed, because sokol_app reads it for the window's whole
    /// lifetime and there is exactly one per process.
    /// </summary>
    private static sbyte* Utf8Z(string value)
    {
        var bytes = SokolNative.Utf8Z(value);
        var pointer = (sbyte*)NativeMemory.Alloc((nuint)bytes.Length);
        for (var i = 0; i < bytes.Length; i++) pointer[i] = (sbyte)bytes[i];
        return pointer;
    }

    /// <summary>
    /// Everything the native callbacks need, held as unmanaged state because sokol_app stores only
    /// a <c>void*</c>. The managed delegates are kept here so they are not collected while the
    /// native loop can still call them.
    /// </summary>
    private sealed class RunState(
        Action<SokolGraphicsDevice> init,
        Action<SokolGraphicsDevice> frame,
        Action<SokolGraphicsDevice>? shutdown)
    {
        private GCHandle _self;

        /// <summary>Set by <see cref="Run"/> after construction; see that method's parameter of the same name.</summary>
        public ConfigureDesc? Configure { get; set; }

        public IntPtr Pinned()
        {
            _self = GCHandle.Alloc(this);
            return GCHandle.ToIntPtr(_self);
        }

        private static RunState From(void* userData) =>
            (RunState)GCHandle.FromIntPtr((IntPtr)userData).Target!;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void InitThunk(void* userData)
        {
            var state = From(userData);

            // The environment has to be built from inside this callback rather than before the
            // loop: sg_setup's D3D11 backend requires an injected ID3D11Device, and until sokol_app
            // has created the window and the 3D API there is none.
            //
            // ownsSokol: false, because CleanupThunk shuts sokol down itself - see Setup's parameter.
            var platform = new SokolAppPlatform();
            var device = Setup(platform.CreateEnvironment(0, 0, 0), platform, ownsSokol: false, state.Configure);
            state.Device = device;

            state.Init(device);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void FrameThunk(void* userData)
        {
            var state = From(userData);

            // sokol_app recreates the swapchain when the window resizes, so the cached size is
            // refreshed each frame rather than only on resize events - the cheap query avoids a
            // missed resize when the OS coalesces events.
            ((SokolAppPlatform)state.Device!.SwapchainState.Platform).Refresh();

            state.Frame(state.Device);

            // The Run path's frame boundary is here, because sokol_app calls this back once per
            // frame. The Create path has no callback and its caller calls EndFrame instead.
            state.Device.EndFrame();
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void CleanupThunk(void* userData)
        {
            var state = From(userData);
            if (state.Device is { } device)
            {
                state.Shutdown?.Invoke(device);
                // The device does not own this shutdown - see Dispose - so it is issued here, and
                // SokolLifetime has to be told too or the exit finalizers below will destroy into
                // freed pools. See SokolLifetime.
                device.Dispose();
            }
            SokolLifetime.ShutDown();
            Gfx.shutdown();
            _current = null;
            if (state._self.IsAllocated) state._self.Free();
        }

        public SokolGraphicsDevice? Device { get; private set; }
        public Action<SokolGraphicsDevice> Init => init;
        public Action<SokolGraphicsDevice> Frame => frame;
        public Action<SokolGraphicsDevice>? Shutdown => shutdown;
    }

    /// <summary>
    /// The live device, or null when no frame loop is running. Exposed for the smoke test and for
    /// backends that need to reach the device from outside a callback.
    /// </summary>
    internal static SokolGraphicsDevice? Current => _current;

    /// <summary>The swapchain wrapper, reached through <see cref="Swapchain"/> but stored concretely here so the frame loop can refresh it.</summary>
    internal SokolSwapchain SwapchainState => _swapchain!;

    /// <summary>
    /// Whether this instance is responsible for <c>sg_shutdown</c>. True when it called
    /// <c>sg_setup</c> itself (<see cref="Create"/>); false under <see cref="Run"/>, where
    /// sokol_app's cleanup callback owns both the shutdown and the frame loop's exit.
    /// </summary>
    private bool _ownsSokol;

    /// <summary>
    /// Counts frames since the device was created, advanced by <see cref="EndFrame"/>.
    ///
    /// This is a resource-upload budget rather than a time source: sokol allows at most one
    /// <c>sg_update_buffer</c>/<c>sg_update_image</c> per resource per frame (<c>VALIDATE_UPDATEBUF_
    /// ONCE</c>), so the command buffer needs to know which frame it is in to collapse several
    /// updates the abstraction permits into the one sokol will accept.
    ///
    /// Under <see cref="Run"/> sokol_app owns the frame boundary and increments this in its frame
    /// callback; under <see cref="Create"/> there is no callback, so the caller's
    /// <see cref="EndFrame"/> call is the boundary instead.
    /// </summary>
    internal long FrameIndex { get; private set; }

    /// <summary>
    /// Marks the end of a frame, advancing the <see cref="FrameIndex"/> upload budget.
    ///
    /// Must be called once per frame <em>after</em> <see cref="Submit"/> - every
    /// <c>sg_update_buffer</c>/<c>sg_update_image</c> issued this frame is a use of the current
    /// frame's allowance, so advancing early would let the same resource be updated twice in what
    /// sokol considers one frame. The <see cref="Run"/> path does this itself, from sokol_app's
    /// frame callback; the <see cref="Create"/> path needs it from its caller.
    /// </summary>
    public void EndFrame() => FrameIndex++;

    /// <summary>
    /// The sample count sokol is told the swapchain uses, and the sample count
    /// <see cref="ISokolPlatform.CreateEnvironment"/> is asked for.
    ///
    /// One, always, and that is a limitation rather than a choice: a multisampled swapchain needs a
    /// separate single-sample resolve target, and sokol neither creates one nor accepts a swapchain
    /// whose <c>resolve_view</c> is absent while <c>sample_count</c> is greater than one
    /// (<c>sokol_gfx.h:24803-24805</c>). An offscreen pass can still be multisampled - only the
    /// final presentation target cannot. So <c>GraphicsSettingsShim.AppliedMultiSampleCount</c>
    /// reports 1 on this backend whatever the settings ask for, which is the same "the request was
    /// not honoured" answer it already gives the GL backend (<c>GlSwapchain.MultiSampleCount</c> is
    /// a hardcoded 0).
    /// </summary>
    private const int SampleCount = 1;

    /// <summary>
    /// Wraps one program's compiled stages for use as pipeline shaders.
    ///
    /// This is the sokol counterpart of <c>FNA3DGraphicsDevice.LoadEffectModule</c>, and the
    /// asymmetry is deliberate: FNA3D wants a whole compiled Effect blob and nothing else, while
    /// sokol wants HLSL (or DXBC) <em>per stage</em> plus an explicit description of every binding
    /// the shader declares. The binding description is derived from
    /// <paramref name="reflection"/> at pipeline-creation time (see <see cref="ShaderBindings"/>),
    /// so a caller only has to supply the code and the reflection.
    ///
    /// <paramref name="vertexSource"/>/<paramref name="pixelSource"/> are what actually get
    /// compiled on this backend, because the native library here is a D3D11-only build that cannot
    /// consume SPIR-V - see <see cref="SokolShaderProgram.VertexSource"/>.
    /// </summary>
    public static IShaderModule LoadProgram(
        ReadOnlyMemory<byte> vertexBytecode,
        ReadOnlyMemory<byte> pixelBytecode,
        ShaderReflection reflection,
        string? vertexSource = null,
        string? pixelSource = null) =>
        new SokolShaderProgram(vertexBytecode, pixelBytecode, reflection, vertexSource, pixelSource);

    /// <summary>
    /// Wraps a generated bundle's per-stage sources for use as pipeline shaders, selecting the form
    /// the running backend actually compiles.
    ///
    /// The selection has to happen here rather than in the bundle: an <c>sg_shader_function</c>
    /// holds a single <c>source</c> field, not one per backend, so a bundle that shipped every
    /// backend's output still has to be told which one to use at shader-creation time. The choice
    /// is read from <c>sg_query_backend()</c>, which is upstream sokol-shdc's own convention - its
    /// generated code emits <c>{prog}_shader_desc(sg_backend backend)</c> and callers pass
    /// <c>sg_query_backend()</c>.
    ///
    /// D3D11 (the default, and by far the common case on Windows) compiles the HLSL with
    /// <c>d3d11_target</c>; Metal compiles the MSL; GLCORE compiles the desktop GLSL; GLES3
    /// compiles the ES 3.0 GLSL; Vulkan takes the SPIR-V. The two GL branches are separate cases
    /// rather than one because the sources are different dialects, not two spellings of the same
    /// thing - see <see cref="ShaderStageSources.GlslEs"/>.
    ///
    /// <para>
    /// <b>Source selection is not the whole of what a backend needs</b>, and this is the part that
    /// has bitten: which struct fields have to be filled in below differs per backend, and sokol
    /// only reports a missing one by logging. The per-backend gaps are:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>Vulkan</b> needs <c>spirv_set0_binding_n</c> on the uniform block and
    /// <c>spirv_set1_binding_n</c> on views and samplers, and the shader's
    /// <c>entry</c> must be the real SPIR-V entry point rather than <c>"main"</c> - see
    /// <see cref="SokolResources.EntryPoint"/>.</item>
    ///
    /// <item><b>GLCORE</b> needs <c>glsl_name</c> on the uniform block's <em>members</em> and on
    /// texture-sampler <em>pairs</em>, since it resolves both by <c>glGetUniformLocation</c> rather
    /// than by binding (<c>sokol_gfx.h:11684</c>, <c>:11736</c>). Note which structs: an earlier
    /// version of this comment claimed texture views and samplers carry a <c>glsl_name</c> too, and
    /// they do not - neither struct has the field, and the GL arm of their validation loop is an
    /// explicit no-op (<c>:24398-24399</c>). A member name must also be instance-qualified
    /// (<c>_Global.LightViewProj0</c>), because spirv-cross emits the block as
    /// <c>uniform _Global _32;</c> and GLSL resolves members only by the qualified path.
    ///
    /// <para>
    /// There is a hard limit underneath this one that filling in names cannot fix:
    /// <c>SG_MAX_UNIFORMBLOCK_MEMBERS</c> is 16 (<c>:2179</c>), and this app's merged <c>_Global</c>
    /// block is larger than that - 22 members for <c>PolyBasic</c>, 34 for <c>LineBasic</c>. sokol
    /// rejects such a shader outright (<c>VALIDATE_SHADERDESC_UNIFORMBLOCK_SIZE_MISMATCH</c>,
    /// <c>:24375</c>), so GLCORE additionally requires the block to be split before any of this is
    /// reachable. Vulkan has no such cap - its arm reads only the scalar
    /// <c>spirv_set0_binding_n</c> (<c>:24341-24343</c>) and never touches the member array.
    /// </para></item>
    ///
    /// <item><b>Metal</b> needs <c>msl_buffer_n</c>/<c>msl_texture_n</c>/<c>msl_sampler_n</c>, none
    /// of which are set or defaulted (<c>:25876-25903</c> defaults only <c>layout</c>,
    /// <c>array_count</c>, <c>image_type</c>, <c>sample_type</c> and <c>sampler_type</c>), so every
    /// view lands on MSL texture 0 and a multi-texture program collides
    /// (<c>VALIDATE_SHADERDESC_VIEW_TEXTURE_METAL_TEXTURE_SLOT_COLLISION</c>, <c>:4968</c>). This one
    /// is inferred from the validation code rather than measured - no Metal run has been attempted.
    /// </item>
    /// </list>
    ///
    /// GLES3 is the case with nothing to do: <c>CompilerBuildCombinedImageSamplers</c> and the
    /// combined-sampler renaming named above are already applied to the ES source, so its
    /// <c>glsl_name</c> gap is closed at the compiler end. What is verified for ES beyond that: all
    /// eight generated programs compile and link under a real ANGLE ES 3.0 context, their std140
    /// block sizes match the emitted reflection exactly, and <c>layout(row_major)</c> is honoured so
    /// matrices are read the same way D3D reads them.
    /// </summary>
    /// <inheritdoc cref="IGraphicsDevice.CreateShaderModule"/>
    /// <remarks>
    /// Instantiated rather than static because selecting the form to compile reads
    /// <c>sg_query_backend()</c>, which is only meaningful once <c>sg_setup</c> has run - and this
    /// overload deliberately goes through the same instance gate as any other device operation.
    /// </remarks>
    IShaderModule IGraphicsDevice.CreateShaderModule(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection) =>
        LoadProgram(vertex, pixel, reflection);

    public static IShaderModule LoadProgram(
        ShaderStageSources vertex,
        ShaderStageSources pixel,
        ShaderReflection reflection)
    {
        var backend = Gfx.query_backend();
        return backend switch
        {
            sg_backend.SG_BACKEND_METAL_IOS or sg_backend.SG_BACKEND_METAL_MACOS
                or sg_backend.SG_BACKEND_METAL_SIMULATOR =>
                new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.Msl, pixel.Msl),

            // A GL backend compiles GLSL and has no use for the SPIR-V, so the bytecode is
            // dropped rather than passed alongside a source - SetShaderFunction prefers the
            // source and would ignore it anyway.
            //
            // GLCORE and GLES3 take different GLSL, and sokol is explicit that the GLES3 backend
            // wants `#version 300 es` (sokol_gfx.h:927-931). They cannot share a source field
            // because the dialects differ in ways a driver rejects rather than tolerates: an ES
            // shader needs explicit precision qualifiers, and it links varyings by name where
            // desktop GLSL links them by layout(location).
            // The GLSL here is the *original*, unsplit source. ShaderBindings rewrites the uniform
            // block to fit sokol's 16-member cap and the pipeline then compiles the rewritten text
            // instead - see SokolPipelineState/ShaderBindings.VertexSource. The split is per-pipeline
            // because it is driven by the descriptor, which is per-pipeline.
            sg_backend.SG_BACKEND_GLCORE =>
                new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.Glsl, pixel.Glsl),

            sg_backend.SG_BACKEND_GLES3 =>
                new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.GlslEs, pixel.GlslEs),

            sg_backend.SG_BACKEND_VULKAN =>
                new SokolShaderProgram(vertex.Spirv, pixel.Spirv, reflection, null, null),

            // D3D11 is the default branch rather than an explicit case so an unrecognized backend
            // fails at sg_make_shader with a clear sokol validation error rather than silently
            // binding a shader in a language that backend cannot compile. D3D11 is the only
            // backend the vendored native library is built with in any case.
            _ => new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.Hlsl, pixel.Hlsl),
        };
    }

    public ICommandBuffer AcquireCommandBuffer()
    {
        if (_activeCommandBuffer is not null)
            throw new InvalidOperationException(
                $"{nameof(AcquireCommandBuffer)} was called again before the previous command buffer was submitted - only one command buffer may be live at a time.");

        var commandBuffer = new SokolCommandBuffer(this);
        _activeCommandBuffer = commandBuffer;
        return commandBuffer;
    }

    public void Submit(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not SokolCommandBuffer sokol)
            throw new ArgumentException($"{nameof(Submit)} needs a command buffer this backend created.", nameof(commandBuffer));

        // A sokol frame is exactly one commit; the abstraction's Submit is the frame boundary, so
        // it closes any open pass and commits here (the command buffer itself never presents).
        sokol.Commit();
        _activeCommandBuffer = null;
    }

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        var buffer = new sg_buffer_desc
        {
            // size is the allocation; data (set below for immutable buffers) is the contents.
            size = (nuint)desc.SizeInBytes,
            usage = new sg_buffer_usage
            {
                vertex_buffer = SokolNative.Bool(desc.Kind == BufferKind.Vertex),
                index_buffer = SokolNative.Bool(desc.Kind == BufferKind.Index),
                // sokol's "immutable" is closer to the abstraction's Immutable than to Dynamic:
                // it lets the backend place the buffer in device-local memory with no CPU access.
                immutable = SokolNative.Bool(desc.Usage == BufferUsage.Immutable),
                // Dynamic buffers are updated through sg_update_buffer, which requires this flag.
                dynamic_update = SokolNative.Bool(desc.Usage == BufferUsage.Dynamic),
            },
        };

        // Initial data has to travel two different ways. An immutable buffer cannot be updated
        // after creation at all (sg_update_buffer rejects it), so its contents must go into the
        // descriptor; a dynamic one is the opposite - sokol creates a descriptor-supplied buffer
        // as immutable, so it must be filled by a post-creation update instead.
        fixed (byte* pointer = initialData)
        {
            if (!initialData.IsEmpty)
            {
                if (desc.Usage == BufferUsage.Immutable)
                    buffer.data = SokolNative.Range(pointer, initialData.Length);

                var handle = Gfx.make_buffer(&buffer);
                var result = new SokolBuffer(handle, desc);

                if (desc.Usage != BufferUsage.Immutable)
                {
                    // Seeded into the mirror rather than pushed with an immediate sg_update_buffer.
                    // A buffer created mid-frame is routinely written again through UpdateBuffer
                    // before that frame's draw (Sparks and Chips both create with a zeroed array
                    // and then fill it), and sokol allows one sg_update_buffer per buffer per frame
                    // (VALIDATE_UPDATEBUF_ONCE) - so an immediate upload here would spend the
                    // frame's only allowance and make the real write the second one. That both
                    // fails validation and, because validation aborts the call before the deferred
                    // mirror flush can run, would leave the buffer holding its initial zeros. The
                    // mirror path coalesces this write with any later one into the single upload
                    // FlushUploads issues at draw time.
                    result.Mirror = initialData.ToArray();
                    result.MarkDirty();
                    TrackCreated(result);
                }

                return result;
            }

            return new SokolBuffer(Gfx.make_buffer(&buffer), desc);
        }
    }

    public unsafe ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        var depth = IsDepthFormat(desc.Format);
        var mipCount = desc.MipMapped && !desc.RenderTargetable ? MipCount(desc.Width, desc.Height) : 1;

        // "Updatable" and "mipmapped" are mutually exclusive here, and the reason is D3D11's rather
        // than sokol's: sg_update_image requires usage.dynamic_update, which _sg_d3d11_image_usage
        // turns into D3D11_USAGE_DYNAMIC - and CreateTexture2D rejects USAGE_DYNAMIC whenever
        // MipLevels > 1 (checked against the driver: E_INVALIDARG). sokol also refuses descriptor
        // data on a dynamic_update image, so the two cases need genuinely different images:
        //
        //   - a CPU-writable single-level texture: dynamic_update, no descriptor data, fed entirely
        //     through sg_update_image, so UpdateTexture can run as often as the caller likes;
        //   - everything else: one immutable image carrying its whole chain in the descriptor.
        //
        // A render target's contents belong to the GPU, and a compressed format has no CPU mirror
        // to stream from, so both take the immutable path whatever their level count.
        var updatable = !desc.RenderTargetable && mipCount == 1
            && desc.Format is TextureFormat.Rgba8 or TextureFormat.Bgra8 or TextureFormat.R8 or TextureFormat.Single or TextureFormat.Rgba32f;

        // An immutable image must carry its data in the descriptor, and sokol wants a whole chain -
        // every level of a multi-level image, not just level 0. Only a format the CPU can average
        // can be given one, so a multi-level Single texture drops back to a single level here: a
        // chain sokol would reject is worse than the one level the abstraction actually asked for.
        if (mipCount > 1 && !SokolTexture.CanGenerateMips(desc.Format)) mipCount = 1;

        var chain = Array.Empty<byte[]>();
        if (!updatable && !initialData.IsEmpty)
            chain = SokolTexture.BuildMipChain(desc.Width, desc.Height, mipCount, desc.Format, initialData);

        var imageDesc = new sg_image_desc
        {
            type = sg_image_type.SG_IMAGETYPE_2D,
            width = desc.Width,
            height = desc.Height,
            num_slices = 1,
            // _sg_image_desc_defaults turns 0 into 1, so "let sokol pick the chain" is not a
            // thing that exists here - the chain length has to be given explicitly.
            num_mipmaps = mipCount,
            pixel_format = desc.Format.ToNative(),
            usage = new sg_image_usage
            {
                // Attachment capability is a usage flag in sokol, not a property of the format.
                color_attachment = SokolNative.Bool(desc.RenderTargetable && !depth),
                depth_stencil_attachment = SokolNative.Bool(desc.RenderTargetable && depth),
                // Left unset otherwise: _sg_image_usage_defaults turns "none of the three set"
                // into immutable = true, which is the one usage that accepts descriptor data.
                dynamic_update = SokolNative.Bool(updatable),
            },
        };

        // The descriptor holds each level by pointer and nothing pins the array itself, so the
        // levels are kept alive across the call one at a time.
        var handles = new GCHandle[chain.Length];
        try
        {
            for (var level = 0; level < chain.Length; level++)
            {
                handles[level] = GCHandle.Alloc(chain[level], GCHandleType.Pinned);
                var pointer = (byte*)handles[level].AddrOfPinnedObject();
                imageDesc.data.mip_levels[level] = SokolNative.Range(pointer, chain[level].Length);
            }

            var texture = new SokolTexture(Gfx.make_image(&imageDesc), desc, mipCount, updatable);
            if (texture.Handle.id == 0)
                throw new InvalidOperationException("sg_make_image failed - see the sokol log for the validation error.");

            // sokol binds views, never bare images, so a sampler-visible texture needs one from
            // the moment it exists - SetShaderResource would otherwise bind a null view. A depth
            // image is the exception: it is only ever attached, and D3D11 needs a typeless/
            // comparison SRV format to sample one, which this minimal backend does not set up.
            if (!depth) texture.View = MakeTextureView(texture.Handle, mipCount);

            if (updatable)
            {
                // Nothing may go into the descriptor, so the contents travel through the mirror and
                // a deferred upload instead. A texture created with no initial data still gets a
                // mirror, because a later sub-rectangle update needs one to preserve what it does
                // not touch.
                texture.CpuShadow = initialData.IsEmpty
                    ? new byte[desc.Width * desc.Height * texture.BytesPerPixel]
                    : initialData.ToArray();
                if (!initialData.IsEmpty)
                {
                    texture.MarkDirty();
                    TrackCreated(texture);
                }

                return texture;
            }

            // The immutable path: the descriptor already carried the data (or the chain just built
            // from it), and a mirror is kept only so ReadTexture has something to serve.
            if (texture.CanMirror && !initialData.IsEmpty) texture.CpuShadow = initialData.ToArray();
            return texture;
        }
        finally
        {
            foreach (var handle in handles)
                if (handle.IsAllocated) handle.Free();
        }
    }

    /// <summary>The full mip chain length for a given size, i.e. 1 + floor(log2(max(w, h))).</summary>
    private static int MipCount(int width, int height)
    {
        var size = Math.Max(width, height);
        var count = 1;
        while (size > 1) { size >>= 1; count++; }
        return count;
    }

    /// <summary>
    /// Whether a format is attached as a depth(-stencil) target rather than a colour one. Only
    /// <see cref="TextureFormat.Depth24Stencil8"/> is: <see cref="TextureFormat.Single"/> is
    /// R32F, a plain float colour format, even though sokol also has a float <em>depth</em> format.
    /// The two are not interchangeable, because the FX shaders sample their shadow maps with
    /// ordinary <c>tex2D</c> rather than a comparison sampler - which sokol only allows on a
    /// filterable colour format.
    /// </summary>
    private static bool IsDepthFormat(TextureFormat format) => format == TextureFormat.Depth24Stencil8;

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        var colorTexture = (SokolTexture)CreateTexture(new TextureDesc(desc.Width, desc.Height, desc.ColorFormat, RenderTargetable: true));

        SokolTexture? depthTexture = null;
        var depthView = default(sg_view);
        if (desc.HasDepthStencil)
        {
            depthTexture = (SokolTexture)CreateTexture(new TextureDesc(desc.Width, desc.Height, desc.DepthStencilFormat, RenderTargetable: true));
            var depthViewDesc = new sg_view_desc
            {
                depth_stencil_attachment = new sg_image_view_desc { image = depthTexture.Handle, mip_level = 0, slice = 0 },
            };
            depthView = Gfx.make_view(&depthViewDesc);
        }

        // The colour image needs two views: the attachment view a pass writes through, and the
        // texture view that later passes sample it through - the latter created by CreateTexture
        // above, which is what lets a rendered-to image be read as a texture.
        return new SokolRenderTarget(colorTexture, depthTexture, MakeColorAttachmentView(colorTexture.Handle), depthView);
    }

    /// <summary>
    /// The sampling view for an image. <c>count = 0</c> means "to the end of the chain" in
    /// sokol's range type, so a mipmapped texture exposes every level through one view - but the
    /// view is only valid against a pipeline that will actually sample those levels.
    /// </summary>
    private static sg_view MakeTextureView(sg_image image, int mipCount)
    {
        var desc = new sg_view_desc
        {
            texture = new sg_texture_view_desc
            {
                image = image,
                mip_levels = new sg_texture_view_range { @base = 0, count = mipCount },
                slices = new sg_texture_view_range { @base = 0, count = 1 },
            },
        };
        return Gfx.make_view(&desc);
    }

    private static sg_view MakeColorAttachmentView(sg_image image)
    {
        var desc = new sg_view_desc
        {
            color_attachment = new sg_image_view_desc { image = image, mip_level = 0, slice = 0 },
        };
        return Gfx.make_view(&desc);
    }

    public ISampler CreateSampler(SamplerDesc desc)
    {
        var samplerDesc = new sg_sampler_desc
        {
            min_filter = desc.Filter.ToNative(),
            mag_filter = desc.Filter.ToNative(),
            mipmap_filter = desc.Filter.ToNative(),
            wrap_u = desc.AddressU.ToNative(),
            wrap_v = desc.AddressV.ToNative(),
            wrap_w = sg_wrap.SG_WRAP_REPEAT,
            // max_lod's default is FLT_MAX via _sg_def_flt, so an explicit 0 would be replaced by
            // "unlimited" anyway - stating it is the honest way to get the whole chain.
            min_lod = 0f,
            max_lod = float.MaxValue,
            border_color = sg_border_color.SG_BORDERCOLOR_TRANSPARENT_BLACK,
            // sokol treats a non-NEVER compare as "this is a comparison sampler", and the shader
            // desc here declares every sampler as SG_SAMPLERTYPE_FILTERING - so ALWAYS (the nearest
            // thing sokol has to XNA's "no comparison") would fail VALIDATE_ABND_EXPECTED_SAMPLER_
            // COMPARE_NEVER at every draw that binds it. NEVER is what a filtering sampler must
            // carry; the shadow map's comparison state would need a separate sampler type.
            compare = sg_compare_func.SG_COMPAREFUNC_NEVER,
            max_anisotropy = 1,
        };
        return new SokolSampler(Gfx.make_sampler(&samplerDesc), desc);
    }

    public IPipelineState CreatePipeline(PipelineDesc desc)
    {
        var vertex = RequireProgram(desc.VertexShader, "VertexShader");
        var pixel = RequireProgram(desc.PixelShader, "PixelShader");

        if (!ReferenceEquals(vertex.Reflection, pixel.Reflection) && vertex.Reflection != pixel.Reflection)
            throw new ArgumentException(
                "Both shader stages must come from the same LoadProgram call: sokol takes one shader object holding " +
                "both stages, and the reflection is a property of the program rather than of a stage.");

        var reflection = vertex.Reflection;

        // The binding tables are needed twice: once in the shader desc (what the shader declares)
        // and once here (how the abstraction's flat slots map onto it).
        // On GLCORE the program's source *is* the GLSL, so this is what the split rewrites; on
        // every other backend it is HLSL or MSL and the pair is ignored.
        var layout = new ShaderBindings(reflection, vertex.VertexSource, pixel.PixelSource);
        var shader = CreateShader(vertex, pixel, layout, desc);

        // A pipeline must declare the pixel format of the attachment it will draw into, and the
        // abstraction has no such field - so the state object holds one native pipeline per format
        // and makes them on demand. See SokolPipelineState._variants.
        return new SokolPipelineState(
            shader, desc, reflection,
            layout.UniformBlockSize, layout.UniformBlocks, layout.ViewSlotMap, layout.SamplerSlotMap,
            layout.ViewCount, layout.SamplerCount,
            (colorFormat, indexType) => MakePipelineVariant(desc, shader, layout, colorFormat, indexType),
            DefaultColorFormat);
    }

    /// <summary>
    /// The colour format an unqualified pipeline declares, i.e. what a pass drawing to the
    /// swapchain uses. Read from sokol's resolved environment rather than hardcoded, because the
    /// value is backend-dependent (<c>_sg_desc_defaults</c>, <c>sokol_gfx.h:26374</c>: BGRA8 on
    /// D3D11 and Metal, RGBA8 elsewhere) and this app fills it in per platform.
    /// </summary>
    internal sg_pixel_format DefaultColorFormat => Gfx.query_desc().environment.defaults.color_format;

    private static sg_pipeline MakePipelineVariant(
        PipelineDesc desc, sg_shader shader, ShaderBindings layout,
        sg_pixel_format colorFormat, sg_index_type indexType)
    {
        var pipelineDesc = BuildPipelineDesc(desc, shader, layout, colorFormat, indexType);
        return Gfx.make_pipeline(&pipelineDesc);
    }

    private static SokolShaderProgram RequireProgram(IShaderModule module, string name) =>
        module as SokolShaderProgram
        ?? throw new ArgumentException($"{name} must be a shader module this backend created.", nameof(module));

    private static unsafe sg_shader CreateShader(SokolShaderProgram vertex, SokolShaderProgram pixel, ShaderBindings layout, PipelineDesc desc)
    {
        var strings = new Utf8Strings();
        try
        {
            var shaderDesc = new sg_shader_desc();

            // The layout's source, not the program's: on GL the block was split, and the descriptor
            // below names its members from the split - so the two have to be the same text.
            SetShaderFunction(&shaderDesc.vertex_func, layout.VertexSource ?? vertex.VertexSource, vertex.VertexBytecode, vertex.EntryPoint, strings, "vs_5_0", "VertexShader");
            SetShaderFunction(&shaderDesc.fragment_func, layout.PixelSource ?? pixel.PixelSource, pixel.PixelBytecode, pixel.EntryPoint, strings, "ps_5_0", "PixelShader");

            // Attributes are matched to shader inputs *by index* (sokol_gfx.h:14444 builds the
            // D3D11 input layout from layout.attrs[i]'s format/slot/offset, but takes the semantic
            // from shd->d3d11.attrs[i]), so this table and BuildPipelineDesc's must be built in the
            // same order - which is why both iterate desc.VertexLayouts the same way.
            var attributeIndex = 0;
            for (var slot = 0; slot < desc.VertexLayouts.Count && slot < 8; slot++)
            {
                foreach (var attribute in desc.VertexLayouts[slot].Attributes)
                {
                    if (attributeIndex >= 16) break;
                    // The semantic index is the attribute's Slot (FNA3D's UsageIndex), not a digit
                    // suffix on the name - see SokolMapping.SemanticName.
                    shaderDesc.attrs[attributeIndex].base_type = sg_shader_attr_base_type.SG_SHADERATTRBASETYPE_FLOAT;
                    shaderDesc.attrs[attributeIndex].hlsl_sem_name = strings.Pin(SokolMapping.SemanticName(attribute.Semantic));
                    shaderDesc.attrs[attributeIndex].hlsl_sem_index = (byte)attribute.Slot;
                    attributeIndex++;
                }
            }

            for (var i = 0; i < layout.UniformBlocks.Count; i++)
            {
                var block = layout.UniformBlocks[i];
                shaderDesc.uniform_blocks[i].stage = block.Stage;
                shaderDesc.uniform_blocks[i].size = (uint)block.Size;
                shaderDesc.uniform_blocks[i].hlsl_register_b_n = block.Register;

                // The member table is what the GL backend resolves each uniform by name through,
                // and it is the only backend that reads it - so a block that carries no members is
                // one being described for D3D11, Metal or Vulkan, which bind the whole block by
                // register and never look here (sokol_gfx.h:24341-24343, :11684).
                //
                // GL must declare its layout as std140, and must be told so explicitly: the field
                // defaults to SG_UNIFORMLAYOUT_NATIVE (vanilla `_sg_def` leaves it at enum 0,
                // _SG_UNIFORMLAYOUT_DEFAULT, which resolves to NATIVE), under which sokol's alignment
                // rule is 1 byte for every member - so it would compute tightly packed offsets where
                // our GLSL actually uses std140's. The names are instance-qualified because GLSL
                // resolves a struct member only by that path: the bare member name gives
                // glGetUniformLocation a -1, which sokol turns into a warning and then skips
                // (:11699-11702) - so the failure would be misplaced uniforms, not an error.
                if (block.Members is { } members)
                {
                    shaderDesc.uniform_blocks[i].layout = sg_uniform_layout.SG_UNIFORMLAYOUT_STD140;

                    for (var m = 0; m < members.Count; m++)
                    {
                        shaderDesc.uniform_blocks[i].glsl_uniforms[m].type = members[m].Type;
                        shaderDesc.uniform_blocks[i].glsl_uniforms[m].array_count = 1;
                        shaderDesc.uniform_blocks[i].glsl_uniforms[m].glsl_name = strings.Pin(members[m].QualifiedName);
                    }
                }
            }

            for (var i = 0; i < layout.ViewCount; i++)
            {
                shaderDesc.views[i].texture.stage = layout.ViewStages[i];
                shaderDesc.views[i].texture.image_type = sg_image_type.SG_IMAGETYPE_2D;
                // Every texture these shaders sample is an ordinary filterable float/UNORM one -
                // the shadow maps included, which is why this backend keeps Single as R32F rather
                // than as a depth format (see IsDepthFormat).
                shaderDesc.views[i].texture.sample_type = sg_image_sample_type.SG_IMAGESAMPLETYPE_FLOAT;
                shaderDesc.views[i].texture.hlsl_register_t_n = layout.ViewRegisters[i];
            }

            for (var i = 0; i < layout.SamplerCount; i++)
            {
                shaderDesc.samplers[i].stage = layout.SamplerStages[i];
                shaderDesc.samplers[i].sampler_type = sg_sampler_type.SG_SAMPLERTYPE_FILTERING;
                shaderDesc.samplers[i].hlsl_register_s_n = layout.SamplerRegisters[i];
            }

            for (var i = 0; i < layout.PairCount; i++)
            {
                shaderDesc.texture_sampler_pairs[i].stage = layout.PairStages[i];
                shaderDesc.texture_sampler_pairs[i].view_slot = (byte)layout.PairViewSlots[i];
                shaderDesc.texture_sampler_pairs[i].sampler_slot = (byte)layout.PairSamplerSlots[i];
                // Required on GL, where the pair is resolved by name, and ignored elsewhere - see
                // ShaderBindings, and NameCombinedSamplers for how the GLSL came to use this name.
                shaderDesc.texture_sampler_pairs[i].glsl_name = strings.Pin(layout.PairGlslNames[i]);
            }

            // sg_shader_desc holds the bytecode by pointer, so a program built from bytecode
            // rather than source has to keep both spans pinned across the call. Source-built
            // programs (the path this backend normally takes - see SetShaderFunction) pin nothing.
            var vertexBytecode = vertex.VertexBytecode.Span;
            var pixelBytecode = pixel.PixelBytecode.Span;
            fixed (byte* vertexPointer = vertexBytecode)
            fixed (byte* pixelPointer = pixelBytecode)
            {
                if (!vertexBytecode.IsEmpty)
                    shaderDesc.vertex_func.bytecode = SokolNative.Range(vertexPointer, vertexBytecode.Length);
                if (!pixelBytecode.IsEmpty)
                    shaderDesc.fragment_func.bytecode = SokolNative.Range(pixelPointer, pixelBytecode.Length);

                var handle = Gfx.make_shader(&shaderDesc);
                if (handle.id == 0)
                    throw new InvalidOperationException("sg_make_shader failed - see the sokol log for the validation error.");
                return handle;
            }
        }
        finally
        {
            strings.Free();
        }
    }

    /// <summary>
    /// Fills one stage's <c>sg_shader_function</c>, preferring HLSL source over bytecode.
    ///
    /// Source is preferred because the native library in this repository is a D3D11-only build:
    /// it has no GL entry points, and its D3D11 shader path does not translate bytecode - it
    /// hands <c>bytecode.ptr</c> straight to <c>CreateVertexShader</c> as a DXBC blob
    /// (sokol_gfx.h:14318), or calls D3DCompile on <c>source</c> if that is set instead. The
    /// shader compiler's bundles carry SPIR-V, which is neither, so a bundle must supply the HLSL
    /// that spirv-cross emits alongside it for this backend to have anything to compile.
    /// </summary>
    private static void SetShaderFunction(
        sg_shader_function* function,
        string? source,
        ReadOnlyMemory<byte> bytecode,
        string entryPoint,
        Utf8Strings strings,
        string target,
        string which)
    {
        if (source is { Length: > 0 })
        {
            function->source = strings.Pin(source);
            function->entry = strings.Pin(entryPoint);
            function->d3d11_target = strings.Pin(target);
            return;
        }

        if (bytecode.IsEmpty)
            throw new InvalidOperationException(
                $"{which} has neither HLSL source nor bytecode. sokol's D3D11 backend accepts HLSL source " +
                "(compiled at make-shader time) or a DXBC blob - SPIR-V, which the shader compiler's bundles " +
                "currently carry, is neither. Pass the HLSL spirv-cross emits as the program's source.");

        function->entry = strings.Pin(entryPoint);
        function->d3d11_target = strings.Pin(target);
        // A fixed pointer is only valid until the enclosing statement, so the bytecode case is
        // handled by the caller's own pinning - see CreateShader.
    }

    private static sg_pipeline_desc BuildPipelineDesc(
        PipelineDesc desc, sg_shader shader, ShaderBindings layout,
        sg_pixel_format colorFormat, sg_index_type indexType)
    {
        var pipelineDesc = new sg_pipeline_desc
        {
            shader = shader,
            primitive_type = desc.Topology.ToNative(),
            // sokol bakes the index format into the pipeline while the abstraction supplies it per
            // buffer at draw time, so this is not a property of <paramref name="desc"/> at all -
            // the caller derives it from the buffer actually bound. NONE for a non-indexed draw.
            index_type = indexType,
            cull_mode = desc.RasterizerState.CullMode.ToNative(),
            // XNA/FNA's winding convention is clockwise, which matches sokol's default - stated
            // rather than assumed so a future default change cannot silently flip every triangle.
            face_winding = sg_face_winding.SG_FACEWINDING_CW,
            // sokol requires the pipeline's sample count to match the pass it draws into. It is always
            // SampleCount here: under Run because sokol_app's frame loop was asked for that count
            // (Run passes multiSampleCount to sapp_desc), and under Create because the platform was
            // told the same. Reading App.sample_count() would be wrong on the Create path anyway -
            // sokol_app was never initialized there, so it reports nothing.
            sample_count = SampleCount,
        };

        // Vertex layout: one buffer per abstraction slot, with its attributes. sokol indexes
        // attributes globally across all buffers via attr.buffer_index, and pairs them with the
        // shader's attribute table by index - see CreateShader.
        var attributeIndex = 0;
        for (var slot = 0; slot < desc.VertexLayouts.Count && slot < 8; slot++)
        {
            var layoutDesc = desc.VertexLayouts[slot];
            pipelineDesc.layout.buffers[slot].stride = layoutDesc.StrideInBytes;
            pipelineDesc.layout.buffers[slot].step_rate = layoutDesc.InstanceStepRate;
            pipelineDesc.layout.buffers[slot].step_func = layoutDesc.InstanceStepRate > 0
                ? sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE
                : sg_vertex_step.SG_VERTEXSTEP_PER_VERTEX;

            foreach (var attribute in layoutDesc.Attributes)
            {
                if (attributeIndex >= 16) break;
                pipelineDesc.layout.attrs[attributeIndex].buffer_index = slot;
                pipelineDesc.layout.attrs[attributeIndex].offset = attribute.OffsetInBytes;
                pipelineDesc.layout.attrs[attributeIndex].format = attribute.Format.ToNative();
                attributeIndex++;
            }
        }

        ApplyBlendState(&pipelineDesc, desc, colorFormat);
        ApplyDepthStencilState(&pipelineDesc, desc);
        _ = layout;
        return pipelineDesc;
    }

    private static void ApplyBlendState(
        sg_pipeline_desc* pipelineDesc, PipelineDesc desc, sg_pixel_format colorFormat)
    {
        pipelineDesc->color_count = 1;
        // The colour attachment's format, which sg_apply_pipeline validates against the open pass.
        // D3D11 ignores it when building the blend state, but sokol's validation layer does not -
        // see SokolPipelineState._variants.
        pipelineDesc->colors[0].pixel_format = colorFormat;
        pipelineDesc->colors[0].write_mask = desc.BlendState.ColorWriteMask.ToNative();
        pipelineDesc->colors[0].blend.enabled = SokolNative.Bool(desc.BlendState.Enabled);
        pipelineDesc->colors[0].blend.src_factor_rgb = desc.BlendState.SourceColor.ToNative();
        pipelineDesc->colors[0].blend.dst_factor_rgb = desc.BlendState.DestinationColor.ToNative();
        pipelineDesc->colors[0].blend.op_rgb = desc.BlendState.ColorOperation.ToNative();
        pipelineDesc->colors[0].blend.src_factor_alpha = desc.BlendState.SourceAlpha.ToNative();
        pipelineDesc->colors[0].blend.dst_factor_alpha = desc.BlendState.DestinationAlpha.ToNative();
        pipelineDesc->colors[0].blend.op_alpha = desc.BlendState.AlphaOperation.ToNative();
    }

    private static void ApplyDepthStencilState(sg_pipeline_desc* pipelineDesc, PipelineDesc desc)
    {
        var depth = desc.DepthStencilState;
        pipelineDesc->depth.compare = depth.DepthCompare.ToNative();
        pipelineDesc->depth.write_enabled = SokolNative.Bool(depth.DepthWriteEnabled);
        // sokol has no single "depth test on/off" flag - it is expressed through the compare
        // function, with ALWAYS being the pass-everything case that XNA's DepthTestEnabled=false
        // means (the read/write flags then decide whether depth is still recorded).
        if (!depth.DepthTestEnabled) pipelineDesc->depth.compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS;

        pipelineDesc->stencil.enabled = SokolNative.Bool(depth.StencilTestEnabled);
        if (!depth.StencilTestEnabled) return;

        pipelineDesc->stencil.read_mask = (byte)depth.StencilReadMask;
        pipelineDesc->stencil.write_mask = (byte)depth.StencilWriteMask;
        pipelineDesc->stencil.@ref = (byte)depth.ReferenceStencil;
        pipelineDesc->stencil.front.compare = depth.StencilFunction.ToNative();
        pipelineDesc->stencil.front.fail_op = depth.StencilFail.ToNative();
        pipelineDesc->stencil.front.depth_fail_op = depth.StencilDepthFail.ToNative();
        pipelineDesc->stencil.front.pass_op = depth.StencilPass.ToNative();

        // TwoSidedStencil:false means "the back face follows the front face", not "the back face
        // takes the Ccw* fields" - and the Ccw* fields are usually left at their struct defaults
        // (ALWAYS/KEEP), so getting this wrong silently disables the stencil on every back face.
        // That matters because NanoVG's pipelines rasterize with CullMode.None, so back faces are
        // real geometry here: its "fill2" pass tests stencil == 0 and its "fill3" pass tests
        // stencil != 0 while zeroing it, and both leave CcwStencilFunction at ALWAYS. A back face
        // under a defaulted ALWAYS/KEEP rule passes unconditionally and writes nothing, which is
        // exactly the wrong answer for both - the fringe overwrites where it must not, and the
        // cleanup quad never clears, so stencil state survives into the next frame's shapes.
        //
        // GL states the same rule from the other side: with no two-sided toggle of its own it
        // explicitly copies the front configuration onto GL_BACK when the flag is false
        // (GlPipelineState.ApplyDepthStencil). FNA3D passes the flag through as
        // twoSidedStencilMode. sokol has no mirroring to lean on, so the copy is made here.
        var backFunction = depth.TwoSidedStencil ? depth.CcwStencilFunction : depth.StencilFunction;
        var backFail = depth.TwoSidedStencil ? depth.CcwStencilFail : depth.StencilFail;
        var backDepthFail = depth.TwoSidedStencil ? depth.CcwStencilDepthFail : depth.StencilDepthFail;
        var backPass = depth.TwoSidedStencil ? depth.CcwStencilPass : depth.StencilPass;

        pipelineDesc->stencil.back.compare = backFunction.ToNative();
        pipelineDesc->stencil.back.fail_op = backFail.ToNative();
        pipelineDesc->stencil.back.depth_fail_op = backDepthFail.ToNative();
        pipelineDesc->stencil.back.pass_op = backPass.ToNative();
    }

    /// <summary>
    /// Reads a texture's pixels back into <paramref name="destination"/>.
    ///
    /// sokol_gfx provides no readback of any kind - there is no <c>sg_read_texture</c>, and the
    /// D3D11 backend never issues a staging-buffer copy - so this can only be served from the
    /// CPU-side mirror the backend keeps for otherwise-unreadable textures. That makes it exact
    /// for textures this backend uploaded and updated itself, and impossible for one the GPU
    /// rendered into.
    ///
    /// <b>Genuine GPU readback is deliberately not implemented yet.</b> It is per-platform work
    /// behind the same seam everything else on this backend uses, and the shape is known - see
    /// <see cref="TryReadTexture"/>'s remarks for what each platform would have to do. Until then
    /// this fails with that message rather than returning stale or zeroed pixels.
    /// </summary>
    public void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
    {
        if (texture is not SokolTexture sokol)
            throw new ArgumentException($"{nameof(ReadTexture)} needs a texture this backend created.", nameof(texture));

        if (level != 0)
            throw new NotSupportedException(
                "This backend's CPU mirror only tracks mip 0, and GPU readback (which would serve the " +
                "other levels) is not implemented yet.");

        if (TryReadTexture(sokol, x, y, width, height, destination, out var error)) return;
        throw new NotSupportedException(error);
    }

    /// <summary>
    /// Mirror-backed readback. Returns false (with a reason) rather than throwing, so callers can
    /// tell "this texture has no mirror" apart from a genuine failure.
    /// </summary>
    /// <remarks>
    /// To serve a texture the GPU wrote - a render target, a depth buffer - each platform would do
    /// the same thing in its own API, and the three facts that make it non-obvious are worth writing
    /// down before anyone rediscovers them:
    ///
    /// <list type="bullet">
    ///   <item>
    ///     The resource is reached through sokol, not around it: <c>sg_d3d11_query_image_info</c>
    ///     returns the raw <c>ID3D11Resource</c>/<c>ID3D11Texture2D</c> (<c>Gfx.cs:3707</c>), and
    ///     <c>sg_d3d11_view_info</c> returns the <c>srv</c>/<c>uav</c>/<c>rtv</c>/<c>dsv</c> of a view
    ///     (<c>Gfx.cs:3117-3129</c>). Render targets need the <em>view</em> path, because sokol holds
    ///     a target as an <c>sg_view</c> rather than an <c>sg_image</c>
    ///     (<c>SokolResources.cs:320-330</c>).
    ///   </item>
    ///   <item>
    ///     Copying is format-sensitive. <c>CopyResource</c> requires matching formats, and sokol
    ///     creates every one of its own textures <em>typeless</em> (<c>sokol_gfx.h:13576</c> RGBA8 to
    ///     <c>R8G8B8A8_TYPELESS</c>, <c>:13556</c> depth to <c>R32_TYPELESS</c>), so a staging
    ///     texture has to mirror the typeless format rather than the logical one. This is the trap a
    ///     naive implementation hits.
    ///   </item>
    ///   <item>
    ///     The technique is a <c>USAGE_STAGING</c>/<c>CPU_ACCESS_READ</c> texture, <c>CopyResource</c>
    ///     into it, then <c>Map</c> polled with <c>DO_NOT_WAIT</c> so a frame does not block on the
    ///     GPU; and the row layout is only linear when <c>Mapped.RowPitch == width * bytesPerPixel</c>,
    ///     so the copy has to walk rows otherwise. The async request/is-ready/wait/copy/destroy shape
    ///     is the one worth adopting. <c>squk/sokol_utils</c>' <c>sokol_gfx_utils.h</c> is the
    ///     reference for the technique, but its code cannot be reused: it is RGBA8-only, level-0-only,
    ///     has no sub-rectangle, explicitly excludes render targets, and reads sokol's private
    ///     internals (<c>_sg_lookup_image</c>, <c>_sg.d3d11</c>), which this binding does not expose.
    ///   </item>
    /// </list>
    ///
    /// The other platforms follow the same shape in their own vocabulary: Metal needs an
    /// <c>MTLBuffer</c> and a blit encoder, Vulkan a staging buffer plus a barrier, and GL a pixel
    /// buffer object with a fence sync.
    /// </remarks>
    internal static bool TryReadTexture(SokolTexture texture, int x, int y, int width, int height, Span<byte> destination, out string error)
    {
        if (texture.CpuShadow is not { } shadow)
        {
            error =
                "sokol_gfx has no texture readback, so this backend can only serve a texture it " +
                "uploaded or updated itself. A render target or depth texture has no CPU mirror, and " +
                "GPU readback (D3D11 staging copy / Metal blit / Vulkan staging buffer / GL PBO) is " +
                "not implemented yet.";
            return false;
        }

        var bytesPerPixel = texture.BytesPerPixel;
        if (bytesPerPixel == 0)
        {
            error = $"{texture.Format} textures have no readable CPU mirror.";
            return false;
        }

        var rowBytes = width * bytesPerPixel;
        if (destination.Length < rowBytes * height)
        {
            error = $"Destination holds {destination.Length} bytes; {rowBytes * height} are required for {width}x{height}.";
            return false;
        }

        for (var row = 0; row < height; row++)
        {
            var source = ((y + row) * texture.Width + x) * bytesPerPixel;
            shadow.AsSpan(source, rowBytes).CopyTo(destination[(row * rowBytes)..]);
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Tears down sokol_gfx and the platform, in that order: <c>sg_shutdown</c> releases every
    /// resource sokol owns, all of which refer to the platform's device, so the device must outlive
    /// it. <see cref="Run"/> does the same thing from sokol_app's cleanup callback.
    /// </summary>
    /// <summary>
    /// The view and sampler bound in place of any texture slot a pipeline declares but the caller
    /// never filled - a 1x1 opaque white texture with nearest filtering.
    ///
    /// This exists because the abstraction permits a declared texture to go unbound, and GL agrees:
    /// a sampler uniform left at its default reads texture unit 0, so an unbound slot samples
    /// whatever happens to be there. sokol's validation does not permit it - every view and sampler
    /// slot its shader desc declares must be filled or <c>sg_apply_bindings</c> fails
    /// (<c>VALIDATE_ABND_EXPECTED_VIEW_BINDING</c>/<c>_SAMPLER_BINDING</c>) - and a failed
    /// <c>sg_apply_bindings</c> <em>skips the bind entirely</em>, so the draw silently proceeds
    /// against the previous draw's bindings rather than merely failing one slot.
    ///
    /// Leaving the slots unbound is not a rare accident, which is why a placeholder is the right
    /// answer rather than an assertion. sokol declares a program's <em>whole</em> reflection, and
    /// several of this app's programs legitimately never fill every slot in a given pass:
    ///
    ///   - <c>PolyShadowModule</c> is one program carrying both Poly.fx techniques, so its
    ///     reflection lists Mad.fxh's ShadowMap0/1/2 (t0/t1/t2) for the Basic technique - while the
    ///     CreateShadowMap technique it is actually drawn with samples none of them;
    ///   - Nvg.fx's gradient pipelines sample <c>g_texture</c> only when the fill has an image, and
    ///     its Simple technique never samples it at all.
    ///
    /// Nearest filtering and opaque white make the substitution as close to invisible as a
    /// placeholder can be: a shader that multiplies by the sample is unaffected, and one that
    /// replaces its colour with it reads white rather than the undefined contents GL would give.
    /// Checked against the three cases above: <c>Simple</c> never samples, the gradient technique's
    /// <c>lerp(innerCol, outerCol, d)</c> never samples, and the one technique that does sample is
    /// only ever reached with a real texture - <c>AbstractionNvgRenderer</c> picks
    /// <c>_pImageFillList</c> iff <c>UniformInfo.Image != null</c> (<c>:443</c>).
    ///
    /// It cannot tell "declared but unused" from "used but wrongly left unbound", because sokol's
    /// reflection does not record which slots a technique reads - so the second case, if it ever
    /// occurs, samples white instead of failing. That is the deliberate trade: the failure it
    /// replaces is a <em>whole-frame</em> one (<c>sg_apply_bindings</c> skipping the bind leaves the
    /// draw against the previous draw's textures), and white is at least attributable to a shader.
    ///
    /// Created on first use rather than in the constructor because it must not be made before
    /// <c>sg_setup</c>, and disposed after every pipeline and pass has stopped referencing it.
    /// </summary>
    internal sg_view PlaceholderView
    {
        get
        {
            if (_placeholderView is not { } view) _placeholderView = view = MakePlaceholder().View;
            return view;
        }
    }

    /// <summary>The sampler half of <see cref="PlaceholderView"/>.</summary>
    internal sg_sampler PlaceholderSampler => (_placeholderSampler ??= MakePlaceholderSampler()).Handle;

    private SokolTexture? _placeholderTexture;
    private sg_view? _placeholderView;
    private SokolSampler? _placeholderSampler;

    private SokolTexture MakePlaceholder()
    {
        // Rgba8 and render-targetable-free: an ordinary sampled texture, which is what every
        // declared texture slot is (a depth image is never bound for sampling by this backend).
        var texture = (SokolTexture)CreateTexture(
            new TextureDesc(1, 1, TextureFormat.Rgba8), [255, 255, 255, 255]);
        _placeholderTexture = texture;
        return texture;
    }

    private SokolSampler MakePlaceholderSampler()
    {
        var sampler = (SokolSampler)CreateSampler(new SamplerDesc(
            Filter: TextureFilter.Point, AddressU: TextureAddressMode.Clamp, AddressV: TextureAddressMode.Clamp));
        _placeholderSampler = sampler;
        return sampler;
    }

    public void Dispose()
    {
        if (_shutdown) return;
        _shutdown = true;

        // sg_shutdown frees the resource pools, so anything destroyed afterwards walks freed memory.
        // The flag goes down first, whatever else happens below: a destroy that lands between this
        // point and the actual shutdown would be reading a pool that is about to disappear.
        SokolLifetime.ShutDown();

        // The placeholder is not referenced by any pipeline, so it is safe to release here - and it
        // has to go before sg_shutdown. Its Dispose is now gated on the lifetime flag rather than
        // called around it, so the ordering here is for clarity, not correctness.
        var sampler = _placeholderSampler;
        var texture = _placeholderTexture;
        _placeholderSampler = null;
        _placeholderView = null;
        _placeholderTexture = null;

        if (_ownsSokol)
        {
            // Directly-created device: this call owns the shutdown. The placeholders are released
            // just before it, as sg_make_* resources its pools still hold.
            SokolLifetime.Resume();
            sampler?.Dispose();
            // _placeholderTexture's View is owned by the texture, so disposing it releases both.
            texture?.Dispose();
            // And down again before the pools go.
            SokolLifetime.ShutDown();
            Gfx.shutdown();
            _ownsSokol = false;
        }
        else
        {
            // A device created by Run: sokol_app's cleanup callback issues the shutdown, after this
            // returns, so the placeholders can be released here - they belong to a pool that is
            // still live. Gating them on the lifetime flag would leak them.
            SokolLifetime.Resume();
            sampler?.Dispose();
            texture?.Dispose();
            SokolLifetime.ShutDown();
        }

        _swapchain?.Dispose();
        _swapchain = null;
        _current = null;
    }
}

/// <summary>
/// UTF-8 strings living in unmanaged memory for the duration of one <c>sg_make_*</c> call.
///
/// sokol copies the strings it is given during the call, so they only need to be stable until it
/// returns - but they must be genuinely pinned. A <c>fixed</c> block cannot be used for the fields
/// of a descriptor (the addresses are written into the struct and read later, inside the native
/// call), so the memory is allocated outside the GC heap instead.
/// </summary>
internal sealed unsafe class Utf8Strings
{
    private readonly List<nint> _allocations = [];

    public sbyte* Pin(string value)
    {
        var bytes = SokolNative.Utf8Z(value);
        var pointer = (sbyte*)NativeMemory.Alloc((nuint)bytes.Length);
        for (var i = 0; i < bytes.Length; i++) pointer[i] = (sbyte)bytes[i];
        _allocations.Add((nint)pointer);
        return pointer;
    }

    public void Free()
    {
        foreach (var allocation in _allocations) NativeMemory.Free((void*)(nint)allocation);
        _allocations.Clear();
    }
}
