using System.Collections.Frozen;
using ManagedBass;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Color = NFMWorld.Util.Color;
using Font = NFMWorld.Util.Font;
using File = NFMWorld.Util.File;
using Keys = NFMWorld.Util.Keys;
using Window = Silk.NET.Windowing.Window;
using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Mad;
using NImpeller;
using Silk.NET.Input.Glfw;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Windowing.Sdl;
using System.Diagnostics;
using Silk.NET.Input.Sdl;
using SkiaSharp;

namespace NFMWorld;

/// <summary>
/// This sample demonstrates how to load a Direct2D1 bitmap from a file.
/// This method will be part of a future version of SharpDX API.
/// </summary>
public unsafe class Program
{
    private int _lastFrameTime;
    private int _lastTickTime;
    private readonly IWindow _window;
    private IInputContext _input;
    
#if USE_IMPELLER
    private NImpellerBackend _backend;
    private ImpellerContext _impellerContext;
    private int _fbo;
    private ImpellerSurface? _surface;
#else
    private GRGlInterface _grgInterface;
    private GRContext _grContext;
    private GRBackendRenderTarget _renderTarget;
    private SKSurface _surface;
    private SKCanvas _canvas;
    private SkiaSharpBackend _backend;
    private int _fbo;
#endif
    
    private readonly Glfw _glfw;
    private static bool loaded;
    private const int FrameDelay = (int) (1000 / 21.3f);
    private const float scale = 1.6f;
    private ImGuiController? _imguiController;
    private GL _gl;

    private static readonly FrozenDictionary<Key, Keys> KeyMapping = new Dictionary<Key, Keys>
    {
        // [Key.Unknown] = Keys.Unknown,
        [Key.Space] = Keys.Space,
        // [Key.Apostrophe] = Keys.Apostrophe,
        // [Key.Comma] = Keys.Comma,
        // [Key.Minus] = Keys.Minus,
        // [Key.Period] = Keys.Period,
        // [Key.Slash] = Keys.Slash,
        // [Key.Number0] = Keys.Zero,
        [Key.D0] = Keys.D0,
        [Key.Number1] = Keys.D1,
        [Key.Number2] = Keys.D2,
        [Key.Number3] = Keys.D3,
        [Key.Number4] = Keys.D4,
        [Key.Number5] = Keys.D5,
        [Key.Number6] = Keys.D6,
        [Key.Number7] = Keys.D7,
        [Key.Number8] = Keys.D8,
        [Key.Number9] = Keys.D9,
        [Key.Semicolon] = Keys.OemSemicolon,
        // [Key.Equal] = Keys.Equal,
        [Key.A] = Keys.A,
        [Key.B] = Keys.B,
        [Key.C] = Keys.C,
        [Key.D] = Keys.D,
        [Key.E] = Keys.E,
        [Key.F] = Keys.F,
        [Key.G] = Keys.G,
        [Key.H] = Keys.H,
        [Key.I] = Keys.I,
        [Key.J] = Keys.J,
        [Key.K] = Keys.K,
        [Key.L] = Keys.L,
        [Key.M] = Keys.M,
        [Key.N] = Keys.N,
        [Key.O] = Keys.O,
        [Key.P] = Keys.P,
        [Key.Q] = Keys.Q,
        [Key.R] = Keys.R,
        [Key.S] = Keys.S,
        [Key.T] = Keys.T,
        [Key.U] = Keys.U,
        [Key.V] = Keys.V,
        [Key.W] = Keys.W,
        [Key.X] = Keys.X,
        [Key.Y] = Keys.Y,
        [Key.Z] = Keys.Z,
        // [Key.LeftBracket] = Keys.LeftBracket,
        // [Key.BackSlash] = Keys.BackSlash,
        // [Key.RightBracket] = Keys.RightBracket,
        // [Key.GraveAccent] = Keys.GraveAccent,
        // [Key.World1] = Keys.World1,
        // [Key.World2] = Keys.World2,
        [Key.Escape] = Keys.Escape,
        [Key.Enter] = Keys.Enter,
        [Key.Tab] = Keys.Tab,
        [Key.Backspace] = Keys.Back,
        [Key.Insert] = Keys.Insert,
        [Key.Delete] = Keys.Delete,
        [Key.Right] = Keys.Right,
        [Key.Left] = Keys.Left,
        [Key.Down] = Keys.Down,
        [Key.Up] = Keys.Up,
        [Key.PageUp] = Keys.PageUp,
        [Key.PageDown] = Keys.PageDown,
        [Key.Home] = Keys.Home,
        [Key.End] = Keys.End,
        [Key.CapsLock] = Keys.CapsLock,
        [Key.ScrollLock] = Keys.Scroll,
        [Key.NumLock] = Keys.NumLock,
        [Key.PrintScreen] = Keys.PrintScreen,
        [Key.Pause] = Keys.Pause,
        [Key.F1] = Keys.F1,
        [Key.F2] = Keys.F2,
        [Key.F3] = Keys.F3,
        [Key.F4] = Keys.F4,
        [Key.F5] = Keys.F5,
        [Key.F6] = Keys.F6,
        [Key.F7] = Keys.F7,
        [Key.F8] = Keys.F8,
        [Key.F9] = Keys.F9,
        [Key.F10] = Keys.F10,
        [Key.F11] = Keys.F11,
        [Key.F12] = Keys.F12,
        [Key.F13] = Keys.F13,
        [Key.F14] = Keys.F14,
        [Key.F15] = Keys.F15,
        [Key.F16] = Keys.F16,
        [Key.F17] = Keys.F17,
        [Key.F18] = Keys.F18,
        [Key.F19] = Keys.F19,
        [Key.F20] = Keys.F20,
        [Key.F21] = Keys.F21,
        [Key.F22] = Keys.F22,
        [Key.F23] = Keys.F23,
        [Key.F24] = Keys.F24,
        // [Key.F25] = Keys.F25,
        [Key.Keypad0] = Keys.NumPad0,
        [Key.Keypad1] = Keys.NumPad1,
        [Key.Keypad2] = Keys.NumPad2,
        [Key.Keypad3] = Keys.NumPad3,
        [Key.Keypad4] = Keys.NumPad4,
        [Key.Keypad5] = Keys.NumPad5,
        [Key.Keypad6] = Keys.NumPad6,
        [Key.Keypad7] = Keys.NumPad7,
        [Key.Keypad8] = Keys.NumPad8,
        [Key.Keypad9] = Keys.NumPad9,
        // [Key.KeypadDecimal] = Keys.NumPadDecimal,
        // [Key.KeypadDivide] = Keys.NumPadDivide,
        // [Key.KeypadMultiply] = Keys.NumPadMultiply,
        // [Key.KeypadSubtract] = Keys.NumPadSubtract,
        // [Key.KeypadAdd] = Keys.NumPadAdd,
        // [Key.KeypadEnter] = Keys.NumPadEnter,
        // [Key.KeypadEqual] = Keys.NumPadEqual,
        [Key.ShiftLeft] = Keys.LShiftKey,
        [Key.ControlLeft] = Keys.LControlKey,
        [Key.AltLeft] = Keys.Alt,
        // [Key.SuperLeft] = Keys.SuperLeft,
        [Key.ShiftRight] = Keys.RShiftKey,
        [Key.ControlRight] = Keys.RControlKey,
        [Key.AltRight] = Keys.Alt,
        // [Key.SuperRight] = Keys.SuperRight,
        [Key.Menu] = Keys.Menu,
    }.ToFrozenDictionary();

    private Program()
    {
        SdlWindowing.RegisterPlatform();
        SdlInput.RegisterPlatform();

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>((int)(800*scale), (int)(450*scale));
        options.Title = "Need For Madness: World";
        options.UpdatesPerSecond = 63f;
        options.FramesPerSecond = 60f;
        options.VSync = false;
        options.ShouldSwapAutomatically = false;
        options.API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0));

        var sdl = Sdl.GetApi();
        // sdl.SetHint("SDL_WINDOWS_DPI_AWARENESS"u8, "permonitorv2"u8);
        // sdl.SetHint("SDL_WINDOWS_DPI_SCALING"u8, "1"u8);

        _window = Window.Create(options);
    
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Update += OnUpdate;

        _window.Run();
    }

    private void OnUpdate(double delta)
    {
        if (!loaded)
        {
            loaded = true;
            var originalOut = Console.Out;
            GameSparker.Writer = new DevConsoleWriter(GameSparker.devConsole, originalOut);
            Console.SetOut(GameSparker.Writer);
            GameSparker.Load(_window);
        }

        var tick = Stopwatch.StartNew();

        GameSparker.GameTick();

        _lastTickTime = (int)tick.ElapsedMilliseconds;
    }

    private void OnLoad()
    {
        _gl = _window.CreateOpenGLES();
        _gl.GetInteger(GLEnum.FramebufferBinding, out var fbo);
        _fbo = fbo;

#if USE_IMPELLER
        _impellerContext = ImpellerContext.CreateOpenGLESNew(name =>
        {
            return _window.GLContext!.TryGetProcAddress(name, out var addr) ? addr : IntPtr.Zero;
        })!;
        
        _surface = _impellerContext.SurfaceCreateWrappedFBONew(
            (ulong)_fbo,
            ImpellerPixelFormat.kImpellerPixelFormatRGBA8888,
            new ImpellerISize(_window.Size.X, _window.Size.Y)
        );
        
        _backend = (NImpellerBackend)(IBackend.Backend = new NImpellerBackend(_surface));
        _backend.Ratio = new Vector2D<float>(scale);
#else
        _grgInterface = GRGlInterface.CreateGles(name => _window.GLContext!.TryGetProcAddress(name, out var addr) ? addr : IntPtr.Zero);
        _grgInterface.Validate();
        _grContext = GRContext.CreateGl(_grgInterface);
        _renderTarget = new GRBackendRenderTarget(_window.Size.X, _window.Size.Y, 0, 8, new GRGlFramebufferInfo(0, (uint)0x8058));
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        _canvas = _surface.Canvas;

        _backend = (SkiaSharpBackend)(IBackend.Backend = new SkiaSharpBackend(_canvas));
#endif

#if USE_BASS
        Bass.Init();
#endif

        _input = _window.CreateInput();
        
        _imguiController = new ImGuiController(_gl, _window, _input);
        
        _input.ConnectionChanged += InputOnConnectionChanged;
        
        foreach (var gamepad in _input.Gamepads)
        {
            if (!gamepad.IsConnected) continue;
            InputOnConnectionChanged(gamepad, gamepad.IsConnected);
        }

        foreach (var joystick in _input.Joysticks)
        {
            if (!joystick.IsConnected) continue;
            InputOnConnectionChanged(joystick, joystick.IsConnected);
        }

        foreach (var keyboard in _input.Keyboards)
        {
            if (!keyboard.IsConnected) continue;
            InputOnConnectionChanged(keyboard, keyboard.IsConnected);
        }

        foreach (var mouse in _input.Mice)
        {
            if (!mouse.IsConnected) continue;
            InputOnConnectionChanged(mouse, mouse.IsConnected);
        }

    }

    private void InputOnConnectionChanged(IInputDevice device, bool isConnected)
    {
        if (device is IKeyboard keyboard)
        {
            if (isConnected)
            {
                keyboard.KeyDown += KeyboardOnKeyDown;
                keyboard.KeyUp += KeyboardOnKeyUp;
            }
            else
            {
                keyboard.KeyDown -= KeyboardOnKeyDown;
                keyboard.KeyUp -= KeyboardOnKeyUp;
            }
        }
        else if (device is IMouse mouse)
        {
            
        }
    }

    private void KeyboardOnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        KeyUp(KeyMapping.GetValueOrDefault(key));
    }

    private void KeyboardOnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        KeyDown(KeyMapping.GetValueOrDefault(key));
    }

    private void OnRender(double delta)
    {
        var t = Stopwatch.StartNew();
        
#if USE_IMPELLER
        ImpellerDisplayList displayList;
        using (var drawListBuilder = ImpellerDisplayListBuilder.New(new ImpellerRect(100, 100, _window.Size.X, _window.Size.Y))!)
        {
            _backend.DrawListBuilder = drawListBuilder;
            using (var paint = ImpellerPaint.New()!)
            {
                paint.SetColor(ImpellerColor.FromArgb(1, 0, 0, 0));
                drawListBuilder.DrawRect(new ImpellerRect(0, 0, _window.Size.X, _window.Size.Y), paint);
            }
            
            GameSparker.Render();
        
            G.SetColor(new Color(0, 0, 0));
            G.DrawString($"Render: {_lastFrameTime}ms", 100, 100);
            G.DrawString($"Tick: {_lastTickTime}ms", 100, 120);
            G.DrawString($"Power: {GameSparker.cars_in_race[0]?.Mad?.Power:0.00}", 100, 140);
        
            displayList = drawListBuilder.CreateDisplayListNew()!;
        }

        using (displayList)
        {
            _surface?.DrawDisplayList(displayList);
        }
#else
        GameSparker.Render();
        
        _grContext.ResetContext(GRGlBackendState.All);
        _canvas.Flush();
        
        // Reset GL state modified by SkiaSharp
        _gl.Disable(EnableCap.Blend);
        // _gl.Disable(EnableCap.VertexProgramPointSize);
        // _gl.BindVertexArray(vertexArrayObject); // Restore default VAO 
        // _gl.FrontFace(FrontFaceDirection.CW);
        _gl.Enable(EnableCap.FramebufferSrgb);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        // _gl.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        _gl.UseProgram(0);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.DrawBuffers([DrawBufferMode.Back]);
        _gl.Enable(EnableCap.Dither);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.Multisample);
        _gl.Disable(EnableCap.ScissorTest);
#endif
        
        // // Render ImGui
        _imguiController?.Update((float)delta);
        _imguiController?.NewFrame();
        GameSparker.RenderDevConsole();
        _imguiController?.Render();

        _window.SwapBuffers();

        _lastFrameTime = (int)t.ElapsedMilliseconds;
    }

    public static void Main()
    {
        _ = new Program();
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
        //GameSparker.MouseReleased(x, y);
    }

    private void MouseDown(int x, int y)
    {
        //GameSparker.MousePressed(x, y);
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

#if USE_IMPELLER
internal class NImpellerBackend : IBackend
{
    public NImpellerBackend(ImpellerSurface? surface)
    {
        Graphics = new NImpellerGraphics(this);
    }

    public ImpellerDisplayListBuilder? DrawListBuilder { get; set; }

    public Vector2D<float> Ratio
    {
        set => ((NImpellerGraphics)Graphics).Ratio = value;
    }

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

    public IGraphics Graphics { get; }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}

internal class NImpellerGraphics(NImpellerBackend backend) : IGraphics
{
    private readonly ImpellerPaint _paint = ImpellerPaint.New()!;
    private readonly ImpellerTypographyContext _typographyContext = ImpellerTypographyContext.New()!;
    public Vector2D<float> Ratio { get; set; }

    private Color currentColor;

    public void SetColor(Color c)
    {
        currentColor = c;
        _paint.SetColor(ImpellerColor.FromArgb(c.A, c.R, c.G, c.B));
    }

    public void FillPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 2)
        {
            return;
        }
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleFill);
        
        using var pathBuilder = ImpellerPathBuilder.New()!;
        
        // calculate centroid
        float cx, cy;
        {
            int x = 0;
            int y = 0;
            for (int i = 0; i < nPoints; i++) {
                x += xPoints[i];
                y += yPoints[i];
            }
            cx = (float) x / nPoints;
            cy = (float) y / nPoints;
        }
        float sx = xPoints[0] < cx ? xPoints[0] - 0.5f : xPoints[0] + 0.5f;
        float sy = yPoints[0] < cy ? yPoints[0] - 0.5f : yPoints[0] + 0.5f;
        
        pathBuilder.MoveTo(new ImpellerPoint { X = sx, Y = sy });
        
        for (int i = 1; i < nPoints; i++) {
            int x = xPoints[i];
            int y = yPoints[i];
            if (xPoints[i - 1] == x && yPoints[i - 1] == y) continue;
            pathBuilder.LineTo(new ImpellerPoint
            {
                X = x < cx ? x - 0.5f : x + 0.5f,
                Y = y < cy ? y - 0.5f : y + 0.5f
            });
        }
        pathBuilder.LineTo(new ImpellerPoint { X = xPoints[0], Y = yPoints[0] });

        using var path = pathBuilder.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        backend.DrawListBuilder?.DrawPath(path, _paint);
    }

    public void DrawPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 1 || (nPoints == 2 && xPoints[0] == xPoints[1] && yPoints[0] == yPoints[1]))
        {
            return;
        }
        
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);

        using var pathBuilder = ImpellerPathBuilder.New()!;
        pathBuilder.MoveTo(new ImpellerPoint { X = xPoints[0], Y = yPoints[0] });
        
        for (int i = 1; i < nPoints; i++) {
            if (xPoints[i] == xPoints[i - 1] && yPoints[i] == yPoints[i - 1]) continue;
            pathBuilder.LineTo(new ImpellerPoint { X = xPoints[i], Y = yPoints[i] });
        }
        pathBuilder.LineTo(new ImpellerPoint { X = xPoints[0], Y = yPoints[0] });

        using var path = pathBuilder.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        backend.DrawListBuilder?.DrawPath(path, _paint);
    }
    
    public void FillRect(int x1, int y1, int width, int height)
    {
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleFill);
        backend.DrawListBuilder?.DrawRect(new ImpellerRect(x1, y1, width, height), _paint);
    }

    public void DrawLine(int x1, int y1, int x2, int y2)
    {
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        backend.DrawListBuilder?.DrawLine(
            new ImpellerPoint { X = x1, Y = y1 },
            new ImpellerPoint { X = x2, Y = y2 },
            _paint
        );
    }

    public void SetAlpha(float f)
    {
        currentColor = new Color(currentColor.R, currentColor.G, currentColor.B, (byte)f*255);
        _paint.SetColor(ImpellerColor.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B));
    }

    public void DrawImage(IImage image, int x, int y)
    {
        throw new NotImplementedException();
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
        using var paragraphBuilder = _typographyContext.ParagraphBuilderNew()!;
        using var impellerParagraphStyle = ImpellerParagraphStyle.New()!;
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleFill);
        impellerParagraphStyle.SetForeground(_paint);
        impellerParagraphStyle.SetFontSize(16f);
        paragraphBuilder.PushStyle(impellerParagraphStyle);
        paragraphBuilder.AddText(text);
        using var paragraph = paragraphBuilder.BuildParagraphNew(10000)!;
        backend.DrawListBuilder?.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = y });
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
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        backend.DrawListBuilder?.DrawRect(new ImpellerRect(x1, y1, width, height), _paint);
    }

    public void DrawImage(IImage image, int x, int y, int width, int height)
    {
        throw new NotImplementedException();
    }
}
#else
internal class SkiaSharpBackend(SKCanvas canvas) : IBackend
{
    public IRadicalMusic LoadMusic(File file)
    {
        return new RadicalMusic(file);
    }

    public IImage LoadImage(File file)
    {
        return new MadSharpSKImage(System.IO.File.ReadAllBytes(file.Path));
    }

    public IImage LoadImage(ReadOnlySpan<byte> file)
    {
        return new MadSharpSKImage(file);
    }

    public void StopAllSounds()
    {
        SoundClip.StopAll();
    }

    public ISoundClip GetSound(string filePath)
    {
        return new SoundClip(filePath);
    }

    public IGraphics Graphics { get; } = new SkiaSharpGraphics(canvas);

    public Vector2D<float> Ratio
    {
        set => ((SkiaSharpGraphics)Graphics).Ratio = value;
    }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}

internal class SkiaSharpGraphics(SKCanvas canvas) : IGraphics
{
    private float sx = 1;
    private float sy = 1;

    public Vector2D<float> Ratio
    {
        set
        {
            //canvas.Scale(1/sx, 1/sy);
            //canvas.Scale(value.X, value.Y);
            sx = value.X;
            sy = value.Y;
            //_paint.StrokeWidth = (sx + sy) / 3;
        }
    }

    private readonly SKPaint _paint = new()
    {
        IsAntialias = false,
    };
    private SKFont _font = new();
    private Dictionary<(string FamilyName, byte Flags), SKTypeface> _typefaceCache = new();

    public void SetColor(Color c)
    {
        _paint.Color = new SKColor(c.R, c.G, c.B, c.A);
    }

    public void FillPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 2)
        {
            return;
        }
        _paint.Style = SKPaintStyle.Fill;
        
        using var path = new SKPath();
        
        // calculate centroid
        float cx, cy;
        {
            int x = 0;
            int y = 0;
            for (int i = 0; i < nPoints; i++) {
                x += xPoints[i];
                y += yPoints[i];
            }
            cx = (float) x / nPoints;
            cy = (float) y / nPoints;
        }
        float sx = xPoints[0] < cx ? xPoints[0] - 0.5f : xPoints[0] + 0.5f;
        float sy = yPoints[0] < cy ? yPoints[0] - 0.5f : yPoints[0] + 0.5f;
        
        path.MoveTo(sx, sy);
        
        for (int i = 1; i < nPoints; i++) {
            int x = xPoints[i];
            int y = yPoints[i];
            if (xPoints[i - 1] == x && yPoints[i - 1] == y) continue;
            path.LineTo(
                x < cx ? x - 0.5f : x + 0.5f,
                y < cy ? y - 0.5f : y + 0.5f
            );
        }
        path.LineTo(xPoints[0], yPoints[0]);

        canvas.DrawPath(path, _paint);
    }

    public void DrawPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 1 || (nPoints == 2 && xPoints[0] == xPoints[1] && yPoints[0] == yPoints[1]))
        {
            return;
        }
        
        _paint.Style = SKPaintStyle.Stroke;
        
        using var path = new SKPath();
        path.MoveTo(xPoints[0], yPoints[0]);
        
        for (int i = 1; i < nPoints; i++) {
            if (xPoints[i] == xPoints[i - 1] && yPoints[i] == yPoints[i - 1]) continue;
            path.LineTo(xPoints[i], yPoints[i]);
        }
        path.LineTo(xPoints[0], yPoints[0]);

        canvas.DrawPath(path, _paint);
    }

    public void FillRect(int x1, int y1, int width, int height)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawRect(x1, y1, width, height, _paint);
    }

    public void DrawLine(int x1, int y1, int x2, int y2)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(x1, y1, x2, y2, _paint);
    }

    public void SetAlpha(float f)
    {
        _paint.Color = _paint.Color.WithAlpha((byte)(f * 255));
    }

    public void DrawImage(IImage image, int x, int y)
    {
        canvas.DrawImage(((MadSharpSKImage)image).SkImage, x, y);
    }

    public void SetFont(Font font)
    {
        if (_typefaceCache.TryGetValue((font.FontName, (byte)font.Flags), out var typeface))
        {
            _font.Typeface = typeface;
        }
        else
        {
            _font.Typeface = _typefaceCache[(font.FontName, (byte)font.Flags)] = SKTypeface.FromFamilyName(
                font.FontName,
                font.Flags == Font.BOLD ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                font.Flags == Font.ITALIC ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright
            );
        }
        
        _font.Size = font.Size;
    }

    public IFontMetrics GetFontMetrics()
    {
        return new SkiaFontMetrics(_font);
    }

    public void DrawString(string text, int x, int y)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawText(text, x, y, SKTextAlign.Left, _font, _paint);
    }

    public void FillOval(int p0, int p1, int p2, int p3)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawOval(p0, p1, p2, p3, _paint);
    }

    public void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(x, y, wid, hei, arcWid, arcHei, _paint);
    }

    public void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawRoundRect(x, y, wid, hei, arcWid, arcHei, _paint);
    }

    public void DrawRect(int x1, int y1, int width, int height)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawRect(x1, y1, width, height, _paint);
    }

    public void DrawImage(IImage image, int x, int y, int width, int height)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawImage(((MadSharpSKImage)image).SkImage, new SKRect(x, y, width + x, height + y));
    }

    public void SetAntialiasing(bool useAntialias)
    {
        _paint.IsAntialias = useAntialias;
    }
}

internal class SkiaFontMetrics(SKFont font) : IFontMetrics
{
    public int StringWidth(string astring)
    {
        return (int)font.MeasureText(astring);
    }

    public int Height(string astring)
    {
        return (int)font.Metrics.Ascent;
    }
}

internal class MadSharpSKImage : IImage
{
    internal readonly SKImage SkImage;

    public MadSharpSKImage(ReadOnlySpan<byte> file)
    {
        SkImage = SKImage.FromEncodedData(file);
    }

    public int Height => SkImage.Width;
    public int Width => SkImage.Height;
}
#endif