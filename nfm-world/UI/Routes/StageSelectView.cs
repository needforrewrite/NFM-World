using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using System.Numerics;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorldLibrary;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>One selectable stage. <see cref="Id"/> is what the game loads; the label is for people.</summary>
public sealed record StageOption(string Id, string Label, string? Subtitle = null);

/// <summary>Reactive state the stage selector renders. The hosting phase owns and writes it.</summary>
public sealed class StageSelectState
{
    public Signal<IReadOnlyList<string>> Collections { get; } = new([]);
    public Signal<string> Collection { get; } = new("");
    public Signal<IReadOnlyList<StageOption>> Stages { get; } = new([]);
    public Signal<string> Selected { get; } = new("");
    public Signal<string> PreviewName { get; } = new("");
    public Signal<StageOption?> SelectedArchiveStage { get; } = new(null);
    public Signal<string> Search { get; } = new("");
    public Signal<bool> ConfirmEnabled { get; } = new(false);
    public Signal<string> ConfirmLabel { get; } = new("RACE");
    public Signal<string> Description { get; } = new("Choose where to race.");

    /// <summary>The selected stage's <c>desc()</c> briefing lines; empty when it has none.</summary>
    public Signal<IReadOnlyList<string>> DescriptionLines { get; } = new([]);

    /// <summary>Vertical offset of the floating briefing, in pixels; the phase animates it.</summary>
    public Signal<float> DescriptionFloat { get; } = new(0);
    public Signal<bool> BrowsingCollections { get; } = new(false);
    public Signal<bool> BrowserOpen { get; } = new(false);

    /// <summary>True while the list shows public archive stages instead of local content.</summary>
    public Signal<bool> OnlineSource { get; } = new(false);

    /// <summary>True while an online search or download is in flight.</summary>
    public Signal<bool> Busy { get; } = new(false);

    /// <summary>Progress, failure or result note shown above the list.</summary>
    public Signal<string> Status { get; } = new("");

    /// <summary>Why the selected stage cannot be used here, or empty when it can.</summary>
    public Signal<string> Restriction { get; } = new("");

    /// <summary>Back to an empty LOCAL list, as a fresh selector would be.</summary>
    public void Reset()
    {
        Collections.Value = [];
        Collection.Value = "";
        Stages.Value = [];
        Selected.Value = "";
        PreviewName.Value = "";
        SelectedArchiveStage.Value = null;
        Search.Value = "";
        ConfirmEnabled.Value = false;
        ConfirmLabel.Value = "RACE";
        Description.Value = "Choose where to race.";
        DescriptionLines.Value = [];
        DescriptionFloat.Value = 0;
        BrowsingCollections.Value = false;
        BrowserOpen.Value = false;
        OnlineSource.Value = false;
        Busy.Value = false;
        Status.Value = "";
        Restriction.Value = "";
    }
}

/// <summary>
/// The one stage selector: collections, search, a scrollable list and confirm/back.
/// </summary>
/// <remarks>
/// The hosting phase keeps the stage preview and its music; the selector only names a stage and
/// reports a selection. The lobby's create flow and free play use this same view, so a stage
/// cannot be offered in one place and missing from the other.
/// </remarks>
public static class StageSelectView
{
    public sealed record Actions(
        Action<string> CollectionSelected,
        Action<string> StageSelected,
        Action<string> SearchChanged,
        Action Confirm,
        Action Back,
        Action<bool>? SourceSelected = null,
        Action<string>? OnlineSearch = null,
        string? SourceControlsHint = null
    );

    private static readonly Signal<string> SearchDraft = new("");

    // Row surfaces sit over the live stage preview, so they carry enough alpha for the label to
    // stay readable against a bright sky without hiding the course behind them.
    private static readonly Color RowIdle = new(16, 30, 58, 170);
    private static readonly Color RowHover = new(34, 58, 100, 185);
    private static readonly Color RowLabel = new(232, 242, 255);
    private static readonly Color DividerColor = new(128, 170, 255, 55);
    private static readonly Color ContentScrim = new(4, 10, 22, 105);
    private static readonly Color FieldFill = new(8, 18, 36, 165);

    /// <summary>Empties the search box. Returns false when it was already empty.</summary>
    public static bool ClearSearch()
    {
        if (SearchDraft.Value.Length == 0)

            return false;

        SearchDraft.Value = "";

        return true;
    }

    public static bool HandleKey(StageSelectState state, Actions actions, Key key)
    {
        if (FocusManager.FocusedNode is TextInput or TextArea)
        {
            if (key != Key.Escape)

                return false;
            ClearSearch();
            actions.SearchChanged("");
            FocusManager.ClearFocus();

            return true;
        }

        switch (key)
        {
            case Key.Left:
                state.BrowsingCollections.Value = !state.OnlineSource.Value;

                break;
            case Key.Right:
                state.BrowsingCollections.Value = false;

                break;
            case Key.Up:
            case Key.Down:
                var delta = key == Key.Up ? -1 : 1;

                if (state.BrowsingCollections.Value && !state.OnlineSource.Value)
                {
                    var collections = state.Collections.Value;

                    if (collections.Count == 0)

                        break;
                    var index = collections.ToList().IndexOf(state.Collection.Value);
                    var next = index < 0 ? (delta > 0 ? 0 : collections.Count - 1)
                        : (index + delta + collections.Count) % collections.Count;
                    actions.CollectionSelected(collections[next]);
                }
                else
                {
                    var stages = state.Stages.Value;

                    if (stages.Count == 0)

                        break;
                    var index = stages.ToList().FindIndex(stage => stage.Id == state.Selected.Value);
                    var next = index < 0 ? (delta > 0 ? 0 : stages.Count - 1)
                        : (index + delta + stages.Count) % stages.Count;
                    actions.StageSelected(stages[next].Id);
                }

                break;
            case Key.Enter:
                if (state.BrowsingCollections.Value)
                    state.BrowsingCollections.Value = false;
                else if (state.ConfirmEnabled.Value && !state.Busy.Value)
                    actions.Confirm();

                break;
            case Key.Escape:
                if (ClearSearch())
                    actions.SearchChanged("");
                else
                    actions.Back();

                break;
            default: return false;
        }

        return true;
    }

    private sealed class NavigableStageSelect(StageSelectState state, bool sideBySide = false) : View
    {
        private Component? _lastCategory;
        private Component? _lastStage;

        protected override bool OnPostLayout()
        {
            var categories = !state.OnlineSource.Value && (sideBySide || state.BrowsingCollections.Value);
            ScrollSelection(categories ? "Stage collection " + state.Collection.Value : null, ref _lastCategory);
            ScrollSelection(sideBySide || !categories ? "Stage row " + state.Selected.Value : null, ref _lastStage);

            return base.OnPostLayout();
        }

        private void ScrollSelection(string? name, ref Component? previous)
        {
            var target = name is null ? null : Find(this, name);
            if (target is not null && !ReferenceEquals(target, previous))
                target.ScrollIntoView();
            previous = target;
        }

        private static Component? Find(Node root, string name)
        {
            if (root is Component component && component.Name == name)

                return component;

            foreach (var child in root.VisualChildren)

                if (Find(child, name) is { } found)

                    return found;

            return null;
        }
    }

    public static Node Build(StageSelectState state, Actions actions, string title = "Select Stage")
    {
        SearchDraft.Value = state.Search.Value;

        var root = new NavigableStageSelect(state)
        {
            Name = "Stage select",
            Styles = Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                flexDirection: FlexDirection.Row,
                backgroundColor: new Color(3, 8, 20, 120)
            )
        };
        root.AddChild(Sidebar(state, actions, title));
        root.AddChild(Content(state, actions));

        return root;
    }

    public static bool HandlePreviewKey(StageSelectState state, Actions actions, Key key)
    {
        if (FocusManager.FocusedNode is TextInput or TextArea)

            return HandleKey(state, actions, key);

        switch (key)
        {
            case Key.PageUp when actions.SourceSelected is not null:
            case Key.PageDown when actions.SourceSelected is not null:
                actions.SourceSelected(key == Key.PageDown);

                return true;
            case Key.Left:
            case Key.Right:
                MovePreview(state, actions, key == Key.Left ? -1 : 1, collection: false);

                return true;
            case Key.Up:
            case Key.Down:
                MovePreview(
                    state,
                    actions,
                    key == Key.Up ? -1 : 1,
                    collection: !state.OnlineSource.Value && state.Collections.Value.Count > 1
                );

                return true;
            case Key.Escape when state.BrowserOpen.Value && ClearSearch():
                actions.SearchChanged("");

                return true;
            case Key.Escape when state.BrowserOpen.Value:
            case Key.Tab:
                TogglePreviewBrowser(state);

                return true;
            case Key.Enter:
                if (state.ConfirmEnabled.Value && !state.Busy.Value)
                    actions.Confirm();

                return true;
            default:

                return HandleKey(state, actions, key);
        }
    }

    private static void MovePreview(StageSelectState state, Actions actions, int delta, bool collection)
    {
        if (collection)
        {
            var items = state.Collections.Value;

            if (items.Count == 0)

                return;
            var index = items.ToList().IndexOf(state.Collection.Value);
            var next = index < 0 ? (delta > 0 ? 0 : items.Count - 1) : (index + delta + items.Count) % items.Count;
            actions.CollectionSelected(items[next]);
        }
        else
        {
            var items = state.Stages.Value;

            if (items.Count == 0)

                return;
            var index = items.ToList().FindIndex(stage => stage.Id == state.Selected.Value);
            var next = index < 0 ? (delta > 0 ? 0 : items.Count - 1) : (index + delta + items.Count) % items.Count;
            if (items[next].Id != state.Selected.Value)
                actions.StageSelected(items[next].Id);
        }
    }

    public static Node Preview(StageSelectState state, Actions actions)
    {
        SearchDraft.Value = state.Search.Value;

        var root = new NavigableStageSelect(state, sideBySide: true)
        {
            Name = "Stage preview controls",
            Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent())
        };
        root.AddChild(Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                top: 0,
                left: 0,
                right: 0,
                bottom: 0
            ),
            children:
            [
                PreviewBrowser(state, actions),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minWidth: 0, minHeight: 0),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(flexGrow: 1, minHeight: 0, position: Position.Relative),
                            children: FloatingDescription(state, bottom: 18, centered: true)
                        ),
                        Sx.Show(
                            () => state.BrowserOpen.Value,
                            () => StageTray(state, actions),
                            () => PreviewActions(state, actions, standalone: true)
                        )
                    ]
                )
            ]
        )
        );
        var title = Sx.Text(
            style: () => Sx.WrappingText(
                fontSize: 30,
                fontFamily: FontFamily.Adventure,
                foregroundColor: Color.White,
                horizontalAlignment: TextHorizontalAlignment.Center
            ).Over(Sx.TextStyles(strokeColor: Color.Black, strokeWidth: 3)),
            layout: () => Sx.Styles(
                position: Position.Absolute,
                top: 16,
                left: 19.Percent(),
                right: 19.Percent(),
                pointerEvents: false
            ),
            text: () => state.PreviewName.Value.Length > 0 ? state.PreviewName.Value : Heading(state)
        );
        title.Name = "Stage preview title";
        root.AddChild(title);
        root.AddChild(Sx.Show(
            () => state.Busy.Value,
            () => Sx.View(
                name: () => "Stage preview loading",
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    top: 0,
                    bottom: 0,
                    left: 0,
                    right: 0,
                    alignItems: Align.Center,
                    justifyContent: Justify.Center,
                    backgroundColor: new Color(0, 0, 0, 145),
                    zIndex: 900,
                    pointerEvents: false
                ),
                children: new LoadingSpinner
            {
                Radius = 16,
                Styles = Sx.Styles(width: 56, height: 56, flexShrink: 0)
            }
            )
        )
        );

        return root;
    }

    private static void TogglePreviewBrowser(StageSelectState state)
    {
        state.BrowserOpen.Value = !state.BrowserOpen.Value;
        FocusManager.ClearFocus();
    }

    private static Node PreviewActions(StageSelectState state, Actions actions, bool standalone = false)
        => Sx.View(
            name: () => "Stage preview header",
            style: () => Sx.Styles(
                maxWidth: standalone ? 100.Percent() : 60.Percent(),
                alignSelf: standalone ? Align.End : Align.Center,
                minWidth: 0,
                flexDirection: FlexDirection.Column,
                gapRow: 5,
                paddingTop: standalone ? 6 : 0,
                paddingBottom: standalone ? 6 : 0,
                paddingLeft: standalone ? 6 : 0,
                paddingRight: standalone ? 6 : 0,
                backgroundColor: standalone ? Theme.MenuPanel : Theme.Transparent
            ),
            children:
            [
                Button.Create(
                    () => "START",
                    () => { if (state.ConfirmEnabled.Value && !state.Busy.Value) actions.Confirm(); },
                    variant: ButtonVariant.Primary,
                    size: ButtonSize.Sm,
                    style: () => Sx.Styles(
                        alignSelf: Align.End,
                        flexShrink: 0,
                        opacity: state.ConfirmEnabled.Value && !state.Busy.Value ? 1f : 0.55f
                    ),
                    name: () => "Stage preview start"
                ),
                Sx.Show(
                    () => !state.Busy.Value && state.Status.Value.Length > 0,
                    () => Sx.Text(
                        style: () => Sx.WrappingText(
                            fontSize: 11,
                            foregroundColor: Theme.MenuTextBlue
                        ),
                        layout: () => Sx.Styles(width: 100.Percent()),
                        text: () => state.Status.Value
                    )
                ),
                Restriction(state)
            ]
        );

    internal static Node ReadOnlyPreview(StageSelectState state, Func<IImage?> image)
        => Sx.View(
            name: () => "Lobby stage preview",
            style: () => Sx.Styles(
                position: Position.Relative,
                flexGrow: 1,
                minHeight: 0,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: new Color(6, 14, 30, 255),
                overflow: Overflow.Hidden
            ),
            children:
            [
                new PreviewImage(image)
                {
                    Styles = Sx.Styles(
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        right: 0,
                        bottom: 0,
                        pointerEvents: false
                    )
                },
                FloatingDescription(state, bottom: 24),
                Sx.Show(() => state.Busy.Value, () => new LoadingSpinner
                {
                    Radius = 16,
                    Styles = Sx.Styles(width: 48, height: 48, flexShrink: 0)
                }),
                Sx.Show(
                    () => state.Status.Value.Length > 0,
                    () => Sx.Text(
                        style: () => Sx.WrappingText(
                            fontSize: 13,
                            foregroundColor: Theme.MenuTextBlue,
                            horizontalAlignment: TextHorizontalAlignment.Center
                        ),
                        layout: () => Sx.Styles(width: 90.Percent()),
                        text: () => state.Status.Value
                    )
                )
            ]
        );

    private sealed class PreviewImage(Func<IImage?> image) : Component
    {
        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            if (image() is not { } frame || size.X <= 0 || size.Y <= 0)

                return;
            var scale = Math.Min(size.X / frame.Width, size.Y / frame.Height);
            var displayedSize = new Vector2(frame.Width, frame.Height) * scale;
            TheGraphics.G.DrawImageTransformed(
                frame,
                position + (size - displayedSize) / 2,
                new Vector2(displayedSize.X, 0),
                new Vector2(0, displayedSize.Y),
                Color.White
            );
        }
    }

    /// <summary>
    /// phy's stage briefing: the <c>desc()</c> lines float over the course preview with no panel
    /// behind them, so the stage itself stays the backdrop. Lines are positional (a blank line
    /// spaces the block out), hence one slot each rather than a keyed list.
    /// </summary>
    private static Node FloatingDescription(StageSelectState state, float bottom = 104, bool centered = false)
        => Sx.View(
            name: () => "Stage floating description",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: centered ? 12 : 34,
                right: centered ? 12 : null,
                bottom: bottom + state.DescriptionFloat.Value,
                width: centered ? null : 460,
                maxWidth: centered ? null : 46.Percent(),
                flexDirection: FlexDirection.Column,
                alignItems: centered ? Align.Center : Align.Stretch,
                gapRow: 2,
                pointerEvents: false
            ),
            // phy kept five lines; a stage that set fewer leaves the rest hidden.
            children:
            [
                .. Enumerable.Range(0, StageLoader.MaxDescriptionLines)
                .Select(index => (Node)DescriptionLine(state, index, centered))
            ]
        );

    private static Node DescriptionLine(StageSelectState state, int index, bool centered)
        => Sx.Show(
            when: () => index < state.DescriptionLines.Value.Count,
            then: () => Sx.Text(
                style: () => Sx.WrappingText(
                    fontSize: 15,
                    fontFamily: FontFamily.Adventure,
                    foregroundColor: Color.White,
                    horizontalAlignment: centered ? TextHorizontalAlignment.Center : TextHorizontalAlignment.Left
                )
                    .Over(Sx.TextStyles(strokeColor: Color.Black, strokeWidth: 2)),
                layout: () => Sx.Styles(
                    width: 100.Percent(),
                    maxWidth: centered ? 560 : null,
                    height: centered ? null : 20,
                    minHeight: 20,
                    flexShrink: 0
                ),
                text: () => state.DescriptionLines.Value[index]
            )
        );

    private static Node PreviewBrowser(StageSelectState state, Actions actions)
        => Sx.View(
            name: () => "Stage preview browser",
            style: () =>
            {
                var open = state.BrowserOpen.Value;

                return PreviewPanelStyle().Over(Sx.Styles(
                    width: open ? 16.Percent() : 174,
                    minWidth: 156,
                    maxWidth: 192,
                    minHeight: 0,
                    alignSelf: open && !state.OnlineSource.Value && state.Collections.Value.Count > 1
                        ? Align.Stretch : Align.Start,
                    flexShrink: 0,
                    flexDirection: FlexDirection.Column,
                    gapRow: 8,
                    borderRight: 1
                )
                );
            },
            children:
            [
                Sx.Show(
                    () => state.BrowserOpen.Value,
                    () => Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 18,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: Theme.MenuBlue,
                            horizontalAlignment: TextHorizontalAlignment.Center
                        ),
                        layout: () => Sx.Styles(flexShrink: 0),
                        text: () => "Select Stage"
                    )
                ),
                Sx.View(
                    style: () => Sx.Styles(alignItems: Align.Center, justifyContent: Justify.SpaceBetween, flexShrink: 0),
                    children:
                    [
                        Button.Create(() => "BACK", actions.Back, size: ButtonSize.Sm, variant: ButtonVariant.Ghost),
                        Button.Create(
                            () => state.BrowserOpen.Value ? "PREVIEW" : "BROWSE",
                            () => TogglePreviewBrowser(state),
                            size: ButtonSize.Sm,
                            name: () => "Stage preview toggle"
                        )
                    ]
                ),
                Sx.Show(() => state.BrowserOpen.Value, () => SourcePicker(state, actions, tabs: true)),
                Sx.Show(
                    () => state.BrowserOpen.Value && !state.OnlineSource.Value && state.Collections.Value.Count > 1,
                    () => PreviewCategories(state, actions)
                ),
                Sx.Show(() => state.BrowserOpen.Value, () => Hint("Tab Preview"))
            ]
        );

    private static Styles PreviewPanelStyle()
        => Sx.Styles(
            paddingTop: 8,
            paddingBottom: 8,
            paddingLeft: 8,
            paddingRight: 8,
            backgroundColor: new Color(12, 22, 42, 247),
            borderColor: Theme.MenuBorder
        );

    private static Node PreviewCategories(StageSelectState state, Actions actions)
        => Sx.View(
            style: () => Sx.Styles(minHeight: 0, flexGrow: 1, flexDirection: FlexDirection.Column, gapRow: 6),
            children:
            [
                Hint("↑ ↓ Categories"),
                CollectionRail(state, actions, sideBySide: true)
            ]
        );

    private static Node StageTray(StageSelectState state, Actions actions)
        => Sx.View(
            name: () => "Stage preview tray",
            style: () => PreviewPanelStyle().Over(Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
                flexDirection: FlexDirection.Column,
                gapRow: 4,
                paddingTop: 5,
                paddingBottom: 5,
                borderTop: 1
            )
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        alignItems: Align.Center,
                        gapColumn: 10,
                        flexShrink: 0
                    ),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(width: 0, minWidth: 0, flexGrow: 1, maxWidth: 240),
                            children: SearchBox(state, actions, compact: true)
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            layout: () => Sx.Styles(flexGrow: 1, minWidth: 0),
                            text: () => state.Stages.Value.Count == 1 ? "← →  1 stage" : $"← →  {state.Stages.Value.Count} stages"
                        ),
                        PreviewActions(state, actions)
                    ]
                ),
                Sx.View(
                    name: () => "Stage select list",
                    style: () => Sx.Styles(
                        height: 56,
                        width: 100.Percent(),
                        minWidth: 0,
                        flexShrink: 0,
                        gapColumn: 6,
                        overflow: Overflow.Scroll,
                        paddingBottom: 7
                    ),
                    onmount: view => view.MouseScrolled = e => view.TryScroll(e.Delta.Y, 0),
                    children:
                    [
                        Sx.For(() => state.Stages.Value, (stage, _) => StageRow(state, stage, actions, card: true)),
                        Sx.Show(() => state.Stages.Value.Count == 0 && !state.Busy.Value, () => Hint("No matching stages."))
                    ]
                )
            ]
        );

    /// <summary>A compact list for a dialog: no page chrome, just source, filter and rows.</summary>
    public static Node Compact(StageSelectState state, Actions actions)
    {
        SearchDraft.Value = state.Search.Value;

        return Sx.View(
            name: () => "Stage select compact",
            // The minimum keeps a couple of rows readable; a dialog shorter than that grows a
            // scrollbar of its own instead of squeezing the list to nothing.
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: MinCompactHeight,
                gapRow: 6
            ),
            children:
            [
                // Null when the host cannot search the archive, so a selector that only offers
                // local content does not show a dead toggle.
                SourcePicker(state, actions),
                SearchBox(state, actions),
                Status(state),
                List(state, actions),
                Restriction(state)
            ]
        );
    }

    /// <summary>Height the compact selector keeps before its host dialog starts scrolling.</summary>
    private const float MinCompactHeight = 200;

    private static Node Sidebar(StageSelectState state, Actions actions, string title)
        => Sx.View(
            name: () => "Stage select sidebar",
            style: () => Sx.Styles(
                width: 300,
                height: 100.Percent(),
                paddingTop: 26,
                paddingBottom: 22,
                paddingLeft: 20,
                paddingRight: 20,
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                gapRow: 14,
                backgroundColor: new Color(6, 14, 30, 215),
                borderRight: 1,
                borderColor: Theme.MenuBorder
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0, gapRow: 2),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 30,
                                fontFamily: FontFamily.Adventure,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Theme.MenuBlue
                            ),
                            layout: () => Sx.Styles(flexShrink: 0),
                            text: () => title
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            layout: () => Sx.Styles(flexShrink: 0),
                            text: () => state.Description.Value
                        )
                    ]
                ),
                Divider(),
                SourcePicker(state, actions),
                Sx.Show(
                    when: () => !state.OnlineSource.Value,
                    then: () => CollectionRail(state, actions),
                    otherwise: () => Sx.View(
                        // Holds the leftover height so the note floats at the top and Back stays
                        // pinned to the bottom; the card itself hugs its text.
                        style: () => Sx.Styles(
                            flexDirection: FlexDirection.Column,
                            flexGrow: 1,
                            flexShrink: 1,
                            minHeight: 0
                        ),
                        children: Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Column,
                                flexShrink: 0,
                                width: 100.Percent(),
                                paddingTop: 12,
                                paddingBottom: 12,
                                paddingLeft: 14,
                                paddingRight: 14,
                                borderRadius: 8,
                                borderTop: 1,
                                borderBottom: 1,
                                borderLeft: 1,
                                borderRight: 1,
                                borderColor: Theme.MenuBorder,
                                backgroundColor: FieldFill,
                                gapRow: 4
                            ),
                            children: Sx.Text(
                                style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                                layout: () => Sx.Styles(width: 100.Percent()),
                                text: () => "Stages published by players. Search to narrow the list."
                            )
                        )
                    )
                ),
                Divider(),
                Button.Create(
                    label: () => "← Back",
                    onPress: actions.Back,
                    variant: ButtonVariant.Ghost,
                    fullWidth: true,
                    name: () => "Stage select back"
                )
            ]
        );

    /// <summary>A hairline that separates the sidebar's blocks without boxing them in.</summary>
    private static Node Divider()
        => Sx.View(
            style: () => Sx.Styles(height: 1, width: 100.Percent(), flexShrink: 0, backgroundColor: DividerColor)
        );

    private static Node Hint(string text)
        => Sx.Text(
            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
            layout: () => Sx.Styles(flexShrink: 0),
            text: () => text
        );

    private static Node SourcePicker(StageSelectState state, Actions actions, bool tabs = false)
    {
        // A host with no archive search cannot leave LOCAL, so it gets no picker at all rather
        // than two buttons where one does nothing. The empty view keeps the node name so a test
        // (and the debugger) can still see that the picker exists and takes no space.

        if (actions.SourceSelected is null || actions.OnlineSearch is null)

            return Sx.View(
                name: () => "Stage select source",
                style: () => Sx.Styles(height: 0, flexShrink: 0)
            );

        return Sx.View(
            name: () => "Stage select source",
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexShrink: 0, gapColumn: tabs ? 4 : 8),
            children:
            [
                SourceButton(false, "LOCAL", "Stage select source local"),
                SourceButton(true, "ONLINE", "Stage select source online")
            ]
        );

        Node SourceButton(bool online, string label, string name)
            => Button.Create(
                () => label,
                () => actions.SourceSelected(online),
                active: () => state.OnlineSource.Value == online,
                size: ButtonSize.Sm,
                style: () => Sx.Styles(
                    flexGrow: 1,
                    paddingTop: 7,
                    paddingBottom: 7,
                    borderRadius: tabs ? 0 : null,
                    borderTop: tabs ? 0 : null,
                    borderLeft: tabs ? 0 : null,
                    borderRight: tabs ? 0 : null,
                    borderBottom: tabs ? 2 : null
                ),
                name: () => name
            );
    }

    private static Node CollectionRail(StageSelectState state, Actions actions, bool sideBySide = false)
        => Sx.View(
            name: () => "Stage select collections",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                overflow: Overflow.Scroll,
                gapRow: 3,
                paddingRight: 8
            ),
            children: Sx.For(
                each: () => state.Collections.Value,
                row: (collection, _) => CollectionRow(state, collection, actions, sideBySide)
            )
        );

    private static Node CollectionRow(StageSelectState state, string collection, Actions actions, bool sideBySide)
    {
        var hovered = new Signal<bool>(false);
        var active = () => state.Collection.Value == collection;

        return Sx.View(
            name: () => "Stage collection " + collection,
            style: () => Sx.Styles(
                flexShrink: 0,
                paddingTop: 9,
                paddingBottom: 9,
                paddingLeft: sideBySide ? 6 : 12,
                paddingRight: sideBySide ? 4 : 10,
                borderRadius: 6,
                borderLeft: 3,
                borderColor: active() ? state.BrowsingCollections.Value ? Color.White : Theme.Accent : Theme.Transparent,
                backgroundColor: active()
                    ? Theme.MenuHighlight
                    : hovered.Value ? new Color(255, 255, 255, 22) : Theme.Transparent
            ),
            onmousedown: _ =>
            {
                if (!sideBySide)
                    state.BrowsingCollections.Value = true;
                actions.CollectionSelected(collection);
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children: Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: sideBySide ? 12 : 13,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: active()
                        ? RowLabel
                        : hovered.Value ? Theme.MenuTextBlue : Theme.MenuDim
                ),
                text: () => collection
            )
        );
    }

    private static Node Content(StageSelectState state, Actions actions)
        => Sx.View(
            name: () => "Stage select content",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                paddingTop: 26,
                paddingBottom: 22,
                paddingLeft: 28,
                paddingRight: 28,
                gapRow: 14,
                backgroundColor: ContentScrim
            ),
            children:
            [
                Header(state),
                SearchBox(state, actions),
                Status(state),
                List(state, actions),
                Restriction(state),
                Footer(state, actions)
            ]
        );

    private static Node Header(StageSelectState state)
        => Sx.View(
            name: () => "Stage select header",
            style: () => Sx.Styles(
                height: 50,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                justifyContent: Justify.SpaceBetween,
                flexShrink: 0,
                gapColumn: 12
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        minWidth: 0,
                        gapRow: 2
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 9,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Theme.TextFaint
                            ),
                            layout: () => Sx.Styles(height: 14, flexShrink: 0),
                            text: () => Overline(state)
                        ),
                        new StageHeading(state)
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexShrink: 0,
                        paddingTop: 3,
                        paddingBottom: 3,
                        paddingLeft: 10,
                        paddingRight: 10,
                        borderRadius: 999,
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: Theme.MenuBorder
                    ),
                    children: Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.MenuDim),
                        text: () => state.Stages.Value.Count == 1 ? "1 stage" : $"{state.Stages.Value.Count} stages"
                    )
                )
            ]
        );

    private sealed class StageHeading : View
    {
        private readonly StageSelectState _state;
        private readonly Signal<float> _fontSize = new(22);

        public StageHeading(StageSelectState state)
        {
            _state = state;
            Styles = Sx.Styles(width: 100.Percent(), height: 32, flexShrink: 0, overflow: Overflow.Hidden);
            AddChild(Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: _fontSize.Value,
                    fontFamily: FontFamily.Adventure,
                    fontStyle: FontStyle.Bold,
                    verticalAlignment: TextVerticalAlignment.Center,
                    foregroundColor: Theme.MenuTextBlue
                ),
                layout: () => Sx.Styles(width: 100.Percent(), height: 100.Percent(), flexShrink: 0),
                text: () => Heading(state)
            )
            );
        }

        protected override bool OnPostLayout()
        {
            var scale = Math.Max(TheGraphics.G.Scale, 0.01f);
            var width = TheGraphics.G.GetFontMetrics(new Font(FontFamily.Adventure, FontStyle.Bold, 22 * scale))
                .MeasureText(Heading(_state)).X;
            if (width > 0 && LayoutContentSize.X > 0)
                _fontSize.Value = Math.Clamp(22 * (LayoutContentSize.X - 2 * scale) / width, 1, 22);

            return base.OnPostLayout();
        }
    }

    /// <summary>Where the list is drawn from: local content, or the public archive.</summary>
    private static string Overline(StageSelectState state)
        => state.OnlineSource.Value ? "PUBLIC ARCHIVE" : "STAGE LIBRARY";

    /// <summary>
    /// The selection's own name, which is the one thing that changes while browsing; it replaces
    /// the large caption the phase used to draw over the preview. With nothing selected the
    /// collection names itself, since that is what the list is then showing.
    /// </summary>
    private static string Heading(StageSelectState state)
    {
        var selected = state.Selected.Value;

        foreach (var stage in state.Stages.Value)
        {
            if (stage.Id == selected)

                return stage.Label;
        }

        if (state.OnlineSource.Value)

            return "Public stages";

        return string.IsNullOrEmpty(state.Collection.Value)
            ? "Stages"
            : state.Collection.Value.ToUpperInvariant();
    }

    private static Node Status(StageSelectState state)
        => Sx.Show(
            when: () => state.Busy.Value || state.Status.Value.Length > 0,
            then: () => Sx.Text(
                style: () => Sx.WrappingText(
                    fontSize: 11,
                    foregroundColor: state.Busy.Value ? Theme.Accent : Theme.MenuDim
                ),
                layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                text: () => state.Busy.Value ? "Working…" : state.Status.Value
            )
        );

    private static Node SearchBox(StageSelectState state, Actions actions, bool compact = false)
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(
                width: 100.Percent(),
                paddingTop: compact ? 3 : 10,
                paddingBottom: compact ? 3 : 10,
                paddingLeft: compact ? 8 : 14,
                paddingRight: compact ? 8 : 14,
                backgroundColor: FieldFill,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                borderRadius: 8,
                flexShrink: 0
            ),
            text: () => SearchDraft.Value,
            onchange: value =>
            {
                SearchDraft.Value = value;
                actions.SearchChanged(value);
            },
            placeholder: () => state.OnlineSource.Value
                ? compact ? "Search online ↵" : "Search public stages, Enter to load..."
                : "Search stages...",
            onsubmit: _ =>
            {
                if (state.OnlineSource.Value)
                {
                    actions.OnlineSearch?.Invoke(SearchDraft.Value);

                    return;
                }

                if (state.Stages.Value.FirstOrDefault() is { } first)
                    actions.StageSelected(first.Id);
            }
        );
        input.Name = "Stage select search";

        return input;
    }

    private static Node List(StageSelectState state, Actions actions)
        => Sx.View(
            name: () => "Stage select list",
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll,
                paddingRight: 12
            ),
            children:
            [
                // A fixed-width column: the scroll container then only ever scrolls
                // vertically, however long a stage or author name happens to be.
                Sx.View(
                    name: () => "Stage select list content",
                    style: () => Sx.Styles(
                        width: 100.Percent(),
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column,
                        gapRow: 4
                    ),
                    children:
                    [
                        Sx.For(
                            each: () => state.Stages.Value,
                            row: (stage, _) => StageRow(state, stage, actions)
                        ),
                        Sx.Show(
                            when: () => state.Stages.Value.Count == 0,
                            then: () => EmptyState(state)
                        )
                    ]
                )
            ]
        );

    private static Node EmptyState(StageSelectState state)
        => Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexDirection: FlexDirection.Column,
                alignItems: Align.Center,
                flexShrink: 0,
                paddingTop: 48,
                gapRow: 6
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 14,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: Theme.MenuDim,
                        horizontalAlignment: TextHorizontalAlignment.Center
                    ),
                    layout: () => Sx.Styles(width: 100.Percent()),
                    text: () => state.OnlineSource.Value ? "No public stages found." : "No stages match."
                ),
                Sx.Text(
                    style: () => Sx.WrappingText(
                        fontSize: 11,
                        foregroundColor: Theme.TextFaint,
                        horizontalAlignment: TextHorizontalAlignment.Center
                    ),
                    layout: () => Sx.Styles(width: 100.Percent()),
                    text: () => state.Search.Value.Length > 0
                        ? "Clear the search to see every stage."
                        : state.OnlineSource.Value
                            ? "Try a different search, or publish a stage to the archive."
                            : "Pick another collection."
                )
            ]
        );

    private static Node StageRow(StageSelectState state, StageOption stage, Actions actions, bool card = false)
    {
        var hovered = new Signal<bool>(false);
        var active = () => state.Selected.Value == stage.Id;

        return Sx.View(
            name: () => "Stage row " + stage.Id,
            style: () => Sx.Styles(
                flexShrink: 0,
                width: card ? 154 : null,
                flexDirection: card ? FlexDirection.Column : FlexDirection.Row,
                alignItems: card ? Align.Stretch : Align.Center,
                justifyContent: Justify.SpaceBetween,
                gapColumn: card ? 0 : 12,
                gapRow: card ? 1 : 0,
                paddingTop: card ? 4 : 11,
                paddingBottom: card ? 4 : 11,
                paddingLeft: card ? 10 : 14,
                paddingRight: card ? 10 : 14,
                overflow: card ? Overflow.Hidden : null,
                borderRadius: 8,
                borderLeft: 3,
                borderColor: active()
                    ? card || state.BrowsingCollections.Value ? Theme.Accent : Color.White
                    : Theme.Transparent,
                backgroundColor: active()
                    ? Theme.MenuHighlight
                    : hovered.Value ? RowHover : RowIdle
            ),
            onmousedown: _ =>
            {
                state.BrowsingCollections.Value = false;
                actions.StageSelected(stage.Id);
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.Text(
                    style: () => card
                        ? Sx.WrappingText(
                            fontSize: 12,
                            foregroundColor: active() || hovered.Value ? RowLabel : Theme.MenuTextBlue
                        )
                        : Sx.TextStyles(
                            fontSize: 14,
                            fontStyle: FontStyle.Bold,
                            foregroundColor: active() || hovered.Value ? RowLabel : Theme.MenuTextBlue
                        ),
                    layout: () => Sx.Styles(flexShrink: 1, minWidth: 0),
                    text: () => stage.Label
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        gapColumn: 10,
                        flexShrink: 0
                    ),
                    children:
                    [
                        Sx.Show(
                            when: () => stage.Subtitle is { Length: > 0 },
                            then: () => Sx.Text(
                                style: () => card
                                    ? Sx.WrappingText(fontSize: 10, foregroundColor: Theme.MenuDim)
                                    : Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                                layout: () => Sx.Styles(minWidth: 0, flexShrink: 1),
                                text: () => stage.Subtitle ?? ""
                            )
                        ),
                        Sx.Show(
                            when: () => !card && active(),
                            then: () => Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 16, foregroundColor: Theme.Accent),
                                text: () => "›"
                            )
                        )
                    ]
                )
            ]
        );
    }

    private static Node Footer(StageSelectState state, Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                justifyContent: Justify.SpaceBetween,
                flexShrink: 0,
                gapColumn: 12,
                paddingTop: 14,
                borderTop: 1,
                borderColor: DividerColor
            ),
            children:
            [
                // A narrow window must not push the button: the hint wraps inside its own column
                // instead of being squeezed to nothing (space-between overlaps under overflow).
                Sx.View(
                    style: () => Sx.Styles(
                        flexShrink: 1,
                        minWidth: 0,
                        flexDirection: FlexDirection.Row,
                        flexWrap: Wrap.Wrap,
                        alignItems: Align.Center,
                        gapColumn: 14,
                        gapRow: 2
                    ),
                    children:
                    [
                        Hint("← →  category / stages"),
                        Hint("↑ ↓  navigate"),
                        Sx.Show(
                            () => actions.SourceControlsHint is not null,
                            () => Hint(actions.SourceControlsHint!)
                        ),
                        Hint("Enter  select"),
                        Hint("Esc  back")
                    ]
                ),
                Button.Create(
                    label: () => state.ConfirmLabel.Value,
                    onPress: actions.Confirm,
                    variant: ButtonVariant.Primary,
                    size: ButtonSize.Lg,
                    // Dimmed while nothing is selectable; Confirm itself re-checks the state.
                    style: () => Sx.Styles(
                        minWidth: 200,
                        flexShrink: 0,
                        opacity: state.ConfirmEnabled.Value ? 1f : 0.45f
                    ),
                    name: () => "Stage select confirm"
                )
            ]
        );

    private static Node Restriction(StageSelectState state)
        => Sx.Show(
            when: () => state.Restriction.Value.Length > 0,
            then: () => Sx.Text(
                style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.Warning),
                layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                text: () => state.Restriction.Value
            )
        );
}
