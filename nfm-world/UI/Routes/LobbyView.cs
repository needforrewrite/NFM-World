using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Plugins;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>
/// The multiplayer lobby, in the main menu's blue.
///
/// The phase owns all state and writes it into <see cref="UiState.Lobby"/>; this view only
/// reads it, so a lobby snapshot updates just the rows that changed. Every action is a callback
/// the phase supplies, as in <c>MainMenuView</c>.
/// </summary>
/// <remarks>
/// A component method here runs once. Anything that can change - the highlighted row, the
/// selected stage - must be read inside a <c>Func</c> passed to Sx, or the row would keep the
/// value it saw when it was built.
/// </remarks>
public static class LobbyView
{
    /// <summary>Join takes the row's id: a button stops propagation, so the row's own click never fires.</summary>
    public sealed record Actions(
        Action<uint> Join,
        Action Leave,
        Action<uint> StartRace,
        Action Create,
        Action Refresh,
        Action<string> SendChat,
        Action Back,
        Action OpenCreate,
        Action CloseCreate,
        Action<uint> Select,
        /// <summary>Watch a race that is already running, without taking a slot.</summary>
        Action<uint> Spectate,
        Action<string> SetStage,
        Action<int> SetMaxPlayers,
        Action<int> SetBots,
        Action<string, string> SetConfig,
        Action<string> SetGamemode
    );

    private static readonly Color Blue = Theme.MenuBlue;
    private static readonly Color TextBlue = Theme.MenuTextBlue;
    private static readonly Color PanelColor = Theme.MenuPanel;
    private static readonly Color Border = Theme.MenuBorder;

    private static readonly Signal<string> ChatInput = new("");

    public static void Reset() => ChatInput.Value = "";

    public static Node Build(Actions actions)
    {
        var lobby = UiState.Lobby;

        return Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                right: 0,
                bottom: 0,
                backgroundColor: new Color(3, 8, 20, 170),
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        width: 92.Percent(),
                        maxWidth: 1100,
                        height: 90.Percent(),
                        flexDirection: FlexDirection.Column,
                        backgroundColor: PanelColor,
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: Border,
                        borderRadius: 12,
                        paddingTop: 18,
                        paddingBottom: 14,
                        paddingLeft: 22,
                        paddingRight: 22,
                        gapRow: 12
                    ),
                    children:
                    [
                        Header(actions),
                        Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                flexGrow: 1,
                                gapColumn: 14,
                                alignItems: Align.Stretch
                            ),
                            children:
                            [
                                PlayersPanel(),
                                GamesPanel(actions),
                                ChatPanel(actions)
                            ]
                        )
                    ]
                ),
                Sx.Show(() => lobby.ShowCreateDialog.Value, () => CreateDialog(actions))
            ]
        );
    }

    private static Node Header(Actions actions)
    {
        var lobby = UiState.Lobby;
        return Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.FlexEnd,
                gapColumn: 12,
                flexShrink: 0
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 30,
                                fontFamily: FontFamily.Adventure,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Blue
                            ),
                            text: () => "MULTIPLAYER"
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 12,
                                foregroundColor: lobby.Connected.Value ? TextBlue : Theme.Danger
                            ),
                            text: () => lobby.Status.Value.Length > 0 ? lobby.Status.Value : "Connecting..."
                        ),
                        // Only once retries are exhausted: leaving is the way out.
                        Sx.Show(
                            () => lobby.Disconnected.Value,
                            () => Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.Danger),
                                text: () => "Could not reconnect. Press BACK and try again."
                            )
                        )
                    ]
                ),
                SmallButton("REFRESH", actions.Refresh),
                SmallButton("BACK", actions.Back)
            ]
        );
    }

    private static Node PlayersPanel()
    {
        var lobby = UiState.Lobby;
        return Panel(
            "PLAYERS",
            240,
            () => Sx.For(
                each: () => lobby.Players.Value,
                row: (player, _) => Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexShrink: 0,
                        paddingTop: 4,
                        paddingBottom: 4,
                        borderBottom: 1,
                        borderColor: new Color(128, 170, 255, 40)
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 14,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: player.IsSelf ? Blue : TextBlue
                            ),
                            text: () => player.Name
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => player.Car
                        )
                    ]
                )
            )
        );
    }

    private static Node GamesPanel(Actions actions)
    {
        var lobby = UiState.Lobby;
        return Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                gapRow: 10,
                overflow: Overflow.Hidden
            ),
            children:
            [
                // flexShrink: 0 would let a long list push the buttons off the bottom; the
                // panel takes what is left and its list scrolls.
                Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, flexShrink: 1, flexDirection: FlexDirection.Column),
                    children:
                    [
                        Panel(
                            "GAMES",
                            null,
                            () => Sx.Show(
                                when: () => lobby.Sessions.Value.Count == 0,
                                then: () => Sx.Text(
                                    style: () => Sx.TextStyles(
                                        fontSize: 13,
                                        foregroundColor: Theme.MenuDim
                                    ),
                                    text: () => "No games yet. Create one to get started."
                                ),
                                otherwise: () => Sx.For(
                                    each: () => lobby.Sessions.Value,
                                    row: (session, _) => SessionRow(session, actions)
                                )
                            )
                        )
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10, flexShrink: 0),
                    children:
                    [
                        ActionButton(
                            "CREATE GAME",
                            actions.OpenCreate,
                            primary: true,
                            disabled: () => !lobby.Connected.Value
                        ),
                        Sx.Show(
                            () => lobby.CurrentSession.Value is not null,
                            () => Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10),
                                children:
                                [
                                    // Only the creator may start, so it is not shown to a member
                                    // who cannot use it.
                                    Sx.Show(
                                        () => lobby.CurrentSession.Value?.IsCreator == true,
                                        () => ActionButton(
                                            () => "START RACE",
                                            () => actions.StartRace(lobby.CurrentSession.Value!.Id),
                                            primary: true,
                                            disabled: () => !lobby.Connected.Value
                                                            || lobby.CurrentSession.Value?.State != SessionState.NotStarted
                                        )
                                    ),
                                    ActionButton(
                                        "LEAVE",
                                        actions.Leave,
                                        disabled: () => !lobby.Connected.Value
                                    )
                                ]
                            )
                        )
                    ]
                )
            ]
        );
    }

    private static Node SessionRow(UiState.LobbySession session, Actions actions)
    {
        var lobby = UiState.Lobby;

        // This row is built once and never re-rendered, so the selected check has to live in
        // the style accessor: selection changes without the record changing.
        return Row(
            onmousedown: _ => actions.Select(session.Id),
            style: () => Sx.Styles(
                backgroundColor: lobby.SelectedSessionId.Value == session.Id
                    ? Theme.MenuHighlight
                    : new Color(22, 42, 80, 110)
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 14,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: TextBlue
                            ),
                            text: () => session.StageName
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => $"{session.CreatorName} · {session.PlayerCount}/{session.MaxPlayers}"
                        )
                    ]
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: StateColor(session.State)),
                    text: () => StateLabel(session.State)
                ),

                // Once a race is under way there is no slot to take, so everyone - member or
                // not - gets WATCH, and a member keeps the LEAVE that gets it out of the
                // session it is stuck in. Before that, only the creator starts an open race;
                // a member leaves it. (Both once called StartRace, so LEAVE launched a race.)
                session.State is SessionState.Started or SessionState.WaitingToLoad
                    ? Sx.View(
                        style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6, flexShrink: 0),
                        children:
                        [
                            ActionButton(
                                () => "WATCH",
                                () => actions.Spectate(session.Id),
                                small: true,
                                disabled: () => !lobby.Connected.Value
                            ),
                            // A member still needs a way out of the session it is stuck in.
                            session.IsMember
                                ? ActionButton(
                                    () => "LEAVE",
                                    actions.Leave,
                                    small: true,
                                    disabled: () => !lobby.Connected.Value
                                )
                                : null
                        ]
                    )
                    : session.IsMember && session.IsCreator
                        ? ActionButton(
                            () => "START",
                            () => actions.StartRace(session.Id),
                            small: true,
                            disabled: () => session.State != SessionState.NotStarted || !lobby.Connected.Value
                        )
                        : session.IsMember
                            ? ActionButton(
                                () => "LEAVE",
                                actions.Leave,
                                small: true,
                                disabled: () => !lobby.Connected.Value
                            )
                            : ActionButton(
                                () => "JOIN",
                                () => actions.Join(session.Id),
                                small: true,
                                disabled: () => session.State != SessionState.NotStarted || !lobby.Connected.Value
                            ),
            ]
        );
    }

    private static Color StateColor(SessionState state) => state switch
    {
        SessionState.Started => Theme.Success,
        SessionState.WaitingToLoad => Theme.Warning,
        _ => Theme.MenuDim
    };

    private static string StateLabel(SessionState state) => state switch
    {
        SessionState.Started => "RACING",
        SessionState.WaitingToLoad => "LOADING",
        SessionState.Finished => "FINISHED",
        _ => "OPEN"
    };

    private static Node ChatPanel(Actions actions)
    {
        var lobby = UiState.Lobby;
        // The input row must not be squeezed as the transcript grows, and the transcript must
        // not grow the column past the viewport: Panel's list takes what is left and scrolls.
        return Sx.View(
            style: () => Sx.Styles(
                width: 300,
                flexShrink: 0,
                flexDirection: FlexDirection.Column,
                gapRow: 8,
                overflow: Overflow.Hidden
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, flexShrink: 1, flexDirection: FlexDirection.Column),
                    children:
                    [
                        Panel(
                            "CHAT",
                            null,
                            () => Sx.View(
                                // flexShrink 0, or the column shrinks to the panel instead of
                                // overflowing it - which leaves the transcript nothing to scroll.
                                style: () => Sx.Styles(
                                    flexDirection: FlexDirection.Column,
                                    flexShrink: 0,
                                    gapRow: 4
                                ),
                                children: Sx.For(
                                    each: () => lobby.Chat.Value,
                                    row: (line, _) => Sx.View(
                                        style: () => Sx.Styles(flexDirection: FlexDirection.Column),
                                        children:
                                        [
                                            Sx.Text(
                                                style: () => Sx.TextStyles(
                                                    fontSize: 11,
                                                    fontStyle: FontStyle.Bold,
                                                    foregroundColor: line.IsSystem ? Theme.MenuDim : line.Color
                                                ),
                                                text: () => line.Sender
                                            ),
                                            // Word-wrap: a long message ran out of the column and
                                            // off the right edge of the lobby.
                                            Sx.Text(
                                                style: () => Sx.WrappingText(
                                                    fontSize: 12,
                                                    foregroundColor: line.IsSystem ? Theme.MenuDim : TextBlue
                                                ),
                                                layout: () => Sx.Styles(width: 100.Percent()),
                                                text: () => line.Message
                                            )
                                        ]
                                    )
                                )
                            )
                        )
                    ]
                ),
                Sx.View(
                    // A row stretches its children by default, which made the button as tall
                    // as the input; centring keeps it beside the field instead.
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        gapColumn: 6,
                        flexShrink: 0,
                        alignItems: Align.Center
                    ),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(flexGrow: 1, flexShrink: 1),
                            children:
                            [
                                Sx.TextInput(
                                    style: () => Sx.Styles(
                                        width: 100.Percent(),
                                        minHeight: 30,
                                        paddingTop: 6,
                                        paddingBottom: 6,
                                        paddingLeft: 8,
                                        paddingRight: 8,
                                        backgroundColor: new Color(255, 255, 255, 16),
                                        borderTop: 1,
                                        borderBottom: 1,
                                        borderLeft: 1,
                                        borderRight: 1,
                                        borderColor: Border,
                                        borderRadius: 6
                                    ),
                                    text: () => ChatInput.Value,
                                    onchange: value => ChatInput.Value = value,
                                    placeholder: () => "Message...",
                                    // Enter submits, so the field behaves like a chat box.
                                    onsubmit: _ => Send(actions)
                                )
                            ]
                        ),
                        ActionButton(
                            "SEND",
                            () => Send(actions),
                            small: true,
                            disabled: () => !lobby.Connected.Value
                        )
                    ]
                )
            ]
        );
    }

    private static void Send(Actions actions)
    {
        var message = ChatInput.Value;
        if (message.Trim().Length == 0)
            return;
        ChatInput.Value = "";
        actions.SendChat(message);
    }

    private static Node CreateDialog(Actions actions)
    {
        var lobby = UiState.Lobby;

        // Read once per open: this enumerates the whole stage tree, and the component only
        // runs when the dialog is mounted.
        var stages = AvailableStages();
        var stageState = new StageSelectState();
        stageState.Collections.Value = stages
            .Select(StageCollectionOf)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        stageState.Collection.Value = stageState.Collections.Value.FirstOrDefault() ?? "";
        stageState.Stages.Value = stages;
        stageState.Selected.Value = lobby.SelectedStage.Value;
        stageState.ConfirmEnabled.Value = stages.Count > 0;

        return Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                right: 0,
                bottom: 0,
                backgroundColor: new Color(3, 8, 20, 200),
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        width: 460,
                        flexDirection: FlexDirection.Column,
                        gapRow: 10,
                        backgroundColor: PanelColor,
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: Border,
                        borderRadius: 10,
                        paddingTop: 18,
                        paddingBottom: 16,
                        paddingLeft: 20,
                        paddingRight: 20
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 20,
                                fontFamily: FontFamily.Adventure,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Blue
                            ),
                            text: () => "CREATE GAME"
                        ),
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6),
                            children: GamemodeCatalog.Ids.Select(id => ActionButton(
                                    () => GamemodeCatalog.DisplayName(id),
                                    () => actions.SetGamemode(id),
                                    small: true,
                                    active: () => lobby.SelectedGamemode.Value == id
                                )
                            ).ToArray()
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => "Stage"
                        ),
                        // The same selector free play uses, in its compact form.
                        StageSelectView.Compact(
                            stageState,
                            new StageSelectView.Actions(
                                CollectionSelected: collection =>
                                {
                                    stageState.Collection.Value = collection;
                                    stageState.Stages.Value = stages
                                        .Where(stage => StageCollectionOf(stage) == collection)
                                        .ToArray();
                                },
                                StageSelected: stage =>
                                {
                                    stageState.Selected.Value = stage;
                                    actions.SetStage(stage);
                                },
                                SearchChanged: query =>
                                {
                                    stageState.Stages.Value = stages
                                        .Where(stage => query.Trim().Length == 0
                                                        || stage.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
                                        )
                                        .ToArray();
                                },
                                Confirm: () => { },
                                Back: () => { }
                            )
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => $"Max players: {lobby.MaxPlayers.Value}"
                        ),
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6),
                            children: Controls.Slider(
                                () => lobby.MaxPlayers.Value,
                                value => actions.SetMaxPlayers((int)value),
                                2,
                                MatchGameplayInfo.MaxPlayers,
                                1
                            )
                        ),

                        Sx.Show(
                            () => lobby.SelectedGamemode.Value != DefaultGamemodes.TimeTrial,
                            () => Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 10),
                                children:
                                [
                                    Sx.Text(
                                        style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                                        text: () => "Bots (they fill the empty slots)"
                                    ),
                                    Sx.View(
                                        style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6),
                                        children: Controls.Slider(
                                            () => lobby.BotCount.Value,
                                            value => actions.SetBots((int)value),
                                            0,
                                            Math.Max(0, MatchGameplayInfo.MaxPlayers - lobby.MaxPlayers.Value),
                                            1
                                        )
                                    )
                                ]
                            )
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => $"Field: {lobby.MaxPlayers.Value + lobby.BotCount.Value}"
                        ),

                        (Func<object?>)(() => Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 10),
                            children: [.. GamemodeConfigControls(actions)]
                        )),
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10, marginTop: 6),
                            children:
                            [
                                Sx.View(
                                    style: () => Sx.Styles(flexGrow: 1),
                                    children:
                                    [ActionButton("CREATE", actions.Create, primary: true)]
                                ),
                                Sx.View(
                                    style: () => Sx.Styles(flexGrow: 1),
                                    children:
                                    [ActionButton("CANCEL", actions.CloseCreate)]
                                )
                            ]
                        )
                    ]
                )
            ]
        );
    }

    /// <summary>
    /// One row per config property the gamemode declares, read from the plugin so the dialog
    /// never has to know what a gamemode's settings are. Empty when the plugin declares none.
    /// </summary>
    private static List<Node> GamemodeConfigControls(Actions actions)
    {
        var lobby = UiState.Lobby;
        var controls = new List<Node>();

        foreach (var property in GamemodeCatalog.ConfigProperties(lobby.SelectedGamemode.Value))
        {
            if (property.Options.Count == 0)
                continue;

            controls.Add(
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                    text: () => property.Label
                )
            );

            var options = new List<Node>();
            foreach (var option in property.Options)
            {
                var value = option.Value;
                options.Add(
                    ActionButton(
                        () => option.Label,
                        () => actions.SetConfig(property.Name, value),
                        small: true,
                        active: () => lobby.GamemodeConfig.Value.TryGetValue(property.Name, out var chosen)
                                      && chosen == value
                    )
                );
            }

            controls.Add(
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6),
                    children: [.. options]
                )
            );
        }

        return controls;
    }

    private static Node StageOption(string stage, Actions actions)
    {
        var lobby = UiState.Lobby;
        return Row(
            onmousedown: _ => actions.SetStage(stage),
            style: () => Sx.Styles(
                backgroundColor: lobby.SelectedStage.Value == stage
                    ? Theme.MenuHighlight
                    : new Color(22, 42, 80, 100)
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 13,
                        foregroundColor: lobby.SelectedStage.Value == stage ? Blue : TextBlue
                    ),
                    text: () => stage
                )
            ]
        );
    }

    private static string StageCollectionOf(string stageId)
    {
        var slash = stageId.IndexOf('/');
        return slash < 0 ? "stages" : stageId[..slash];
    }

    /// <summary>Stage names for the create dialog.</summary>
    private static IReadOnlyList<string> AvailableStages()
    {
        try
        {
            return GameSparker.GetAvailableStages();
        }
        catch (Exception)
        {
            // A stage tree that failed to load must not take the lobby down with it.
            return [];
        }
    }

    // ── Shared pieces ───────────────────────────────────────────────

    private static Node Panel(string title, int? width, Func<Node> content)
    {
        return Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: width is null ? 1 : 0,
                flexShrink: 1,
                minHeight: 0,
                width: width is { } w ? w : null,
                gapRow: 8,
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 12,
                paddingRight: 12,
                backgroundColor: new Color(22, 42, 80, 90),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 70),
                borderRadius: 8
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 11,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: Blue
                    ),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => title
                ),
                // The panel's height is its parent's, so the list takes the remainder and
                // scrolls: an unbounded list would push past the panel instead.
                Sx.View(
                    style: () => Sx.Styles(
                        flexGrow: 1,
                        flexShrink: 1,
                        flexDirection: FlexDirection.Column,
                        overflow: Overflow.Scroll
                    ),
                    children: [content()]
                )
            ]
        );
    }

    /// <summary>A clickable row with the menu's hover treatment.</summary>
    private static View Row(
        Action<NFMWorld.ClayDom.Events.MouseEvent> onmousedown,
        Func<Styles> style,
        params object?[] children
    )
    {
        var hovered = new Signal<bool>(false);
        return Sx.View(
            style: () => style().Over(
                Sx.Styles(
                    paddingTop: 6,
                    paddingBottom: 6,
                    paddingLeft: 8,
                    paddingRight: 8,
                    gapColumn: 10,
                    alignItems: Align.Center,
                    flexDirection: FlexDirection.Row,
                    // List rows keep their height and overflow the scroll container; the web
                    // default (shrink 1) squeezed every row to its padding instead.
                    flexShrink: 0,
                    borderRadius: 6,
                    backgroundColor: hovered.Value ? Theme.MenuHighlight : null
                )
            ),
            onmousedown: onmousedown,
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children: children
        );
    }

    private static Node ActionButton(
        Func<string> label,
        Action onPress,
        bool primary = false,
        bool small = false,
        Func<bool>? disabled = null,
        Func<bool>? active = null
    )
        => MenuButton(label, onPress, primary, small, disabled, active);

    private static Node ActionButton(
        string label,
        Action onPress,
        bool primary = false,
        bool small = false,
        Func<bool>? disabled = null
    )
        => MenuButton(() => label, onPress, primary, small, disabled, null);

    private static Node SmallButton(string label, Action onPress)
        => MenuButton(() => label, onPress, false, true, null, null);

    /// <summary>The shared button, restyled into the menu palette.</summary>
    private static Node MenuButton(
        Func<string> label,
        Action onPress,
        bool primary,
        bool small,
        Func<bool>? disabled,
        Func<bool>? active
    )
    {
        return Button.Create(
            label,
            onPress,
            variant: primary ? ButtonVariant.Primary : ButtonVariant.Default,
            active: active,
            size: small ? ButtonSize.Sm : ButtonSize.Md,
            // Evaluated per style pass, so it follows the connection state. The press guard in
            // the handler is what actually stops the action.
            style: () => Sx.Styles(
                opacity: disabled?.Invoke() == true ? 0.4f : 1f,
                borderColor: primary ? Theme.AccentBorder : new Color(128, 170, 255, 170)
            ),
            textStyle: () => Sx.TextStyles(
                fontSize: small ? 11 : 13,
                fontStyle: FontStyle.Bold,
                foregroundColor: Blue
            )
        );
    }
}
