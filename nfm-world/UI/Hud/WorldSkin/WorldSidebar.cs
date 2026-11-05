using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.WorldSkin;

/// <summary>Standings column. Fixed row slots whose display and text track the racer list each frame.</summary>
internal static class WorldSidebar
{
    private const int MaxRows = 8;

    private static readonly TextStyles Body = new()
    {
        FontSize = 13,
        FontStyle = FontStyle.Bold,
        ForegroundColor = Theme.MenuTextBlue,
    };

    private static readonly TextStyles Dim = new()
    {
        FontSize = 13,
        FontStyle = FontStyle.Bold,
        ForegroundColor = Theme.MenuDim,
    };

    private static readonly TextStyles Chip = new()
    {
        FontSize = 11,
        FontStyle = FontStyle.Bold,
        ForegroundColor = Theme.MenuDim,
        HorizontalAlignment = TextHorizontalAlignment.Center,
    };

    /// <param name="tick">Read first in every accessor so the plain racer list is re-read once per frame.</param>
    public static View Build(IHudData race, Func<int> tick)
    {
        HudRacer? At(int i)
        {
            _ = tick();
            return i < race.Racers.Count ? race.Racers[i] : null;
        }

        var rows = new object?[MaxRows];
        for (var i = 0; i < MaxRows; i++)
        {
            var index = i;
            rows[i] = Sx.View(
                style: () => At(index) is { } r
                    ? new Styles
                    {
                        AlignItems = Align.Center,
                        GapColumn = 6,
                        PaddingTop = 4,
                        PaddingBottom = 4,
                        PaddingLeft = 8,
                        PaddingRight = 8,
                        BorderRadius = 6,
                        BackgroundColor = r.IsClient ? Theme.MenuHighlight : Theme.MenuFill,
                        Opacity = r.Wasted ? 0.5f : 1,
                    }
                    : new Styles { Display = Display.None },
                children:
                [
                    Sx.Text(
                        style: () => At(index) is { IsClient: true } ? Body : Dim,
                        layout: () => new Styles { Width = 18, FlexShrink = 0 },
                        text: () => At(index) is { } r ? $"{r.Position}" : ""),
                    Sx.View(
                        style: () => new Styles { Width = 100, FlexShrink = 0, Overflow = Overflow.Hidden },
                        children: Sx.Text(
                            style: () => Body,
                            text: () => At(index)?.Name)),
                    Sx.View(
                        style: () => new Styles
                        {
                            Width = 34,
                            Height = 4,
                            FlexShrink = 0,
                            BackgroundColor = new Color(255, 255, 255, 26),
                            BorderRadius = 2,
                            Overflow = Overflow.Hidden,
                        },
                        children: Sx.View(style: () => new Styles
                        {
                            Width = MeasurementWidthHeight.Percent(Math.Clamp(At(index)?.Damage ?? 0, 0, 1) * 100),
                            Height = MeasurementWidthHeight.Percent(100),
                            BackgroundColor = Theme.Danger,
                        })),
                    Sx.Text(
                        style: () => Chip,
                        layout: () => new Styles { Width = 14, FlexShrink = 0 },
                        text: () => At(index) is { LockSlot: > 0 } r ? $"{r.LockSlot % 10}" : "")
                ]);
        }

        return Sx.View(
            style: () => new Styles { FlexDirection = FlexDirection.Column, GapRow = 4 },
            children: rows);
    }
}
