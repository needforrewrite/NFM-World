using System.Numerics;
using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards flex shrinking inside a scroll container (facebook/yoga#2038).
///
/// Once the first pass freezes items the shrink total can be a small positive float residual
/// instead of 0. Dividing by it produced astronomically large child sizes: every row of the
/// lobby's stage list came out hundreds of millions of pixels tall, so the list drew as one
/// solid block with no text.
/// </summary>
[TestClass]
public class YogaScrollFlexTests
{
    [TestInitialize]
    public void Setup()
    {
        // Style values are scaled by the UI scale; the layout that overflowed came from a window
        // whose height put the scale here.
        IBackend.Backend = new MetricsBackend { Scale = 1.4736f };
    }

    [TestMethod]
    public void ShrinkingRowsInScrollContainer_KeepSaneHeights()
    {
        var stages = new List<string>();
        for (var i = 0; i < 20; i++)
            stages.Add("stage_" + i);

        var root = Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, width: 460, height: 600),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        maxHeight: 20,
                        gapRow: 4,
                        overflow: Overflow.Scroll
                    ),
                    children: Sx.For(
                        each: () => stages,
                        row: (stage, _) => Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                alignItems: Align.Center,
                                height: 30,
                                paddingTop: 6,
                                paddingBottom: 6,
                                paddingLeft: 8,
                                paddingRight: 8
                            ),
                            children: [Sx.Text(style: () => Sx.TextStyles(fontSize: 13), text: () => stage)]
                        )
                    )
                )
            ]
        );

        ((Component)root).LayoutAndRender(new Vector2(460, 600));

        var list = (Component)((Component)root).VisualChildren[0];
        var rows = list.VisualChildren.SelectMany(row => row.VisualChildren).OfType<Component>().ToArray();

        Assert.AreEqual(20, rows.Length, "every stage rendered a row");
        foreach (var row in rows)
        {
            Assert.IsTrue(
                row.LayoutHeight < 100f,
                $"a row laid out {row.LayoutHeight}px tall - a shrink residual was divided by"
            );
        }
    }

    /// <summary>A backend whose text has a real line height, which the shrink path needs.</summary>
    private sealed class MetricsBackend : IBackend
    {
        private readonly DummyBackend _inner = new();
        private readonly MetricsGraphics _graphics = new();

        public float Scale
        {
            get => _graphics.Scale;
            set => _graphics.Scale = value;
        }

        public IGraphics Graphics => _graphics;
        public IGraphics3D Graphics3D => _inner.Graphics3D;
        public IRadicalMusic LoadMusic(string file, double tempomul = 1) => _inner.LoadMusic(file, tempomul);
        public void StopAllSounds() => _inner.StopAllSounds();
        public ISoundClip GetSound(string filePath) => _inner.GetSound(filePath);
        public void SetAllVolumes(float vol) => _inner.SetAllVolumes(vol);
        public Key GetKeyFromScancode(Key key) => _inner.GetKeyFromScancode(key);
    }

    private sealed class MetricsGraphics : IGraphics
    {
        public Vector2 Viewport => new(1920, 1080);
        public float Scale { get; set; } = 1;

        public IFontMetrics GetFontMetrics() => new Metrics(12);
        public IFontMetrics GetFontMetrics(Font font) => new Metrics(font.Size);

        public void SetFont(Font font)
        {
        }

        public IImage LoadImage(string file) => new BlankImage();
        public IImage LoadImage(ReadOnlyMemory<byte> file) => new BlankImage();
        public IImage LoadSvgImage(string file, Vector3 rgbMultiplier, byte maximumChannel = 255) => new BlankImage();

        public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos)
        {
        }

        public void SetColor(Color c)
        {
        }

        public float Alpha
        {
            set { }
        }

        public void DrawImage(IImage image, int x, int y)
        {
        }

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
        }

        public void DrawString(ReadOnlySpan<char> text, int x, int y)
        {
        }

        public void DrawStringOutlined(ReadOnlySpan<char> text, int x, int y, Color outline, float outlineWidth)
        {
        }

        public void DrawStringAligned(
            ReadOnlySpan<char> text,
            int x,
            int y,
            int areaWidth,
            int areaHeight,
            TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left,
            TextVerticalAlignment vAlign = TextVerticalAlignment.Top
        )
        {
        }

        public void DrawStringOutlinedAligned(
            ReadOnlySpan<char> text,
            int x,
            int y,
            int areaWidth,
            int areaHeight,
            Color outline,
            float outlineWidth,
            TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left,
            TextVerticalAlignment vAlign = TextVerticalAlignment.Top
        )
        {
        }

        public void BeginPath()
        {
        }

        public void MoveTo(float x, float y)
        {
        }

        public void LineTo(float x, float y)
        {
        }

        public void BezierTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
        }

        public void ClosePath()
        {
        }

        public void MarkHole()
        {
        }

        public void Stroke()
        {
        }

        public void Fill()
        {
        }

        public void Arc(float cx, float cy, float arcRadius, float startAngleDeg, float endAngleDeg, bool clockWise)
        {
        }

        public void LineCapButt()
        {
        }

        public void SaveState()
        {
        }

        public void RestoreState()
        {
        }

        public void Scissor(float x, float y, float w, float h)
        {
        }

        public void IntersectScissor(float x, float y, float w, float h, float cornerRadius = 0f)
        {
        }

        public void ResetScissor()
        {
        }
    }

    private sealed class Metrics(float size) : IFontMetrics
    {
        public Vector2 MeasureText(ReadOnlySpan<char> text) => new(text.Length * size * 0.55f, size * 1.36f);
        public float LineHeight => size * 1.36f;
    }

    private sealed class BlankImage : IImage
    {
        public int Height => 1;
        public int Width => 1;
    }
}
