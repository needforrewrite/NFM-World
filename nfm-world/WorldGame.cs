extern alias SDL3New;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Maxine.Extensions.Mathematics;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.ImGuiNet;
using NFMWorld.DriverInterface;
using NFMWorld.Graphics;
using NFMWorld.Graphics.FNA3D;
using NFMWorld.Platform.SDL3;
using NFMWorld.UI;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using Keys = NFMWorld.DriverInterface.Keys;
using Logging = NFMWorldLibrary.Logging;
using NFMWorld.Sentry;
using WorldXaml.UI.Yoga;
using ClearOptions = NFMWorld.Graphics.ClearOptions;

namespace NFMWorld;

/// <summary>
/// This sample demonstrates how to load a Direct2D1 bitmap from a file.
/// This method will be part of a future version of SharpDX API.
/// </summary>
/// <remarks>
/// Milestone 5 Stage A: window/input/device ownership moved off FNA's <c>Game</c>/
/// <c>GraphicsDeviceManager</c> onto <see cref="SdlWindow"/> + <see cref="FNA3DGraphicsDevice"/>,
/// following the pattern proven in <c>NFMWorld.Graphics.FNA3D.Smoke/Program.cs</c>. Everything that
/// still needs an XNA <c>GraphicsDevice</c> (<see cref="GameSparker.Load"/>, <c>Effects.Initialize</c>,
/// <see cref="RebuildCascades"/>, <see cref="ImGuiRenderer"/>, <see cref="UiRenderer"/>,
/// <see cref="NanoVGRenderer"/>, and everything downstream of <see cref="GameSparker.CurrentPhase"/>,
/// which throws until a phase is pushed) is deliberately NOT constructed/called yet - that's
/// Milestone 5 Stage B (converting <c>Scene</c>/<c>RenderQueue</c> and the renderer files to the
/// command-buffer model). This stage only proves window creation, input, resize, and device
/// lifetime against the real game entry point; no gameplay/UI renders yet.
/// </remarks>
public class WorldGame : IDisposable
{
    public static UnlimitedArray<RenderTarget2D?> ShadowRenderTargets = [];

    /// <summary>Stage A compatibility shim for <c>Mad/UI/SettingsMenu.cs</c>'s existing settings-application logic. See <see cref="GraphicsSettingsShim"/>.</summary>
    public GraphicsSettingsShim Graphics;

    /// <summary>The SDL3 window this game owns. Replaces FNA's <c>Game.Window</c>/<c>GraphicsDeviceManager</c>.</summary>
    public SdlWindow Window;

    private readonly FNA3DGraphicsDevice _device;

    public static ImGuiRenderer? ImguiRenderer;
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

    /// <summary>Replaces FNA's <c>Game.IsFixedTimeStep</c> - read by the manual loop in <see cref="Main"/>.</summary>
    public bool IsFixedTimeStep { get; set; } = false;

    /// <summary>Replaces FNA's <c>Game.TargetElapsedTime</c> - read by the manual loop in <see cref="Main"/> and by <see cref="Draw"/>'s alpha calculation.</summary>
    public TimeSpan TargetElapsedTime { get; set; } = TimeSpan.FromMilliseconds(1000 / Physics.TargetTps);

    /// <summary>All real key codes in <see cref="Key"/> (excludes the <c>KeyCode</c>/<c>Modifiers</c>/<c>Shift</c>/<c>Control</c>/<c>Alt</c> bitmask sentinels), for <see cref="UpdateInput"/>'s per-frame diff. Replaces enumerating <c>Microsoft.Xna.Framework.Input.Keys</c>.</summary>
    private static readonly Key[] AllKeys = Enum.GetValues<Key>().Where(k => (uint)k <= 0xFE).ToArray();

    private bool _disposed;

    private WorldGame()
    {
        GameThreadContext.Install();

        // Uses the raw-bitmask overload rather than SDL3.Core's SDL.SDL_WindowFlags directly: this
        // project also references FNA (transitively, via NvgSharp.FNA.Core), whose own vendored
        // SDL3 bindings define a same-named SDL type in a different assembly, and referencing
        // SDL3.Core directly here would make every "SDL" reference in this project ambiguous.
        Window = SdlWindow.Create("NFM World", 1280, 720, FNA3DInterop.PrepareWindowAttributes());
        _device = FNA3DGraphicsDevice.Create(Window.Handle, Window.Width, Window.Height, vsync: true, debugMode: true);
        Graphics = new GraphicsSettingsShim(Window);

        Window.Resized += (w, h) =>
        {
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
        // TODO(Stage B): construct ImguiRenderer/UiRenderer/NanoVGRenderer here once they (and the
        // GraphicsDevice they need) are converted to the new graphics abstraction.

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
        foreach (var shadowRenderTarget in ShadowRenderTargets)
        {
            shadowRenderTarget?.Dispose();
        }
        ImguiRenderer?.Dispose();

        _device.Dispose();
        Window.Dispose();

        // FNA's own FAudio device is cleaned up via FAudioContext.Dispose() on app domain
        // exit, and the direct-FAudio engine (FaudioEngine) lives for the process lifetime.
    }

    private void LoadContent()
    {
        // TODO(Stage C / Milestone 6): ImguiRenderer.RebuildFontAtlas(), the ImGui style block,
        // and NanoVGRenderer construction all need a live XNA GraphicsDevice/Game (ImGuiRenderer's
        // ctor takes an FNA Game, which no longer exists) or a converted 2D UI backend - none of
        // which exist yet. Left uncalled; 2D UI (menus/HUD/dev console) doesn't render yet.

        // GameSparker.NewGraphicsDevice must be set before Effects.Initialize/GameSparker.Load,
        // since both transitively construct render elements (Ground/Sky/meshes/...) that read it
        // directly (see that field's doc comment).
        GameSparker.NewGraphicsDevice = _device;
        Effects.Initialize(_device);

        // TODO(Stage C / Milestone 6): IBackend.Backend is still whatever BackendGameSparker.Load's
        // earlier server-side data load left it as (ServerBackend, which throws on client-only
        // calls like GetSound) - normally NanoVGRenderer's constructor overwrites it with the real
        // client WorldClientBackend, but that's a hard blocker (needs a live XNA GraphicsDevice/
        // NvgContext) until 2D UI is converted. DummyBackend is a safe, fully-inert stand-in (silent
        // audio, no-op 2D drawing) so GameSparker.Load's SfxLibrary.LoadSounds() and anything else
        // touching IBackend.Backend/G doesn't crash - not a real fix, just keeps this stage's scope
        // (3D rendering) from being blocked by 2D UI/audio-backend work that belongs in Stage C.
        NFMWorld.DriverInterface.DriverInterface.IBackend.Backend = new NFMWorld.DriverInterface.DriverInterface.DummyBackend();

        GameSparker.Load(this);

        SettingsMenu.LoadConfig();
    }

    public void RebuildCascades()
    {
        // TODO(Stage B): port shadow-cascade render targets to IRenderTarget. Not called yet -
        // see LoadContent.
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
        // TODO(Stage B): SdlWindow doesn't expose a polling scroll accumulator yet (only the
        // per-event MouseWheel delta) - scroll handling is wired up properly once UiRenderer exists.
        var scrollValue = _oldScrollValue;

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

    private void Draw(GameTime gameTime)
    {
        var transaction = SentrySdk.StartTransaction("GameDraw", "gameloop.draw");

        var alpha = LowLatency ? 1f : (float)((double)gameTime.ElapsedGameTime.Ticks / TargetElapsedTime.Ticks);

        var t = Stopwatch.StartNew();

        // TODO(Stage C / Milestone 6): _uiRenderer.Render()/YogaDebugger.Render()/FPSCounter.Render()/
        // _nvg.Render()/GameSparker.Render3DOverlays()/ImguiRenderer's layout+RenderImgui all need
        // either a converted 2D UI backend or ImguiRenderer, neither of which exist yet (see
        // LoadContent's TODO). 3D rendering (GameSparker.Render, below) doesn't depend on any of
        // them.
        var cb = _device.AcquireCommandBuffer();
        cb.Clear(ClearOptions.Color | ClearOptions.Depth,
            new ColorRgba(Color.CornflowerBlue.R / 255f, Color.CornflowerBlue.G / 255f, Color.CornflowerBlue.B / 255f));
        cb.SetViewport(new NFMWorld.Graphics.Viewport(0, 0, Window.Width, Window.Height));

        GameSparker.Render(cb, alpha);

        _device.Submit(cb);
        _device.Swapchain.Present();

        LastFrameTime = t.ElapsedMilliseconds;

        transaction.Finish();
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
        // FNA.dll's own internal P/Invokes (e.g. from NvgSharp.FNA.Core/Effect.cs, still compiled
        // into this exe even though Stage A doesn't exercise those paths yet) still need this too.
        // NFMWorld.Audio.FAudioBindings.FAudio's assembly registers its own resolver in
        // FaudioEngine's static ctor (a resolver can only be set once per assembly) - not
        // registered again here.
        NativeLibrary.SetDllImportResolver(typeof(Game).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(WorldGame).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(NFMWorld.FNA3D.FNA3D).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(SDL3New::SDL3.SDL).Assembly, ImportResolver);

        SettingsMenu.LoadFnaRenderer();

        var fnaLogger = Logging.LoggerFactory.CreateLogger("FNA");
        FNALoggerEXT.LogError = (message) =>
        {
            fnaLogger.LogError(message);
        };
        FNALoggerEXT.LogInfo = (message) =>
        {
            fnaLogger.LogInformation(message);
        };
        FNALoggerEXT.LogWarn = (message) =>
        {
            fnaLogger.LogWarning(message);
        };

        BackendGameSparker.Load(isHeadless: false);

        var program = new WorldGame();
        GameSparker.Game = program;
        program.Initialize();
        program.LoadContent();

        // Manual game loop replacing XNA's Game.Run(). The game's own tick pacing lives in
        // TimeStep (see _tickTimeStep), not in this loop's Update/Draw call frequency, so this
        // only needs to approximate XNA's variable-timestep behavior (IsFixedTimeStep = false is
        // WorldGame's default) plus an optional frame-rate cap when IsFixedTimeStep is set (see
        // SettingsMenu.cs's FPS limiter).
        var stopwatch = Stopwatch.StartNew();
        var lastElapsed = TimeSpan.Zero;
        while (!program.Window.ShouldQuit)
        {
            program.Window.PumpEvents();

            var now = stopwatch.Elapsed;
            var frameElapsed = now - lastElapsed;
            if (program.IsFixedTimeStep && frameElapsed < program.TargetElapsedTime)
            {
                Thread.Sleep(program.TargetElapsedTime - frameElapsed);
                now = stopwatch.Elapsed;
                frameElapsed = now - lastElapsed;
            }
            lastElapsed = now;

            var gameTime = new GameTime(now, frameElapsed);
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
