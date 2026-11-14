using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class ReplayBrowserViewTests
{
    [TestInitialize]
    public void Setup() => IBackend.Backend = new DummyBackend();

    [TestMethod]
    public void ClickingARowSelectsIt_OnlyExplicitActionsPlayOrUploadThatRecording()
    {
        var state = new ReplayBrowserState();
        state.Entries.Value =
        [
            new("first", "stage", "car", "1:00.000", "Local only", true, true),
            new("second", "stage", "car", "1:02.000", "Local only", true, true)
        ];
        string? opened = null;
        string? uploaded = null;
        using var mounted = Renderer.Mount(() => ReplayBrowserView.Build(
            state,
            new ReplayBrowserView.Actions(
                id => opened = id,
                id => uploaded = id,
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { }
            )
        )
        );

        Click(mounted.Root, "Replay row second");
        Assert.AreEqual("second", state.SelectedId.Value);
        Assert.IsNull(opened);
        Assert.IsNull(uploaded);
        Click(mounted.Root, "Replay upload");
        Assert.AreEqual("second", uploaded);
        Assert.IsNull(opened);
        Click(mounted.Root, "Replay play");
        Assert.AreEqual("second", opened);

        state.Entries.Value = [state.Entries.Value[0]];
        Renderer.Flush();
        Assert.IsNull(state.Selected);
        Assert.IsFalse(Descendants(mounted.Root).Any(node => node.Name == "Replay play"));
        Assert.IsFalse(Descendants(mounted.Root).Any(node => node.Name == "Replay upload"));
    }

    [TestMethod]
    public void DeleteAsksForConfirmation_CancelKeepsTheReplay_ConfirmDeletesThePromptedRun()
    {
        var state = new ReplayBrowserState();
        state.Entries.Value =
        [
            new(
                "first",
                "stage",
                "car",
                "1:00.000",
                "Local only",
                true,
                true,
                new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.Zero)
            ),
            new("second", "stage", "car", "1:02.000", "Local only", true, true)
        ];
        string? deleted = null;
        using var mounted = Renderer.Mount(() => ReplayBrowserView.Build(
            state,
            new ReplayBrowserView.Actions(
                _ => { },
                _ => { },
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { },
                Delete: id => deleted = id
            )
        )
        );

        Click(mounted.Root, "Replay row first");
        var savedDate = state.Entries.Value[0].SavedAt!.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        Assert.IsTrue(Nodes(mounted.Root).OfType<TextNode>().Any(node => node.Text!.Contains("Saved " + savedDate)));
        Click(mounted.Root, "Replay delete");
        Assert.IsNull(deleted);
        Assert.IsNotNull(state.DeleteCandidate.Value);
        Click(mounted.Root, "Replay delete cancel");
        Assert.IsNull(deleted);
        Assert.IsNull(state.DeleteCandidate.Value);

        Click(mounted.Root, "Replay delete");
        state.SelectedId.Value = "second";
        Renderer.Flush();
        Click(mounted.Root, "Replay delete confirm");
        Assert.AreEqual("first", deleted, "Confirmation must apply to the run named in the prompt.");
        Assert.IsNull(state.DeleteCandidate.Value);
    }

    [TestMethod]
    public void OnlineSelectionWaitsForPlay_AndNeverOffersUpload()
    {
        var state = new ReplayBrowserState();
        state.Online.Value = true;
        state.Entries.Value = [new("archive:42", "stage", "car", "1:00.000", "racer", true, false)];
        string? opened = null;
        using var mounted = Renderer.Mount(() => ReplayBrowserView.Build(
            state,
            new ReplayBrowserView.Actions(
                id => opened = id,
                _ => Assert.Fail("Uploaded online replay"),
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { },
                Delete: _ => Assert.Fail("Deleted online replay")
            )
        )
        );

        Click(mounted.Root, "Replay row archive:42");
        Assert.IsNull(opened);
        Assert.IsFalse(Descendants(mounted.Root).Any(node => node.Name == "Replay upload"));
        Assert.IsFalse(Descendants(mounted.Root).Any(node => node.Name == "Replay delete"));
        Click(mounted.Root, "Replay play");
        Assert.AreEqual("archive:42", opened);

        state.Busy.Value = true;
        Renderer.Flush();
        Assert.IsFalse(Descendants(mounted.Root).Any(node => node.Name == "Replay play"));
    }

    [TestMethod]
    public void IncompatibleRecordingHasNoPlayOrUploadAction()
    {
        var state = new ReplayBrowserState();
        state.Entries.Value = [new("old", "stage", "car", "1:00.000", "Local only", false, false)];
        using var mounted = Renderer.Mount(() => ReplayBrowserView.Build(
            state,
            new ReplayBrowserView.Actions(
                _ => Assert.Fail("Played incompatible replay"),
                _ => Assert.Fail("Uploaded incompatible replay"),
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { }
            )
        )
        );

        Click(mounted.Root, "Replay row old");
        Assert.AreEqual("old", state.SelectedId.Value);
        Assert.IsFalse(Descendants(mounted.Root).Any(node => node.Name == "Replay play"));
        Assert.IsFalse(Descendants(mounted.Root).Any(node => node.Name == "Replay upload"));
    }

    private static void Click(Node root, string name)
    {
        Descendants(root).Single(node => node.Name == name).MousePressed!.Invoke(default);
        Renderer.Flush();
    }

    private static IEnumerable<Component> Descendants(Node root)
    {
        if (root is Component component)
            yield return component;
        foreach (var child in root.VisualChildren)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }

    private static IEnumerable<Node> Nodes(Node root)
    {
        yield return root;
        foreach (var child in root.VisualChildren)
        foreach (var descendant in Nodes(child))
            yield return descendant;
    }
}
