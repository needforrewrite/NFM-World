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
        FocusManager.ClearFocus();
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

    [TestMethod]
    public void ArrowsSwitchListsAndNavigateOnlyTheActiveList()
    {
        var state = new StageSelectState();
        state.Collections.Value = ["first", "second", "third"];
        state.Collection.Value = "first";
        state.Stages.Value = [new("a", "A"), new("b", "B"), new("c", "C")];
        state.Selected.Value = "a";
        state.ConfirmEnabled.Value = true;
        var confirms = 0;
        var actions = NavigationActions(state, () => confirms++);

        StageSelectView.HandleKey(state, actions, Key.Left);
        StageSelectView.HandleKey(state, actions, Key.Up);
        Assert.AreEqual("third", state.Collection.Value);
        Assert.AreEqual("a", state.Selected.Value);
        StageSelectView.HandleKey(state, actions, Key.Down);
        Assert.AreEqual("first", state.Collection.Value);
        StageSelectView.HandleKey(state, actions, Key.Enter);
        Assert.IsFalse(state.BrowsingCollections.Value);
        Assert.AreEqual(0, confirms);
        StageSelectView.HandleKey(state, actions, Key.Up);
        Assert.AreEqual("c", state.Selected.Value);
        StageSelectView.HandleKey(state, actions, Key.Down);
        Assert.AreEqual("a", state.Selected.Value);
        StageSelectView.HandleKey(state, actions, Key.Enter);
        Assert.AreEqual(1, confirms);
    }

    [TestMethod]
    public void EmptyStageListsStillAllowCategoryNavigationAndTextFieldsKeepCaretKeys()
    {
        var state = new StageSelectState();
        state.Collections.Value = ["first", "second"];
        state.Collection.Value = "first";
        var actions = NavigationActions(state);
        StageSelectView.HandleKey(state, actions, Key.Left);
        StageSelectView.HandleKey(state, actions, Key.Down);
        Assert.AreEqual("second", state.Collection.Value);

        new TextInput().Focus();
        Assert.IsFalse(StageSelectView.HandleKey(state, actions, Key.Right));
        Assert.IsTrue(state.BrowsingCollections.Value);
        FocusManager.ClearFocus();
        state.OnlineSource.Value = true;
        StageSelectView.HandleKey(state, actions, Key.Left);
        Assert.IsFalse(state.BrowsingCollections.Value);
    }

    [TestMethod]
    public void KeyboardSelectionsScrollBothListsIntoView()
    {
        var state = new StageSelectState();
        state.Collections.Value = Enumerable.Range(0, 20).Select(i => $"collection{i}").ToArray();
        state.Collection.Value = "collection0";
        state.Stages.Value = Enumerable.Range(0, 20).Select(i => new StageOption($"stage{i}", $"Stage {i}")).ToArray();
        state.Selected.Value = "stage0";
        var actions = NavigationActions(state);
        using var mounted = Renderer.Mount(() => StageSelectView.Build(state, actions));
        var root = (Component)mounted.Root;
        root.LayoutAndRender(new Vector2(1280, 480));
        StageSelectView.HandleKey(state, actions, Key.Left);
        StageSelectView.HandleKey(state, actions, Key.Up);
        UiScheduler.Instance.Flush();
        root.LayoutAndRender(new Vector2(1280, 480));
        Assert.IsTrue(Find(root, "Stage select collections").ScrollTop > 0);

        StageSelectView.HandleKey(state, actions, Key.Right);
        StageSelectView.HandleKey(state, actions, Key.Up);
        UiScheduler.Instance.Flush();
        root.LayoutAndRender(new Vector2(1280, 480));
        Assert.IsTrue(Find(root, "Stage select list").ScrollTop > 0);
    }

    [TestMethod]
    public void StageTitleLengthDoesNotMoveSearchOrListAndEmptyMessagesLeaveNoExtraGap()
    {
        var state = new StageSelectState();
        state.Collections.Value = ["nfm1"];
        state.Collection.Value = "nfm1";
        state.Stages.Value =
        [
            new("short", "Short"),
            new("long", "A very long stage title " + new string('W', 120))
        ];
        state.Selected.Value = "short";
        using var mounted = Renderer.Mount(() => StageSelectView.Build(state, NavigationActions(state)));
        var root = (Component)mounted.Root;
        root.LayoutAndRender(new Vector2(1280, 720));
        var search = Find(root, "Stage select search");
        var list = Find(root, "Stage select list");
        var searchTop = search.LayoutBorderPosition.Y;
        var listTop = list.LayoutBorderPosition.Y;
        Assert.AreEqual(
            14 * TheGraphics.G.Scale,
            listTop - searchTop - search.LayoutBorderSize.Y,
            0.5f
        );

        foreach (var selected in new[] { "long", "short" })
        {
            state.Selected.Value = selected;
            UiScheduler.Instance.Flush();
            root.LayoutAndRender(new Vector2(1280, 720));
            UiScheduler.Instance.Flush();
            root.LayoutAndRender(new Vector2(1280, 720));
            Assert.AreEqual(searchTop, search.LayoutBorderPosition.Y, 0.5f);
            Assert.AreEqual(listTop, list.LayoutBorderPosition.Y, 0.5f);
        }
    }

    [TestMethod]
    public void PreviewLoading_CoversTheViewport_AndClearsWhenWorkEnds()
    {
        var state = new StageSelectState();
        using var mounted = Renderer.Mount(() => StageSelectView.Preview(state, NavigationActions(state)));
        var root = (Component)mounted.Root;
        var viewport = new Vector2(1280, 720);
        foreach (var busy in new[] { true, false, true, false })
        {
            state.Busy.Value = busy;
            UiScheduler.Instance.Flush();
            root.LayoutAndRender(viewport);
            var overlay = Descendants(root).SingleOrDefault(node => node.Name == "Stage preview loading");
            if (busy)
            {
                Assert.IsNotNull(overlay);
                Assert.AreEqual(viewport.X, overlay.LayoutBorderSize.X, 0.5f);
                Assert.AreEqual(viewport.Y, overlay.LayoutBorderSize.Y, 0.5f);
            }
            else Assert.IsNull(overlay);
        }
    }

    [TestMethod]
    public void PreviewFloatingDescription_FollowsTheStagesBriefing()
    {
        var state = new StageSelectState();
        state.Stages.Value = [new StageOption("stage1", "Stage 1"), new StageOption("stage2", "Stage 2")];
        state.Selected.Value = "stage1";
        using var mounted = Renderer.Mount(() => StageSelectView.Preview(state, NavigationActions(state)));
        var root = (Component)mounted.Root;
        var viewport = new Vector2(1280, 720);

        root.LayoutAndRender(viewport);
        var block = Find(root, "Stage floating description");
        Assert.AreEqual(0f, block.LayoutBorderSize.Y, 0.5f, "a stage with no desc() must not float a blank block");

        state.DescriptionLines.Value = ["A briefing that floats", "", "Second line"];
        UiScheduler.Instance.Flush();
        root.LayoutAndRender(viewport);

        block = Find(root, "Stage floating description");
        // Three 20px rows plus the 2px gaps: the blank line is a gap, not a collapsed row.
        Assert.AreEqual(64f, block.LayoutBorderSize.Y, 0.5f);
        Assert.IsLessThan(100f, block.LayoutBorderPosition.X, "the briefing should hug the preview's left edge");

        var footer = Find(root, "Stage preview footer");
        Assert.IsLessThanOrEqualTo(
            footer.LayoutBorderPosition.Y + 0.5f,
            block.LayoutBorderPosition.Y + block.LayoutBorderSize.Y,
            "the briefing must stay clear of the footer"
        );

        var lines = Descendants(block)
            .OfType<Text>()
            .SelectMany(text => text.Children.OfType<TextNode>())
            .Select(node => node.Text)
            .ToArray();
        CollectionAssert.Contains(lines, "A briefing that floats");
        CollectionAssert.Contains(lines, "Second line");

        state.DescriptionLines.Value = [];
        UiScheduler.Instance.Flush();
        root.LayoutAndRender(viewport);
        Assert.AreEqual(0f, Find(root, "Stage floating description").LayoutBorderSize.Y, 0.5f);
    }

    private static StageSelectView.Actions NavigationActions(StageSelectState state, Action? confirm = null)
        => new(
            collection => state.Collection.Value = collection,
            stage => state.Selected.Value = stage,
            query => state.Search.Value = query,
            confirm ?? (() => { }),
            () => { }
        );

    [TestMethod]
    public void PreviewArrows_CycleStagesAndCategories_AndRespectBusyConfirmation()
    {
        var state = new StageSelectState();
        state.Collections.Value = ["nfm1", "nfm2"];
        state.Collection.Value = "nfm1";
        state.Stages.Value = [new("a", "A"), new("b", "B")];
        state.Selected.Value = "a";
        state.ConfirmEnabled.Value = true;
        var confirms = 0;
        var actions = NavigationActions(state, () => confirms++);

        StageSelectView.HandlePreviewKey(state, actions, Key.Left);
        Assert.AreEqual("b", state.Selected.Value);
        StageSelectView.HandlePreviewKey(state, actions, Key.Right);
        Assert.AreEqual("a", state.Selected.Value);
        StageSelectView.HandlePreviewKey(state, actions, Key.Up);
        Assert.AreEqual("nfm2", state.Collection.Value);
        StageSelectView.HandlePreviewKey(state, actions, Key.Down);
        Assert.AreEqual("nfm1", state.Collection.Value);
        state.Busy.Value = true;
        StageSelectView.HandlePreviewKey(state, actions, Key.Enter);
        Assert.AreEqual(0, confirms);
        state.Busy.Value = false;
        StageSelectView.HandlePreviewKey(state, actions, Key.Enter);
        Assert.AreEqual(1, confirms);
    }

    [TestMethod]
    public void Preview_LeavesTheCourseVisible_AndBoundsTheBrowser()
    {
        var state = new StageSelectState();
        state.Collections.Value = ["nfm1"];
        state.Collection.Value = "nfm1";
        state.Stages.Value = Enumerable.Range(1, 30).Select(i => new StageOption($"stage{i}", $"Stage {i}")).ToArray();
        state.Selected.Value = "stage1";
        using var mounted = Renderer.Mount(() => StageSelectView.Preview(state, NavigationActions(state)));
        var root = (Component)mounted.Root;
        foreach (var viewport in new[] { new Vector2(1280, 720), new Vector2(640, 360) })
        {
            root.LayoutAndRender(viewport);
            var header = Find(root, "Stage preview header");
            var footer = Find(root, "Stage preview footer");
            Assert.IsTrue(header.LayoutBorderSize.Y < viewport.Y * 0.5f);
            Assert.IsTrue(header.LayoutBorderSize.X <= viewport.X * 0.91f);
            Assert.IsTrue(header.LayoutBorderPosition.Y + header.LayoutBorderSize.Y < footer.LayoutBorderPosition.Y);
            Assert.IsFalse(Descendants(root).Any(node => node.Name == "Stage select list"));
        }

        state.BrowserOpen.Value = true;
        UiScheduler.Instance.Flush();
        root.LayoutAndRender(new Vector2(1280, 720));
        var list = Find(root, "Stage select list");
        Assert.IsTrue(list.LayoutBorderSize.Y > 0);
        Assert.IsTrue(list.ScrollableHeight > 0);
        StageSelectView.HandlePreviewKey(state, NavigationActions(state), Key.Escape);
        Assert.IsFalse(state.BrowserOpen.Value);
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
    internal sealed class MetricsBackend : IBackend
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
