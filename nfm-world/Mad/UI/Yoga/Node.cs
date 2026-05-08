using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Yoga;

namespace NFMWorld.UI.Yoga;

// ReSharper disable InconsistentNaming

/// <summary>
/// Represents a single node in the Yoga layout system.
/// </summary>
[DebuggerDisplay("{DebugToString()}")]
public class Node : IDisposable, INamed
{
    internal static readonly YGConfigPtr Config;

    internal YGNodePtr NodeInternal = new(Config);

    internal readonly string __INTERNAL_CtorCallerFilePath = "";
    internal readonly int __INTERNAL_CtorCallerLineNumber = 0;
    internal readonly string __INTERNAL_CtorCallerMemberName = "";

    internal static readonly List<Node> __INTERNAL_YogaRootsThisFrame = new();

#if DEBUG
    [MethodImpl(MethodImplOptions.NoInlining)]
#endif
    public Node()
    {
#if DEBUG
        var stackTrace = new StackTrace(1, true);
        // skip inherited constructors
        var stackFrame = stackTrace.GetFrames()
            .FirstOrDefault(e => e.GetMethod()?.DeclaringType?.IsAssignableTo(typeof(Node)) != true);
        __INTERNAL_CtorCallerFilePath = stackFrame?.GetFileName() ?? "";
        __INTERNAL_CtorCallerLineNumber = stackFrame?.GetFileLineNumber() ?? 0;
        __INTERNAL_CtorCallerMemberName = stackFrame?.GetMethod()?.Name ?? "";
#endif
    }

    public string? Name { get; set; }

    public string DebugToString()
    {
        return $"Node(Name={Name}, LayoutX={LayoutX}, LayoutY={LayoutY}, LayoutWidth={LayoutWidth}, LayoutHeight={LayoutHeight})";
    }

    #region Layout

    // https://www.w3schools.com/css/css_boxmodel.asp
    private protected Vector2 _root;
    public Vector2 LayoutMarginPosition => _root + new Vector2(LayoutX, LayoutY);
    public Vector2 LayoutMarginSize => new(LayoutWidth, LayoutHeight);
    public Vector2 LayoutBorderPosition => _root + new Vector2(LayoutX + LayoutMarginLeft, LayoutY + LayoutMarginTop);
    public Vector2 LayoutBorderSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom));
    public Vector2 LayoutPaddingPosition => _root + new Vector2(LayoutX + LayoutMarginLeft + LayoutBorderLeft, LayoutY + LayoutMarginTop + LayoutBorderTop);
    public Vector2 LayoutPaddingSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight + LayoutBorderLeft + LayoutBorderRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom + LayoutBorderTop + LayoutBorderBottom));
    public Vector2 LayoutContentPosition => _root + new Vector2(LayoutX + LayoutMarginLeft + LayoutBorderLeft + LayoutPaddingLeft, LayoutY + LayoutMarginTop + LayoutBorderTop + LayoutPaddingTop);
    public Vector2 LayoutContentSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight + LayoutBorderLeft + LayoutBorderRight + LayoutPaddingLeft + LayoutPaddingRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom + LayoutBorderTop + LayoutBorderBottom + LayoutPaddingTop + LayoutPaddingBottom));

    public Vector2 LayoutMargin => new(LayoutMarginLeft + LayoutMarginRight, LayoutMarginTop + LayoutMarginBottom);
    public Vector2 LayoutPadding => new(LayoutPaddingLeft + LayoutPaddingRight, LayoutPaddingTop + LayoutPaddingBottom);
    public Vector2 LayoutBorder => new(LayoutBorderLeft + LayoutBorderRight, LayoutBorderTop + LayoutBorderBottom);

    public float LayoutWidth => NodeInternal.LayoutWidth;
    public float LayoutHeight => NodeInternal.LayoutHeight;
    public float LayoutX => NodeInternal.LayoutX;
    public float LayoutY => NodeInternal.LayoutY;
    public YgDirection LayoutDirection => NodeInternal.LayoutDirection.ToNfmDirection();
    public bool HadOverflow => NodeInternal.HadOverflow;
    public float LayoutMarginTop => NodeInternal.LayoutMarginTop;
    public float LayoutMarginBottom => NodeInternal.LayoutMarginBottom;
    public float LayoutMarginLeft => NodeInternal.LayoutMarginLeft;
    public float LayoutMarginRight => NodeInternal.LayoutMarginRight;
    public float LayoutPaddingTop => NodeInternal.LayoutPaddingTop;
    public float LayoutPaddingBottom => NodeInternal.LayoutPaddingBottom;
    public float LayoutPaddingLeft => NodeInternal.LayoutPaddingLeft;
    public float LayoutPaddingRight => NodeInternal.LayoutPaddingRight;
    public float LayoutBorderTop => NodeInternal.LayoutBorderTop;
    public float LayoutBorderBottom => NodeInternal.LayoutBorderBottom;
    public float LayoutBorderLeft => NodeInternal.LayoutBorderLeft;
    public float LayoutBorderRight => NodeInternal.LayoutBorderRight;

    public bool HasNewLayout
    {
        get => NodeInternal.HasNewLayout;
        set => NodeInternal.HasNewLayout = value;
    }

    public bool IsDirty
    {
        get => NodeInternal.IsDirty;
        set => NodeInternal.IsDirty = value;
    }

    public bool IsReferenceBaseline
    {
        set => NodeInternal.IsReferenceBaseline = value;
        get => NodeInternal.IsReferenceBaseline;
    }

    public YgNodeType NodeType
    {
        get => NodeInternal.NodeType.ToNfmNodeType();
        set => NodeInternal.NodeType = value.ToYogaNodeType();
    }

    public bool AlwaysFormsContainingBlock
    {
        get => NodeInternal.AlwaysFormsContainingBlock;
        set => NodeInternal.AlwaysFormsContainingBlock = value;
    }

    #endregion

    #region Style

    /// <summary>
    /// CSS: visibility - Controls whether the element is visible (visible/hidden/collapsed)
    /// </summary>
    public Visibility Visibility { get; set; } = Visibility.Visible;

    /// <summary>
    /// CSS: opacity - Sets the transparency level (0.0 = fully transparent, 1.0 = fully opaque)
    /// </summary>
    public float Opacity { get; set; } = 1.0f;

    // https://css-tricks.com/snippets/css/a-guide-to-flexbox/
    /// <summary>
    /// CSS: direction - Establishes the main-axis (ltr/rtl/inherit)
    /// </summary>
    public YgDirection Direction
    {
        get => NodeInternal.Direction.ToNfmDirection();
        set => NodeInternal.Direction = value.ToYogaDirection();
    }

    /// <summary>
    /// CSS: flex-direction - Establishes the main-axis (row/column/row-reverse/column-reverse)
    /// </summary>
    public YgFlexDirection FlexDirection
    {
        get => NodeInternal.FlexDirection.ToNfmFlexDirection();
        set => NodeInternal.FlexDirection = value.ToYogaFlexDirection();
    }

    /// <summary>
    /// CSS: justify-content - Defines alignment along the main axis
    /// </summary>
    public YgJustify JustifyContent
    {
        get => NodeInternal.JustifyContent.ToNfmJustify();
        set => NodeInternal.JustifyContent = value.ToYogaJustify();
    }

    /// <summary>
    /// CSS: align-items - Defines default alignment for all children along the cross axis
    /// </summary>
    public YgAlign AlignItems
    {
        get => NodeInternal.AlignItems.ToNfmAlign();
        set => NodeInternal.AlignItems = value.ToYogaAlign();
    }

    /// <summary>
    /// CSS: align-self - Allows a child to override the default cross-axis alignment
    /// </summary>
    public YgAlign AlignSelf
    {
        get => NodeInternal.AlignSelf.ToNfmAlign();
        set => NodeInternal.AlignSelf = value.ToYogaAlign();
    }

    /// <summary>
    /// CSS: align-content - Aligns flex container's lines when there is extra space in the cross-axis
    /// </summary>
    public YgAlign AlignContent
    {
        get => NodeInternal.AlignContent.ToNfmAlign();
        set => NodeInternal.AlignContent = value.ToYogaAlign();
    }

    /// <summary>
    /// CSS: position - Sets how an element is positioned (static/relative/absolute/fixed)
    /// </summary>
    public YgPositionType Position
    {
        get => NodeInternal.PositionType.ToNfmPositionType();
        set => NodeInternal.PositionType = value.ToYogaPositionType();
    }

    /// <summary>
    /// CSS: flex-wrap - Controls whether flex items wrap onto multiple lines (nowrap/wrap/wrap-reverse)
    /// </summary>
    public YgWrap FlexWrap
    {
        get => NodeInternal.FlexWrap.ToNfmWrap();
        set => NodeInternal.FlexWrap = value.ToYogaWrap();
    }

    /// <summary>
    /// CSS: overflow - Controls what happens to content that is too big to fit (visible/hidden/scroll)
    /// </summary>
    public YgOverflow Overflow
    {
        get => NodeInternal.Overflow.ToNfmOverflow();
        set => NodeInternal.Overflow = value.ToYogaOverflow();
    }

    /// <summary>
    /// CSS: display - Defines the display type of the element (flex/none/block)
    /// </summary>
    public YgDisplay Display
    {
        get => NodeInternal.Display.ToNfmDisplay();
        set => NodeInternal.Display = value.ToYogaDisplay();
    }

    public sealed class PixelsConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                var trimmed = str.AsSpan().Trim();
                if (trimmed.EndsWith("px"))
                {
                    if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                    {
                        return pointValue;
                    }
                }
                else
                {
                    if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                    {
                        return pointValue;
                    }
                }

                throw new FormatException($"Cannot convert '{str}' to pixels.");
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    public class PixelsOrUndefinedConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                var trimmed = str.AsSpan().Trim();
                if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    return (float?)0;
                }
                if (trimmed.EndsWith("px"))
                {
                    if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                    {
                        return (float?)pointValue;
                    }
                }
                else
                {
                    if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                    {
                        return (float?)pointValue;
                    }
                }

                throw new FormatException($"Cannot convert '{str}' to pixels or undefined.");
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    /// <summary>
    /// CSS: flex - Shorthand for flex-grow, flex-shrink, and flex-basis combined
    /// </summary>
    public float? Flex
    {
        get => NodeInternal.Flex is var v && !float.IsNaN(v) ? v : null;
        set => NodeInternal.Flex = value ?? float.NaN;
    }

    /// <summary>
    /// CSS: flex-grow - Defines the ability for a flex item to grow if necessary
    /// </summary>
    public float? FlexGrow
    {
        get => NodeInternal.FlexGrow is var v && !float.IsNaN(v) ? v : null;
        set => NodeInternal.FlexGrow = value ?? float.NaN;
    }

    /// <summary>
    /// CSS: flex-shrink - Defines the ability for a flex item to shrink if necessary
    /// </summary>
    public float? FlexShrink
    {
        get => NodeInternal.FlexShrink is var v && !float.IsNaN(v) ? v : null;
        set => NodeInternal.FlexShrink = value ?? float.NaN;
    }

    public Action<Node> Ref
    {
        set => value(this);
    }

    [TypeConverter(typeof(MeasurementFlexBasisTypeConverter))]
    public struct MeasurementFlexBasis
    {
        public sealed class MeasurementFlexBasisTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.AsSpan().Trim();
                    if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        return Undefined;
                    }
                    if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    {
                        return Auto;
                    }
                    if (trimmed.Equals("max-content", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Equals("maxcontent", StringComparison.OrdinalIgnoreCase))
                    {
                        return MaxContent;
                    }
                    if (trimmed.Equals("stretch", StringComparison.OrdinalIgnoreCase))
                    {
                        return Stretch;
                    }
                    if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
                    {
                        if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                        {
                            return Percent(percentValue);
                        }
                    }
                    else if (trimmed.EndsWith("px"))
                    {
                        if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }
                    else
                    {
                        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }

                    throw new FormatException($"Cannot convert '{str}' to MeasurementFlexBasis. Expected 'auto', 'max-content', 'stretch', '<number>px', '<number>%', or '<number>'.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        internal YGValue InternalValue;
        public YgUnit Unit => InternalValue.unit.ToNfmUnit();
        public float Value => InternalValue.value;
        public float? PointValue => InternalValue.unit == YGUnit.YGUnitPoint ? InternalValue.value : null;
        public float? PercentValue => InternalValue.unit == YGUnit.YGUnitPercent ? InternalValue.value : null;

        public static implicit operator MeasurementFlexBasis(float value)
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
        public static implicit operator MeasurementFlexBasis(YGValue value)
        {
            return new MeasurementFlexBasis
            {
                InternalValue = value
            };
        }
        public static implicit operator YGValue(MeasurementFlexBasis value)
        {
            return value.InternalValue;
        }

        public static MeasurementFlexBasis Undefined = new()
        {
            InternalValue = new YGValue
            {
                unit = YGUnit.YGUnitUndefined
            }
        };

        public static MeasurementFlexBasis Auto =>
            new()
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitFitContent
                }
            };

        public static MeasurementFlexBasis MaxContent =>
            new()
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitMaxContent
                }
            };

        public static MeasurementFlexBasis Stretch =>
            new()
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitStretch
                }
            };

        public static MeasurementFlexBasis Percent(float value)
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPercent,
                    value = value
                }
            };
        }

        public static MeasurementFlexBasis Point(float value)
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }

        public static MeasurementFlexBasis FitContent =>
            new()
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitFitContent
                }
            };

        public MeasurementFlexBasis Scale(float scale)
        {
            if (InternalValue.unit == YGUnit.YGUnitPoint)
            {
                return Point(InternalValue.value * scale);
            }

            return this;
        }
    }

    /// <summary>
    /// CSS: flex-basis - Defines the default size of an element before remaining space is distributed
    /// </summary>
    public MeasurementFlexBasis FlexBasis
    {
        get;
        set
        {
            field = value;
            NodeInternal.FlexBasis = value.Scale(G.Scale);
        }
    } = MeasurementFlexBasis.Undefined;

    [TypeConverter(typeof(MeasurementMarginPositionTypeConverter))]
    public struct MeasurementMarginPosition
    {
        public sealed class MeasurementMarginPositionTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.AsSpan().Trim();
                    if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        return Undefined;
                    }
                    if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    {
                        return Auto;
                    }
                    if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
                    {
                        if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                        {
                            return Percent(percentValue);
                        }
                    }
                    else if (trimmed.EndsWith("px"))
                    {
                        if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }
                    else
                    {
                        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }

                    throw new FormatException($"Cannot convert '{str}' to MeasurementMarginPosition. Expected 'auto', '<number>px', '<number>%', or '<number>'.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        internal YGValue InternalValue;
        public YgUnit Unit => InternalValue.unit.ToNfmUnit();
        public float Value => InternalValue.value;
        public float? PointValue => InternalValue.unit == YGUnit.YGUnitPoint ? InternalValue.value : null;
        public float? PercentValue => InternalValue.unit == YGUnit.YGUnitPercent ? InternalValue.value : null;

        public static implicit operator MeasurementMarginPosition(float value)
        {
            return new MeasurementMarginPosition
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
        public static implicit operator MeasurementMarginPosition(YGValue value)
        {
            return new MeasurementMarginPosition
            {
                InternalValue = value
            };
        }
        public static implicit operator YGValue(MeasurementMarginPosition value)
        {
            return value.InternalValue;
        }

        public static MeasurementMarginPosition Auto =>
            new()
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitAuto
                }
            };

        public static MeasurementMarginPosition Undefined => new()
        {
            InternalValue = new YGValue
            {
                unit = YGUnit.YGUnitUndefined
            }
        };

        public static MeasurementMarginPosition Percent(float value)
        {
            return new MeasurementMarginPosition
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPercent,
                    value = value
                }
            };
        }
        public static MeasurementMarginPosition Point(float value)
        {
            return new MeasurementMarginPosition
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }

        public MeasurementMarginPosition Scale(float scale)
        {
            if (InternalValue.unit == YGUnit.YGUnitPoint)
            {
                return Point(InternalValue.value * scale);
            }

            return this;
        }
    }

    /// <summary>
    /// CSS: left - Specifies the left position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Left
    {
        get;
        set
        {
            field = value;
            NodeInternal.Left = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: top - Specifies the top position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Top
    {
        get;
        set
        {
            field = value;
            NodeInternal.Top = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: right - Specifies the right position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Right
    {
        get;
        set
        {
            field = value;
            NodeInternal.Right = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: bottom - Specifies the bottom position of a positioned element
    /// </summary>
    public MeasurementMarginPosition Bottom
    {
        get;
        set
        {
            field = value;
            NodeInternal.Bottom = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    [TypeConverter(typeof(MeasurementMultiMarginTypeConverter))]
    public struct MeasurementMultiMargin
    {
        public class MeasurementMultiMarginTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.AsSpan().Trim();
                    if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        return All(MeasurementMarginPosition.Undefined);
                    }
                    if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    {
                        return All(MeasurementMarginPosition.Auto);
                    }

                    var idx = 0;
                    var sides = new InlineArray4<MeasurementMarginPosition>();
                    foreach (var elementRange in trimmed.SplitAny(',', ' '))
                    {
                        var element = trimmed[elementRange];

                        if (element.EndsWith("%", StringComparison.OrdinalIgnoreCase))
                        {
                            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                            {
                                sides[idx] = MeasurementMarginPosition.Percent(percentValue);
                            }
                        }
                        else if (element.EndsWith("px"))
                        {
                            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                            {
                                sides[idx] = MeasurementMarginPosition.Point(pointValue);
                            }
                        }
                        else
                        {
                            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                            {
                                sides[idx] = MeasurementMarginPosition.Point(pointValue);
                            }
                        }

                        idx++;
                    }

                    if (idx == 1)
                    {
                        return All(sides[0]);
                    }

                    if (idx == 2)
                    {
                        return new MeasurementMultiMargin
                        {
                            Top = sides[0],
                            Bottom = sides[0],
                            Left = sides[1],
                            Right = sides[1]
                        };
                    }

                    if (idx == 4)
                    {
                        return new MeasurementMultiMargin
                        {
                            Top = sides[0],
                            Right = sides[1],
                            Bottom = sides[2],
                            Left = sides[3]
                        };
                    }

                    throw new FormatException($"Cannot convert '{str}' to MeasurementMultiMargin. Expected 'auto', '<number>px', '<number>%', or '<number>', as 1, 2 or 4 elements, in order top-right-bottom-left, separated by comma or space.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        public InlineArray4<MeasurementMarginPosition> Sides;
        public MeasurementMarginPosition Top
        {
            get => Sides[0];
            set => Sides[0] = value;
        }
        public MeasurementMarginPosition Bottom
        {
            get => Sides[1];
            set => Sides[1] = value;
        }
        public MeasurementMarginPosition Left
        {
            get => Sides[2];
            set => Sides[2] = value;
        }
        public MeasurementMarginPosition Right
        {
            get => Sides[3];
            set => Sides[3] = value;
        }

        public static MeasurementMultiMargin Auto => MeasurementMarginPosition.Auto;

        public static MeasurementMultiMargin Undefined => MeasurementMarginPosition.Undefined;

        public static MeasurementMultiMargin All(MeasurementMarginPosition value)
        {
            return new MeasurementMultiMargin
            {
                Top = value,
                Bottom = value,
                Left = value,
                Right = value
            };
        }

        public static implicit operator MeasurementMultiMargin(MeasurementMarginPosition value) => All(value);
    }

    /// <summary>
    /// CSS: margin - Shorthand for setting all margin values (top, right, bottom, left)
    /// </summary>
    public MeasurementMultiMargin Margin
    {
        set
        {
            MarginLeft = value.Left;
            MarginRight = value.Right;
            MarginTop = value.Top;
            MarginBottom = value.Bottom;
        }
    }

    /// <summary>
    /// CSS: margin-top - Sets the top margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginTop
    {
        get;
        set
        {
            field = value;
            NodeInternal.MarginTop = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: margin-bottom - Sets the bottom margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginBottom
    {
        get;
        set
        {
            field = value;
            NodeInternal.MarginBottom = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: margin-left - Sets the left margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginLeft
    {
        get;
        set
        {
            field = value;
            NodeInternal.MarginLeft = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    /// <summary>
    /// CSS: margin-right - Sets the right margin space outside the element
    /// </summary>
    public MeasurementMarginPosition MarginRight
    {
        get;
        set
        {
            field = value;
            NodeInternal.MarginRight = value.Scale(G.Scale);
        }
    } = MeasurementMarginPosition.Undefined;

    [TypeConverter(typeof(MeasurementPaddingTypeConverter))]
    public struct MeasurementPadding
    {
        public sealed class MeasurementPaddingTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.AsSpan().Trim();
                    if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        return Undefined;
                    }
                    if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
                    {
                        if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                        {
                            return Percent(percentValue);
                        }
                    }
                    else if (trimmed.EndsWith("px"))
                    {
                        if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }
                    else
                    {
                        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }

                    throw new FormatException($"Cannot convert '{str}' to MeasurementPadding. Expected '<number>px', '<number>%', or '<number>'.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        internal YGValue InternalValue;
        public YgUnit Unit => InternalValue.unit.ToNfmUnit();
        public float Value => InternalValue.value;
        public float? PointValue => InternalValue.unit == YGUnit.YGUnitPoint ? InternalValue.value : null;
        public float? PercentValue => InternalValue.unit == YGUnit.YGUnitPercent ? InternalValue.value : null;

        public static implicit operator MeasurementPadding(float value)
        {
            return new MeasurementPadding
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
        public static implicit operator MeasurementPadding(YGValue value)
        {
            return new MeasurementPadding
            {
                InternalValue = value
            };
        }
        public static implicit operator YGValue(MeasurementPadding value)
        {
            return value.InternalValue;
        }

        public static MeasurementPadding Undefined => new()
        {
            InternalValue = new YGValue
            {
                unit = YGUnit.YGUnitUndefined
            }
        };

        public static MeasurementPadding Percent(float value)
        {
            return new MeasurementPadding
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPercent,
                    value = value
                }
            };
        }
        public static MeasurementPadding Point(float value)
        {
            return new MeasurementPadding
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }

        public MeasurementPadding Scale(float scale)
        {
            if (InternalValue.unit == YGUnit.YGUnitPoint)
            {
                return Point(InternalValue.value * scale);
            }

            return this;
        }
    }

    [TypeConverter(typeof(MeasurementMultiPaddingTypeConverter))]
    public struct MeasurementMultiPadding
    {
        public class MeasurementMultiPaddingTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.AsSpan().Trim();
                    if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        return All(MeasurementPadding.Undefined);
                    }

                    var idx = 0;
                    var sides = new InlineArray4<MeasurementPadding>();
                    foreach (var elementRange in trimmed.SplitAny(',', ' '))
                    {
                        var element = trimmed[elementRange];

                        if (element.EndsWith("%", StringComparison.OrdinalIgnoreCase))
                        {
                            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                            {
                                sides[idx] = MeasurementPadding.Percent(percentValue);
                            }
                        }
                        else if (element.EndsWith("px"))
                        {
                            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                            {
                                sides[idx] = MeasurementPadding.Point(pointValue);
                            }
                        }
                        else
                        {
                            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                            {
                                sides[idx] = MeasurementPadding.Point(pointValue);
                            }
                        }

                        idx++;
                    }

                    if (idx == 1)
                    {
                        return All(sides[0]);
                    }

                    if (idx == 2)
                    {
                        return new MeasurementMultiPadding
                        {
                            Top = sides[0],
                            Bottom = sides[0],
                            Left = sides[1],
                            Right = sides[1]
                        };
                    }

                    if (idx == 4)
                    {
                        return new MeasurementMultiPadding
                        {
                            Top = sides[0],
                            Right = sides[1],
                            Bottom = sides[2],
                            Left = sides[3]
                        };
                    }

                    throw new FormatException($"Cannot convert '{str}' to MeasurementMultiMargin. Expected '<number>px', '<number>%', or '<number>', as 1, 2 or 4 elements, in order top-right-bottom-left, separated by comma or space.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        public InlineArray4<MeasurementPadding> Sides;
        public MeasurementPadding Top
        {
            get => Sides[0];
            set => Sides[0] = value;
        }
        public MeasurementPadding Bottom
        {
            get => Sides[1];
            set => Sides[1] = value;
        }
        public MeasurementPadding Left
        {
            get => Sides[2];
            set => Sides[2] = value;
        }
        public MeasurementPadding Right
        {
            get => Sides[3];
            set => Sides[3] = value;
        }

        public static MeasurementMultiPadding Undefined => MeasurementPadding.Undefined;

        public static MeasurementMultiPadding All(MeasurementPadding value)
        {
            return new MeasurementMultiPadding
            {
                Top = value,
                Bottom = value,
                Left = value,
                Right = value
            };
        }

        public static implicit operator MeasurementMultiPadding(MeasurementPadding value) => All(value);
    }

    /// <summary>
    /// CSS: padding - Shorthand for setting all padding values (top, right, bottom, left)
    /// </summary>
    public MeasurementMultiPadding Padding
    {
        set
        {
            PaddingLeft = value.Left;
            PaddingRight = value.Right;
            PaddingTop = value.Top;
            PaddingBottom = value.Bottom;
        }
    }

    /// <summary>
    /// CSS: padding-top - Sets the top padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingTop
    {
        get;
        set
        {
            field = value;
            NodeInternal.PaddingTop = value.Scale(G.Scale);
        }
    } = MeasurementPadding.Undefined;

    /// <summary>
    /// CSS: padding-bottom - Sets the bottom padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingBottom
    {
        get;
        set
        {
            field = value;
            NodeInternal.PaddingBottom = value.Scale(G.Scale);
        }
    } = MeasurementPadding.Undefined;

    /// <summary>
    /// CSS: padding-left - Sets the left padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingLeft
    {
        get;
        set
        {
            field = value;
            NodeInternal.PaddingLeft = value.Scale(G.Scale);
        }
    } = MeasurementPadding.Undefined;

    /// <summary>
    /// CSS: padding-right - Sets the right padding space inside the element
    /// </summary>
    public MeasurementPadding PaddingRight
    {
        get;
        set
        {
            field = value;
            NodeInternal.PaddingRight = value.Scale(G.Scale);
        }
    } = MeasurementPadding.Undefined;

    [TypeConverter(typeof(MeasurementMultiBorderTypeConverter))]
    public struct MeasurementMultiBorder
    {
        public class MeasurementMultiBorderTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.AsSpan().Trim();
                    if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        return Undefined;
                    }

                    var idx = 0;
                    var sides = new InlineArray4<float>();
                    foreach (var elementRange in trimmed.SplitAny(',', ' '))
                    {
                        var element = trimmed[elementRange];

                        if (element.EndsWith("px"))
                        {
                            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                            {
                                sides[idx] = pointValue;
                            }
                        }
                        else
                        {
                            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                            {
                                sides[idx] = pointValue;
                            }
                        }

                        idx++;
                    }

                    if (idx == 1)
                    {
                        return All(sides[0]);
                    }

                    if (idx == 2)
                    {
                        return new MeasurementMultiBorder
                        {
                            Top = sides[0],
                            Bottom = sides[0],
                            Left = sides[1],
                            Right = sides[1]
                        };
                    }

                    if (idx == 4)
                    {
                        return new MeasurementMultiBorder
                        {
                            Top = sides[0],
                            Right = sides[1],
                            Bottom = sides[2],
                            Left = sides[3]
                        };
                    }

                    throw new FormatException($"Cannot convert '{str}' to MeasurementMultiMargin. Expected '<number>px' or '<number>', as 1, 2 or 4 elements, in order top-right-bottom-left, separated by comma or space.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        public InlineArray4<float?> Sides;
        public float? Top
        {
            get => Sides[0];
            set => Sides[0] = value;
        }
        public float? Bottom
        {
            get => Sides[1];
            set => Sides[1] = value;
        }
        public float? Left
        {
            get => Sides[2];
            set => Sides[2] = value;
        }
        public float? Right
        {
            get => Sides[3];
            set => Sides[3] = value;
        }

        public static MeasurementMultiBorder Undefined => All(null);

        public static MeasurementMultiBorder All(float? value)
        {
            return new MeasurementMultiBorder
            {
                Top = value,
                Bottom = value,
                Left = value,
                Right = value
            };
        }

        public static implicit operator MeasurementMultiBorder(float? value) => All(value);
    }

    /// <summary>
    /// CSS: border - Shorthand for setting all border widths
    /// </summary>
    public MeasurementMultiBorder Border
    {
        set
        {
            BorderLeft = value.Left;
            BorderRight = value.Right;
            BorderTop = value.Top;
            BorderBottom = value.Bottom;
        }
    }

    /// <summary>
    /// CSS: border-top-width - Sets the width of the top border
    /// </summary>
    [TypeConverter(typeof(PixelsOrUndefinedConverter))]
    public float? BorderTop
    {
        get;
        set
        {
            field = value;
            NodeInternal.BorderTop = (value * G.Scale) ?? YG.YGUndefined;
        }
    }

    /// <summary>
    /// CSS: border-bottom-width - Sets the width of the bottom border
    /// </summary>
    [TypeConverter(typeof(PixelsOrUndefinedConverter))]
    public float? BorderBottom
    {
        get;
        set
        {
            field = value;
            NodeInternal.BorderBottom = (value * G.Scale) ?? YG.YGUndefined;
        }
    }

    /// <summary>
    /// CSS: border-left-width - Sets the width of the left border
    /// </summary>
    [TypeConverter(typeof(PixelsOrUndefinedConverter))]
    public float? BorderLeft
    {
        get;
        set
        {
            field = value;
            NodeInternal.BorderLeft = (value * G.Scale) ?? YG.YGUndefined;
        }
    }

    /// <summary>
    /// CSS: border-right-width - Sets the width of the right border
    /// </summary>
    [TypeConverter(typeof(PixelsOrUndefinedConverter))]
    public float? BorderRight
    {
        get;
        set
        {
            field = value;
            NodeInternal.BorderRight = (value * G.Scale) ?? YG.YGUndefined;
        }
    }

    [TypeConverter(typeof(MeasurementGapTypeConverter))]
    public struct MeasurementGap
    {
        public class MeasurementGapTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.AsSpan().Trim();
                    if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        return Undefined;
                    }
                    if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
                    {
                        if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                        {
                            return Percent(percentValue);
                        }
                    }
                    else if (trimmed.EndsWith("px"))
                    {
                        if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }
                    else
                    {
                        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                        {
                            return Point(pointValue);
                        }
                    }

                    throw new FormatException($"Cannot convert '{str}' to MeasurementGap. Expected '<number>px', '<number>%', or '<number>'.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        internal YGValue InternalValue;
        public YgUnit Unit => InternalValue.unit.ToNfmUnit();
        public float Value => InternalValue.value;
        public float? PointValue => InternalValue.unit == YGUnit.YGUnitPoint ? InternalValue.value : null;
        public float? PercentValue => InternalValue.unit == YGUnit.YGUnitPercent ? InternalValue.value : null;

        public static implicit operator MeasurementGap(float value)
        {
            return new MeasurementGap
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
        public static implicit operator MeasurementGap(YGValue value)
        {
            return new MeasurementGap
            {
                InternalValue = value
            };
        }
        public static implicit operator YGValue(MeasurementGap value)
        {
            return value.InternalValue;
        }

        public static MeasurementGap Undefined => new()
        {
            InternalValue = new YGValue
            {
                unit = YGUnit.YGUnitUndefined
            }
        };

        public static MeasurementGap Percent(float value)
        {
            return new MeasurementGap
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPercent,
                    value = value
                }
            };
        }
        public static MeasurementGap Point(float value)
        {
            return new MeasurementGap
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }

        public MeasurementGap Scale(float scale)
        {
            if (InternalValue.unit == YGUnit.YGUnitPoint)
            {
                return Point(InternalValue.value * scale);
            }

            return this;
        }
    }

    /// <summary>
    /// CSS: gap - Shorthand for setting row-gap and column-gap
    /// </summary>
    public MeasurementGap Gap
    {
        set
        {
            GapColumn = value;
            GapRow = value;
        }
    }

    /// <summary>
    /// CSS: column-gap - Sets the gap between columns in a flex container
    /// </summary>
    public MeasurementGap GapColumn
    {
        get;
        set
        {
            field = value;
            NodeInternal.GapColumn = value;
        }
    } = MeasurementGap.Undefined;

    /// <summary>
    /// CSS: row-gap - Sets the gap between rows in a flex container
    /// </summary>
    public MeasurementGap GapRow
    {
        get;
        set
        {
            field = value;
            NodeInternal.GapRow = value;
        }
    } = MeasurementGap.Undefined;

    /// <summary>
    /// CSS: box-sizing - Defines how width/height calculations include padding/border (content-box/border-box)
    /// </summary>
    public YgBoxSizing BoxSizing
    {
        get => NodeInternal.BoxSizing.ToNfmBoxSizing();
        set => NodeInternal.BoxSizing = value.ToYogaBoxSizing();
    }

    [TypeConverter(typeof(MeasurementWidthHeightTypeConverter))]
    public struct MeasurementWidthHeight
    {
        /// <summary>
        /// Type converter for Node.MeasurementWidthHeight.
        /// Parses values like "100", "50%", "auto", "stretch", etc.
        /// </summary>
        public class MeasurementWidthHeightTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string str)
                {
                    var trimmed = str.Trim();

                    if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    {
                        return Auto();
                    }
                    if (trimmed.Equals("stretch", StringComparison.OrdinalIgnoreCase))
                    {
                        return Stretch();
                    }
                    if (trimmed.Equals("fit-content", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Equals("fitcontent", StringComparison.OrdinalIgnoreCase))
                    {
                        return FitContent();
                    }
                    if (trimmed.Equals("max-content", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Equals("maxcontent", StringComparison.OrdinalIgnoreCase))
                    {
                        return MaxContent();
                    }
                    if (trimmed.EndsWith('%'))
                    {
                        if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                        {
                            return Percent(percentValue);
                        }
                    }
                    else if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                    {
                        if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                        {
                            return Point(floatValue);
                        }
                    }
                    else if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        return Point(floatValue);
                    }

                    throw new FormatException($"Cannot convert {str} to MeasurementWidthHeight. Expected a number, percentage, 'auto', 'stretch', 'fit-content', or 'max-content'.");
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        internal YGValue InternalValue;
        public YgUnit Unit => InternalValue.unit.ToNfmUnit();
        public float Value => InternalValue.value;
        public float? PointValue => InternalValue.unit == YGUnit.YGUnitPoint ? InternalValue.value : null;
        public float? PercentValue => InternalValue.unit == YGUnit.YGUnitPercent ? InternalValue.value : null;

        public static implicit operator MeasurementWidthHeight(float value)
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
        public static implicit operator MeasurementWidthHeight(YGValue value)
        {
            return new MeasurementWidthHeight
            {
                InternalValue = value
            };
        }
        public static implicit operator YGValue(MeasurementWidthHeight value)
        {
            return value.InternalValue;
        }

        public static MeasurementWidthHeight Undefined => new()
        {
            InternalValue = new YGValue
            {
                unit = YGUnit.YGUnitUndefined
            }
        };

        public static MeasurementWidthHeight Auto()
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitAuto
                }
            };
        }
        public static MeasurementWidthHeight Percent(float value)
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPercent,
                    value = value
                }
            };
        }
        public static MeasurementWidthHeight Point(float value)
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }

        public static MeasurementWidthHeight FitContent()
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitFitContent
                }
            };
        }
        public static MeasurementWidthHeight MaxContent()
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitMaxContent
                }
            };
        }

        public static MeasurementWidthHeight Stretch()
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue
                {
                    unit = YGUnit.YGUnitStretch
                }
            };
        }

        public MeasurementWidthHeight Scale(float scale)
        {
            if (InternalValue.unit == YGUnit.YGUnitPoint)
            {
                return Point(InternalValue.value * scale);
            }

            return this;
        }
    }

    /// <summary>
    /// CSS: width - Sets the width of the element
    /// </summary>
    public MeasurementWidthHeight Width
    {
        get;
        set
        {
            field = value;
            NodeInternal.Width = value.Scale(G.Scale);
        }
    } = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: height - Sets the height of the element
    /// </summary>
    public MeasurementWidthHeight Height
    {
        get;
        set
        {
            field = value;
            NodeInternal.Height = value.Scale(G.Scale);
        }
    } = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: min-width - Sets the minimum width of the element
    /// </summary>
    public MeasurementWidthHeight MinWidth
    {
        get;
        set
        {
            field = value;
            NodeInternal.MinWidth = value.Scale(G.Scale);
        }
    } = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: min-height - Sets the minimum height of the element
    /// </summary>
    public MeasurementWidthHeight MinHeight
    {
        get;
        set
        {
            field = value;
            NodeInternal.MinHeight = value.Scale(G.Scale);
        }
    } = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: max-width - Sets the maximum width of the element
    /// </summary>
    public MeasurementWidthHeight MaxWidth
    {
        get;
        set
        {
            field = value;
            NodeInternal.MaxWidth = value.Scale(G.Scale);
        }
    } = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: max-height - Sets the maximum height of the element
    /// </summary>
    public MeasurementWidthHeight MaxHeight
    {
        get;
        set
        {
            field = value;
            NodeInternal.MaxHeight = value.Scale(G.Scale);
        }
    } = MeasurementWidthHeight.Undefined;

    /// <summary>
    /// CSS: aspect-ratio - Sets the preferred aspect ratio for the element (width / height)
    /// </summary>
    [TypeConverter(typeof(PixelsConverter))]
    public float? AspectRatio
    {
        get => NodeInternal.AspectRatio is var v && !float.IsNaN(v) ? v : null;
        set => NodeInternal.AspectRatio = value ?? float.NaN;
    }

    #endregion

    private float _lastScale = 1f;

    static Node()
    {
        Config = YGConfigPtr.GetDefault();
        Config.UseWebDefaults = true;
    }

    ~Node()
    {
        Dispose(false);
    }

    private void ReleaseUnmanagedResources()
    {
        NodeInternal.Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            // Free any other managed objects here.
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Do not use directly.
    /// </summary>
    /// <returns>true if scale changed</returns>
    private protected bool Rescale()
    {
        if (Math.Abs(_lastScale - G.Scale) > 0.001f)
        {
            // Update all size related properties to trigger re-calculation with new scale
#pragma warning disable CA2245
            Width = Width;
            Height = Height;
            MinWidth = MinWidth;
            MinHeight = MinHeight;
            MaxWidth = MaxWidth;
            MaxHeight = MaxHeight;
            MarginTop = MarginTop;
            MarginBottom = MarginBottom;
            MarginLeft = MarginLeft;
            MarginRight = MarginRight;
            PaddingTop = PaddingTop;
            PaddingBottom = PaddingBottom;
            PaddingLeft = PaddingLeft;
            PaddingRight = PaddingRight;
            BorderTop = BorderTop;
            BorderBottom = BorderBottom;
            BorderLeft = BorderLeft;
            BorderRight = BorderRight;
            GapColumn = GapColumn;
            GapRow = GapRow;
            FlexBasis = FlexBasis;
            Left = Left;
            Top = Top;
            Right = Right;
            Bottom = Bottom;
#pragma warning restore CA2245

            _lastScale = G.Scale;

            return true;
        }

        return false;
    }

    protected virtual void OnScaleChanged()
    {
    }

    /// <summary>
    /// DO NOT OVERRIDE. Override OnScaleChanged() instead.
    /// </summary>
    protected internal virtual void RescaleRecursive()
    {
        if (Rescale())
        {
            OnScaleChanged();
        }
    }

    protected virtual void RenderBackground(Vector2 position, Vector2 size)
    {
    }

    protected virtual void RenderBorder(Vector2 position, Vector2 size)
    {
    }

    protected virtual void RenderContent(Vector2 position, Vector2 size)
    {
    }

    protected virtual void Render()
    {
        G.SetAlpha(Opacity);
        RenderBackground(LayoutPaddingPosition, LayoutPaddingSize);
        RenderBorder(LayoutBorderPosition, LayoutBorderSize);
        RenderContent(LayoutContentPosition, LayoutContentSize);
        G.SetAlpha(1f);
    }

    protected internal virtual void RenderRecursive(Vector2 root, float rootOpacity = 1f)
    {
        _root = root;
        if (Display != YgDisplay.None && Visibility == Visibility.Visible && Opacity > 0f)
        {
            var ownOpacity = rootOpacity * Opacity;
            G.SetAlpha(ownOpacity);
            Render();
            G.SetAlpha(1f);
        }
    }

    protected virtual void GameTick()
    {
    }

    public void LayoutAndRender(Vector2 availableSize, Vector2? origin = null)
    {
#if DEBUG
        __INTERNAL_YogaRootsThisFrame.Add(this);
#endif

        RescaleRecursive();
        NodeInternal.CalculateLayout(availableSize, YGDirection.YGDirectionLTR);
        RenderRecursive(origin ?? Microsoft.Xna.Framework.Vector2.Zero);
    }

    /// <summary>
    /// DO NOT OVERRIDE. Override GameTick() instead.
    /// </summary>
    protected internal virtual void Update()
    {
        GameTick();
    }
}