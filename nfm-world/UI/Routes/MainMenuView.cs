using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface.DriverInterface;
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
    public sealed record MenuItem(string Label, string Description, Action Action);

    public sealed record MenuPage(string Title, IReadOnlyList<MenuItem> Items);

    /// <summary>What the menu asks the game to do. The phase fills these in.</summary>
    public sealed record Actions(Action Play, Action Garage, Action ModelEditor, Action StageEditor, Action<string> Unavailable);

    /// <summary>The default actions: everything unavailable, which is what the editors see.</summary>
    public static readonly Actions NoActions = new(
        Play: static () => { },
        Garage: static () => { },
        ModelEditor: static () => { },
        StageEditor: static () => { },
        Unavailable: static _ => { });

    private static readonly Signal<IReadOnlyList<MenuPage>> PageStack = new([]);
    private static readonly Signal<bool> SettingsOpen = new(false);

    /// <summary>The page tree, with the current page's items. <paramref name="actions"/> supplies the game callbacks.</summary>
    public static MenuPage MainPage(Actions actions) => new("NFM WORLD",
    [
        new("PLAY", "Play public, private matches online or play singleplayer.", () => PushPage(BuildPlayMenu(actions))),
        new("GARAGE", "Customize and inspect your vehicles in the garage.", actions.Garage),
        new("WORKSHOP", "Build your own models and stages.", () => PushPage(BuildWorkshopMenu(actions))),
        new("SETTINGS", "Adjust game settings.", () => SettingsOpen.Value = true),
        new("CREDITS", "View game credits.", () => actions.Unavailable("credits")),
        new("QUIT", "Exit the game.", () => actions.Unavailable("quit")),
    ]);

    // ── Styles ────────────────────────────────────────────────────────

    private static readonly StyledView ItemRow =
        Styled.StyleView(new StyledStyle
        {
            Base = new Styles
            {
                FlexDirection = FlexDirection.Column,
                AlignItems = Align.FlexStart,
                PaddingTop = 14,
                PaddingBottom = 14,
                PaddingLeft = 14,
                PaddingRight = 14,
                BackgroundColor = new Color(60, 60, 60, 31),
                BorderTop = 1,
                BorderBottom = 1,
                BorderLeft = 1,
                BorderRight = 1,
                BorderColor = new Color(255, 255, 255, 26),
                BorderRadius = 8,
            },
            Hover = new Styles
            {
                BackgroundColor = Theme.AccentSoftStrong,
                BorderColor = Theme.AccentBorder,
            },
        });

    private static readonly StyledText ItemLabel =
        Styled.StyleText(new StyledStyle
        {
            Text = new TextStyles { FontSize = 16, FontStyle = FontStyle.Bold },
        });

    private static readonly StyledText ItemDesc =
        Styled.StyleText(new StyledStyle
        {
            Text = new TextStyles { FontSize = 12, ForegroundColor = new Color(255, 255, 255, 102) },
        });

    // The Luau originals carry their own spacing, so it lives with the style rather than
    // at the call site where it is easy to drop.
    private static readonly StyledText PageTitle =
        Styled.StyleText(new StyledStyle
        {
            Base = new Styles { MarginBottom = 8 },
            Text = new TextStyles { FontSize = 48, FontStyle = FontStyle.Bold, FontFamily = FontFamily.Adventure },
        });

    private static readonly StyledText Subtitle =
        Styled.StyleText(new StyledStyle
        {
            Base = new Styles { MarginBottom = 40 },
            Text = new TextStyles { FontSize = 14, ForegroundColor = Theme.TextMuted },
        });

    public static Node Build(Actions actions)
    {
        MenuPage CurrentPage()
        {
            var stack = PageStack.Value;
            return stack.Count > 0 ? stack[^1] : MainPage(actions);
        }

        return Sx.View(
            style: () => new Styles
            {
                Width = MeasurementWidthHeight.Percent(100),
                Height = MeasurementWidthHeight.Percent(100),
                FlexDirection = FlexDirection.Column,
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
            },
            children:
            [
                PageTitle(text: () => CurrentPage().Title),

                Sx.Show(
                    when: () => PageStack.Value.Count == 0 && UiState.MainMenu.IsLoggedIn.Value,
                    then: () => Subtitle(text: () => $"Welcome, {UiState.MainMenu.PlayerName.Value}")),

                Sx.View(
                    style: () => new Styles
                    {
                        FlexDirection = FlexDirection.Column,
                        GapColumn = 6,
                        GapRow = 6,
                        MinWidth = 320,
                        AlignItems = Align.Stretch,
                        AlignContent = Align.Stretch,
                    },
                    children: Sx.For(
                        each: () => PageStack.Value.Count > 0 ? PageStack.Value[^1].Items : MainPage(actions).Items,
                        row: (item, _) => ItemButton(item))),

                Sx.Show(
                    when: () => PageStack.Value.Count == 0,
                    then: () => Sx.Text(
                        style: () => new TextStyles { FontSize = 11, ForegroundColor = Theme.TextFaint },
                        text: () => "NFM World",
                        layout: () => new Styles { Position = Position.Absolute, Bottom = 20 })),

                Sx.Show(
                    when: () => SettingsOpen.Value,
                    then: () => SettingsView.Build(() => SettingsOpen.Value = false)),
            ]);
    }

    private static View ItemButton(MenuItem item)
    {
        return ItemRow(
            onmousedown: _ => item.Action(),
            children:
            [
                ItemLabel(text: () => item.Label),
                ItemDesc(text: () => item.Description)
            ]);
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
        new("TIME TRIALS", "Flex your fastest time against other people.", () => actions.Unavailable("timeTrials")),
        new("CHALLENGES", "Complete challenges to sharpen your mechanical skills.", () => actions.Unavailable("challenges")),
        new("GAME INSTRUCTIONS", "Read about the rules and controls of the game.", () => actions.Unavailable("gameInstructions")),
        new("BACK", "Return to the previous menu.", PopPage),
    ]);

    private static void PushPage(MenuPage page)
        => PageStack.Value = [.. PageStack.Value, page];

    private static void PopPage()
        => PageStack.Value = PageStack.Value.Count > 0
            ? PageStack.Value.Cast<MenuPage>().Take(PageStack.Value.Count - 1).ToArray()
            : PageStack.Value;

    /// <summary>Clears the page stack, for when the menu is re-entered.</summary>
    public static void Reset()
    {
        PageStack.Value = [];
        SettingsOpen.Value = false;
    }
}
