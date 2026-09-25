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
/// <c>ANGLE</c> selects the older GL/GLES path instead (see the define in NFMWorld.csproj): SDL
/// creates the window with <c>SDL_WINDOW_OPENGL</c> and a context, and the device attaches to that
/// context rather than bringing up its own. It is kept because it is the baseline the sokol numbers
/// are compared against.
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
    /// The device, whichever backend is running. Not <c>GlGraphicsDevice</c>: which one it is comes
    /// from <see cref="SelectedBackend"/>, and nothing outside the constructor needs to know.
    /// </summary>
    private readonly IGraphicsDevice _device;

    /// <summary>
    /// The sokol device and the D3D11 platform behind it, both null when the device is the GL one.
    ///
    /// Held for the two things <see cref="IGraphicsDevice"/> deliberately does not expose: the sokol
    /// frame boundary (<see cref="SokolGraphicsDevice.EndFrame"/>, which is the per-frame upload
    /// budget rather than a device operation) and <c>VSync</c>, which is a property of the present
    /// call rather than of the device and so lives on the platform.
    /// </summary>
    private readonly SokolGraphicsDevice? _sokolDevice;

    private readonly SokolD3D11Platform? _sokolPlatform;

    /// <summary>
    /// The GL context SDL created for that window, owned and destroyed by this class - or zero on
    /// every other backend, because SDL never made one.
    ///
    /// It is not owned by the device: on that path <c>GlGraphicsDevice.Create</c> attaches to a
    /// context somebody else made current and never creates or destroys one, which is why the
    /// destroy below is here rather than in the backend's own Dispose.
    /// </summary>
#pragma warning disable CS0649 // only assigned on the ANGLE path; see the constructor
    private readonly IntPtr _glContext;
#pragma warning restore CS0649

#if ANGLE
    /// <summary><c>SDL_GL_CONTEXT_PROFILE_ES</c>, from <c>SDL_video.h</c> - the EGL/ES profile, as opposed to Core or Compatibility. Only the GL path asks for a profile.</summary>
    private const int EsProfile = 0x0004;
#endif

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

    /// <summary>TEMPORARY profiling frame counter - remove with the block in <see cref="Draw"/>.</summary>
    private int _profileFrame;

    /// <summary>Whether the window currently has input focus. Replaces FNA's <c>Game.IsActive</c>.</summary>
    public bool IsActive => Window.HasFocus;

    /// <summary>
    /// Pushes <see cref="GraphicsSettingsShim.SynchronizeWithVerticalRetrace"/> onto the sokol
    /// platform's present interval. A no-op on the GL path, where the interval was fixed when the
    /// context was created - <see cref="GraphicsSettingsShim"/>'s own remarks say so, and the
    /// settings menu still reports the change as needing a restart there.
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

#if ANGLE
        // Forces SDL down its EGL path rather than WGL, so the context below is ANGLE's.
        SDL.SDL_SetHint(SDL.SDL_HINT_OPENGL_ES_DRIVER, "1");

        // ES 3.0. SDL_GL_EGL_PLATFORM is deliberately not set, so SDL takes ANGLE's default display.
        // Setting it to EGL_PLATFORM_ANGLE_ANGLE (0x3202, as Egl.cs does for the headless path) is
        // what the original version did, and is not needed here.
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 0);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, EsProfile);

        // A stencil buffer is not optional here: every one of NanoVG's nine pipelines is a
        // stencil pass (AbstractionNvgRenderer's StencilFill1/2/3 descriptions), and the UI is
        // drawn straight into the default framebuffer - there is no separate depth-stencil
        // attachment the way the sokol path allocated one for NVG. Without asking for one SDL
        // picks a config with stencil=0 (measured on this machine: stencil=0 depth=16), and
        // glStencilFunc/glStencilOp against a buffer that does not exist fail silently in ES -
        // no GL error, no draw. The visible result is the UI's coverage/blend maths running
        // against a stencil that reads as a constant, which shows up as fills that do not
        // accumulate alpha over one another.
        //
        // 8 is the smallest depth every driver here offers; ANGLE's D3D11 backend maps the
        // request onto a D24S8 depth-stencil surface, so this does not cost a separate buffer.
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_STENCIL_SIZE, 8);

        if (!SDL.SDL_GL_LoadLibrary(null))
            throw new InvalidOperationException($"SDL_GL_LoadLibrary failed: {SDL.SDL_GetError()}");

        // The attributes above have to be set before the window, because SDL builds the window's EGL
        // surface during window creation and reads the ES profile from them at that point.
        Window = SdlWindow.Create("NFM World", 1280, 720, extraFlags: SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);

        _glContext = SDL.SDL_GL_CreateContext(Window.Handle);
        if (_glContext == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL.SDL_GetError()}");
        }

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
        _device = GlGraphicsDevice.Create(
            SDL.SDL_GL_GetProcAddress, Window.Width, Window.Height,
            () => SDL.SDL_GL_SwapWindow(Window.Handle));
#else
        // The sokol path. None of the GL setup above runs: no SDL_HINT_OPENGL_ES_DRIVER, no ES
        // attributes, no SDL_GL_LoadLibrary, no SDL_WINDOW_OPENGL, and no context - sokol_gfx has no
        // window concept at all. Its D3D11 backend is handed an ID3D11Device and an
        // ID3D11DeviceContext and nothing else (sokol_gfx.h:5361-5364), so the swapchain is built
        // here, over SDL's HWND, which is what makes hosting it on somebody else's window possible.
        //
        // The window is created with no extra flags, which is also what the GL path would look like
        // if SDL did not need to know the surface type up front - there is nothing to ask for.
        Window = SdlWindow.Create("NFM World", 1280, 720);

        var platform = new SokolD3D11Platform(Window.NativeWindowHandle, Window.Width, Window.Height);

        var sokol = SokolGraphicsDevice.Create(platform, Window.Width, Window.Height);
        _sokolPlatform = platform;
        _sokolDevice = sokol;
        _device = sokol;
#endif

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

        // TEMPORARY profiling - remove this block and the counters it reads. Attributes the frame's
        // draw-call volume and per-phase CPU cost to each renderer, which is the only way to tell
        // whether the port's 600-vs-1600fps menu regression is draw-call fixed cost (the GL backend
        // re-applies pipeline state and re-uploads the UBO per draw, where FNA3D diffed and cached)
        // or something in the per-frame path. Logged every 100 frames so the number does not itself
        // dominate the frame it reports.
        if (_profileFrame++ % 100 == 0)
        {
            DrawProfiler.Reset();
            var swScene = new MicroStopwatch();
            swScene.Start();
            GameSparker.Render(cb, alpha);
            var sceneUs = swScene.ElapsedMicroseconds;

            var sw3D = new MicroStopwatch();
            sw3D.Start();
            GameSparker.Render3DOverlays(cb);
            var overlayUs = sw3D.ElapsedMicroseconds;

            var swUi = new MicroStopwatch();
            swUi.Start();
            _uiRenderer?.Render();
            if (_yogaDebugPage >= 0) YogaDebugger.Render(_yogaDebugPage);
            FPSCounter.Render();
            var uiUs = swUi.ElapsedMicroseconds;

            var swNvg = new MicroStopwatch();
            swNvg.Start();
            _nvg.Render();
            var nvgUs = swNvg.ElapsedMicroseconds;

            // Ticks -> microseconds: the counter's frequency, not Stopwatch.Frequency guesses.
            var tickToUs = 1_000_000.0 / System.Diagnostics.Stopwatch.Frequency;
            var drawCallWallUs = (long)(DrawProfiler.DrawCallWallTicks * tickToUs);
            Logging.Info(
                $"[DRAWPROF] sceneUs={sceneUs} overlayUs={overlayUs} uiUs={uiUs} nvgUs={nvgUs} " +
                $"draws={DrawProfiler.CountDraw} " +
                $"uniformUs={(long)(DrawProfiler.UniformTicks * tickToUs)} " +
                $"attribUs={(long)(DrawProfiler.AttribTicks * tickToUs)} " +
                $"drawCallUs={(long)(DrawProfiler.DrawCallTicks * tickToUs)} " +
                // sceneUsMinusDrawUs is the number to compare across backends: sceneUs times the
                // whole phase, so it absorbs whatever the backend defers to draw time, and the two
                // backends defer very different amounts. nonDrawUs is the game's own scene work.
                $"drawCallWallUs={drawCallWallUs} " +
                $"sceneNoDrawUs={sceneUs - drawCallWallUs} " +
                // Non-zero means draws were recorded that did nothing - see the counter's doc.
                $"drawsWithNoPipeline={DrawProfiler.CountDrawWithNoPipeline}");
        }
        else
        {
            GameSparker.Render(cb, alpha);
            GameSparker.Render3DOverlays(cb);

            _uiRenderer?.Render();
            if (_yogaDebugPage >= 0) YogaDebugger.Render(_yogaDebugPage);

            FPSCounter.Render();

            _nvg.Render();
        }

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

        // NativeLibrary.SetDllImportResolver is scoped to the assembly that DECLARES the
        // [DllImport], not the assembly that calls it - so every project with its own P/Invoke
        // declarations against a "libs/<arch>/..." deployment layout needs its own registration.
        // NFMWorld.Audio.FAudioBindings.FAudio's assembly registers its own resolver in
        // FaudioEngine's static ctor (a resolver can only be set once per assembly) - not
        // registered again here.
        NativeLibrary.SetDllImportResolver(typeof(WorldGame).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, ImportResolver);

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
