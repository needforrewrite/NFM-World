using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>
/// In-race HUD overlay. Port of <c>data/uis/routes/racehud.luau</c>.
/// </summary>
/// <remarks>
/// Every telemetry value is its own slot, so a per-frame push re-sets only the leaves
/// whose value actually changed.
/// </remarks>
public static class RaceHudView
{
    /// <summary>What the pause menu asks the race to do. The phase fills these in.</summary>
    public sealed record Actions(Action Resume, Action Restart, Action Quit);

    private static UiState.RaceState Race => UiState.Race;

    public static Node Build(Actions actions) => Sx.View(
        style: () => new Styles
        {
            Width = MeasurementWidthHeight.Percent(100),
            Height = MeasurementWidthHeight.Percent(100),
            Position = Position.Relative,
        },
        children:
        [
            Sx.Show(
                when: () => StateText().Length > 0,
                then: () => GlassCardUi.CenterText(StateText, 24)),

            Sx.View(
                style: () => new Styles
                {
                    Position = Position.Absolute,
                    Top = 16,
                    Right = 16,
                    Width = 200,
                    FlexDirection = FlexDirection.Column,
                    GapColumn = 8,
                    GapRow = 8,
                },
                children:
                [
                    GlassCardUi.Card(
                        children: CornerRow("Power", () => Race.Power.Value / 0.98, Theme.Danger)),
                    GlassCardUi.Card(
                        children: CornerRow("Damage", () => Race.Damage.Value, Theme.Accent))
                ]),

            Sx.View(
                style: () => new Styles
                {
                    Position = Position.Absolute,
                    Top = 16,
                    Left = 16,
                    Width = 180,
                },
                children: Splits()),

            Sx.View(
                style: () => new Styles
                {
                    Position = Position.Absolute,
                    Bottom = 24,
                    Left = 0,
                    Right = 0,
                    FlexDirection = FlexDirection.Column,
                    AlignItems = Align.Center,
                },
                children: GlassCardUi.Card(
                    children: Sx.View(
                        style: () => new Styles { FlexDirection = FlexDirection.Column },
                        children:
                        [
                            Sx.Text(
                                style: () => Large,
                                text: () => $"{SpeedKmh()}"),
                            Sx.Text(
                                style: () => new TextStyles { FontSize = 14, ForegroundColor = Theme.TextMuted, StrokeColor = new Color(100, 100, 100, 102) },
                                text: () => "KM/H")
                        ]))),

            Sx.View(
                style: () => new Styles
                {
                    Position = Position.Absolute,
                    Bottom = 24,
                    Right = 24,
                    AlignItems = Align.FlexEnd,
                },
                children: GlassCardUi.Card(
                    children: Sx.View(
                        style: () => new Styles { FlexDirection = FlexDirection.Column, AlignItems = Align.Center, JustifyContent = Justify.Center },
                        children:
                        [
                            Sx.Text(
                                style: () => Large36,
                                text: () => $"{Race.Position.Value}/{Race.TotalRacers.Value}"),
                            Sx.Text(
                                style: () => new TextStyles { FontSize = 12, ForegroundColor = Theme.TextMuted, StrokeColor = new Color(100, 100, 100, 102) },
                                text: () => "POSITION")
                        ]))),

            Sx.Show(
                when: () => Race.Paused.Value,
                then: () => PauseMenuView.Build(actions))
        ]);

    private static readonly TextStyles Large = new()
    {
        FontSize = 56,
        FontStyle = FontStyle.Bold,
        StrokeColor = new Color(0, 0, 0),
    };

    private static readonly TextStyles Large36 = new()
    {
        FontSize = 36,
        FontStyle = FontStyle.Bold,
        StrokeColor = new Color(0, 0, 0),
    };

    private static readonly TextStyles CornerLabel = new()
    {
        FontSize = 11,
        ForegroundColor = Theme.TextMuted,
        StrokeColor = new Color(100, 100, 100, 102),
    };

    private static View CornerRow(string label, Func<double> value, Color color)
        => Sx.View(
            style: () => new Styles { FlexDirection = FlexDirection.Row },
            children:
            [
                Sx.View(
                    style: () => new Styles { MarginBottom = 4 },
                    children: Sx.Text(style: () => CornerLabel, text: () => label)),

                // Shrink-0 wrapper: the bar has to keep its own width instead of being
                // squeezed by the row, and it still needs the label off to one side.
                Sx.View(
                    style: () => new Styles { FlexShrink = 0, FlexGrow = 1 },
                    children: GlassCardUi.StatBar("", value, color, 10))
            ]);

    private static View Splits() => GlassCardUi.Card(
        color: () => Theme.Success,
        children: Sx.View(
            style: () => new Styles { FlexDirection = FlexDirection.Column },
            children:
            [
                Sx.View(
                    style: () => new Styles { MarginBottom = 4 },
                    children: Sx.Text(
                        style: () => CornerLabel,
                        text: () => $"Lap {Race.Lap.Value}/{Race.TotalLaps.Value}")),
                Sx.View(
                    children: Sx.Text(
                        style: () => Large28,
                        text: () => FormatTime(Race.LapTimeMs.Value)))
            ]));

    private static readonly TextStyles Large28 = new()
    {
        FontSize = 28,
        FontStyle = FontStyle.Bold,
        StrokeColor = new Color(0, 0, 0),
    };

    private static int SpeedKmh() => (int)Math.Floor(Race.Speed.Value * 1.4 * 21.0 * 60.0 * 60.0 / 100_000.0 + 0.5);

    private static string FormatTime(int ms)
    {
        var minutes = ms / (1000 * 60);
        var seconds = ms / 1000 % 60;
        var millis = ms % 1000;

        var prefix = minutes > 0 ? $"{minutes:00}:" : "";
        return $"{prefix}{seconds:00}.{millis:000}";
    }

    /// <summary>The centre-screen text, counting down or showing a transient message.</summary>
    private static string StateText()
    {
        var countdown = Race.CountdownTimer.Value;
        if (countdown > 0)
            return $"Starting in {countdown}";

        if (Race.StateText.Value is { } text)
        {
            if (Race.StateTextEndsAt.Value is { } endsAt && DateTime.Now >= endsAt)
                return "";

            return text;
        }

        return "";
    }
}
