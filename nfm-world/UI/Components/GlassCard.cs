using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.UI.Components;

/// <summary>
/// Shared glassmorphism primitives. Port of <c>data/uis/components/glasscard.luau</c>.
/// </summary>
public static class GlassCardUi
{
    private static readonly Styles Wrapper = new()
    {
        BackgroundColor = new Color(0, 0, 0, 51),
        BorderTop = 1,
        BorderBottom = 1,
        BorderLeft = 1,
        BorderRight = 1,
        BorderColor = Theme.Transparent,
        PaddingTop = 16,
        PaddingBottom = 16,
        PaddingLeft = 20,
        PaddingRight = 20,
        Position = Position.Relative,
        Overflow = Overflow.Hidden,
    };

    /// <summary>
    /// A card with a 3px coloured accent bar. <paramref name="color"/> and
    /// <paramref name="style"/> are read inside the style effect, so changing either
    /// costs one property write.
    /// </summary>
    public static View Card(
        Func<Color>? color = null,
        Func<Styles>? style = null,
        Action<ClayDom.Events.MouseEvent>? onmousedown = null,
        params object?[] children)
        => Sx.View(
            style: () => Merge(style?.Invoke(), color?.Invoke()),
            onmousedown: onmousedown,
            children: children);

    /// <summary>
    /// The card's own style, then the caller's overrides last. <see cref="Styles.Over"/>
    /// only applies fields the caller actually set, so a card whose style says nothing
    /// about padding keeps the wrapper's.
    /// </summary>
    private static Styles Merge(Styles? overrides, Color? border)
    {
        var merged = overrides is { } s ? Wrapper.Over(s) : Wrapper;

        if (border is { } c)
            merged.BorderColor = c;

        return merged;
    }

    /// <summary>
    /// A labelled 0..1 progress bar. A changing value re-sets only this bar's fill.
    /// </summary>
    public static View StatBar(
        string label = "",
        Func<double>? value = null,
        Color? color = null,
        float height = 8)
    {
        var fillColor = color ?? Theme.Accent;

        var fill = Sx.View(style: () => new Styles
        {
            Width = MeasurementWidthHeight.Percent((float)(Clamped(value) * 100)),
            Height = MeasurementWidthHeight.Percent(100),
            BackgroundColor = fillColor,
            BorderRadius = height / 2,
        });

        return Sx.View(
            style: () => new Styles { MarginBottom = 8, FlexDirection = FlexDirection.Column, Width = MeasurementWidthHeight.Percent(100) },
            children:
            [
                Sx.View(
                    style: () => new Styles { JustifyContent = Justify.SpaceBetween, MarginBottom = 4 },
                    children:
                    [
                        Sx.Text(style: () => SmallLabel, layout: () => new Styles { FlexShrink = 0 }, children: label),
                        Sx.Text(
                            style: () => SmallLabel,
                            layout: () => new Styles { FlexShrink = 0 },
                            text: () => $"{Math.Floor(Clamped(value) * 100 + 0.5)}%")
                    ]),
                Sx.View(
                    style: () => new Styles
                    {
                        Width = MeasurementWidthHeight.Percent(100),
                        Height = height,
                        BackgroundColor = new Color(255, 255, 255, 26),
                        BorderRadius = height / 2,
                        Overflow = Overflow.Hidden,
                    },
                    children: fill)
            ]);
    }

    /// <summary>Large centre-screen text, split on newlines.</summary>
    public static View CenterText(Func<string?>? text = null, float size = 64, Color? color = null)
    {
        var textColor = color ?? Theme.TextSecondary;

        return Sx.View(
            style: () => new Styles
            {
                Position = Position.Absolute,
                Top = 0,
                Left = 0,
                Right = 0,
                Bottom = 0,
                FlexDirection = FlexDirection.Column,
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
            },
            children: Sx.For(
                each: () => SplitLines(text?.Invoke() ?? ""),
                row: (line, _) => Sx.Text(
                    style: () => new TextStyles
                    {
                        FontSize = size,
                        FontStyle = FontStyle.Bold,
                        ForegroundColor = textColor,
                        HorizontalAlignment = TextHorizontalAlignment.Center,
                    },
                    text: () => line)));
    }

    private static readonly TextStyles SmallLabel = new()
    {
        FontSize = 12,
        ForegroundColor = new Color(255, 255, 255, 178),
        StrokeColor = new Color(100, 100, 100, 102),
    };

    private static double Clamped(Func<double>? value)
        => Math.Clamp(value?.Invoke() ?? 0, 0, 1);

    private static IReadOnlyList<string> SplitLines(string text)
        => text.Split('\n');
}
