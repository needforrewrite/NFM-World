using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Reactor;

public struct Styles() : IEquatable<Styles>
{
    /// <summary>
    /// CSS: visibility - Controls whether the element is visible (visible/hidden/collapsed)
    /// </summary>
    public Visibility Visibility = Visibility.Visible;

    /// <summary>
    /// CSS: opacity - Sets the transparency level (0.0 = fully transparent, 1.0 = fully opaque)
    /// </summary>
    public float Opacity = 1.0f;

    // https://css-tricks.com/snippets/css/a-guide-to-flexbox/
    /// <summary>
    /// CSS: direction - Establishes the main-axis (ltr/rtl/inherit)
    /// </summary>
    public Direction Direction = Direction.Ltr;

    /// <summary>
    /// CSS: flex-direction - Establishes the main-axis (row/column/row-reverse/column-reverse)
    /// </summary>
    public FlexDirection FlexDirection = FlexDirection.Row;

    /// <summary>
    /// CSS: justify-content - Defines alignment along the main axis
    /// </summary>
    public Justify JustifyContent = Justify.FlexStart;

    /// <summary>
    /// CSS: align-items - Defines default alignment for all children along the cross axis
    /// </summary>
    public Align AlignItems = Align.Stretch;

    /// <summary>
    /// CSS: align-self - Allows a child to override the default cross-axis alignment
    /// </summary>
    public Align AlignSelf = Align.Auto;

    /// <summary>
    /// CSS: align-content - Aligns flex container's lines when there is extra space in the cross-axis
    /// </summary>
    public Align AlignContent = Align.Stretch;

    /// <summary>
    /// CSS: position - Sets how an element is positioned (static/relative/absolute/fixed)
    /// </summary>
    public Position Position = Position.Static;

    /// <summary>
    /// CSS: flex-wrap - Controls whether flex items wrap onto multiple lines (nowrap/wrap/wrap-reverse)
    /// </summary>
    public Wrap FlexWrap = Wrap.NoWrap;

    /// <summary>
    /// CSS: overflow - Controls what happens to content that is too big to fit (visible/hidden/scroll)
    /// </summary>
    public Overflow Overflow = Overflow.Visible;

    /// <summary>
    /// CSS: display - Defines the display type of the element (flex/none/block)
    /// </summary>
    public Display Display = Display.Flex;

    /// <summary>
    /// CSS: box-sizing - Defines how width/height calculations include padding/border (content-box/border-box)
    /// </summary>
    public BoxSizing BoxSizing = BoxSizing.BorderBox;

    /// <summary>
    /// CSS: flex - Shorthand for flex-grow, flex-shrink, and flex-basis combined
    /// </summary>
    public float? Flex;

    /// <summary>
    /// CSS: flex-grow - Defines the ability for a flex item to grow if necessary
    /// </summary>
    public float? FlexGrow;

    /// <summary>
    /// CSS: flex-shrink - Defines the ability for a flex item to shrink if necessary
    /// </summary>
    public float? FlexShrink;

    /// <summary>
    /// CSS: flex-basis - Defines the default size of an element before remaining space is distributed
    /// </summary>
    public MeasurementFlexBasis FlexBasis = MeasurementFlexBasis.Undefined;

    /// <summary>
    /// CSS: left - Specifies the left position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Left = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: top - Specifies the top position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Top = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: right - Specifies the right position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Right = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: bottom - Specifies the bottom position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Bottom = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: margin-top - Sets the top margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginTop = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: margin-bottom - Sets the bottom margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginBottom = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: margin-left - Sets the left margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginLeft = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: margin-right - Sets the right margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginRight = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: padding-top - Sets the top padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingTop = MeasurementPadding.Undefined;

    /// <summary>
    /// CSS: padding-bottom - Sets the bottom padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingBottom = MeasurementPadding.Undefined;

    /// <summary>
    /// CSS: padding-left - Sets the left padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingLeft = MeasurementPadding.Undefined;

    /// <summary>
    /// CSS: padding-right - Sets the right padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingRight = MeasurementPadding.Undefined;

    /// <summary>
    /// CSS: border-top-width - Sets the width of the top border
    /// </summary>
    public Pixels? BorderTop;

    /// <summary>
    /// CSS: border-bottom-width - Sets the width of the bottom border
    /// </summary>
    public Pixels? BorderBottom;

    /// <summary>
    /// CSS: border-left-width - Sets the width of the left border
    /// </summary>
    public Pixels? BorderLeft;

    /// <summary>
    /// CSS: border-right-width - Sets the width of the right border
    /// </summary>
    public Pixels? BorderRight;

    /// <summary>
    /// CSS: column-gap - Sets the gap between columns in a flex container
    /// </summary>
    public MeasurementGap GapColumn = MeasurementGap.Undefined;

    /// <summary>
    /// CSS: row-gap - Sets the gap between rows in a flex container
    /// </summary>
    public MeasurementGap GapRow = MeasurementGap.Undefined;

    /// <summary>
    /// CSS: width - Sets the width of the element
    /// </summary>
    public MeasurementWidthHeight Width = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: height - Sets the height of the element
    /// </summary>
    public MeasurementWidthHeight Height = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: min-width - Sets the minimum width of the element
    /// </summary>
    public MeasurementWidthHeight MinWidth = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: min-height - Sets the minimum height of the element
    /// </summary>
    public MeasurementWidthHeight MinHeight = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: max-width - Sets the maximum width of the element
    /// </summary>
    public MeasurementWidthHeight MaxWidth = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: max-height - Sets the maximum height of the element
    /// </summary>
    public MeasurementWidthHeight MaxHeight = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: aspect-ratio - Sets the preferred aspect ratio for the element (width / height)
    /// </summary>
    public Pixels? AspectRatio;

    public Color? BorderColor = null;
    public Color? BackgroundColor = null;
    public CornerRadius BorderTopLeftRadius = 0f;
    public CornerRadius BorderTopRightRadius = 0f;
    public CornerRadius BorderBottomLeftRadius = 0f;
    public CornerRadius BorderBottomRightRadius = 0f;

    public bool PointerEvents = true;

    /// <summary>
    /// CSS: z-index - Controls the paint / hit-test stacking order of the element.
    /// Higher values are hit-tested (and rendered) above lower values, regardless of
    /// tree order. A node's effective z for ordering is the max z-index in its subtree
    /// (so a high-z descendant like a dropdown popup wins over lower-z siblings of an
    /// ancestor). Default 0.
    /// </summary>
    public int ZIndex = 0;

    public bool Equals(Styles other)
    {
        return Visibility == other.Visibility && Opacity.Equals(other.Opacity) && Direction == other.Direction && FlexDirection == other.FlexDirection && JustifyContent == other.JustifyContent && AlignItems == other.AlignItems && AlignSelf == other.AlignSelf && AlignContent == other.AlignContent && Position == other.Position && FlexWrap == other.FlexWrap && Overflow == other.Overflow && Display == other.Display && BoxSizing == other.BoxSizing && Nullable.Equals(Flex, other.Flex) && Nullable.Equals(FlexGrow, other.FlexGrow) && Nullable.Equals(FlexShrink, other.FlexShrink) && FlexBasis.Equals(other.FlexBasis) && Left.Equals(other.Left) && Top.Equals(other.Top) && Right.Equals(other.Right) && Bottom.Equals(other.Bottom) && MarginTop.Equals(other.MarginTop) && MarginBottom.Equals(other.MarginBottom) && MarginLeft.Equals(other.MarginLeft) && MarginRight.Equals(other.MarginRight) && PaddingTop.Equals(other.PaddingTop) && PaddingBottom.Equals(other.PaddingBottom) && PaddingLeft.Equals(other.PaddingLeft) && PaddingRight.Equals(other.PaddingRight) && Nullable.Equals(BorderTop, other.BorderTop) && Nullable.Equals(BorderBottom, other.BorderBottom) && Nullable.Equals(BorderLeft, other.BorderLeft) && Nullable.Equals(BorderRight, other.BorderRight) && GapColumn.Equals(other.GapColumn) && GapRow.Equals(other.GapRow) && Width.Equals(other.Width) && Height.Equals(other.Height) && MinWidth.Equals(other.MinWidth) && MinHeight.Equals(other.MinHeight) && MaxWidth.Equals(other.MaxWidth) && MaxHeight.Equals(other.MaxHeight) && Nullable.Equals(AspectRatio, other.AspectRatio) && Nullable.Equals(BorderColor, other.BorderColor) && Nullable.Equals(BackgroundColor, other.BackgroundColor) && BorderTopLeftRadius.Equals(other.BorderTopLeftRadius) && BorderTopRightRadius.Equals(other.BorderTopRightRadius) && BorderBottomLeftRadius.Equals(other.BorderBottomLeftRadius) && BorderBottomRightRadius.Equals(other.BorderBottomRightRadius) && PointerEvents == other.PointerEvents && ZIndex == other.ZIndex;
    }

    public override bool Equals(object? obj)
    {
        return obj is Styles other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add((int)Visibility);
        hashCode.Add(Opacity);
        hashCode.Add((int)Direction);
        hashCode.Add((int)FlexDirection);
        hashCode.Add((int)JustifyContent);
        hashCode.Add((int)AlignItems);
        hashCode.Add((int)AlignSelf);
        hashCode.Add((int)AlignContent);
        hashCode.Add((int)Position);
        hashCode.Add((int)FlexWrap);
        hashCode.Add((int)Overflow);
        hashCode.Add((int)Display);
        hashCode.Add((int)BoxSizing);
        hashCode.Add(Flex);
        hashCode.Add(FlexGrow);
        hashCode.Add(FlexShrink);
        hashCode.Add(FlexBasis);
        hashCode.Add(Left);
        hashCode.Add(Top);
        hashCode.Add(Right);
        hashCode.Add(Bottom);
        hashCode.Add(MarginTop);
        hashCode.Add(MarginBottom);
        hashCode.Add(MarginLeft);
        hashCode.Add(MarginRight);
        hashCode.Add(PaddingTop);
        hashCode.Add(PaddingBottom);
        hashCode.Add(PaddingLeft);
        hashCode.Add(PaddingRight);
        hashCode.Add(BorderTop);
        hashCode.Add(BorderBottom);
        hashCode.Add(BorderLeft);
        hashCode.Add(BorderRight);
        hashCode.Add(GapColumn);
        hashCode.Add(GapRow);
        hashCode.Add(Width);
        hashCode.Add(Height);
        hashCode.Add(MinWidth);
        hashCode.Add(MinHeight);
        hashCode.Add(MaxWidth);
        hashCode.Add(MaxHeight);
        hashCode.Add(AspectRatio);
        hashCode.Add(BorderColor);
        hashCode.Add(BackgroundColor);
        hashCode.Add(BorderTopLeftRadius);
        hashCode.Add(BorderTopRightRadius);
        hashCode.Add(BorderBottomLeftRadius);
        hashCode.Add(BorderBottomRightRadius);
        hashCode.Add(PointerEvents);
        hashCode.Add(ZIndex);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(Styles left, Styles right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Styles left, Styles right)
    {
        return !left.Equals(right);
    }
}