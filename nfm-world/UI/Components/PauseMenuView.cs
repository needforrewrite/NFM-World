using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

/// <summary>
/// Pause overlay with confirm dialogs. Port of
/// <c>data/uis/components/pausemenu.luau</c>.
/// </summary>
public static class PauseMenuView
{
    private enum Page
    {
        Menu,
        Settings,
        ConfirmQuit,
        ConfirmRestart
    }

    public static Node Build(RaceHudView.Actions actions)
    {
        var page = new Signal<Page>(Page.Menu);
        var race = UiState.Race;

        return Sx.Switch(
            page,
            new Dictionary<Page, Func<Node>>
            {
                [Page.Menu] = () => MenuPage(page, race, actions),
                [Page.Settings] = () => SettingsView.Build(() => page.Value = Page.Menu),
                [Page.ConfirmQuit] = () => Confirm(
                    "Quit to Main Menu?",
                    "All race progress will be lost.",
                    "Quit",
                    () =>
                    {
                        race.Paused.Value = false;
                        actions.Quit();
                    },
                    () => page.Value = Page.Menu),
                [Page.ConfirmRestart] = () => Confirm(
                    "Restart Race?",
                    "Your current lap time and position will be reset.",
                    "Restart",
                    () =>
                    {
                        race.Paused.Value = false;
                        actions.Restart();
                    },
                    () => page.Value = Page.Menu)
            });
    }

    private static Node MenuPage(Signal<Page> page, UiState.RaceState race, RaceHudView.Actions actions) => Overlay(
        Sx.View(
            style: () => new Styles
            {
                Width = 380,
                PaddingTop = 36,
                PaddingBottom = 36,
                PaddingLeft = 40,
                PaddingRight = 40,
                BackgroundColor = new Color(20, 20, 40, 217),
                BorderTop = 1,
                BorderBottom = 1,
                BorderLeft = 1,
                BorderRight = 1,
                BorderColor = new Color(255, 255, 255, 26),
                BorderRadius = 16,
                FlexDirection = FlexDirection.Column,
                AlignItems = Align.Center,
                GapColumn = 6,
                GapRow = 6,
            },
            children:
            [
                Sx.Text(
                    style: () => new TextStyles { FontSize = 22, FontStyle = FontStyle.Bold },
                    layout: () => new Styles { MarginBottom = 4 },
                    text: () => "Paused"),
                Sx.Text(
                    style: () => new TextStyles { FontSize = 12, ForegroundColor = Theme.TextFaint },
                    layout: () => new Styles { MarginBottom = 20 },
                    text: () => "Race"),
                RaceInfo(race),
                MenuButton("Resume", ButtonVariant.Primary, actions.Resume),
                MenuButton("Restart", ButtonVariant.Default, () => page.Value = Page.ConfirmRestart),
                MenuButton("Settings", ButtonVariant.Default, () => page.Value = Page.Settings),
                MenuButton("Quit to Main Menu", ButtonVariant.Danger, () => page.Value = Page.ConfirmQuit)
            ]));

    private static Node RaceInfo(UiState.RaceState race) => Sx.View(
        style: () => new Styles
        {
            Width = MeasurementWidthHeight.Percent(100),
            FlexDirection = FlexDirection.Row,
            JustifyContent = Justify.Center,
            GapColumn = 32,
            GapRow = 32,
            MarginBottom = 20,
            PaddingTop = 10,
            PaddingBottom = 10,
            BorderTop = 1,
            BorderBottom = 1,
            BorderColor = new Color(255, 255, 255, 15),
        },
        children:
        [
            InfoItem("Lap", () => $"{race.Lap.Value}/{race.TotalLaps.Value}"),
            InfoItem("Position", () => $"{race.Position.Value}/{race.TotalRacers.Value}")
        ]);

    private static View InfoItem(string label, Func<string> value) => Sx.View(
        style: () => new Styles { FlexDirection = FlexDirection.Column, AlignItems = Align.Center },
        children:
        [
            Sx.Text(style: () => new TextStyles { FontSize = 10, ForegroundColor = Theme.TextFaint }, text: () => label),
            Sx.Text(style: () => new TextStyles { FontSize = 18, FontStyle = FontStyle.Bold }, text: value)
        ]);

    private static View Overlay(Node content) => Sx.View(
        style: () => new Styles
        {
            Position = Position.Absolute,
            Top = 0,
            Left = 0,
            Right = 0,
            Bottom = 0,
            AlignItems = Align.Center,
            JustifyContent = Justify.Center,
            BackgroundColor = Theme.Overlay,
            ZIndex = 10,
        },
        children: content);

    private static View MenuButton(string label, ButtonVariant variant, Action onPress) => Button.Create(
        label: () => label,
        onPress: onPress,
        variant: variant,
        size: ButtonSize.Lg,
        fullWidth: true,
        style: () => new Styles { BorderRadius = 8 },
        textStyle: () => new TextStyles { FontSize = 15 });

    private static Node Confirm(string title, string message, string confirmLabel, Action onConfirm, Action onCancel)
    {
        var box = Sx.View(
            style: () => new Styles
            {
                BackgroundColor = Theme.Panel,
                BorderTop = 1,
                BorderBottom = 1,
                BorderLeft = 1,
                BorderRight = 1,
                BorderColor = Theme.SurfaceBorder,
                BorderRadius = 10,
                PaddingTop = 24,
                PaddingBottom = 24,
                PaddingLeft = 28,
                PaddingRight = 28,
                MinWidth = 320,
                AlignItems = Align.Center,
                FlexDirection = FlexDirection.Column,
            },
            children:
            [
                Sx.Text(
                    style: () => new TextStyles { FontSize = 16, FontStyle = FontStyle.Bold },
                    layout: () => new Styles { MarginBottom = 8 },
                    text: () => title),
                Sx.Text(
                    style: () => new TextStyles
                    {
                        FontSize = 13,
                        ForegroundColor = Theme.TextFaint,
                        HorizontalAlignment = TextHorizontalAlignment.Center,
                    },
                    layout: () => new Styles { MarginBottom = 20 },
                    text: () => message),
                Sx.View(
                    style: () => new Styles { FlexDirection = FlexDirection.Row, JustifyContent = Justify.Center, GapColumn = 10, GapRow = 10 },
                    children:
                    [
                        ConfirmButton("Cancel", ButtonVariant.Default, onCancel),
                        ConfirmButton(confirmLabel, ButtonVariant.Danger, onConfirm)
                    ])
            ]);

        // Clicking the backdrop (but not the box) cancels.
        return Overlay(Sx.View(
            style: () => new Styles
            {
                Position = Position.Absolute,
                Top = 0,
                Left = 0,
                Right = 0,
                Bottom = 0,
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
                BackgroundColor = new Color(0, 0, 0, 179),
                ZIndex = 11,
            },
            onmousedown: _ => onCancel(),
            children: box));
    }

    private static View ConfirmButton(string label, ButtonVariant variant, Action onPress) => Button.Create(
        label: () => label,
        onPress: onPress,
        variant: variant,
        style: () => new Styles { PaddingTop = 10, PaddingBottom = 10, PaddingLeft = 28, PaddingRight = 28 });
}
