using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards <see cref="FocusManager.HandleMousePressed"/> / <see cref="FocusManager.HandleMouseReleased"/>.
/// A click on a focusable element focuses it; a click on a tree with no focusable
/// element clears focus (unfocuses whatever was focused); release clears the
/// pressed/active state but keeps keyboard focus.
/// </summary>
[TestClass]
public class FocusManagerMousePressTests
{
    private static View MakeRoot(float w = 200, float h = 200)
    {
        var root = new View { Name = "root" };
        root.Styles = root.Styles with { Width = w, Height = h };
        return root;
    }

    private static View MakeBox(string name, float left, float top, float width, float height, bool focusable = false)
    {
        var v = new View { Name = name, IsFocusable = focusable };
        v.Styles = v.Styles with
        {
            Position = Position.Absolute,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
        };
        return v;
    }

    private static BaseMouseEvent Press(float x, float y) =>
        new(new Vector2(x, y), MouseButton.Primary, MouseButtons.Primary, false, false, false);

    [TestInitialize]
    public void Setup()
    {
        FocusManager.ClearFocus();
        IBackend.Backend = new DummyBackend();
    }

    [TestMethod]
    public void HandleMousePressed_OnFocusableElement_FocusesIt()
    {
        var root = MakeRoot();
        var btn = MakeBox("btn", 0, 0, 100, 100, focusable: true);
        root.AddChild(btn);
        root.LayoutAndRender(new Vector2(200, 200));

        FocusManager.HandleMousePressed(root, Press(50, 50));

        Assert.AreSame(btn, FocusManager.FocusedNode);
        Assert.AreSame(btn, FocusManager.ActiveNode);
    }

    [TestMethod]
    public void HandleMousePressed_OnNonFocusableTree_ClearsFocus()
    {
        var root = MakeRoot();
        var btn = MakeBox("btn", 0, 0, 100, 100, focusable: true);
        var spacer = MakeBox("spacer", 100, 100, 100, 100); // non-focusable
        root.AddChild(btn);
        root.AddChild(spacer);
        root.LayoutAndRender(new Vector2(200, 200));

        // Focus the button first.
        FocusManager.HandleMousePressed(root, Press(50, 50));
        Assert.AreSame(btn, FocusManager.FocusedNode);

        // Click a non-focusable area → focus must be dropped.
        FocusManager.HandleMousePressed(root, Press(150, 150));

        Assert.IsNull(FocusManager.FocusedNode);
        Assert.IsNull(FocusManager.ActiveNode);
    }

    [TestMethod]
    public void HandleMousePressed_OnContainerWithFocusableChild_FocusesChild()
    {
        var root = MakeRoot();
        var container = MakeBox("container", 0, 0, 100, 100); // non-focusable
        var childBtn = MakeBox("childBtn", 10, 10, 50, 50, focusable: true);
        container.AddChild(childBtn);
        root.AddChild(container);
        root.LayoutAndRender(new Vector2(200, 200));

        FocusManager.HandleMousePressed(root, Press(30, 30));

        Assert.AreSame(childBtn, FocusManager.FocusedNode);
    }

    [TestMethod]
    public void HandleMouseReleased_ClearsActiveButKeepsFocus()
    {
        var root = MakeRoot();
        var btn = MakeBox("btn", 0, 0, 100, 100, focusable: true);
        root.AddChild(btn);
        root.LayoutAndRender(new Vector2(200, 200));

        FocusManager.HandleMousePressed(root, Press(50, 50));
        Assert.AreSame(btn, FocusManager.ActiveNode);

        FocusManager.HandleMouseReleased();

        Assert.IsNull(FocusManager.ActiveNode);
        // Keyboard focus survives release.
        Assert.AreSame(btn, FocusManager.FocusedNode);
    }
}
