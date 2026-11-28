using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class MessageWindowTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();
    }

    [TestMethod]
    public void ConfirmationDefaultsToCancelAndKeyboardCanChooseConfirm()
    {
        var message = new MessageWindow();
        MessageWindow.MessageResult? result = null;
        message.ShowOKCancel("Confirm", "Continue?", value => result = value);
        Assert.IsTrue(message.HandleKey(Key.Enter));
        Assert.AreEqual(MessageWindow.MessageResult.Cancel, result);
        Assert.IsFalse(message.IsOpen);

        message.ShowOKCancel("Confirm", "Continue?", value => result = value);
        message.HandleKey(Key.Left);
        message.HandleKey(Key.Enter);
        Assert.AreEqual(MessageWindow.MessageResult.OK, result);
    }

    [TestMethod]
    public void CompletingAMessageCanOpenAnotherMessageWithoutClosingIt()
    {
        var message = new MessageWindow();
        message.ShowMessage("First", "First message", _ => message.ShowMessage("Next", "Next message"));
        message.HandleKey(Key.Enter);
        Assert.IsTrue(message.IsOpen);
        message.HandleKey(Key.Escape);
        Assert.IsFalse(message.IsOpen);
    }

    [TestMethod]
    public void SxDialogBlocksBackgroundAndMouseInvokesTheChosenCallbackOnce()
    {
        var message = new MessageWindow();
        var root = new View { Styles = Sx.Styles(width: 800, height: 600) };
        var background = new View { Styles = Sx.Styles(width: 800, height: 600) };
        var backgroundClicks = 0;
        var callbacks = 0;
        background.MousePressed = _ => backgroundClicks++;
        root.AddChild(background);
        using var mounted = Renderer.Mount(() =>
            {
                root.AddChild(message.BuildUi());

                return root;
            }
        );
        message.ShowMessage("Update required", "This server uses protocol v2; this build uses protocol v3.", _ => callbacks++);
        Renderer.Flush();
        root.LayoutAndRender(new Vector2(800, 600));
        var dialog = message.ActiveView!;
        var button = Descendants(dialog).Single(node => node.Name == "Message action OK");
        var point = button.LayoutBorderPosition + button.LayoutBorderSize / 2;
        root.DispatchMousePressed(new BaseMouseEvent(point, MouseButton.Primary, MouseButtons.Primary, false, false, false));
        Renderer.Flush();
        Assert.AreEqual(1, callbacks);
        Assert.AreEqual(0, backgroundClicks);
        Assert.IsFalse(message.IsOpen);
    }

    private static IEnumerable<Component> Descendants(Node root)
    {
        if (root is Component component)

            yield return component;

        foreach (var child in root.VisualChildren)

        foreach (var descendant in Descendants(child))

            yield return descendant;
    }
}
