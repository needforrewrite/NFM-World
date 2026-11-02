using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Routes;

/// <summary>
/// In-race HUD overlay. Port of <c>data/uis/routes/racehud.luau</c>.
/// </summary>
/// <remarks>
/// Every telemetry value is its own slot, so a per-frame push re-sets only the leaves
/// whose value actually changed.
/// </remarks>
internal sealed class WorldHudView(IHudData race)
{
    public Node Build() => Sx.View(
        style: () => Sx.Styles(
            width: MeasurementWidthHeight.Percent(100),
            height: MeasurementWidthHeight.Percent(100),
            position: Position.Relative),
        children:
        [
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
                        children: CornerRow("Power", () => race.PowerFraction, Theme.Danger)),
                    GlassCardUi.Card(
                        children: CornerRow("Damage", () => race.DamageFraction, Theme.Accent))
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
                                text: () => $"{race.SpeedKmh}"),
                            Sx.Text(
                                style: () => new TextStyles { FontSize = 14, ForegroundColor = Theme.TextMuted, StrokeColor = new Color(100, 100, 100, 102) },
                                text: () => "KM/H")
                        ]))),

            Sx.Show(() => race.HasPosition, () => Sx.View(
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
                                text: () => $"{race.Position}/{race.TotalRacers}"),
                            Sx.Text(
                                style: () => new TextStyles { FontSize = 12, ForegroundColor = Theme.TextMuted, StrokeColor = new Color(100, 100, 100, 102) },
                                text: () => "POSITION")
                        ]))))
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

    private View Splits() => GlassCardUi.Card(
        color: () => Theme.Success,
        children: Sx.View(
            style: () => new Styles { FlexDirection = FlexDirection.Column },
            children:
            [
                Sx.View(
                    style: () => new Styles { MarginBottom = 4 },
                    children: Sx.Text(
                        style: () => CornerLabel,
                        text: () => $"Lap {race.Lap}/{race.TotalLaps}")),
                Sx.View(
                    children: Sx.Text(
                        style: () => Large28,
                        text: () => FormatTime(race.LapTimeMs)))
            ]));

    private static readonly TextStyles Large28 = new()
    {
        FontSize = 28,
        FontStyle = FontStyle.Bold,
        StrokeColor = new Color(0, 0, 0),
    };

    private static string FormatTime(int ms)
    {
        var minutes = ms / (1000 * 60);
        var seconds = ms / 1000 % 60;
        var millis = ms % 1000;

        var prefix = minutes > 0 ? $"{minutes:00}:" : "";
        return $"{prefix}{seconds:00}.{millis:000}";
    }

}
