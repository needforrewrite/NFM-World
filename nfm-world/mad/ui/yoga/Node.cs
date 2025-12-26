using System.Collections;
using Yoga;

namespace NFMWorld.Mad.UI.yoga;

public class Node : IDisposable
{
    internal static readonly YGConfigPtr Config = new()
    {
        UseWebDefaults = true
    };

    internal YGNodePtr NodeInternal = new(Config);

    public Node()
    {
        Children = new(this);
    }

    public NodeChildCollection Children { get; }

    #region Layout

    // https://www.w3schools.com/css/css_boxmodel.asp
    public Vector2 LayoutMarginPosition => new(LayoutX, LayoutY);
    public Vector2 LayoutMarginSize => new(LayoutWidth, LayoutHeight);
    public Vector2 LayoutBorderPosition => new(LayoutX + LayoutMarginLeft, LayoutY + LayoutMarginTop);
    public Vector2 LayoutBorderSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom));
    public Vector2 LayoutPaddingPosition => new(LayoutX + LayoutMarginLeft + LayoutBorderLeft, LayoutY + LayoutMarginTop + LayoutBorderTop);
    public Vector2 LayoutPaddingSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight + LayoutBorderLeft + LayoutBorderRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom + LayoutBorderTop + LayoutBorderBottom));
    public Vector2 LayoutContentPosition => new(LayoutX + LayoutMarginLeft + LayoutBorderLeft + LayoutPaddingLeft, LayoutY + LayoutMarginTop + LayoutBorderTop + LayoutPaddingTop);
    public Vector2 LayoutContentSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight + LayoutBorderLeft + LayoutBorderRight + LayoutPaddingLeft + LayoutPaddingRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom + LayoutBorderTop + LayoutBorderBottom + LayoutPaddingTop + LayoutPaddingBottom));
    
    public Vector2 LayoutMargin => new(LayoutMarginLeft + LayoutMarginRight, LayoutMarginTop + LayoutMarginBottom);
    public Vector2 LayoutPadding => new(LayoutPaddingLeft + LayoutPaddingRight, LayoutPaddingTop + LayoutPaddingBottom);
    public Vector2 LayoutBorder => new(LayoutBorderLeft + LayoutBorderRight, LayoutBorderTop + LayoutBorderBottom);
    
    public float LayoutWidth => NodeInternal.LayoutWidth;
    public float LayoutHeight => NodeInternal.LayoutHeight;
    public float LayoutX => NodeInternal.LayoutX;
    public float LayoutY => NodeInternal.LayoutY;
    public YGDirection LayoutDirection => NodeInternal.LayoutDirection;
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
    
    public YGNodeType NodeType
    {
        get => NodeInternal.NodeType;
        set => NodeInternal.NodeType = value;
    }

    public bool AlwaysFormsContainingBlock
    {
        get => NodeInternal.AlwaysFormsContainingBlock;
        set => NodeInternal.AlwaysFormsContainingBlock = value;
    }

    #endregion
    
    #region Style
    
    // https://css-tricks.com/snippets/css/a-guide-to-flexbox/
    public YGDirection Direction
    {
        get => NodeInternal.Direction;
        set => NodeInternal.Direction = value;
    }
    public YGFlexDirection FlexDirection
    {
        get => NodeInternal.FlexDirection;
        set => NodeInternal.FlexDirection = value;
    }
    public YGJustify JustifyContent
    {
        get => NodeInternal.JustifyContent;
        set => NodeInternal.JustifyContent = value;
    }
    public YGAlign AlignItems
    {
        get => NodeInternal.AlignItems;
        set => NodeInternal.AlignItems = value;
    }
    public YGAlign AlignSelf
    {
        get => NodeInternal.AlignSelf;
        set => NodeInternal.AlignSelf = value;
    }
    public YGAlign AlignContent
    {
        get => NodeInternal.AlignContent;
        set => NodeInternal.AlignContent = value;
    }
    public YGPositionType Position
    {
        get => NodeInternal.PositionType;
        set => NodeInternal.PositionType = value;
    }
    public YGWrap FlexWrap
    {
        get => NodeInternal.FlexWrap;
        set => NodeInternal.FlexWrap = value;
    }
    public YGOverflow Overflow
    {
        get => NodeInternal.Overflow;
        set => NodeInternal.Overflow = value;
    }
    public YGDisplay Display
    {
        get => NodeInternal.Display;
        set => NodeInternal.Display = value;
    }
    
    public float Flex
    {
        get => NodeInternal.Flex;
        set => NodeInternal.Flex = value;
    }
    public float FlexGrow
    {
        get => NodeInternal.FlexGrow;
        set => NodeInternal.FlexGrow = value;
    }
    public float FlexShrink
    {
        get => NodeInternal.FlexShrink;
        set => NodeInternal.FlexShrink = value;
    }

    public struct MeasurementFlexBasis
    {
        public YGValue InternalValue;
        
        public static implicit operator MeasurementFlexBasis(float value)
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue()
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
        public static MeasurementFlexBasis Auto()
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitFitContent
                }
            };
        }

        public static MeasurementFlexBasis MaxContent()
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitMaxContent
                }
            };
        }
        
        public static MeasurementFlexBasis Stretch()
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitStretch
                }
            };
        }
        
        public static MeasurementFlexBasis Percent(float value)
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue()
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
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
        
        public static MeasurementFlexBasis FitContent()
        {
            return new MeasurementFlexBasis
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitFitContent
                }
            };
        }
    }
    
    public MeasurementFlexBasis FlexBasis
    {
        get => NodeInternal.FlexBasis;
        set => NodeInternal.FlexBasis = value;
    }

    public struct MeasurementMarginPosition
    {
        public YGValue InternalValue;
        
        public static implicit operator MeasurementMarginPosition(float value)
        {
            return new MeasurementMarginPosition
            {
                InternalValue = new YGValue()
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
        public static MeasurementMarginPosition Auto()
        {
            return new MeasurementMarginPosition
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitAuto
                }
            };
        }
        public static MeasurementMarginPosition Percent(float value)
        {
            return new MeasurementMarginPosition
            {
                InternalValue = new YGValue()
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
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
    }

    public MeasurementMarginPosition Left
    {
        get => NodeInternal.Left;
        set => NodeInternal.Left = value;
    }
    
    public MeasurementMarginPosition Top
    {
        get => NodeInternal.Top;
        set => NodeInternal.Top = value;
    }
    
    public MeasurementMarginPosition Right
    {
        get => NodeInternal.Right;
        set => NodeInternal.Right = value;
    }
    
    public MeasurementMarginPosition Bottom
    {
        get => NodeInternal.Bottom;
        set => NodeInternal.Bottom = value;
    }

    public MeasurementMarginPosition MarginTop
    {
        get => NodeInternal.MarginTop;
        set => NodeInternal.MarginTop = value;
    }

    public MeasurementMarginPosition MarginBottom
    {
        get => NodeInternal.MarginBottom;
        set => NodeInternal.MarginBottom = value;
    }

    public MeasurementMarginPosition MarginLeft
    {
        get => NodeInternal.MarginLeft;
        set => NodeInternal.MarginLeft = value;
    }

    public MeasurementMarginPosition MarginRight
    {
        get => NodeInternal.MarginRight;
        set => NodeInternal.MarginRight = value;
    }

    public struct MeasurementPadding
    {
        public YGValue InternalValue;
        
        public static implicit operator MeasurementPadding(float value)
        {
            return new MeasurementPadding
            {
                InternalValue = new YGValue()
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
        public static MeasurementPadding Percent(float value)
        {
            return new MeasurementPadding
            {
                InternalValue = new YGValue()
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
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
    }
    
    public MeasurementPadding Padding
    {
        set
        {
            PaddingLeft = value;
            PaddingRight = value;
            PaddingTop = value;
            PaddingBottom = value;
        }
    }
    public MeasurementPadding PaddingTop
    {
        get => NodeInternal.PaddingTop;
        set => NodeInternal.PaddingTop = value;
    }

    public MeasurementPadding PaddingBottom
    {
        get => NodeInternal.PaddingBottom;
        set => NodeInternal.PaddingBottom = value;
    }
    
    public MeasurementPadding PaddingLeft
    {
        get => NodeInternal.PaddingLeft;
        set => NodeInternal.PaddingLeft = value;
    }

    public MeasurementPadding PaddingRight
    {
        get => NodeInternal.PaddingRight;
        set => NodeInternal.PaddingRight = value;
    }

    public float Border
    {
        set
        {
            BorderLeft = value;
            BorderRight = value;
            BorderTop = value;
            BorderBottom = value;
        }
    }
    public float BorderTop
    {
        get => NodeInternal.BorderTop;
        set => NodeInternal.BorderTop = value;
    }
    public float BorderBottom
    {
        get => NodeInternal.BorderBottom;
        set => NodeInternal.BorderBottom = value;
    }
    public float BorderLeft
    {
        get => NodeInternal.BorderLeft;
        set => NodeInternal.BorderLeft = value;
    }
    public float BorderRight
    {
        get => NodeInternal.BorderRight;
        set => NodeInternal.BorderRight = value;
    }
    
    public struct MeasurementGap
    {
        public YGValue InternalValue;
        
        public static implicit operator MeasurementGap(float value)
        {
            return new MeasurementGap
            {
                InternalValue = new YGValue()
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
        public static MeasurementGap Percent(float value)
        {
            return new MeasurementGap
            {
                InternalValue = new YGValue()
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
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitPoint,
                    value = value
                }
            };
        }
    }

    public MeasurementGap Gap
    {
        set
        {
            GapColumn = value;
            GapRow = value;
        }
    }

    public MeasurementGap GapColumn
    {
        get => NodeInternal.GapColumn;
        set => NodeInternal.GapColumn = value;
    }
    
    public MeasurementGap GapRow
    {
        get => NodeInternal.GapRow;
        set => NodeInternal.GapRow = value;
    }
    
    public YGBoxSizing BoxSizing
    {
        get => NodeInternal.BoxSizing;
        set => NodeInternal.BoxSizing = value;
    }

    public struct MeasurementWidthHeight
    {
        public YGValue InternalValue;
        
        public static implicit operator MeasurementWidthHeight(float value)
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue()
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
        public static MeasurementWidthHeight Auto()
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitAuto
                }
            };
        }
        public static MeasurementWidthHeight Percent(float value)
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue()
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
                InternalValue = new YGValue()
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
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitFitContent
                }
            };
        }
        public static MeasurementWidthHeight MaxContent()
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitMaxContent
                }
            };
        }

        public static MeasurementWidthHeight Stretch()
        {
            return new MeasurementWidthHeight
            {
                InternalValue = new YGValue()
                {
                    unit = YGUnit.YGUnitStretch
                }
            };
        }
    }
    
    public MeasurementWidthHeight Width
    {
        get => NodeInternal.Width;
        set => NodeInternal.Width = value;
    }
    
    public MeasurementWidthHeight Height
    {
        get => NodeInternal.Height;
        set => NodeInternal.Height = value;
    }
    
    public MeasurementWidthHeight MinWidth
    {
        get => NodeInternal.MinWidth;
        set => NodeInternal.MinWidth = value;
    }
    
    public MeasurementWidthHeight MinHeight
    {
        get => NodeInternal.MinHeight;
        set => NodeInternal.MinHeight = value;
    }
    
    public MeasurementWidthHeight MaxWidth
    {
        get => NodeInternal.MaxWidth;
        set => NodeInternal.MaxWidth = value;
    }
    
    public MeasurementWidthHeight MaxHeight
    {
        get => NodeInternal.MaxHeight;
        set => NodeInternal.MaxHeight = value;
    }
    
    public float AspectRatio
    {
        get => NodeInternal.AspectRatio;
        set => NodeInternal.AspectRatio = value;
    }

    #endregion

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
    
    public virtual void RenderBackground(Vector2 position, Vector2 size)
    {
    }

    public virtual void RenderBorder(Vector2 position, Vector2 size)
    {
    }
    
    public virtual void RenderContent(Vector2 position, Vector2 size)
    {
    }

    public virtual void Render()
    {
        RenderBackground(LayoutMarginPosition, LayoutMarginSize);
        RenderBorder(LayoutBorderPosition, LayoutBorderSize);
        RenderContent(LayoutContentPosition, LayoutContentSize);
    }

    private void RenderRecursive()
    {
        Render();
        foreach (var child in Children)
        {
            child.RenderRecursive();
        }
    }
    
    public virtual void GameTick()
    {
    }

    public void LayoutAndRender(Vector2 availableSize)
    {
        NodeInternal.CalculateLayout(availableSize, YGDirection.YGDirectionLTR);
        RenderRecursive();
    }

    public void Update()
    {
        GameTick();
        foreach (var child in Children)
        {
            child.Update();
        }
    }
}

public class NodeChildCollection(Node parent) : IList<Node>
{
    private List<Node> _internalList = new();
    
    public IEnumerator<Node> GetEnumerator()
    {
        return _internalList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(Node item)
    {
        parent.NodeInternal.InsertChild(item.NodeInternal, parent.NodeInternal.GetChildCount());
        _internalList.Add(item);
    }

    public void Clear()
    {
        parent.NodeInternal.RemoveAllChildren();
        _internalList.Clear();
    }

    public bool Contains(Node item)
    {
        return _internalList.Contains(item);
    }

    public void CopyTo(Node[] array, int arrayIndex)
    {
        _internalList.CopyTo(array, arrayIndex);
    }

    public bool Remove(Node item)
    {
        if (_internalList.Remove(item))
        {
            parent.NodeInternal.RemoveChild(item.NodeInternal);
            return true;
        }

        return false;
    }

    public int Count => _internalList.Count;
    public bool IsReadOnly => false;
    public int IndexOf(Node item)
    {
        return _internalList.IndexOf(item);
    }

    public void Insert(int index, Node item)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        parent.NodeInternal.InsertChild(item.NodeInternal, (uint)index);
        _internalList.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        var item = _internalList[index];
        parent.NodeInternal.RemoveChild(item.NodeInternal);
        _internalList.RemoveAt(index);
    }

    public Node this[int index]
    {
        get => _internalList[index];
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
            _internalList[index] = value;
            parent.NodeInternal.SwapChild(value.NodeInternal, (uint)index);
        }
    }
}