using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

/// <summary>The menu palette and keyboard focus treatment for the shared button.</summary>
public static class MenuButton
{
    public static View Create(
        Func<string?> label,
        Action onPress,
        ButtonVariant variant = ButtonVariant.Default,
        Func<bool>? active = null,
        ButtonSize size = ButtonSize.Md,
        bool disabled = false,
        Func<string?>? name = null,
        Func<Styles>? style = null,
        Func<TextStyles>? textStyle = null
    )
    {
        var focused = new Signal<bool>(false);
        var hovered = new Signal<bool>(false);
        var node = Button.Create(
            label,
            onPress,
            variant,
            active,
            size,
            disabled: disabled,
            name: name,
            style: () =>
            {
                var defaults = Sx.Styles(
                    backgroundColor: focused.Value || hovered.Value || active?.Invoke() == true ? Theme.MenuHighlight : Theme
                        .MenuFill,
                    borderColor: focused.Value || hovered.Value ? new Color(205, 227, 255)
                        : variant == ButtonVariant.Danger ? Theme.DangerBorder : Theme.MenuBorder,
                    opacity: disabled ? 0.5f : 1
                );

                return style is null ? defaults : defaults.Over(style());
            },
            textStyle: () =>
            {
                var defaults = Sx.TextStyles(
                    fontSize: 15,
                    fontFamily: FontFamily.NotoSans,
                    fontStyle: FontStyle.Plain,
                    foregroundColor: focused.Value || hovered.Value ? new Color(235, 244, 255) : Theme.MenuBlue
                );

                return textStyle is null ? defaults : defaults.Over(textStyle());
            }
        );
        node.MouseEntered += _ => hovered.Value = !disabled;
        node.MouseLeft += _ => hovered.Value = false;

        if (disabled)

            return node;

        return KeyboardNavigation.Bind(
            node,
            key =>
            {
                if (key is not (Key.Enter or Key.Space))

                    return false;
                onPress();

                return true;
            },
            value => focused.Value = value
        );
    }
}
