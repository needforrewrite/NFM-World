using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

public enum ButtonVariant
{
    Default,
    Primary,
    Danger,
    Warning,
    Ghost
}

public enum ButtonSize
{
    Sm,
    Md,
    Lg
}

/// <summary>
/// The one button, shared by every view. Port of <c>data/library/ui/button.luau</c>.
/// </summary>
/// <remarks>
/// Hover is a signal local to each button, so moving the mouse re-styles the hovered
/// button only - not its parent, and not its siblings.
/// </remarks>
public static class Button
{
    public static View Create(
        Func<string?> label,
        Action onPress,
        ButtonVariant variant = ButtonVariant.Default,
        Func<bool>? active = null,
        ButtonSize size = ButtonSize.Md,
        bool fullWidth = false,
        bool disabled = false,
        Func<Styles>? style = null,
        Func<TextStyles>? textStyle = null
    )
    {
        var hovered = new Signal<bool>(false);

        var (padTop, padBottom, padLeft, padRight) = size switch
        {
            ButtonSize.Lg => (14, 14, 24, 24),
            ButtonSize.Sm => (4, 4, 10, 10),
            _ => (8, 8, 20, 20)
        };

        return Sx.View(
            style: () =>
            {
                var isActive = active?.Invoke() ?? false;
                var hover = hovered.Value && !disabled;

                var (background, border) = variant switch
                {
                    ButtonVariant.Primary => (hover ? Theme.AccentSoftStrong : Theme.Surface, Theme.AccentBorder),
                    ButtonVariant.Danger => (hover ? Theme.DangerSoftStrong : Theme.DangerSoft, Theme.DangerBorder),
                    ButtonVariant.Warning => (hover ? Theme.WarningSoftStrong : Theme.WarningSoft, Theme.WarningBorder),
                    _ when isActive => (hover ? Theme.AccentSoftStrong : Theme.AccentSoft, Theme.AccentBorder),
                    ButtonVariant.Ghost => (hover ? Theme.SurfaceHover : Theme.Transparent, Theme.Transparent),
                    _ => (hover ? Theme.SurfaceHover : Theme.Surface, Theme.SurfaceBorder)
                };

                var result = Sx.Styles(
                    flexDirection: FlexDirection.Row,
                    alignItems: Align.Center,
                    justifyContent: Justify.Center,
                    gapColumn: 6,
                    gapRow: 6,
                    paddingTop: padTop,
                    paddingBottom: padBottom,
                    paddingLeft: padLeft,
                    paddingRight: padRight,
                    backgroundColor: background,
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: border,
                    borderRadius: 6,
                    width: fullWidth ? 100.Percent() : MeasurementWidthHeight.Undefined
                );

                // Over only applies what the caller set, so an override that mentions
                // only, say, MinWidth keeps the variant's padding and border.
                return style?.Invoke() is { } overrides ? result.Over(overrides) : result;
            },
            onmousedown: e =>
            {
                // Handled here: a modal backdrop behind the button must not also see it.
                e.StopPropagation();
                if (!disabled)
                    onPress();
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.Text(
                    style: () => TextFor(variant, active?.Invoke() ?? false, disabled, textStyle?.Invoke()),
                    text: label
                )
            ]
        );
    }

    private static TextStyles TextFor(ButtonVariant variant, bool active, bool disabled, TextStyles? overrides)
    {
        var color = disabled
            ? Theme.TextFaint
            : variant switch
            {
                ButtonVariant.Warning when active => Theme.Warning,
                ButtonVariant.Ghost when !active => Theme.TextSecondary,
                _ => Theme.TextPrimary
            };

        var result = Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold, foregroundColor: color);
        return overrides is { } over ? result.Over(over) : result;
    }
}
