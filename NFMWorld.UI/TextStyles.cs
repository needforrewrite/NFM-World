using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Reactor;

public struct TextStyles() : IEquatable<TextStyles>
{
    /// <summary>
    /// Sets the fill color of the text. The default value is white.
    /// </summary>
    public Color ForegroundColor = new Color(255, 255, 255);
    
    /// <summary>
    /// Sets the stroke color of the text. Or set to null to disable the stroke.
    /// </summary>
    public Color? StrokeColor;
    
    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public FontFamily FontFamily = FontFamily.NotoSans;
    
    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public float FontSize = 12f;
    
    /// <summary>
    /// Gets or sets the font style.
    /// </summary>
    public FontStyle FontStyle = FontStyle.Plain;
    public BreakType BreakType = BreakType.Word;
    public OverflowBehavior OverflowBehavior = OverflowBehavior.Stretch;
    
    /// <summary>
    /// Sets the horizontal alignment of the text. The default value is <see cref="TextHorizontalAlignment.Left"/>.
    /// </summary>
    public TextHorizontalAlignment HorizontalAlignment = TextHorizontalAlignment.Left;
    
    /// <summary>
    /// Sets the vertical alignment of the text. The default value is <see cref="TextVerticalAlignment.Top"/>.
    /// </summary>
    public TextVerticalAlignment VerticalAlignment = TextVerticalAlignment.Top;

    public bool Equals(TextStyles other)
    {
        return ForegroundColor.Equals(other.ForegroundColor) && Nullable.Equals(StrokeColor, other.StrokeColor) && FontFamily == other.FontFamily && FontSize.Equals(other.FontSize) && FontStyle == other.FontStyle && BreakType == other.BreakType && OverflowBehavior == other.OverflowBehavior && HorizontalAlignment == other.HorizontalAlignment && VerticalAlignment == other.VerticalAlignment;
    }

    public override bool Equals(object? obj)
    {
        return obj is TextStyles other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ForegroundColor);
        hashCode.Add(StrokeColor);
        hashCode.Add((int)FontFamily);
        hashCode.Add(FontSize);
        hashCode.Add((int)FontStyle);
        hashCode.Add((int)BreakType);
        hashCode.Add((int)OverflowBehavior);
        hashCode.Add((int)HorizontalAlignment);
        hashCode.Add((int)VerticalAlignment);
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