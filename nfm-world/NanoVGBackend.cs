using FontStashSharp;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Util;
using NvgSharp;
using File = NFMWorld.Util.File;

namespace NFMWorld;

public class NanoVGRenderer : IDisposable
{
    private NvgContext _context;
    private readonly FontSystem fontSystem;

    public NanoVGRenderer(GraphicsDevice graphicsDevice)
    {
        fontSystem = new FontSystem();
        fontSystem.AddFont(System.IO.File.ReadAllBytes("./data/fonts/DroidSans.ttf"));

        _context = new NvgContext(graphicsDevice);
        IBackend.Backend = new NanoVGBackend(_context, fontSystem);
    }

    public void Render()
    {
        _context.Flush();
    }

    public void Dispose()
    {
        fontSystem.Dispose();
    }
}

internal class NanoVGBackend(NvgContext context, FontSystem fontSystem) : IBackend
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

    public IGraphics Graphics { get; } = new NvgGraphics(context, fontSystem);

    public class NvgGraphics(NvgContext context, FontSystem fontSystem) : IGraphics
    {
        private Paint _paint = new Paint();
        private DynamicSpriteFont _font = fontSystem.GetFont(18);

        public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos)
        {
            throw new NotImplementedException(); // TODO
        }

        public void SetColor(Color c)
        {
            _paint.InnerColor = new Microsoft.Xna.Framework.Color(c.R, c.G, c.B, c.A);
        }

        public void FillPolygon(Span<int> x, Span<int> y, int n)
        {
            throw new NotImplementedException(); // TODO
        }

        public void DrawPolygon(Span<int> x, Span<int> y, int n)
        {
            throw new NotImplementedException(); // TODO
        }

        public void FillRect(int x1, int y1, int width, int height)
        {
            context.FillPaint(_paint);
            context.Rect(x1, y1, width, height);
            context.Fill();
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
            context.StrokePaint(_paint);
            context.BeginPath();
            context.MoveTo(x1, y1);
            context.LineTo(x2, y2);
            context.Stroke();
        }

        public void SetAlpha(float f)
        {
            _paint.InnerColor.A = (byte)(255 * f);
        }

        public void DrawImage(IImage image, int x, int y)
        {
        }

        public void SetFont(Font font)
        {
            // TODO
        }

        public IFontMetrics GetFontMetrics()
        {
            throw new NotImplementedException();
        }

        public void DrawString(string text, int x, int y)
        {
            context.Text(_font, text, x, y);
        }

        public void FillOval(int p0, int p1, int p2, int p3)
        {
            throw new NotImplementedException();
        }

        public void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
        {
            throw new NotImplementedException();
        }

        public void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
        {
            throw new NotImplementedException();
        }

        public void DrawRect(int x1, int y1, int width, int height)
        {
            context.StrokePaint(_paint);
            context.Rect(x1, y1, width, height);
            context.Stroke();
        }

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
            throw new NotImplementedException();
        }
    }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}