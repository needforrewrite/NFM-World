using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>Reactive state the stage selector renders. The hosting phase owns and writes it.</summary>
public sealed class StageSelectState
{
    public Signal<IReadOnlyList<string>> Collections { get; } = new([]);
    public Signal<string> Collection { get; } = new("");
    public Signal<IReadOnlyList<string>> Stages { get; } = new([]);
    public Signal<string> Selected { get; } = new("");
    public Signal<string> Search { get; } = new("");
    public Signal<bool> ConfirmEnabled { get; } = new(false);

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
        Action Back
    );

    private static readonly Signal<string> SearchDraft = new("");

    public static Node Build(StageSelectState state, Actions actions, string title = "Select Stage")
    {
        SearchDraft.Value = state.Search.Value;

        return Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                flexDirection: FlexDirection.Row,
                backgroundColor: new Color(0, 0, 0, 90)
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
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, flexShrink: 1, minHeight: 0, gapRow: 6),
            children:
            [
                SearchBox(state, actions),
                List(state, actions),
                Restriction(state)
            ]
        );

    private static Node Sidebar(StageSelectState state, Actions actions, string title)
        => Sx.View(
            style: () => Sx.Styles(
                width: 300,
                height: 100.Percent(),
                paddingTop: 32,
                paddingBottom: 32,
                paddingLeft: 24,
                paddingRight: 24,
                flexDirection: FlexDirection.Column,
                gapColumn: 10,
                gapRow: 10,
                backgroundColor: new Color(0, 0, 0, 100),
                borderRight: 1,
                borderColor: new Color(255, 255, 255, 15)
            ),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 28, fontStyle: FontStyle.Bold), text: () => title),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        overflow: Overflow.Scroll
                    ),
                    children: Sx.For(
                        each: () => state.Collections.Value,
                        row: (collection, _) => CollectionRow(state, collection, actions)
                    )
                ),
                Button.Create(label: () => "← Back", onPress: actions.Back)
            ]
        );

    private static Node CollectionRow(StageSelectState state, string collection, Actions actions)
    {
        var active = () => state.Collection.Value == collection;
        return Sx.View(
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
                style: () => Sx.TextStyles(fontSize: 14, foregroundColor: active() ? Theme.Accent : Theme.TextFaint),
                text: () => collection
            )
        );
    }

    private static Node Content(StageSelectState state, Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                paddingTop: 32,
                paddingBottom: 32,
                paddingLeft: 24,
                paddingRight: 24,
                gapRow: 12
            ),
            children:
            [
                SearchBox(state, actions),
                List(state, actions),
                Restriction(state),
                Button.Create(label: () => "RACE", onPress: actions.Confirm)
            ]
        );

    private static Node SearchBox(StageSelectState state, Actions actions)
        => Sx.TextInput(
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
                borderColor: new Color(255, 255, 255, 31),
                borderRadius: 8,
                flexShrink: 0
            ),
            text: () => SearchDraft.Value,
            onchange: value =>
            {
                SearchDraft.Value = value;
                actions.SearchChanged(value);
            },
            placeholder: () => "Search stages...",
            onsubmit: _ =>
            {
                if (state.Stages.Value.FirstOrDefault() is { } first)
                    actions.StageSelected(first);
            }
        );

    private static Node List(StageSelectState state, Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll,
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
                        style: () => Sx.TextStyles(fontSize: 14, foregroundColor: Theme.TextFaint),
                        text: () => "No stages match."
                    )
                )
            ]
        );

    private static Node StageRow(StageSelectState state, string stage, Actions actions)
    {
        var active = () => state.Selected.Value == stage;
        return Sx.View(
            style: () => Sx.Styles(
                flexShrink: 0,
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 14,
                paddingRight: 14,
                borderRadius: 6,
                backgroundColor: active() ? Theme.MenuHighlight : new Color(22, 42, 80, 120)
            ),
            onmousedown: _ => actions.StageSelected(stage),
            children: Sx.Text(
                style: () => Sx.TextStyles(fontSize: 14, foregroundColor: active() ? Theme.Accent : Theme.TextFaint),
                text: () => stage
            )
        );
    }

    private static Node Restriction(StageSelectState state)
        => Sx.Show(
            when: () => state.Restriction.Value.Length > 0,
            then: () => Sx.Text(
                style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(255, 160, 80)),
                text: () => state.Restriction.Value
            ),
            otherwise: () => Sx.View(style: () => Sx.Styles(height: 0))
        );
}
