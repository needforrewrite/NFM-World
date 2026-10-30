using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Reactor;

/// <summary>
/// Text styling. Every field is nullable and null means "not set", so one style can be
/// merged onto another without its unset fields overwriting anything. Defaults live in
/// <see cref="Resolved"/> and are applied where the text is laid out and drawn.
/// </summary>
public struct TextStyles() : IEquatable<TextStyles>
{
    /// <summary>Sets the fill color of the text. The default is white.</summary>
    public Color? ForegroundColor;

    /// <summary>Sets the stroke color of the text. Null disables the stroke.</summary>
    public Color? StrokeColor;

    public FontFamily? FontFamily;
    public float? FontSize;
    public FontStyle? FontStyle;
    public BreakType? BreakType;
    public OverflowBehavior? OverflowBehavior;
    public TextHorizontalAlignment? HorizontalAlignment;
    public TextVerticalAlignment? VerticalAlignment;

    /// <summary>Defaults for every field this style leaves null.</summary>
    public readonly TextStyles Resolved => new()
    {
        ForegroundColor = ForegroundColor ?? new Color(255, 255, 255),
        StrokeColor = StrokeColor,
        FontFamily = FontFamily ?? DriverInterface.DriverInterface.FontFamily.NotoSans,
        FontSize = FontSize ?? 12f,
        FontStyle = FontStyle ?? DriverInterface.DriverInterface.FontStyle.Plain,
        BreakType = BreakType ?? Reactor.BreakType.Word,
        OverflowBehavior = OverflowBehavior ?? Reactor.OverflowBehavior.Stretch,
        HorizontalAlignment = HorizontalAlignment ?? TextHorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment ?? TextVerticalAlignment.Top,
    };

    /// <summary>This style with <paramref name="overlay"/>'s set fields applied on top.</summary>
    public readonly TextStyles Over(TextStyles overlay) => new()
    {
        ForegroundColor = overlay.ForegroundColor ?? ForegroundColor,
        StrokeColor = overlay.StrokeColor ?? StrokeColor,
        FontFamily = overlay.FontFamily ?? FontFamily,
        FontSize = overlay.FontSize ?? FontSize,
        FontStyle = overlay.FontStyle ?? FontStyle,
        BreakType = overlay.BreakType ?? BreakType,
        OverflowBehavior = overlay.OverflowBehavior ?? OverflowBehavior,
        HorizontalAlignment = overlay.HorizontalAlignment ?? HorizontalAlignment,
        VerticalAlignment = overlay.VerticalAlignment ?? VerticalAlignment,
    };

    public readonly bool Equals(TextStyles other)
    {
        return Nullable.Equals(ForegroundColor, other.ForegroundColor) && Nullable.Equals(StrokeColor, other.StrokeColor) && FontFamily == other.FontFamily && Nullable.Equals(FontSize, other.FontSize) && FontStyle == other.FontStyle && BreakType == other.BreakType && OverflowBehavior == other.OverflowBehavior && HorizontalAlignment == other.HorizontalAlignment && VerticalAlignment == other.VerticalAlignment;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is TextStyles other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ForegroundColor);
        hashCode.Add(StrokeColor);
        hashCode.Add(FontFamily);
        hashCode.Add(FontSize);
        hashCode.Add(FontStyle);
        hashCode.Add(BreakType);
        hashCode.Add(OverflowBehavior);
        hashCode.Add(HorizontalAlignment);
        hashCode.Add(VerticalAlignment);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(TextStyles left, TextStyles right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TextStyles left, TextStyles right)
    {
        return !left.Equals(right);
    }
}
