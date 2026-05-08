using System.Diagnostics;
using System.Reflection;
using Hexa.NET.ImGui;
using ManagedBass;
using Maxine.Extensions.Collections.SpanLinq;
using MonoGame.ImGuiNet.MoonWorks;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using nfm_world_library;
using nfm_world_library.util;
using nfm_world.compat;
using nfm_world.ui;
using nfm_world.ui.hud;
using nfm_world.ui.yoga;
using nfm_world.ui.yoga.xaml;
using Keys = nfm_world.util.Keys;

namespace nfm_world;

/// <summary>
/// NFM-World main game class (MoonWorks).
/// </summary>
public class WorldGame : MoonWorks.Game
{
    // D3D12, Vulkan, Metal
    public string Renderer { get; private set; }

    // FNA compat: GraphicsDeviceManager stub
#pragma warning disable CS0618
    public GraphicsDeviceManager _graphics { get; } = new();
    public bool IsFixedTimeStep { get; set; } = true;
    public TimeSpan TargetElapsedTime { get; set; } = TimeSpan.FromMilliseconds(1000d / 60);
    public bool IsActive => true;
#pragma warning restore CS0618

    /// <summary>
    /// Sets VSync on/off by changing the swapchain present mode.
    /// </summary>
    public void SetVSync(bool enabled)
    {
        var mode = enabled ? PresentMode.VSync : PresentMode.Immediate;
        GraphicsDevice.SetSwapchainParameters(MainWindow, SwapchainComposition.SDR, mode);
    }

    public static RenderTarget2D[] shadowRenderTargets { get; private set; }
    public static Texture[] shadowDepthTargets { get; private set; }
    public static Texture MainDepthTexture { get; private set; }
    private ImGuiRenderer _imguiRenderer;
    public static ImGuiRenderer ImguiRenderer { get; private set; }

    internal static int _lastFrameTime;
    internal static int _lastTickTime;
    internal static int _lastTickCount;
    private NanoVGRenderer _nvg;
    private TimeStep _tickTimeStep = new((1000f / Physics.TargetTps) / 1000f);
    public const int NumCascades = 3;

    private static bool loaded;
    private const int FrameDelay = (int) (1000 / 21.3f);

    private static bool _yogaInspectorEnabled = false;
    private static int _yogaInspectorPage = 0;
        
#if DEBUG
    internal static string? DebugUiClass;
    internal static Node? DebugUiRoot;
#endif
        
    public static ResourceUploader ResourceUploader;

    private static Keys TranslateScanCode(ScanCode scancode)
    {
        return scancode switch
        {
            ScanCode.Space => Keys.Space,
            ScanCode.D0 => Keys.D0,
            ScanCode.D1 => Keys.D1,
            ScanCode.D2 => Keys.D2,
            ScanCode.D3 => Keys.D3,
            ScanCode.D4 => Keys.D4,
            ScanCode.D5 => Keys.D5,
            ScanCode.D6 => Keys.D6,
            ScanCode.D7 => Keys.D7,
            ScanCode.D8 => Keys.D8,
            ScanCode.D9 => Keys.D9,
            ScanCode.Semicolon => Keys.OemSemicolon,
            ScanCode.A => Keys.A,
            ScanCode.B => Keys.B,
            ScanCode.C => Keys.C,
            ScanCode.D => Keys.D,
            ScanCode.E => Keys.E,
            ScanCode.F => Keys.F,
            ScanCode.G => Keys.G,
            ScanCode.H => Keys.H,
            ScanCode.I => Keys.I,
            ScanCode.J => Keys.J,
            ScanCode.K => Keys.K,
            ScanCode.L => Keys.L,
            ScanCode.M => Keys.M,
            ScanCode.N => Keys.N,
            ScanCode.O => Keys.O,
            ScanCode.P => Keys.P,
            ScanCode.Q => Keys.Q,
            ScanCode.R => Keys.R,
            ScanCode.S => Keys.S,
            ScanCode.T => Keys.T,
            ScanCode.U => Keys.U,
            ScanCode.V => Keys.V,
            ScanCode.W => Keys.W,
            ScanCode.X => Keys.X,
            ScanCode.Y => Keys.Y,
            ScanCode.Z => Keys.Z,
            ScanCode.Escape => Keys.Escape,
            ScanCode.Return => Keys.Enter,
            ScanCode.Tab => Keys.Tab,
            ScanCode.Backspace => Keys.Back,
            ScanCode.Insert => Keys.Insert,
            ScanCode.Delete => Keys.Delete,
            ScanCode.Right => Keys.Right,
            ScanCode.Left => Keys.Left,
            ScanCode.Down => Keys.Down,
            ScanCode.Up => Keys.Up,
            ScanCode.PageUp => Keys.PageUp,
            ScanCode.PageDown => Keys.PageDown,
            ScanCode.Home => Keys.Home,
            ScanCode.End => Keys.End,
            ScanCode.CapsLock => Keys.CapsLock,
            ScanCode.ScrollLock => Keys.Scroll,
            ScanCode.NumLockClear => Keys.NumLock,
            ScanCode.PrintScreen => Keys.PrintScreen,
            ScanCode.Pause => Keys.Pause,
            ScanCode.F1 => Keys.F1,
            ScanCode.F2 => Keys.F2,
            ScanCode.F3 => Keys.F3,
            ScanCode.F4 => Keys.F4,
            ScanCode.F5 => Keys.F5,
            ScanCode.F6 => Keys.F6,
            ScanCode.F7 => Keys.F7,
            ScanCode.F8 => Keys.F8,
            ScanCode.F9 => Keys.F9,
            ScanCode.F10 => Keys.F10,
            ScanCode.F11 => Keys.F11,
            ScanCode.F12 => Keys.F12,
            ScanCode.Keypad0 => Keys.NumPad0,
            ScanCode.Keypad1 => Keys.NumPad1,
            ScanCode.Keypad2 => Keys.NumPad2,
            ScanCode.Keypad3 => Keys.NumPad3,
            ScanCode.Keypad4 => Keys.NumPad4,
            ScanCode.Keypad5 => Keys.NumPad5,
            ScanCode.Keypad6 => Keys.NumPad6,
            ScanCode.Keypad7 => Keys.NumPad7,
            ScanCode.Keypad8 => Keys.NumPad8,
            ScanCode.Keypad9 => Keys.NumPad9,
            ScanCode.LeftShift => Keys.LShiftKey,
            ScanCode.LeftControl => Keys.LControlKey,
            ScanCode.LeftAlt => Keys.Alt,
            ScanCode.RightShift => Keys.RShiftKey,
            ScanCode.RightControl => Keys.RControlKey,
            ScanCode.RightAlt => Keys.Alt,
            ScanCode.KeypadMultiply => Keys.Multiply,
            ScanCode.KeypadPlus => Keys.Add,
            ScanCode.KeypadMinus => Keys.Subtract,
            ScanCode.KeypadPeriod => Keys.Decimal,
            ScanCode.KeypadDivide => Keys.Divide,
            ScanCode.Equals => Keys.Oemplus,
            ScanCode.Comma => Keys.Oemcomma,
            ScanCode.Minus => Keys.OemMinus,
            ScanCode.Period => Keys.OemPeriod,
            ScanCode.Slash => Keys.OemQuestion,
            ScanCode.Grave => Keys.Oemtilde,
            ScanCode.LeftBracket => Keys.OemOpenBrackets,
            ScanCode.Backslash => Keys.OemPipe,
            ScanCode.RightBracket => Keys.OemCloseBrackets,
            ScanCode.Apostrophe => Keys.OemQuotes,
            _ => Keys.None
        };
    }

    // Track previous keyboard/mouse state for edge detection
    private readonly HashSet<ScanCode> _prevKeysDown = new();
    private bool _prevMouseLeft;
    private bool _prevMouseRight;
    private int _prevMouseX;
    private int _prevMouseY;
    private int _prevMouseWheel;

    private WorldGame()
        : base(
            new AppInfo("NFM-World", "NFM-World"),
            new WindowCreateInfo
            {
                WindowTitle = "NFM-World",
                WindowWidth = 1280,
                WindowHeight = 720,
                ScreenMode = ScreenMode.Windowed,
                SystemResizable = true
            },
            FramePacingSettings.CreateUncapped(
                (int)Physics.TargetTps,
                150
            ),
            Environment.OSVersion.Platform switch
            {
                PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.Win32NT or PlatformID.WinCE => ShaderFormat.DXIL | ShaderFormat.SPIRV,
                PlatformID.Unix => ShaderFormat.SPIRV,
                PlatformID.Xbox => ShaderFormat.DXIL,
                PlatformID.MacOSX => ShaderFormat.MSL,
                _ => ShaderFormat.SPIRV
            },
            Environment.OSVersion.Platform switch
            {
                PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.Win32NT or PlatformID.WinCE => SettingsMenu.GetRendererFromConfig() switch
                {
                    "D3D12" => "direct3d12", 
                    "Vulkan" => "vulkan",
                    _ => null
                },
                _ => null
            },
            debugMode:
#if DEBUG
                true
#else
                false
#endif
        )
    {
        Renderer = GraphicsDevice.Backend switch
        {
            "direct3d12" => "D3D12",
            "vulkan" => "Vulkan",
            "metal" => "Metal",
            _ => "Unknown"
        };

        ResourceUploader = new ResourceUploader(GraphicsDevice);

        MainWindow.RegisterSizeChangeCallback((w, h) =>
        {
            GameSparker.WindowSizeChanged((int)w, (int)h);
            GameSparker.CurrentPhase.WindowSizeChanged((int)w, (int)h);
            G.Scale = h / 720f;
            RecreateMainDepthTexture(w, h);
        });

#if USE_BASS
        Bass.Init();
#endif

#if DEBUG
#pragma warning disable IL3050
#pragma warning disable IL2026
        XamlHotReload.Initialize();
#pragma warning restore IL2026
#pragma warning restore IL3050
#endif

        _nvg = new NanoVGRenderer(GraphicsDevice, RootTitleStorage, ResourceUploader);

        _imguiRenderer = new ImGuiRenderer(
            GraphicsDevice, ResourceUploader, MainWindow, RootTitleStorage,
            "data/shaders", TextureFormat.B8G8R8A8Unorm);
        ImguiRenderer = _imguiRenderer;

        LoadShaders();
        LoadShadowTargets();
        RecreateMainDepthTexture(MainWindow.Width, MainWindow.Height);

        GameSparker.Load(this);

        _imguiRenderer.RebuildFontAtlas();
        SetupImGuiStyle();
    }
    private void LoadShaders()
    {
        Pipelines.Initialize(GraphicsDevice, RootTitleStorage,
            MainWindow.SwapchainFormat, GraphicsDevice.SupportedDepthFormat);
    }

    private void RecreateMainDepthTexture(uint width, uint height)
    {
        MainDepthTexture?.Dispose();
        MainDepthTexture = Texture.Create2D(
            GraphicsDevice, width, height,
            GraphicsDevice.SupportedDepthFormat,
            TextureUsageFlags.DepthStencilTarget
        );
    }

    private void LoadShadowTargets()
    {
        shadowRenderTargets = new RenderTarget2D[NumCascades];
        shadowDepthTargets = new Texture[NumCascades];
        for (int i = 0; i < NumCascades; i++)
        {
            shadowRenderTargets[i] = new RenderTarget2D(
                GraphicsDevice, 2048, 2048, false,
                SurfaceFormat.Single, DepthFormat.None);
            shadowDepthTargets[i] = Texture.Create2D(
                GraphicsDevice, 2048, 2048,
                GraphicsDevice.SupportedDepthFormat,
                TextureUsageFlags.DepthStencilTarget);
        }
    }

    protected override void Step()
    {
        // Called once per accumulation iteration
    }

    protected override void Update(TimeSpan delta)
    {
        FPSCounter.Update(delta);
        
        UpdateInput();

        if (!loaded)
        {
            loaded = true;
        }

        var tick = new MicroStopwatch();
        tick.Start();

        var timesToTick = _tickTimeStep.Update(delta);
        for (int i = 0; i < timesToTick; i++)
        {
            GameSparker.CurrentPhase.BeginGameTick();
            GameSparker.GameTick();
            GameSparker.CurrentPhase.GameTick();
            GameSparker.CurrentPhase.EndGameTick();
        }
        
        GameThreadContext.Current.ExecutePendingTasks();

        _lastTickCount = timesToTick;
        _lastTickTime = (int)tick.ElapsedMicroseconds;
    }

    protected override void Destroy()
    {
        foreach (var shadowRenderTarget in shadowRenderTargets)
            shadowRenderTarget.Dispose();
        foreach (var shadowDepthTarget in shadowDepthTargets)
            shadowDepthTarget.Dispose();
        _imguiRenderer.Dispose();
    }

    private void SetupImGuiStyle()
    {
        ImGui.StyleColorsDark();

        var style = ImGui.GetStyle();
        
        // Rounding 
        style.WindowRounding = 4.0f;
        style.FrameRounding = 6.0f;
        style.GrabRounding = 4.0f;
        style.PopupRounding = 6.0f;
        style.ScrollbarRounding = 6.0f;
        style.TabRounding = 4.0f;
        
        // Spacing and padding
        style.WindowPadding = new System.Numerics.Vector2(12, 12);
        style.FramePadding = new System.Numerics.Vector2(8, 4);
        style.ItemSpacing = new System.Numerics.Vector2(8, 6);
        
        // Border
        style.WindowBorderSize = 2.0f;
        style.FrameBorderSize = 2.0f;

        var colors = style.Colors;
        
        // Windows and backgrounds
        colors[(int)ImGuiCol.WindowBg] = RGB(31, 26, 46, 0.95f);          // Dark purple
        colors[(int)ImGuiCol.ChildBg] = RGB(26, 20, 38, 0.90f);           // Darker purple
        colors[(int)ImGuiCol.PopupBg] = RGB(26, 20, 38, 0.95f);           // Darker purple
        colors[(int)ImGuiCol.MenuBarBg] = RGB(38, 31, 56, 1.0f);          // Medium purple
        
        // Borders
        colors[(int)ImGuiCol.Border] = RGB(230, 128, 26, 0.8f);           // Orange
        colors[(int)ImGuiCol.BorderShadow] = RGB(0, 0, 0, 0.5f);          // Black shadow
        
        // Text
        colors[(int)ImGuiCol.Text] = RGB(255, 191, 51, 1.0f);             // Light orange/yellow
        colors[(int)ImGuiCol.TextDisabled] = RGB(153, 115, 38, 1.0f);     // Dimmed orange
        
        // Title bar
        colors[(int)ImGuiCol.TitleBg] = RGB(38, 31, 64, 1.0f);            // Dark purple
        colors[(int)ImGuiCol.TitleBgActive] = RGB(51, 38, 89, 1.0f);      // Medium purple
        colors[(int)ImGuiCol.TitleBgCollapsed] = RGB(31, 26, 51, 0.75f);  // Very dark purple
        
        // Frames (inputs, etc)
        colors[(int)ImGuiCol.FrameBg] = RGB(38, 31, 56, 0.9f);            // Medium purple
        colors[(int)ImGuiCol.FrameBgHovered] = RGB(64, 51, 89, 1.0f);     // Lighter purple
        colors[(int)ImGuiCol.FrameBgActive] = RGB(77, 64, 102, 1.0f);     // Even lighter purple
        
        // Buttons (dark with orange on hover)
        colors[(int)ImGuiCol.Button] = RGB(38, 31, 64, 1.0f);             // Dark purple
        colors[(int)ImGuiCol.ButtonHovered] = RGB(64, 51, 89, 1.0f);      // Lighter purple
        colors[(int)ImGuiCol.ButtonActive] = RGB(128, 77, 3, 0.8f);       // Dark orange
        
        // Headers
        colors[(int)ImGuiCol.Header] = RGB(51, 38, 77, 1.0f);             // Medium purple
        colors[(int)ImGuiCol.HeaderHovered] = RGB(230, 128, 26, 0.6f);    // Orange
        colors[(int)ImGuiCol.HeaderActive] = RGB(128, 77, 3, 0.8f);       // Dark orange
        
        // Tabs
        colors[(int)ImGuiCol.Tab] = RGB(38, 31, 64, 1.0f);                     // Dark purple (inactive)
        colors[(int)ImGuiCol.TabHovered] = RGB(230, 128, 26, 0.8f);            // Orange (hovered)
        colors[(int)ImGuiCol.TabSelected] = RGB(128, 77, 3, 1.0f);           // Orange (active/selected)
        colors[(int)ImGuiCol.TabDimmed] = RGB(31, 26, 51, 1.0f);               // Very dark purple (unfocused)
        colors[(int)ImGuiCol.TabDimmedSelected] = RGB(128, 77, 26, 0.8f);      // Dimmed orange (unfocused selected)
        colors[(int)ImGuiCol.TabDimmedSelectedOverline] = RGB(230, 128, 26, 1.0f); // Orange underline
        colors[(int)ImGuiCol.TabSelectedOverline] = RGB(230, 128, 26, 1.0f);   // Orange underline (focused)
        
        // Checkmarks and sliders (orange)
        colors[(int)ImGuiCol.CheckMark] = RGB(255, 179, 51, 1.0f);        // Light orange
        colors[(int)ImGuiCol.SliderGrab] = RGB(230, 128, 26, 1.0f);       // Orange
        colors[(int)ImGuiCol.SliderGrabActive] = RGB(255, 166, 51, 1.0f); // Lighter orange
        
        // Scrollbar
        colors[(int)ImGuiCol.ScrollbarBg] = RGB(26, 20, 38, 0.9f);        // Dark purple
        colors[(int)ImGuiCol.ScrollbarGrab] = RGB(64, 51, 89, 1.0f);      // Medium purple
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = RGB(89, 71, 115, 1.0f); // Lighter purple
        colors[(int)ImGuiCol.ScrollbarGrabActive] = RGB(230, 128, 26, 1.0f); // Orange
        
        // Separators (orange)
        colors[(int)ImGuiCol.Separator] = RGB(230, 128, 26, 0.5f);        // Orange
        colors[(int)ImGuiCol.SeparatorHovered] = RGB(230, 128, 26, 0.8f); // Orange
        colors[(int)ImGuiCol.SeparatorActive] = RGB(255, 153, 51, 1.0f);  // Lighter orange
        
        // Resize grip
        colors[(int)ImGuiCol.ResizeGrip] = RGB(230, 128, 26, 0.3f);       // Orange
        colors[(int)ImGuiCol.ResizeGripHovered] = RGB(230, 128, 26, 0.6f); // Orange
        colors[(int)ImGuiCol.ResizeGripActive] = RGB(255, 153, 51, 1.0f);  // Lighter orange
        style.FrameRounding = 3.0f;
        style.WindowPadding = new System.Numerics.Vector2(10, 10);
        style.FramePadding = new System.Numerics.Vector2(5, 3);
        style.ItemSpacing = new System.Numerics.Vector2(8, 4);

        return;

        static System.Numerics.Vector4 RGB(int r, int g, int b, float a = 1.0f) => new(r / 255f, g / 255f, b / 255f, a);
    }

    private void UpdateInput()
    {
        var keyboard = Inputs.Keyboard;
        var mouse = Inputs.Mouse;

        // Keyboard — edge detection via prev state
        foreach (var scancode in Enum.GetValues<ScanCode>())
        {
            if (scancode == ScanCode.Unknown) continue;
            bool isDown = keyboard.IsHeld(scancode);
            bool wasDown = _prevKeysDown.Contains(scancode);

            if (isDown && !wasDown)
            {
                KeyDown(TranslateScanCode(scancode));
                _prevKeysDown.Add(scancode);
            }
            else if (!isDown && wasDown)
            {
                KeyUp(TranslateScanCode(scancode));
                _prevKeysDown.Remove(scancode);
            }
        }

        // Mouse buttons
        bool mouseLeft = mouse.LeftButton.IsHeld;
        if (mouseLeft && !_prevMouseLeft)
            MouseDown(mouse.X, mouse.Y);
        else if (!mouseLeft && _prevMouseLeft)
            MouseUp(mouse.X, mouse.Y);
        _prevMouseLeft = mouseLeft;

        // Mouse move
        if (mouse.X != _prevMouseX || mouse.Y != _prevMouseY)
        {
#if DEBUG
            if (_yogaInspectorEnabled)
                YogaDebugger.MouseMove(mouse.X, mouse.Y);
#endif
            GameSparker.CurrentPhase.MouseMoved(mouse.X, mouse.Y, ImGui.GetIO().WantCaptureMouse);
        }
        _prevMouseX = mouse.X;
        _prevMouseY = mouse.Y;

        // Mouse scroll (MoonWorks gives delta per frame)
        if (mouse.Wheel != 0)
        {
            GameSparker.CurrentPhase.MouseScrolled((int)(mouse.Wheel * 120), ImGui.GetIO().WantCaptureMouse);
        }
    }

    protected override void Draw(double alpha)
    {
        var renderCmd = GraphicsDevice.AcquireCommandBuffer();
        var backbuffer = renderCmd.AcquireSwapchainTexture(MainWindow);
        if (backbuffer == null)
        {
            GraphicsDevice.Submit(renderCmd);
            return;
        }

        var t = Stopwatch.StartNew();
        
#if DEBUG
        Node.__INTERNAL_YogaRootsThisFrame.Clear();
#endif
        
        // Set render context so Scene/renderers can access the command buffer
        RenderState.BeginDraw(
            renderCmd,
            backbuffer,
            MainDepthTexture
        );

        // 3D rendering (GameSparker.Render / CurrentPhase.Render)
        // Scene.Render() will create shadow + main render passes within this cmd.
        {
            GameSparker.Render();
            GameSparker.CurrentPhase.Render();
        }

#if DEBUG
        if (DebugUiClass != null)
        {
            if (DebugUiRoot == null)
            {
#pragma warning disable IL2057 // Never run during AOT compilation
#pragma warning disable IL2026 // Never run during AOT compilation
                var type = Type.GetType(DebugUiClass) ?? Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .FirstOrDefault(e => e.Name == DebugUiClass);
#pragma warning restore IL2026
#pragma warning restore IL2057
                if (type != null)
                {
#pragma warning disable IL2072 // Never run during AOT compilation
                    DebugUiRoot = Activator.CreateInstance(type) as Node;
#pragma warning restore IL2072
                }
            }

            G.SetColor(Color.CornflowerBlue);
            G.FillRect(0, 0, (int)G.Viewport.X, (int)G.Viewport.Y);
            DebugUiRoot?.LayoutAndRender(G.Viewport);
        }

        if (_yogaInspectorEnabled)
            YogaDebugger.Render(_yogaInspectorPage);
#endif

        FPSCounter.Render();

        {
            // Begin overlay render pass for 2D (NVG + ImGui).
            // Use LoadOp.Load to preserve any 3D content already rendered to backbuffer.
            var colorTarget = new ColorTargetInfo
            {
                Texture = backbuffer,
                LoadOp = LoadOp.Load,
                StoreOp = StoreOp.Store,
            };

            var renderPass = renderCmd.BeginRenderPass(colorTarget);

            // NVG flush into this render pass
            _nvg.Render(renderCmd, renderPass, MainWindow.Width, MainWindow.Height);

            GameSparker.Render3DOverlays();

            // ImGui render
            float deltaTime = (float)(1.0 / 60.0); // TODO: track actual delta
            _imguiRenderer.BeginLayout(deltaTime, Inputs, MainWindow.Width, MainWindow.Height);
            GameSparker.RenderImgui();
            _imguiRenderer.EndLayout(renderCmd, renderPass);

            renderCmd.EndRenderPass(renderPass);
        }

        // Clear render context
        RenderState.EndDraw();

        ResourceUploader.Upload();
        GraphicsDevice.Submit(renderCmd);
        
        _lastFrameTime = (int)t.ElapsedMilliseconds;
    }

    public static void Main(string[] args)
    {
#if DEBUG
        if (args.IndexOf("-debugui", StringComparer.OrdinalIgnoreCase) is var index and >= 0)
        {
            DebugUiClass = args.Length > index + 1 ? args[index + 1] : typeof(CentralTextView).FullName;
            _yogaInspectorEnabled = true;
        }
#endif
        
        BackendGameSparker.Load();

        GameThreadContext.Install();

        var program = new WorldGame();
        program.Run();
    }

    private void KeyDown(Keys key)
    {
        const bool isDown = true;
        HandleKeyPress(key, isDown);
    }

    protected void KeyUp(Keys key)
    {
        const bool isDown = false;
        HandleKeyPress(key, isDown);
    }

    private void MouseUp(int x, int y)
    {
        GameSparker.CurrentPhase.MouseReleased(x, y, ImGui.GetIO().WantCaptureMouse);
    }

    private void MouseDown(int x, int y)
    {
        GameSparker.CurrentPhase.MousePressed(x, y, ImGui.GetIO().WantCaptureMouse);
    }

    private void HandleKeyPress(Keys key, bool isDown)
    {
        if (isDown)
        {
            GameSparker.KeyPressed(key);
            GameSparker.CurrentPhase.KeyPressed(key, ImGui.GetIO().WantCaptureKeyboard);

#if DEBUG
            if (key == Keys.F9)
            {
                _yogaInspectorEnabled = !_yogaInspectorEnabled;
            }

            if (key == Keys.F10)
            {
                _yogaInspectorPage++;
                if (_yogaInspectorPage > YogaDebugger.MaxPages)
                    _yogaInspectorPage = 0;
            }
#endif
        }
        else
        {
            GameSparker.KeyReleased(key);
            GameSparker.CurrentPhase.KeyReleased(key, ImGui.GetIO().WantCaptureKeyboard);
        }
    }
}