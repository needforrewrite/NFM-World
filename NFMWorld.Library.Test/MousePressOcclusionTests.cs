using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards the occlusion-aware mouse press/release dispatch in <see cref="Component"/>.
/// <see cref="Component.DispatchMousePressed"/> must only deliver the event to the topmost
/// hit-test chain under the cursor (like focus/hover), NOT every element whose bounds
/// contain the point — otherwise clicking a z-indexed popup (e.g. a dropdown option)
/// would ALSO trigger an occluded dropdown whose trigger sits beneath the popup.
/// </summary>
[TestClass]
public class MousePressOcclusionTests
{
    private static View MakeRoot()
    {
        var root = new View { Name = "root" };
        root.Styles = root.Styles with { Width = 200, Height = 200 };
        return root;
    }

    private static View MakeBox(string name, int z, float left, float top, float width, float height)
    {
        var v = new View { Name = name };
        v.Styles = v.Styles with
        {
            Position = Position.Absolute,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            ZIndex = z,
        };
        return v;
    }

    private static BaseMouseEvent Press(float x, float y) =>
        new(new Vector2(x, y), MouseButton.Primary, MouseButtons.Primary, false, false, false);

    [TestMethod]
    public void DispatchMousePressed_HighZPopup_DoesNotTriggerOccludedSibling()
    {
        var root = MakeRoot();
        var popup = MakeBox("popup", 1000, 0, 0, 100, 100);     // z-indexed (e.g. dropdown popup)
        var occluded = MakeBox("occluded", 0, 0, 0, 100, 100);  // later sibling, drawn beneath
        root.AddChild(occluded);
        root.AddChild(popup);

        int popupPresses = 0, occludedPresses = 0;
        popup.MousePressed += _ => popupPresses++;
        occluded.MousePressed += _ => occludedPresses++;

        IBackend.Backend = new DummyBackend();
        root.LayoutAndRender(new Vector2(200, 200));

        root.DispatchMousePressed(Press(50, 50));

        Assert.AreEqual(1, popupPresses, "topmost (high-z) popup must receive the press");
        Assert.AreEqual(0, occludedPresses, "occluded sibling beneath the popup must NOT receive the press");
    }

    [TestMethod]
    public void DispatchMousePressed_NoOverlap_TargetAndAncestorsFire()
    {
        var root = MakeRoot();
        var parent = new View { Name = "parent" };
        parent.Styles = parent.Styles with { Width = 100, Height = 100 };
        var child = new View { Name = "child" };
        child.Styles = child.Styles with { Width = 50, Height = 50 };
        parent.AddChild(child);
        root.AddChild(parent);

        int rootPresses = 0, parentPresses = 0, childPresses = 0;
        root.MousePressed += _ => rootPresses++;
        parent.MousePressed += _ => parentPresses++;
        child.MousePressed += _ => childPresses++;

        IBackend.Backend = new DummyBackend();
        root.LayoutAndRender(new Vector2(200, 200));

        root.DispatchMousePressed(Press(25, 25)); // over `child`

        Assert.AreEqual(1, rootPresses, "root ancestor still receives the press");
        Assert.AreEqual(1, parentPresses, "parent ancestor still receives the press");
        Assert.AreEqual(1, childPresses, "topmost target receives the press");
    }
}
