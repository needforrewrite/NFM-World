using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>One recording in the replay browser, local or published.</summary>
/// <param name="Id">VFS path for a local recording, or <c>archive:&lt;id&gt;</c> for a remote one.</param>
/// <param name="Compatible">Whether this build can play the recording at all.</param>
/// <param name="Outdated">Playable, but recorded under older physics.</param>
public sealed record ReplayBrowserEntry(
    string Id,
    string Stage,
    string Car,
    string Time,
    string Detail,
    bool Compatible,
    bool Uploadable,
    DateTimeOffset? SavedAt = null,
    bool Deletable = true,
    bool Outdated = false
);

/// <summary>Reactive state the replay browser renders. The hosting phase owns and writes it.</summary>
public sealed class ReplayBrowserState
{
    public Signal<IReadOnlyList<ReplayBrowserEntry>> Entries { get; } = new([]);
    public Signal<string?> SelectedId { get; } = new(null);
    public ReplayBrowserEntry? Selected => Entries.Value.FirstOrDefault(entry => entry.Id == SelectedId.Value);
    public Signal<string> SearchDraft { get; } = new("");
    public Signal<ReplayBrowserEntry?> DeleteCandidate { get; } = new(null);

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
public static class ReplayBrowserView
{
    internal sealed class InputView(Func<bool> canNavigate) : View
    {
        private readonly KeyboardNavigation _navigation = new();
        public bool HandleKey(Key key) => _navigation.HandleKey(this, key);

        protected override bool OnPostLayout()
        {
            if (canNavigate())
                _navigation.UpdateFocus(this);

            return base.OnPostLayout();
        }
    }

    public sealed record Actions(
        Action<string> Open,
        Action<string> Upload,
        Action UploadAll,
        Action<bool> SourceSelected,
        Action<string> Search,
        Action LoadMore,
        Action Back,
        Action<string>? Delete = null
    );

    public static Node Build(ReplayBrowserState state, Actions actions, Func<bool>? canNavigate = null)
    {
        var content = Sx.View(
            name: () => "Replay browser",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                paddingTop: 20,
                paddingBottom: 20,
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            children:
            [
                Panel(state, actions),
                Sx.Show(
                    when: () => state.DeleteCandidate.Value is not null,
                    then: () => DeleteConfirmation(state, actions)
                )
            ]
        );

        return new InputView(canNavigate ?? (() => true))
        {
            Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent()),
            Children = { content }
        };
    }

    private static Node Panel(ReplayBrowserState state, Actions actions)
        => Sx.View(
            name: () => "Replay browser panel",
            style: () => Sx.Styles(
                width: 860,
                maxWidth: 96.Percent(),
                height: 600,
                maxHeight: 100.Percent(),
                minHeight: 0,
                flexDirection: FlexDirection.Column,
                gapRow: 10,
                paddingTop: 20,
                paddingBottom: 20,
                paddingLeft: 20,
                paddingRight: 20,
                borderRadius: 10,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                backgroundColor: Theme.MenuPanel
            ),
            children:
            [
                Header(actions),
                ButtonRow(state, actions),
                SearchBox(state, actions),
                Status(state),
                List(state, actions),
                Selection(state, actions),
                Footer(state, actions),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.TextFaint),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => "↑ ↓ navigate · ← → choose control · Enter play / select · Esc back"
                )
            ]
        );

    private static Node Header(Actions actions)
        => Sx.View(
            style: () => Sx.Styles(alignItems: Align.Center, justifyContent: Justify.SpaceBetween, flexShrink: 0),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 32,
                        fontStyle: FontStyle.Bold,
                        fontFamily: FontFamily.Adventure,
                        foregroundColor: Theme.MenuBlue
                    ),
                    text: () => "REPLAYS"
                ),
                MenuButton.Create(
                    label: () => "BACK",
                    onPress: actions.Back,
                    variant: ButtonVariant.Ghost,
                    size: ButtonSize.Sm,
                    name: () => "Replay back"
                )
            ]
        );

    private static Node ButtonRow(ReplayBrowserState state, Actions actions)
        => KeyboardNavigation.Row(Sx.View(
            name: () => "Replay browser buttons",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                justifyContent: Justify.FlexStart,
                alignItems: Align.Center,
                flexShrink: 0,
                gapColumn: 8
            ),
            children:
            [
                MenuButton.Create(
                    label: () => "LOCAL",
                    onPress: () => actions.SourceSelected(false),
                    active: () => !state.Online.Value,
                    size: ButtonSize.Sm,
                    name: () => "Replay source local"
                ),
                MenuButton.Create(
                    label: () => "ONLINE",
                    onPress: () => actions.SourceSelected(true),
                    active: () => state.Online.Value,
                    size: ButtonSize.Sm,
                    name: () => "Replay source online"
                ),
                Sx.Show(
                    when: () => !state.Online.Value && state.Entries.Value.Any(entry => entry.Uploadable),
                    then: () => MenuButton.Create(
                        label: () => $"UPLOAD ALL ({state.Entries.Value.Count(entry => entry.Uploadable)})",
                        onPress: actions.UploadAll,
                        variant: ButtonVariant.Ghost,
                        size: ButtonSize.Sm,
                        name: () => "Replay upload all"
                    )
                )
            ]
        )
        );

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
            text: () => state.SearchDraft.Value,
            onchange: value =>
            {
                state.SearchDraft.Value = value;
                if (!state.Online.Value)
                    actions.Search(value);
            },
            placeholder: () => state.Online.Value
                ? "Search published replays, Enter to search..."
                : "Search your recordings...",
            onsubmit: _ => actions.Search(state.SearchDraft.Value)
        );
        input.Name = "Replay search";

        return input;
    }

    private static Node Status(ReplayBrowserState state)
        => Sx.Show(
            when: () => state.Status.Value.Length > 0,
            then: () =>
            {
                var status = Sx.Text(
                    layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                    style: () => Sx.WrappingText(
                        fontSize: 11,
                        foregroundColor: state.Busy.Value ? Theme.Accent : Theme.Warning
                    ),
                    text: () => state.Status.Value
                );
                status.Name = "Replay status";

                return status;
            }
        );

    private static Node List(ReplayBrowserState state, Actions actions)
        => Sx.View(
            name: () => "Replay list",
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                paddingRight: 8,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll
            ),
            children:
            [
                Sx.View(
                    name: () => "Replay list content",
                    style: () => Sx.Styles(
                        width: 100.Percent(),
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column,
                        gapRow: 8
                    ),
                    children:
                    [
                        Sx.For(each: () => state.Entries.Value, row: (entry, _) => Row(entry, state, actions)),
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

    private static Node Row(ReplayBrowserEntry entry, ReplayBrowserState state, Actions actions)
    {
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);
        var node = Sx.View(
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
                borderTop: 1,
                borderBottom: 1,
                borderRight: 1,
                borderLeft: 1,
                borderColor: focused.Value ? new Color(205, 227, 255) : Theme.MenuBorder,
                backgroundColor: state.SelectedId.Value == entry.Id ? Theme.MenuHighlight
                    : hovered.Value ? Theme.SurfaceHover : Theme.MenuFill
            ),
            onmousedown: _ => state.SelectedId.Value = entry.Id,
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, flexShrink: 1, minWidth: 0, flexDirection: FlexDirection.Column),
                    children:
                    [
                        Sx.Text(
                            layout: () => Sx.Styles(width: 100.Percent()),
                            style: () => Sx.WrappingText(
                                fontSize: 16,
                                fontStyle: FontStyle.Bold,
                                fontFamily: FontFamily.Adventure,
                                foregroundColor: Theme.MenuBlue
                            ),
                            text: () => entry.Stage
                        ),
                        Sx.Text(
                            layout: () => Sx.Styles(width: 100.Percent()),
                            style: () => Sx.WrappingText(
                                fontSize: 11,
                                foregroundColor: entry.Compatible && !entry.Outdated ? Theme.MenuDim : Theme.Warning
                            ),
                            text: () => $"{entry.Car}  ·  {StatusLabel(entry)}"
                        )
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column,
                        alignItems: Align.FlexEnd,
                        gapRow: 2
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 17,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Theme.MenuTextBlue
                            ),
                            text: () => entry.Time
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.MenuDim),
                            text: () => DateLabel(entry, state.Online.Value)
                        )
                    ]
                )
            ]
        );

        return KeyboardNavigation.Bind(
            node,
            key =>
            {
                if (key is not (Key.Enter or Key.Space))

                    return false;
                state.SelectedId.Value = entry.Id;
                if (entry.Compatible && !state.Busy.Value)
                    actions.Open(entry.Id);

                return true;
            },
            value =>
            {
                focused.Value = value;
                if (value)
                    state.SelectedId.Value = entry.Id;
            },
            () => state.SelectedId.Value == entry.Id
        );
    }

    private static string StatusLabel(ReplayBrowserEntry entry) => entry switch
    {
        { Compatible: false } => "Incompatible recording",
        { Outdated: true } => $"{entry.Detail}  ·  Outdated: may not play back correctly",
        _ => entry.Detail
    };

    private static Node Selection(ReplayBrowserState state, Actions actions)
        => Sx.View(
            name: () => "Replay selection",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                gapRow: 8,
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 12,
                paddingRight: 12,
                backgroundColor: Theme.Surface,
                borderRadius: 6
            ),
            children:
            [
                Sx.Text(
                    layout: () => Sx.Styles(width: 100.Percent()),
                    style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.TextSecondary),
                    text: () => state.Selected is { } entry
                        ? $"{entry.Stage}  ·  {entry.Car}  ·  {entry.Time}  ·  {StatusLabel(entry)}  ·  {DateLabel(entry, state.Online.Value)}"
                        : "Select a recording to watch or upload it."
                ),
                KeyboardNavigation.Row(Sx.View(
                    style: () => Sx.Styles(gapColumn: 8, alignItems: Align.Center),
                    children:
                    [
                        Sx.Show(
                            when: () => state.Selected is { Compatible: true } && !state.Busy.Value,
                            then: () => MenuButton.Create(
                                label: () => "PLAY REPLAY",
                                onPress: () => { if (state.Selected is { } entry) actions.Open(entry.Id); },
                                variant: ButtonVariant.Primary,
                                size: ButtonSize.Sm,
                                name: () => "Replay play"
                            ),
                            otherwise: () => MenuButton.Create(
                                label: () => state.Busy.Value ? "PLEASE WAIT..." : "PLAY REPLAY",
                                onPress: () => { },
                                disabled: true,
                                size: ButtonSize.Sm
                            )
                        ),
                        Sx.Show(
                            when: () => !state.Online.Value
                                        && state.Selected is { Compatible: true, Uploadable: true }
                                        && !state.Busy.Value,
                            then: () => MenuButton.Create(
                                label: () => "UPLOAD REPLAY",
                                onPress: () => { if (state.Selected is { } entry) actions.Upload(entry.Id); },
                                size: ButtonSize.Sm,
                                name: () => "Replay upload"
                            )
                        ),
                        Sx.Show(
                            when: () => !state.Online.Value
                                        && state.Selected is { Deletable: true }
                                        && !state.Busy.Value
                                        && actions.Delete is not null,
                            then: () => MenuButton.Create(
                                label: () => "DELETE REPLAY",
                                onPress: () => state.DeleteCandidate.Value = state.Selected,
                                variant: ButtonVariant.Danger,
                                size: ButtonSize.Sm,
                                name: () => "Replay delete"
                            )
                        )
                    ]
                )
                )
            ]
        );

    private static string DateLabel(ReplayBrowserEntry entry, bool online)
        => entry.SavedAt is { } date
            ? $"{(online ? "Published" : "Saved")} {date.ToLocalTime():yyyy-MM-dd HH:mm}"
            : "Date unavailable";

    private static Node DeleteConfirmation(ReplayBrowserState state, Actions actions)
    {
        var entry = state.DeleteCandidate.Value!;
        void Cancel() => state.DeleteCandidate.Value = null;

        return KeyboardNavigation.Scope(Sx.View(
            name: () => "Replay delete confirmation",
            style: () => Sx.Styles(
                position: Position.Absolute,
                top: 0,
                bottom: 0,
                left: 0,
                right: 0,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 180),
                zIndex: 30
            ),
            onmousedown: _ => Cancel(),
            children: Sx.View(
                style: () => Sx.Styles(
                    width: 90.Percent(),
                    maxWidth: 460,
                    flexDirection: FlexDirection.Column,
                    gapRow: 18,
                    paddingTop: 28,
                    paddingBottom: 28,
                    paddingLeft: 28,
                    paddingRight: 28,
                    backgroundColor: new Color(12, 22, 42, 250),
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: Theme.MenuBlue,
                    borderRadius: 10
                ),
                onmousedown: e => e.StopPropagation(),
                children:
                [
                    Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 26,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: Theme.MenuBlue
                        ),
                        text: () => "DELETE REPLAY?"
                    ),
                    Sx.Text(
                        layout: () => Sx.Styles(width: 100.Percent()),
                        style: () => Sx.WrappingText(fontSize: 14, foregroundColor: new Color(185, 211, 255)),
                        text: () => "Are you sure you want to delete this replay?"
                    ),
                    Sx.View(
                        style: () => Sx.Styles(
                            width: 100.Percent(),
                            flexDirection: FlexDirection.Column,
                            paddingTop: 10,
                            paddingBottom: 10,
                            paddingLeft: 12,
                            paddingRight: 12,
                            backgroundColor: Theme.MenuFill,
                            borderRadius: 6
                        ),
                        children:
                        [
                            Sx.Text(
                                layout: () => Sx.Styles(width: 100.Percent()),
                                style: () => Sx.WrappingText(fontSize: 13, foregroundColor: Theme.MenuTextBlue),
                                text: () => $"{entry.Stage}\n{entry.Car} · {entry.Time}\n{DateLabel(entry, false)}"
                            )
                        ]
                    ),
                    Sx.Text(
                        layout: () => Sx.Styles(width: 100.Percent()),
                        style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.MenuDim),
                        text: () => "This removes local copies of this run and its pending upload. Published replays stay online."
                    ),
                    KeyboardNavigation.Row(Sx.View(
                        style: () => Sx.Styles(gapColumn: 12, justifyContent: Justify.FlexEnd),
                        children:
                        [
                            KeyboardNavigation.Bind(
                                MenuButton.Create(
                                    label: () => "CANCEL",
                                    onPress: Cancel,
                                    size: ButtonSize.Sm,
                                    name: () => "Replay delete cancel"
                                ),
                                key =>
                                {
                                    if (key is not (Key.Escape or Key.Enter or Key.Space))

                                        return false;
                                    Cancel();

                                    return true;
                                },
                                _ => { },
                                preferred: () => true
                            ),
                            KeyboardNavigation.Bind(
                                MenuButton.Create(
                                    label: () => "DELETE REPLAY",
                                    onPress: () =>
                                    {
                                        Cancel();
                                        actions.Delete?.Invoke(entry.Id);
                                    },
                                    variant: ButtonVariant.Danger,
                                    size: ButtonSize.Sm,
                                    name: () => "Replay delete confirm"
                                ),
                                key =>
                                {
                                    if (key == Key.Escape)
                                    {
                                        Cancel();

                                        return true;
                                    }

                                    if (key is not (Key.Enter or Key.Space))

                                        return false;
                                    Cancel();
                                    actions.Delete?.Invoke(entry.Id);

                                    return true;
                                },
                                _ => { }
                            )
                        ]
                    )
                    )
                ]
            )
        )
        );
    }

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
                    text: () => $"{state.Entries.Value.Count} {(state.Online.Value ? "published replays" : "recordings")}"
                ),
                Sx.Show(
                    when: () => state.HasMore.Value,
                    then: () => MenuButton.Create(
                        label: () => state.Busy.Value ? "LOADING..." : "MORE",
                        onPress: actions.LoadMore,
                        size: ButtonSize.Sm,
                        name: () => "Replay more"
                    )
                )
            ]
        );
}
