using System.Numerics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class MenuTabTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();
        FocusManager.ClearHover();
    }

    [TestCleanup]
    public void Cleanup()
    {
        Renderer.Flush();
        FocusManager.ClearFocus();
        FocusManager.ClearHover();
    }

    [TestMethod]
    public void KeyboardNavigation_PrefersSelectedTabAndActivatesItsSibling()
    {
        var selected = new Signal<int>(1);
        View? first = null;
        View? second = null;
        using var mounted = Renderer.Mount(() => Sx.View(children: KeyboardNavigation.Row(Sx.View(children:
        [
            first = MenuTab.Create(() => "First", () => selected.Value == 0, () => selected.Value = 0),
            second = MenuTab.Create(() => "Second", () => selected.Value == 1, () => selected.Value = 1)
        ]))));
        var root = (View)mounted.Root;
        root.LayoutAndRender(new Vector2(800, 600));
        var navigation = new KeyboardNavigation();
        navigation.UpdateFocus(root);
        Assert.AreSame(second, FocusManager.FocusedNode);

        Assert.IsTrue(navigation.HandleKey(root, Key.Left));
        Assert.AreSame(first, FocusManager.FocusedNode);
        Assert.AreEqual(1, selected.Value, "Focus alone must not change the selected page.");
        Assert.IsTrue(navigation.HandleKey(root, Key.Enter));
        Renderer.Flush();
        Assert.AreEqual(0, selected.Value);
        Assert.AreNotEqual(first!.Styles.BackgroundColor, second!.Styles.BackgroundColor);
    }

    [TestMethod]
    public void HostNavigation_HighlightsTabWithoutTakingKeyboardFocus()
    {
        var highlighted = new Signal<bool>(false);
        var label = new Signal<string>("Collection");
        View? tab = null;
        using var mounted = Renderer.Mount(() => tab = MenuTab.Create(
            () => label.Value,
            () => true,
            () => { },
            highlighted: () => highlighted.Value,
            keyboardNavigation: false
        ));
        var text = tab!.Children.OfType<Text>().Single();
        var underline = tab.Children.OfType<View>().Single();
        var selectedColor = underline.Styles.BackgroundColor;
        highlighted.Value = true;
        label.Value = "Online";
        Renderer.Flush();
        Assert.AreNotEqual(selectedColor, underline.Styles.BackgroundColor);
        Assert.AreEqual("Online", text.Children.OfType<TextNode>().Single().Text);
        Assert.IsFalse(tab.IsFocusable, "The garage controller, not this tab, owns arrow navigation.");
        highlighted.Value = false;
        Renderer.Flush();
        Assert.AreEqual(selectedColor, underline.Styles.BackgroundColor);
    }
}
