using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class AccountViewTests
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
        AccountView.Reset();
        Renderer.Flush();
        FocusManager.ClearFocus();
        FocusManager.ClearHover();
    }

    [TestMethod]
    public void SignedOutDialog_OffersSignInAndBrowserRegistrationOnly()
    {
        AccountView.Reset();
        using var mounted = Renderer.Mount(() => AccountView.Build(
                new AccountView.Actions((_, _) => { }, () => { }, () => { }, AccountView.Hide),
                () => false,
                () => ""
            )
        );
        AccountView.Show();
        AccountView.SetStatus("Sign in to play multiplayer.", false);
        Renderer.Flush();

        var labels = Descendants(mounted.Root).OfType<TextNode>().Select(t => t.Text).ToArray();
        CollectionAssert.Contains(labels, "SIGN IN");
        CollectionAssert.Contains(labels, "REGISTER IN BROWSER");
        CollectionAssert.Contains(labels, "Sign in to play multiplayer.");
        CollectionAssert.DoesNotContain(labels, "CREATE ACCOUNT");
        CollectionAssert.DoesNotContain(labels, "CREATE");
    }

    [TestMethod]
    public void ClosingDialog_RemovesItsFields()
    {
        AccountView.Reset();
        AccountView.Show();
        using var mounted = Renderer.Mount(() => AccountView.Build(
                new AccountView.Actions((_, _) => { }, () => { }, () => { }, AccountView.Hide),
                () => false,
                () => ""
            )
        );
        Assert.IsTrue(Descendants(mounted.Root).OfType<TextNode>().Any(t => t.Text == "USERNAME"));
        AccountView.Hide();
        Renderer.Flush();
        Assert.IsFalse(Descendants(mounted.Root).OfType<TextNode>().Any(t => t.Text == "USERNAME"));
    }

    [TestMethod]
    public void Hover_HighlightsDialogButtons()
    {
        using var mounted = Renderer.Mount(() => BuildDialog((_, _) => { }, () => { }, () => { }, () => { }));
        AccountView.Show();
        Renderer.Flush();
        var root = (View)mounted.Root;
        root.LayoutAndRender(new Vector2(900, 700));

        var cancel = Descendants(root).OfType<Component>().Single(node => node.Name == "Account cancel");
        var idle = cancel.Styles.BackgroundColor;
        var center = cancel.LayoutBorderPosition + cancel.LayoutBorderSize / 2;

        FocusManager.DispatchMouseMove(root, new BaseMouseMoveEvent(center, default, false, false, false));
        Renderer.Flush();
        root.LayoutAndRender(new Vector2(900, 700));

        Assert.IsTrue(cancel.IsHovered);
        Assert.AreNotEqual(idle, cancel.Styles.BackgroundColor);
    }

    [TestMethod]
    public void ArrowKeysWalkTheDialogAndEnterActivatesTheFocusedButton()
    {
        var presses = new List<string>();
        using var mounted = Renderer.Mount(() => BuildDialog(
            (_, _) => presses.Add("sign in"),
            () => presses.Add("register"),
            () => presses.Add("sign out"),
            () => presses.Add("close")
        ));
        AccountView.Show();
        Renderer.Flush();
        var root = (View)mounted.Root;
        root.LayoutAndRender(new Vector2(900, 700));

        // Opening focuses the first control, so the dialog is usable without the mouse.
        Assert.IsInstanceOfType(FocusManager.FocusedNode, typeof(TextInput));

        Assert.IsTrue(AccountView.HandleKeyPressed(root, Key.Down));
        Renderer.Flush();
        root.LayoutAndRender(new Vector2(900, 700));
        Assert.IsInstanceOfType(FocusManager.FocusedNode, typeof(TextInput));

        Assert.IsTrue(AccountView.HandleKeyPressed(root, Key.Down));
        Renderer.Flush();
        root.LayoutAndRender(new Vector2(900, 700));
        Assert.IsFalse(FocusManager.FocusedNode is TextInput, "Down should reach the button row");

        // Typing and Escape stay with the caller: the field needs the first, the phase the second.
        Assert.IsFalse(AccountView.HandleKeyPressed(root, Key.A));
        Assert.IsFalse(AccountView.HandleKeyPressed(root, Key.Escape));

        for (var step = 0; step < 4 && (FocusManager.FocusedNode as Component)?.Name != "Account register"; step++)
        {
            Assert.IsTrue(AccountView.HandleKeyPressed(root, Key.Right));
            Renderer.Flush();
            root.LayoutAndRender(new Vector2(900, 700));
        }

        Assert.AreEqual("Account register", (FocusManager.FocusedNode as Component)?.Name);
        Assert.IsTrue(AccountView.HandleKeyPressed(root, Key.Enter));
        CollectionAssert.AreEqual(new[] { "register" }, presses);
    }

    private static Node BuildDialog(
        Action<string, string> signIn,
        Action register,
        Action signOut,
        Action close
    ) => AccountView.Build(
        new AccountView.Actions(signIn, register, signOut, close),
        () => false,
        () => ""
    );

    private static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (var child in root.VisualChildren)
        foreach (var node in Descendants(child))
            yield return node;
    }
}
