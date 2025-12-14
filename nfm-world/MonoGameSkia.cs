using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Util;
using Silk.NET.OpenGL;
using SkiaSharp;
using Stride.Core.Mathematics;
using Color = NFMWorld.Util.Color;
using File = NFMWorld.Util.File;

namespace NFMWorld;

public class MonoGameSkia
{
    private GRGlInterface _grgInterface;
    private GRContext _grContext;
    private GRBackendRenderTarget _renderTarget;
    private SKSurface _surface;
    private SKCanvas _canvas;
    private SkiaSharpBackend _backend;
    private int _fbo;
    private readonly GL _gl;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr d_sdl_gl_getprocaddress(string proc);

    public MonoGameSkia(GraphicsDevice graphicsDevice)
    {
        var glType = Type.GetType("Sdl+GL, MonoGame.Framework") ?? throw new InvalidOperationException("SDL GL type not found");
        var getProcAddressField = glType.GetField("GetProcAddress", BindingFlags.Public | BindingFlags.Static) ?? throw new InvalidOperationException("GetProcAddress field not found");
        var getProcAddressDelegate = (Delegate)getProcAddressField.GetValue(null)!;
        var GetProcAddress = Marshal.GetDelegateForFunctionPointer<d_sdl_gl_getprocaddress>(Marshal.GetFunctionPointerForDelegate(getProcAddressDelegate));

        _gl = GL.GetApi(e => GetProcAddress(e));

        _grgInterface = GRGlInterface.CreateOpenGl(e =>
        {
            switch (e)
            {
                // Fix segfault on Linux
                case "eglQueryString":
                case "eglGetCurrentDisplay":
                    return 0;
                default:
                    return GetProcAddress(e);
            }
        });
        _grgInterface.Validate();
        _grContext = GRContext.CreateGl(_grgInterface);
        _renderTarget = new GRBackendRenderTarget(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, 0, 8, new GRGlFramebufferInfo(0, (uint)0x8058));
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        _canvas = _surface.Canvas;

        _backend = (SkiaSharpBackend)(IBackend.Backend = new SkiaSharpBackend(_canvas));
    }

    public void Render()
    {
        _grContext.ResetContext(GRGlBackendState.All);
        _canvas.Flush();
        //
        // Reset GL state modified by SkiaSharp
        _gl.Disable(EnableCap.Blend);
        // _gl.Disable(EnableCap.VertexProgramPointSize);
        // _gl.BindVertexArray(vertexArrayObject); // Restore default VAO 
        // _gl.FrontFace(FrontFaceDirection.CW);
        // _gl.Enable(EnableCap.FramebufferSrgb);
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

    public Vector2 Ratio
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

    public Vector2 Ratio
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