using System.Numerics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards z-index aware hit-testing in <see cref="FocusManager"/>. A higher z-index
/// must win occlusion over a later tree sibling (e.g. a dropdown popup over content
/// ordered after it), while equal z-index keeps the later-sibling-wins rule. Also
/// verifies a high-z box only occludes inside its own bounds.
/// </summary>
[TestClass]
public class ZIndexHitTestTests
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

    [TestMethod]
    public void HitTest_NoZIndex_LaterSiblingWins()
    {
        var root = MakeRoot();
        var back = MakeBox("back", 0, 0, 0, 100, 100);
        var front = MakeBox("front", 0, 0, 0, 100, 100);
        root.AddChild(back);
        root.AddChild(front);

        IBackend.Backend = new DummyBackend();
        root.LayoutAndRender(new Vector2(200, 200));

        Assert.AreEqual(front, FocusManager.HitTest(root, new Vector2(50, 50)));
    }

    [TestMethod]
    public void HitTest_HigherZIndex_EarlierSiblingWinsOcclusion()
    {
        var root = MakeRoot();
        var back = MakeBox("back", 5, 0, 0, 100, 100);   // earlier, higher z
        var front = MakeBox("front", 0, 0, 0, 100, 100); // later, lower z
        root.AddChild(back);
        root.AddChild(front);

        IBackend.Backend = new DummyBackend();
        root.LayoutAndRender(new Vector2(200, 200));

        Assert.AreEqual(back, FocusManager.HitTest(root, new Vector2(50, 50)));
    }

    [TestMethod]
    public void HitTest_HigherZIndex_DoesNotOccludeOutsideOwnBounds()
    {
        var root = MakeRoot();
        var back = MakeBox("back", 5, 0, 0, 50, 50);       // high z, small, top-left
        var front = MakeBox("front", 0, 60, 60, 100, 100); // later, lower-right
        root.AddChild(back);
        root.AddChild(front);

        IBackend.Backend = new DummyBackend();
        root.LayoutAndRender(new Vector2(200, 200));

        // (120,120) is over `front` only, not `back`.
        Assert.AreEqual(front, FocusManager.HitTest(root, new Vector2(120, 120)));
    }
}
