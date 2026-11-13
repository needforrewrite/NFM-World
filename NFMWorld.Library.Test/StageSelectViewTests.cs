using System.Numerics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;

namespace NFMWorld.Library.Test;

/// <summary>
/// The stage selector as the lobby's create dialog mounts it. The dialog bounds its own height
/// and hands the leftover space to the compact selector, so the list has to scroll: without a
/// bound the panel grew past the window and the rows below the fold were unreachable.
/// </summary>
[TestClass]
[DoNotParallelize]
public class StageSelectViewTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new MetricsBackend();
    }

    [TestCleanup]
    public void Cleanup() => UiScheduler.Instance.Flush();

    [TestMethod]
    public void CompactInBoundedDialog_GivesTheListAViewportThatScrolls()
    {
        var root = MountCompactInDialog(18, windowHeight: 640, dialogMaxHeight: 512);

        var list = Find(root, "Stage select list");
        Assert.IsGreaterThan(0f, list.LayoutBorderSize.Y, "the list collapsed to nothing");
        Assert.IsGreaterThan(
            0f,
            list.ScrollableHeight,
            "18 stages did not overflow their viewport, so there was nothing to scroll"
        );

        var dialog = Find(root, "Create dialog");
        Assert.IsLessThanOrEqualTo(
            0.5f,
            dialog.ScrollableHeight,
            $"the dialog itself overflowed by {dialog.ScrollableHeight} instead of holding the list"
        );
    }

    [TestMethod]
    public void CompactInShortDialog_KeepsAUsableList_AndLetsTheDialogScroll()
    {
        var root = MountCompactInDialog(18, windowHeight: 340, dialogMaxHeight: 220);

        var compact = Find(root, "Stage select compact");
        Assert.IsGreaterThanOrEqualTo(
            200f,
            compact.LayoutBorderSize.Y,
            $"the selector was squeezed to {compact.LayoutBorderSize.Y}, so only a sliver of rows showed"
        );

        var list = Find(root, "Stage select list");
        Assert.IsGreaterThan(0f, list.ScrollableHeight, "the list had no rows left to scroll");

        var dialog = Find(root, "Create dialog");
        Assert.IsGreaterThan(0f, dialog.ScrollableHeight, "the dialog did not take over scrolling in a short window");
    }

    /// <summary>
    /// Only a host that can search the archive gets the LOCAL/ONLINE toggle; free play has no
    /// archive search, so a toggle there would be a button that cannot do anything.
    /// </summary>
    [TestMethod]
    public void Compact_ShowsTheSourcePicker_OnlyWhenTheHostCanSearchTheArchive()
    {
        var withSearch = MountCompactInDialog(4, windowHeight: 640, dialogMaxHeight: 512, archiveSearch: true);
        Assert.IsGreaterThan(0f, Find(withSearch, "Stage select source").LayoutBorderSize.Y);
        Find(withSearch, "Stage select source online");
        Find(withSearch, "Stage select source local");

        var withoutSearch = MountCompactInDialog(4, windowHeight: 640, dialogMaxHeight: 512, archiveSearch: false);
        var picker = Find(withoutSearch, "Stage select source");
        Assert.AreEqual(
            0f,
            picker.LayoutBorderSize.Y,
            "the picker still reserved height, so a local-only host shows a gap it cannot use"
        );
    }

    /// <summary>
    /// The dialog as the lobby builds it: bounded by the window, with the compact selector as the
    /// only child allowed to give up space.
    /// </summary>
    private static Component MountCompactInDialog(
        int stageCount,
        float windowHeight,
        float dialogMaxHeight,
        bool archiveSearch = false
    )
    {
        var state = new StageSelectState();
        state.Stages.Value = Enumerable.Range(1, stageCount)
            .Select(i => new StageOption($"nfm1/{i}_stage", $"Stage {i}"))
            .ToArray();
        state.Selected.Value = state.Stages.Value[0].Id;

        using var mounted = Renderer.Mount(() => Sx.View(
            style: () => Sx.Styles(
                width: 460,
                height: windowHeight,
                flexDirection: FlexDirection.Column,
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            children: Sx.View(
                name: () => "Create dialog",
                style: () => Sx.Styles(
                    width: 460,
                    maxHeight: dialogMaxHeight,
                    flexDirection: FlexDirection.Column,
                    gapRow: 10,
                    overflow: Overflow.Scroll,
                    paddingTop: 18,
                    paddingBottom: 16,
                    paddingLeft: 20,
                    paddingRight: 28
                ),
                children:
                [
                    Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 20),
                        layout: () => Sx.Styles(flexShrink: 0),
                        text: () => "CREATE GAME"
                    ),
                    StageSelectView.Compact(
                        state,
                        new StageSelectView.Actions(
                            CollectionSelected: _ => { },
                            StageSelected: id => state.Selected.Value = id,
                            SearchChanged: _ => { },
                            Confirm: () => { },
                            Back: () => { },
                            SourceSelected: archiveSearch ? _ => { } : null,
                            OnlineSearch: archiveSearch ? _ => { } : null
                        )
                    )
                ]
            )
        ));

        Renderer.Flush();
        var root = (Component)mounted.Root;
        root.LayoutAndRender(new Vector2(460, windowHeight));
        return root;
    }

    private static Component Find(Component root, string name)
        => Descendants(root).Single(node => node.Name == name);

    private static IEnumerable<Component> Descendants(Component parent)
    {
        foreach (var child in parent.VisualChildren.OfType<Component>())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    /// <summary>A backend whose text has a real line height, which row sizing depends on.</summary>
    private sealed class MetricsBackend : IBackend
    {
        private readonly DummyBackend _inner = new();
        private readonly MetricsGraphics _graphics = new();

        public IGraphics Graphics => _graphics;
        public IGraphics3D Graphics3D => _inner.Graphics3D;
        public IRadicalMusic LoadMusic(string file, double tempomul = 1) => _inner.LoadMusic(file, tempomul);
        public void StopAllSounds() => _inner.StopAllSounds();
        public ISoundClip GetSound(string filePath) => _inner.GetSound(filePath);
        public void SetAllVolumes(float vol) => _inner.SetAllVolumes(vol);
        public Key GetKeyFromScancode(Key key) => _inner.GetKeyFromScancode(key);
    }

    /// <summary>Only the metrics matter here; drawing goes nowhere.</summary>
    private sealed class MetricsGraphics : IGraphics
    {
        public Vector2 Viewport => new(1280, 720);
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
