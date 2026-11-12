using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>One recording in the replay browser, local or published.</summary>
/// <param name="Id">VFS path for a local recording, or <c>archive:&lt;id&gt;</c> for a remote one.</param>
public sealed record ReplayBrowserEntry(
    string Id,
    string Stage,
    string Car,
    string Time,
    string Detail,
    bool Compatible,
    bool Uploadable
);

/// <summary>Reactive state the replay browser renders. The hosting phase owns and writes it.</summary>
public sealed class ReplayBrowserState
{
    public Signal<IReadOnlyList<ReplayBrowserEntry>> Entries { get; } = new([]);

    /// <summary>True while the list shows published replays instead of local recordings.</summary>
    public Signal<bool> Online { get; } = new(false);

    /// <summary>True while a search, download or upload is in flight.</summary>
    public Signal<bool> Busy { get; } = new(false);

    /// <summary>Progress or failure note above the list.</summary>
    public Signal<string> Status { get; } = new("");

    /// <summary>Whether another page of remote results exists.</summary>
    public Signal<bool> HasMore { get; } = new(false);
}

/// <summary>
/// Browse local recordings and published replays, open one, or upload a local one.
/// </summary>
/// <remarks>
/// A centred panel with every button on one row above it: the browser is reached from the menu and
/// a full-screen sheet would hide the scene behind it for no gain.
/// </remarks>
public static class ReplayBrowserView
{
    public sealed record Actions(
        Action<string> Open,
        Action Upload,
        Action<bool> SourceSelected,
        Action<string> Search,
        Action LoadMore,
        Action Back
    );

    private static readonly Signal<string> SearchDraft = new("");

    public static Node Build(ReplayBrowserState state, Actions actions)
    {
        SearchDraft.Value = "";

        return Sx.View(
            name: () => "Replay browser",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                paddingTop: 20,
                paddingBottom: 20,
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            children: [Panel(state, actions)]
        );
    }

    private static Node Panel(ReplayBrowserState state, Actions actions)
        => Sx.View(
            name: () => "Replay browser panel",
            style: () => Sx.Styles(
                width: 860,
                maxWidth: 96.Percent(),
                maxHeight: 78.Percent(),
                flexDirection: FlexDirection.Column,
                gapRow: 10,
                paddingTop: 14,
                paddingBottom: 14,
                paddingLeft: 16,
                paddingRight: 16,
                borderRadius: 10,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                backgroundColor: new Color(6, 14, 30, 225)
            ),
            children:
            [
                ButtonRow(state, actions),
                SearchBox(state, actions),
                Status(state),
                List(state, actions),
                Footer(state, actions)
            ]
        );

    private static Node ButtonRow(ReplayBrowserState state, Actions actions)
        => Sx.View(
            name: () => "Replay browser buttons",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                justifyContent: Justify.Center,
                alignItems: Align.Center,
                flexShrink: 0,
                gapColumn: 8
            ),
            children:
            [
                Button.Create(
                    label: () => "LOCAL",
                    onPress: () => actions.SourceSelected(false),
                    active: () => !state.Online.Value,
                    size: ButtonSize.Sm,
                    name: () => "Replay source local"
                ),
                Button.Create(
                    label: () => "ONLINE",
                    onPress: () => actions.SourceSelected(true),
                    active: () => state.Online.Value,
                    size: ButtonSize.Sm,
                    name: () => "Replay source online"
                ),
                Sx.Show(
                    when: () => !state.Online.Value,
                    then: () => Button.Create(
                        label: () => UploadLabel(state),
                        onPress: actions.Upload,
                        size: ButtonSize.Sm,
                        name: () => "Replay upload"
                    )
                ),
                Button.Create(
                    label: () => "← BACK",
                    onPress: actions.Back,
                    variant: ButtonVariant.Ghost,
                    size: ButtonSize.Sm,
                    name: () => "Replay back"
                )
            ]
        );

    private static string UploadLabel(ReplayBrowserState state)
    {
        var count = state.Entries.Value.Count(entry => entry.Uploadable);
        return count > 1 ? $"UPLOAD ALL ({count})" : "UPLOAD";
    }

    private static Node SearchBox(ReplayBrowserState state, Actions actions)
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(
                width: 100.Percent(),
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 12,
                paddingRight: 12,
                backgroundColor: new Color(255, 255, 255, 18),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                borderRadius: 6,
                flexShrink: 0
            ),
            text: () => SearchDraft.Value,
            onchange: value => SearchDraft.Value = value,
            placeholder: () => state.Online.Value
                ? "Search published replays, Enter to search..."
                : "Search your recordings...",
            onsubmit: _ => actions.Search(SearchDraft.Value)
        );
        input.Name = "Replay search";
        return input;
    }

    private static Node Status(ReplayBrowserState state)
        => Sx.Show(
            when: () => state.Status.Value.Length > 0,
            then: () => Sx.Text(
                layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                style: () => Sx.WrappingText(
                    fontSize: 11,
                    foregroundColor: state.Busy.Value ? Theme.Accent : Theme.Warning
                ),
                text: () => state.Status.Value
            )
        );

    private static Node List(ReplayBrowserState state, Actions actions)
        => Sx.View(
            name: () => "Replay list",
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll
            ),
            children:
            [
                // A fixed-width column, so long stage or driver names cannot add a horizontal
                // scrollbar to a container that only ever scrolls vertically.
                Sx.View(
                    name: () => "Replay list content",
                    style: () => Sx.Styles(
                        width: 100.Percent(),
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column,
                        gapRow: 4
                    ),
                    children:
                    [
                        Sx.For(each: () => state.Entries.Value, row: (entry, _) => Row(entry, actions)),
                        Sx.Show(
                            when: () => state.Entries.Value.Count == 0,
                            then: () => Sx.Text(
                                layout: () => Sx.Styles(width: 100.Percent()),
                                style: () => Sx.WrappingText(fontSize: 13, foregroundColor: Theme.TextFaint),
                                text: () => state.Online.Value
                                    ? "No public replays yet. Search the archive, or press LOCAL to see your own."
                                    : "No replays yet. Finish a time trial to record one."
                            )
                        )
                    ]
                )
            ]
        );

    private static Node Row(ReplayBrowserEntry entry, Actions actions)
        => Sx.View(
            name: () => "Replay row " + entry.Id,
            style: () => Sx.Styles(
                flexShrink: 0,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                justifyContent: Justify.SpaceBetween,
                gapColumn: 12,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 12,
                paddingRight: 12,
                borderRadius: 6,
                backgroundColor: new Color(22, 42, 80, 140)
            ),
            onmousedown: _ => actions.Open(entry.Id),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, flexShrink: 1, minWidth: 0, flexDirection: FlexDirection.Column),
                    children:
                    [
                        Sx.Text(
                            layout: () => Sx.Styles(width: 100.Percent()),
                            style: () => Sx.WrappingText(fontSize: 13, fontStyle: FontStyle.Bold),
                            text: () => $"{entry.Stage}  ·  {entry.Car}"
                        ),
                        Sx.Text(
                            layout: () => Sx.Styles(width: 100.Percent()),
                            style: () => Sx.WrappingText(
                                fontSize: 11,
                                foregroundColor: entry.Compatible ? Theme.TextFaint : Theme.Warning
                            ),
                            text: () => entry.Compatible
                                ? entry.Detail
                                : $"Incompatible recording  ·  {entry.Detail}"
                        )
                    ]
                ),
                Sx.Text(
                    layout: () => Sx.Styles(flexShrink: 0),
                    style: () => Sx.TextStyles(fontSize: 13, foregroundColor: Theme.MenuTextBlue),
                    text: () => entry.Time
                )
            ]
        );

    private static Node Footer(ReplayBrowserState state, Actions actions)
        => Sx.View(
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
                    text: () => state.Online.Value ? "Published replays" : "Your recordings"
                ),
                Sx.Show(
                    when: () => state.HasMore.Value,
                    then: () => Button.Create(
                        label: () => state.Busy.Value ? "LOADING..." : "MORE",
                        onPress: actions.LoadMore,
                        size: ButtonSize.Sm,
                        name: () => "Replay more"
                    )
                )
            ]
        );
}
