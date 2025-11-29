﻿using System;
using System.Collections.Frozen;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using ManagedBass;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using SkiaSharp;
using Color = NFMWorld.Util.Color;
using Font = NFMWorld.Util.Font;
using File = NFMWorld.Util.File;
using Keys = NFMWorld.Util.Keys;
using Window = Silk.NET.Windowing.Window;
using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Mad;

namespace NFMWorld;

/// <summary>
/// This sample demonstrates how to load a Direct2D1 bitmap from a file.
/// This method will be part of a future version of SharpDX API.
/// </summary>
public unsafe class Program
{
    private readonly IWindow _window;
    private GRGlInterface _grgInterface;
    private GRContext _grContext;
    private GRBackendRenderTarget _renderTarget;
    private SKSurface _surface;
    private SKCanvas _canvas;
    private IInputContext _input;
    private SkiaSharpBackend _backend;
    private readonly Glfw _glfw;
    private static bool loaded;
    private const int FrameDelay = (int) (1000 / 21.3f);
    private const float scale = 1.6f;

    private static readonly FrozenDictionary<Key, Keys> KeyMapping = new Dictionary<Key, Keys>()
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
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>((int)(800*scale), (int)(450*scale));
        options.Title = "Silk.NET with SkiaSharp Triangle";
        options.UpdatesPerSecond = 120.0f;
        options.ShouldSwapAutomatically = false;
        options.API = GraphicsAPI.Default;

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
        _canvas.DrawRect(0, 0, _window.Size.X, _window.Size.Y, new SKPaint
        {
            Color = SKColors.Black
        });
        
        if (!loaded)
        {
            loaded = true;
                
            GameSparker.Load();
        }
            
        GameSparker.GameTick();
        
        _canvas.Flush();
        _window.SwapBuffers();
    }

    private void OnLoad()
    {
        _grgInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(_grgInterface);
        _renderTarget = new GRBackendRenderTarget(_window.Size.X, _window.Size.Y, 0, 8, new GRGlFramebufferInfo(0, (uint)0x8058));
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        _canvas = _surface.Canvas;

        _backend = (SkiaSharpBackend)(IBackend.Backend = new SkiaSharpBackend(_canvas));
        _backend.Ratio = new Vector2D<float>(scale);

#if USE_BASS
        Bass.Init();
#endif

        _input = _window.CreateInput();
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
            //GameSparker.KeyPressed(key);
        }
        else
        {
            //GameSparker.KeyReleased(key);
        }
    }
}

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

    public void FillPolygon(int[] xPoints, int[] yPoints, int nPoints)
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

    public void DrawPolygon(int[] xPoints, int[] yPoints, int nPoints)
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