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
        Func<TextStyles>? textStyle = null)
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

                var result = new Styles
                {
                    FlexDirection = FlexDirection.Row,
                    AlignItems = Align.Center,
                    JustifyContent = Justify.Center,
                    GapColumn = 6,
                    GapRow = 6,
                    PaddingTop = (float)padTop,
                    PaddingBottom = (float)padBottom,
                    PaddingLeft = (float)padLeft,
                    PaddingRight = (float)padRight,
                    BackgroundColor = background,
                    BorderTop = 1,
                    BorderBottom = 1,
                    BorderLeft = 1,
                    BorderRight = 1,
                    BorderColor = border,
                    BorderRadius = 6,
                    Width = fullWidth ? MeasurementWidthHeight.Percent(100) : MeasurementWidthHeight.Undefined,
                };

                // Over only applies what the caller set, so an override that mentions
                // only, say, MinWidth keeps the variant's padding and border.
                return style?.Invoke() is { } overrides ? result.Over(overrides) : result;
            },
            onmousedown: _ =>
            {
                if (!disabled)
                    onPress();
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.Text(
                    style: () => TextFor(variant, active?.Invoke() ?? false, disabled, textStyle?.Invoke()),
                    text: label)
            ]);
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

        TextStyles result = new TextStyles { FontSize = 13, FontStyle = FontStyle.Bold, ForegroundColor = color };
        return overrides is { } over ? result.Over(over) : result;
    }

}
