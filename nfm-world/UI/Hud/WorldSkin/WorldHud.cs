using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.WorldSkin;

/// <summary>
/// The World race HUD, in the main menu's blue glass look: stats top-left, destination arrow top-centre,
/// standings on the right, radar bottom-left, speed bottom-centre, position bottom-right.
/// </summary>
internal sealed class WorldHud(IHudData race)
{
    private static readonly TextStyles Label = Sx.TextStyles(
        fontSize: 12,
        foregroundColor: Theme.MenuTextBlue,
        strokeColor: new Color(0, 0, 0, 100)
    );

    private static TextStyles Big(float size) => Sx.TextStyles(
        fontSize: size,
        fontFamily: FontFamily.Adventure,
        fontStyle: FontStyle.Bold,
        strokeColor: new Color(0, 0, 0)
    );

    // Plain-list data (racers, arrow) has no signal of its own; reading this re-runs the slot once per frame.
    private int Tick() => (race as UiState.RaceState)?.PresentationRevision.Value ?? 0;

    public View Build() => Sx.View(
        style: () => Sx.Styles(width: 100.Percent(), height: 100.Percent(), position: Position.Relative, pointerEvents: false),
        children:
        [
            Anchored(
                Sx.Styles(top: 16, left: 16, width: 190, flexDirection: FlexDirection.Column, gapRow: 8),
                [
                    Panel(
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Column),
                            children:
                            [
                                Sx.Text(style: () => Label, text: () => $"LAP {race.Lap}/{race.TotalLaps}"),
                                Sx.Text(style: () => Big(26), text: () => FormatTime(race.LapTimeMs))
                            ]
                        )
                    ),
                    Panel(
                        GlassCardUi.StatBar("POWER", () => race.PowerFraction, Theme.MenuBlue, 8),
                        GlassCardUi.StatBar("DAMAGE", () => race.DamageFraction, Theme.Danger, 8)
                    )
                ]
            ),

            Anchored(
                Sx.Styles(top: 16, left: 0, right: 0, flexDirection: FlexDirection.Column, alignItems: Align.Center),
                [Arrow()]
            ),

            Anchored(Sx.Styles(top: 16, right: 16), [WorldSidebar.Build(race, Tick)]),

            Anchored(Sx.Styles(bottom: 16, left: 16), [new WorldRadar(race)]),

            Anchored(
                Sx.Styles(bottom: 16, left: 0, right: 0, flexDirection: FlexDirection.Column, alignItems: Align.Center),
                [
                    Panel(
                        Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Column, alignItems: Align.Center),
                            children:
                            [
                                Sx.Text(style: () => Big(48), text: () => $"{race.SpeedKmh}"),
                                Sx.Text(style: () => Label, text: () => "KM/H")
                            ]
                        )
                    )
                ]
            ),

            Sx.Show(
                () => race.HasPosition,
                () => Anchored(
                    Sx.Styles(bottom: 16, right: 16),
                    [
                        Panel(
                            Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Column, alignItems: Align.Center),
                                children:
                                [
                                    Sx.Text(style: () => Big(32), text: () => $"{race.Position}/{race.TotalRacers}"),
                                    Sx.Text(style: () => Label, text: () => "POSITION")
                                ]
                            )
                        )
                    ]
                )
            )
        ]
    );

    private View Arrow() => Sx.View(
        // Hidden while there is nothing to point at (no checkpoint, noarrow(), wasted). A
        // spectating client has no heading to point from, but still needs the name of the car it
        // is riding.
        style: () => Tick() >= 0 && (race.ArrowAngle is not null || race.Spectating)
            ? PanelStyle(Sx.Styles(alignItems: Align.Center))
            : Sx.Styles(display: Display.None),
        children:
        [
            // Draws nothing when ArrowAngle is null, which is the spectating case.
            new WorldArrow(race, 44),
            Sx.Text(
                style: () => Label,
                layout: () => Sx.Styles(marginTop: 4),
                text: () => Tick() < 0 || race.ArrowTarget.Length == 0
                    ? "CHECKPOINT"
                    : race.ArrowAtCars || race.Spectating
                        ? race.ArrowTarget
                        : "CHECKPOINT"
            )
        ]
    );

    private static View Anchored(Styles style, object?[] children) => Sx.View(
        style: () => style.Over(Sx.Styles(position: Position.Absolute, flexShrink: 0)),
        children: children
    );

    private static View Panel(params object?[] children) => Sx.View(style: () => PanelStyle(null), children: children);

    private static Styles PanelStyle(Styles? extra)
    {
        var style = Sx.Styles(
            backgroundColor: Theme.MenuPanel,
            borderColor: Theme.MenuBorder,
            borderTop: 1,
            borderBottom: 1,
            borderLeft: 1,
            borderRight: 1,
            borderRadius: 10,
            paddingTop: 10,
            paddingBottom: 10,
            paddingLeft: 14,
            paddingRight: 14,
            flexDirection: FlexDirection.Column
        );
        return extra is { } e ? style.Over(e) : style;
    }

    private static string FormatTime(int ms)
    {
        var minutes = ms / (1000 * 60);
        var seconds = ms / 1000 % 60;
        var millis = ms % 1000;

        var prefix = minutes > 0 ? $"{minutes:00}:" : "";
        return $"{prefix}{seconds:00}.{millis:000}";
    }
}
