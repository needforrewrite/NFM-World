using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Opus;
using Maxine.Extensions;
using Maxine.Extensions.Mathematics;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.ImGuiNet;
using NFMWorld.CrashReporter;
using NFMWorld.DriverInterface;
using NFMWorld.UI;
using NFMWorld.UI.Cef;
using NFMWorld.UI.Hud;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Util;
using Sokol;
using Keys = NFMWorld.DriverInterface.Keys;
using Logging = NFMWorldLibrary.Logging;
using NFMWorld.Sentry;

namespace NFMWorld;

/// <summary>
/// This sample demonstrates how to load a Direct2D1 bitmap from a file.
/// This method will be part of a future version of SharpDX API.
/// </summary>
public class WorldGame : Game
{
    public GraphicsDeviceManager Graphics;
    public static RenderTarget2D?[] ShadowRenderTargets { get; } = new RenderTarget2D[3];
    private ImGuiRenderer _imguiRenderer;
    public static ImGuiRenderer ImguiRenderer { get; private set; }
    private CefRenderer _cefRenderer;

    internal static long LastFrameTime;
    internal static long LastTickTime;
    internal static int LastTickCount;
    private Keys _oldKeyState;
    private MouseButtons _oldMouseState;
    private Int2 _oldMousePosition;
    private int _oldScrollValue;
    private NanoVGRenderer _nvg;
    private TimeStep _tickTimeStep = new((1000f / Physics.TargetTps) / 1000f);
    public static bool LowLatency = false;
    public static int NumCascades = 3;
    public static int ShadowResolution = 2048;

    private static bool _loaded;
    private const int FrameDelay = (int) (1000 / 21.3f);

    private static readonly Microsoft.Xna.Framework.Input.Keys[] XnaKeys = Enum.GetValues<Microsoft.Xna.Framework.Input.Keys>();

    private WorldGame()
    {
        GameThreadContext.Install();

        Graphics = new GraphicsDeviceManager(this);
        Graphics.GraphicsProfile = GraphicsProfile.Reach;
        Graphics.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        Graphics.SynchronizeWithVerticalRetrace = true;
        IsFixedTimeStep = false;
        TargetElapsedTime = TimeSpan.FromMilliseconds(1000 / Physics.TargetTps);
        Graphics.PreferredBackBufferWidth = 1280;
        Graphics.PreferredBackBufferHeight = 720;
        Graphics.PreferMultiSampling = true;

        // IBackend.Backend = new DummyBackend();
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (sender, args) =>
        {
            var viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
            GraphicsDevice.Viewport = viewport;
            // _skia.RemakeRenderTarget(Window.ClientBounds.Width, Window.ClientBounds.Height);
            GameSparker.WindowSizeChanged(Window.ClientBounds.Width, Window.ClientBounds.Height);
            GameSparker.CurrentPhase.WindowSizeChanged(Window.ClientBounds.Width, Window.ClientBounds.Height);
            G.Scale = Window.ClientBounds.Height / 720f;
            _cefRenderer?.Resize(Window.ClientBounds.Width, Window.ClientBounds.Height);
        };

        TextInputEXT.TextInput += character =>
        {
            GameSparker.CurrentPhase.KeyTyped(character, ImGui.GetIO().WantCaptureKeyboard);
        };
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        FPSCounter.Update(gameTime, LastTickTime, LastFrameTime);

        UpdateInput();
        UpdateMouse();

        _cefRenderer.Update(gameTime);

        if (!_loaded)
        {
            _loaded = true;
        }

        var timesToTick = _tickTimeStep.Update(gameTime);
        if (timesToTick > 0)
        {
            LastTickTime = 0;

            for (int i = 0; i < timesToTick; i++)
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
            GameThreadContext.Current.ExecutePendingTasks();
            transaction.Finish();
        }

        // Dispose any phases that were popped/replaced this frame.
        // Must happen after all game logic to avoid disposal during event handlers.
        GameSparker.Phases.FlushDisposals();
    }

    protected override void Initialize()
    {
        _imguiRenderer = new ImGuiRenderer(this);
        ImguiRenderer = _imguiRenderer;

        // Initialize CEF renderer after GraphicsDevice is ready.
        // Load the single-page app (hash router) as the initial URL.
        // Phase bridges use ExecuteJavaScript to change the hash on enter.
        var baseUrl = CefRenderer.ResolveBasePageUrl();
        _cefRenderer = new CefRenderer(this, baseUrl);
        _cefRenderer.Initialize();
        GameSparker.CefRenderer = _cefRenderer;

#if USE_BASS
        Bass.Init();
#endif
#if USE_FAUDIO
        // FAudio is lazily initialized by FNA's SoundEffect on first use.
        // No explicit init needed.
#endif

        _oldKeyState = Keys.FromState(Keyboard.GetState());
        var mouseState = Mouse.GetState();
        _oldMouseState = MouseButtons.FromState(mouseState);
        _oldMousePosition = new Int2(mouseState.X, mouseState.Y);
        _oldScrollValue = mouseState.ScrollWheelValue;

        _nvg = new NanoVGRenderer(GraphicsDevice);

        // MSAA is set by SettingsMenu.LoadConfig -> ApplySettings at startup.
        // Fallback: ensure MSAA is at least enabled if no config file exists.
        if (GraphicsDevice.PresentationParameters.MultiSampleCount == 0)
        {
            GraphicsDevice.PresentationParameters.MultiSampleCount = 8;
            Graphics.ApplyChanges();
        }

        base.Initialize();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Dispose all phases before tearing down CEF and graphics.
            GameSparker.Phases.Shutdown();

            _cefRenderer?.Dispose();
            foreach (var shadowRenderTarget in ShadowRenderTargets)
            {
                shadowRenderTarget?.Dispose();
            }
            _imguiRenderer.Dispose();

#if USE_BASS
            Bass.Free();
#endif
#if USE_FAUDIO
            // FAudio is managed by FNA and cleaned up via FAudioContext.Dispose()
            // on app domain exit. No explicit free needed.
#endif
        }
    }

    protected override void LoadContent()
    {
        GameSparker.Load(this);

        _imguiRenderer.RebuildFontAtlas();

        Effects.Initialize(GraphicsDevice);

        RebuildCascades();

        SettingsMenu.LoadConfig();

        #region Imgui

        // Initialize ImGui
        ImGui.CreateContext();
        ImGui.StyleColorsDark();


        // custom style
        var style = ImGui.GetStyle();

        // Rounding
        style.WindowRounding = 4.0f;
        style.FrameRounding = 6.0f;
        style.GrabRounding = 4.0f;
        style.PopupRounding = 6.0f;
        style.ScrollbarRounding = 6.0f;
        style.TabRounding = 4.0f;

        // Spacing and padding
        style.WindowPadding = new Vector2(12, 12);
        style.FramePadding = new Vector2(8, 4);
        style.ItemSpacing = new Vector2(8, 6);

        // Border
        style.WindowBorderSize = 2.0f;
        style.FrameBorderSize = 2.0f;

        var colors = style.Colors;

        // Windows and backgrounds
        colors[(int)ImGuiCol.WindowBg] = Rgb(31, 26, 46, 0.95f);          // Dark purple
        colors[(int)ImGuiCol.ChildBg] = Rgb(26, 20, 38, 0.90f);           // Darker purple
        colors[(int)ImGuiCol.PopupBg] = Rgb(26, 20, 38, 0.95f);           // Darker purple
        colors[(int)ImGuiCol.MenuBarBg] = Rgb(38, 31, 56, 1.0f);          // Medium purple

        // Borders
        colors[(int)ImGuiCol.Border] = Rgb(230, 128, 26, 0.8f);           // Orange
        colors[(int)ImGuiCol.BorderShadow] = Rgb(0, 0, 0, 0.5f);          // Black shadow

        // Text
        colors[(int)ImGuiCol.Text] = Rgb(255, 191, 51, 1.0f);             // Light orange/yellow
        colors[(int)ImGuiCol.TextDisabled] = Rgb(153, 115, 38, 1.0f);     // Dimmed orange

        // Title bar
        colors[(int)ImGuiCol.TitleBg] = Rgb(38, 31, 64, 1.0f);            // Dark purple
        colors[(int)ImGuiCol.TitleBgActive] = Rgb(51, 38, 89, 1.0f);      // Medium purple
        colors[(int)ImGuiCol.TitleBgCollapsed] = Rgb(31, 26, 51, 0.75f);  // Very dark purple

        // Frames (inputs, etc)
        colors[(int)ImGuiCol.FrameBg] = Rgb(38, 31, 56, 0.9f);            // Medium purple
        colors[(int)ImGuiCol.FrameBgHovered] = Rgb(64, 51, 89, 1.0f);     // Lighter purple
        colors[(int)ImGuiCol.FrameBgActive] = Rgb(77, 64, 102, 1.0f);     // Even lighter purple

        // Buttons (dark with orange on hover)
        colors[(int)ImGuiCol.Button] = Rgb(38, 31, 64, 1.0f);             // Dark purple
        colors[(int)ImGuiCol.ButtonHovered] = Rgb(64, 51, 89, 1.0f);      // Lighter purple
        colors[(int)ImGuiCol.ButtonActive] = Rgb(128, 77, 3, 0.8f);       // Dark orange

        // Headers
        colors[(int)ImGuiCol.Header] = Rgb(51, 38, 77, 1.0f);             // Medium purple
        colors[(int)ImGuiCol.HeaderHovered] = Rgb(230, 128, 26, 0.6f);    // Orange
        colors[(int)ImGuiCol.HeaderActive] = Rgb(128, 77, 3, 0.8f);       // Dark orange

        // Tabs
        colors[(int)ImGuiCol.Tab] = Rgb(38, 31, 64, 1.0f);                     // Dark purple (inactive)
        colors[(int)ImGuiCol.TabHovered] = Rgb(230, 128, 26, 0.8f);            // Orange (hovered)
        colors[(int)ImGuiCol.TabSelected] = Rgb(128, 77, 3, 1.0f);           // Orange (active/selected)
        colors[(int)ImGuiCol.TabDimmed] = Rgb(31, 26, 51, 1.0f);               // Very dark purple (unfocused)
        colors[(int)ImGuiCol.TabDimmedSelected] = Rgb(128, 77, 26, 0.8f);      // Dimmed orange (unfocused selected)
        colors[(int)ImGuiCol.TabDimmedSelectedOverline] = Rgb(230, 128, 26, 1.0f); // Orange underline
        colors[(int)ImGuiCol.TabSelectedOverline] = Rgb(230, 128, 26, 1.0f);   // Orange underline (focused)

        // Checkmarks and sliders (orange)
        colors[(int)ImGuiCol.CheckMark] = Rgb(255, 179, 51, 1.0f);        // Light orange
        colors[(int)ImGuiCol.SliderGrab] = Rgb(230, 128, 26, 1.0f);       // Orange
        colors[(int)ImGuiCol.SliderGrabActive] = Rgb(255, 166, 51, 1.0f); // Lighter orange

        // Scrollbar
        colors[(int)ImGuiCol.ScrollbarBg] = Rgb(26, 20, 38, 0.9f);        // Dark purple
        colors[(int)ImGuiCol.ScrollbarGrab] = Rgb(64, 51, 89, 1.0f);      // Medium purple
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = Rgb(89, 71, 115, 1.0f); // Lighter purple
        colors[(int)ImGuiCol.ScrollbarGrabActive] = Rgb(230, 128, 26, 1.0f); // Orange

        // Separators (orange)
        colors[(int)ImGuiCol.Separator] = Rgb(230, 128, 26, 0.5f);        // Orange
        colors[(int)ImGuiCol.SeparatorHovered] = Rgb(230, 128, 26, 0.8f); // Orange
        colors[(int)ImGuiCol.SeparatorActive] = Rgb(255, 153, 51, 1.0f);  // Lighter orange

        // Resize grip
        colors[(int)ImGuiCol.ResizeGrip] = Rgb(230, 128, 26, 0.3f);       // Orange
        colors[(int)ImGuiCol.ResizeGripHovered] = Rgb(230, 128, 26, 0.6f); // Orange
        colors[(int)ImGuiCol.ResizeGripActive] = Rgb(255, 153, 51, 1.0f);  // Lighter orange
        style.FrameRounding = 3.0f;
        style.WindowPadding = new Vector2(10, 10);
        style.FramePadding = new Vector2(5, 3);
        style.ItemSpacing = new Vector2(8, 4);

        #endregion

        return;

        static Vector4 Rgb(int r, int g, int b, float a = 1.0f) => new(r / 255f, g / 255f, b / 255f, a);
    }

    public void RebuildCascades()
    {
        foreach (var shadowRenderTarget in ShadowRenderTargets)
        {
            shadowRenderTarget?.Dispose();
        }

        // Create floating point render target
        for (int i = NumCascades - 1; i >= 0; i--)
        {
            ShadowRenderTargets[i] = new RenderTarget2D(
                GraphicsDevice,
                ShadowResolution,
                ShadowResolution,
                false,
                SurfaceFormat.Single,
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.DiscardContents);
        }

        // Clear all render targets AFTER creating them all
        for (int i = 0; i < NumCascades; i++)
        {
            GraphicsDevice.SetRenderTarget(ShadowRenderTargets[i]);
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1.0f, 0);
            GraphicsDevice.SetRenderTarget(null);
        }
    }

    private void UpdateInput()
    {
        var newState = Keyboard.GetState();

        var keys = Keys.FromState(newState);

        foreach (var xnaKey in XnaKeys)
        {
            var nfmKey = Key.FromXna(xnaKey);
            if (keys[nfmKey] && !_oldKeyState[nfmKey])
            {
                GameSparker.KeyPressed(nfmKey);
                GameSparker.CurrentPhase.KeyPressed(nfmKey, ImGui.GetIO().WantCaptureKeyboard, keys);
            }
            else if (!keys[nfmKey] && _oldKeyState[nfmKey])
            {
                GameSparker.KeyReleased(nfmKey);
                GameSparker.CurrentPhase.KeyReleased(nfmKey, ImGui.GetIO().WantCaptureKeyboard, keys);
            }
        }

        // Update saved state.
        _oldKeyState = keys;
    }

    private static readonly MouseButtons[] MouseButtonsArray = Enum.GetValues<MouseButtons>();
    private void UpdateMouse()
    {
        var newState = Mouse.GetState();
        var buttons = MouseButtons.FromState(newState);
        var mousePosition = new Int2(newState.X, newState.Y);
        var scrollValue = newState.ScrollWheelValue;

        var ctrlKey = _oldKeyState[Key.LControlKey] || _oldKeyState[Key.RControlKey];
        var shiftKey = _oldKeyState[Key.LShiftKey] || _oldKeyState[Key.RShiftKey];
        var altKey = _oldKeyState[Key.Alt];
        var wantCaptureMouse = ImGui.GetIO().WantCaptureMouse;

        foreach (var button in MouseButtonsArray)
        {
            if (buttons.HasFlag(button) && !_oldMouseState.HasFlag(button))
            {
                GameSparker.CurrentPhase.MousePressed(newState.X, newState.Y, wantCaptureMouse, MouseButton.Primary, buttons, ctrlKey, shiftKey, altKey);
            }
            else if (!buttons.HasFlag(button) && _oldMouseState.HasFlag(button))
            {
                GameSparker.CurrentPhase.MouseReleased(newState.X, newState.Y, wantCaptureMouse, MouseButton.Primary, buttons, ctrlKey, shiftKey, altKey);
            }
        }

        if (mousePosition.X != _oldMousePosition.X || mousePosition.Y != _oldMousePosition.Y)
        {
            GameSparker.CurrentPhase.MouseMoved(mousePosition.X, mousePosition.Y, wantCaptureMouse, buttons, ctrlKey, shiftKey, altKey);
        }

        if (scrollValue != _oldScrollValue)
        {
            var delta = scrollValue - _oldScrollValue;
            GameSparker.CurrentPhase.MouseScrolled(mousePosition.X, mousePosition.Y, delta, wantCaptureMouse, buttons, ctrlKey, shiftKey, altKey);
        }

        _oldMouseState = buttons;
        _oldMousePosition = mousePosition;
        _oldScrollValue = scrollValue;
    }

    protected override void Draw(GameTime gameTime)
    {
        var transaction = SentrySdk.StartTransaction("GameDraw", "gameloop.draw");

        var alpha = LowLatency ? 1f : (float)((double)gameTime.ElapsedGameTime.Ticks / TargetElapsedTime.Ticks);

        GraphicsDevice.Clear(Color.CornflowerBlue);

        var t = Stopwatch.StartNew();

        GameSparker.Render();

        // Render based on game state
        GameSparker.CurrentPhase.Render(alpha);

        FPSCounter.Render();
        _nvg.Render();

        // Render CEF browser overlay (between NanoVG and ImGui)
        _cefRenderer.Render();

        GameSparker.Render3DOverlays();

        // // Render ImGui
        _imguiRenderer.BeginLayout(gameTime);
        GameSparker.RenderImgui();
        _imguiRenderer.EndLayout();

        base.Draw(gameTime);
        LastFrameTime = t.ElapsedMilliseconds;

        transaction.Finish();
    }

    public static void Main(string[] args)
    {
        ClientServer.IsRunningOnClient = true;

        // TODO figure out why SDL ProcessExit doesn't work properly
        AppDomain.CurrentDomain.ProcessExit += static (sender, args) =>
        {
            Process.GetCurrentProcess().Kill(false);
        };

        NativeLibrary.SetDllImportResolver(typeof(Game).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(WorldGame).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(Bass).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(BassFx).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(BassOpus).Assembly, ImportResolver);
        NativeLibrary.SetDllImportResolver(typeof(SokolExtensions).Assembly, ImportResolver);

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
        program.Run();
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

        if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
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
            "bass" => os switch
            {
                "windows" => "bass.dll",
                "osx" => "libbass.dylib",
                "linux" or "freebsd" or "netbsd" => "libbass.so",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "bass_fx" => os switch
            {
                "windows" => "bass_fx.dll",
                "osx" => "libbass_fx.dylib",
                "linux" or "freebsd" or "netbsd" => "libbass_fx.so",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "bassopus" => os switch
            {
                "windows" => "bassopus.dll",
                "osx" => "libbassopus.dylib",
                "linux" or "freebsd" or "netbsd" => "libbassopus.so",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "steam_api" or "steam_api64" => os switch
            {
                "windows" => wordsize is "64" ? "steam_api64.dll" : "steam_api.dll",
                "osx" => "libsteam_api.dylib",
                "linux" or "freebsd" or "netbsd" => "libsteam_api.so",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            },
            "nanosvg" => os switch
            {
                "windows" => "nanosvg.dll",
                "osx" => "libnanosvg.dylib",
                "linux" or "freebsd" or "netbsd" => "libnanosvg.so",
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

        return NativeLibrary.Load($"libs/{dir}/{newLibraryName}");
    }
}
