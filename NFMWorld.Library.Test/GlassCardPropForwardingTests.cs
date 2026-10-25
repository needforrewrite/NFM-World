using Lua;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards that the GlassCard wrapper forwards the props it does not consume.
///
/// The Sx port only built its inner View from <c>{ style }</c> + children, so every other prop —
/// notably the <c>onmousedown</c> the garage's CarCard passes to select a car — was silently
/// dropped and the card was not clickable. The preact version forwarded them, and
/// <c>styled()</c> still does; hand-written wrappers must too.
///
/// This mounts the real <c>glasscard.luau</c> through the real LuaUiLibrary host, so it fails if
/// the wrapper stops forwarding — a Lua-only fake host would not catch a host-level drop.
/// </summary>
[TestClass]
public class GlassCardPropForwardingTests
{
    private static View? _root;
    private static readonly List<string> Calls = [];
    private static LuaState? _state;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "NFMWorld.Library", "data", "library")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root not found");
    }

    private static void MountGlassCard()
    {
        _root = null;
        Calls.Clear();

        IBackend.Backend = new DummyBackend();
        GameThreadContext.Install();
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(FindRepoRoot());

        var state = LuaHelpers.OpenState();
        LuaUiLibrary.Register(
            state,
            view => _root = view,
            (method, payload) => Calls.Add(method),
            (_, _) => () => { }
        );

        state.DoString(
            """
            local Sx = require("../../library/sx/index")
            local GlassCard = require("./glasscard").GlassCard

            Sx.render(Sx.x(GlassCard) {
              name = "card",
              color = "#fff",
              style = { marginBottom = 8 },
              onmousedown = function() UiLib.call("cardPressed", {}) end,
              Sx.x(Sx.Text) { "Skyline" },
            })
            """,
            "NFMWorld.Library/data/uis/components/probe.luau"
        );

        GameThreadContext.Current.ExecutePendingTasks();
        _state = state;
    }

    private static IEnumerable<Component> Descendants(Component node)
    {
        foreach (var child in node.VisualChildren)
        {
            if (child is not Component c)
                continue;

            yield return c;
            foreach (var d in Descendants(c))
                yield return d;
        }
    }

    [TestMethod]
    public void GlassCard_ForwardsOnMouseDown_AndName()
    {
        MountGlassCard();
        Assert.IsNotNull(_root, "Sx.render must call setActiveRoot");

        // Find the wrapper by structure, not by name: `name` is one of the props the bug drops,
        // so locating by name would throw element-not-found instead of failing the real assert.
        var card = Descendants(_root!).First(c => c.VisualChildren.Any(v => v is Component));

        Assert.AreEqual(
            "card",
            card.Name,
            "GlassCard must forward `name` to its wrapper View"
        );

        Assert.IsNotNull(
            card.MousePressed,
            "GlassCard must forward onmousedown to its wrapper View"
        );

        // ...and it must actually fire through the real hit-test + dispatch path.
        _root!.LayoutAndRender(new LuaVector2(400, 300));

        var p = card.LayoutPaddingPosition;
        var s = card.LayoutPaddingSize;
        var centre = new LuaVector2(p.X + s.X / 2f, p.Y + s.Y / 2f);

        var ev = new BaseMouseEvent(centre, MouseButton.Primary, MouseButtons.Primary, false, false, false);
        FocusManager.HandleMousePressed(_root, ev);
        _root.DispatchMousePressed(ev);
        GameThreadContext.Current.ExecutePendingTasks();

        CollectionAssert.Contains(
            Calls,
            "cardPressed",
            "clicking the card must reach the forwarded onmousedown handler"
        );
    }
}
