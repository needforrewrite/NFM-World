using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards UI rescaling (<see cref="Component.NotifyUiScaleChanged"/>) for a subtree mounted
/// while the UI scale is not 1 — e.g. the Sx devtools panel reopened on a maximized window.
///
/// Two things have to line up for such a subtree to follow a later scale change:
/// <list type="number">
/// <item>each node must record the scale its styles were actually applied at, and</item>
/// <item>the rescale walk only descends into a subtree whose own scale changed.</item>
/// </list>
/// A node left at the field's default <c>_lastScale</c> of 1 believes it is already up to
/// date, so <see cref="Component.Rescale"/> reports "no change" and the walk stops there —
/// stranding the node and every descendant at the size they were mounted at.
/// </summary>
[TestClass]
public class UiScaleRescaleTests
{
    [TestInitialize]
    public void Setup()
    {
        // A fresh backend each test, so Scale starts at 1.
        IBackend.Backend = new DummyBackend();
    }

    private static View MakeBox(string name, float size)
    {
        var v = new View { Name = name };
        v.Styles = v.Styles with { Width = size, Height = size };
        return v;
    }

    [TestMethod]
    public void SubtreeMountedAtNonDefaultScale_RescalesWhenScaleReturnsToDefault()
    {
        IBackend.Backend.Graphics.Scale = 2f;

        var root = MakeBox("root", 200);
        root.LayoutAndRender(new LuaVector2(400, 400));

        // Mounted while the scale is already 2: the root's own scale does not change on this
        // pass, so nothing walks down into the new subtree.
        var child = MakeBox("child", 100);
        var grandchild = MakeBox("grandchild", 50);
        child.AddChild(grandchild);
        root.AddChild(child);
        root.LayoutAndRender(new LuaVector2(400, 400));

        Assert.AreEqual(200f, child.LayoutWidth, 0.01f, "child is scaled by 2 when it is mounted");
        Assert.AreEqual(100f, grandchild.LayoutWidth, 0.01f, "grandchild is scaled by 2 too");

        IBackend.Backend.Graphics.Scale = 1f;
        root.LayoutAndRender(new LuaVector2(200, 200));

        Assert.AreEqual(200f, root.LayoutWidth, 0.01f);
        Assert.AreEqual(100f, child.LayoutWidth, 0.01f, "child must rescale with the root");
        Assert.AreEqual(50f, grandchild.LayoutWidth, 0.01f,
            "the walk must descend through the child to reach the grandchild");
    }

    [TestMethod]
    public void NodeMountedAtDefaultScale_RescalesWhenScaleChanges()
    {
        var root = MakeBox("root", 200);
        root.LayoutAndRender(new LuaVector2(200, 200));

        var child = MakeBox("child", 100);
        root.AddChild(child);
        root.LayoutAndRender(new LuaVector2(200, 200));

        Assert.AreEqual(100f, child.LayoutWidth, 0.01f);

        IBackend.Backend.Graphics.Scale = 2f;
        root.LayoutAndRender(new LuaVector2(400, 400));

        Assert.AreEqual(200f, child.LayoutWidth, 0.01f, "child must grow with the scale");
    }
}
