using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
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
    public Signal<string> Search { get; } = new("");
    public Signal<bool> ConfirmEnabled { get; } = new(false);

    /// <summary>True while the list shows public archive stages instead of local content.</summary>
    public Signal<bool> OnlineSource { get; } = new(false);

    /// <summary>True while an online search or download is in flight.</summary>
    public Signal<bool> Busy { get; } = new(false);

    /// <summary>Progress, failure or result note shown above the list.</summary>
    public Signal<string> Status { get; } = new("");

    /// <summary>Why the selected stage cannot be used here, or empty when it can.</summary>
    public Signal<string> Restriction { get; } = new("");
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
        Action<string>? OnlineSearch = null
    );

    private static readonly Signal<string> SearchDraft = new("");

    /// <summary>Empties the search box. Returns false when it was already empty.</summary>
    public static bool ClearSearch()
    {
        if (SearchDraft.Value.Length == 0)
            return false;

        SearchDraft.Value = "";
        return true;
    }

    public static Node Build(StageSelectState state, Actions actions, string title = "Select Stage")
    {
        SearchDraft.Value = state.Search.Value;

        return Sx.View(
            name: () => "Stage select",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                flexDirection: FlexDirection.Row,
                backgroundColor: new Color(3, 8, 20, 120)
            ),
            children:
            [
                Sidebar(state, actions, title),
                Content(state, actions)
            ]
        );
    }

    /// <summary>A compact list for a dialog: no page chrome, just filter and rows.</summary>
    public static Node Compact(StageSelectState state, Actions actions)
    {
        SearchDraft.Value = state.Search.Value;

        return Sx.View(
            name: () => "Stage select compact",
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, flexShrink: 1, minHeight: 0, gapRow: 6),
            children:
            [
                SearchBox(state, actions),
                List(state, actions),
                Restriction(state)
            ]
        );
    }

    private static Node Sidebar(StageSelectState state, Actions actions, string title)
        => Sx.View(
            name: () => "Stage select sidebar",
            style: () => Sx.Styles(
                width: 300,
                height: 100.Percent(),
                paddingTop: 28,
                paddingBottom: 28,
                paddingLeft: 24,
                paddingRight: 24,
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                gapRow: 12,
                backgroundColor: new Color(6, 14, 30, 205),
                borderRight: 1,
                borderColor: Theme.MenuBorder
            ),
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
                    layout: () => Sx.Styles(flexShrink: 0, marginBottom: 4),
                    text: () => "Choose where to race."
                ),
                SourcePicker(state, actions),
                Sx.Show(
                    when: () => !state.OnlineSource.Value,
                    then: () => CollectionRail(state, actions),
                    otherwise: () => Sx.View(
                        style: () => Sx.Styles(flexGrow: 1, flexShrink: 1, minHeight: 0),
                        children: Sx.Text(
                            style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.TextFaint),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => "Public archive stages. Search to narrow the list."
                        )
                    )
                ),
                Button.Create(
                    label: () => "← Back",
                    onPress: actions.Back,
                    variant: ButtonVariant.Ghost,
                    fullWidth: true,
                    name: () => "Stage select back"
                )
            ]
        );

    private static Node SourcePicker(StageSelectState state, Actions actions)
        => Sx.View(
            name: () => "Stage select source",
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexShrink: 0, gapColumn: 8, gapRow: 8),
            children:
            [
                Button.Create(
                    label: () => "LOCAL",
                    onPress: () => actions.SourceSelected?.Invoke(false),
                    active: () => !state.OnlineSource.Value,
                    size: ButtonSize.Sm,
                    style: () => Sx.Styles(flexGrow: 1),
                    name: () => "Stage select source local"
                ),
                Button.Create(
                    label: () => "ONLINE",
                    onPress: () => actions.SourceSelected?.Invoke(true),
                    active: () => state.OnlineSource.Value,
                    size: ButtonSize.Sm,
                    style: () => Sx.Styles(flexGrow: 1),
                    name: () => "Stage select source online"
                )
            ]
        );

    private static Node CollectionRail(StageSelectState state, Actions actions)
        => Sx.View(
            name: () => "Stage select collections",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                overflow: Overflow.Scroll,
                gapRow: 4
            ),
            children: Sx.For(
                each: () => state.Collections.Value,
                row: (collection, _) => CollectionRow(state, collection, actions)
            )
        );

    private static Node CollectionRow(StageSelectState state, string collection, Actions actions)
    {
        var active = () => state.Collection.Value == collection;
        return Sx.View(
            name: () => "Stage collection " + collection,
            style: () => Sx.Styles(
                flexShrink: 0,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 12,
                paddingRight: 12,
                borderRadius: 6,
                borderLeft: 2,
                borderColor: active() ? Theme.Accent : Theme.Transparent,
                backgroundColor: active() ? Theme.AccentSoft : new Color(255, 255, 255, 12)
            ),
            onmousedown: _ => actions.CollectionSelected(collection),
            children: Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 14,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: active() ? Theme.Accent : Theme.MenuDim
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
                paddingTop: 28,
                paddingBottom: 28,
                paddingLeft: 28,
                paddingRight: 28,
                gapRow: 12
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        justifyContent: Justify.SpaceBetween,
                        flexShrink: 0,
                        gapColumn: 12
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => state.OnlineSource.Value ? "PUBLIC STAGES" : "STAGES"
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                            text: () => $"{state.Stages.Value.Count} available"
                        )
                    ]
                ),
                SearchBox(state, actions),
                Status(state),
                List(state, actions),
                Restriction(state),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        justifyContent: Justify.SpaceBetween,
                        flexShrink: 0,
                        gapColumn: 12
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                            text: () => "↑ ↓ or ← → to browse  ·  Enter to race"
                        ),
                        Button.Create(
                            label: () => "RACE",
                            onPress: actions.Confirm,
                            variant: ButtonVariant.Primary,
                            size: ButtonSize.Lg,
                            style: () => Sx.Styles(minWidth: 180),
                            name: () => "Stage select confirm"
                        )
                    ]
                )
            ]
        );

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
            ),
            otherwise: () => Sx.View(style: () => Sx.Styles(height: 0, flexShrink: 0))
        );

    private static Node SearchBox(StageSelectState state, Actions actions)
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(
                width: 100.Percent(),
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 14,
                paddingRight: 14,
                backgroundColor: new Color(255, 255, 255, 20),
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
            placeholder: () => state.OnlineSource.Value ? "Search public stages, Enter to load..." : "Search stages...",
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
                overflow: Overflow.Scroll
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
                            then: () => Sx.Text(
                                style: () => Sx.WrappingText(fontSize: 14, foregroundColor: Theme.TextFaint),
                                layout: () => Sx.Styles(width: 100.Percent()),
                                text: () => state.OnlineSource.Value
                                    ? "No public stages yet. Search the archive for one."
                                    : "No stages match."
                            )
                        )
                    ]
                )
            ]
        );

    private static Node StageRow(StageSelectState state, StageOption stage, Actions actions)
    {
        var active = () => state.Selected.Value == stage.Id;
        return Sx.View(
            name: () => "Stage row " + stage.Id,
            style: () => Sx.Styles(
                flexShrink: 0,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                justifyContent: Justify.SpaceBetween,
                gapColumn: 12,
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 14,
                paddingRight: 14,
                borderRadius: 6,
                borderLeft: 2,
                borderColor: active() ? Theme.Accent : Theme.Transparent,
                backgroundColor: active() ? Theme.MenuHighlight : new Color(22, 42, 80, 120)
            ),
            onmousedown: _ => actions.StageSelected(stage.Id),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 14,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: active() ? new Color(235, 244, 255) : Theme.MenuTextBlue
                    ),
                    text: () => stage.Label
                ),
                Sx.Show(
                    when: () => stage.Subtitle is { Length: > 0 },
                    then: () => Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                        text: () => stage.Subtitle ?? ""
                    )
                )
            ]
        );
    }

    private static Node Restriction(StageSelectState state)
        => Sx.Show(
            when: () => state.Restriction.Value.Length > 0,
            then: () => Sx.Text(
                style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.Warning),
                layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                text: () => state.Restriction.Value
            ),
            otherwise: () => Sx.View(style: () => Sx.Styles(height: 0, flexShrink: 0))
        );
}
