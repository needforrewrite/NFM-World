using System.Numerics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.Library.Test;

[TestClass]
public class KeyboardNavigationTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();
    }

    private static View Control(Func<Key, bool>? handle = null, bool preferred = false)
        => KeyboardNavigation.Bind(new View(), handle ?? (_ => false), _ => { }, () => preferred);

    [TestMethod]
    public void ArrowsNavigateRowsWithoutWalkingEveryTab()
    {
        var navigation = new KeyboardNavigation();
        var root = new View();
        var tabs = KeyboardNavigation.Row(new View());
        var keyboard = Control();
        var video = Control(preferred: true);
        var audio = Control();
        tabs.AddChild(keyboard);
        tabs.AddChild(video);
        tabs.AddChild(audio);
        var setting = Control();
        root.AddChild(tabs);
        root.AddChild(setting);

        navigation.UpdateFocus(root);
        Assert.AreSame(video, FocusManager.FocusedNode);
        Assert.IsTrue(navigation.HandleKey(root, Key.Right));
        Assert.AreSame(audio, FocusManager.FocusedNode);
        navigation.HandleKey(root, Key.Down);
        Assert.AreSame(setting, FocusManager.FocusedNode);
        navigation.HandleKey(root, Key.Up);
        Assert.IsTrue(tabs.VisualChildren.Contains(FocusManager.FocusedNode));
    }

    [TestMethod]
    public void HiddenAncestorsAndUnfocusableControlsAreSkipped()
    {
        var root = new View();
        var first = Control();
        var hidden = new View { Styles = Sx.Styles(visibility: Visibility.Hidden) };
        hidden.AddChild(Control());
        root.AddChild(first);
        root.AddChild(hidden);
        root.AddChild(new View());
        var last = Control();
        root.AddChild(last);
        var navigation = new KeyboardNavigation();

        navigation.HandleKey(root, Key.Down);
        Assert.AreSame(last, FocusManager.FocusedNode);
        navigation.HandleKey(root, Key.Down);
        Assert.AreSame(first, FocusManager.FocusedNode);
    }

    [TestMethod]
    public void EnterOpensModalWithoutActivatingItsButtonAndRestoresFocusOnClose()
    {
        var root = new View();
        var modal = KeyboardNavigation.Scope(new View { Styles = Sx.Styles(display: Display.None) });
        var activations = 0;
        var confirm = Control(key =>
            {
                if (key != Key.Enter) return false;
                activations++;
                modal.Styles = modal.Styles with { Display = Display.None };
                return true;
            }
        );
        modal.AddChild(confirm);
        var opener = Control(key =>
            {
                if (key != Key.Enter) return false;
                modal.Styles = modal.Styles with { Display = Display.Flex };
                return true;
            }
        );
        root.AddChild(opener);
        root.AddChild(Control());
        root.AddChild(modal);
        var navigation = new KeyboardNavigation();

        navigation.HandleKey(root, Key.Enter);
        Assert.AreEqual(0, activations);
        navigation.UpdateFocus(root);
        Assert.AreSame(confirm, FocusManager.FocusedNode);
        navigation.HandleKey(root, Key.Down);
        Assert.AreSame(confirm, FocusManager.FocusedNode);
        navigation.HandleKey(root, Key.Enter);
        Assert.AreEqual(1, activations);
        navigation.UpdateFocus(root);
        Assert.AreSame(opener, FocusManager.FocusedNode);
    }

    [TestMethod]
    public void ControlCanConsumeArrowAndEscapeWithoutMovingFocus()
    {
        var moves = 0;
        var dropdown = Control(key =>
            {
                if (key == Key.Down)
                {
                    moves++;
                    return true;
                }

                return key == Key.Escape;
            }
        );
        var root = new View();
        root.AddChild(dropdown);
        root.AddChild(Control());
        var navigation = new KeyboardNavigation();

        Assert.IsTrue(navigation.HandleKey(root, Key.Down));
        Assert.AreEqual(1, moves);
        Assert.AreSame(dropdown, FocusManager.FocusedNode);
        Assert.IsTrue(navigation.HandleKey(root, Key.Escape));
        Assert.IsFalse(navigation.HandleKey(root, Key.A));
    }

    [TestMethod]
    public void TextInputKeepsItsCaretKeysAndEnter()
    {
        var input = new TextInput();
        var root = new View();
        root.AddChild(input);
        root.AddChild(Control());
        var navigation = new KeyboardNavigation();

        Assert.IsFalse(navigation.HandleKey(root, Key.Left));
        Assert.IsFalse(navigation.HandleKey(root, Key.Right));
        Assert.IsFalse(navigation.HandleKey(root, Key.Enter));
        Assert.IsFalse(navigation.HandleKey(root, Key.Space));
        Assert.AreSame(input, FocusManager.FocusedNode);
        navigation.HandleKey(root, Key.Down);
        Assert.AreNotSame(input, FocusManager.FocusedNode);
    }

    [TestMethod]
    public void RebuildingASettingKeepsFocusOnItsRow()
    {
        var root = new View();
        root.AddChild(Control());
        root.AddChild(Control());
        var navigation = new KeyboardNavigation();
        navigation.HandleKey(root, Key.Down);
        root.RemoveAt(1);
        var replacement = Control();
        root.AddChild(replacement);

        navigation.UpdateFocus(root);

        Assert.AreSame(replacement, FocusManager.FocusedNode);
    }

    [TestMethod]
    public void NavigatingOffscreenScrollsTheFocusedSettingIntoView()
    {
        var root = new View { Styles = Sx.Styles(width: 300, height: 100) };
        var scroll = new View
        {
            Styles = Sx.Styles(width: 300, height: 100, overflow: Overflow.Scroll, flexDirection: FlexDirection.Column)
        };
        root.AddChild(scroll);
        var controls = Enumerable.Range(0, 6).Select(_ => Control()).ToArray();
        foreach (var control in controls)
        {
            control.Styles = Sx.Styles(height: 50, flexShrink: 0);
            scroll.AddChild(control);
        }

        root.LayoutAndRender(new Vector2(300, 100));
        var navigation = new KeyboardNavigation();
        navigation.UpdateFocus(root);
        for (var i = 0; i < 5; i++) navigation.HandleKey(root, Key.Down);

        Assert.AreSame(controls[^1], FocusManager.FocusedNode);
        Assert.IsTrue(scroll.ScrollTop > 0);
    }
}
