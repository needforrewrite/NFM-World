using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

public static class PauseMenuView
{
    private static readonly Color Blue = new(128, 170, 255);
    private static readonly Color TextBlue = new(185, 211, 255);
    private static readonly Color Panel = new(12, 22, 42, 245);
    private static readonly Color Border = new(128, 170, 255, 140);

    private enum Page
    {
        Menu,
        Settings,
        ConfirmQuit,
        ConfirmRestart
    }

    private sealed class InputView(Func<Key, bool> handleKey) : View
    {
        public bool HandleKey(Key key) => handleKey(key);
    }

    public static bool HandleKeyPressed(Node? node, Key key)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is InputView menu) return menu.HandleKey(key);
        foreach (var child in node.VisualChildren)
            if (HandleKeyPressed(child, key))
                return true;
        return false;
    }

    public static Node Build(RaceHudView.Actions actions)
    {
        var page = new Signal<Page>(Page.Menu);
        var selected = new Signal<int>(0);
        var race = UiState.Race;

        // RESTART only exists when this client owns the race. Online the Game Master does, and
        // resetting the local gamemode would just desync it.
        var items = new List<(string Label, string Description, Action Activate)>
        {
            ("RESUME", "Get back to the action.", actions.Resume)
        };
        if (actions.Restart is not null)
            items.Add(("RESTART", "Start a fresh run on this stage.", () => Open(Page.ConfirmRestart)));
        items.Add(("SETTINGS", "Adjust controls, display, audio and gameplay.", () => Open(Page.Settings)));
        items.Add(("LEAVE GAME", "Leave this game and return to the main menu.", () => Open(Page.ConfirmQuit)));

        void Open(Page next)
        {
            selected.Value = 0;
            page.Value = next;
        }

        void Activate()
        {
            if (page.Value == Page.Menu)
                items[selected.Value].Activate();
            else if (selected.Value == 0)
                Open(Page.Menu);
            else if (page.Value == Page.ConfirmRestart)
                actions.Restart?.Invoke();
            else if (page.Value == Page.ConfirmQuit)
                actions.Quit();
        }

        bool HandleKey(Key key)
        {
            if (page.Value == Page.Settings) return true;
            var count = page.Value == Page.Menu ? items.Count : 2;
            if (key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                var direction = key is Key.Up or Key.Left ? -1 : 1;
                selected.Value = (selected.Value + direction + count) % count;
            }
            else if (key is Key.Enter or Key.Space)
                Activate();
            else if (key == Key.Escape)
            {
                if (page.Value == Page.Menu) actions.Resume();
                else Open(Page.Menu);
            }

            return true;
        }

        Node Menu() => Overlay(
            Card(
                [
                    Heading("PAUSED"),
                    RaceInfo(race),
                    .. items.Select((item, index) => MenuButton(item.Label, index, selected, Activate)),
                    Sx.View(
                        style: () => Sx.Styles(
                            marginTop: 8,
                            paddingTop: 8,
                            paddingBottom: 8,
                            paddingLeft: 12,
                            paddingRight: 12,
                            backgroundColor: new Color(3, 8, 20, 170),
                            borderRadius: 6
                        ),
                        children: Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 12,
                                foregroundColor: TextBlue,
                                horizontalAlignment: TextHorizontalAlignment.Center
                            ),
                            text: () => items[Math.Clamp(selected.Value, 0, items.Count - 1)].Description
                        )
                    )
                ]
            )
        );

        Node Confirmation(bool restart) => Overlay(
            Card(
                [
                    Heading(restart ? "RESTART RUN?" : "LEAVE GAME?"),
                    Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 14,
                            foregroundColor: TextBlue,
                            horizontalAlignment: TextHorizontalAlignment.Center
                        ),
                        layout: () => Sx.Styles(marginBottom: 22),
                        text: () => restart ? "Your current run will be reset." : "Your current game progress will be lost."
                    ),
                    MenuButton("CANCEL", 0, selected, Activate),
                    MenuButton(restart ? "RESTART" : "LEAVE GAME", 1, selected, Activate)
                ]
            )
        );

        var root = new InputView(HandleKey)
        {
            Styles = Sx.Styles(
                position: Position.Absolute,
                top: 0,
                bottom: 0,
                left: 0,
                right: 0,
                zIndex: 10
            )
        };
        root.AddChild(
            Sx.Switch(
                page,
                new Dictionary<Page, Func<Node>>
                {
                    [Page.Menu] = Menu,
                    [Page.Settings] = () => SettingsView.Build(() => Open(Page.Menu)),
                    [Page.ConfirmQuit] = () => Confirmation(false),
                    [Page.ConfirmRestart] = () => Confirmation(true)
                }
            )
        );
        return root;
    }

    private static View Overlay(Node content) => Sx.View(
        style: () => Sx.Styles(
            position: Position.Absolute,
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            alignItems: Align.Center,
            justifyContent: Justify.Center,
            backgroundColor: new Color(3, 8, 20, 145)
        ),
        children: content
    );

    private static View Card(object?[] children) => Sx.View(
        style: () => Sx.Styles(
            width: "90%",
            maxWidth: 430,
            paddingTop: 20,
            paddingBottom: 20,
            paddingLeft: 28,
            paddingRight: 28,
            flexDirection: FlexDirection.Column,
            alignItems: Align.Stretch,
            gapRow: 8,
            backgroundColor: Panel,
            borderTop: 1,
            borderBottom: 1,
            borderLeft: 1,
            borderRight: 1,
            borderColor: Border,
            borderRadius: 10
        ),
        onmousedown: e => e.StopPropagation(),
        children: children
    );

    private static Text Heading(string text) => Sx.Text(
        style: () => Sx.TextStyles(
            fontSize: 32,
            fontFamily: FontFamily.Adventure,
            fontStyle: FontStyle.Bold,
            foregroundColor: Blue,
            horizontalAlignment: TextHorizontalAlignment.Center
        ),
        text: () => text
    );

    private static View RaceInfo(UiState.RaceState race) => Sx.View(
        style: () => Sx.Styles(
            flexDirection: FlexDirection.Row,
            alignItems: Align.Center,
            justifyContent: Justify.Center,
            gapColumn: 20,
            paddingTop: 8,
            paddingBottom: 8,
            marginBottom: 4,
            borderTop: 1,
            borderBottom: 1,
            borderLeft: 0,
            borderRight: 0,
            borderColor: Border
        ),
        children:
        [
            InfoItem("LAP", () => $"{race.Lap.Value} / {race.TotalLaps.Value}"),
            InfoItem("POSITION", () => $"{race.Position.Value} / {race.TotalRacers.Value}")
        ]
    );

    private static View InfoItem(string label, Func<string> value) => Sx.View(
        style: () => Sx.Styles(
            flexDirection: FlexDirection.Column,
            alignItems: Align.Stretch,
            flexGrow: 1,
            width: 0,
            minWidth: 0,
            gapRow: 3
        ),
        children:
        [
            Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 11,
                    foregroundColor: TextBlue,
                    horizontalAlignment: TextHorizontalAlignment.Center
                ),
                text: () => label
            ),
            Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 20,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: Blue,
                    horizontalAlignment: TextHorizontalAlignment.Center
                ),
                text: value
            )
        ]
    );

    private static View MenuButton(string label, int index, Signal<int> selected, Action activate) => Sx.View(
        style: () => Sx.Styles(
            height: 38,
            flexShrink: 0,
            flexDirection: FlexDirection.Row,
            alignItems: Align.Center,
            justifyContent: Justify.Center,
            paddingLeft: 14,
            paddingRight: 14,
            backgroundColor: selected.Value == index ? new Color(55, 90, 150, 220) : new Color(22, 42, 80, 170),
            borderColor: selected.Value == index ? new Color(205, 227, 255) : Border,
            borderTop: 1,
            borderBottom: 1,
            borderLeft: 1,
            borderRight: 1,
            borderRadius: 6
        ),
        onmouseenter: _ => selected.Value = index,
        onmousedown: e =>
        {
            e.StopPropagation();
            if (e.Button != MouseButton.Primary) return;
            selected.Value = index;
            activate();
        },
        children: Sx.Text(
            style: () => Sx.TextStyles(
                fontSize: 23,
                fontFamily: FontFamily.Adventure,
                fontStyle: FontStyle.Bold,
                foregroundColor: Blue,
                horizontalAlignment: TextHorizontalAlignment.Center
            ),
            text: () => label
        )
    );
}
