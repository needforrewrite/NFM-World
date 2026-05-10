using System.Collections.Concurrent;
using System.Text;
using FontStashSharp;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Util;
using NvgSharp;
using TextHorizontalAlignment = NFMWorld.DriverInterface.TextHorizontalAlignment;

namespace NFMWorld;

public class NanoVGRenderer
{
    private NvgContext _context;

    public NanoVGRenderer(GraphicsDevice graphicsDevice)
    {
        _context = new NvgContext(graphicsDevice);
        IBackend.Backend = new NanoVGBackend(_context);
    }

    public void Render()
    {
        _context.Flush();
    }
}

internal class NanoVGBackend(NvgContext context) : IBackend
{
    public float Scale { get; set; } = 1;
    
    private ConcurrentDictionary<string, IImage> _imageCache = new();

    public IRadicalMusic LoadMusic(string file, double tempomul)
    {
        return new RadicalMusic(file, tempomul);
    }

    public IImage LoadImage(string file)
    {
        using var stream = VFS.OpenRead(file);
        if (VFS.Path.GetExtension(file) == ".svg")
        {
            return NanoSVGImage.FromStream(stream);
        }
        return new NanoVGImage(Texture2D.FromStream(context.GraphicsDevice, stream));
    }
    
    public IImage LoadCachedImage(string file)
    {
        var fullPath = VFS.Path.GetFullPath(file);
        return _imageCache.GetOrAdd(fullPath, _ => LoadImage(fullPath));
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

    public IGraphics Graphics { get; } = new NvgGraphics(context);

    public class NvgGraphics : IGraphics
    {
        private Paint _paint;
        private float layerDepth = 0.0f;
        private float characterSpacing = 0.0f;
        private float lineSpacing = 0.0f;
        private TextStyle textStyle = TextStyle.None;
        private FontSystemEffect effect = FontSystemEffect.None;
        private int effectAmount = 1;
        private readonly NvgContext _context;
        
        private Dictionary<FontFamily, FontSystem> _fontSystems = new();
        private DynamicSpriteFont _font;

        public NvgGraphics(NvgContext context)
        {
            _context = context;
            
            _fontSystems[FontFamily.DroidSans] = LoadFont("./data/fonts/DroidSans.ttf");
            _fontSystems[FontFamily.AdventureHollow] = LoadFont("./data/fonts/AdventureHollow.otf");
            _fontSystems[FontFamily.Adventure] = LoadFont("./data/fonts/Adventure.otf");
            _fontSystems[FontFamily.RobotoMono] = LoadFont("./data/fonts/RobotoMono-Regular.ttf");
            _font = _fontSystems[FontFamily.DroidSans].GetFont(18);
        }

        private FontSystem LoadFont(string fontFile)
        {
            var fontSystem = new FontSystem();
            fontSystem.AddFont(VFS.ReadAllBytes(fontFile));
            return fontSystem;
        }

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
            
            var gradientPaint = _context.LinearGradient(x, y, x + width, y + height, 
                new Microsoft.Xna.Framework.Color(colors[0].R, colors[0].G, colors[0].B, colors[0].A), 
                new Microsoft.Xna.Framework.Color(colors[1].R, colors[1].G, colors[1].B, colors[1].A));
            _paint = gradientPaint;
        }

        public void SetColor(Color c)
        {
            _paint = new Paint(new Microsoft.Xna.Framework.Color(c.R, c.G, c.B, c.A));
        }

        public void FillPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
        {
            throw new NotImplementedException(); // TODO
        }

        public void DrawPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
        {
            throw new NotImplementedException(); // TODO
        }

        public void FillRect(int x1, int y1, int width, int height)
        {
            _context.BeginPath();
            _context.Rect(x1, y1, width, height);
            _context.FillPaint(_paint);
            _context.Fill();
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
            _context.BeginPath();
            _context.MoveTo(x1, y1);
            _context.LineTo(x2, y2);
            _context.StrokePaint(_paint);
            _context.Stroke();
        }

        public float Alpha
        {
            set
            {
                _paint.InnerColor.A = (byte)(255 * value);
                _paint.OuterColor.A = (byte)(255 * value);
            }
        }

        public void DrawImage(IImage image, int x, int y)
        {
            if (image is not NanoVGImage img) throw new ArgumentException("Invalid image type for NanoVGBackend.");

            var imgPaint = _context.ImagePattern(x, y, img.Width, img.Height, 0.0f, img.Texture, 1.0f);
            _context.BeginPath();
            _context.FillPaint(imgPaint);
            _context.Rect(x, y, img.Width, img.Height);
            _context.Fill();
        }

        public void SetFont(Font font)
        {
            _font = _fontSystems[font.FontFamily].GetFont(font.Size);
        }

        public IFontMetrics GetFontMetrics()
        {
            return new NanoVGFontMetrics(_font);
        }

        public void DrawString(string text, int x, int y)
        {
            _context.FillPaint(_paint);
            _context.Text(_font, text, x, y - _font.FontSize, layerDepth, characterSpacing, lineSpacing, textStyle, effect, effectAmount);
        }

        public void DrawStringAligned(string text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top)
        {
            _context.FillPaint(_paint);

            float xFloat = x;
            float yFloat = y;
            AlignText(text, areaWidth, areaHeight, hAlign, vAlign, ref xFloat, ref yFloat);

            _context.Text(_font, text, xFloat, yFloat, layerDepth, characterSpacing, lineSpacing, textStyle, effect, effectAmount);
        }

        public void DrawStringStroke(string text, int x, int y, int effectAmount = 1)
        {
            _context.FillPaint(_paint);
            _context.Text(_font, text, x, y - _font.FontSize, layerDepth, characterSpacing, lineSpacing, textStyle, FontSystemEffect.Stroked, effectAmount);
        }

        public void DrawStringStrokeAligned(string text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        {
            _context.FillPaint(_paint);

            float xFloat = x;
            float yFloat = y;
            AlignText(text, areaWidth, areaHeight, hAlign, vAlign, ref xFloat, ref yFloat);

            _context.Text(_font, text, xFloat, yFloat, layerDepth, characterSpacing, lineSpacing, textStyle, FontSystemEffect.Stroked, effectAmount);
        }

        public string LayoutText(string text, float width, float height, BreakType breakType = BreakType.Word, OverflowBehavior overflowBehavior = OverflowBehavior.ContinueHorizontally)
        {
            return new TextLayout(_font, text, new Vector2(width, height), breakType, overflowBehavior).LaidOutText;
        }

        private void AlignText(string text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign, TextVerticalAlignment vAlign, ref float x, ref float y)
        {
            if (hAlign != TextHorizontalAlignment.Left)
            {
                var sz = _font.MeasureString(text);
                if (hAlign == TextHorizontalAlignment.Center)
                {
                    x += areaWidth / 2f;
                    x -= sz.X / 2f;
                }
                else if (hAlign == TextHorizontalAlignment.Right)
                {
                    x += areaWidth;
                    x -= sz.X;
                }
            }
            
            if (vAlign == TextVerticalAlignment.Center)
            {
                y += areaHeight / 2f;
                y -= _font.LineHeight / 2.0f;
            }
            else if (vAlign == TextVerticalAlignment.Bottom)
            {
                y += areaHeight;
                y -= _font.LineHeight;
            }
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
            _context.BeginPath();
            _context.Rect(x1, y1, width, height);
            _context.StrokePaint(_paint);
            _context.Stroke();
        }

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
            if (image is not NanoVGImage img) throw new ArgumentException("Invalid image type for NanoVGBackend.");

            var imgPaint = _context.ImagePattern(x, y, width, height, 0.0f, img.Texture, 1.0f);
            _context.BeginPath();
            _context.FillPaint(imgPaint);
            _context.Rect(x, y, width, height);
            _context.Fill();
        }
    }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }

    public Vector2 Viewport => new(context.GraphicsDevice.Viewport.Width, context.GraphicsDevice.Viewport.Height);
}

internal struct NanoVGFontMetrics(DynamicSpriteFont font) : IFontMetrics
{
    public float StringWidth(string astring)
    {
        return font.MeasureString(astring).X;
    }

    public float Height(string astring)
    {
        return font.MeasureString(astring).Y;
    }
}

public enum BreakType
{
    None,
    Word,
    Character
}

public enum OverflowBehavior
{
    ContinueVertically,
    ContinueHorizontally
}

public readonly struct TextLayout(DynamicSpriteFont font, string text, Vector2 bounds, BreakType breakType = BreakType.Word, OverflowBehavior overflowBehavior = OverflowBehavior.ContinueHorizontally)
{
    public string LaidOutText { get; } = LayoutText(font, text, bounds, breakType, overflowBehavior);

    private static string LayoutText(DynamicSpriteFont font, string text, Vector2 bounds, BreakType breakType, OverflowBehavior overflowBehavior)
    {
        if (breakType == BreakType.None)
        {
            return text;
        }
        
        var sb = new StringBuilder(text.Length);
        var spaceWidth = font.MeasureString(" ").X;
        var lineWidth = 0.0f;
        
        var textHeight = 0f;

        foreach (var wordRange in text.AsSpan().Split(' '))
        {
            var word = text.AsSpan(wordRange);
            
            var wordSize = font.MeasureString(word);

            if (lineWidth + wordSize.X > bounds.X &&
                (textHeight + (wordSize.Y * 2) < bounds.Y || overflowBehavior == OverflowBehavior.ContinueVertically))
            {
                if (breakType == BreakType.Word)
                {
                    sb.Append('\n');
                    textHeight += wordSize.Y;
                    lineWidth = 0.0f;
                }
                else if (breakType == BreakType.Character)
                {
                    foreach (var ch in word)
                    {
                        var charWidth = font.MeasureString([ch]).X;
                        if (lineWidth + charWidth > bounds.X)
                        {
                            sb.Append('\n');
                            textHeight += wordSize.Y;
                            lineWidth = 0.0f;
                        }
                        sb.Append(ch);
                        lineWidth += charWidth;
                    }
                    sb.Append(' ');
                    lineWidth += spaceWidth;
                    continue;
                }
            }

            sb.Append(word).Append(' ');
            lineWidth += wordSize.X + spaceWidth;
        }

        return sb.ToString().TrimEnd();
    }
}

internal class NanoVGImage(Texture2D texture) : IImage
{
    public Texture2D Texture { get; } = texture;
    public int Height => Texture.Height;
    public int Width => Texture.Width;
}