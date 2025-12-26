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
        private Paint _paint = new();
        private DynamicSpriteFont _font = fontSystem.GetFont(18);
        private float layerDepth = 0.0f;
        private float characterSpacing = 0.0f;
        private float lineSpacing = 0.0f;
        private TextStyle textStyle = TextStyle.None;
        private FontSystemEffect effect = FontSystemEffect.None;
        private int effectAmount = 1;

        public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos)
        {
            if (colors.Length > 2)
            {
                throw new NotImplementedException("Only two-color gradients are supported currently.");
            }

            if (colorPos != null)
            {
                throw new NotImplementedException("Custom color positions are not supported currently.");
            }
            
            var gradientPaint = context.LinearGradient(x, y, x + width, y + height, 
                new Microsoft.Xna.Framework.Color(colors[0].R, colors[0].G, colors[0].B, colors[0].A), 
                new Microsoft.Xna.Framework.Color(colors[1].R, colors[1].G, colors[1].B, colors[1].A));
            _paint = gradientPaint;
        }

        public void SetColor(Color c)
        {
            _paint = new Paint(new Microsoft.Xna.Framework.Color(c.R, c.G, c.B, c.A));
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
            context.BeginPath();
            context.Rect(x1, y1, width, height);
            context.FillPaint(_paint);
            context.Fill();
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
            context.BeginPath();
            context.MoveTo(x1, y1);
            context.LineTo(x2, y2);
            context.StrokePaint(_paint);
            context.Stroke();
        }

        public void SetAlpha(float f)
        {
            _paint.InnerColor.A = (byte)(255 * f);
            _paint.OuterColor.A = (byte)(255 * f);
        }

        public void DrawImage(IImage image, int x, int y)
        {
        }

        public void SetFont(Font font)
        {
            _font = fontSystem.GetFont(font.Size);
        }

        public IFontMetrics GetFontMetrics()
        {
            throw new NotImplementedException();
        }

        public void DrawString(string text, int x, int y)
        {
            context.FillPaint(_paint);
            context.Text(_font, text, x, y - _font.FontSize, layerDepth, characterSpacing, lineSpacing, textStyle, effect, effectAmount);
        }

        public void DrawStringAligned(string text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top)
        {
            context.FillPaint(_paint);
            float x = areaWidth / 2;
            float y = areaHeight / 2;

            if (hAlign != TextHorizontalAlignment.Left)
			{
				var sz = _font.MeasureString(text);
				if (hAlign == TextHorizontalAlignment.Center)
				{
					x -= sz.X / 2.0f;
				}
				else if (hAlign == TextHorizontalAlignment.Right)
				{
					x -= sz.X;
				}
			}


			if (vAlign == TextVerticalAlignment.Center)
			{
				y -= _font.LineHeight / 2.0f;
			}
			else if (vAlign == TextVerticalAlignment.Bottom)
			{
				y -= _font.LineHeight;
			}

            context.Text(_font, text, x, y, layerDepth, characterSpacing, lineSpacing, textStyle, effect, effectAmount);
        }

        public void DrawStringStroke(string text, int x, int y, int effectAmount = 1)
        {
            context.StrokePaint(_paint);
            context.Text(_font, text, x, y - _font.FontSize, layerDepth, characterSpacing, lineSpacing, textStyle, FontSystemEffect.Stroked, effectAmount);
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
            context.BeginPath();
            context.Rect(x1, y1, width, height);
            context.StrokePaint(_paint);
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