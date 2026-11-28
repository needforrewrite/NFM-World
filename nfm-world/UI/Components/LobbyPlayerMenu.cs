using System.Numerics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

internal static class LobbyPlayerMenu
{
    public static Node Build(
        Vector2 position,
        Action copyCar,
        Action copyColors,
        Action close,
        Func<bool> canInvite,
        Action invite
    ) => BuildMenu(
        position,
        close,
        [
            Item("Copy car", copyCar),
            Item("Copy colors", copyColors),
            Sx.Show(canInvite, () => Item("Invite to Game", invite))
        ]
    );

    public static Node Build(Vector2 position, Action close, params (string Label, Action Press)[] items)
        => BuildMenu(position, close, items.Select(item => Item(item.Label, item.Press)).ToArray());

    private static Node BuildMenu(Vector2 position, Action close, Node[] items)
        => Sx.View(
            name: () => "Lobby player menu overlay",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                right: 0,
                bottom: 0,
                zIndex: 2000
            ),
            onmousedown: e => { e.StopPropagation(); close(); },
            onkeydown: e => { if (e.KeyCode == Key.Escape) close(); },
            tabIndex: 0,
            onmount: view => FocusManager.FocusedNode = view,
            children: [Sx.View(
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: position.X,
                    top: position.Y,
                    width: 170,
                    flexDirection: FlexDirection.Column,
                    paddingTop: 4,
                    paddingBottom: 4,
                    backgroundColor: Theme.MenuPanel,
                    borderRadius: 6
                ),
                onmousedown: e => e.StopPropagation(),
                children: items
            )]
        );

    private static Node Item(string label, Action action)
    {
        var hovered = new Signal<bool>(false);

        return Sx.View(
            name: () => label,
            style: () => Sx.Styles(
                height: 28,
                flexShrink: 0,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: hovered.Value ? Theme.MenuHighlight : Theme.Surface,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: hovered.Value ? Theme.MenuBlue : Theme.MenuBorder,
                borderRadius: 4
            ),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: e =>
            {
                e.StopPropagation();
                if (e.Button == MouseButton.Primary)
                    action();
            },
            children: [Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 11,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: hovered.Value ? new Color(255, 255, 255) : Theme.MenuBlue
                ),
                text: () => label
            )]
        );
    }
}
