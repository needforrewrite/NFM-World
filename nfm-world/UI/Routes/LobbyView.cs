using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.Hud;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Plugins;
using SignalsDotnet;
using LobbyPage = NFMWorld.UI.ViewModels.UiState.LobbyPage;

namespace NFMWorld.UI.Routes;

/// <summary>
/// The multiplayer lobby, in the main menu's blue. It has three full-area pages: the room
/// (players and games), the create-game flow and a game's match lobby.
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
        /// <summary>Opens a game's match lobby: slots, status and chat, then join or watch.</summary>
        Action<uint> OpenSession,
        /// <summary>Returns from a match lobby to the games list.</summary>
        Action CloseSession,
        /// <summary>Watch a race that is already running, without taking a slot.</summary>
        Action<uint> Spectate,
        Action<string> SetStage,
        /// <summary>The create page's search box: filters LOCAL, and is what ONLINE searches.</summary>
        Action<string> SetStageSearch,
        /// <summary>Switches the create page's stage list between local content and the archive.</summary>
        Action<bool> SetStageSource,
        /// <summary>Searches the public archive for stages. Enter in the search box.</summary>
        Action<string> SearchStagesOnline,
        Action<int> SetMaxPlayers,
        Action<int> SetBots,
        Action<string, string> SetConfig,
        Action<uint, string, string> SetGameRule,
        Action<string> SetGamemode,
        Action ChangeCar,
        Action<Guid> CopyCar,
        Action<Guid> CopyColors,
        Action<uint> PreviewStage
    );

    private static readonly Color Blue = Theme.MenuBlue;
    private static readonly Color TextBlue = Theme.MenuTextBlue;
    private static readonly Color PanelColor = Theme.MenuPanel;
    private static readonly Color Border = Theme.MenuBorder;
    private static readonly Color FieldFill = new(255, 255, 255, 16);
    private static readonly Color RowIdle = new(22, 42, 80, 110);
    private static readonly Color RowHover = new(34, 58, 100, 185);

    private static readonly Signal<string> ChatInput = new("");

    private static readonly string[] GamemodeIds = [.. GamemodeCatalog.Ids];
    private static readonly string[] GamemodeLabels = [.. GamemodeIds.Select(GamemodeCatalog.DisplayName)];
    private static readonly string[] PlayerCountLabels =
        [.. Enumerable.Range(2, MatchGameplayInfo.MaxPlayers - 1).Select(count => $"{count} players")];

    public static void Reset() => ChatInput.Value = "";

    public static Node Build(Actions actions, Node? stagePreview = null)
    {
        var lobby = UiState.Lobby;
        var contextPlayer = new Signal<Guid?>(null);
        var contextPosition = new Signal<Vector2>(default);

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
                    name: () => "Lobby window",
                    style: () => Sx.Styles(
                        width: 96.Percent(),
                        maxWidth: 2400,
                        height: 92.Percent(),
                        flexDirection: FlexDirection.Column,
                        backgroundColor: PanelColor,
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: Border,
                        borderRadius: 12,
                        paddingTop: 16,
                        paddingBottom: 14,
                        paddingLeft: 20,
                        paddingRight: 20,
                        gapRow: 12
                    ),
                    children:
                    [
                        Header(actions),
                        Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                flexGrow: 1,
                                flexShrink: 1,
                                minHeight: 0,
                                gapColumn: 16,
                                alignItems: Align.Stretch
                            ),
                            children:
                            [
                                PlayersRail((id, position) =>
                                    {
                                        var scale = Math.Max(G.Scale, 0.01f);
                                        contextPosition.Value = new Vector2(
                                            Math.Clamp(position.X / scale, 0, Math.Max(0, G.Viewport.X / scale - 170)),
                                            Math.Clamp(position.Y / scale, 0, Math.Max(0, G.Viewport.Y / scale - 94))
                                        );
                                        contextPlayer.Value = id;
                                    }
                                ),
                                Sx.View(
                                    style: () => Sx.Styles(
                                        flexGrow: 1,
                                        flexShrink: 1,
                                        minWidth: 0,
                                        minHeight: 0
                                    ),
                                    children:
                                    [
                                        Sx.Show(
                                            () => lobby.Page.Value == LobbyPage.Create,
                                            () => CreatePage(actions),
                                            () => Sx.Show(
                                                () => lobby.Page.Value == LobbyPage.Match,
                                                () => MatchPage(actions),
                                                () => RoomPage(actions)
                                            )
                                        )
                                    ]
                                )
                            ]
                        )
                    ]
                ),
                Sx.Show(
                    () => contextPlayer.Value.HasValue,
                    () => LobbyPlayerMenu.Build(
                        contextPosition.Value,
                        () => Copy(actions.CopyCar),
                        () => Copy(actions.CopyColors),
                        () => contextPlayer.Value = null
                    )
                ),
                stagePreview
            ]
        );

        void Copy(Action<Guid> action)
        {
            if (contextPlayer.Value is not { } id) return;
            contextPlayer.Value = null;
            action(id);
        }
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
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minWidth: 0),
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
                SmallButton("CHANGE CAR", actions.ChangeCar, () => "Lobby change car"),
                SmallButton("REFRESH", actions.Refresh, () => "Lobby refresh"),
                SmallButton("BACK", actions.Back, () => "Lobby back")
            ]
        );
    }

    // ── Room ───────────────────────────────────────────────────────

    private static Node RoomPage(Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                gapRow: 12
            ),
            children:
            [
                Sx.View(
                    name: () => "Lobby games",
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 2, height: 0, minHeight: 0),
                    children: GamesPanel(actions)
                ),
                ChatPanel(actions, () => 0, () => true, "ROOM CHAT", "Lobby room chat")
            ]
        );

    private static Node PlayersRail(Action<Guid, Vector2> openContextMenu)
    {
        var lobby = UiState.Lobby;
        return Sx.View(
            name: () => "Lobby players",
            style: () => Sx.Styles(
                width: 26.Percent(),
                minWidth: 240,
                maxWidth: 320,
                flexShrink: 0,
                flexDirection: FlexDirection.Column,
                minHeight: 0,
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
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        flexShrink: 0,
                        gapColumn: 8
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Blue
                            ),
                            text: () => "ROOM PLAYERS"
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => $"{lobby.Players.Value.Count}"
                        )
                    ]
                ),
                Sx.View(
                    name: () => "Lobby player list",
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
                        each: () => lobby.Players.Value,
                        row: (player, _) => PlayerRow(player, openContextMenu)
                    )
                )
            ]
        );
    }

    private static Node PlayerRow(UiState.LobbyPlayer player, Action<Guid, Vector2> openContextMenu)
    {
        var hovered = new Signal<bool>(false);
        return Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                paddingTop: 6,
                paddingBottom: 6,
                paddingLeft: 8,
                paddingRight: 8,
                gapRow: 2,
                borderRadius: 6,
                backgroundColor: hovered.Value ? Theme.MenuHighlight : new Color(128, 170, 255, 20)
            ),
            onmousedown: e =>
            {
                if (e.Button != MouseButton.Secondary || player.IsSelf) return;
                e.StopPropagation();
                openContextMenu(player.Id, e.Position);
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        gapColumn: 5
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
                        Sx.Show(
                            () => player.Role >= NFMWorldLibrary.Multiplayer.UserRole.Moderator,
                            () => Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                                text: () => player.Role == NFMWorldLibrary.Multiplayer.UserRole.Admin
                                    ? "[ADMIN]"
                                    : "[MOD]"
                            )
                        ),
                        Sx.For(
                            () => (player.CarColors ?? []).Select((color, slot) => (color, slot)).ToArray(),
                            (swatch, _) => Sx.View(
                                style: () => Sx.Styles(
                                    width: 10,
                                    height: 10,
                                    flexShrink: 0,
                                    backgroundColor: swatch.color,
                                    borderTop: 1,
                                    borderBottom: 1,
                                    borderLeft: 1,
                                    borderRight: 1,
                                    borderColor: Border
                                )
                            )
                        )
                    ]
                ),
                Sx.Text(
                    style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                    text: () => player.Car
                )
            ]
        );
    }

    private static Node GamesPanel(Actions actions)
    {
        var lobby = UiState.Lobby;
        return Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                height: 0,
                minHeight: 0,
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
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        flexShrink: 0,
                        gapColumn: 10
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Blue
                            ),
                            text: () => "GAMES"
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => lobby.Sessions.Value.Count switch
                            {
                                0 => "",
                                1 => "1 game",
                                var count => $"{count} games"
                            }
                        ),
                        Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                        ActionButton(
                            "CREATE GAME",
                            actions.OpenCreate,
                            primary: true,
                            small: true,
                            disabled: () => !lobby.Connected.Value,
                            name: () => "Lobby create game"
                        )
                    ]
                ),
                GameColumns(),
                Sx.View(
                    name: () => "Lobby games list",
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        minHeight: 0,
                        overflow: Overflow.Scroll,
                        gapRow: 4,
                        paddingRight: 8
                    ),
                    children:
                    [
                        Sx.Show(
                            when: () => lobby.Sessions.Value.Count == 0,
                            then: () => Sx.View(
                                style: () => Sx.Styles(
                                    flexGrow: 1,
                                    alignItems: Align.Center,
                                    justifyContent: Justify.Center,
                                    paddingTop: 20,
                                    paddingBottom: 20
                                ),
                                children: Sx.Text(
                                    style: () => Sx.TextStyles(fontSize: 13, foregroundColor: Theme.MenuDim),
                                    text: () => "No games yet. Create one, or wait for someone to host."
                                )
                            ),
                            otherwise: () => Sx.For(
                                each: () => lobby.Sessions.Value,
                                row: (session, _) => SessionRow(session, actions)
                            )
                        )
                    ]
                )
            ]
        );
    }

    private static Node GameColumns()
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                flexShrink: 0,
                gapColumn: 10,
                paddingLeft: 10,
                paddingRight: 10
            ),
            children:
            [
                ColumnLabel("STAGE", flexGrow: 1),
                ColumnLabel("PLAYERS", width: 76),
                ColumnLabel("STATUS", width: 96),
                Sx.View(style: () => Sx.Styles(width: 62))
            ]
        );

    private static Node ColumnLabel(string label, float? width = null, float? flexGrow = null)
        => Sx.Text(
            style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.MenuDim),
            layout: () => Sx.Styles(width: width, flexGrow: flexGrow, flexShrink: width is null ? null : 0f),
            text: () => label
        );

    private static Node SessionRow(UiState.LobbySession session, Actions actions)
    {
        var lobby = UiState.Lobby;
        var hovered = new Signal<bool>(false);

        // This row is built once and never re-rendered, so the selected check has to live in
        // the style accessor: selection changes without the record changing.
        return Sx.View(
            name: () => $"Lobby game {session.Id}",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                flexShrink: 0,
                gapColumn: 10,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 10,
                paddingRight: 10,
                borderRadius: 6,
                backgroundColor: lobby.SelectedSessionId.Value == session.Id
                    ? Theme.MenuHighlight
                    : hovered.Value ? RowHover : RowIdle
            ),
            onmousedown: e =>
            {
                if (e.Button == MouseButton.Primary)
                    actions.OpenSession(session.Id);
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        minWidth: 0,
                        gapRow: 1
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.WrappingText(
                                fontSize: 14,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: TextBlue
                            ),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => session.StageName
                        ),
                        Sx.Text(
                            style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => session.IsMember
                                ? $"{session.GamemodeLabel} · {session.CreatorName} · your game"
                                : $"{session.GamemodeLabel} · {session.CreatorName}"
                        )
                    ]
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                    layout: () => Sx.Styles(width: 76, flexShrink: 0),
                    text: () => $"{session.PlayerCount}/{session.MaxPlayers}"
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        width: 96,
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column,
                        alignItems: Align.FlexStart
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: StateColor(session.State)
                            ),
                            text: () => StateLabel(session.State)
                        ),
                        Sx.Show(
                            () => session.State == SessionState.Finished && session.ClosesAtUnixMs > 0,
                            () => Sx.View(
                                name: () => $"Lobby game {session.Id} countdown",
                                children: Sx.Text(
                                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.MenuDim),
                                    text: () => CountdownShort(session)
                                )
                            )
                        )
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(width: 62, flexShrink: 0, alignItems: Align.FlexEnd),
                    children: ActionButton(
                        () => "VIEW",
                        () => actions.OpenSession(session.Id),
                        small: true,
                        name: () => $"Lobby game view {session.Id}"
                    )
                )
            ]
        );
    }

    private static Color StateColor(SessionState state) => state switch
    {
        SessionState.Started => Theme.Success,
        SessionState.WaitingToLoad => Theme.Warning,
        SessionState.Finished => Theme.MenuDim,
        _ => Theme.MenuDim
    };

    private static string StateLabel(SessionState state) => state switch
    {
        SessionState.Started => "RACING",
        SessionState.WaitingToLoad => "LOADING",
        SessionState.Finished => "FINISHED",
        _ => "OPEN"
    };

    // ── Match lobby ────────────────────────────────────────────────

    private static Node MatchPage(Actions actions)
    {
        var lobby = UiState.Lobby;
        var showRules = new Signal<bool>(false);
        UiState.LobbySession? Session() =>
            lobby.Sessions.Value.FirstOrDefault(session => session.Id == lobby.ViewedSessionId.Value);

        return Sx.View(
            name: () => "Lobby match",
            style: () => Sx.Styles(
                position: Position.Relative,
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                gapRow: 12
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        flexShrink: 0,
                        gapColumn: 10
                    ),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minWidth: 0),
                            children:
                            [
                                Sx.Text(
                                    style: () => Sx.WrappingText(
                                        fontSize: 22,
                                        fontFamily: FontFamily.Adventure,
                                        fontStyle: FontStyle.Bold,
                                        foregroundColor: Blue
                                    ),
                                    layout: () => Sx.Styles(width: 100.Percent()),
                                    text: () => Session()?.StageName ?? "GAME"
                                ),
                                Sx.Text(
                                    style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                                    layout: () => Sx.Styles(width: 100.Percent()),
                                    text: () => Session() is { } session
                                        ? $"{session.GamemodeLabel} · hosted by {session.CreatorName} · {StateLabel(session.State)}"
                                        : "This game is no longer listed."
                                )
                            ]
                        ),
                        ActionButton(
                            "BACK TO GAMES",
                            actions.CloseSession,
                            small: true,
                            name: () => "Lobby match back"
                        ),
                        // The host starts. A member only leaves; a first-time viewer joins or
                        // watches. The phase re-checks all of these conditions.
                        Sx.Show(
                            () => Session() is { IsMember: true, IsCreator: true, State: SessionState.NotStarted },
                            () => ActionButton(
                                "START GAME",
                                () => actions.StartRace(lobby.ViewedSessionId.Value ?? 0),
                                primary: true,
                                small: true,
                                disabled: () => !lobby.Connected.Value,
                                name: () => "Lobby match start"
                            )
                        ),
                        Sx.Show(
                            () => Session() is { IsMember: true },
                            () => ActionButton(
                                "LEAVE GAME",
                                actions.Leave,
                                small: true,
                                disabled: () => !lobby.Connected.Value,
                                name: () => "Lobby match leave"
                            )
                        ),
                        Sx.Show(
                            () => Session() is { IsMember: false, State: SessionState.NotStarted, EmptySlots: > 0 },
                            () => ActionButton(
                                "JOIN GAME",
                                () => actions.Join(lobby.ViewedSessionId.Value ?? 0),
                                primary: true,
                                small: true,
                                disabled: () => !lobby.Connected.Value,
                                name: () => "Lobby match join"
                            )
                        ),
                        Sx.Show(
                            () => Session() is { IsMember: false, State: SessionState.Started },
                            () => ActionButton(
                                "WATCH RACE",
                                () => actions.Spectate(lobby.ViewedSessionId.Value ?? 0),
                                primary: true,
                                small: true,
                                disabled: () => !lobby.Connected.Value,
                                name: () => "Lobby match watch"
                            )
                        )
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        flexGrow: 1,
                        flexShrink: 1,
                        minHeight: 0,
                        gapColumn: 14,
                        alignItems: Align.Stretch
                    ),
                    children:
                    [
                        SlotsPanel(Session),
                        Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Column,
                                flexGrow: 1,
                                flexShrink: 1,
                                minWidth: 0,
                                minHeight: 0,
                                gapRow: 12
                            ),
                            children:
                            [
                                StatusPanel(Session),
                                StageCard(Session, actions.PreviewStage, () => showRules.Value = true),
                                ChatPanel(
                                    actions,
                                    () => Session()?.Id ?? 0,
                                    () => Session() is { CanChat: true },
                                    "GAME CHAT",
                                    "Lobby match chat"
                                )
                            ]
                        )
                    ]
                ),
                Sx.Show(
                    () => showRules.Value,
                    () => MatchOverlayView(() => "GAME RULES", RulesContent(Session, actions), () => showRules.Value = false)
                )
            ]
        );
    }

    private static Node SlotsPanel(Func<UiState.LobbySession?> session)
    {
        return Sx.View(
            style: () => Sx.Styles(
                width: 40.Percent(),
                minWidth: 280,
                maxWidth: 400,
                flexShrink: 0,
                flexDirection: FlexDirection.Column,
                minHeight: 0,
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
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        flexShrink: 0,
                        gapColumn: 8
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Blue
                            ),
                            text: () => session()?.State == SessionState.Finished ? "PARTICIPANTS" : "PLAYERS"
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => session() is { } s
                                ? s.State == SessionState.Finished ? s.Slots.Count.ToString() : $"{s.PlayerCount}/{s.MaxPlayers}"
                                : ""
                        )
                    ]
                ),
                Sx.View(
                    name: () => "Lobby match slots",
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        minHeight: 0,
                        overflow: Overflow.Scroll,
                        gapRow: 5,
                        paddingRight: 8
                    ),
                    children: Sx.For(
                        each: () => session()?.Slots ?? [],
                        row: (slot, _) => SlotRow(slot)
                    )
                )
            ]
        );
    }

    private static Node SlotRow(UiState.LobbySessionSlot slot)
    {
        return Sx.View(
            name: () => $"Lobby match slot {slot.Slot + 1}",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                flexShrink: 0,
                gapColumn: 10,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 10,
                paddingRight: 10,
                borderRadius: 6,
                backgroundColor: slot.IsEmpty
                    ? new Color(128, 170, 255, 10)
                    : slot.IsSelf ? Theme.MenuHighlight : new Color(128, 170, 255, 24),
                opacity: slot.IsEmpty ? 0.65f : 1f
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                    layout: () => Sx.Styles(width: 18),
                    text: () => (slot.Slot + 1).ToString()
                ),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minWidth: 0),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 14,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: slot.IsEmpty ? Theme.MenuDim : slot.IsSelf ? Blue : TextBlue
                            ),
                            text: () => slot.Name
                        ),
                        Sx.Text(
                            style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => slot.Car
                        )
                    ]
                ),
                Sx.Show(
                    () => slot.IsBot,
                    () => Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.Warning),
                        text: () => "BOT"
                    )
                ),
                Sx.For(
                    () => (slot.CarColors ?? []).Select((color, index) => (color, index)).ToArray(),
                    (swatch, _) => Sx.View(
                        style: () => Sx.Styles(
                            width: 12,
                            height: 12,
                            flexShrink: 0,
                            backgroundColor: swatch.color,
                            borderTop: 1,
                            borderBottom: 1,
                            borderLeft: 1,
                            borderRight: 1,
                            borderColor: Border
                        )
                    )
                )
            ]
        );
    }

    private static Node StatusPanel(Func<UiState.LobbySession?> session)
        => Sx.View(
            name: () => "Lobby match status",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 3,
                flexShrink: 1,
                height: 0,
                minHeight: 0,
                gapRow: 8,
                paddingTop: 12,
                paddingBottom: 12,
                paddingLeft: 14,
                paddingRight: 14,
                backgroundColor: new Color(22, 42, 80, 120),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 110),
                borderRadius: 8
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        flexShrink: 0,
                        gapColumn: 8
                    ),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Blue
                            ),
                            text: () => "STATUS"
                        ),
                        Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: StatusHeadingColor(session()?.State)
                            ),
                            text: () => session() is { } current ? StateLabel(current.State) : "—"
                        )
                    ]
                ),
                Sx.Show(
                    () => session() is { State: SessionState.Started, LiveStandings.Count: > 0 },
                    () => StandingsTable(session, final: false)
                ),
                Sx.Show(
                    () => session() is { State: SessionState.Finished, FinalStandings.Count: > 0 },
                    () => StandingsTable(session, final: true)
                ),
                Sx.Show(
                    () => session() is not { State: SessionState.Started, LiveStandings.Count: > 0 },
                    () => Sx.Text(
                        style: () => Sx.WrappingText(fontSize: 13, foregroundColor: TextBlue),
                        layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                        text: () => StatusDetail(session())
                    )
                ),
                Sx.Show(
                    () => session() is { State: SessionState.Finished } && session()!.ClosesAtUnixMs > 0,
                    () => Sx.View(
                        name: () => "Lobby match countdown",
                        children: Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 13,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: Theme.Warning
                            ),
                            text: () => CountdownText(session())
                        )
                    )
                )
            ]
        );

    private static Color StatusHeadingColor(SessionState? state) => state switch
    {
        SessionState.Started => Theme.Success,
        SessionState.Finished => Blue,
        SessionState.WaitingToLoad => Theme.Warning,
        _ => Theme.MenuDim
    };

    private static string StatusDetail(UiState.LobbySession? session)
    {
        if (session is null)
            return "This game is no longer listed.";

        if (session.State == SessionState.Finished)
        {
            if (session.FinalStandings.FirstOrDefault() is { Position: 0, Dnf: false } winner)
                return $"Game finished · Winner: {winner.Name}";
            return session.FinalStandings.Count == 0 ? "No results were recorded." : "Game finished";
        }

        if (session.State == SessionState.Started)
            return "Waiting for race stats…";

        if (session.State == SessionState.WaitingToLoad)
            return "The race is starting. Hang tight.";

        return session switch
        {
            { IsCreator: true, EmptySlots: 0 } => "You have created this game. Start when ready.",
            { IsCreator: true } => $"Waiting for {session.EmptySlots} more player(s) to join to start.",
            { IsMember: true } => $"{session.CreatorName} starts the race when everyone is ready.",
            { EmptySlots: 0 } => "This game is full.",
            _ => "Open game. Join to take a free slot."
        };
    }

    private static Node StandingsTable(Func<UiState.LobbySession?> session, bool final)
        => Sx.View(
            name: () => final ? "Lobby match final standings" : "Lobby match live standings",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                gapRow: 4
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        flexShrink: 0,
                        paddingRight: 8
                    ),
                    children:
                    [
                        StandingLabel("PLAYER", 30, Theme.MenuDim, centered: false),
                        StandingLabel("POSITION", 16, Theme.MenuDim),
                        StandingLabel("LAPS", 14, Theme.MenuDim),
                        StandingLabel("POWER", 20, Theme.MenuDim),
                        StandingLabel("DAMAGE", 20, Theme.MenuDim)
                    ]
                ),
                Sx.View(
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
                        each: () => final
                            ? session()?.FinalStandings ?? []
                            : session()?.LiveStandings ?? [],
                        row: (standing, _) => StandingRow(standing)
                    )
                )
            ]
        );

    private static Node StandingRow(UiState.LobbyStanding standing)
    {
        return Sx.View(
            name: () => $"Lobby standing {standing.Name}",
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                flexShrink: 0,
                paddingTop: 4,
                paddingBottom: 4,
                borderRadius: 4,
                backgroundColor: new Color(128, 170, 255, 14)
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        width: 30.Percent(),
                        minWidth: 0,
                        paddingLeft: 6,
                        alignItems: Align.Center,
                        gapColumn: 5
                    ),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(width: 7, height: 7, flexShrink: 0, backgroundColor: standing.Color)
                        ),
                        Sx.Text(
                            style: () => Sx.WrappingText(fontSize: 11, foregroundColor: TextBlue),
                            layout: () => Sx.Styles(flexGrow: 1, minWidth: 0),
                            text: () => standing.Name
                        )
                    ]
                ),
                .. (standing.Wasted || standing.Dnf
                    ? new Node[]
                    {
                        Sx.View(
                            style: () => Sx.Styles(
                                width: 70.Percent(),
                                minWidth: 0,
                                backgroundColor: new Color(172, 44, 54, 55)
                            ),
                            children: StandingLabel(
                                standing.Wasted ? "WASTED" : "DNF",
                                100,
                                Theme.Danger,
                                name: $"Lobby result {standing.Name}"
                            )
                        )
                    }
                    : new Node[]
                    {
                        StandingLabel(PositionLabel(standing.Position + 1), 16, TextBlue),
                        StandingLabel(standing.TotalLaps > 0
                            ? $"{Math.Min(standing.Lap + 1, standing.TotalLaps)}/{standing.TotalLaps}" : "—", 14, TextBlue),
                        StandingMeter(standing.PowerPercent, $"Lobby power {standing.Name}"),
                        StandingMeter(standing.DamagePercent, $"Lobby damage {standing.Name}", damage: true)
                    })
            ]
        );
    }

    private static string PositionLabel(int position)
    {
        var suffix = position is 11 or 12 or 13 ? "th" : (position % 10) switch
        {
            1 => "st", 2 => "nd", 3 => "rd", _ => "th"
        };
        return $"{position}{suffix}";
    }

    private static Node StandingLabel(
        string text,
        int width,
        Color color,
        bool centered = true,
        string? name = null
    )
    {
        var label = Sx.Text(
            style: () => Sx.WrappingText(
                fontSize: 10,
                fontStyle: FontStyle.Bold,
                foregroundColor: color,
                horizontalAlignment: centered ? TextHorizontalAlignment.Center : TextHorizontalAlignment.Left
            ),
            layout: () => Sx.Styles(
                width: width.Percent(),
                minWidth: 0,
                paddingLeft: centered ? 0 : 6
            ),
            text: () => text
        );
        label.Name = name;
        return label;
    }

    private static Node StandingMeter(byte? percent, string name, bool damage = false)
    {
        var fraction = Math.Min((percent ?? 0) / 100f, 1f);
        var fill = damage ? RaceMeterColors.Damage(fraction) : RaceMeterColors.Power(fraction);
        var track = new StandingMeterTrack(fraction, fill)
        {
            Name = name + " track",
            Styles = Sx.Styles(
                width: 100.Percent(),
                height: 14,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: new Color(8, 17, 36, 190),
                borderRadius: 3,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Border
            )
        };
        track.AddChild(Sx.Text(
            style: () => Sx.TextStyles(
                fontSize: 9,
                fontStyle: FontStyle.Bold,
                foregroundColor: Color.White,
                strokeColor: Color.Black,
                strokeWidth: 1
            ),
            text: () => percent is { } value ? $"{value}%" : "—"
        ));

        return Sx.View(
            name: () => name,
            style: () => Sx.Styles(width: 20.Percent(), minWidth: 0, paddingLeft: 3, paddingRight: 3),
            children: track
        );
    }

    private sealed class StandingMeterTrack(float fraction, Color fill) : View
    {
        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            var left = (int)MathF.Ceiling(position.X);
            var top = (int)MathF.Ceiling(position.Y);
            var width = (int)MathF.Floor(position.X + size.X * fraction) - left;
            var height = (int)MathF.Floor(position.Y + size.Y) - top;
            if (width <= 0 || height <= 0) return;

            G.SaveState();
            G.SetColor(fill);
            G.FillRect(left, top, width, height);
            G.RestoreState();
        }
    }

    private static Node StageCard(Func<UiState.LobbySession?> session, Action<uint> previewStage, Action showRules)
    {
        var hovered = new Signal<bool>(false);

        return Sx.View(
            name: () => "Lobby match stage",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                flexShrink: 0,
                gapColumn: 12,
                paddingTop: 8,
                paddingBottom: 8,
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
                    layout: () => Sx.Styles(width: 52, flexShrink: 0),
                    text: () => "STAGE"
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        position: Position.Relative,
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        minWidth: 0
                    ),
                    onmouseenter: _ => hovered.Value = true,
                    onmouseleave: _ => hovered.Value = false,
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                alignItems: Align.Center,
                                gapColumn: 6
                            ),
                            children:
                            [
                                Sx.Text(
                                    style: () => Sx.WrappingText(
                                        fontSize: 14,
                                        fontStyle: FontStyle.Bold,
                                        foregroundColor: TextBlue
                                    ),
                                    text: () => session()?.StageName ?? "—"
                                ),
                                Sx.Text(
                                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                                    text: () => "▾"
                                )
                            ]
                        ),
                        Sx.Show(
                            () => hovered.Value,
                            () => Sx.View(
                                name: () => "Lobby stage actions",
                                style: () => Sx.Styles(
                                    position: Position.Absolute,
                                    top: 100.Percent(),
                                    left: 0,
                                    zIndex: 1000,
                                    minWidth: 210,
                                    flexDirection: FlexDirection.Column,
                                    gapRow: 4,
                                    paddingTop: 6,
                                    paddingBottom: 6,
                                    paddingLeft: 6,
                                    paddingRight: 6,
                                    backgroundColor: new Color(10, 20, 40, 250),
                                    borderTop: 1,
                                    borderBottom: 1,
                                    borderLeft: 1,
                                    borderRight: 1,
                                    borderColor: Border,
                                    borderRadius: 7
                                ),
                                onmousedown: e => e.StopPropagation(),
                                children:
                                [
                                    StageActionButton(
                                        "PREVIEW STAGE",
                                        () =>
                                        {
                                            hovered.Value = false;
                                            if (session() is { } match) previewStage(match.Id);
                                        },
                                        () => "Lobby stage preview button"
                                    ),
                                    StageActionButton(
                                        "GAME RULES",
                                        () =>
                                        {
                                            hovered.Value = false;
                                            showRules();
                                        },
                                        () => "Lobby game rules button"
                                    )
                                ]
                            )
                        )
                    ]
                )
            ]
        );
    }

    private static Node StageActionButton(string label, Action onPress, Func<string> name)
        => Button.Create(
            () => label,
            onPress,
            size: ButtonSize.Sm,
            fullWidth: true,
            style: () => Sx.Styles(
                justifyContent: Justify.FlexStart,
                borderColor: new Color(128, 170, 255, 80)
            ),
            textStyle: () => Sx.TextStyles(fontSize: 11, foregroundColor: TextBlue),
            name: name
        );

    internal static Node StagePreviewWindow(Func<string> title, StageSelectState state, Func<IImage?> image, Action close)
        => MatchOverlayView(title, StageSelectView.ReadOnlyPreview(state, image), close, stagePreview: true);

    private static Node MatchOverlayView(Func<string> title, Node content, Action close, bool stagePreview = false)
        => Sx.View(
            name: () => "Lobby match overlay",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                right: 0,
                bottom: 0,
                zIndex: 2000,
                backgroundColor: new Color(3, 8, 20, 210),
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            onmousedown: _ => close(),
            children:
            [
                Sx.View(
                    name: () => "Lobby match overlay card",
                    style: () => Sx.Styles(
                        width: stagePreview ? 900 : 640,
                        height: stagePreview ? 540 : null,
                        maxWidth: 90.Percent(),
                        maxHeight: stagePreview ? 90.Percent() : 82.Percent(),
                        minHeight: 0,
                        flexDirection: FlexDirection.Column,
                        gapRow: 10,
                        paddingTop: 16,
                        paddingBottom: 16,
                        paddingLeft: 18,
                        paddingRight: stagePreview ? 18 : 28,
                        overflow: stagePreview ? Overflow.Hidden : Overflow.Scroll,
                        backgroundColor: PanelColor,
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: Border,
                        borderRadius: 10
                    ),
                    onmousedown: e => e.StopPropagation(),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                alignItems: Align.Center,
                                flexShrink: 0,
                                gapColumn: 10
                            ),
                            children:
                            [
                                Sx.Text(
                                    style: () => Sx.WrappingText(
                                        fontSize: 20,
                                        fontFamily: FontFamily.Adventure,
                                        fontStyle: FontStyle.Bold,
                                        foregroundColor: Blue
                                    ),
                                    layout: () => Sx.Styles(flexGrow: 1, minWidth: 0),
                                    text: title
                                ),
                                ActionButton(
                                    "CLOSE",
                                    close,
                                    small: true,
                                    name: () => "Lobby match overlay close"
                                )
                            ]
                        ),
                        content
                    ]
                )
            ]
        );

    private static Node RulesContent(Func<UiState.LobbySession?> session, Actions actions)
        => Sx.View(
            name: () => "Lobby game rules",
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0, gapRow: 4),
            children:
            [
                DetailRow("Gamemode", () => session()?.GamemodeLabel ?? "—"),
                DetailRow("Host", () => session()?.CreatorName ?? "—"),
                DetailRow("Players", () => session() is { } s ? $"{s.PlayerCount}/{s.MaxPlayers}" : "—"),
                DetailRow("Bots", () => session()?.Slots.Count(slot => slot.IsBot).ToString() ?? "0"),
                Sx.For(
                    each: () => session()?.Settings ?? [],
                    row: (setting, _) => RuleRow(session, setting, actions)
                )
            ]
        );

    private static Node RuleRow(Func<UiState.LobbySession?> session, UiState.LobbySessionSetting setting, Actions actions)
    {
        if (setting.Field is not { Choices.Count: > 0 } field)
            return DetailRow(setting.Label, () => setting.Value);

        var options = field.Choices.ToArray();
        var labels = options.Select(option => option.Label).ToArray();
        var selected = Array.FindIndex(options, option => option.Value == setting.RawValue);
        bool CanEdit() => UiState.Lobby.Connected.Value
                          && session() is { IsCreator: true, IsMember: true, State: SessionState.NotStarted };

        return DetailRow(setting.Label, Sx.Show(
            CanEdit,
            () => Controls.Dropdown(
                () => labels,
                () => selected,
                index =>
                {
                    if (CanEdit() && session() is { } match && index != selected)
                        actions.SetGameRule(match.Id, field.Name, options[index].Value);
                },
                unselectedLabel: setting.Value,
                compact: true,
                name: $"Lobby game rule {field.Name}"
            ),
            () => DetailValue(() => setting.Value)
        ));
    }

    private static Node DetailValue(Func<string> value)
        => Sx.Text(style: () => Sx.WrappingText(fontSize: 12, foregroundColor: TextBlue), text: value);

    private static Node DetailRow(string label, Func<string> value) => DetailRow(label, DetailValue(value));

    private static Node DetailRow(string label, Node value)
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                flexShrink: 0,
                gapColumn: 12,
                paddingTop: 2,
                paddingBottom: 2
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                    layout: () => Sx.Styles(width: 130),
                    text: () => label
                ),
                Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, minWidth: 0),
                    children: value
                )
            ]
        );

    private static int RemainingSeconds(UiState.LobbySession? session)
    {
        var now = UiState.Lobby.NowUnixMs.Value;
        if (session is null || session.ClosesAtUnixMs <= 0 || now <= 0)
            return -1;

        var remaining = session.ClosesAtUnixMs - now;
        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining / 1000.0);
    }

    private static string CountdownText(UiState.LobbySession? session)
    {
        var seconds = RemainingSeconds(session);
        return seconds switch
        {
            < 0 => "",
            0 => "Game closes now...",
            _ => $"Game closes in {FormatCountdown(seconds)}"
        };
    }

    private static string CountdownShort(UiState.LobbySession session)
    {
        var seconds = RemainingSeconds(session);
        return seconds switch
        {
            < 0 => "",
            0 => "closing",
            _ => FormatCountdown(seconds)
        };
    }

    private static string FormatCountdown(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    // ── Create game ────────────────────────────────────────────────

    private static Node CreatePage(Actions actions)
    {
        var lobby = UiState.Lobby;
        return Sx.View(
            name: () => "Lobby create",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                gapRow: 12
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        flexShrink: 0,
                        gapColumn: 10
                    ),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minWidth: 0),
                            children:
                            [
                                Sx.Text(
                                    style: () => Sx.TextStyles(
                                        fontSize: 24,
                                        fontFamily: FontFamily.Adventure,
                                        fontStyle: FontStyle.Bold,
                                        foregroundColor: Blue
                                    ),
                                    text: () => "CREATE GAME"
                                ),
                                Sx.Text(
                                    style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                                    layout: () => Sx.Styles(width: 100.Percent()),
                                    text: () => "Pick a stage and rules. Shipped stages and public archive stages work online."
                                )
                            ]
                        ),
                        ActionButton(
                            "CANCEL",
                            actions.CloseCreate,
                            small: true,
                            name: () => "Lobby create cancel"
                        ),
                        ActionButton(
                            "CREATE GAME",
                            actions.Create,
                            primary: true,
                            disabled: () => !lobby.Connected.Value || lobby.SelectedStage.Value.Length == 0,
                            name: () => "Lobby create confirm"
                        )
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        flexGrow: 1,
                        flexShrink: 1,
                        minHeight: 0,
                        gapColumn: 14,
                        alignItems: Align.Stretch
                    ),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(
                                flexGrow: 3,
                                flexShrink: 1,
                                minWidth: 0,
                                minHeight: 0,
                                flexDirection: FlexDirection.Column,
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
                                Sx.View(
                                    style: () => Sx.Styles(
                                        flexDirection: FlexDirection.Row,
                                        alignItems: Align.Center,
                                        flexShrink: 0,
                                        gapColumn: 8
                                    ),
                                    children:
                                    [
                                        Sx.Text(
                                            style: () => Sx.TextStyles(
                                                fontSize: 11,
                                                fontStyle: FontStyle.Bold,
                                                foregroundColor: Blue
                                            ),
                                            text: () => "STAGE"
                                        ),
                                        Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                                        Sx.Text(
                                            style: () => Sx.WrappingText(fontSize: 11, foregroundColor: TextBlue),
                                            layout: () => Sx.Styles(maxWidth: 70.Percent()),
                                            text: () => SelectedStageLabel()
                                        )
                                    ]
                                ),
                                StageSelectView.Compact(
                                    lobby.CreateStages,
                                    new StageSelectView.Actions(
                                        CollectionSelected: _ => { },
                                        StageSelected: actions.SetStage,
                                        SearchChanged: actions.SetStageSearch,
                                        Confirm: actions.Create,
                                        Back: actions.CloseCreate,
                                        SourceSelected: actions.SetStageSource,
                                        OnlineSearch: actions.SearchStagesOnline
                                    )
                                )
                            ]
                        ),
                        Sx.View(
                            style: () => Sx.Styles(
                                width: 40.Percent(),
                                minWidth: 320,
                                maxWidth: 460,
                                flexShrink: 0,
                                flexDirection: FlexDirection.Column,
                                minHeight: 0,
                                paddingTop: 10,
                                paddingBottom: 10,
                                paddingLeft: 12,
                                paddingRight: 28,
                                overflow: Overflow.Scroll,
                                gapRow: 10,
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
                                FieldLabel("GAMEMODE"),
                                Controls.Dropdown(
                                    () => GamemodeLabels,
                                    () => Math.Max(0, Array.IndexOf(GamemodeIds, lobby.SelectedGamemode.Value)),
                                    index => actions.SetGamemode(GamemodeIds[index]),
                                    width: 380,
                                    name: "Create gamemode"
                                ),
                                FieldLabel("MAX PLAYERS"),
                                Controls.Dropdown(
                                    () => PlayerCountLabels,
                                    () => Math.Clamp(lobby.MaxPlayers.Value - 2, 0, MatchGameplayInfo.MaxPlayers - 2),
                                    index => actions.SetMaxPlayers(index + 2),
                                    width: 380,
                                    name: "Create max players"
                                ),
                                Sx.Show(
                                    () => lobby.SelectedGamemode.Value != DefaultGamemodes.TimeTrial,
                                    () => Sx.View(
                                        style: () => Sx.Styles(
                                            flexDirection: FlexDirection.Column,
                                            flexShrink: 0,
                                            gapRow: 10
                                        ),
                                        children:
                                        [
                                            FieldLabel("BOTS"),
                                            Controls.Dropdown(
                                                () => BotCountOptions(lobby.MaxPlayers.Value),
                                                () => Math.Clamp(lobby.BotCount.Value, 0, Math.Max(0, lobby.MaxPlayers.Value - 1)),
                                                index => actions.SetBots(index),
                                                width: 380,
                                                name: "Create bots"
                                            ),
                                            Sx.Text(
                                                style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.MenuDim),
                                                text: () => "Bots take slots other players could join."
                                            )
                                        ]
                                    )
                                ),
                                (Func<object?>)(() => Sx.View(
                                    style: () => Sx.Styles(
                                        flexDirection: FlexDirection.Column,
                                        flexShrink: 0,
                                        gapRow: 10
                                    ),
                                    children: [.. GamemodeConfigControls(actions)]
                                )),
                                Sx.View(
                                    style: () => Sx.Styles(
                                        flexDirection: FlexDirection.Column,
                                        flexShrink: 0,
                                        gapRow: 3,
                                        paddingTop: 8,
                                        borderTop: 1,
                                        borderColor: new Color(128, 170, 255, 40)
                                    ),
                                    children:
                                    [
                                        Sx.Text(
                                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                                            text: () => "SUMMARY"
                                        ),
                                        Sx.Text(
                                            style: () => Sx.WrappingText(fontSize: 12, foregroundColor: TextBlue),
                                            text: () => SelectedStageLabel()
                                        ),
                                        Sx.Text(
                                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                                            text: () => $"{GamemodeCatalog.DisplayName(lobby.SelectedGamemode.Value)} · {FieldSummary()}"
                                        )
                                    ]
                                )
                            ]
                        )
                    ]
                )
            ]
        );

        string SelectedStageLabel()
        {
            var selected = lobby.SelectedStage.Value;
            if (selected.Length == 0) return "No stage selected";
            return lobby.CreateStages.Stages.Value.FirstOrDefault(option => option.Id == selected)?.Label
                   ?? (selected.StartsWith("archive/", StringComparison.OrdinalIgnoreCase)
                       ? "Public stage " + selected[^8..]
                       : selected);
        }

        string FieldSummary()
        {
            var humans = lobby.MaxPlayers.Value - (lobby.SelectedGamemode.Value == DefaultGamemodes.TimeTrial ? 0 : lobby.BotCount
                .Value);
            humans = Math.Max(1, humans);
            var bots = lobby.SelectedGamemode.Value == DefaultGamemodes.TimeTrial ? 0 : lobby.BotCount.Value;
            return bots == 0
                ? $"{humans} player(s) max"
                : $"{humans} human(s) + {bots} bot(s)";
        }
    }

    private static string[] BotCountOptions(int maxPlayers)
        => [.. Enumerable.Range(0, Math.Max(1, maxPlayers))
            .Select(count => count switch
            {
                0 => "No bots",
                1 => "1 bot",
                _ => $"{count} bots"
            })];

    private static Node FieldLabel(string label)
        => Sx.Text(
            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
            layout: () => Sx.Styles(flexShrink: 0),
            text: () => label
        );

    /// <summary>
    /// One row per config field the phase read from the selected gamemode's plugin. The view
    /// only renders <see cref="UiState.LobbyState.GamemodeFields"/>, because reading the plugin
    /// here would load and unload its assembly every time this slot re-runs.
    /// </summary>
    private static List<Node> GamemodeConfigControls(Actions actions)
    {
        var lobby = UiState.Lobby;
        var controls = new List<Node>();

        foreach (var configField in lobby.GamemodeFields.Value)
        {
            // The option list is fixed for the field, so only the chosen index is reactive.
            var options = configField.Choices;
            var labels = options.Select(option => option.Label).ToArray();

            controls.Add(
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                    text: () => configField.Label
                )
            );

            controls.Add(
                Controls.Dropdown(
                    () => labels,
                    () => SelectedOption(options, lobby.GamemodeConfig.Value, configField.Name),
                    index => actions.SetConfig(configField.Name, options[index].Value),
                    width: 380,
                    name: $"Gamemode config {configField.Name}"
                )
            );
        }

        return controls;
    }

    /// <summary>The chosen option's index, or -1 when the config holds a value the plugin does not list.</summary>
    private static int SelectedOption(
        IReadOnlyList<GamemodeConfigChoice> options,
        IReadOnlyDictionary<string, string> config,
        string name
    )
    {
        if (!config.TryGetValue(name, out var chosen))
            return -1;

        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].Value == chosen)
                return i;
        }

        return -1;
    }

    // ── Chat ───────────────────────────────────────────────────────

    private static Node ChatPanel(
        Actions actions,
        Func<uint> sessionId,
        Func<bool> canSend,
        string title,
        string name
    )
    {
        var lobby = UiState.Lobby;
        IReadOnlyList<UiState.LobbyChatLine> Lines()
            => lobby.Chat.Value.Where(line => line.SessionId == sessionId()).ToArray();

        return Sx.View(
            name: () => name,
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 2,
                flexShrink: 1,
                // Match the games pane: a long transcript must scroll, not resize the room.
                height: 0,
                minHeight: 0,
                gapRow: 8
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        minHeight: 0,
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
                        new PanelList(() => Lines().LastOrDefault()?.Id ?? 0)
                        {
                            Name = name + " list",
                            Styles = Sx.Styles(
                                flexGrow: 1,
                                flexShrink: 1,
                                minHeight: 0,
                                flexDirection: FlexDirection.Column,
                                overflow: Overflow.Scroll,
                                gapRow: 4,
                                paddingRight: 8
                            )
                        }.AddChildren(
                            Sx.For(
                                each: Lines,
                                row: (line, _) => ChatLineRow(line)
                            )
                        )
                    ]
                ),
                Sx.Show(
                    canSend,
                    () => Sx.View(
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
                                            backgroundColor: FieldFill,
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
                                        onsubmit: _ => Send(actions)
                                    )
                                ]
                            ),
                            ActionButton(
                                "SEND",
                                () => Send(actions),
                                small: true,
                                disabled: () => !lobby.Connected.Value,
                                name: () => "Lobby chat send"
                            )
                        ]
                    ),
                    () => Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                        text: () => "Chat is available to this game's members."
                    )
                )
            ]
        );
    }

    private static Node ChatLineRow(UiState.LobbyChatLine line)
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                paddingTop: 2,
                paddingBottom: 2
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.WrappingText(
                        fontSize: 11,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: line.IsSystem ? Theme.MenuDim : line.Color
                    ),
                    layout: () => Sx.Styles(width: 100.Percent()),
                    text: () => line.Sender
                ),
                // Word-wrap: a long message ran out of the column and off the right edge.
                Sx.Text(
                    style: () => Sx.WrappingText(
                        fontSize: 12,
                        foregroundColor: line.IsSystem ? Theme.MenuDim : TextBlue
                    ),
                    layout: () => Sx.Styles(width: 100.Percent()),
                    text: () => line.Message
                )
            ]
        );

    private static void Send(Actions actions)
    {
        var message = ChatInput.Value;
        if (message.Trim().Length == 0)
            return;
        ChatInput.Value = "";
        actions.SendChat(message);
    }

    // ── Shared pieces ──────────────────────────────────────────────

    /// <summary>A scroll container that follows a transcript's newest line.</summary>
    private sealed class PanelList(Func<long> latestMessage) : View
    {
        private long _lastMessage = -1;
        private float _lastScrollableHeight;

        public PanelList AddChildren(object? children)
        {
            if (children is Node node)
                AddChild(node);
            else if (children is IEnumerable<Node> nodes)
                foreach (var child in nodes)
                    AddChild(child);
            return this;
        }

        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            var id = latestMessage();
            var grewAtBottom = ScrollableHeight > _lastScrollableHeight
                               && ScrollTop >= _lastScrollableHeight - 0.5f;
            if (id != _lastMessage || grewAtBottom)
                ScrollTop = float.MaxValue;
            _lastMessage = id;
            _lastScrollableHeight = ScrollableHeight;

            base.RenderContent(position, size);
        }
    }

    private static Node ActionButton(
        Func<string> label,
        Action onPress,
        bool primary = false,
        bool small = false,
        Func<bool>? disabled = null,
        Func<bool>? active = null,
        Func<string>? name = null
    )
        => MenuButton(label, onPress, primary, small, disabled, active, name);

    private static Node ActionButton(
        string label,
        Action onPress,
        bool primary = false,
        bool small = false,
        Func<bool>? disabled = null,
        Func<string>? name = null
    )
        => MenuButton(() => label, onPress, primary, small, disabled, null, name);

    private static Node SmallButton(string label, Action onPress, Func<string>? name = null)
        => MenuButton(() => label, onPress, false, true, null, null, name);

    /// <summary>The shared button, restyled into the menu palette.</summary>
    private static Node MenuButton(
        Func<string> label,
        Action onPress,
        bool primary,
        bool small,
        Func<bool>? disabled,
        Func<bool>? active,
        Func<string>? name
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
            ),
            name: name
        );
    }
}
