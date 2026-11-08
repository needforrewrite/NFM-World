using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.UI.Components;

/// <summary>
/// Shared glassmorphism primitives. Port of <c>data/uis/components/glasscard.luau</c>.
/// </summary>
public static class GlassCardUi
{
    private static readonly Styles Wrapper = Sx.Styles(
        backgroundColor: new Color(0, 0, 0, 51),
        borderTop: 1,
        borderBottom: 1,
        borderLeft: 1,
        borderRight: 1,
        borderColor: Theme.Transparent,
        paddingTop: 16,
        paddingBottom: 16,
        paddingLeft: 20,
        paddingRight: 20,
        position: Position.Relative,
        overflow: Overflow.Hidden
    );

    /// <summary>
    /// A card with a 3px coloured accent bar. <paramref name="color"/> and
    /// <paramref name="style"/> are read inside the style effect, so changing either
    /// costs one property write.
    /// </summary>
    public static View Card(
        Func<Color>? color = null,
        Func<Styles>? style = null,
        Action<ClayDom.Events.MouseEvent>? onmousedown = null,
        params object?[] children
    )
        => Sx.View(
            style: () => Merge(style?.Invoke(), color?.Invoke()),
            onmousedown: onmousedown,
            children: children
        );

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
        float height = 8
    )
    {
        var fillColor = color ?? Theme.Accent;

        var fill = Sx.View(
            style: () => Sx.Styles(
                width: ((float)(Clamped(value) * 100)).Percent(),
                height: 100.Percent(),
                backgroundColor: fillColor,
                borderRadius: height / 2
            )
        );

        return Sx.View(
            style: () => Sx.Styles(marginBottom: 8, flexDirection: FlexDirection.Column, width: 100.Percent()),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(justifyContent: Justify.SpaceBetween, marginBottom: 4),
                    children:
                    [
                        Sx.Text(style: () => SmallLabel, layout: () => Sx.Styles(flexShrink: 0), children: label),
                        Sx.Text(
                            style: () => SmallLabel,
                            layout: () => Sx.Styles(flexShrink: 0),
                            text: () => $"{Math.Floor(Clamped(value) * 100 + 0.5)}%"
                        )
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        width: 100.Percent(),
                        height: height,
                        backgroundColor: new Color(255, 255, 255, 26),
                        borderRadius: height / 2,
                        overflow: Overflow.Hidden
                    ),
                    children: fill
                )
            ]
        );
    }

    /// <summary>Large centre-screen text, split on newlines.</summary>
    public static View CenterText(
        Func<string?>? text = null,
        float size = 64,
        Color? color = null,
        Func<Color>? colorAccessor = null
    )
    {
        var textColor = color ?? Theme.TextSecondary;

        return Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                flexDirection: FlexDirection.Column,
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            children: Sx.For(
                each: () => SplitLines(text?.Invoke() ?? ""),
                row: (line, _) => Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: size,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: colorAccessor?.Invoke() ?? textColor,
                        horizontalAlignment: TextHorizontalAlignment.Center
                    ),
                    text: () => line
                )
            )
        );
    }

    private static readonly TextStyles SmallLabel = Sx.TextStyles(
        fontSize: 12,
        foregroundColor: new Color(255, 255, 255, 178),
        strokeColor: new Color(100, 100, 100, 102)
    );

    private static double Clamped(Func<double>? value)
        => Math.Clamp(value?.Invoke() ?? 0, 0, 1);

    private static IReadOnlyList<string> SplitLines(string text)
        => text.Split('\n');
}
