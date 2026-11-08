using System.Numerics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
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
        var root = Box("root", Sx.Styles(width: 340, height: 600, flexDirection: FlexDirection.Column));
        var card = Box(
            "card",
            Sx.Styles(
                width: 100,
                height: 40,
                marginBottom: 16,
                flexDirection: FlexDirection.Column,
                flexShrink: 0
            )
        );
        root.AddChild(card);

        root.LayoutAndRender(new Vector2(340, 600));

        // The margin must not be taken out of the painted rect.
        Assert.AreEqual(40f, card.LayoutBorderSize.Y, 0.01f, "the border box is the layout height");
        Assert.AreEqual(100f, card.LayoutBorderSize.X, 0.01f);
        Assert.AreEqual(
            16f,
            card.LayoutMarginSize.Y - card.LayoutBorderSize.Y,
            0.01f,
            "the margin box extends past the border box by the margin"
        );

        // These are the rects BeginPaint hands to RenderBackground / RenderBorder / the clip.
        Assert.AreEqual(0f, card.LayoutBorderPosition.Y, 0.01f, "the card is the first child of the root");
        Assert.AreEqual(40f, card.LayoutPaddingSize.Y, 0.01f, "no border or padding, so padding box == border box");
        Assert.AreEqual(40f, card.LayoutContentSize.Y, 0.01f);
    }

    [TestMethod]
    public void MarginBoxPosition_StepsOutwardFromTheBorderBox()
    {
        var root = Box("root", Sx.Styles(width: 400, height: 400, flexDirection: FlexDirection.Column));
        var card = Box(
            "card",
            Sx.Styles(
                height: 40,
                width: 100,
                marginTop: 10,
                marginBottom: 16,
                marginLeft: 7,
                marginRight: 9,
                paddingTop: 5,
                paddingBottom: 6,
                paddingLeft: 11,
                paddingRight: 12,
                flexShrink: 0
            )
        );
        root.AddChild(card);

        root.LayoutAndRender(new Vector2(400, 400));

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
        var root = Box("root", Sx.Styles(width: 400, height: 400, flexDirection: FlexDirection.Column));
        var card = Box(
            "card",
            Sx.Styles(
                height: 40,
                width: 100,
                borderTop: 2,
                borderBottom: 3,
                borderLeft: 4,
                borderRight: 5,
                paddingTop: 6,
                paddingBottom: 7,
                paddingLeft: 8,
                paddingRight: 9,
                flexShrink: 0
            )
        );
        root.AddChild(card);

        root.LayoutAndRender(new Vector2(400, 400));

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
        var root = Box("root", Sx.Styles(width: 340, height: 600, flexDirection: FlexDirection.Column));
        var inner = Box("inner", Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0));
        var card = Box(
            "card",
            Sx.Styles(
                height: 72,
                width: 120,
                marginBottom: 16,
                flexDirection: FlexDirection.Column,
                flexShrink: 0
            )
        );
        var next = Box("next", Sx.Styles(height: 40, width: 120, flexShrink: 0));
        inner.AddChild(card);
        inner.AddChild(next);
        root.AddChild(inner);

        root.LayoutAndRender(new Vector2(340, 600));

        Assert.AreEqual(72f, card.LayoutBorderSize.Y, 0.01f, "the card keeps its full height");
        Assert.AreEqual(
            72f + 16f,
            next.LayoutBorderPosition.Y,
            0.01f,
            "the margin leaves a 16px gap below the card"
        );
    }

    /// <summary>
    /// Scroll extent must still include a child's trailing margin, which Yoga's layout values
    /// do not fold in.
    /// </summary>
    [TestMethod]
    public void ScrollExtent_IncludesTrailingMarginOfLastChild()
    {
        var root = Box(
            "root",
            Sx.Styles(
                width: 300,
                height: 100,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll,
                paddingTop: 10,
                paddingBottom: 10
            )
        );
        var child = Box("child", Sx.Styles(height: 100, width: 100, flexShrink: 0, marginBottom: 20));
        root.AddChild(child);

        root.LayoutAndRender(new Vector2(300, 100));

        // Content box is 80 tall; the child reaches 100 + its 20px trailing margin = 120 from the
        // content origin, so the scrollable extent is 40.
        Assert.AreEqual(40f, root.ScrollableHeight, 0.01f);
    }

    /// <summary>
    /// A flow host (<c>Display.Contents</c>) is flattened by Yoga and carries a zero-size box, so
    /// measuring only direct children loses everything inside it - which is how a list of rows
    /// rendered by <c>Sx.For</c> ended up with no scrollable extent at all.
    /// </summary>
    [TestMethod]
    public void ScrollExtent_DescendsIntoFlowHosts()
    {
        var root = Box(
            "root",
            Sx.Styles(
                width: 300,
                height: 100,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll
            )
        );
        var host = Box("host", Sx.Styles(display: Display.Contents));
        host.AddChild(Box("row", Sx.Styles(height: 60, width: 100, flexShrink: 0)));
        host.AddChild(Box("row2", Sx.Styles(height: 60, width: 100, flexShrink: 0)));
        root.AddChild(host);

        root.LayoutAndRender(new Vector2(300, 100));

        // The rows lay out in the root itself: 120 of content in a 100-tall box.
        Assert.AreEqual(0f, host.LayoutBorderSize.Y, 0.01f, "a flow host takes no box");
        Assert.AreEqual(20f, root.ScrollableHeight, 0.01f);
    }
}
