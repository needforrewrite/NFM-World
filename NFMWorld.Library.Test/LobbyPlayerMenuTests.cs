using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI;
using NFMWorld.UI.Components;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class LobbyPlayerMenuTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();
        FocusManager.ClearHover();
    }

    [TestMethod]
    public void OutsidePress_ClosesAboveElevatedControlsWithoutActivatingThem()
    {
        var root = new View { Styles = Sx.Styles(width: 800, height: 600) };
        var backgroundPresses = 0;
        var closed = 0;
        root.AddChild(Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                right: 0,
                bottom: 0,
                zIndex: 1000
            ),
            onmousedown: _ => backgroundPresses++
        )
        );
        using var mounted = Renderer.Mount(() =>
            {
                root.AddChild(LobbyPlayerMenu.Build(
                    new Vector2(100, 100),
                    copyCar: () => { },
                    copyColors: () => { },
                    close: () => closed++,
                    canInvite: () => false,
                    invite: () => { }
                )
                );
                return root;
            }
        );
        root.LayoutAndRender(new Vector2(800, 600));

        root.DispatchMousePressed(Press(new Vector2(700, 500), MouseButton.Primary));
        root.DispatchMousePressed(Press(new Vector2(20, 20), MouseButton.Secondary));

        Assert.AreEqual(2, closed);
        Assert.AreEqual(0, backgroundPresses);
    }

    [TestMethod]
    public void MenuItem_HighlightsOnHoverAndInvokesOnlyItsOwnAction()
    {
        var root = new View { Styles = Sx.Styles(width: 800, height: 600) };
        var cars = 0;
        var colors = 0;
        var closed = 0;
        using var mounted = Renderer.Mount(() =>
            {
                root.AddChild(LobbyPlayerMenu.Build(
                    new Vector2(100, 100),
                    copyCar: () => cars++,
                    copyColors: () => colors++,
                    close: () => closed++,
                    canInvite: () => false,
                    invite: () => { }
                )
                );
                return root;
            }
        );
        root.LayoutAndRender(new Vector2(800, 600));
        var item = Descendants(root).Single(node => node.Name == "Copy colors");
        var point = item.LayoutBorderPosition + item.LayoutBorderSize / 2;
        FocusManager.DispatchMouseMove(root, new BaseMouseMoveEvent(point, default, false, false, false));
        Renderer.Flush();
        Assert.AreEqual(Theme.MenuHighlight, item.Styles.BackgroundColor);

        root.DispatchMousePressed(Press(point, MouseButton.Primary));
        Assert.AreEqual(0, cars);
        Assert.AreEqual(1, colors);
        Assert.AreEqual(0, closed);

        FocusManager.DispatchMouseMove(root, new BaseMouseMoveEvent(new Vector2(700, 500), default, false, false, false));
        Renderer.Flush();
        Assert.AreEqual(Theme.Surface, item.Styles.BackgroundColor);
    }

    private static BaseMouseEvent Press(Vector2 point, MouseButton button)
        => new(point, button, default, false, false, false);

    private static IEnumerable<Component> Descendants(Node root)
    {
        if (root is Component component) yield return component;
        foreach (var child in root.VisualChildren)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }
}
