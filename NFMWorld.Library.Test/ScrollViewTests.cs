using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.Library.Test;

[TestClass]
public class ScrollViewTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();
    }

    private static (View Root, ScrollView Scroll) Create()
    {
        var root = new View { Styles = Sx.Styles(width: 200, height: 100) };
        var scroll = new ScrollView
        {
            Styles = Sx.Styles(
                width: 200,
                height: 100,
                overflow: Overflow.Scroll,
                flexDirection: FlexDirection.Column,
                paddingRight: 10
            )
        };
        scroll.AddChild(new View { Styles = Sx.Styles(height: 500, flexShrink: 0) });
        root.AddChild(scroll);
        root.LayoutAndRender(new Vector2(200, 100));

        return (root, scroll);
    }

    private static BaseMouseEvent Press(Vector2 position)
        => new(position, MouseButton.Primary, MouseButtons.Primary, false, false, false);

    private static BaseMouseDragEvent Drag(Vector2 start, Vector2 position)
        => new(start, position, (byte)MouseButton.Primary, MouseButtons.Primary, false, false, false);

    [TestMethod]
    public void ThumbCanBeDraggedOutsideTheTrackAndStopsOnRelease()
    {
        var (root, scroll) = Create();
        var start = new Vector2(197, 10);
        root.DispatchMousePressed(Press(start));
        Assert.AreEqual(0f, scroll.ScrollTop, 0.01f);
        root.DispatchMouseDragged(Drag(start, new Vector2(250, 90)));
        Assert.AreEqual(scroll.ScrollableHeight, scroll.ScrollTop, 0.01f);
        root.DispatchMouseReleased(Press(new Vector2(197, 90)));
        root.DispatchMouseDragged(Drag(start, new Vector2(197, 10)));
        Assert.AreEqual(scroll.ScrollableHeight, scroll.ScrollTop, 0.01f);
    }

    [TestMethod]
    public void TrackClickScrollsButContentClickDoesNot()
    {
        var (root, scroll) = Create();
        root.DispatchMousePressed(Press(new Vector2(50, 80)));
        Assert.AreEqual(0f, scroll.ScrollTop);
        root.DispatchMousePressed(Press(new Vector2(197, 80)));
        Assert.IsTrue(scroll.ScrollTop > 0);
    }

    [TestMethod]
    public void ModalOverScrollbarPreventsDragging()
    {
        var (root, scroll) = Create();
        root.AddChild(new View
        {
            Styles = Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                width: 200,
                height: 100,
                zIndex: 60
            )
        });
        root.LayoutAndRender(new Vector2(200, 100));
        var start = new Vector2(197, 10);
        root.DispatchMousePressed(Press(start));
        root.DispatchMouseDragged(Drag(start, new Vector2(197, 90)));
        Assert.AreEqual(0f, scroll.ScrollTop);
    }

    [TestMethod]
    public void WheelScrollsAnOpenDropdownOutsideTheCardAndKeepsTheListBehindStill()
    {
        var (root, background) = Create();
        var overlay = new View
        {
            Styles = Sx.Styles(position: Position.Absolute, left: 0, top: 0, right: 0, bottom: 0, zIndex: 60)
        };
        var card = new View
        {
            ClipOnlyWhenScrollable = true,
            Styles = Sx.Styles(width: 120, height: 40, overflow: Overflow.Scroll, flexDirection: FlexDirection.Column)
        };
        var anchor = new View { Styles = Sx.Styles(position: Position.Relative, height: 20, flexShrink: 0) };
        var dropdown = new View
        {
            Styles = Sx.Styles(
                position: Position.Absolute,
                top: 20,
                width: 100,
                height: 65,
                zIndex: 1000,
                overflow: Overflow.Scroll,
                flexDirection: FlexDirection.Column
            )
        };
        dropdown.AddChild(new View { Styles = Sx.Styles(height: 200, flexShrink: 0) });
        anchor.AddChild(dropdown);
        card.AddChild(anchor);
        overlay.AddChild(card);
        root.AddChild(overlay);
        root.LayoutAndRender(new Vector2(200, 100));

        Assert.IsFalse(card.IsClipping);
        var overDropdown = new BaseMouseWheelEvent(
            new Vector3(0, -300, 0),
            new Vector2(50, 60),
            MouseButtons.None,
            false,
            false,
            false
        );
        Assert.IsTrue(root.DispatchMouseScrolled(overDropdown));
        Assert.AreEqual(dropdown.ScrollableHeight, dropdown.ScrollTop, 0.01f);

        root.DispatchMouseScrolled(overDropdown);
        root.DispatchMouseScrolled(overDropdown with { Position = new Vector2(150, 60) });
        Assert.AreEqual(0f, background.ScrollTop);

        card.AddChild(new View { Styles = Sx.Styles(height: 60, flexShrink: 0) });
        root.LayoutAndRender(new Vector2(200, 100));
        Assert.IsTrue(card.IsClipping);
        Assert.IsTrue(card.ScrollableHeight > 0);
    }
}
