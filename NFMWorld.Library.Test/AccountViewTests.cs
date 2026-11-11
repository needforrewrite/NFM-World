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
    }

    [TestCleanup]
    public void Cleanup()
    {
        AccountView.Reset();
        Renderer.Flush();
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

    private static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (var child in root.VisualChildren)
        foreach (var node in Descendants(child))
            yield return node;
    }
}
