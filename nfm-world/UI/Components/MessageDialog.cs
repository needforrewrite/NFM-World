using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

public static class MessageDialog
{
    public sealed record ActionButton(string Label, Action OnPress);

    public static View Build(string title, string message, IReadOnlyList<ActionButton> buttons, Signal<int> selected)
        => Build(title, () => message, buttons, selected);

    public static View Build(string title, Func<string> message, IReadOnlyList<ActionButton> buttons, Signal<int> selected)
        => Sx.View(
            name: () => "Message dialog " + title,
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                top: 0,
                bottom: 0,
                zIndex: 10000,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 180)
            ),
            onmousedown: e => e.StopPropagation(),
            children: Sx.View(
                style: () => Sx.Styles(
                    width: 90.Percent(),
                    maxWidth: 500,
                    maxHeight: 90.Percent(),
                    minHeight: 0,
                    flexDirection: FlexDirection.Column,
                    paddingTop: 28,
                    paddingBottom: 28,
                    paddingLeft: 28,
                    paddingRight: 28,
                    gapRow: 20,
                    overflow: Overflow.Scroll,
                    backgroundColor: Theme.MenuPanel,
                    borderRadius: 8,
                    borderTop: 2,
                    borderBottom: 2,
                    borderLeft: 2,
                    borderRight: 2,
                    borderColor: Theme.MenuBlue
                ),
                children:
                [
                    Sx.Text(
                        style: () => Sx.WrappingText(
                            fontSize: 26,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: Theme.MenuBlue
                        ),
                        layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                        text: () => title
                    ),
                    Sx.Text(
                        style: () => Sx.WrappingText(fontSize: 14, foregroundColor: Theme.MenuTextBlue),
                        layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                        text: message
                    ),
                    Sx.View(
                        style: () => Sx.Styles(
                            width: 100.Percent(),
                            flexShrink: 0,
                            flexDirection: FlexDirection.Row,
                            flexWrap: Wrap.Wrap,
                            justifyContent: Justify.FlexEnd,
                            gapColumn: 10,
                            gapRow: 8
                        ),
                        children: buttons.Select((button, index) =>
                            {
                                var node = Button.Create(
                                    () => button.Label,
                                    button.OnPress,
                                    active: () => selected.Value == index,
                                    style: () => Sx.Styles(minWidth: 90, backgroundColor: selected.Value == index
                                    ? Theme.MenuHighlight : Theme.MenuFill, borderColor: selected.Value == index
                                    ? new Color(205, 227, 255) : Theme.MenuBorder),
                                    textStyle: () => Sx.TextStyles(
                                        fontSize: 13,
                                        fontStyle: FontStyle.Plain,
                                        foregroundColor: Theme.MenuTextBlue
                                    ),
                                    name: () => "Message action " + button.Label
                                );
                                node.MouseEntered += _ => selected.Value = index;

                                return node;
                            }
                        ).ToArray()
                    )
                ]
            )
        );
}
