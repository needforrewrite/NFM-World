using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards the CSS box-model accessors on <see cref="Component"/> against Yoga's layout results.
///
/// Yoga reports <c>LayoutWidth</c>/<c>LayoutHeight</c> as the border-box size and
/// <c>LayoutX</c>/<c>LayoutY</c> as the border-box corner (relative to the parent's border box,
/// with the child's leading margins already folded in). The accessors used to subtract margins
/// from <c>LayoutWidth</c>/<c>LayoutHeight</c> a second time, which shrank the drawn box rather
/// than spacing it: a card with only <c>marginBottom = 16</c> had its background, border and clip
/// rects come out 16px shorter than its own layout height, so the bottom of the card was cut off
/// and the margin looked like it had eaten into the card instead of pushing the next one down.
///
/// The numbers below are not derived from the implementation — they are the values upstream Yoga
/// produces for the same styles, so a regression here means the accessors drifted from Yoga.
/// </summary>
[TestClass]
public class BoxModelTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
    }

    private static View Box(string name, Styles styles) => new() { Name = name, Styles = styles };

    [TestMethod]
    public void MarginBottom_DoesNotShrinkTheBorderBox()
    {
        var root = Box("root", new Styles { Width = 340, Height = 600, FlexDirection = FlexDirection.Column });
        var card = Box("card", new Styles
        {
            Width = 100,
            Height = 40,
            MarginBottom = 16,
            FlexDirection = FlexDirection.Column,
            FlexShrink = 0,
        });
        root.AddChild(card);

        root.LayoutAndRender(new LuaVector2(340, 600));

        // The margin must not be taken out of the painted rect.
        Assert.AreEqual(40f, card.LayoutBorderSize.Y, 0.01f, "the border box is the layout height");
        Assert.AreEqual(100f, card.LayoutBorderSize.X, 0.01f);
        Assert.AreEqual(16f, card.LayoutMarginSize.Y - card.LayoutBorderSize.Y, 0.01f,
            "the margin box extends past the border box by the margin");

        // These are the rects BeginPaint hands to RenderBackground / RenderBorder / the clip.
        Assert.AreEqual(0f, card.LayoutBorderPosition.Y, 0.01f, "the card is the first child of the root");
        Assert.AreEqual(40f, card.LayoutPaddingSize.Y, 0.01f, "no border or padding, so padding box == border box");
        Assert.AreEqual(40f, card.LayoutContentSize.Y, 0.01f);
    }

    [TestMethod]
    public void MarginBoxPosition_StepsOutwardFromTheBorderBox()
    {
        var root = Box("root", new Styles { Width = 400, Height = 400, FlexDirection = FlexDirection.Column });
        var card = Box("card", new Styles
        {
            Height = 40,
            Width = 100,
            MarginTop = 10,
            MarginBottom = 16,
            MarginLeft = 7,
            MarginRight = 9,
            PaddingTop = 5,
            PaddingBottom = 6,
            PaddingLeft = 11,
            PaddingRight = 12,
            FlexShrink = 0,
        });
        root.AddChild(card);

        root.LayoutAndRender(new LuaVector2(400, 400));

        // Yoga: border-box corner at (7, 10), border-box size 100x40.
        Assert.AreEqual(7f, card.LayoutBorderPosition.X, 0.01f);
        Assert.AreEqual(10f, card.LayoutBorderPosition.Y, 0.01f);
        Assert.AreEqual(100f, card.LayoutBorderSize.X, 0.01f);
        Assert.AreEqual(40f, card.LayoutBorderSize.Y, 0.01f);

        // The margin box is one margin further out on every side.
        Assert.AreEqual(0f, card.LayoutMarginPosition.X, 0.01f);
        Assert.AreEqual(0f, card.LayoutMarginPosition.Y, 0.01f);
        Assert.AreEqual(116f, card.LayoutMarginSize.X, 0.01f, "100 + 7 + 9");
        Assert.AreEqual(66f, card.LayoutMarginSize.Y, 0.01f, "40 + 10 + 16");

        // The padding box is the border box inset by the border only — with no border, the two
        // coincide. The content box is one padding further in.
        Assert.AreEqual(7f, card.LayoutPaddingPosition.X, 0.01f, "border box + border left (none set)");
        Assert.AreEqual(10f, card.LayoutPaddingPosition.Y, 0.01f);
        Assert.AreEqual(100f, card.LayoutPaddingSize.X, 0.01f, "no border, so padding box == border box");
        Assert.AreEqual(40f, card.LayoutPaddingSize.Y, 0.01f);
        Assert.AreEqual(18f, card.LayoutContentPosition.X, 0.01f, "padding box + padding left");
        Assert.AreEqual(15f, card.LayoutContentPosition.Y, 0.01f);
        Assert.AreEqual(77f, card.LayoutContentSize.X, 0.01f, "100 - 11 - 12");
        Assert.AreEqual(29f, card.LayoutContentSize.Y, 0.01f, "40 - 5 - 6");
    }

    [TestMethod]
    public void BorderWidth_IsTakenOutOfThePaddingAndContentBoxes()
    {
        var root = Box("root", new Styles { Width = 400, Height = 400, FlexDirection = FlexDirection.Column });
        var card = Box("card", new Styles
        {
            Height = 40,
            Width = 100,
            BorderTop = 2,
            BorderBottom = 3,
            BorderLeft = 4,
            BorderRight = 5,
            PaddingTop = 6,
            PaddingBottom = 7,
            PaddingLeft = 8,
            PaddingRight = 9,
            FlexShrink = 0,
        });
        root.AddChild(card);

        root.LayoutAndRender(new LuaVector2(400, 400));

        Assert.AreEqual(100f, card.LayoutBorderSize.X, 0.01f);
        Assert.AreEqual(40f, card.LayoutBorderSize.Y, 0.01f);
        Assert.AreEqual(91f, card.LayoutPaddingSize.X, 0.01f, "100 - 4 - 5");
        Assert.AreEqual(35f, card.LayoutPaddingSize.Y, 0.01f, "40 - 2 - 3");
        Assert.AreEqual(4f, card.LayoutPaddingPosition.X - card.LayoutBorderPosition.X, 0.01f);
        Assert.AreEqual(2f, card.LayoutPaddingPosition.Y - card.LayoutBorderPosition.Y, 0.01f);
        Assert.AreEqual(74f, card.LayoutContentSize.X, 0.01f, "100 - 4 - 5 - 8 - 9");
        Assert.AreEqual(22f, card.LayoutContentSize.Y, 0.01f, "40 - 2 - 3 - 6 - 7");
        Assert.AreEqual(12f, card.LayoutContentPosition.X - card.LayoutBorderPosition.X, 0.01f);
        Assert.AreEqual(8f, card.LayoutContentPosition.Y - card.LayoutBorderPosition.Y, 0.01f);
    }

    /// <summary>
    /// The reported regression: a card followed by a sibling. The margin must push the sibling
    /// down by its full value rather than being absorbed into the card's own box.
    /// </summary>
    [TestMethod]
    public void CardMarginBottom_PushesTheNextSiblingDown()
    {
        var root = Box("root", new Styles { Width = 340, Height = 600, FlexDirection = FlexDirection.Column });
        var inner = Box("inner", new Styles { FlexDirection = FlexDirection.Column, FlexShrink = 0 });
        var card = Box("card", new Styles
        {
            Height = 72,
            Width = 120,
            MarginBottom = 16,
            FlexDirection = FlexDirection.Column,
            FlexShrink = 0,
        });
        var next = Box("next", new Styles { Height = 40, Width = 120, FlexShrink = 0 });
        inner.AddChild(card);
        inner.AddChild(next);
        root.AddChild(inner);

        root.LayoutAndRender(new LuaVector2(340, 600));

        Assert.AreEqual(72f, card.LayoutBorderSize.Y, 0.01f, "the card keeps its full height");
        Assert.AreEqual(72f + 16f, next.LayoutBorderPosition.Y, 0.01f,
            "the margin leaves a 16px gap below the card");
    }

    /// <summary>
    /// Scroll extent must still include a child's trailing margin, which Yoga's layout values
    /// do not fold in.
    /// </summary>
    [TestMethod]
    public void ScrollExtent_IncludesTrailingMarginOfLastChild()
    {
        var root = Box("root", new Styles
        {
            Width = 300,
            Height = 100,
            FlexDirection = FlexDirection.Column,
            Overflow = Overflow.Scroll,
            PaddingTop = 10,
            PaddingBottom = 10,
        });
        var child = Box("child", new Styles { Height = 100, Width = 100, FlexShrink = 0, MarginBottom = 20 });
        root.AddChild(child);

        root.LayoutAndRender(new LuaVector2(300, 100));

        // Content box is 80 tall; the child reaches 100 + its 20px trailing margin = 120 from the
        // content origin, so the scrollable extent is 40.
        Assert.AreEqual(40f, root.ScrollableHeight, 0.01f);
    }
}
