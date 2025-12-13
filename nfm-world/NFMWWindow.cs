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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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
    private SpriteFont _basicFont;

    private int _lastFrameTime;
    private int _lastTickTime;
    private KeyboardState oldKeyState;
    private MouseState oldMouseState;

    private static bool loaded;
    private const int FrameDelay = (int) (1000 / 21.3f);

    private static readonly FrozenDictionary<Microsoft.Xna.Framework.Input.Keys, Keys> KeyMapping = new Dictionary<Microsoft.Xna.Framework.Input.Keys, Keys>
    {
        // [Key.Unknown] = Keys.Unknown,
        [Microsoft.Xna.Framework.Input.Keys.Space] = Keys.Space,
        // [Key.Apostrophe] = Keys.Apostrophe,
        // [Key.Comma] = Keys.Comma,
        // [Key.Minus] = Keys.Minus,
        // [Key.Period] = Keys.Period,
        // [Key.Slash] = Keys.Slash,
        // [Key.Number0] = Keys.Zero,
        [Microsoft.Xna.Framework.Input.Keys.D0] = Keys.D0,
        [Microsoft.Xna.Framework.Input.Keys.D1] = Keys.D1,
        [Microsoft.Xna.Framework.Input.Keys.D2] = Keys.D2,
        [Microsoft.Xna.Framework.Input.Keys.D3] = Keys.D3,
        [Microsoft.Xna.Framework.Input.Keys.D4] = Keys.D4,
        [Microsoft.Xna.Framework.Input.Keys.D5] = Keys.D5,
        [Microsoft.Xna.Framework.Input.Keys.D6] = Keys.D6,
        [Microsoft.Xna.Framework.Input.Keys.D7] = Keys.D7,
        [Microsoft.Xna.Framework.Input.Keys.D8] = Keys.D8,
        [Microsoft.Xna.Framework.Input.Keys.D9] = Keys.D9,
        [Microsoft.Xna.Framework.Input.Keys.OemSemicolon] = Keys.OemSemicolon,
        // [Key.Equal] = Keys.Equal,
        [Microsoft.Xna.Framework.Input.Keys.A] = Keys.A,
        [Microsoft.Xna.Framework.Input.Keys.B] = Keys.B,
        [Microsoft.Xna.Framework.Input.Keys.C] = Keys.C,
        [Microsoft.Xna.Framework.Input.Keys.D] = Keys.D,
        [Microsoft.Xna.Framework.Input.Keys.E] = Keys.E,
        [Microsoft.Xna.Framework.Input.Keys.F] = Keys.F,
        [Microsoft.Xna.Framework.Input.Keys.G] = Keys.G,
        [Microsoft.Xna.Framework.Input.Keys.H] = Keys.H,
        [Microsoft.Xna.Framework.Input.Keys.I] = Keys.I,
        [Microsoft.Xna.Framework.Input.Keys.J] = Keys.J,
        [Microsoft.Xna.Framework.Input.Keys.K] = Keys.K,
        [Microsoft.Xna.Framework.Input.Keys.L] = Keys.L,
        [Microsoft.Xna.Framework.Input.Keys.M] = Keys.M,
        [Microsoft.Xna.Framework.Input.Keys.N] = Keys.N,
        [Microsoft.Xna.Framework.Input.Keys.O] = Keys.O,
        [Microsoft.Xna.Framework.Input.Keys.P] = Keys.P,
        [Microsoft.Xna.Framework.Input.Keys.Q] = Keys.Q,
        [Microsoft.Xna.Framework.Input.Keys.R] = Keys.R,
        [Microsoft.Xna.Framework.Input.Keys.S] = Keys.S,
        [Microsoft.Xna.Framework.Input.Keys.T] = Keys.T,
        [Microsoft.Xna.Framework.Input.Keys.U] = Keys.U,
        [Microsoft.Xna.Framework.Input.Keys.V] = Keys.V,
        [Microsoft.Xna.Framework.Input.Keys.W] = Keys.W,
        [Microsoft.Xna.Framework.Input.Keys.X] = Keys.X,
        [Microsoft.Xna.Framework.Input.Keys.Y] = Keys.Y,
        [Microsoft.Xna.Framework.Input.Keys.Z] = Keys.Z,
        // [Key.LeftBracket] = Keys.LeftBracket,
        // [Key.BackSlash] = Keys.BackSlash,
        // [Key.RightBracket] = Keys.RightBracket,
        // [Key.GraveAccent] = Keys.GraveAccent,
        // [Key.World1] = Keys.World1,
        // [Key.World2] = Keys.World2,
        [Microsoft.Xna.Framework.Input.Keys.Escape] = Keys.Escape,
        [Microsoft.Xna.Framework.Input.Keys.Enter] = Keys.Enter,
        [Microsoft.Xna.Framework.Input.Keys.Tab] = Keys.Tab,
        [Microsoft.Xna.Framework.Input.Keys.Back] = Keys.Back,
        [Microsoft.Xna.Framework.Input.Keys.Insert] = Keys.Insert,
        [Microsoft.Xna.Framework.Input.Keys.Delete] = Keys.Delete,
        [Microsoft.Xna.Framework.Input.Keys.Right] = Keys.Right,
        [Microsoft.Xna.Framework.Input.Keys.Left] = Keys.Left,
        [Microsoft.Xna.Framework.Input.Keys.Down] = Keys.Down,
        [Microsoft.Xna.Framework.Input.Keys.Up] = Keys.Up,
        [Microsoft.Xna.Framework.Input.Keys.PageUp] = Keys.PageUp,
        [Microsoft.Xna.Framework.Input.Keys.PageDown] = Keys.PageDown,
        [Microsoft.Xna.Framework.Input.Keys.Home] = Keys.Home,
        [Microsoft.Xna.Framework.Input.Keys.End] = Keys.End,
        [Microsoft.Xna.Framework.Input.Keys.CapsLock] = Keys.CapsLock,
        [Microsoft.Xna.Framework.Input.Keys.Scroll] = Keys.Scroll,
        [Microsoft.Xna.Framework.Input.Keys.NumLock] = Keys.NumLock,
        [Microsoft.Xna.Framework.Input.Keys.PrintScreen] = Keys.PrintScreen,
        [Microsoft.Xna.Framework.Input.Keys.Pause] = Keys.Pause,
        [Microsoft.Xna.Framework.Input.Keys.F1] = Keys.F1,
        [Microsoft.Xna.Framework.Input.Keys.F2] = Keys.F2,
        [Microsoft.Xna.Framework.Input.Keys.F3] = Keys.F3,
        [Microsoft.Xna.Framework.Input.Keys.F4] = Keys.F4,
        [Microsoft.Xna.Framework.Input.Keys.F5] = Keys.F5,
        [Microsoft.Xna.Framework.Input.Keys.F6] = Keys.F6,
        [Microsoft.Xna.Framework.Input.Keys.F7] = Keys.F7,
        [Microsoft.Xna.Framework.Input.Keys.F8] = Keys.F8,
        [Microsoft.Xna.Framework.Input.Keys.F9] = Keys.F9,
        [Microsoft.Xna.Framework.Input.Keys.F10] = Keys.F10,
        [Microsoft.Xna.Framework.Input.Keys.F11] = Keys.F11,
        [Microsoft.Xna.Framework.Input.Keys.F12] = Keys.F12,
        [Microsoft.Xna.Framework.Input.Keys.F13] = Keys.F13,
        [Microsoft.Xna.Framework.Input.Keys.F14] = Keys.F14,
        [Microsoft.Xna.Framework.Input.Keys.F15] = Keys.F15,
        [Microsoft.Xna.Framework.Input.Keys.F16] = Keys.F16,
        [Microsoft.Xna.Framework.Input.Keys.F17] = Keys.F17,
        [Microsoft.Xna.Framework.Input.Keys.F18] = Keys.F18,
        [Microsoft.Xna.Framework.Input.Keys.F19] = Keys.F19,
        [Microsoft.Xna.Framework.Input.Keys.F20] = Keys.F20,
        [Microsoft.Xna.Framework.Input.Keys.F21] = Keys.F21,
        [Microsoft.Xna.Framework.Input.Keys.F22] = Keys.F22,
        [Microsoft.Xna.Framework.Input.Keys.F23] = Keys.F23,
        [Microsoft.Xna.Framework.Input.Keys.F24] = Keys.F24,
        [Microsoft.Xna.Framework.Input.Keys.NumPad0] = Keys.NumPad0,
        [Microsoft.Xna.Framework.Input.Keys.NumPad1] = Keys.NumPad1,
        [Microsoft.Xna.Framework.Input.Keys.NumPad2] = Keys.NumPad2,
        [Microsoft.Xna.Framework.Input.Keys.NumPad3] = Keys.NumPad3,
        [Microsoft.Xna.Framework.Input.Keys.NumPad4] = Keys.NumPad4,
        [Microsoft.Xna.Framework.Input.Keys.NumPad5] = Keys.NumPad5,
        [Microsoft.Xna.Framework.Input.Keys.NumPad6] = Keys.NumPad6,
        [Microsoft.Xna.Framework.Input.Keys.NumPad7] = Keys.NumPad7,
        [Microsoft.Xna.Framework.Input.Keys.NumPad8] = Keys.NumPad8,
        [Microsoft.Xna.Framework.Input.Keys.NumPad9] = Keys.NumPad9,
        // [Key.KeypadDecimal] = Keys.NumPadDecimal,
        // [Key.KeypadDivide] = Keys.NumPadDivide,
        // [Key.KeypadMultiply] = Keys.NumPadMultiply,
        // [Key.KeypadSubtract] = Keys.NumPadSubtract,
        // [Key.KeypadAdd] = Keys.NumPadAdd,
        // [Key.KeypadEnter] = Keys.NumPadEnter,
        // [Key.KeypadEqual] = Keys.NumPadEqual,
        [Microsoft.Xna.Framework.Input.Keys.LeftShift] = Keys.LShiftKey,
        [Microsoft.Xna.Framework.Input.Keys.LeftControl] = Keys.LControlKey,
        [Microsoft.Xna.Framework.Input.Keys.LeftAlt] = Keys.Alt,
        // [Key.SuperLeft] = Keys.SuperLeft,
        [Microsoft.Xna.Framework.Input.Keys.RightShift] = Keys.RShiftKey,
        [Microsoft.Xna.Framework.Input.Keys.RightControl] = Keys.RControlKey,
        [Microsoft.Xna.Framework.Input.Keys.RightAlt] = Keys.Alt,
        // [Key.SuperRight] = Keys.SuperRight,
        // [Key.Menu] = Keys.Menu,
    }.ToFrozenDictionary();

    private Program()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromMilliseconds(1000 / 63f);
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.ApplyChanges();
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

        GameSparker.GameTick();

        _lastTickTime = (int)tick.ElapsedMilliseconds;
    }

    protected override void Initialize()
    {
        base.Initialize();
        
        IBackend.Backend = new DummyBackend(); // TODO

#if USE_BASS
        Bass.Init();
#endif

        oldKeyState = Keyboard.GetState();
        oldMouseState = Mouse.GetState();
        
        var originalOut = Console.Out;
        GameSparker.Writer = new DevConsoleWriter(GameSparker.devConsole, originalOut);
        Console.SetOut(GameSparker.Writer);
        GameSparker.Load(this);
        
        GameSparker.StartGame();
    }

    protected override void LoadContent()
    {
        _polyShader = Content.Load<Effect>("Poly");
        _basicFont = Content.Load<SpriteFont>("BasicFont");
    }

    private void UpdateInput()
    {
        var newState = Keyboard.GetState();
        
        foreach (var (xnaKey, nfmKey) in KeyMapping)
        {
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
            if (GameSparker.CurrentState == GameSparker.GameState.Menu && GameSparker.MainMenu != null)
            {
                // GameSparker.MainMenu.UpdateMouse(newState.X, newState.Y);
            }
        }

        oldMouseState = newState;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        var t = Stopwatch.StartNew();
        
        // Render based on game state
        if (GameSparker.CurrentState == GameSparker.GameState.Menu && GameSparker.MainMenu != null)
        {
            GameSparker.MainMenu.Render();
        }
        else if (GameSparker.CurrentState == GameSparker.GameState.InGame)
        {
            GameSparker.Render();
        
            G.SetColor(new Color(0, 0, 0));
            G.DrawString($"Render: {_lastFrameTime}ms", 100, 100);
            G.DrawString($"Tick: {_lastTickTime}ms", 100, 120);
            G.DrawString($"Power: {GameSparker.cars_in_race[0]?.Mad?.Power:0.00}", 100, 140);
        }
        
        // // Render ImGui
        // _imguiController?.Update((float)delta);
        // _imguiController?.NewFrame();
        // GameSparker.RenderImgui();
        // _imguiController?.Render();

        base.Draw(gameTime);
        _lastFrameTime = (int)t.ElapsedMilliseconds;
    }

    public static void Main()
    {
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
        if (GameSparker.CurrentState == GameSparker.GameState.Menu && GameSparker.MainMenu != null)
        {
            // GameSparker.MainMenu.HandleClick(x, y);
        }
    }

    private void MouseDown(int x, int y)
    {
        // Currently not needed, but could be used for button press effects
    }

    private void HandleKeyPress(Keys key, bool isDown)
    {
        if (isDown)
        {
            GameSparker.KeyPressed(key);
        }
        else
        {
            GameSparker.KeyReleased(key);
        }
    }
}

public class DummyBackend : IBackend
{
    public IRadicalMusic LoadMusic(File file)
    {
        return new RadicalMusic(file);
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