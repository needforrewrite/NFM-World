using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Reactor;

/// <summary>
/// A Yoga style. Every field is nullable and null means "not set", so one style can be
/// merged onto another (the styled components, hover states) without its unset fields
/// overwriting anything. The web defaults live in <see cref="Resolved"/>, applied in one
/// place when the style is pushed into Yoga.
/// </summary>
public struct Styles() : IEquatable<Styles>
{
    /// <summary>Defaults for every field this style leaves null. Web defaults, matching the Luau UI.</summary>
    public readonly Styles Resolved => new()
    {
        Visibility = Visibility ?? Reactor.Visibility.Visible,
        Opacity = Opacity ?? 1.0f,
        Direction = Direction ?? Reactor.Direction.Ltr,
        FlexDirection = FlexDirection ?? Reactor.FlexDirection.Row,
        JustifyContent = JustifyContent ?? Justify.FlexStart,
        AlignItems = AlignItems ?? Align.Stretch,
        AlignSelf = AlignSelf ?? Align.Auto,
        AlignContent = AlignContent ?? Align.Stretch,
        Position = Position ?? Reactor.Position.Static,
        FlexWrap = FlexWrap ?? Wrap.NoWrap,
        Overflow = Overflow ?? Reactor.Overflow.Visible,
        Display = Display ?? Reactor.Display.Flex,
        BoxSizing = BoxSizing ?? Reactor.BoxSizing.BorderBox,
        PointerEvents = PointerEvents ?? true,
        Flex = Flex,
        FlexGrow = FlexGrow,
        FlexShrink = FlexShrink,
        FlexBasis = FlexBasis,
        Left = Left,
        Top = Top,
        Right = Right,
        Bottom = Bottom,
        MarginTop = MarginTop,
        MarginBottom = MarginBottom,
        MarginLeft = MarginLeft,
        MarginRight = MarginRight,
        PaddingTop = PaddingTop,
        PaddingBottom = PaddingBottom,
        PaddingLeft = PaddingLeft,
        PaddingRight = PaddingRight,
        BorderTop = BorderTop,
        BorderBottom = BorderBottom,
        BorderLeft = BorderLeft,
        BorderRight = BorderRight,
        GapColumn = GapColumn,
        GapRow = GapRow,
        Width = Width,
        Height = Height,
        MinWidth = MinWidth,
        MinHeight = MinHeight,
        MaxWidth = MaxWidth,
        MaxHeight = MaxHeight,
        AspectRatio = AspectRatio,
        BorderColor = BorderColor,
        BackgroundColor = BackgroundColor,
        BorderTopLeftRadius = BorderTopLeftRadius,
        BorderTopRightRadius = BorderTopRightRadius,
        BorderBottomLeftRadius = BorderBottomLeftRadius,
        BorderBottomRightRadius = BorderBottomRightRadius,
        ZIndex = ZIndex ?? 0,
    };

    /// <summary>This style with <paramref name="overlay"/>'s set fields applied on top.</summary>
    public readonly Styles Over(Styles overlay) => new()
    {
        Visibility = overlay.Visibility ?? Visibility,
        Opacity = overlay.Opacity ?? Opacity,
        Direction = overlay.Direction ?? Direction,
        FlexDirection = overlay.FlexDirection ?? FlexDirection,
        JustifyContent = overlay.JustifyContent ?? JustifyContent,
        AlignItems = overlay.AlignItems ?? AlignItems,
        AlignSelf = overlay.AlignSelf ?? AlignSelf,
        AlignContent = overlay.AlignContent ?? AlignContent,
        Position = overlay.Position ?? Position,
        FlexWrap = overlay.FlexWrap ?? FlexWrap,
        Overflow = overlay.Overflow ?? Overflow,
        Display = overlay.Display ?? Display,
        BoxSizing = overlay.BoxSizing ?? BoxSizing,
        Flex = overlay.Flex ?? Flex,
        FlexGrow = overlay.FlexGrow ?? FlexGrow,
        FlexShrink = overlay.FlexShrink ?? FlexShrink,
        FlexBasis = overlay.FlexBasis.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.FlexBasis : FlexBasis,
        Left = overlay.Left.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.Left : Left,
        Top = overlay.Top.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.Top : Top,
        Right = overlay.Right.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.Right : Right,
        Bottom = overlay.Bottom.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.Bottom : Bottom,
        MarginTop = overlay.MarginTop.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MarginTop : MarginTop,
        MarginBottom = overlay.MarginBottom.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MarginBottom : MarginBottom,
        MarginLeft = overlay.MarginLeft.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MarginLeft : MarginLeft,
        MarginRight = overlay.MarginRight.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MarginRight : MarginRight,
        PaddingTop = overlay.PaddingTop.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.PaddingTop : PaddingTop,
        PaddingBottom = overlay.PaddingBottom.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.PaddingBottom : PaddingBottom,
        PaddingLeft = overlay.PaddingLeft.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.PaddingLeft : PaddingLeft,
        PaddingRight = overlay.PaddingRight.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.PaddingRight : PaddingRight,
        BorderTop = overlay.BorderTop ?? BorderTop,
        BorderBottom = overlay.BorderBottom ?? BorderBottom,
        BorderLeft = overlay.BorderLeft ?? BorderLeft,
        BorderRight = overlay.BorderRight ?? BorderRight,
        GapColumn = overlay.GapColumn.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.GapColumn : GapColumn,
        GapRow = overlay.GapRow.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.GapRow : GapRow,
        Width = overlay.Width.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.Width : Width,
        Height = overlay.Height.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.Height : Height,
        MinWidth = overlay.MinWidth.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MinWidth : MinWidth,
        MinHeight = overlay.MinHeight.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MinHeight : MinHeight,
        MaxWidth = overlay.MaxWidth.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MaxWidth : MaxWidth,
        MaxHeight = overlay.MaxHeight.Unit != Microsoft.UI.Reactor.Layout.YogaUnit.Undefined ? overlay.MaxHeight : MaxHeight,
        AspectRatio = overlay.AspectRatio ?? AspectRatio,
        BorderColor = overlay.BorderColor ?? BorderColor,
        BackgroundColor = overlay.BackgroundColor ?? BackgroundColor,
        BorderTopLeftRadius = overlay.BorderTopLeftRadius ?? BorderTopLeftRadius,
        BorderTopRightRadius = overlay.BorderTopRightRadius ?? BorderTopRightRadius,
        BorderBottomLeftRadius = overlay.BorderBottomLeftRadius ?? BorderBottomLeftRadius,
        BorderBottomRightRadius = overlay.BorderBottomRightRadius ?? BorderBottomRightRadius,
        PointerEvents = overlay.PointerEvents ?? PointerEvents,
        ZIndex = overlay.ZIndex ?? ZIndex,
    };

    /// <summary>
    /// CSS: visibility - Controls whether the element is visible (visible/hidden/collapsed)
    /// </summary>
    public Visibility? Visibility;

    /// <summary>
    /// CSS: opacity - Sets the transparency level (0.0 = fully transparent, 1.0 = fully opaque)
    /// </summary>
    public float? Opacity;

    // https://css-tricks.com/snippets/css/a-guide-to-flexbox/
    /// <summary>
    /// CSS: direction - Establishes the main-axis (ltr/rtl/inherit)
    /// </summary>
    public Direction? Direction;

    /// <summary>
    /// CSS: flex-direction - Establishes the main-axis (row/column/row-reverse/column-reverse)
    /// </summary>
    public FlexDirection? FlexDirection;

    /// <summary>
    /// CSS: justify-content - Defines alignment along the main axis
    /// </summary>
    public Justify? JustifyContent;

    /// <summary>
    /// CSS: align-items - Defines default alignment for all children along the cross axis
    /// </summary>
    public Align? AlignItems;

    /// <summary>
    /// CSS: align-self - Allows a child to override the default cross-axis alignment
    /// </summary>
    public Align? AlignSelf;

    /// <summary>
    /// CSS: align-content - Aligns flex container's lines when there is extra space in the cross-axis
    /// </summary>
    public Align? AlignContent;

    /// <summary>
    /// CSS: position - Sets how an element is positioned (static/relative/absolute/fixed)
    /// </summary>
    public Position? Position;

    /// <summary>
    /// CSS: flex-wrap - Controls whether flex items wrap onto multiple lines (nowrap/wrap/wrap-reverse)
    /// </summary>
    public Wrap? FlexWrap;

    /// <summary>
    /// CSS: overflow - Controls what happens to content that is too big to fit (visible/hidden/scroll)
    /// </summary>
    public Overflow? Overflow;

    /// <summary>
    /// CSS: display - Defines the display type of the element (flex/none/block)
    /// </summary>
    public Display? Display;

    /// <summary>
    /// CSS: box-sizing - Defines how width/height calculations include padding/border (content-box/border-box)
    /// </summary>
    public BoxSizing? BoxSizing;

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
    public CornerRadius? BorderTopLeftRadius;
    public CornerRadius? BorderTopRightRadius;
    public CornerRadius? BorderBottomLeftRadius;
    public CornerRadius? BorderBottomRightRadius;

    /// <summary>
    /// CSS: border-radius (shorthand) - Sets all four corners at once, and reads back
    /// the top-left. The individual corners stay authoritative when set explicitly.
    /// </summary>
    public CornerRadius? BorderRadius
    {
        readonly get => BorderTopLeftRadius;
        set => BorderTopLeftRadius = BorderTopRightRadius = BorderBottomLeftRadius = BorderBottomRightRadius = value;
    }

    public bool? PointerEvents;

    /// <summary>
    /// CSS: z-index - Controls the paint / hit-test stacking order of the element.
    /// Higher values are hit-tested (and rendered) above lower values, regardless of
    /// tree order. A node's effective z for ordering is the max z-index in its subtree
    /// (so a high-z descendant like a dropdown popup wins over lower-z siblings of an
    /// ancestor). Default 0.
    /// </summary>
    public int? ZIndex;

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