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

    private static readonly TextStyles Body = Sx.TextStyles(
        fontSize: 13,
        fontStyle: FontStyle.Bold,
        foregroundColor: Theme.MenuTextBlue
    );

    private static readonly TextStyles Dim = Sx.TextStyles(
        fontSize: 13,
        fontStyle: FontStyle.Bold,
        foregroundColor: Theme.MenuDim
    );

    private static readonly TextStyles Chip = Sx.TextStyles(
        fontSize: 11,
        fontStyle: FontStyle.Bold,
        foregroundColor: Theme.MenuDim,
        horizontalAlignment: TextHorizontalAlignment.Center
    );

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
                    ? Sx.Styles(
                        alignItems: Align.Center,
                        gapColumn: 6,
                        paddingTop: 4,
                        paddingBottom: 4,
                        paddingLeft: 8,
                        paddingRight: 8,
                        borderRadius: 6,
                        backgroundColor: r.IsClient ? Theme.MenuHighlight : Theme.MenuFill,
                        opacity: r.Wasted ? 0.5f : 1
                    )
                    : Sx.Styles(display: Display.None),
                children:
                [
                    Sx.Text(
                        style: () => At(index) is { IsClient: true } ? Body : Dim,
                        layout: () => Sx.Styles(width: 18, flexShrink: 0),
                        text: () => At(index) is { } r ? $"{r.Position}" : ""
                    ),
                    Sx.View(
                        style: () => Sx.Styles(width: 100, flexShrink: 0, overflow: Overflow.Hidden),
                        children: Sx.Text(
                            style: () => Body,
                            text: () => At(index)?.Name
                        )
                    ),
                    Sx.View(
                        style: () => Sx.Styles(
                            width: 34,
                            height: 4,
                            flexShrink: 0,
                            backgroundColor: new Color(255, 255, 255, 26),
                            borderRadius: 2,
                            overflow: Overflow.Hidden
                        ),
                        children: Sx.View(
                            style: () => Sx.Styles(
                                width: (Math.Clamp(At(index)?.Damage ?? 0, 0, 1) * 100).Percent(),
                                height: 100.Percent(),
                                backgroundColor: Theme.Danger
                            )
                        )
                    ),
                    Sx.Text(
                        style: () => Chip,
                        layout: () => Sx.Styles(width: 14, flexShrink: 0),
                        text: () => At(index) is { LockSlot: > 0 } r ? $"{r.LockSlot % 10}" : ""
                    )
                ]
            );
        }

        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
            children: rows
        );
    }
}
