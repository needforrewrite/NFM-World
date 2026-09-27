using Microsoft.UI.Reactor.Layout;
using NFMWorld.Lua;

namespace NFMWorld.Reactor;

[LuaVisible]
public enum Direction
{
    Inherit,
    Ltr,
    Rtl,
}

public enum FlexDirection
{
    Column,
    ColumnReverse,
    Row,
    RowReverse,
}

public enum Justify
{
    // NOTE: must stay value-identical to Microsoft.UI.Reactor.Layout.FlexJustify
    // (which has Auto=0, FlexStart=1, Center=2, ...) because ToYogaJustify() is a
    // raw cast. Missing `Auto` here shifted every member by one, so
    // Justify.Center cast to FlexJustify.FlexStart and vertical centering broke.
    Auto,
    FlexStart,
    Center,
    FlexEnd,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

public enum Align
{
    // value-identical to Microsoft.UI.Reactor.Layout.FlexAlign (raw cast in ToYogaAlign)
    Auto,
    FlexStart,
    Center,
    FlexEnd,
    Stretch,
    Baseline,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
    Start,
    End,
}

public enum Position
{
    Static,
    Relative,
    Absolute,
}

public enum Wrap
{
    NoWrap,
    Wrap,
    WrapReverse,
}

public enum Overflow
{
    Visible,
    Hidden,
    Scroll,
}

public enum Display
{
    // value-identical to Microsoft.UI.Reactor.Layout.YogaDisplay (raw cast in ToYogaDisplay)
    Flex,
    None,
    Contents,
    Grid,
}

public enum BoxSizing
{
    BorderBox,
    ContentBox,
}

public enum NodeType
{
    Default,
    Text,
}

public enum Visibility
{
    Hidden,
    Visible
}

// Implicit conversions between these enums and Yoga-CS enums
public static class Conversions
{
    public static FlexLayoutDirection ToYogaDirection(this Direction d) => (FlexLayoutDirection)d;
    public static Direction ToNfmDirection(this FlexLayoutDirection d) => (Direction)d;
    public static Microsoft.UI.Reactor.Layout.FlexDirection ToYogaFlexDirection(this FlexDirection d) => (Microsoft.UI.Reactor.Layout.FlexDirection)d;
    public static FlexDirection ToNfmFlexDirection(this Microsoft.UI.Reactor.Layout.FlexDirection d) => (FlexDirection)d;
    public static FlexJustify ToYogaJustify(this Justify j) => (FlexJustify)j;
    public static Justify ToNfmJustify(this FlexJustify j) => (Justify)j;
    public static FlexAlign ToYogaAlign(this Align a) => (FlexAlign)a;
    public static Align ToNfmAlign(this FlexAlign a) => (Align)a;
    public static FlexPositionType ToYogaPositionType(this Position p) => (FlexPositionType)p;
    public static Position ToNfmPositionType(this FlexPositionType p) => (Position)p;
    public static FlexWrap ToYogaWrap(this Wrap w) => (FlexWrap)w;
    public static Wrap ToNfmWrap(this FlexWrap w) => (Wrap)w;
    public static YogaOverflow ToYogaOverflow(this Overflow o) => (YogaOverflow)o;
    public static Overflow ToNfmOverflow(this YogaOverflow o) => (Overflow)o;
    public static YogaDisplay ToYogaDisplay(this Display d) => (YogaDisplay)d;
    public static Display ToNfmDisplay(this YogaDisplay d) => (Display)d;
    public static YogaBoxSizing ToYogaBoxSizing(this BoxSizing b) => (YogaBoxSizing)b;
    public static BoxSizing ToNfmBoxSizing(this YogaBoxSizing b) => (BoxSizing)b;
    public static YogaNodeType ToYogaNodeType(this NodeType n) => (YogaNodeType)n;
    public static NodeType ToNfmNodeType(this YogaNodeType n) => (NodeType)n;
}