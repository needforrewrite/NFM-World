using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

/// <summary>An underlined menu tab, with local hover and optional host-owned navigation.</summary>
public static class MenuTab
{
    public static View Create(
        Func<string?> label,
        Func<bool> active,
        Action onPress,
        Func<string?>? name = null,
        Func<bool>? highlighted = null,
        bool keyboardNavigation = true,
        Func<Styles>? style = null,
        Func<TextStyles>? textStyle = null
    )
    {
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);
        var node = Sx.View(
            name: name,
            style: () =>
            {
                var defaults = Sx.Styles(
                    position: Position.Relative,
                    paddingTop: 14,
                    paddingBottom: 14,
                    paddingLeft: 24,
                    paddingRight: 24,
                    minWidth: 110,
                    alignItems: Align.Center,
                    justifyContent: Justify.Center,
                    backgroundColor: active() ? new Color(22, 42, 80, 220)
                        : hovered.Value || focused.Value ? new Color(22, 42, 80, 130) : Theme.Transparent
                );

                return style is null ? defaults : defaults.Over(style());
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: _ => onPress(),
            children:
            [
                Sx.Text(
                    text: label,
                    style: () =>
                    {
                        var defaults = Sx.TextStyles(
                            fontSize: 14,
                            fontStyle: FontStyle.Bold,
                            foregroundColor: active() ? new Color(205, 227, 255)
                                : hovered.Value ? new Color(185, 211, 255) : new Color(130, 155, 195)
                        );

                        return textStyle is null ? defaults : defaults.Over(textStyle());
                    }
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        position: Position.Absolute,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        height: 3,
                        backgroundColor: focused.Value || highlighted?.Invoke() == true ? new Color(230, 240, 255)
                            : active() ? new Color(128, 170, 255) : Theme.Transparent,
                        pointerEvents: false
                    )
                )
            ]
        );

        return keyboardNavigation
            ? KeyboardNavigation.Bind(
                node,
                key =>
                {
                    if (key is not (Key.Enter or Key.Space))

                        return false;
                    onPress();

                    return true;
                },
                value => focused.Value = value,
                active
            )
            : node;
    }
}
