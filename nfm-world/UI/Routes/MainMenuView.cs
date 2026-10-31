using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>
/// The main menu. Port of <c>data/uis/routes/mainmenu.luau</c>.
/// </summary>
/// <remarks>
/// Pages are a stack of plain records; pushing or popping replaces the stack signal,
/// which re-renders the title and the keyed item list. Items are keyed by object
/// identity, so a page change rebuilds only what differs.
/// </remarks>
public static class MainMenuView
{
    private const int ItemHeight = 36;
    private const int ItemGap = 11;
    private const int MenuRows = 6;
    public sealed class MenuItem(string label, string description, Action action)
    {
        public string Label { get; } = label;
        public string Description { get; } = description;
        public Action Action { get; } = action;
    }

    public sealed record MenuPage(string Title, IReadOnlyList<MenuItem> Items);

    /// <summary>What the menu asks the game to do. The phase fills these in.</summary>
    public sealed record Actions(
        Action Play,
        Action Garage,
        Action ModelEditor,
        Action StageEditor,
        Action TimeTrials,
        Action<string> Unavailable);

    private static readonly Signal<IReadOnlyList<MenuPage>> PageStack = new([]);
    private static readonly Signal<bool> SettingsOpen = new(false);
    private static readonly Signal<bool> QuitOpen = new(false);
    private static readonly Signal<int> QuitChoice = new(0);
    private static readonly Signal<string> Description = new("Choose your next drive.");
    private static readonly Color MenuBlue = new(128, 170, 255);
    private static readonly Signal<int> SelectedIndex = new(0);
    private static Func<MenuPage>? _currentPage;

    /// <summary>The page tree, with the current page's items. <paramref name="actions"/> supplies the game callbacks.</summary>
    public static MenuPage MainPage(Actions actions) => new("NFM WORLD",
    [
        new("PLAY", "Play public, private matches online or play singleplayer.", () => PushPage(BuildPlayMenu(actions))),
        new("GARAGE", "Customize and inspect your vehicles in the garage.", actions.Garage),
        new("WORKSHOP", "Build your own models and stages.", () => PushPage(BuildWorkshopMenu(actions))),
        new("SETTINGS", "Adjust game settings.", () => SettingsOpen.Value = true),
        new("CREDITS", "View game credits.", () => actions.Unavailable("credits")),
        new("QUIT", "Exit the game.", () =>
        {
            QuitChoice.Value = 0;
            QuitOpen.Value = true;
        }),
    ]);

    // ── Styles ────────────────────────────────────────────────────────

    private static readonly StyledView ItemRow =
        Styled.StyleView(
            baseStyle: Sx.Styles(
                flexDirection: FlexDirection.Column,
                alignItems: Align.FlexStart,
                paddingTop: 3,
                paddingBottom: 3,
                paddingLeft: 12,
                paddingRight: 12,
                backgroundColor: new Color(22, 42, 80, 145),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 195),
                borderRadius: 6
            ),
            hoverStyle: Sx.Styles(
                backgroundColor: new Color(55, 90, 150, 190),
                borderColor: new Color(205, 227, 255)
            )
        );

    private static readonly StyledText ItemLabel =
        Styled.StyleText(
            textStyle: Sx.TextStyles(fontSize: 22, fontFamily: FontFamily.Adventure,
                fontStyle: FontStyle.Bold, foregroundColor: MenuBlue)
        );

    public static Node Build(Actions actions, Node logo)
    {
        // Built once, not per read: the items hold lambdas that capture `actions`, so a
        // freshly built page never compares equal to the mounted one. `For` keys by value,
        // so those items would be torn down and re-appended on every read - which is what
        // reordered the menu (the stable-delegate items kept their slots) and gave the
        // recreated rows fresh hover signals.
        var root = MainPage(actions);
        Description.Value = root.Items[0].Description;

        MenuPage CurrentPage()
        {
            var stack = PageStack.Value;
            return stack.Count > 0 ? stack[^1] : root;
        }

        _currentPage = CurrentPage;

        return Sx.View(
            style: () => Sx.Styles(
                visibility: SettingsOpen.Value ? Visibility.Hidden : Visibility.Visible,
                pointerEvents: !SettingsOpen.Value && !QuitOpen.Value,
                position: Position.Absolute,
                left: "7%",
                bottom: "8%",
                width: "36%",
                maxWidth: 460,
                flexDirection: FlexDirection.Column,
                alignItems: Align.Stretch,
                justifyContent: Justify.FlexEnd
            ),
            children:
            [
                Sx.View(style: () => Sx.Styles(position: Position.Absolute, bottom: "100%",
                        width: "100%", maxWidth: 370,
                        marginBottom: 60, pointerEvents: false),
                    children: [logo]),
                Sx.Show(() => PageStack.Value.Count > 0,
                    () => Sx.Text(style: () => Sx.TextStyles(fontSize: 28, fontFamily: FontFamily.Adventure,
                            fontStyle: FontStyle.Bold,
                            foregroundColor: MenuBlue),
                        layout: () => Sx.Styles(position: Position.Absolute, bottom: "100%",
                            height: 40, marginBottom: 8),
                        text: () => CurrentPage().Title)),

                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        height: ItemHeight * MenuRows + ItemGap * (MenuRows - 1),
                        flexShrink: 0,
                        width: "68%",
                        minWidth: 230,
                        maxWidth: 330,
                        gapRow: ItemGap,
                        alignItems: Align.Stretch,
                        alignContent: Align.Stretch
                    ),
                    children: Sx.For(
                        each: () => CurrentPage().Items,
                        row: (item, index) => ItemButton(item, index))),

                Sx.View(style: () => Sx.Styles(width: "78%", minWidth: 250, maxWidth: 370,
                        height: 29, flexShrink: 0,
                        marginTop: 13, paddingLeft: 12, paddingRight: 12, paddingTop: 6, paddingBottom: 6,
                        backgroundColor: new Color(3, 8, 20, 135), borderRadius: 14),
                    children: [Sx.Text(style: () => Sx.TextStyles(fontSize: 12,
                        foregroundColor: new Color(185, 211, 255)), text: () => Description.Value)]),
                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: MenuBlue),
                    layout: () => Sx.Styles(marginTop: 8),
                    text: () => UiState.MainMenu.StageName.Value)
            ]);
    }

    private static View ItemButton(MenuItem item, int index)
    {
        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, alignItems: Align.Stretch,
                height: ItemHeight, flexShrink: 0),
            onmouseenter: _ =>
            {
                SelectedIndex.Value = index;
                Description.Value = item.Description;
            },
            children: [ItemRow(style: () => SelectedIndex.Value == index
                    ? Sx.Styles(height: "100%", backgroundColor: new Color(55, 90, 150, 190),
                        borderColor: new Color(205, 227, 255))
                    : Sx.Styles(height: "100%"), onmousedown: _ => item.Action(),
                children: [ItemLabel(text: () => item.Label)])]);
    }

    private static MenuPage BuildPlayMenu(Actions actions) => new("PLAY",
    [
        new("SINGLEPLAYER", "Play the original single player experiences.", () => PushPage(BuildSpMenu(actions))),
        new("MULTIPLAYER", "Play online with other players.", () => PushPage(BuildMpMenu(actions))),
        new("TRAINING", "Train your skills and learn the game mechanics.", () => PushPage(BuildTrainingMenu(actions))),
        new("BACK", "Return to the previous menu.", PopPage),
    ]);

    private static MenuPage BuildSpMenu(Actions actions) => new("SINGLEPLAYER",
    [
        new("NFM1", "Play the original NFM1 singleplayer campaign.", () => actions.Unavailable("playNfm1")),
        new("NFM2", "Play the original NFM2 singleplayer campaign.", () => actions.Unavailable("playNfm2")),
        new("COMMUNITY", "Play custom experiences crafted by the community.", () => actions.Unavailable("playCommunity")),
        new("FREE PLAY", "Play freely without any restrictions.", actions.Play),
        new("BACK", "Return to the previous menu.", PopPage),
    ]);

    private static MenuPage BuildMpMenu(Actions actions) => new("MULTIPLAYER",
    [
        new("COMPETITIVE", "Compete against other players via matchmaking.", () => actions.Unavailable("multiplayer")),
        new("CASUAL", "Play with people in a free relaxed environment.", () => actions.Unavailable("casual")),
        new("BACK", "Return to the previous menu.", PopPage),
    ]);

    private static MenuPage BuildWorkshopMenu(Actions actions) => new("WORKSHOP",
    [
        new("MODEL EDITOR", "View and edit custom models.", actions.ModelEditor),
        new("STAGE EDITOR", "Design your own stages.", actions.StageEditor),
        new("CAMPAIGN EDITOR", "Craft custom experiences.", () => actions.Unavailable("campaignEditor")),
        new("BACK", "Return to the previous menu.", PopPage),
    ]);

    private static MenuPage BuildTrainingMenu(Actions actions) => new("TRAINING",
    [
        new("TIME TRIALS", "Flex your fastest time against other people.", actions.TimeTrials),
        new("CHALLENGES", "Complete challenges to sharpen your mechanical skills.", () => actions.Unavailable("challenges")),
        new("GAME INSTRUCTIONS", "Read about the rules and controls of the game.", () => actions.Unavailable("gameInstructions")),
        new("BACK", "Return to the previous menu.", PopPage),
    ]);

    private static void PushPage(MenuPage page)
    {
        PageStack.Value = [.. PageStack.Value, page];
        SelectFirst();
    }

    private static void PopPage()
    {
        PageStack.Value = PageStack.Value.Count > 0
            ? [.. PageStack.Value.Take(PageStack.Value.Count - 1)]
            : PageStack.Value;
        SelectFirst();
    }

    private static void SelectFirst()
    {
        SelectedIndex.Value = 0;
        Description.Value = _currentPage?.Invoke().Items.FirstOrDefault()?.Description ?? "Choose your next drive.";
    }

    public static void KeyPressed(Key key)
    {
        if (QuitOpen.Value)
        {
            if (key is Key.Left or Key.Right or Key.Up or Key.Down)
                QuitChoice.Value = 1 - QuitChoice.Value;
            else if (key == Key.Enter)
            {
                if (QuitChoice.Value == 1)
                    GameSparker.Game.Window.RequestClose();
                else
                    QuitOpen.Value = false;
            }
            return;
        }
        if (SettingsOpen.Value || _currentPage is null) return;
        var items = _currentPage().Items;
        if (key is Key.Up or Key.Down)
        {
            SelectedIndex.Value = (SelectedIndex.Value + (key == Key.Up ? -1 : 1) + items.Count) % items.Count;
            Description.Value = items[SelectedIndex.Value].Description;
        }
        else if (key == Key.Enter)
            items[Math.Clamp(SelectedIndex.Value, 0, items.Count - 1)].Action();
        else if (key == Key.Escape)
            NavigateBack();
    }

    public static bool NavigateBack()
    {
        if (QuitOpen.Value)
        {
            QuitOpen.Value = false;
            return true;
        }
        if (SettingsOpen.Value)
        {
            SettingsOpen.Value = false;
            return true;
        }
        if (PageStack.Value.Count == 0) return false;
        PopPage();
        return true;
    }

    /// <summary>Clears the page stack, for when the menu is re-entered.</summary>
    public static void Reset()
    {
        PageStack.Value = [];
        SettingsOpen.Value = false;
        QuitOpen.Value = false;
        SelectFirst();
    }

    public static Node SettingsOverlay() => Sx.Show(
        when: () => SettingsOpen.Value,
        then: () => SettingsView.Build(() => SettingsOpen.Value = false));

    public static Node QuitOverlay() => Sx.Show(() => QuitOpen.Value, () => Sx.View(
        style: () => Sx.Styles(position: Position.Absolute, top: 0, left: 0, right: 0, bottom: 0,
            alignItems: Align.Center, justifyContent: Justify.Center,
            backgroundColor: new Color(3, 8, 20, 180), zIndex: 30),
        children: Sx.View(
            style: () => Sx.Styles(width: "90%", maxWidth: 420, flexDirection: FlexDirection.Column,
                paddingTop: 28, paddingBottom: 28, paddingLeft: 28, paddingRight: 28,
                backgroundColor: new Color(12, 22, 42, 250),
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                borderColor: MenuBlue, borderRadius: 10, gapRow: 18),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 26, fontFamily: FontFamily.Adventure,
                    foregroundColor: MenuBlue), text: () => "QUIT NFM WORLD?"),
                Sx.Text(style: () => Sx.TextStyles(fontSize: 14, foregroundColor: new Color(185, 211, 255)),
                    text: () => "Are you sure you want to quit the game?"),
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row,
                        justifyContent: Justify.FlexEnd, gapColumn: 12),
                    children:
                    [
                        QuitButton("Cancel", 0, () => QuitOpen.Value = false),
                        QuitButton("Quit", 1, () => GameSparker.Game.Window.RequestClose())
                    ])
            ])));

    private static Node QuitButton(string label, int index, Action action) => Sx.View(
        onmouseenter: _ => QuitChoice.Value = index,
        children: Button.Create(() => label, action,
            style: () => Sx.Styles(minWidth: 110,
                backgroundColor: QuitChoice.Value == index ? new Color(55, 90, 150, 190) : new Color(22, 42, 80),
                borderColor: QuitChoice.Value == index ? new Color(205, 227, 255) : MenuBlue)));
}
