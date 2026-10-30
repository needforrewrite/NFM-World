using Maxine.Extensions.Mathematics;
using Microsoft.UI.Reactor.Layout;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface.DriverInterface;
using SignalsDotnet;

namespace NFMWorld.Reactor.Sx;

/// <summary>
/// A style with an optional hover variant, as written at the definition site.
/// </summary>
/// <remarks>
/// Either style may set only what it changes: every <see cref="Styles"/> field is
/// nullable, so <see cref="Styles.Over"/> leaves anything a style leaves null alone.
/// </remarks>
public sealed class StyledStyle
{
    public Styles Base { get; init; } = new();

    /// <summary>Applied over <see cref="Base"/> while the pointer is over the node.</summary>
    public Styles? Hover { get; init; }

    public TextStyles Text { get; init; } = new();
    public TextStyles? HoverText { get; init; }
}

/// <summary>
/// Named, reusable styles - the equivalent of the Luau <c>styled(Tag){...}</c>.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// private static readonly Func&lt;Func&lt;Styles&gt;?, View&gt; Panel = Sx.Styled(Sx.View, new StyledStyle
/// {
///     Base = new Styles { FlexDirection = FlexDirection.Column, GapRow = 6 },
/// });
/// ...
/// Panel(children: ...);
/// </code>
///
/// The returned component runs the caller's style accessor inside the hover-dependent
/// style effect, so a hover change costs one property write and re-runs nothing else.
/// </remarks>
/// <summary>A styled <see cref="View"/> factory.</summary>
public delegate View StyledView(
    Func<Styles>? style = null,
    string? name = null,
    Action<MouseEvent>? onmousedown = null,
    params object?[] children);

/// <summary>A styled <see cref="Text"/> factory.</summary>
public delegate Text StyledText(
    Func<TextStyles>? style = null,
    Func<string?>? text = null,
    params object?[] children);

public static class Styled
{
    /// <summary>Styles a <see cref="View"/>.</summary>
    public static StyledView StyleView(
        StyledStyle definition) => (style, name, onmousedown, children) =>
    {
        var hovered = new Signal<bool>(false);

        return Sx.View(
            style: () => Merge(definition, style?.Invoke(), hovered.Value),
            name: () => name,
            onmousedown: onmousedown is null
                ? null
                : e => onmousedown(e),
            onmouseenter: definition.Hover is null ? null : _ => hovered.Value = true,
            onmouseleave: definition.Hover is null ? null : _ => hovered.Value = false,
            children: children);
    };

    /// <summary>Styles a <see cref="Text"/>.</summary>
    public static StyledText StyleText(
        StyledStyle definition) => (style, text, children) =>
    {
        var hovered = new Signal<bool>(false);

        return Sx.Text(
            style: () => MergeText(definition, style?.Invoke(), hovered.Value),
            text: text,
            children: children);
    };

    private static TextStyles MergeText(StyledStyle definition, TextStyles? caller, bool hovered)
    {
        var merged = definition.Text;

        if (hovered && definition.HoverText is { } hover)
            merged = merged.Over(hover);

        if (caller is { } overrides)
            merged = merged.Over(overrides);

        return merged;
    }

    private static Styles Merge(StyledStyle definition, Styles? caller, bool hovered)
    {
        var merged = definition.Base;

        if (hovered && definition.Hover is { } hover)
            merged = merged.Over(hover);

        if (caller is { } overrides)
            merged = merged.Over(overrides);

        return merged;
    }

}
