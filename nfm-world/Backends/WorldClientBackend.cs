using System.Collections.Concurrent;
using System.Text;
using CommunityToolkit.HighPerformance;
using FontStashSharp;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NFMWorld.Audio;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Util;
using NvgSharp;
using TextHorizontalAlignment = NFMWorld.DriverInterface.DriverInterface.TextHorizontalAlignment;

namespace NFMWorld;

public class NanoVGRenderer
{
    private NvgContext _context;

    public NanoVGRenderer(GraphicsDevice graphicsDevice)
    {
        _context = new NvgContext(graphicsDevice, false);
        IBackend.Backend = new WorldClientBackend(_context);
    }

    public void Render()
    {
        _context.Flush();
    }
}

internal sealed class WorldClientBackend(NvgContext context) : IBackend
{
    public IRadicalMusic LoadMusic(string file, double tempomul)
    {
#if USE_FAUDIO
        return new FaudioMusic(file, tempomul);
#else
        return new RadicalMusic(file, tempomul);
#endif
    }

    public void StopAllSounds()
    {
#if USE_FAUDIO
        FaudioSoundClip.StopAll();
#else
        SoundClip.StopAll();
#endif
    }

    public ISoundClip GetSound(string filePath)
    {
#if USE_FAUDIO
        return new FaudioSoundClip(filePath);
#else
        return new SoundClip(filePath);
#endif
    }

    public IGraphics Graphics { get; } = new NvgGraphics(context);

    public sealed class NvgGraphics : IGraphics
    {
        public Vector2 Viewport => new(_context.GraphicsDevice.Viewport.Width, _context.GraphicsDevice.Viewport.Height);

        public float Scale { get; set; } = 1;

        private ConcurrentDictionary<string, IImage> _imageCache = new();

        private Paint _paint;
        private float layerDepth = 0.0f;
        private float characterSpacing = 0.0f;
        private float lineSpacing = 0.0f;
        private TextStyle textStyle = TextStyle.None;
        private FontSystemEffect effect = FontSystemEffect.None;
        private int effectAmount = 1;
        private readonly NvgContext _context;

        private Color _color1;
        private Color _color2;
        private float _alpha = 1.0f;

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

        public IImage LoadImage(string file)
        {
            return _imageCache.GetOrAdd(file, _ => LoadImageInternal());

            IImage LoadImageInternal()
            {
                using var stream = VFS.OpenRead(file);
                if (Path.GetExtension(file) == ".svg")
                {
                    return NanoSVGImage.FromStream(stream);
                }
                if (Path.GetExtension(file) == ".dds")
                {
                    return new NanoVGImage(Texture2D.DDSFromStreamEXT(_context.GraphicsDevice, stream));
                }

                return new NanoVGImage(Texture2D.FromStream(_context.GraphicsDevice, stream));
            }
        }

        public IImage LoadImage(ReadOnlyMemory<byte> file)
        {
            if (file.Span is [(byte)'<', (byte)'s', (byte)'v', (byte)'g', ..] or [(byte)'<', (byte)'?', (byte)'x', (byte)'m', (byte)'l', ..])
            {
                return NanoSVGImage.FromStream(file.AsStream());
            }

            if (file.Span is [(byte)'D', (byte)'D', (byte)'S', (byte)' ', ..])
            {
                return new NanoVGImage(Texture2D.DDSFromStreamEXT(_context.GraphicsDevice, file.AsStream()));
            }

            return new NanoVGImage(Texture2D.FromStream(_context.GraphicsDevice, file.AsStream()));
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

            _color1 = colors[0];
            _color2 = colors[1];
            var icol = colors[0] with { A = (byte)(_color1.A / 255f * _alpha * 255f) };
            var ocol = colors[1] with { A = (byte)(_color2.A / 255f * _alpha * 255f) };

            var gradientPaint = _context.LinearGradient(x, y, x + width, y + height, icol, ocol);
            _paint = gradientPaint;
            _context.FillPaint(_paint);
            _context.StrokePaint(_paint);
        }

        public void SetStrokeWidth(float width = 1f)
        {
            _context.StrokeWidth(width);
        }

        public void SetColor(Color c)
        {
            _color1 = c;
            _color2 = c;

            c = c with { A = (byte)(_color1.A / 255f * _alpha * 255f) };

            _paint = new Paint(c);
            _context.FillPaint(_paint);
            _context.StrokePaint(_paint);
        }

        public void BeginPath()
        {
            _context.BeginPath();
        }

        public void MoveTo(float x, float y)
        {
            _context.MoveTo(x, y);
        }

        public void LineTo(float x, float y)
        {
            _context.LineTo(x, y);
        }

        public void BezierTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            _context.BezierTo(c1x, c1y, c2x, c2y, x, y);
        }

        public void ClosePath()
        {
            _context.ClosePath();
        }

        public void MarkHole()
        {
            _context.PathWinding(Solidity.Hole);
        }

        public void Stroke()
        {
            _context.Stroke();
        }

        public void Fill()
        {
            _context.Fill();
        }

        public float Alpha
        {
            set
            {
                _alpha = value;

                var icol = _color1 with { A = (byte)(_color1.A / 255f * _alpha * 255f) };
                var ocol = _color2 with { A = (byte)(_color2.A / 255f * _alpha * 255f) };
                _paint.InnerColor = icol;
                _paint.OuterColor = ocol;
                _context.FillPaint(_paint);
                _context.StrokePaint(_paint);
            }
        }

        public void DrawImage(IImage image, int x, int y)
        {
            if (image is not NanoVGImage and not NanoSVGImage) throw new ArgumentException("Invalid image type for NanoVGBackend.");

            if (image is NanoVGImage img)
            {
                var imgPaint = _context.ImagePattern(x, y, img.Width, img.Height, 0.0f, img.Texture, 1.0f);
                _context.BeginPath();
                _context.FillPaint(imgPaint);
                _context.Rect(x, y, img.Width, img.Height);
                _context.Fill();
                _context.FillPaint(_paint);
            }
            else if (image is NanoSVGImage nsvg)
            {
                nsvg.Draw(_context, x, y, nsvg.Width, nsvg.Height);
            }
        }

        public void SetFont(Font font)
        {
            _font = _fontSystems[font.FontFamily].GetFont(font.Size);
        }

        public IFontMetrics GetFontMetrics()
        {
            return new NanoVGFontMetrics(_font);
        }

        public IFontMetrics GetFontMetrics(Font font)
        {
            return new NanoVGFontMetrics(_fontSystems[font.FontFamily].GetFont(font.Size));
        }

        public void DrawString(ReadOnlySpan<char> text, int x, int y)
        {
            _context.Text(_font, text, x, y - _font.FontSize, layerDepth, characterSpacing, lineSpacing, textStyle, effect, effectAmount);
        }

        public void DrawStringAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top)
        {
            float xFloat = x;
            float yFloat = y;
            AlignText(text, areaWidth, areaHeight, hAlign, vAlign, ref xFloat, ref yFloat);

            _context.Text(_font, text, xFloat, yFloat, layerDepth, characterSpacing, lineSpacing, textStyle, effect, effectAmount);
        }

        public void DrawStringStroke(ReadOnlySpan<char> text, int x, int y, int effectAmount = 1)
        {
            _context.Text(_font, text, x, y - _font.FontSize, layerDepth, characterSpacing, lineSpacing, textStyle, FontSystemEffect.Stroked, effectAmount);
        }

        public void DrawStringStrokeAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        {
            float xFloat = x;
            float yFloat = y;
            AlignText(text, areaWidth, areaHeight, hAlign, vAlign, ref xFloat, ref yFloat);

            _context.Text(_font, text, xFloat, yFloat, layerDepth, characterSpacing, lineSpacing, textStyle, FontSystemEffect.Stroked, effectAmount);
        }

        private void AlignText(ReadOnlySpan<char> text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign, TextVerticalAlignment vAlign, ref float x, ref float y)
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

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
            if (image is not NanoVGImage and not NanoSVGImage) throw new ArgumentException("Invalid image type for NanoVGBackend.");

            if (image is NanoVGImage img)
            {
                var imgPaint = _context.ImagePattern(x, y, width, height, 0.0f, img.Texture, 1.0f);
                _context.BeginPath();
                _context.FillPaint(imgPaint);
                _context.Rect(x, y, width, height);
                _context.Fill();
                _context.FillPaint(_paint);
            }
            else if (image is NanoSVGImage nsvg)
            {
                nsvg.Draw(_context, x, y, width, height);
            }
        }
    }

    public void SetAllVolumes(float vol)
    {
#if USE_FAUDIO
        FaudioSoundClip.SetAllVolumes(vol);
#else
        SoundClip.SetAllVolumes(vol);
#endif
    }

    public Key GetKeyFromScancode(Key key)
    {
        return Key.FromScanCode(key);
    }
}

internal readonly struct NanoVGFontMetrics(DynamicSpriteFont font) : IFontMetrics
{
    public Vector2 MeasureText(ReadOnlySpan<char> text)
    {
        return font.MeasureString(text);
    }

    public float LineHeight => font.LineHeight;
}

internal class NanoVGImage(Texture2D texture) : IImage
{
    public Texture2D Texture { get; } = texture;
    public int Height => Texture.Height;
    public int Width => Texture.Width;
}