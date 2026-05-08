namespace NFMWorld.UI.Yoga;

public enum YgDirection
{
    Inherit,
    Ltr,
    Rtl,
}

public enum YgFlexDirection
{
    Column,
    ColumnReverse,
    Row,
    RowReverse,
}

public enum YgJustify
{
    FlexStart,
    Center,
    FlexEnd,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

public enum YgAlign
{
    Auto,
    FlexStart,
    Center,
    FlexEnd,
    Stretch,
    Baseline,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

public enum YgPositionType
{
    Static,
    Relative,
    Absolute,
}

public enum YgWrap
{
    NoWrap,
    Wrap,
    WrapReverse,
}

public enum YgOverflow
{
    Visible,
    Hidden,
    Scroll,
}

public enum YgDisplay
{
    Flex,
    None,
    Contents,
}

public enum YgBoxSizing
{
    BorderBox,
    ContentBox,
}
public enum YgNodeType
{
    Default,
    Text,
}

public enum Visibility
{
    Hidden,
    Visible
}

public enum YgUnit
{
    Undefined,
    Point,
    Percent,
    Auto,
    MaxContent,
    FitContent,
    Stretch,
}

// Implicit conversions between these enums and Yoga-CS enums
public static class Conversions
{
    public static global::Yoga.YGDirection ToYogaDirection(this YgDirection d) => (global::Yoga.YGDirection)d;
    public static YgDirection ToNfmDirection(this global::Yoga.YGDirection d) => (YgDirection)d;
    public static global::Yoga.YGFlexDirection ToYogaFlexDirection(this YgFlexDirection d) => (global::Yoga.YGFlexDirection)d;
    public static YgFlexDirection ToNfmFlexDirection(this global::Yoga.YGFlexDirection d) => (YgFlexDirection)d;
    public static global::Yoga.YGJustify ToYogaJustify(this YgJustify j) => (global::Yoga.YGJustify)j;
    public static YgJustify ToNfmJustify(this global::Yoga.YGJustify j) => (YgJustify)j;
    public static global::Yoga.YGAlign ToYogaAlign(this YgAlign a) => (global::Yoga.YGAlign)a;
    public static YgAlign ToNfmAlign(this global::Yoga.YGAlign a) => (YgAlign)a;
    public static global::Yoga.YGPositionType ToYogaPositionType(this YgPositionType p) => (global::Yoga.YGPositionType)p;
    public static YgPositionType ToNfmPositionType(this global::Yoga.YGPositionType p) => (YgPositionType)p;
    public static global::Yoga.YGWrap ToYogaWrap(this YgWrap w) => (global::Yoga.YGWrap)w;
    public static YgWrap ToNfmWrap(this global::Yoga.YGWrap w) => (YgWrap)w;
    public static global::Yoga.YGOverflow ToYogaOverflow(this YgOverflow o) => (global::Yoga.YGOverflow)o;
    public static YgOverflow ToNfmOverflow(this global::Yoga.YGOverflow o) => (YgOverflow)o;
    public static global::Yoga.YGDisplay ToYogaDisplay(this YgDisplay d) => (global::Yoga.YGDisplay)d;
    public static YgDisplay ToNfmDisplay(this global::Yoga.YGDisplay d) => (YgDisplay)d;
    public static global::Yoga.YGBoxSizing ToYogaBoxSizing(this YgBoxSizing b) => (global::Yoga.YGBoxSizing)b;
    public static YgBoxSizing ToNfmBoxSizing(this global::Yoga.YGBoxSizing b) => (YgBoxSizing)b;
    public static global::Yoga.YGNodeType ToYogaNodeType(this YgNodeType n) => (global::Yoga.YGNodeType)n;
    public static YgNodeType ToNfmNodeType(this global::Yoga.YGNodeType n) => (YgNodeType)n;
    public static global::Yoga.YGUnit ToYogaUnit(this YgUnit u) => (global::Yoga.YGUnit)u;
    public static YgUnit ToNfmUnit(this global::Yoga.YGUnit u) => (YgUnit)u;
}