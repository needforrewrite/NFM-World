using System.Collections.Frozen;
using ManagedBass;
using Color = NFMWorld.Util.Color;
using Font = NFMWorld.Util.Font;
using File = NFMWorld.Util.File;
using Keys = NFMWorld.Util.Keys;
using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Mad;
using System.Diagnostics;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.ImGuiNet;
using Environment = System.Environment;

namespace NFMWorld;

/// <summary>
/// This sample demonstrates how to load a Direct2D1 bitmap from a file.
/// This method will be part of a future version of SharpDX API.
/// </summary>
public unsafe class Program : Game
{
    private GraphicsDeviceManager _graphics;
    public static SpriteBatch _spriteBatch { get; private set; }
    public static Effect _polyShader { get; private set; }
    public static Effect _skyShader { get; private set; }
    public static Effect _groundShader { get; private set; }
    public static Effect _mountainsShader { get; private set; }
    public static RenderTarget2D[] shadowRenderTargets { get; private set; }
    private ImGuiRenderer _imguiRenderer;

    internal static int _lastFrameTime;
    internal static int _lastTickTime;
    private KeyboardState oldKeyState;
    private MouseState oldMouseState;
    private NanoVGRenderer _nvg;
    public const int NumCascades = 3;

    private static bool loaded;
    private const int FrameDelay = (int) (1000 / 21.3f);
    
    private static readonly Microsoft.Xna.Framework.Input.Keys[] XnaKeys = Enum.GetValues<Microsoft.Xna.Framework.Input.Keys>();

    private static Keys TranslateKey(Microsoft.Xna.Framework.Input.Keys key)
    {
        return key switch
        {
            Microsoft.Xna.Framework.Input.Keys.Space => Keys.Space,
            Microsoft.Xna.Framework.Input.Keys.D0 => Keys.D0,
            Microsoft.Xna.Framework.Input.Keys.D1 => Keys.D1,
            Microsoft.Xna.Framework.Input.Keys.D2 => Keys.D2,
            Microsoft.Xna.Framework.Input.Keys.D3 => Keys.D3,
            Microsoft.Xna.Framework.Input.Keys.D4 => Keys.D4,
            Microsoft.Xna.Framework.Input.Keys.D5 => Keys.D5,
            Microsoft.Xna.Framework.Input.Keys.D6 => Keys.D6,
            Microsoft.Xna.Framework.Input.Keys.D7 => Keys.D7,
            Microsoft.Xna.Framework.Input.Keys.D8 => Keys.D8,
            Microsoft.Xna.Framework.Input.Keys.D9 => Keys.D9,
            Microsoft.Xna.Framework.Input.Keys.OemSemicolon => Keys.OemSemicolon,
            Microsoft.Xna.Framework.Input.Keys.A => Keys.A,
            Microsoft.Xna.Framework.Input.Keys.B => Keys.B,
            Microsoft.Xna.Framework.Input.Keys.C => Keys.C,
            Microsoft.Xna.Framework.Input.Keys.D => Keys.D,
            Microsoft.Xna.Framework.Input.Keys.E => Keys.E,
            Microsoft.Xna.Framework.Input.Keys.F => Keys.F,
            Microsoft.Xna.Framework.Input.Keys.G => Keys.G,
            Microsoft.Xna.Framework.Input.Keys.H => Keys.H,
            Microsoft.Xna.Framework.Input.Keys.I => Keys.I,
            Microsoft.Xna.Framework.Input.Keys.J => Keys.J,
            Microsoft.Xna.Framework.Input.Keys.K => Keys.K,
            Microsoft.Xna.Framework.Input.Keys.L => Keys.L,
            Microsoft.Xna.Framework.Input.Keys.M => Keys.M,
            Microsoft.Xna.Framework.Input.Keys.N => Keys.N,
            Microsoft.Xna.Framework.Input.Keys.O => Keys.O,
            Microsoft.Xna.Framework.Input.Keys.P => Keys.P,
            Microsoft.Xna.Framework.Input.Keys.Q => Keys.Q,
            Microsoft.Xna.Framework.Input.Keys.R => Keys.R,
            Microsoft.Xna.Framework.Input.Keys.S => Keys.S,
            Microsoft.Xna.Framework.Input.Keys.T => Keys.T,
            Microsoft.Xna.Framework.Input.Keys.U => Keys.U,
            Microsoft.Xna.Framework.Input.Keys.V => Keys.V,
            Microsoft.Xna.Framework.Input.Keys.W => Keys.W,
            Microsoft.Xna.Framework.Input.Keys.X => Keys.X,
            Microsoft.Xna.Framework.Input.Keys.Y => Keys.Y,
            Microsoft.Xna.Framework.Input.Keys.Z => Keys.Z,
            Microsoft.Xna.Framework.Input.Keys.Escape => Keys.Escape,
            Microsoft.Xna.Framework.Input.Keys.Enter => Keys.Enter,
            Microsoft.Xna.Framework.Input.Keys.Tab => Keys.Tab,
            Microsoft.Xna.Framework.Input.Keys.Back => Keys.Back,
            Microsoft.Xna.Framework.Input.Keys.Insert => Keys.Insert,
            Microsoft.Xna.Framework.Input.Keys.Delete => Keys.Delete,
            Microsoft.Xna.Framework.Input.Keys.Right => Keys.Right,
            Microsoft.Xna.Framework.Input.Keys.Left => Keys.Left,
            Microsoft.Xna.Framework.Input.Keys.Down => Keys.Down,
            Microsoft.Xna.Framework.Input.Keys.Up => Keys.Up,
            Microsoft.Xna.Framework.Input.Keys.PageUp => Keys.PageUp,
            Microsoft.Xna.Framework.Input.Keys.PageDown => Keys.PageDown,
            Microsoft.Xna.Framework.Input.Keys.Home => Keys.Home,
            Microsoft.Xna.Framework.Input.Keys.End => Keys.End,
            Microsoft.Xna.Framework.Input.Keys.CapsLock => Keys.CapsLock,
            Microsoft.Xna.Framework.Input.Keys.Scroll => Keys.Scroll,
            Microsoft.Xna.Framework.Input.Keys.NumLock => Keys.NumLock,
            Microsoft.Xna.Framework.Input.Keys.PrintScreen => Keys.PrintScreen,
            Microsoft.Xna.Framework.Input.Keys.Pause => Keys.Pause,
            Microsoft.Xna.Framework.Input.Keys.F1 => Keys.F1,
            Microsoft.Xna.Framework.Input.Keys.F2 => Keys.F2,
            Microsoft.Xna.Framework.Input.Keys.F3 => Keys.F3,
            Microsoft.Xna.Framework.Input.Keys.F4 => Keys.F4,
            Microsoft.Xna.Framework.Input.Keys.F5 => Keys.F5,
            Microsoft.Xna.Framework.Input.Keys.F6 => Keys.F6,
            Microsoft.Xna.Framework.Input.Keys.F7 => Keys.F7,
            Microsoft.Xna.Framework.Input.Keys.F8 => Keys.F8,
            Microsoft.Xna.Framework.Input.Keys.F9 => Keys.F9,
            Microsoft.Xna.Framework.Input.Keys.F10 => Keys.F10,
            Microsoft.Xna.Framework.Input.Keys.F11 => Keys.F11,
            Microsoft.Xna.Framework.Input.Keys.F12 => Keys.F12,
            Microsoft.Xna.Framework.Input.Keys.F13 => Keys.F13,
            Microsoft.Xna.Framework.Input.Keys.F14 => Keys.F14,
            Microsoft.Xna.Framework.Input.Keys.F15 => Keys.F15,
            Microsoft.Xna.Framework.Input.Keys.F16 => Keys.F16,
            Microsoft.Xna.Framework.Input.Keys.F17 => Keys.F17,
            Microsoft.Xna.Framework.Input.Keys.F18 => Keys.F18,
            Microsoft.Xna.Framework.Input.Keys.F19 => Keys.F19,
            Microsoft.Xna.Framework.Input.Keys.F20 => Keys.F20,
            Microsoft.Xna.Framework.Input.Keys.F21 => Keys.F21,
            Microsoft.Xna.Framework.Input.Keys.F22 => Keys.F22,
            Microsoft.Xna.Framework.Input.Keys.F23 => Keys.F23,
            Microsoft.Xna.Framework.Input.Keys.F24 => Keys.F24,
            Microsoft.Xna.Framework.Input.Keys.NumPad0 => Keys.NumPad0,
            Microsoft.Xna.Framework.Input.Keys.NumPad1 => Keys.NumPad1,
            Microsoft.Xna.Framework.Input.Keys.NumPad2 => Keys.NumPad2,
            Microsoft.Xna.Framework.Input.Keys.NumPad3 => Keys.NumPad3,
            Microsoft.Xna.Framework.Input.Keys.NumPad4 => Keys.NumPad4,
            Microsoft.Xna.Framework.Input.Keys.NumPad5 => Keys.NumPad5,
            Microsoft.Xna.Framework.Input.Keys.NumPad6 => Keys.NumPad6,
            Microsoft.Xna.Framework.Input.Keys.NumPad7 => Keys.NumPad7,
            Microsoft.Xna.Framework.Input.Keys.NumPad8 => Keys.NumPad8,
            Microsoft.Xna.Framework.Input.Keys.NumPad9 => Keys.NumPad9,
            Microsoft.Xna.Framework.Input.Keys.LeftShift => Keys.LShiftKey,
            Microsoft.Xna.Framework.Input.Keys.LeftControl => Keys.LControlKey,
            Microsoft.Xna.Framework.Input.Keys.LeftAlt => Keys.Alt,
            Microsoft.Xna.Framework.Input.Keys.RightShift => Keys.RShiftKey,
            Microsoft.Xna.Framework.Input.Keys.RightControl => Keys.RControlKey,
            Microsoft.Xna.Framework.Input.Keys.RightAlt => Keys.Alt,
            Microsoft.Xna.Framework.Input.Keys.Select => Keys.Select,
            Microsoft.Xna.Framework.Input.Keys.Print => Keys.Print,
            Microsoft.Xna.Framework.Input.Keys.Execute => Keys.Execute,
            Microsoft.Xna.Framework.Input.Keys.Help => Keys.Help,
            Microsoft.Xna.Framework.Input.Keys.LeftWindows => Keys.LWin,
            Microsoft.Xna.Framework.Input.Keys.RightWindows => Keys.RWin,
            Microsoft.Xna.Framework.Input.Keys.Apps => Keys.Apps,
            Microsoft.Xna.Framework.Input.Keys.Sleep => Keys.Sleep,
            Microsoft.Xna.Framework.Input.Keys.Multiply => Keys.Multiply,
            Microsoft.Xna.Framework.Input.Keys.Add => Keys.Add,
            Microsoft.Xna.Framework.Input.Keys.Separator => Keys.Separator,
            Microsoft.Xna.Framework.Input.Keys.Subtract => Keys.Subtract,
            Microsoft.Xna.Framework.Input.Keys.Decimal => Keys.Decimal,
            Microsoft.Xna.Framework.Input.Keys.Divide => Keys.Divide,
            Microsoft.Xna.Framework.Input.Keys.BrowserBack => Keys.BrowserBack,
            Microsoft.Xna.Framework.Input.Keys.BrowserForward => Keys.BrowserForward,
            Microsoft.Xna.Framework.Input.Keys.BrowserRefresh => Keys.BrowserRefresh,
            Microsoft.Xna.Framework.Input.Keys.BrowserStop => Keys.BrowserStop,
            Microsoft.Xna.Framework.Input.Keys.BrowserSearch => Keys.BrowserSearch,
            Microsoft.Xna.Framework.Input.Keys.BrowserFavorites => Keys.BrowserFavorites,
            Microsoft.Xna.Framework.Input.Keys.BrowserHome => Keys.BrowserHome,
            Microsoft.Xna.Framework.Input.Keys.VolumeMute => Keys.VolumeMute,
            Microsoft.Xna.Framework.Input.Keys.VolumeDown => Keys.VolumeDown,
            Microsoft.Xna.Framework.Input.Keys.VolumeUp => Keys.VolumeUp,
            Microsoft.Xna.Framework.Input.Keys.MediaNextTrack => Keys.MediaNextTrack,
            Microsoft.Xna.Framework.Input.Keys.MediaPreviousTrack => Keys.MediaPreviousTrack,
            Microsoft.Xna.Framework.Input.Keys.MediaStop => Keys.MediaStop,
            Microsoft.Xna.Framework.Input.Keys.MediaPlayPause => Keys.MediaPlayPause,
            Microsoft.Xna.Framework.Input.Keys.LaunchMail => Keys.LaunchMail,
            Microsoft.Xna.Framework.Input.Keys.SelectMedia => Keys.SelectMedia,
            Microsoft.Xna.Framework.Input.Keys.LaunchApplication1 => Keys.LaunchApplication1,
            Microsoft.Xna.Framework.Input.Keys.LaunchApplication2 => Keys.LaunchApplication2,
            Microsoft.Xna.Framework.Input.Keys.OemPlus => Keys.Oemplus,
            Microsoft.Xna.Framework.Input.Keys.OemComma => Keys.Oemcomma,
            Microsoft.Xna.Framework.Input.Keys.OemMinus => Keys.OemMinus,
            Microsoft.Xna.Framework.Input.Keys.OemPeriod => Keys.OemPeriod,
            Microsoft.Xna.Framework.Input.Keys.OemQuestion => Keys.OemQuestion,
            Microsoft.Xna.Framework.Input.Keys.OemTilde => Keys.Oemtilde,
            Microsoft.Xna.Framework.Input.Keys.OemOpenBrackets => Keys.OemOpenBrackets,
            Microsoft.Xna.Framework.Input.Keys.OemPipe => Keys.OemPipe,
            Microsoft.Xna.Framework.Input.Keys.OemCloseBrackets => Keys.OemCloseBrackets,
            Microsoft.Xna.Framework.Input.Keys.OemQuotes => Keys.OemQuotes,
            Microsoft.Xna.Framework.Input.Keys.Oem8 => Keys.Oem8,
            Microsoft.Xna.Framework.Input.Keys.OemBackslash => Keys.OemBackslash,
            Microsoft.Xna.Framework.Input.Keys.ProcessKey => Keys.ProcessKey,
            Microsoft.Xna.Framework.Input.Keys.Attn => Keys.Attn,
            Microsoft.Xna.Framework.Input.Keys.Crsel => Keys.Crsel,
            Microsoft.Xna.Framework.Input.Keys.Exsel => Keys.Exsel,
            Microsoft.Xna.Framework.Input.Keys.EraseEof => Keys.EraseEof,
            Microsoft.Xna.Framework.Input.Keys.Play => Keys.Play,
            Microsoft.Xna.Framework.Input.Keys.Zoom => Keys.Zoom,
            Microsoft.Xna.Framework.Input.Keys.Pa1 => Keys.Pa1,
            Microsoft.Xna.Framework.Input.Keys.OemClear => Keys.OemClear,
            Microsoft.Xna.Framework.Input.Keys.ChatPadGreen => Keys.None,
            Microsoft.Xna.Framework.Input.Keys.ChatPadOrange => Keys.None,
            Microsoft.Xna.Framework.Input.Keys.ImeConvert => Keys.IMEConvert,
            Microsoft.Xna.Framework.Input.Keys.ImeNoConvert => Keys.IMENonconvert,
            Microsoft.Xna.Framework.Input.Keys.Kana => Keys.KanaMode,
            Microsoft.Xna.Framework.Input.Keys.Kanji => Keys.KanjiMode,
            Microsoft.Xna.Framework.Input.Keys.OemAuto => Keys.None,
            Microsoft.Xna.Framework.Input.Keys.OemCopy => Keys.None,
            Microsoft.Xna.Framework.Input.Keys.OemEnlW => Keys.None,
            Microsoft.Xna.Framework.Input.Keys.None => Keys.None,
            _ => Keys.None
        };
    }

    private Program()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromMilliseconds(1000 / 63f);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.PreferMultiSampling = false;

        // IBackend.Backend = new DummyBackend();
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (sender, args) =>
        {
            var viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
            GraphicsDevice.Viewport = viewport;
            // _skia.RemakeRenderTarget(Window.ClientBounds.Width, Window.ClientBounds.Height);
            GameSparker.WindowSizeChanged(Window.ClientBounds.Width, Window.ClientBounds.Height);
            GameSparker.CurrentPhase.WindowSizeChanged(Window.ClientBounds.Width, Window.ClientBounds.Height);
        };
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        
        UpdateInput();
        UpdateMouse();

        if (!loaded)
        {
            loaded = true;
        }

        var tick = Stopwatch.StartNew();

        GameSparker.CurrentPhase.BeginGameTick();
        GameSparker.GameTick();
        GameSparker.CurrentPhase.GameTick();
        GameSparker.CurrentPhase.EndGameTick();

        _lastTickTime = (int)tick.ElapsedMilliseconds;
    }

    protected override void Initialize()
    {
        _imguiRenderer = new ImGuiRenderer(this);

#if USE_BASS
        Bass.Init();
#endif

        oldKeyState = Keyboard.GetState();
        oldMouseState = Mouse.GetState();
        
        _nvg = new NanoVGRenderer(GraphicsDevice);
        
        base.Initialize();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            foreach (var shadowRenderTarget in shadowRenderTargets)
            {
                shadowRenderTarget.Dispose();
            }
            _nvg.Dispose();
            _imguiRenderer.Dispose();
        }
    }

    protected override void LoadContent()
    {
        _polyShader = new Effect(GraphicsDevice, System.IO.File.ReadAllBytes("./data/shaders/Poly.fxb"));
        _skyShader = new Effect(GraphicsDevice, System.IO.File.ReadAllBytes("./data/shaders/Sky.fxb"));
        _groundShader = new Effect(GraphicsDevice, System.IO.File.ReadAllBytes("./data/shaders/Ground.fxb"));
        _mountainsShader = new Effect(GraphicsDevice, System.IO.File.ReadAllBytes("./data/shaders/Mountains.fxb"));
        
        GameSparker.Load(this);

        // Create floating point render target
        shadowRenderTargets = new RenderTarget2D[3];
        for (int i = NumCascades - 1; i >= 0; i--)
        {
            shadowRenderTargets[i] = new RenderTarget2D(
                GraphicsDevice,
                2048,
                2048,
                false,
                SurfaceFormat.Single,
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.DiscardContents);
        }
        
        // Clear all render targets AFTER creating them all
        for (int i = 0; i < NumCascades; i++)
        {
            GraphicsDevice.SetRenderTarget(shadowRenderTargets[i]);
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.White, 1.0f, 0);
            GraphicsDevice.SetRenderTarget(null);
        }
        
        _imguiRenderer.RebuildFontAtlas();

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
        
        #endregion

        return;

        static System.Numerics.Vector4 RGB(int r, int g, int b, float a = 1.0f) => new(r / 255f, g / 255f, b / 255f, a);
    }

    private void UpdateInput()
    {
        var newState = Keyboard.GetState();
        
        foreach (var xnaKey in XnaKeys)
        {
            var nfmKey = TranslateKey(xnaKey);
            if (newState.IsKeyDown(xnaKey) && !oldKeyState.IsKeyDown(xnaKey))
            {
                KeyDown(nfmKey);
            }
            else if (newState.IsKeyUp(xnaKey) && !oldKeyState.IsKeyUp(xnaKey))
            {
                KeyUp(nfmKey);
            }
        }

        // Update saved state.
        oldKeyState = newState;
    }

    private void UpdateMouse()
    {
        var newState = Mouse.GetState();
        
        if (newState.LeftButton == ButtonState.Pressed && oldMouseState.LeftButton != ButtonState.Pressed)
        {
            MouseDown(newState.X, newState.Y);
        }
        else if (newState.LeftButton == ButtonState.Released && oldMouseState.LeftButton != ButtonState.Released)
        {
            MouseUp(newState.X, newState.Y);
        }

        if (newState.X != oldMouseState.X || newState.Y != oldMouseState.Y)
        {
            GameSparker.CurrentPhase.MouseMoved(newState.X, newState.Y, ImGui.GetIO().WantCaptureMouse);
        }

        if (newState.ScrollWheelValue != oldMouseState.ScrollWheelValue)
        {
            var delta = newState.ScrollWheelValue - oldMouseState.ScrollWheelValue;
            GameSparker.CurrentPhase.MouseScrolled(delta, ImGui.GetIO().WantCaptureMouse);
        }

        oldMouseState = newState;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        var t = Stopwatch.StartNew();
        
        // Render based on game state
        GameSparker.CurrentPhase.Render();
        
        _nvg.Render();

        GameSparker.CurrentPhase.RenderAfterSkia();
        
        // // Render ImGui
        _imguiRenderer.BeginLayout(gameTime);
        GameSparker.RenderImgui();
        GameSparker.CurrentPhase.RenderImgui();
        _imguiRenderer.EndLayout();

        base.Draw(gameTime);
        _lastFrameTime = (int)t.ElapsedMilliseconds;
    }

    public static void Main()
    {
        // TODO figure out why SDL ProcessExit doesn't work properly
        AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
        {
            Process.GetCurrentProcess().Kill();
        };

        var program = new Program();
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
        }
        else
        {
            GameSparker.KeyReleased(key);
            GameSparker.CurrentPhase.KeyReleased(key, ImGui.GetIO().WantCaptureKeyboard);
        }
    }
}

public class DummyBackend : IBackend
{
    public IRadicalMusic LoadMusic(File file, double tempomul)
    {
        return new RadicalMusic(file, tempomul);
    }

    public IImage LoadImage(File file)
    {
        throw new NotImplementedException();
    }

    public IImage LoadImage(ReadOnlySpan<byte> file)
    {
        throw new NotImplementedException();
    }

    public void StopAllSounds()
    {
        SoundClip.StopAll();
    }

    public ISoundClip GetSound(string filePath)
    {
        return new SoundClip(filePath);
    }

    public IGraphics Graphics { get; } = new DummyGraphics();

    public class DummyGraphics : IGraphics
    {
        public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[] colorPos)
        {
            
        }
        public void SetColor(Color c)
        {
        }

        public void FillPolygon(Span<int> x, Span<int> y, int n)
        {
        }

        public void DrawPolygon(Span<int> x, Span<int> y, int n)
        {
        }

        public void FillRect(int x1, int y1, int width, int height)
        {
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
        }

        public void SetAlpha(float f)
        {
        }

        public void DrawImage(IImage image, int x, int y)
        {
        }

        public void SetFont(Font font)
        {
        }

        public IFontMetrics GetFontMetrics()
        {
            throw new NotImplementedException();
        }

        public void DrawString(string text, int x, int y)
        {
        }

        public void FillOval(int p0, int p1, int p2, int p3)
        {
        }

        public void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
        {
        }

        public void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
        {
        }

        public void DrawRect(int x1, int y1, int width, int height)
        {
        }

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
        }
    }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}