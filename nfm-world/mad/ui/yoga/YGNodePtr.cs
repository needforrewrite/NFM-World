using System.Collections;
using Yoga;

namespace NFMWorld.Mad.UI.yoga;

public readonly unsafe struct YGNodePtr : IDisposable, IEnumerable<YGNodePtr>
{
    public readonly YGNode* Ptr;

    public YGConfigPtr Config
    {
        get => new YGConfigPtr(YG.NodeGetConfig(Ptr));
        set => YG.NodeSetConfig(Ptr, value.Ptr);
    }

    public float LayoutWidth => YG.NodeLayoutGetWidth(Ptr);
    public float LayoutHeight => YG.NodeLayoutGetHeight(Ptr);
    public float LayoutX => YG.NodeLayoutGetLeft(Ptr);
    public float LayoutY => YG.NodeLayoutGetTop(Ptr);
    public YGDirection LayoutDirection => YG.NodeLayoutGetDirection(Ptr);
    public bool HadOverflow => YG.NodeLayoutGetHadOverflow(Ptr) != 0;
    public float LayoutMarginTop => YG.NodeLayoutGetMargin(Ptr, YGEdge.YGEdgeTop);
    public float LayoutMarginBottom => YG.NodeLayoutGetMargin(Ptr, YGEdge.YGEdgeBottom);
    public float LayoutMarginLeft => YG.NodeLayoutGetMargin(Ptr, YGEdge.YGEdgeLeft);
    public float LayoutMarginRight => YG.NodeLayoutGetMargin(Ptr, YGEdge.YGEdgeRight);
    public float LayoutPaddingTop => YG.NodeLayoutGetPadding(Ptr, YGEdge.YGEdgeTop);
    public float LayoutPaddingBottom => YG.NodeLayoutGetPadding(Ptr, YGEdge.YGEdgeBottom);
    public float LayoutPaddingLeft => YG.NodeLayoutGetPadding(Ptr, YGEdge.YGEdgeLeft);
    public float LayoutPaddingRight => YG.NodeLayoutGetPadding(Ptr, YGEdge.YGEdgeRight);
    public float LayoutBorderTop => YG.NodeLayoutGetBorder(Ptr, YGEdge.YGEdgeTop);
    public float LayoutBorderBottom => YG.NodeLayoutGetBorder(Ptr, YGEdge.YGEdgeBottom);
    public float LayoutBorderLeft => YG.NodeLayoutGetBorder(Ptr, YGEdge.YGEdgeLeft);
    public float LayoutBorderRight => YG.NodeLayoutGetBorder(Ptr, YGEdge.YGEdgeRight);
    public YGDirection Direction
    {
        get => YG.NodeStyleGetDirection(Ptr);
        set => YG.NodeStyleSetDirection(Ptr, value);
    }
    public YGFlexDirection FlexDirection
    {
        get => YG.NodeStyleGetFlexDirection(Ptr);
        set => YG.NodeStyleSetFlexDirection(Ptr, value);
    }
    public YGJustify JustifyContent
    {
        get => YG.NodeStyleGetJustifyContent(Ptr);
        set => YG.NodeStyleSetJustifyContent(Ptr, value);
    }
    public YGAlign AlignItems
    {
        get => YG.NodeStyleGetAlignItems(Ptr);
        set => YG.NodeStyleSetAlignItems(Ptr, value);
    }
    public YGAlign AlignSelf
    {
        get => YG.NodeStyleGetAlignSelf(Ptr);
        set => YG.NodeStyleSetAlignSelf(Ptr, value);
    }
    public YGAlign AlignContent
    {
        get => YG.NodeStyleGetAlignContent(Ptr);
        set => YG.NodeStyleSetAlignContent(Ptr, value);
    }
    public YGPositionType PositionType
    {
        get => YG.NodeStyleGetPositionType(Ptr);
        set => YG.NodeStyleSetPositionType(Ptr, value);
    }
    public YGWrap FlexWrap
    {
        get => YG.NodeStyleGetFlexWrap(Ptr);
        set => YG.NodeStyleSetFlexWrap(Ptr, value);
    }
    public YGOverflow Overflow
    {
        get => YG.NodeStyleGetOverflow(Ptr);
        set => YG.NodeStyleSetOverflow(Ptr, value);
    }
    public YGDisplay Display
    {
        get => YG.NodeStyleGetDisplay(Ptr);
        set => YG.NodeStyleSetDisplay(Ptr, value);
    }
    
    public float Flex
    {
        get => YG.NodeStyleGetFlex(Ptr);
        set => YG.NodeStyleSetFlex(Ptr, value);
    }
    public float FlexGrow
    {
        get => YG.NodeStyleGetFlexGrow(Ptr);
        set => YG.NodeStyleSetFlexGrow(Ptr, value);
    }
    public float FlexShrink
    {
        get => YG.NodeStyleGetFlexShrink(Ptr);
        set => YG.NodeStyleSetFlexShrink(Ptr, value);
    }
    public YGValue FlexBasis
    {
        get => YG.NodeStyleGetFlexBasis(Ptr);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetFlexBasis(Ptr, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetFlexBasisPercent(Ptr, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetFlexBasisAuto(Ptr);
                    break;
                case YGUnit.YGUnitMaxContent:
                    YG.NodeStyleSetFlexBasisMaxContent(Ptr);
                    break;
                case YGUnit.YGUnitFitContent:
                    YG.NodeStyleSetFlexBasisFitContent(Ptr);
                    break;
                case YGUnit.YGUnitStretch:
                    YG.NodeStyleSetFlexBasisStretch(Ptr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }

    public YGValue Left
    {
        get => YG.NodeStyleGetPosition(Ptr, YGEdge.YGEdgeLeft);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPosition(Ptr, YGEdge.YGEdgeLeft, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPositionPercent(Ptr, YGEdge.YGEdgeLeft, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetPositionAuto(Ptr, YGEdge.YGEdgeLeft);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue Top
    {
        get => YG.NodeStyleGetPosition(Ptr, YGEdge.YGEdgeTop);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPosition(Ptr, YGEdge.YGEdgeTop, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPositionPercent(Ptr, YGEdge.YGEdgeTop, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetPositionAuto(Ptr, YGEdge.YGEdgeTop);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue Right
    {
        get => YG.NodeStyleGetPosition(Ptr, YGEdge.YGEdgeRight);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPosition(Ptr, YGEdge.YGEdgeRight, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPositionPercent(Ptr, YGEdge.YGEdgeRight, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetPositionAuto(Ptr, YGEdge.YGEdgeRight);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue Bottom
    {
        get => YG.NodeStyleGetPosition(Ptr, YGEdge.YGEdgeBottom);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPosition(Ptr, YGEdge.YGEdgeBottom, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPositionPercent(Ptr, YGEdge.YGEdgeBottom, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetPositionAuto(Ptr, YGEdge.YGEdgeBottom);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue MarginTop
    {
        get => YG.NodeStyleGetMargin(Ptr, YGEdge.YGEdgeTop);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMargin(Ptr, YGEdge.YGEdgeTop, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMarginPercent(Ptr, YGEdge.YGEdgeTop, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetMarginAuto(Ptr, YGEdge.YGEdgeTop);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }

    public YGValue MarginBottom
    {
        get => YG.NodeStyleGetMargin(Ptr, YGEdge.YGEdgeBottom);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMargin(Ptr, YGEdge.YGEdgeBottom, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMarginPercent(Ptr, YGEdge.YGEdgeBottom, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetMarginAuto(Ptr, YGEdge.YGEdgeBottom);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }

    public YGValue MarginLeft
    {
        get => YG.NodeStyleGetMargin(Ptr, YGEdge.YGEdgeLeft);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMargin(Ptr, YGEdge.YGEdgeLeft, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMarginPercent(Ptr, YGEdge.YGEdgeLeft, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetMarginAuto(Ptr, YGEdge.YGEdgeLeft);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }

    public YGValue MarginRight
    {
        get => YG.NodeStyleGetMargin(Ptr, YGEdge.YGEdgeRight);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMargin(Ptr, YGEdge.YGEdgeRight, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMarginPercent(Ptr, YGEdge.YGEdgeRight, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetMarginAuto(Ptr, YGEdge.YGEdgeRight);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    public YGValue PaddingTop
    {
        get => YG.NodeStyleGetPadding(Ptr, YGEdge.YGEdgeTop);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPadding(Ptr, YGEdge.YGEdgeTop, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPaddingPercent(Ptr, YGEdge.YGEdgeTop, value.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }

    public YGValue PaddingBottom
    {
        get => YG.NodeStyleGetPadding(Ptr, YGEdge.YGEdgeBottom);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPadding(Ptr, YGEdge.YGEdgeBottom, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPaddingPercent(Ptr, YGEdge.YGEdgeBottom, value.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue PaddingLeft
    {
        get => YG.NodeStyleGetPadding(Ptr, YGEdge.YGEdgeLeft);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPadding(Ptr, YGEdge.YGEdgeLeft, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPaddingPercent(Ptr, YGEdge.YGEdgeLeft, value.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }

    public YGValue PaddingRight
    {
        get => YG.NodeStyleGetPadding(Ptr, YGEdge.YGEdgeRight);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetPadding(Ptr, YGEdge.YGEdgeRight, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetPaddingPercent(Ptr, YGEdge.YGEdgeRight, value.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }

    public float BorderTop
    {
        get => YG.NodeStyleGetBorder(Ptr, YGEdge.YGEdgeTop);
        set => YG.NodeStyleSetBorder(Ptr, YGEdge.YGEdgeTop, value);
    }
    public float BorderBottom
    {
        get => YG.NodeStyleGetBorder(Ptr, YGEdge.YGEdgeBottom);
        set => YG.NodeStyleSetBorder(Ptr, YGEdge.YGEdgeBottom, value);
    }
    public float BorderLeft
    {
        get => YG.NodeStyleGetBorder(Ptr, YGEdge.YGEdgeLeft);
        set => YG.NodeStyleSetBorder(Ptr, YGEdge.YGEdgeLeft, value);
    }
    public float BorderRight
    {
        get => YG.NodeStyleGetBorder(Ptr, YGEdge.YGEdgeRight);
        set => YG.NodeStyleSetBorder(Ptr, YGEdge.YGEdgeRight, value);
    }

    public YGValue GapColumn
    {
        get => YG.NodeStyleGetGap(Ptr, YGGutter.YGGutterColumn);
        set {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetGap(Ptr, YGGutter.YGGutterColumn, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetGapPercent(Ptr, YGGutter.YGGutterColumn, value.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue GapRow
    {
        get => YG.NodeStyleGetGap(Ptr, YGGutter.YGGutterRow);
        set {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetGap(Ptr, YGGutter.YGGutterRow, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetGapPercent(Ptr, YGGutter.YGGutterRow, value.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGBoxSizing BoxSizing
    {
        get => YG.NodeStyleGetBoxSizing(Ptr);
        set => YG.NodeStyleSetBoxSizing(Ptr, value);
    }
    
    public YGValue Width
    {
        get => YG.NodeStyleGetWidth(Ptr);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetWidth(Ptr, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetWidthPercent(Ptr, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetWidthAuto(Ptr);
                    break;
                case YGUnit.YGUnitFitContent:
                    YG.NodeStyleSetWidthFitContent(Ptr);
                    break;
                case YGUnit.YGUnitMaxContent:
                    YG.NodeStyleSetWidthMaxContent(Ptr);
                    break;
                case YGUnit.YGUnitStretch:
                    YG.NodeStyleSetWidthStretch(Ptr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue Height
    {
        get => YG.NodeStyleGetHeight(Ptr);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetHeight(Ptr, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetHeightPercent(Ptr, value.value);
                    break;
                case YGUnit.YGUnitAuto:
                    YG.NodeStyleSetHeightAuto(Ptr);
                    break;
                case YGUnit.YGUnitFitContent:
                    YG.NodeStyleSetHeightFitContent(Ptr);
                    break;
                case YGUnit.YGUnitMaxContent:
                    YG.NodeStyleSetHeightMaxContent(Ptr);
                    break;
                case YGUnit.YGUnitStretch:
                    YG.NodeStyleSetHeightStretch(Ptr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue MinWidth
    {
        get => YG.NodeStyleGetMinWidth(Ptr);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMinWidth(Ptr, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMinWidthPercent(Ptr, value.value);
                    break;
                case YGUnit.YGUnitFitContent:
                    YG.NodeStyleSetMinWidthFitContent(Ptr);
                    break;
                case YGUnit.YGUnitMaxContent:
                    YG.NodeStyleSetMinWidthMaxContent(Ptr);
                    break;
                case YGUnit.YGUnitStretch:
                    YG.NodeStyleSetMinWidthStretch(Ptr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue MinHeight
    {
        get => YG.NodeStyleGetMinHeight(Ptr);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMinHeight(Ptr, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMinHeightPercent(Ptr, value.value);
                    break;
                case YGUnit.YGUnitFitContent:
                    YG.NodeStyleSetMinHeightFitContent(Ptr);
                    break;
                case YGUnit.YGUnitMaxContent:
                    YG.NodeStyleSetMinHeightMaxContent(Ptr);
                    break;
                case YGUnit.YGUnitStretch:
                    YG.NodeStyleSetMinHeightStretch(Ptr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue MaxWidth
    {
        get => YG.NodeStyleGetMaxWidth(Ptr);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMaxWidth(Ptr, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMaxWidthPercent(Ptr, value.value);
                    break;
                case YGUnit.YGUnitFitContent:
                    YG.NodeStyleSetMaxWidthFitContent(Ptr);
                    break;
                case YGUnit.YGUnitMaxContent:
                    YG.NodeStyleSetMaxWidthMaxContent(Ptr);
                    break;
                case YGUnit.YGUnitStretch:
                    YG.NodeStyleSetMaxWidthStretch(Ptr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public YGValue MaxHeight
    {
        get => YG.NodeStyleGetMaxHeight(Ptr);
        set
        {
            switch (value.unit)
            {
                case YGUnit.YGUnitUndefined:
                    break;
                case YGUnit.YGUnitPoint:
                    YG.NodeStyleSetMaxHeight(Ptr, value.value);
                    break;
                case YGUnit.YGUnitPercent:
                    YG.NodeStyleSetMaxHeightPercent(Ptr, value.value);
                    break;
                case YGUnit.YGUnitFitContent:
                    YG.NodeStyleSetMaxHeightFitContent(Ptr);
                    break;
                case YGUnit.YGUnitMaxContent:
                    YG.NodeStyleSetMaxHeightMaxContent(Ptr);
                    break;
                case YGUnit.YGUnitStretch:
                    YG.NodeStyleSetMaxHeightStretch(Ptr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.unit), value.unit, null);
            }
        }
    }
    
    public float AspectRatio
    {
        get => YG.NodeStyleGetAspectRatio(Ptr);
        set => YG.NodeStyleSetAspectRatio(Ptr, value);
    }
    
    public bool HasNewLayout
    {
        get => YG.NodeGetHasNewLayout(Ptr) != 0;
        set => YG.NodeSetHasNewLayout(Ptr, value ? (byte)1 : (byte)0);
    }

    public bool IsDirty
    {
        get => YG.NodeIsDirty(Ptr) != 0;
        set
        {
            if (value)
            {
                YG.NodeMarkDirty(Ptr);
            }
        }
    }

    public bool IsReferenceBaseline
    {
        set => YG.NodeSetIsReferenceBaseline(Ptr, value ? (byte)1 : (byte)0);
        get => YG.NodeIsReferenceBaseline(Ptr) != 0;
    }
    
    public YGNodeType NodeType
    {
        get => YG.NodeGetNodeType(Ptr);
        set => YG.NodeSetNodeType(Ptr, value);
    }

    public bool AlwaysFormsContainingBlock
    {
        get => YG.NodeGetAlwaysFormsContainingBlock(Ptr) != 0;
        set => YG.NodeSetAlwaysFormsContainingBlock(Ptr, value ? (byte)1 : (byte)0);
    }

    public YGNodePtr()
    {
        Ptr = YG.NodeNew();
    }

    public YGNodePtr(YGConfigPtr config)
    {
        Ptr = YG.NodeNewWithConfig(config.Ptr);
    }

    private YGNodePtr(YGNode* ptr)
    {
        Ptr = ptr;
    }
    
    public YGNodePtr Clone(YGConfigPtr config)
    {
        return new YGNodePtr(YG.NodeClone(Ptr));
    }

    public IEnumerator<YGNodePtr> GetEnumerator()
    {
        nuint count = GetChildCount();
        for (uint i = 0; i < count; i++)
        {
            yield return GetChild(i)!.Value;
        }
    }

    public void FinalizeNode()
    {
        YG.NodeFinalize(Ptr);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Reset()
    {
        YG.NodeReset(Ptr);
    }
    
    public void SetDirtiedFunc(delegate* unmanaged[Cdecl]<YGNode*, void> dirtiedFunc)
    {
        YG.NodeSetDirtiedFunc(Ptr, dirtiedFunc);
    }
    
    public void InsertChild(YGNodePtr child, uint index)
    {
        YG.NodeInsertChild(Ptr, child.Ptr, index);
    }
    
    public void SwapChild(YGNodePtr child, uint index)
    {
        YG.NodeSwapChild(Ptr, child.Ptr, index);
    }
    
    public void RemoveChild(YGNodePtr child)
    {
        YG.NodeRemoveChild(Ptr, child.Ptr);
    }
    
    public void RemoveAllChildren()
    {
        YG.NodeRemoveAllChildren(Ptr);
    }
    
    public void SetChildren(Span<YGNodePtr> children)
    {
        fixed (YGNode** childPtrs = new YGNode*[children.Length])
        {
            for (int i = 0; i < children.Length; i++)
            {
                childPtrs[i] = children[i].Ptr;
            }
            YG.NodeSetChildren(Ptr, childPtrs, (UIntPtr)children.Length);
        }
    }
    
    public YGNodePtr? GetChild(uint index)
    {
        var child = YG.NodeGetChild(Ptr, index);
        if (child == null)
        {
            return null;
        }
        return new YGNodePtr(child);
    }
    
    public uint GetChildCount()
    {
        return (uint)YG.NodeGetChildCount(Ptr);
    }

    public YGNodePtr? GetParent()
    {
        var parent = YG.NodeGetParent(Ptr);
        if (parent == null)
        {
            return null;
        }
        return new YGNodePtr(parent);
    }
    
    public YGNodePtr? GetOwner()
    {
        var owner = YG.NodeGetOwner(Ptr);
        if (owner == null)
        {
            return null;
        }
        return new YGNodePtr(owner);
    }
    
    public void SetMeasureFunc(delegate* unmanaged[Cdecl]<YGNode*, float, YGMeasureMode, float, YGMeasureMode, YGSize> measureFunc)
    {
        YG.NodeSetMeasureFunc(Ptr, measureFunc);
    }
    
    public bool HasMeasureFunc()
    {
        return YG.NodeHasMeasureFunc(Ptr) != 0;
    }
    
    public void SetBaselineFunc(delegate* unmanaged[Cdecl]<YGNode*, float, float, float> baselineFunc)
    {
        YG.NodeSetBaselineFunc(Ptr, baselineFunc);
    }
    
    public bool HasBaselineFunc()
    {
        return YG.NodeHasBaselineFunc(Ptr) != 0;
    }
    
    public void CopyStyle(YGNodePtr srcNode)
    {
        YG.NodeCopyStyle(Ptr, srcNode.Ptr);
    }
    
    public YGNodeChildCollection Children()
    {
        return new YGNodeChildCollection(this);
    }

    public readonly struct YGNodeChildCollection(YGNodePtr ygNodePtr) : IList<YGNodePtr>
    {
        private readonly YGNodePtr _ygNodePtr = ygNodePtr;

        public IEnumerator<YGNodePtr> GetEnumerator()
        {
            nuint count = _ygNodePtr.GetChildCount();
            for (uint i = 0; i < count; i++)
            {
                yield return _ygNodePtr.GetChild(i)!.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(YGNodePtr item)
        {
            _ygNodePtr.InsertChild(item, _ygNodePtr.GetChildCount());
        }

        void ICollection<YGNodePtr>.Clear()
        {
            _ygNodePtr.RemoveAllChildren();
        }

        public bool Contains(YGNodePtr item)
        {
            nuint count = _ygNodePtr.GetChildCount();
            for (uint i = 0; i < count; i++)
            {
                var child = _ygNodePtr.GetChild(i);
                if (child.HasValue && child.Value.Ptr == item.Ptr)
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(YGNodePtr[] array, int arrayIndex)
        {
            nuint count = _ygNodePtr.GetChildCount();
            for (uint i = 0; i < count; i++)
            {
                var child = _ygNodePtr.GetChild(i);
                if (child.HasValue)
                {
                    array[arrayIndex + (int)i] = child.Value;
                }
            }
        }

        bool ICollection<YGNodePtr>.Remove(YGNodePtr item)
        {
            nuint count = _ygNodePtr.GetChildCount();
            for (uint i = 0; i < count; i++)
            {
                var child = _ygNodePtr.GetChild(i);
                if (child.HasValue && child.Value.Ptr == item.Ptr)
                {
                    _ygNodePtr.RemoveChild(item);
                    return true;
                }
            }
            return false;
        }

        public int Count => (int)_ygNodePtr.GetChildCount();
        public bool IsReadOnly => false;
        
        public int IndexOf(YGNodePtr item)
        {
            nuint count = _ygNodePtr.GetChildCount();
            for (uint i = 0; i < count; i++)
            {
                var child = _ygNodePtr.GetChild(i);
                if (child.HasValue && child.Value.Ptr == item.Ptr)
                {
                    return (int)i;
                }
            }
            return -1;
        }

        public void Insert(int index, YGNodePtr item)
        {
            _ygNodePtr.InsertChild(item, (uint)index);
        }

        void IList<YGNodePtr>.RemoveAt(int index)
        {
            var child = _ygNodePtr.GetChild((uint)index);
            if (child.HasValue)
            {
                _ygNodePtr.RemoveChild(child.Value);
            }
        }

        public YGNodePtr this[int index]
        {
            get => _ygNodePtr.GetChild((uint)index)!.Value;
            set => _ygNodePtr.SwapChild(value, (uint)index);
        }
    }

    public void Dispose()
    {
        YG.NodeFree(Ptr);
    }

    public void CalculateLayout(float availableWidth, float availableHeight, YGDirection ownerDirection)
    {
        YG.NodeCalculateLayout(Ptr, availableWidth, availableHeight, ownerDirection);
    }
    public void CalculateLayout(YGDirection ownerDirection)
    {
        YG.NodeCalculateLayout(Ptr, YG.YGUndefined, YG.YGUndefined, ownerDirection);
    }
    public void CalculateLayout(Vector2 availableSize, YGDirection ownerDirection)
    {
        YG.NodeCalculateLayout(Ptr, availableSize.X, availableSize.Y, ownerDirection);
    }
}