extern alias SDL3New;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Maxine.Extensions.Mathematics;
using Microsoft.Extensions.Logging;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Graphics;
using NFMWorld.Graphics.Sokol;
#if ANGLE
using NFMWorld.Graphics.OpenGL;
#endif
// Aliased because both GL backends declare a `GlGraphicsDevice` in their own namespace, and this
// class names one type from the DesktopGL one. The ANGLE type keeps its bare name so the code
// already written against it does not move.
using GlGraphicsDeviceDesktop = NFMWorld.Graphics.DesktopGL.GlGraphicsDevice;
using NFMWorld.Platform.SDL3;
using NFMWorld.UI;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using Keys = NFMWorld.DriverInterface.Keys;
using Logging = NFMWorldLibrary.Logging;
using NFMWorld.Sentry;
using SDL3New::SDL3;
using WorldXaml.UI.Yoga;
using ClearOptions = NFMWorld.Graphics.ClearOptions;

namespace NFMWorld;

/// <summary>
/// This sample demonstrates how to load a Direct2D1 bitmap from a file.
/// This method will be part of a future version of SharpDX API.
/// </summary>
/// <remarks>
/// Milestone 5 Stage A: window/input/device ownership moved off FNA's <c>Game</c>/
/// <c>GraphicsDeviceManager</c> onto <see cref="SdlWindow"/> and a device from the graphics
/// abstraction, following the pattern proven in <c>NFMWorld.Graphics.FNA3D.Smoke/Program.cs</c>.
///
/// <para>
/// The device is sokol_gfx on D3D11. SDL still owns the window, and sokol_gfx is deliberately never
/// told about it - its D3D11 backend wants an <c>ID3D11Device</c> and an <c>ID3D11DeviceContext</c>
/// and nothing else (<c>sokol_gfx.h:5361-5364</c>), which
/// <see cref="SokolD3D11Platform"/> builds over SDL's <c>HWND</c>. That is why input, ImGui's
/// backend and NanoVG are all untouched by the change.
/// </para>
///
/// <para>
/// Which device it is comes from <c>--backend=</c> (<see cref="Renderer"/>), and within sokol from
/// <c>--sokol-backend=</c> as well. Both are runtime choices so the same binary can be run through
/// each backend in turn, which is what the comparison this harness exists for needs: numbers from
/// different builds would not be comparable. See the constructor for the switch.
/// </para>
///
/// <para>
/// The exception is the <c>ANGLE</c> renderer, which stays behind its define (see
/// NFMWorld.csproj) because it is the one path that needs a native ANGLE package. It can be named
/// on the command line only when the define is on; <see cref="ParseRenderer"/> says so by name
/// rather than reporting it as a typo.
/// </para>
/// </remarks>
public class WorldGame : IDisposable
{
    public static readonly UnlimitedArray<IRenderTarget?> ShadowRenderTargets = [];

    /// <summary>Stage A compatibility shim for <c>Mad/UI/SettingsMenu.cs</c>'s existing settings-application logic. See <see cref="GraphicsSettingsShim"/>.</summary>
    public readonly GraphicsSettingsShim Graphics;

    /// <summary>The SDL3 window this game owns.</summary>
    public readonly SdlWindow Window;

    /// <summary>
    /// The device, whichever backend is running - a GL one under <c>--backend=desktopgl</c> or the
    /// ANGLE renderer, a <see cref="SokolGraphicsDevice"/> otherwise. Nothing outside the
    /// constructor needs to know which, which is the point of the abstraction; the choice is made
    /// at launch by <see cref="ParseRenderer"/> and, within the sokol half, by
    /// <c>--sokol-backend=</c> as well (see <see cref="Main"/>).
    /// </summary>
    private readonly IGraphicsDevice _device;

    /// <summary>
    /// The sokol device and the platform behind it, both null when the device is the GL one.
    ///
    /// Held for the two things <see cref="IGraphicsDevice"/> deliberately does not expose: the sokol
    /// frame boundary (<see cref="SokolGraphicsDevice.EndFrame"/>, which is the per-frame upload
    /// budget rather than a device operation) and <see cref="ISokolPlatform.VSync"/>, which is a
    /// property of the present call rather than of the device and so lives on the platform.
    ///
    /// The platform is typed as the interface, not as <see cref="SokolD3D11Platform"/>: which one it
    /// is follows from <c>--sokol-backend=</c>, and the only thing this class needs from it is
    /// <c>VSync</c>.
    /// </summary>
    private readonly SokolGraphicsDevice? _sokolDevice;

    private readonly ISokolPlatform? _sokolPlatform;

    /// <summary>
    /// The GL context SDL created for that window, owned and destroyed by this class - or zero on
    /// every other backend, because SDL never made one.
    ///
    /// It is not owned by the device: on that path <c>GlGraphicsDevice.Create</c> attaches to a
    /// context somebody else made current and never creates or destroys one, which is why the
    /// destroy below is here rather than in the backend's own Dispose.
    /// </summary>
#pragma warning disable CS0649 // only assigned on the two GL renderers; see the constructor
    private readonly IntPtr _glContext;
#pragma warning restore CS0649

    /// <summary>
    /// Which sokol backend this process was launched with, from <c>--sokol-backend=</c>.
    ///
    /// Static, and read by the constructor, because the choice has to be made before the constructor
    /// runs: it decides which native <c>sokol.dll</c> the resolver loads (see
    /// <see cref="Main"/>), and a wrong answer is a backend that cannot be swapped afterwards -
    /// <c>sg_setup</c> is process-global and sokol's backend is fixed in the DLL itself.
    ///
    /// Defaults to <see cref="SokolBackend.D3d11"/> so that a process started without the flag (or
    /// under a debugger, or from a test) behaves as it did before the flag existed.
    /// </summary>
    private static SokolBackend _sokolBackend = SokolBackend.D3d11;

    /// <summary>
    /// Which rendering backend this process was launched with, from <c>--backend=</c>.
    ///
    /// Sokol's own backend is chosen inside it by <c>--sokol-backend=</c> (see
    /// <see cref="_sokolBackend"/>); this is the level above, picking between sokol and the two GL
    /// devices. Static and read by the constructor for the same reason as
    /// <see cref="_sokolBackend"/>: the answer decides what the constructor builds, and it has to be
    /// settled before the first line of it runs.
    ///
    /// The default is <see cref="Renderer.Sokol"/>, matching <c>--sokol-backend=</c> defaulting to
    /// D3D11 - a process started with no arguments behaves as it did before either flag existed.
    /// </summary>
    private static Renderer _renderer = Renderer.Sokol;

    /// <summary>
    /// The top-level rendering backend behind <c>--backend=</c>.
    ///
    /// A runtime choice rather than a define, unlike the older <c>ANGLE</c> one: the comparison this
    /// harness exists for is between sokol and desktop GL on the same machine, and a build-time
    /// switch would mean the two numbers came from different binaries. Only the ANGLE backend stays
    /// behind a define, because it is the one that needs a native ANGLE package the other paths do
    /// not.
    /// </summary>
    private enum Renderer
    {
        /// <summary>sokol_gfx, with its own backend from <c>--sokol-backend=</c>.</summary>
        Sokol,

        /// <summary>
        /// Desktop OpenGL 3.3 core, through our own backend rather than sokol's.
        ///
        /// Independent of sokol entirely: it links its own program from the bundles' <c>Glsl330</c>
        /// form and drives the host's context directly. This is the path that answers whether GL's
        /// cost on this scene was ANGLE's translation layer or GL's own.
        /// </summary>
        DesktopGl,

        /// <summary>
        /// The ANGLE/GLES backend, only present under the <c>ANGLE</c> define.
        ///
        /// Kept selectable so the baseline number can be reproduced without rebuilding, but it can
        /// only be named when the define is on - see <see cref="ParseRenderer"/>.
        /// </summary>
        Angle,
    }

    /// <summary>The <c>--backend=</c> argument. See <see cref="ParseRenderer"/>.</summary>
    private const string RendererArgumentPrefix = "--backend=";

    /// <summary>
    /// Which renderer <paramref name="args"/> names, defaulting to <see cref="Renderer.Sokol"/>.
    ///
    /// An unknown value throws rather than falling back, for the same reason
    /// <see cref="SokolBackendSelection.Parse"/> does: the default is a working backend, so a typo
    /// would otherwise produce an entirely normal-looking run that measured the wrong thing.
    /// </summary>
    /// <exception cref="ArgumentException">On an unknown renderer name.</exception>
    private static Renderer ParseRenderer(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(RendererArgumentPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = arg[RendererArgumentPrefix.Length..];
            if (value.Equals("sokol", StringComparison.OrdinalIgnoreCase))
                return Renderer.Sokol;

            // Named but not built in: the message has to say so, because the generic "unknown"
            // below would send someone looking for a typo in a name that is spelled correctly.
            if (value.Equals("angle", StringComparison.OrdinalIgnoreCase))
            {
#if ANGLE
                return Renderer.Angle;
#else
                throw new ArgumentException(
                    $"{RendererArgumentPrefix}angle needs this build to include the ANGLE backend, " +
                    "which is behind the NfmWorldAngle property - rebuild with " +
                    "-p:NfmWorldAngle=true to use it.");
#endif
            }

            if (value.Equals("desktopgl", StringComparison.OrdinalIgnoreCase))
                return Renderer.DesktopGl;

            throw new ArgumentException(
                $"Unknown {RendererArgumentPrefix}{value}. Valid renderers: sokol, desktopgl" +
#if ANGLE
                ", angle" +
#endif
                ".");
        }

        return Renderer.Sokol;
    }

#if ANGLE
    /// <summary><c>SDL_GL_CONTEXT_PROFILE_ES</c>, from <c>SDL_video.h</c> - the EGL/ES profile, as opposed to Core or Compatibility. Only the GL path asks for a profile.</summary>
    private const int EsProfile = 0x0004;
#endif

    /// <summary><c>SDL_GL_CONTEXT_PROFILE_CORE</c>, from <c>SDL_video.h</c>.</summary>
    private const int CoreProfile = 0x0001;

    /// <summary>
    /// The multisample count most recently asked of the swapchain, so
    /// <see cref="EnsureSwapchainMatchesWindow"/> can tell "the setting changed" from "the backend
    /// allocated something else". See that method for why the allocated count is the wrong thing to
    /// compare against.
    /// </summary>
    private int _appliedMultiSampleRequest;

    public static SdlImGuiRenderer? ImguiRenderer;
    private UiRenderer? _uiRenderer;

    internal static long LastFrameTime;
    internal static long LastTickTime;
    internal static double LastAsyncTime;
    internal static int LastTickCount;
    private Keys _oldKeyState;
    private MouseButtons _oldMouseState;
    private Int2 _oldMousePosition;
    private int _oldScrollValue;
    // UI drag state: the renderer only receives pressed/moved/released today, so
    // mouse-drag events (needed by the Sx Slider) are synthesized here from a held
    // primary button + pointer movement since the press.
    private bool _mouseDragging;
    private Int2 _mouseDragStart;
    private NanoVGRenderer? _nvg;
    private TimeStep _tickTimeStep = new((1000f / Physics.TargetTps) / 1000f);
    public static bool LowLatency = false;
    public static int NumCascades = 3;
    public static int ShadowResolution = 2048;

    private static bool _loaded;
    private const int FrameDelay = (int) (1000 / 21.3f);

    private int _yogaDebugPage = -1;

    /// <summary>Whether the window currently has input focus. Replaces FNA's <c>Game.IsActive</c>.</summary>
    public bool IsActive => Window.HasFocus;

    /// <summary>
    /// Pushes <see cref="GraphicsSettingsShim.SynchronizeWithVerticalRetrace"/> onto the sokol
    /// platform's present interval. A no-op on the ANGLE path, where <c>_sokolPlatform</c> is null
    /// and the interval was fixed when the context was created -
    /// <see cref="GraphicsSettingsShim"/>'s own remarks say so, and the settings menu still reports
    /// the change as needing a restart there.
    ///
    /// Called every frame from the loop rather than from wherever the setting is written, because the
    /// setting is written from an ImGui callback that runs with a command buffer already live: the
    /// same reason <see cref="EnsureSwapchainMatchesWindow"/> defers its rebuild to here.
    /// </summary>
    private void SyncSokolVSync()
    {
        if (_sokolPlatform is not null) _sokolPlatform.VSync = Graphics.SynchronizeWithVerticalRetrace;
    }

    /// <summary>Replaces FNA's <c>Game.IsFixedTimeStep</c> - read by the manual loop in <see cref="Main"/>.</summary>
    public bool IsFixedTimeStep { get; set; } = false;

    /// <summary>Replaces FNA's <c>Game.TargetElapsedTime</c> - read by the manual loop in <see cref="Main"/> and by <see cref="Draw"/>'s alpha calculation.</summary>
    public TimeSpan TargetElapsedTime { get; set; } = TimeSpan.FromMilliseconds(1000 / Physics.TargetTps);

    /// <summary>All real key codes in <see cref="Key"/> (excludes the <c>KeyCode</c>/<c>Modifiers</c>/<c>Shift</c>/<c>Control</c>/<c>Alt</c> bitmask sentinels), for <see cref="UpdateInput"/>'s per-frame diff. Replaces enumerating <c>Microsoft.Xna.Framework.Input.Keys</c>.</summary>
    private static readonly Key[] AllKeys = Enum.GetValues<Key>().Where(k => (uint)k <= 0xFE).Distinct().ToArray();

    private bool _disposed;

    private WorldGame()
    {
        GameThreadContext.Install();

        // The context belongs to SDL, so there is no GL library to load and no context to create
        // here - this only has to bring up the one SDL makes current. The order around it is not
        // free, though, and every step below is load-bearing.

        // SDL_GL_LoadLibrary refuses to run before the video subsystem is up ("Video subsystem has
        // not been initialized"), and SdlWindow.Create is what brings the subsystem up - so it has
        // to run first, here, explicitly. Its result is checked rather than ignored: it was the
        // ignored failure that made the original ordering look harmless.
        if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.SDL_GetError()}");

        // Which device this process runs on. Three arms, and what they actually differ in is who
        // creates the GL context - SDL for both GL paths, nobody for D3D11.
        switch (_renderer)
        {
            case Renderer.Angle:
#if ANGLE
            {
                // ANGLE only exists under its define, so this arm is compiled out with it. The
                // switch's default arm below reports that rather than this failing to build.
                var (angleContext, angleWindow) = CreateGlContext(
                    profileMask: EsProfile, major: 3, minor: 0,
                    // Forces SDL down its EGL path rather than WGL, so the context is ANGLE's.
                    hint: (SDL.SDL_HINT_OPENGL_ES_DRIVER, "1"));

                _glContext = angleContext;
                Window = angleWindow;
                _device = CreateAngleDevice(angleWindow);
                break;
            }
#else
                throw new NotSupportedException(
                    "The ANGLE renderer needs a build with the ANGLE backend enabled - rebuild with " +
                    "-p:NfmWorldAngle=true. See ParseRenderer, which normally rejects it earlier.");
#endif

            case Renderer.DesktopGl:
            {
                // Desktop GL 3.3 core. Same shape as the ANGLE arm - SDL owns the context - but no
                // ES hint, a core profile, and a device that compiles the bundles' Glsl330 form
                // rather than their GlslEs one.
                var (desktopContext, desktopWindow) = CreateGlContext(
                    profileMask: CoreProfile, major: 3, minor: 3, hint: null);

                _glContext = desktopContext;
                Window = desktopWindow;
                var desktopDevice = GlGraphicsDeviceDesktop.Create(
                    SDL.SDL_GL_GetProcAddress, desktopWindow.Width, desktopWindow.Height,
                    () => SDL.SDL_GL_SwapWindow(desktopWindow.Handle));
                _device = desktopDevice;
                break;
            }

            default:
            {
                // The sokol path. Which backend it is comes from --sokol-backend= (see Main), and
                // the two differ in who brings up the 3D API - which is the whole of what this
                // switch decides.
                SokolGraphicsDevice sokol;

            switch (_sokolBackend)
            {
                case SokolBackend.D3d11:
                {
                    // Nothing to ask SDL for: sokol_gfx has no window concept at all, and its D3D11
                    // backend is handed an ID3D11Device and an ID3D11DeviceContext and nothing else
                    // (sokol_gfx.h:5361-5364). The swapchain is built here, over SDL's HWND, which is
                    // what makes hosting it on somebody else's window possible.
                    Window = SdlWindow.Create("NFM World", 1280, 720);

                    var platform = new SokolD3D11Platform(Window.NativeWindowHandle, Window.Width, Window.Height);
                    _sokolPlatform = platform;
                    sokol = SokolGraphicsDevice.Create(platform, Window.Width, Window.Height);
                    break;
                }

                case SokolBackend.Glcore:
                {
                    // Desktop GL is the one sokol backend that is the *other* way round: sokol does not
                    // bring up the 3D API and is told nothing about it, because on this path SDL owns
                    // the context. Two consequences follow, and both are ordered deliberately.
                    //
                    // First, the attributes and the context have to exist before sg_setup: sokol's GL
                    // backend reads GL_MAJOR_VERSION/GL_MINOR_VERSION during setup to build its
                    // capability table (sokol_gfx.h:10494-10520) and takes no environment struct to
                    // defer them through. Hence the same ordering the ANGLE path above uses.
                    //
                    // Second, SDL has to know the surface type up front, so unlike the D3D11 arm above
                    // the window is created with SDL_WINDOW_OPENGL.
                    //
                    // A *core* profile, not the ANGLE path's ES profile: sokol's GLCORE backend emits
                    // and accepts desktop GLSL and uses desktop-only entry points. The profile enum is
                    // SDL_GL_CONTEXT_PROFILE_CORE = 0x0001 from SDL_video.h; the binding has the attr
                    // but not the value, hand-declared for the same reason EsProfile above is.
                    const int coreProfile = 0x0001;

                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, coreProfile);

                    // Not optional here either, and for a reason specific to this backend: a GLCORE
                    // swapchain pass binds only the default framebuffer (sokol_gfx.h:12158) and has no
                    // attachment list, so the depth-stencil buffers are whatever the window's pixel
                    // format has. Without asking, SDL picks a config with stencil=0 (measured on this
                    // machine: stencil=0 depth=16) and the NanoVG stencil passes - every one of that
                    // renderer's nine pipelines - silently operate on a buffer that does not exist.
                    SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_STENCIL_SIZE, 8);

                    Window = SdlWindow.Create(
                        "NFM World", 1280, 720, extraFlags: SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);

                    _glContext = SDL.SDL_GL_CreateContext(Window.Handle);
                    if (_glContext == IntPtr.Zero)
                        throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL.SDL_GetError()}");

                    // SDL owns the context and the drawable behind it, so this platform owns neither:
                    // it names the default framebuffer for sokol to bind and presents through SDL. Both
                    // callbacks are the caller's because this assembly holds no SDL reference - the same
                    // shape GlGraphicsDevice.Create already takes.
                    var glPlatform = new SokolGlPlatform(
                        Window.Width, Window.Height,
                        present: () => SDL.SDL_GL_SwapWindow(Window.Handle),
                        setSwapInterval: interval => SDL.SDL_GL_SetSwapInterval(interval));

                    _sokolPlatform = glPlatform;
                    sokol = SokolGraphicsDevice.Create(glPlatform, Window.Width, Window.Height);
                    break;
                }

                    default:
                        // Unreachable while SokolBackend has only these two members; the throw is here so
                        // that adding one without a platform is a named failure rather than a null device.
                        throw new NotSupportedException(
                            $"No platform is implemented for the {_sokolBackend} sokol backend, please update " +
                            $"{nameof(WorldGame)}'s constructor.");
                }

            _sokolDevice = sokol;
            _device = sokol;
            break;
            }
        }

        Graphics = new GraphicsSettingsShim(Window, _device.Swapchain);

        // VSync is applied per present rather than at creation (the present interval is an argument
        // to IDXGISwapChain::Present), so unlike the GL path it can follow the setting - the frame
        // loop re-syncs it every frame, and this only seeds it so the very first frame is not
        // presented with the wrong interval.
        SyncSokolVSync();


        Window.Resized += (w, h) =>
        {
            EnsureSwapchainMatchesWindow();
            GameSparker.WindowSizeChanged(w, h);
            GameSparker.CurrentPhase.WindowSizeChanged(w, h);
            G.Scale = h / 720f;
        };

        Window.TextInput += character =>
        {
            var imguiWantsKeyboard = ImguiRenderer is not null && ImGui.GetIO().WantCaptureKeyboard;
            if (!imguiWantsKeyboard)
            {
                GameSparker.UiRenderer?.HandleKeyTyped(character);
            }

            GameSparker.CurrentPhase.KeyTyped(character, imguiWantsKeyboard);
        };
    }

    /// <summary>
    /// Creates the SDL window and the GL context the two GL renderers run on, and returns both.
    ///
    /// Shared by the ANGLE and DesktopGL arms because the sequence is identical and the ordering is
    /// load-bearing in a way that is easy to lose by duplicating it: SDL_GL_LoadLibrary refuses to
    /// run before the video subsystem is up, the attributes have to be set before the window because
    /// SDL builds the window's surface during creation and reads the profile from them at that point,
    /// and the context has to exist before the device attaches to it.
    ///
    /// The two callers differ only in what they ask for, which is why that is the parameter list:
    /// ANGLE wants an ES 3.0 profile and SDL's EGL hint, desktop GL wants a 3.3 core profile and no
    /// hint at all.
    /// </summary>
    /// <param name="profileMask">
    /// <c>SDL_GL_CONTEXT_PROFILE_MASK</c>: <see cref="EsProfile"/> or <see cref="CoreProfile"/>.
    /// </param>
    /// <param name="hint">
    /// A hint to set before anything else, or null for none. Tuple rather than two parameters so the
    /// null case cannot be half-specified.
    /// </param>
    private static (IntPtr Context, SdlWindow Window) CreateGlContext(
        int profileMask, int major, int minor, (string Name, string Value)? hint)
    {
        if (hint is { } h)
            SDL.SDL_SetHint(h.Name, h.Value);

        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, minor);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, profileMask);

        // A stencil buffer is not optional: every one of NanoVG's nine pipelines is a stencil pass
        // (AbstractionNvgRenderer's StencilFill1/2/3 descriptions), and the UI is drawn straight into
        // the default framebuffer - there is no separate depth-stencil attachment the way the sokol
        // path allocated one for NVG. Without asking for one SDL picks a config with stencil=0
        // (measured on this machine: stencil=0 depth=16), and glStencilFunc/glStencilOp against a
        // buffer that does not exist fail silently - no GL error, no draw. The visible result is the
        // UI's coverage/blend maths running against a stencil that reads as a constant, which shows
        // up as fills that do not accumulate alpha over one another.
        //
        // 8 is the smallest depth every driver here offers; ANGLE's D3D11 backend maps the request
        // onto a D24S8 depth-stencil surface, so this does not cost a separate buffer.
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_STENCIL_SIZE, 8);

        if (!SDL.SDL_GL_LoadLibrary(null))
            throw new InvalidOperationException($"SDL_GL_LoadLibrary failed: {SDL.SDL_GetError()}");

        var window = SdlWindow.Create(
            "NFM World", 1280, 720, extraFlags: SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);

        var context = SDL.SDL_GL_CreateContext(window.Handle);
        if (context == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL.SDL_GetError()}");

        return (context, window);
    }

#if ANGLE
    /// <summary>
    /// Builds the ANGLE device over a context <see cref="CreateGlContext"/> made.
    ///
    /// Kept separate from the switch arm because of the long note below, which is about this path
    /// specifically and not about the arm that selects it.
    /// </summary>
    private static IGraphicsDevice CreateAngleDevice(SdlWindow window)
    {
        // Attaches to the context SDL just made current, rather than creating one: the device takes
        // the getProcAddress callback and never owns a context on this path.
        //
        // This was the last thing to work, and what was wrong before it is not fully understood -
        // recorded here so nobody re-derives the wrong answer from the shape of the fix. What is
        // measured: with SDL's EGL config, a context reports current under eglGetCurrentContext yet
        // every gl* call through it returns 0 with GL_INVALID_OPERATION, which GlShaderProgram turned
        // into "shader failed to compile" with an empty info log. That config (put in place by an
        // earlier SDL_GL_EGL_PLATFORM) differs from a working one only in DEPTH_SIZE (16 vs 24) and
        // NATIVE_RENDERABLE (false vs true). A context built from a config we chose on SDL's own
        // display works, so the discriminator is the config and not the display - and removing
        // SDL_GL_EGL_PLATFORM was tried on its own and did not fix it, so that is not the mechanism
        // either. Two other unverified explanations were offered later and are both contradicted:
        // that SDL_HINT_VIDEO_FORCE_EGL is required (it is not set here and the tree works), and
        // that SDL's config is NULL (it was read back, and it has real attributes).
        return GlGraphicsDevice.Create(
            SDL.SDL_GL_GetProcAddress, window.Width, window.Height,
            () => SDL.SDL_GL_SwapWindow(window.Handle));
    }
#endif

    private void Update(GameTime gameTime)
    {
        FPSCounter.Update(gameTime, LastTickTime, LastFrameTime, LastAsyncTime);

        UpdateInput();
        UpdateMouse();

        _uiRenderer?.Update(gameTime);

        if (!_loaded)
        {
            _loaded = true;
        }

        var timesToTick = _tickTimeStep.Update(gameTime);
        if (timesToTick > 0)
        {
            LastTickTime = 0;

            for (var i = 0; i < timesToTick; i++)
            {
                var tick = new MicroStopwatch();
                tick.Start();

                var transaction = SentrySdk.StartTransaction("GameTick", "gameloop.tick");
                GameSparker.CurrentPhase.BeginGameTick();
                GameSparker.GameTick();
                GameSparker.CurrentPhase.GameTick();
                GameSparker.CurrentPhase.EndGameTick();
                transaction.Finish();

                LastTickTime += tick.ElapsedMicroseconds;
            }

            LastTickCount = timesToTick;
        }

        {
            var transaction = SentrySdk.StartTransaction("GameThreadContext", "gameloop.gamethread");
            var t = new MicroStopwatch();
            t.Start();
            if (GameThreadContext.Current.ExecutePendingTasks())
                LastAsyncTime = t.ElapsedTicks * (1000000D / Stopwatch.Frequency);
            transaction.Finish();
        }

        // Dispose any phases that were popped/replaced this frame.
        // Must happen after all game logic to avoid disposal during event handlers.
        GameSparker.Phases.FlushDisposals();
    }

    private void Initialize()
    {
        ImguiRenderer = new SdlImGuiRenderer(_device, Window);
        _nvg = new NanoVGRenderer(_device);

        // Must be constructed (and GameSparker.UiRenderer assigned) before GameSparker.Load()
        // (called from LoadContent(), which runs after Initialize()) pushes MainMenuPhase - phases
        // register their PhaseBridge with GameSparker.UiRenderer from BasePhase.Enter(), which
        // no-ops silently if it's still null at that point.
        _uiRenderer = new UiRenderer(this);
        GameSparker.UiRenderer = _uiRenderer;

        _oldKeyState = SdlWindow.GetKeyboardState();
        var (mouseButtons, mouseX, mouseY) = SdlWindow.GetMouseState();
        _oldMouseState = mouseButtons;
        _oldMousePosition = new Int2(mouseX, mouseY);
        _oldScrollValue = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose all phases before tearing down CEF and graphics.
        GameSparker.Phases.Shutdown();

        _uiRenderer?.Dispose();
        ShadowMapDebugView.Dispose();
        foreach (var shadowRenderTarget in ShadowRenderTargets)
        {
            shadowRenderTarget?.Dispose();
        }
        ImguiRenderer?.Dispose();
        _nvg?.Dispose();

        // Device before window, and context before either: the device holds GL objects created
        // against the context, and the next two lines destroy the thing they live in. Currently
        // GlGraphicsDevice.Dispose is a no-op on this path (it owns nothing without an EGL context of
        // its own), but the ordering is the contract its own Dispose documents, so it is kept.
        //
        // Disposing the device also tears down the platform behind it (SokolSwapchain owns the
        // platform, and SokolGraphicsDevice.Dispose disposes the swapchain) - which is why the
        // D3D11 device and swapchain go before the window they were created against.
        _device.Dispose();

        // Zero on every backend that did not create a GL context, which SDL_GL_DestroyContext treats
        // as a no-op rather than an error.
        SDL.SDL_GL_DestroyContext(_glContext);
        Window.Dispose();

        // FNA's own FAudio device is cleaned up via FAudioContext.Dispose() on app domain
        // exit, and the direct-FAudio engine (FaudioEngine) lives for the process lifetime.
    }

    private void LoadContent()
    {
        ImguiRenderer!.RebuildFontAtlas();
        ImGuiTheme.Apply();

        // NanoVGRenderer (constructed in Initialize(), before this runs) already set
        // IBackend.Backend to the real WorldClientBackend - no DummyBackend stand-in needed anymore.

        // GameSparker.NewGraphicsDevice must be set before Effects.Initialize/GameSparker.Load,
        // since both transitively construct render elements (Ground/Sky/meshes/...) that read it
        // directly (see that field's doc comment).
        GameSparker.NewGraphicsDevice = _device;
        Effects.Initialize(_device);
        RebuildCascades();

        GameSparker.Load(this);

        SettingsMenu.LoadConfig();
    }

    public void RebuildCascades()
    {
        foreach (var target in ShadowRenderTargets)
        {
            target?.Dispose();
        }
        ShadowRenderTargets.Clear();

        for (var i = 0; i < NumCascades; i++)
        {
            ShadowRenderTargets.Add(_device.CreateRenderTarget(new global::NFMWorld.Graphics.RenderTargetDesc(
                ShadowResolution, ShadowResolution, global::NFMWorld.Graphics.TextureFormat.Single,
                HasDepthStencil: true, global::NFMWorld.Graphics.TextureFormat.Depth24Stencil8)));
        }
    }

    private void UpdateInput()
    {
        var keys = SdlWindow.GetKeyboardState();

        foreach (var nfmKey in AllKeys)
        {
            var imguiWantsKeyboard = ImguiRenderer is not null && ImGui.GetIO().WantCaptureKeyboard;
            if (keys[nfmKey] && !_oldKeyState[nfmKey])
            {
                GameSparker.KeyPressed(nfmKey);

                if (!imguiWantsKeyboard)
                {
                    GameSparker.UiRenderer?.HandleKeyPressed(nfmKey, keys);
                }

                GameSparker.CurrentPhase.KeyPressed(nfmKey, imguiWantsKeyboard, keys);

                if (nfmKey == Key.F9)
                {
                    _yogaDebugPage++;
                    if (_yogaDebugPage > YogaDebugger.MaxPages) _yogaDebugPage = -1;
                }
            }
            else if (!keys[nfmKey] && _oldKeyState[nfmKey])
            {
                GameSparker.KeyReleased(nfmKey);

                if (!imguiWantsKeyboard)
                {
                    GameSparker.UiRenderer?.HandleKeyReleased(nfmKey, keys);
                }

                GameSparker.CurrentPhase.KeyReleased(nfmKey, imguiWantsKeyboard, keys);
            }
        }

        // Update saved state.
        _oldKeyState = keys;
    }

    private static readonly MouseButtons[] MouseButtonsArray = Enum.GetValues<MouseButtons>();
    private void UpdateMouse()
    {
        var (buttons, mouseX, mouseY) = SdlWindow.GetMouseState();
        var mousePosition = new Int2(mouseX, mouseY);
        var scrollValue = SdlWindow.GetScrollWheelValue();

        var ctrlKey = _oldKeyState[Key.LControlKey] || _oldKeyState[Key.RControlKey];
        var shiftKey = _oldKeyState[Key.LShiftKey] || _oldKeyState[Key.RShiftKey];
        var altKey = _oldKeyState[Key.Alt];
        var wantCaptureMouse = ImguiRenderer is not null && ImGui.GetIO().WantCaptureMouse;

        foreach (var button in MouseButtonsArray)
        {
            var nfmButton = button switch
            {
                MouseButtons.None => MouseButton.Primary,
                MouseButtons.Primary => MouseButton.Primary,
                MouseButtons.Secondary => MouseButton.Secondary,
                MouseButtons.Middle => MouseButton.Middle,
                MouseButtons.XButton1 => MouseButton.XButton1,
                MouseButtons.XButton2 => MouseButton.XButton2,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (buttons.HasFlag(button) && !_oldMouseState.HasFlag(button))
            {
                if (!wantCaptureMouse)
                {
                    GameSparker.UiRenderer?.HandleMousePressed(mouseX, mouseY, nfmButton, buttons, ctrlKey, shiftKey, altKey);
                    if (nfmButton == MouseButton.Primary)
                    {
                        _mouseDragging = true;
                        _mouseDragStart = mousePosition;
                    }
                }

                GameSparker.CurrentPhase.MousePressed(mouseX, mouseY, wantCaptureMouse, nfmButton, buttons, ctrlKey, shiftKey, altKey);
            }
            else if (!buttons.HasFlag(button) && _oldMouseState.HasFlag(button))
            {
                if (!wantCaptureMouse)
                {
                    GameSparker.UiRenderer?.HandleMouseReleased(mouseX, mouseY, nfmButton, buttons, ctrlKey, shiftKey, altKey);
                }

                if (nfmButton == MouseButton.Primary)
                {
                    _mouseDragging = false;
                }

                GameSparker.CurrentPhase.MouseReleased(mouseX, mouseY, wantCaptureMouse, nfmButton, buttons, ctrlKey, shiftKey, altKey);
            }
        }

        if (mousePosition.X != _oldMousePosition.X || mousePosition.Y != _oldMousePosition.Y)
        {
            if (!wantCaptureMouse)
            {
                GameSparker.UiRenderer?.HandleMouseMoved(mouseX, mouseY, buttons, ctrlKey, shiftKey, altKey);
                if (_mouseDragging)
                {
                    GameSparker.UiRenderer?.HandleMouseDragged(mouseX, mouseY, _mouseDragStart.X, _mouseDragStart.Y, MouseButton.Primary, buttons, ctrlKey, shiftKey, altKey);
                }
            }

            GameSparker.CurrentPhase.MouseMoved(mouseX, mouseY, wantCaptureMouse, buttons, ctrlKey, shiftKey, altKey);
            YogaDebugger.MouseMove(mousePosition.X, mousePosition.Y);
        }

        if (scrollValue != _oldScrollValue)
        {
            var delta = scrollValue - _oldScrollValue;

            if (!wantCaptureMouse)
            {
                GameSparker.UiRenderer?.HandleMouseScrolled(mouseX, mouseY, delta, buttons, ctrlKey, shiftKey, altKey);
            }

            GameSparker.CurrentPhase.MouseScrolled(mouseX, mouseY, delta, wantCaptureMouse, buttons, ctrlKey, shiftKey, altKey);
        }

        _oldMouseState = buttons;
        _oldMousePosition = mousePosition;
        _oldScrollValue = scrollValue;
    }

    /// <summary>
    /// Binds the scissor rect to the whole window. This is the invariant that goes with every
    /// pipeline having scissor testing enabled (<see cref="Effects.ScissorRasterizer"/>): wherever a
    /// render target or viewport is bound, a scissor rect is bound too. A full-target rect is a
    /// no-op, but it has to be *set* rather than left alone, because the ImGui renderer binds one
    /// rect per draw command and never restores it - without this the first 3D draws of the next
    /// frame would be clipped to whatever rectangle ImGui's last widget occupied.
    /// </summary>
    public void SetFullScreenScissor(ICommandBuffer cb)
    {
        cb.SetScissorRect(new ScissorRect(0, 0, Window.Width, Window.Height));
    }

    /// <summary>
    /// Keeps the device's drawable in step with the OS window and the video settings. FNA's
    /// GraphicsDeviceManager did the size half of this on every ApplyChanges; without it the device
    /// keeps rendering into the size it was created at while <see cref="SdlWindow.Width"/>/<see cref="SdlWindow.Height"/>
    /// - and every SDL mouse coordinate - follow the real window. Everything laid out from
    /// <c>Swapchain.Width/Height</c> (the NanoVG canvas and its ortho transform, <c>Scene.Render</c>'s
    /// viewport, the particle/line "Resolution" uniforms) then stays at the old size, so the 2D UI
    /// occupies that corner of the window and input only lands inside it.
    /// <para>
    /// This is also the only place the multisample count can change: it is a backbuffer property, so
    /// it needs a drawable rebuild, and the settings screen requests it from an ImGui callback that
    /// runs with the frame's command buffer already acquired - the one moment a rebuild must not
    /// happen. Requested here, applied here, before anything is drawn.
    /// </para>
    /// </summary>
    private void EnsureSwapchainMatchesWindow()
    {
        var swapchain = _device.Swapchain;
        var multiSampleCount = Graphics.DesiredMultiSampleCount;

        // The multisample half of this comparison is against AppliedMultiSampleCount rather than
        // MultiSampleCount, and that is not a rename. ISwapchain documents MultiSampleCount as what
        // the device *actually allocated* - the honest answer after clamping - which is exactly what
        // the settings UI wants to display (and does, via AppliedMultiSampleCount). But comparing a
        // *request* against an *allocation* never converges when they differ: the GL backend cannot
        // multisample the default framebuffer at all, so it allocates 0 whatever it is asked for, and
        // `desired != allocated` would rebuild the drawable on every frame forever. Resize is not
        // free (it is a backbuffer rebuild), and on the GL path it would not even help.
        //
        // So the comparison is against the last count actually *requested*, which is what makes it
        // converge: FNA3DSwapchain already tracks this internally and documents the same trap. Here
        // the request is recoverable from the shim, because Resize only ever writes back what it was
        // asked for - so a mismatch means the setting changed, or the window did, and one rebuild is
        // enough. Note a request the backend cannot honour does not loop; it simply takes effect as 0.
        if (Window.Width != swapchain.Width || Window.Height != swapchain.Height
            || multiSampleCount != _appliedMultiSampleRequest)
        {
            swapchain.Resize(Window.Width, Window.Height, multiSampleCount);
            _appliedMultiSampleRequest = multiSampleCount;
        }
    }

    private void Draw(GameTime gameTime)
    {
        var transaction = SentrySdk.StartTransaction("GameDraw", "gameloop.draw");

        var alpha = LowLatency ? 1f : (float)((double)gameTime.ElapsedGameTime.Ticks / TargetElapsedTime.Ticks);

        var t = Stopwatch.StartNew();

        // Before the command buffer, never between AcquireCommandBuffer and Submit: resetting the
        // backbuffer rebuilds the driver's swapchain, which can't happen with a recording (even an
        // immediate-mode one) live. This is the second call site on purpose - the Resized handler
        // covers the common case, this covers anything that changes the window size without one
        // (a fullscreen/borderless toggle that emits no resize event, say) so no frame can render
        // into a drawable that no longer matches the window.
        EnsureSwapchainMatchesWindow();

        // Beside the swapchain check rather than inside it: that method is about the drawable, and
        // this is about the present call. Both are here, before the command buffer, so nothing
        // reaches the device while the frame is being recorded.
        SyncSokolVSync();

        var cb = _device.AcquireCommandBuffer();
        cb.Clear(ClearOptions.Color | ClearOptions.Depth | ClearOptions.Stencil,
            new ColorRgba(Color.CornflowerBlue.R / 255f, Color.CornflowerBlue.G / 255f, Color.CornflowerBlue.B / 255f));
        cb.SetViewport(new Graphics.Viewport(0, 0, Window.Width, Window.Height));
        SetFullScreenScissor(cb);

        // Started before the phases render, not after: phases draw their HUD text through
        // TheGraphics (the same NvgContext), and rasterizing a glyph uploads its atlas rectangle
        // through this renderer straight away. Deferred updates still work (the renderer queues
        // them), but starting the frame here means the common case uploads immediately.
        _nvg!.BeginFrame(cb);

        // The frame's render phases, in order: the 3D scene, its overlays, the UI, then NanoVG's
        // flush. Deferred NVG updates still work (the renderer queues them), but BeginFrame above
        // means the common case - a phase drawing HUD text through TheGraphics, which uploads a
        // glyph atlas rectangle straight away - happens immediately.
        GameSparker.Render(cb, alpha);
        GameSparker.Render3DOverlays(cb);

        _uiRenderer?.Render();
        if (_yogaDebugPage >= 0) YogaDebugger.Render(_yogaDebugPage);

        FPSCounter.Render();

        _nvg.Render();

        ImguiRenderer!.BeginLayout(gameTime);
        GameSparker.RenderImgui();
        ImguiRenderer.EndLayout(cb);

        _device.Submit(cb);
        _device.Swapchain.Present();

        // sokol's frame boundary, and its placement is load-bearing rather than tidy: sokol allows at
        // most one sg_update_buffer/sg_update_image per resource per frame, and the command buffer
        // collapses a frame's writes into that one upload by consulting this counter. Advancing it
        // before the frame's uploads have been flushed would let the same resource be written twice
        // in what sokol still considers one frame. Nothing presents here - Present above does.
        _sokolDevice?.EndFrame();

        LastFrameTime = t.ElapsedMilliseconds;

        transaction.Finish();
    }

    // Ported from FNA/src/Game.cs's Tick()/AdvanceElapsedTime()/UpdateEstimatedSleepPrecision() -
    // the manual loop below previously reset its elapsed-time tracking to `now` every iteration
    // instead of carrying the accumulator's remainder into the next frame, and assumed a fixed
    // 1ms Thread.Sleep precision instead of measuring it - both cause a fixed-timestep cap (e.g.
    // 63fps) to systematically undershoot (61-62fps) since every frame's sleep/spin overshoot past
    // TargetElapsedTime was silently discarded rather than subtracted back out of the next frame's
    // budget. This is independent of vsync (matches the report that toggling vsync didn't help).
    private const int PreviousSleepTimeCount = 128; // must be a power of 2 for the bitmask below
    private const int SleepTimeMask = PreviousSleepTimeCount - 1;
    private static readonly TimeSpan[] previousSleepTimes =
        Enumerable.Repeat(TimeSpan.FromMilliseconds(1), PreviousSleepTimeCount).ToArray();
    private static int sleepTimeIndex;
    private static TimeSpan worstCaseSleepPrecision = TimeSpan.FromMilliseconds(1);
    private static TimeSpan accumulatedElapsedTime;
    private static long previousTicks;

    private static TimeSpan AdvanceElapsedTime(Stopwatch stopwatch)
    {
        var currentTicks = stopwatch.Elapsed.Ticks;
        var timeAdvanced = TimeSpan.FromTicks(currentTicks - previousTicks);
        accumulatedElapsedTime += timeAdvanced;
        previousTicks = currentTicks;
        return timeAdvanced;
    }

    private static void UpdateEstimatedSleepPrecision(TimeSpan timeSpentSleeping)
    {
        var upperTimeBound = TimeSpan.FromMilliseconds(4);
        if (timeSpentSleeping > upperTimeBound)
        {
            timeSpentSleeping = upperTimeBound;
        }

        if (timeSpentSleeping >= worstCaseSleepPrecision)
        {
            worstCaseSleepPrecision = timeSpentSleeping;
        }
        else if (previousSleepTimes[sleepTimeIndex] == worstCaseSleepPrecision)
        {
            var maxSleepTime = TimeSpan.MinValue;
            foreach (var t in previousSleepTimes)
            {
                if (t > maxSleepTime) maxSleepTime = t;
            }
            worstCaseSleepPrecision = maxSleepTime;
        }

        previousSleepTimes[sleepTimeIndex] = timeSpentSleeping;
        sleepTimeIndex = (sleepTimeIndex + 1) & SleepTimeMask;
    }

    public static void Main(string[] args)
    {
        // TODO figure out why SDL ProcessExit doesn't work properly
        AppDomain.CurrentDomain.ProcessExit += static (sender, args) =>
        {
            Process.GetCurrentProcess().Kill(false);
        };

        // Which native sokol_gfx to load, decided before anything can touch it. Both halves have to
        // happen here and in this order: the backend decides which DLL the resolver returns, and the
        // resolver has to be installed before the first sg_* call (SettingsMenu.LoadFnaRenderer
        // below already reaches into the graphics layer).
        //
        // Only the sokol path reads this; under either GL renderer the sokol device is never
        // constructed. It is parsed unconditionally anyway so a typo is reported on every path
        // rather than only the one it was meant for.
        _sokolBackend = SokolBackendSelection.Parse(args);
        if (!SokolBackendSelection.IsSupportedOnThisPlatform(_sokolBackend, out var unsupportedReason))
            throw new PlatformNotSupportedException(unsupportedReason);

        // The level above it, and parsed first for the same reason: it decides whether sokol is in
        // play at all, which is what gates the resolver installation below.
        _renderer = ParseRenderer(args);

        // NativeLibrary.SetDllImportResolver is scoped to the assembly that DECLARES the
        // [DllImport], not the assembly that calls it - so every project with its own P/Invoke
        // declarations against a "libs/<arch>/..." deployment layout needs its own registration.
        // NFMWorld.Audio.FAudioBindings.FAudio's assembly registers its own resolver in
        // FaudioEngine's static ctor (a resolver can only be set once per assembly) - not
        // registered again here.
        NativeLibrary.SetDllImportResolver(typeof(WorldGame).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, ImportResolver);

        // A third registration, and the reason it is needed is the same rule: all 453 of SharpSokol's
        // [DllImport("sokol")] declarations live in *its* assembly, so a "sokol" arm in the resolver
        // above would never be consulted. Without this, sokol.dll resolves only because it happens to
        // sit next to the executable, which is a layout that cannot carry a per-backend choice.
        //
        // Installed only when sokol is the renderer: the two GL paths never touch a sg_* entry point,
        // and loading a sokol.dll they will not use is at best wasted and at worst a startup failure
        // on a machine that has no build of it. Nothing about the GL paths depends on this being
        // registered.
        if (_renderer == Renderer.Sokol)
            SokolBackendSelection.InstallResolver(_sokolBackend);

        SettingsMenu.LoadFnaRenderer();

        BackendGameSparker.Load(isHeadless: false);
        var program = new WorldGame();
        GameSparker.Game = program;
        program.Initialize();
        program.LoadContent();

        // Manual game loop replacing XNA's Game.Run() - timing algorithm ported from FNA's own
        // Game.Tick() (see AdvanceElapsedTime/UpdateEstimatedSleepPrecision above) so a fixed-cap
        // target (e.g. 63fps) holds precisely instead of drifting.
        var stopwatch = Stopwatch.StartNew();
        while (!program.Window.ShouldQuit)
        {
            program.Window.PumpEvents();

            AdvanceElapsedTime(stopwatch);

            if (program.IsFixedTimeStep)
            {
                // Coarse-sleep down to the estimated OS sleep precision, then spin-wait the rest -
                // avoids oversleeping past the target on platforms where Sleep(1) isn't accurate.
                while (accumulatedElapsedTime + worstCaseSleepPrecision < program.TargetElapsedTime)
                {
                    Thread.Sleep(1);
                    UpdateEstimatedSleepPrecision(AdvanceElapsedTime(stopwatch));
                }

                while (accumulatedElapsedTime < program.TargetElapsedTime)
                {
                    Thread.SpinWait(1);
                    AdvanceElapsedTime(stopwatch);
                }
            }

            TimeSpan frameElapsed;
            if (program.IsFixedTimeStep)
            {
                // Single fixed step per loop iteration (matches this loop's existing one-Update-
                // per-tick architecture) - subtract, don't zero, so any overshoot past
                // TargetElapsedTime carries into the next frame's budget instead of being lost.
                frameElapsed = program.TargetElapsedTime;
                accumulatedElapsedTime -= program.TargetElapsedTime;
            }
            else
            {
                frameElapsed = accumulatedElapsedTime;
                accumulatedElapsedTime = TimeSpan.Zero;
            }

            var gameTime = new GameTime(stopwatch.Elapsed, frameElapsed);
            program.Update(gameTime);
            program.Draw(gameTime);
        }

        program.Dispose();
    }

    private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        static string GetPlatformName()
        {
            if (OperatingSystem.IsWindows())
            {
                return "windows";
            }

            if (OperatingSystem.IsMacOS())
            {
                return  "osx";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux";
            }

            if (OperatingSystem.IsFreeBSD())
            {
                return "freebsd";
            }

            if (OperatingSystem.IsAndroid())
            {
                return "android";
            }

            // What is this platform??
            return "unknown";
        }

        if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsBrowser())
        {
            return NativeLibrary.GetMainProgramHandle(); // statically linked
        }

        string os = GetPlatformName();
        string cpu = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string wordsize = (IntPtr.Size * 8).ToString();

        var newLibraryName = libraryName switch
        {
            "SDL3" => os switch
            {
                "windows" => "SDL3.dll",
                "osx" => "libSDL3.0.dylib",
                "linux" or "freebsd" or "netbsd" => "libSDL3.so.0",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "FNA3D" => os switch
            {
                "windows" => "FNA3D.dll",
                "osx" => "libFNA3D.0.dylib",
                "linux" or "freebsd" or "netbsd" => "libFNA3D.so.0",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "FAudio" => os switch
            {
                "windows" => "FAudio.dll",
                "osx" => "libFAudio.0.dylib",
                "linux" or "freebsd" or "netbsd" => "libFAudio.so.0",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "dav1dfile" => os switch
            {
                "windows" => "dav1dfile.dll",
                "osx" => "dav1dfile.1.dylib",
                "linux" or "freebsd" or "netbsd" => "dav1dfile.so.0",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "SDL2" => os switch
            {
                "windows" => "SDL2.dll",
                "osx" => "libSDL2-2.0.0.dylib",
                "linux" or "freebsd" or "netbsd" => "libSDL2-2.0.so.0",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "steam_api" or "steam_api64" => os switch
            {
                "windows" => wordsize is "64" ? "steam_api64.dll" : "steam_api.dll",
                "osx" => "libsteam_api.dylib",
                "linux" or "freebsd" or "netbsd" => "libsteam_api.so",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            _ => os switch
            {
                "windows" => $"{libraryName}.dll",
                "osx" => $"lib{libraryName}.dylib",
                "linux" or "freebsd" or "netbsd" => $"lib{libraryName}.so",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            }
        };

        var dir = os switch
        {
            "windows" => cpu switch
            {
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "arm64",
                "x64" or "x86_64" or "amd64" => "x64",
                "x86" or "x86_32" or "i386" => "x86",
                _ => throw new PlatformNotSupportedException($"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}")
            },
            "osx" => "osx",
            "linux" or "freebsd" or "netbsd" => cpu switch
            {
                "arm32" or "armv7" or "aarch32" or "armeabi-v7a" => "libarmhf",
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "libaarch64",
                "x64" or "x86_64" or "amd64" => "lib64",
                "x86" or "x86_32" or "i386" => "lib32",
                _ => throw new PlatformNotSupportedException($"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}")
            },
            "android" => cpu switch
            {
                "arm32" or "armv7" or "aarch32" or "armeabi-v7a" => "android-armeabi-v7a",
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "android-arm64-v8a",
                "x64" or "x86_64" or "amd64" => "android-x86_64",
                "x86" or "x86_32" or "i386" => "android-x86",
                _ => throw new PlatformNotSupportedException($"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}")
            },
            _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
        };

        // Anchor to the app base directory rather than the process working directory:
        // dlopen treats a slash-containing name as a path relative to the CWD (ignoring
        // LD_LIBRARY_PATH), so a relative "libs/..." only resolves when launched from the
        // output folder. AppContext.BaseDirectory is always the output folder.
        return NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "libs", dir, newLibraryName));
    }
}
