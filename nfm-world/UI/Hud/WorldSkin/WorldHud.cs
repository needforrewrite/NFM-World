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
    private static readonly TextStyles Label = new()
    {
        FontSize = 12,
        ForegroundColor = Theme.MenuTextBlue,
        StrokeColor = new Color(0, 0, 0, 100),
    };

    private static TextStyles Big(float size) => new()
    {
        FontSize = size,
        FontFamily = FontFamily.Adventure,
        FontStyle = FontStyle.Bold,
        StrokeColor = new Color(0, 0, 0),
    };

    // Plain-list data (racers, arrow) has no signal of its own; reading this re-runs the slot once per frame.
    private int Tick() => (race as UiState.RaceState)?.PresentationRevision.Value ?? 0;

    public View Build() => Sx.View(
        style: () => Sx.Styles(width: "100%", height: "100%", position: Position.Relative, pointerEvents: false),
        children:
        [
            Anchored(new Styles { Top = 16, Left = 16, Width = 190, FlexDirection = FlexDirection.Column, GapRow = 8 },
            [
                Panel(Sx.View(
                    style: () => new Styles { FlexDirection = FlexDirection.Column },
                    children:
                    [
                        Sx.Text(style: () => Label, text: () => $"LAP {race.Lap}/{race.TotalLaps}"),
                        Sx.Text(style: () => Big(26), text: () => FormatTime(race.LapTimeMs))
                    ])),
                Panel(
                    GlassCardUi.StatBar("POWER", () => race.PowerFraction, Theme.MenuBlue, 8),
                    GlassCardUi.StatBar("DAMAGE", () => race.DamageFraction, Theme.Danger, 8))
            ]),

            Anchored(new Styles { Top = 16, Left = 0, Right = 0, FlexDirection = FlexDirection.Column, AlignItems = Align.Center }, [Arrow()]),

            Anchored(new Styles { Top = 16, Right = 16 }, [WorldSidebar.Build(race, Tick)]),

            Anchored(new Styles { Bottom = 16, Left = 16 }, [new WorldRadar(race)]),

            Anchored(new Styles { Bottom = 16, Left = 0, Right = 0, FlexDirection = FlexDirection.Column, AlignItems = Align.Center },
            [
                Panel(Sx.View(
                    style: () => new Styles { FlexDirection = FlexDirection.Column, AlignItems = Align.Center },
                    children:
                    [
                        Sx.Text(style: () => Big(48), text: () => $"{race.SpeedKmh}"),
                        Sx.Text(style: () => Label, text: () => "KM/H")
                    ]))
            ]),

            Sx.Show(() => race.HasPosition, () => Anchored(new Styles { Bottom = 16, Right = 16 },
            [
                Panel(Sx.View(
                    style: () => new Styles { FlexDirection = FlexDirection.Column, AlignItems = Align.Center },
                    children:
                    [
                        Sx.Text(style: () => Big(32), text: () => $"{race.Position}/{race.TotalRacers}"),
                        Sx.Text(style: () => Label, text: () => "POSITION")
                    ]))
            ]))
        ]);

    private View Arrow() => Sx.View(
        // Hidden while there is nothing to point at (no checkpoint, noarrow(), wasted).
        style: () => Tick() >= 0 && race.ArrowAngle is not null
            ? PanelStyle(new Styles { AlignItems = Align.Center })
            : new Styles { Display = Display.None },
        children:
        [
            new WorldArrow(race, 44),
            Sx.Text(
                style: () => Label,
                layout: () => new Styles { MarginTop = 4 },
                text: () => Tick() >= 0 && race.ArrowAtCars && race.ArrowTarget.Length > 0 ? race.ArrowTarget : "CHECKPOINT")
        ]);

    private static View Anchored(Styles style, object?[] children) => Sx.View(
        style: () => style.Over(new Styles { Position = Position.Absolute, FlexShrink = 0 }),
        children: children);

    private static View Panel(params object?[] children) => Sx.View(style: () => PanelStyle(null), children: children);

    private static Styles PanelStyle(Styles? extra)
    {
        var style = new Styles
        {
            BackgroundColor = Theme.MenuPanel,
            BorderColor = Theme.MenuBorder,
            BorderTop = 1,
            BorderBottom = 1,
            BorderLeft = 1,
            BorderRight = 1,
            BorderRadius = 10,
            PaddingTop = 10,
            PaddingBottom = 10,
            PaddingLeft = 14,
            PaddingRight = 14,
            FlexDirection = FlexDirection.Column,
        };
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
