using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Regression for the in-game crash "System.ArgumentOutOfRangeException: Index must be
/// within the bounds of the List" at YogaNode.InsertChild, triggered by the Sx renderer's
/// insertBefore: the host computed the insert index from <c>VisualChildren</c> (all nodes,
/// including TextNode anchors) but passed it to <c>YogaNode.InsertChild</c>, which only
/// counts Components — so inserting before a TextNode anchor that follows Components threw.
/// </summary>
[TestClass]
public class ComponentChildCollectionTests
{
    [TestMethod]
    public void InsertBefore_TextNodeAnchorAfterComponents_DoesNotThrow()
    {
        // Mirrors the LuaUiLibrary.InsertBefore path: anchor TextNodes interleaved with
        // Component children, then inserting a Component before the trailing anchor.
        var parent = new View { Name = "parent" };
        var anchor1 = new TextNode { Text = "" };
        var a = new View { Name = "a" };
        var b = new View { Name = "b" };
        var c = new View { Name = "c" };
        var d = new View { Name = "d" };
        var anchor2 = new TextNode { Text = "" };

        parent.AddChild(anchor1); // index 0 (TextNode, not in YogaNode)
        parent.AddChild(a);       // index 1
        parent.AddChild(b);       // index 2
        parent.AddChild(c);       // index 3
        parent.AddChild(d);       // index 4
        parent.AddChild(anchor2); // index 5 (TextNode, not in YogaNode) -> 4 Components before it

        // Insert a Component before anchor2. VisualChildren.IndexOf(anchor2) == 5, but the
        // Yoga node only has 4 children; the fix maps to the Component-only index (4).
        var x = new View { Name = "x" };
        parent.InsertAt(parent.VisualChildren.IndexOf(anchor2), x);

        Assert.AreEqual(7, parent.Children.Count, "x inserted before anchor2");
        Assert.AreSame(x, parent.Children[5], "x sits right before the trailing anchor");
        Assert.AreSame(anchor2, parent.Children[6], "anchor2 is last");
    }

    [TestMethod]
    public void InsertBefore_TextNodeAnchorAtStart_StillWorks()
    {
        var parent = new View { Name = "parent" };
        var anchor = new TextNode { Text = "" };
        var a = new View { Name = "a" };
        parent.AddChild(anchor); // index 0
        parent.AddChild(a);      // index 1

        var x = new View { Name = "x" };
        parent.InsertAt(parent.VisualChildren.IndexOf(anchor), x);

        Assert.AreSame(x, parent.Children[0], "x inserted before the leading anchor");
        Assert.AreSame(anchor, parent.Children[1]);
        Assert.AreSame(a, parent.Children[2]);
    }

    [TestMethod]
    public void AppendChild_MixedTextAndComponents_KeepsYogaOrder()
    {
        var parent = new View { Name = "parent" };
        var anchor = new TextNode { Text = "" };
        var a = new View { Name = "a" };
        parent.AddChild(anchor);
        parent.AddChild(a);

        var x = new View { Name = "x" };
        parent.AddChild(x); // append -> must go to Yoga index 1 (after a), not 2

        Assert.AreSame(anchor, parent.Children[0]);
        Assert.AreSame(a, parent.Children[1]);
        Assert.AreSame(x, parent.Children[2]);
    }
}
