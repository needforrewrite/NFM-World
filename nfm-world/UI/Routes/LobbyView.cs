using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Multiplayer;
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
        Action StartRace,
        Action Create,
        Action Refresh,
        Action<string> SendChat,
        Action Back,
        Action OpenCreate,
        Action CloseCreate,
        Action<uint> Select,
        Action<string> SetStage,
        Action<int> SetMaxPlayers);

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
            style: () => Sx.Styles(position: Position.Absolute, left: 0, top: 0, right: 0, bottom: 0,
                backgroundColor: new Color(3, 8, 20, 170),
                alignItems: Align.Center, justifyContent: Justify.Center),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(width: MeasurementWidthHeight.Percent(92),
                        maxWidth: 1100, height: MeasurementWidthHeight.Percent(90),
                        flexDirection: FlexDirection.Column,
                        backgroundColor: PanelColor,
                        borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                        borderColor: Border, borderRadius: 12, paddingTop: 18, paddingBottom: 14,
                        paddingLeft: 22, paddingRight: 22, gapRow: 12),
                    children:
                    [
                        Header(actions),
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexGrow: 1, gapColumn: 14,
                                alignItems: Align.Stretch),
                            children:
                            [
                                PlayersPanel(),
                                GamesPanel(actions),
                                ChatPanel(actions)
                            ])
                    ]),
                Sx.Show(() => lobby.ShowCreateDialog.Value, () => CreateDialog(actions))
            ]);
    }

    private static Node Header(Actions actions)
    {
        var lobby = UiState.Lobby;
        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.FlexEnd, gapColumn: 12,
                flexShrink: 0),
            children:
            [
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 30, fontFamily: FontFamily.Adventure,
                                fontStyle: FontStyle.Bold, foregroundColor: Blue),
                            text: () => "MULTIPLAYER"),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 12,
                                foregroundColor: lobby.Connected.Value ? TextBlue : Theme.Danger),
                            text: () => lobby.Status.Value.Length > 0 ? lobby.Status.Value : "Connecting...")
                    ]),
                SmallButton("REFRESH", actions.Refresh),
                SmallButton("BACK", actions.Back)
            ]);
    }

    private static Node PlayersPanel()
    {
        var lobby = UiState.Lobby;
        return Panel("PLAYERS", 240, () => Sx.For(
            each: () => lobby.Players.Value,
            row: (player, _) => Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Column, paddingTop: 4, paddingBottom: 4,
                    borderBottom: 1, borderColor: new Color(128, 170, 255, 40)),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 14, fontStyle: FontStyle.Bold,
                        foregroundColor: player.IsSelf ? Blue : TextBlue), text: () => player.Name),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                        text: () => player.Car)
                ])));
    }

    private static Node GamesPanel(Actions actions)
    {
        var lobby = UiState.Lobby;
        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, gapRow: 10),
            children:
            [
                Panel("GAMES", null, () => Sx.Show(
                    when: () => lobby.Sessions.Value.Count == 0,
                    then: () => Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: Theme.MenuDim),
                        text: () => "No games yet. Create one to get started."),
                    otherwise: () => Sx.For(
                        each: () => lobby.Sessions.Value,
                        row: (session, _) => SessionRow(session, actions)))),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10, flexShrink: 0),
                    children:
                    [
                        ActionButton("CREATE GAME", actions.OpenCreate, primary: true),
                        Sx.Show(() => lobby.CurrentSession.Value is not null,
                            () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10),
                                children:
                                [
                                    ActionButton(() => "START RACE", actions.StartRace, primary: true),
                                    ActionButton("LEAVE", actions.Leave)
                                ]))
                    ])
            ]);
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
                    : new Color(22, 42, 80, 110)),
            children:
            [
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 14, fontStyle: FontStyle.Bold,
                            foregroundColor: TextBlue), text: () => session.StageName),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => $"{session.CreatorName} · {session.PlayerCount}/{session.MaxPlayers}")
                    ]),
                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: StateColor(session.State)),
                    text: () => StateLabel(session.State)),

                // A member who did not create the race leaves it; only the creator starts it.
                // Both used to call StartRace, so LEAVE launched a race and nothing left.
                session.IsMember && session.IsCreator
                    ? ActionButton(() => "START", actions.StartRace, small: true,
                        disabled: session.State != SessionState.NotStarted)
                    : session.IsMember
                        ? ActionButton(() => "LEAVE", actions.Leave, small: true)
                        : ActionButton(() => "JOIN", () => actions.Join(session.Id), small: true,
                            disabled: session.State != SessionState.NotStarted)
            ]);
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
        return Sx.View(
            style: () => Sx.Styles(width: 300, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 8),
            children:
            [
                Panel("CHAT", null, () => Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4,
                        overflow: Overflow.Scroll),
                    children: Sx.For(
                        each: () => lobby.Chat.Value,
                        row: (line, _) => Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Column),
                            children:
                            [
                                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold,
                                    foregroundColor: line.IsSystem ? Theme.MenuDim : line.Color),
                                    text: () => line.Sender),
                                Sx.Text(style: () => Sx.TextStyles(fontSize: 12,
                                    foregroundColor: line.IsSystem ? Theme.MenuDim : TextBlue),
                                    text: () => line.Message)
                            ])))),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6, flexShrink: 0),
                    children:
                    [
                        Sx.View(style: () => Sx.Styles(flexGrow: 1), children:
                        [
                            Sx.TextInput(
                                style: () => Sx.Styles(width: MeasurementWidthHeight.Percent(100),
                                    paddingTop: 6, paddingBottom: 6, paddingLeft: 8, paddingRight: 8,
                                    backgroundColor: new Color(255, 255, 255, 16),
                                    borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                                    borderColor: Border, borderRadius: 6),
                                text: () => ChatInput.Value,
                                onchange: value => ChatInput.Value = value,
                                placeholder: () => "Message...",
                                // Enter submits, so the field behaves like a chat box.
                                onsubmit: _ => Send(actions))
                        ]),
                        ActionButton("SEND", () => Send(actions), small: true)
                    ])
            ]);
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

        return Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, left: 0, top: 0, right: 0, bottom: 0,
                backgroundColor: new Color(3, 8, 20, 200),
                alignItems: Align.Center, justifyContent: Justify.Center),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(width: 460, flexDirection: FlexDirection.Column, gapRow: 10,
                        backgroundColor: PanelColor, borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                        borderColor: Border, borderRadius: 10, paddingTop: 18, paddingBottom: 16,
                        paddingLeft: 20, paddingRight: 20),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 20, fontFamily: FontFamily.Adventure,
                            fontStyle: FontStyle.Bold, foregroundColor: Blue), text: () => "CREATE GAME"),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => "Stage"),
                        Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4,
                                maxHeight: 220, overflow: Overflow.Scroll),
                            children: Sx.For(
                                each: () => stages,
                                row: (stage, _) => StageOption(stage, actions))),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                            text: () => $"Max players: {lobby.MaxPlayers.Value}"),
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6),
                            children: MaxPlayerButtons(actions)),
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10, marginTop: 6),
                            children:
                            [
                                Sx.View(style: () => Sx.Styles(flexGrow: 1), children:
                                    [ActionButton("CREATE", actions.Create, primary: true)]),
                                Sx.View(style: () => Sx.Styles(flexGrow: 1), children:
                                    [ActionButton("CANCEL", actions.CloseCreate)])
                            ])
                    ])
            ]);
    }

    private static List<Node> MaxPlayerButtons(Actions actions)
    {
        var lobby = UiState.Lobby;
        var buttons = new List<Node>();
        for (var count = 2; count <= 8; count++)
        {
            // Captured per iteration: the lambdas must not all see the last count.
            var value = count;
            buttons.Add(ActionButton(() => value.ToString(), () => actions.SetMaxPlayers(value), small: true,
                active: () => lobby.MaxPlayers.Value == value));
        }
        return buttons;
    }

    private static Node StageOption(string stage, Actions actions)
    {
        var lobby = UiState.Lobby;
        return Row(
            onmousedown: _ => actions.SetStage(stage),
            style: () => Sx.Styles(
                backgroundColor: lobby.SelectedStage.Value == stage
                    ? Theme.MenuHighlight
                    : new Color(22, 42, 80, 100)),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 13,
                    foregroundColor: lobby.SelectedStage.Value == stage ? Blue : TextBlue),
                    text: () => stage)
            ]);
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
                flexDirection: FlexDirection.Column, flexGrow: width is null ? 1 : 0,
                width: width is { } w ? w : null,
                gapRow: 8, paddingTop: 10, paddingBottom: 10, paddingLeft: 12, paddingRight: 12,
                backgroundColor: new Color(22, 42, 80, 90),
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                borderColor: new Color(128, 170, 255, 70), borderRadius: 8),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold,
                    foregroundColor: Blue), text: () => title),
                content()
            ]);
    }

    /// <summary>A clickable row with the menu's hover treatment.</summary>
    private static View Row(Action<NFMWorld.ClayDom.Events.MouseEvent> onmousedown, Func<Styles> style,
        params object?[] children)
    {
        var hovered = new Signal<bool>(false);
        return Sx.View(
            style: () => style().Over(Sx.Styles(
                paddingTop: 6, paddingBottom: 6, paddingLeft: 8, paddingRight: 8,
                gapColumn: 10, alignItems: Align.Center,
                flexDirection: FlexDirection.Row,
                borderRadius: 6,
                backgroundColor: hovered.Value ? Theme.MenuHighlight : null)),
            onmousedown: onmousedown,
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children: children);
    }

    private static Node ActionButton(Func<string> label, Action onPress, bool primary = false, bool small = false,
        bool disabled = false, Func<bool>? active = null)
        => MenuButton(label, onPress, primary, small, disabled, active);

    private static Node ActionButton(string label, Action onPress, bool primary = false, bool small = false)
        => MenuButton(() => label, onPress, primary, small, false, null);

    private static Node SmallButton(string label, Action onPress)
        => MenuButton(() => label, onPress, false, true, false, null);

    /// <summary>The shared button, restyled into the menu palette.</summary>
    private static Node MenuButton(Func<string> label, Action onPress, bool primary, bool small, bool disabled,
        Func<bool>? active)
    {
        return Button.Create(
            label,
            onPress,
            variant: primary ? ButtonVariant.Primary : ButtonVariant.Default,
            active: active,
            size: small ? ButtonSize.Sm : ButtonSize.Md,
            disabled: disabled,
            // Only the border is overridden: the variant still owns the fill, so the button
            // keeps its hover and active feedback.
            style: () => Sx.Styles(
                borderColor: primary ? Theme.AccentBorder : new Color(128, 170, 255, 170)),
            textStyle: () => Sx.TextStyles(fontSize: small ? 11 : 13, fontStyle: FontStyle.Bold,
                foregroundColor: Blue));
    }
}
