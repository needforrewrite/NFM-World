using System.Collections.Concurrent;
using System.Text;
using Apos.Shapes;
using CommunityToolkit.HighPerformance;
using NFMWorld.Audio;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorld.Util;
using NvgSharp;
using DrawingColor = System.Drawing.Color;
using TextHorizontalAlignment = NFMWorld.DriverInterface.DriverInterface.TextHorizontalAlignment;

namespace NFMWorld;

/// <summary>
/// Owns the <see cref="AbstractionNvgRenderer"/> (NanoVG's <c>INvgRenderer</c> against
/// <see cref="IGraphicsDevice"/>) and the <c>NvgContext</c> built from it. Replaces the old
/// <c>NvgContext(GraphicsDevice, ...)</c> XNA constructor.
/// </summary>
public class AposRenderer : IDisposable
{
    private readonly ShapeBatch _sb;

    public AposRenderer(IGraphicsDevice graphicsDevice)
    {
        _sb = new ShapeBatch(graphicsDevice);
        IBackend.Backend = new WorldClientBackend(_sb, graphicsDevice);
    }

    /// <summary>Must be called with the frame's live command buffer before any drawing is done
    /// through <see cref="IBackend.Backend"/>'s <see cref="IGraphics"/> this frame.</summary>
    public void BeginFrame(ICommandBuffer cb) => _sb.Begin(cb);

    public void Render()
    {
        _sb.End();
    }

    public void Dispose()
    {
        _sb.Dispose();
    }
}

internal sealed class WorldClientBackend(ShapeBatch sb, IGraphicsDevice graphicsDevice) : IBackend
{
    public IRadicalMusic LoadMusic(string file, double tempomul) => new FaudioMusic(file, tempomul);

    public void StopAllSounds() => FaudioSoundClip.StopAll();

    public ISoundClip GetSound(string filePath) => new FaudioSoundClip(filePath);

    public IGraphics Graphics { get; } = new AposGraphics(sb, graphicsDevice);
    
    public sealed class AposGraphics : IGraphics
    {
        private readonly IGraphicsDevice _graphicsDevice;

        public Vector2 Viewport => new(_graphicsDevice.Swapchain.Width, _graphicsDevice.Swapchain.Height);

        public float Scale { get; set; } = 1;
        
        private Color _alpha = new Color(1f, 1f, 1f, 1f);

        public float Alpha
        {
            set
            {
                
                _alpha = new Color(1f, 1f, 1f, value);

                if (_colorOrig.Colors != null)
                {
                    _color = new Gradient(
                        _colorOrig.AXY,
                        _colorOrig.BXY,
                        new ColorRamp(_colorOrig.Colors.Colors.Zip(_colorOrig.Colors.Positions).Select(c => (c.Second, c.First * _alpha)).ToArray()),
                        _color.S,
                        _color.RS,
                        _color.AOffset,
                        _color.BOffset,
                        _color.IsLocal
                    );
                }
                else
                {
                    _color = new Gradient(
                        _colorOrig.AXY,
                        _colorOrig.AC * _alpha,
                        _colorOrig.BXY,
                        _color.BC * _alpha,
                        _color.S,
                        _color.RS,
                        _color.AOffset,
                        _color.BOffset,
                        _color.IsLocal
                    );
                }
            }
        }

        private readonly ShapeBatch _sb;
        private Dictionary<FontFamily, ShapeFont> _fonts = new();
        // Fonts that hold only symbols, stood behind every text font so an arrow or a triangle
        // draws instead of the missing glyph box. Loaded once and shared by all of them, so
        // they are deliberately left out of _fonts and out of the disposal pass.
        private readonly List<ShapeFont> _symbols = new();
        private ConcurrentDictionary<string, IImage> _imageCache = new();

        private Gradient _colorOrig;
        private Gradient _color;
        private Font _font;
        private float _strokeWidth = 0.5f;
        // Canvas tracks whether a point is current for you; this interface doesn't hand one back,
        // so the path builder keeps its own for the Arc overload to join to.
        private bool _hasCurrentPoint;
        private RectangleF? _clipRect;
        private readonly Stack<RectangleF?> _savedClipRects = [];

        public AposGraphics(ShapeBatch sb, IGraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _sb = sb;
            
            LoadSymbolFonts();

            _fonts[FontFamily.DroidSans] = LoadFont("./data/fonts/DroidSans.ttf");
            _fonts[FontFamily.AdventureHollow] = LoadFont("./data/fonts/AdventureHollow.otf");
            _fonts[FontFamily.Adventure] = LoadFont("./data/fonts/Adventure.otf");
            _fonts[FontFamily.RobotoMono] = LoadFont("./data/fonts/RobotoMono-Regular.ttf");
            _fonts[FontFamily.NotoSans] = LoadFont("./data/fonts/NotoSans-Regular.ttf");
        }

        private ShapeFont LoadFont(string fontFile)
        {
            ShapeFont font = new ShapeFont(VFS.ReadAllBytes(fontFile));
            // Order matters: this first, so it covers the arrows, then the second for the
            // geometric shapes and dingbats it has nothing for.
            foreach (ShapeFont symbols in _symbols)
            {
                font.AddFallback(symbols);
            }
            return font;
        }

        // Tolerated if absent: text falls back to the missing glyph box, which is where it
        // started, rather than the whole UI failing to load over a decorative character.
        private void LoadSymbolFonts()
        {
            foreach (string file in (ReadOnlySpan<string>)[
                         "./data/fonts/NotoSansSymbols-Regular.ttf",
                         "./data/fonts/NotoSansSymbols2-Regular.ttf"])
            {
                if (VFS.FileExists(file))
                {
                    _symbols.Add(new ShapeFont(VFS.ReadAllBytes(file)));
                }
            }
        }

        public IImage LoadImage(string file)
        {
            return _imageCache.GetOrAdd(file, _ => LoadImageInternal());

            IImage LoadImageInternal()
            {
                using var stream = VFS.OpenRead(file);
                if (Path.GetExtension(file) == ".svg")
                {
                    return new AposShapeSvg(stream);
                }
                if (Path.GetExtension(file) == ".dds")
                {
                    return new TextureImage(DdsReader.LoadFromStream(_graphicsDevice, stream));
                }

                return new TextureImage(TextureLoader.LoadFromStream(_graphicsDevice, stream));
            }
        }

        public IImage LoadImage(ReadOnlyMemory<byte> file)
        {
            if (file.Span is [(byte)'D', (byte)'D', (byte)'S', (byte)' ', ..])
            {
                return new TextureImage(DdsReader.LoadFromStream(_graphicsDevice, file.AsStream()));
            }

            return new TextureImage(TextureLoader.LoadFromStream(_graphicsDevice, file.AsStream()));
        }

        public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos)
        {
            if (colors.Length != 2)
            {
                if (colorPos == null)
                {
                    _colorOrig = new Gradient(new Vector2(x, y), colors[0], new Vector2(x + width, y + height), colors[1]);
                    _color = new Gradient(new Vector2(x, y), colors[0] * _alpha, new Vector2(x + width, y + height), colors[1] * _alpha);
                }
                else
                {
                    _colorOrig = new Gradient(new Vector2(x, y), new Vector2(x + width, y + height), new ColorRamp([
                        (colorPos[0], colors[0]),
                        (colorPos[1], colors[2]),
                    ]));
                    _color = new Gradient(new Vector2(x, y), new Vector2(x + width, y + height), new ColorRamp([
                        (colorPos[0], colors[0] * _alpha),
                        (colorPos[1], colors[2] * _alpha),
                    ]));
                }
            }
            else
            {
                _colorOrig = new Gradient(
                    new Vector2(x, y),
                    new Vector2(x + width, y + height),
                    new ColorRamp(colors.Select((c, i) => (colorPos?[i] ?? (float)i / (colors.Length - 1), c)).ToArray())
                );
                _color = new Gradient(
                    new Vector2(x, y),
                    new Vector2(x + width, y + height),
                    new ColorRamp(colors.Select((c, i) => (colorPos?[i] ?? (float)i / (colors.Length - 1), c * _alpha)).ToArray())
                );
            }
        }

        public void SetColor(Color c)
        {
            _colorOrig = c;
            _color = c * _alpha;
        }

        public void DrawImage(IImage image, int x, int y)
        {
            if (image is TextureImage img)
            {
                _sb.Draw(img.Texture, new RectangleF(x, y, img.Width, img.Height));
            }
            else if (image is AposShape shape)
            {
                shape.Draw(_sb, x, y, shape.Width, shape.Height);
            }
        }
        
        public void SetFont(Font font)
        {
            _font = font;
        }

        public IFontMetrics GetFontMetrics()
        {
            return new AposFontMetrics(_fonts[_font.FontFamily], _font.Size);
        }

        public IFontMetrics GetFontMetrics(Font font)
        {
            return new AposFontMetrics(_fonts[font.FontFamily], font.Size);
        }

        public void DrawString(ReadOnlySpan<char> text, int x, int y)
        {
            var f = _fonts[_font.FontFamily];
            _sb.DrawString(f, text, new Vector2(x, y - (f.LineHeight * _font.Size)), _font.Size, _color);
        }

        public void DrawStringAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top)
        {
            float xFloat = x;
            float yFloat = y;
            AlignText(text, areaWidth, areaHeight, hAlign, vAlign, ref xFloat, ref yFloat);

            var f = _fonts[_font.FontFamily];
            _sb.DrawString(f, text, new Vector2(xFloat, yFloat - (f.LineHeight * _font.Size)), _font.Size, _color);
        }

        public void DrawStringStroke(ReadOnlySpan<char> text, int x, int y, int effectAmount = 1)
        {
            // dogshit string stroke
            var f = _fonts[_font.FontFamily];
            var pos = new Vector2(x, y - (f.LineHeight * _font.Size));
            foreach (var (ax, ay) in (ReadOnlySpan<(int, int)>)[(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)])
            {
                _sb.DrawString(f, text, pos + new Vector2(ax, ay), _font.Size, _color);
            }
        }

        public void DrawStringStrokeAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        {
            float xFloat = x;
            float yFloat = y;
            AlignText(text, areaWidth, areaHeight, hAlign, vAlign, ref xFloat, ref yFloat);
            
            // dogshit string stroke
            var f = _fonts[_font.FontFamily];
            var pos = new Vector2(xFloat, yFloat - (f.LineHeight * _font.Size));
            foreach (var (ax, ay) in (ReadOnlySpan<(int, int)>)[(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)])
            {
                _sb.DrawString(f, text, pos + new Vector2(ax, ay), _font.Size, _color);
            }
        }

        private void AlignText(ReadOnlySpan<char> text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign, TextVerticalAlignment vAlign, ref float x, ref float y)
        {
            var font = _fonts[_font.FontFamily];
            
            if (hAlign != TextHorizontalAlignment.Left)
            {
                var sz = font.MeasureString(text, _font.Size);
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
                y -= (font.LineHeight * _font.Size) / 2.0f;
            }
            else if (vAlign == TextVerticalAlignment.Bottom)
            {
                y += areaHeight;
                y -= (font.LineHeight * _font.Size);
            }
        }

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
            if (image is TextureImage img)
            {
                _sb.Draw(img.Texture, new RectangleF(x, y, width, height));
            }
            else if (image is AposShape shape)
            {
                shape.Draw(_sb, x, y, width, height);
            }
        }

        public void BeginPath()
        {
            _sb.BeginShapePath();
            _hasCurrentPoint = false;
        }

        public void MoveTo(float x, float y)
        {
            _sb.ShapeMoveTo(new Vector2(x, y));
            _hasCurrentPoint = true;
        }

        public void LineTo(float x, float y)
        {
            _sb.ShapeLineTo(new Vector2(x, y));
            _hasCurrentPoint = true;
        }

        public void BezierTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            _sb.ShapeCubicTo(new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(x, y));
            _hasCurrentPoint = true;
        }

        public void ClosePath()
        {
            _sb.ShapeClose();
        }

        public void MarkHole()
        {
            _sb.ShapeMarkHole();
        }

        public void Stroke()
        {
            // The width is already a radius here, and the batch takes a radius too, so this does
            // not go through the halving the shape painter's width overload does.
            _sb.StrokeShapeRadius(_sb.EndShapePath(), _color, _strokeWidth);
            _hasCurrentPoint = false;
        }

        public void Fill()
        {
            _sb.FillShape(_sb.EndShapePath(), _color);
            _hasCurrentPoint = false;
        }

        // Canvas semantics: an arc joins the current point if there is one, otherwise it starts
        // there. IGraphics keeps no current point of its own, so this tracks one, the same way
        // callers of the Ellipse overload that takes a `ref` are expected to.
        public void Arc(float cx, float cy, float arcRadius, float startAngleDeg, float endAngleDeg, bool clockWise)
        {
            if (!_sb.HasOpenShapePath) BeginPath();
            float a0 = startAngleDeg * (MathF.PI / 180f);
            float a1 = endAngleDeg * (MathF.PI / 180f);
            var start = new Vector2(cx + arcRadius * MathF.Cos(a0), cy + arcRadius * MathF.Sin(a0));

            // Angles run the way the interface's own corner arcs run them: clockwise is increasing
            // angle, which is the sense a y-down space gives it. Going the other way means going
            // round by the long way.
            float da = a1 - a0;
            if (clockWise) {
                while (da < 0f) da += 2f * MathF.PI;
            } else {
                while (da > 0f) da -= 2f * MathF.PI;
            }

            if (_hasCurrentPoint) LineTo(start.X, start.Y);
            else MoveTo(start.X, start.Y);

            // Sweep matches the flag directly: positive angles are clockwise in a y-down space,
            // which is SVG's positive sweep. A full turn cannot be told from a zero one by its
            // endpoints alone, so it is split into two half turns, which the endpoint
            // parameterization can express. Each piece gets its own flags, or the half-turn halves
            // would each be handed back the full turn's answer.
            int pieces = MathF.Abs(da) > MathF.Tau - 1e-4f ? 2 : 1;
            float step = da / pieces;
            for (int i = 1; i <= pieces; i++) {
                float a = a0 + step * i;
                _sb.ShapeArcTo(new Vector2(cx + arcRadius * MathF.Cos(a), cy + arcRadius * MathF.Sin(a)),
                               arcRadius, arcRadius, 0f, MathF.Abs(step) > MathF.PI, clockWise);
            }
        }

        public void LineCapButt()
        {
        }

        public void SaveState()
        {
            _savedClipRects.Push(_clipRect);
        }

        public void RestoreState()
        {
            _clipRect = _savedClipRects.Pop();
            _sb.SetClipRect(_clipRect);
        }

        public void Scissor(float x, float y, float w, float h)
        {
            _clipRect = new RectangleF(x, y, w, h);
        }

        public void IntersectScissor(float x, float y, float w, float h)
        {
            _clipRect = RectangleF.Intersect(_clipRect ?? new RectangleF(0, 0, float.MaxValue, float.MaxValue), new RectangleF(x, y, w, h));
            _sb.SetClipRect(_clipRect);
        }

        public void ResetScissor()
        {
            _clipRect = null;
            _sb.SetClipRect(_clipRect);
        }

        public void SetStrokeWidth(float width = 1)
        {
            _strokeWidth = width * 0.5f;
        }

        public void DrawRect(int x1, int y1, int width, int height)
        {
            _sb.DrawRectangle(new Vector2(x1, y1), new Vector2(width, height), Color.Transparent, _color, _strokeWidth, new CornerRadii());
        }

        public void DrawVariableBorderRect(float x, float y, float width, float height, float top, float right, float bottom,
            float left, CornerRadius tl, CornerRadius tr, CornerRadius br, CornerRadius bl)
        {
            _sb.DrawRectangle(new Vector2(x, y), new Vector2(width, height), Color.Transparent, _color, _strokeWidth, new CornerRadii
                ((tl.Rx + tl.Ry) / 2,
                (tr.Rx + tr.Ry) / 2,
                (br.Rx + br.Ry) / 2,
                (bl.Rx + bl.Ry) / 2
            ));
        }

        public void FillVariableBorderRect(float x, float y, float width, float height, float top, float right, float bottom,
            float left, CornerRadius tl, CornerRadius tr, CornerRadius br, CornerRadius bl)
        {
            _sb.DrawRectangle(new Vector2(x, y), new Vector2(width, height), _color, Color.Transparent, 0, new CornerRadii
            ((tl.Rx + tl.Ry) / 2,
                (tr.Rx + tr.Ry) / 2,
                (br.Rx + br.Ry) / 2,
                (bl.Rx + bl.Ry) / 2
            ));
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
            _sb.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), _strokeWidth, _color, _color);
        }

        public void DrawRoundedRect(int x, int y, int width, int height, float radTopLeft, float radTopRight, float radBottomRight,
            float radBottomLeft)
        {
            _sb.DrawRectangle(new Vector2(x, y), new Vector2(width, height), Color.Transparent, _color, _strokeWidth,
                new CornerRadii(radTopLeft, radTopRight, radBottomRight, radBottomLeft));
        }

        public void FillRoundedRect(int x, int y, int width, int height, float radTopLeft, float radTopRight, float radBottomRight,
            float radBottomLeft)
        {
            _sb.DrawRectangle(new Vector2(x, y), new Vector2(width, height), _color, Color.Transparent, 0,
                new CornerRadii(radTopLeft, radTopRight, radBottomRight, radBottomLeft));
        }

        public void FillRect(int x, int y, int width, int height)
        {
            _sb.DrawRectangle(new Vector2(x, y), new Vector2(width, height), _color, Color.Transparent, 0, new CornerRadii());
        }

        public void FillPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
        {
            Span<Vector2> points = stackalloc Vector2[n];
            for (var i = 0; i < n; i++)
            {
                points[i] = new Vector2(x[i], y[i]);
            }

            _sb.DrawPath(points, 0, _color, Color.Transparent, 0);
        }

        public void DrawPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
        {
            Span<Vector2> points = stackalloc Vector2[n];
            for (var i = 0; i < n; i++)
            {
                points[i] = new Vector2(x[i], y[i]);
            }

            _sb.DrawPath(points, 0, Color.Transparent, _color, _strokeWidth);
        }
    }

    public void SetAllVolumes(float vol) => FaudioSoundClip.SetAllVolumes(vol);

    /// <summary>
    /// Layout-independent ("physical") key for the same press - used by the UI's
    /// <c>KeyboardEvent.KeyCode</c>. This used to round-trip through FNA's
    /// <c>Keyboard.GetKeyFromScancodeEXT</c> to translate a logical XNA key into its physical
    /// position; SDL already hands us scancode-derived keys (<c>SdlKeyMap.FromScancode</c>), so the
    /// key arriving here *is* the physical one and there is nothing left to translate.
    /// </summary>
    public Key GetKeyFromScancode(Key key)
    {
        return key;
    }
}

internal abstract class AposShape : IImage
{
    public abstract void Draw(ShapeBatch sb, float x, float y, float width, float height);
    public abstract int Height { get; }
    public abstract int Width { get; }
}

internal class AposShapeSvg(Stream stream) : AposShape
{
    private readonly ShapeSvg _shape = new(stream);

    public override void Draw(ShapeBatch sb, float x, float y, float width, float height)
    {
        sb.DrawSvg(_shape, new Vector2(x, y), height);
    }

    public override int Height => (int)_shape.Height;
    public override int Width => (int)_shape.Width;
}

internal readonly struct AposFontMetrics(ShapeFont font, float size) : IFontMetrics
{
    public Vector2 MeasureText(ReadOnlySpan<char> text)
    {
        return font.MeasureString(text, size);
    }
    
    public float LineHeight => font.LineHeight * size;
}

internal class TextureImage(ITexture texture) : IImage
{
    public ITexture Texture { get; } = texture;
    public int Height => Texture.Height;
    public int Width => Texture.Width;
}