using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

namespace NFMWorld.Library.Test;

[TestClass]
public class SxRendererTests
{
    [TestCleanup]
    public void Cleanup() => UiScheduler.Instance.Flush();

    [TestMethod]
    public void TextSlot_UpdatesOnlyOnChange()
    {
        var name = new Signal<string>("a");
        var commits = 0;

        var root = Sx.Text(text: () => name.Value);
        ((Text)root).TextContent = null; // detach so the bind's own write is the observable one

        var mounted = Renderer.Mount(() =>
        {
            var node = Sx.Text(text: () =>
            {
                commits++;
                return name.Value;
            });

            return node;
        });

        var before = commits;

        name.Value = "b";
        Renderer.Flush();

        Assert.IsTrue(commits > before, "The effect did not re-run.");

        // A write that does not change the value must not re-apply.
        var afterChange = commits;
        name.Value = "b";
        Renderer.Flush();

        Assert.AreEqual(afterChange, commits, "An unchanged value re-applied the slot.");

        mounted.Dispose();
    }

    [TestMethod]
    public void StyleEffect_NotRerunWhenUnchanged()
    {
        var active = new Signal<bool>(false);
        var writes = 0;

        var mounted = Renderer.Mount(() => Sx.View(() =>
        {
            writes++;
            return new Styles { BorderColor = active.Value ? new Maxine.Extensions.Mathematics.Color(79, 195, 247) : null };
        }));

        var afterMount = writes;
        active.Value = true;
        Renderer.Flush();

        Assert.IsTrue(writes > afterMount, "The style effect did not re-run.");

        mounted.Dispose();
    }

    [TestMethod]
    public void For_KeepsRowsForUnchangedItems()
    {
        var items = new Signal<IReadOnlyList<string>>(["a", "b", "c"]);
        var built = new List<string>();

        var mounted = Renderer.Mount(() => Sx.For(
            () => items.Value,
            (item, _) =>
            {
                built.Add(item);
                return Sx.Text(text: () => item);
            }));

        Assert.AreEqual(3, built.Count);

        // Reversing keeps every row: no rebuild, only moves.
        items.Value = ["c", "b", "a"];
        Renderer.Flush();

        Assert.AreEqual(3, built.Count, "Unchanged rows were rebuilt.");

        // Adding one builds exactly one row.
        items.Value = ["c", "b", "a", "d"];
        Renderer.Flush();

        Assert.AreEqual(4, built.Count);

        mounted.Dispose();
    }

    /// <summary>
    /// A row whose item is reused on a shorter list must move, not keep its old slot.
    /// The menu's BACK button is the same item (its method-group delegate compares equal)
    /// on every page, so before this it stayed first while the new page's items were
    /// appended behind it.
    /// </summary>
    [TestMethod]
    public void For_ReordersAReusedRowToTheItemOrder()
    {
        var page = new Signal<string>("training");

        // "back" is on both pages and must end up last in both.
        IReadOnlyList<string> Items() => page.Value == "training"
            ? ["trials", "challenges", "back"]
            : ["single", "multi", "back"];

        var mounted = Renderer.Mount(() => Sx.For(
            each: Items,
            row: (item, _) => Sx.Text(text: () => item)));

        var host = (View)mounted.Root;
        List<string?> Order() => host.VisualChildren.OfType<Text>()
            .Select(t => t.Children.OfType<TextNode>().FirstOrDefault()?.Text)
            .ToList();

        CollectionAssert.AreEqual(new[] { "trials", "challenges", "back" }, Order());

        page.Value = "play";
        Renderer.Flush();

        CollectionAssert.AreEqual(new[] { "single", "multi", "back" }, Order(),
            "the reused BACK row kept its old position");

        mounted.Dispose();
    }

    /// <summary>
    /// A card style that mentions only layout must not wipe the card's own padding. The
    /// old merge copied every field unconditionally, so an unset PaddingTop reset it to 0
    /// and the content sat flush against the accent bar - the "margin where padding should
    /// be" symptom in the garage.
    /// </summary>
    [TestMethod]
    public void Styles_Over_KeepsTheBasePadding()
    {
        var card = new Styles { PaddingTop = 16, PaddingLeft = 20 };
        var caller = new Styles { FlexDirection = FlexDirection.Column, MarginBottom = 16 };

        var merged = card.Over(caller);

        Assert.AreEqual(16f, (float)(merged.PaddingTop.PointValue ?? 0), "the card's padding survived");
        Assert.AreEqual(20f, (float)(merged.PaddingLeft.PointValue ?? 0));
        Assert.AreEqual(FlexDirection.Column, merged.FlexDirection);
        Assert.AreEqual(16f, (float)(merged.MarginBottom.PointValue ?? 0));
    }

    [TestMethod]
    public void TextStyles_Over_LeavesUnsetFieldsAlone()
    {
        var baseStyle = new TextStyles { FontSize = 48, FontStyle = FontStyle.Bold };
        var overlay = new TextStyles { ForegroundColor = new Maxine.Extensions.Mathematics.Color(1, 2, 3) };

        var merged = baseStyle.Over(overlay);

        Assert.AreEqual(48f, merged.FontSize);
        Assert.AreEqual(FontStyle.Bold, merged.FontStyle);
        Assert.AreEqual(new Maxine.Extensions.Mathematics.Color(1, 2, 3), merged.ForegroundColor);
    }

    [TestMethod]
    public void TextStyles_Resolved_AppliesDefaults()
    {
        var resolved = new TextStyles().Resolved;

        Assert.AreEqual(12f, resolved.FontSize);
        Assert.AreEqual(FontFamily.NotoSans, resolved.FontFamily);
        Assert.AreEqual(new Maxine.Extensions.Mathematics.Color(255, 255, 255), resolved.ForegroundColor);
    }

    [TestMethod]
    public void Styles_Over_LeavesUnsetFieldsAlone()
    {
        var baseline = new Styles { FlexDirection = FlexDirection.Column, AlignItems = Align.FlexStart, PaddingTop = 8 };
        var overlay = new Styles { BackgroundColor = new Maxine.Extensions.Mathematics.Color(1, 2, 3) };

        var merged = baseline.Over(overlay);

        Assert.AreEqual(FlexDirection.Column, merged.FlexDirection);
        Assert.AreEqual(Align.FlexStart, merged.AlignItems);
        Assert.AreEqual(8f, (float)(merged.PaddingTop.PointValue ?? 0));
        Assert.IsNotNull(merged.BackgroundColor);
    }

    [TestMethod]
    public void Styles_Resolved_AppliesWebDefaults()
    {
        var resolved = new Styles().Resolved;

        Assert.AreEqual(FlexDirection.Row, resolved.FlexDirection);
        Assert.AreEqual(Align.Stretch, resolved.AlignItems);
        Assert.AreEqual(Display.Flex, resolved.Display);
        Assert.AreEqual(1f, resolved.Opacity);
    }

    [TestMethod]
    public void Show_SwapsBranchWithoutRebuildingOther()
    {
        var visible = new Signal<bool>(true);
        var mounts = 0;

        var mounted = Renderer.Mount(() => Sx.Show(
            () => visible.Value,
            () =>
            {
                mounts++;
                return Sx.Text(text: () => "on");
            },
            () => Sx.Text(text: () => "off")));

        Assert.AreEqual(1, mounts);

        visible.Value = false;
        Renderer.Flush();
        Assert.AreEqual(1, mounts);

        visible.Value = true;
        Renderer.Flush();
        Assert.AreEqual(2, mounts, "The `then` branch was not remounted after toggling back.");

        mounted.Dispose();
    }

    /// <summary>
    /// A hidden branch's effects must stop. They used to survive every toggle, so a
    /// pause menu reopened N times left N live effect sets re-running on every HUD write.
    /// </summary>
    [TestMethod]
    public void Show_DisposesTheHiddenBranch()
    {
        var visible = new Signal<bool>(true);
        var probe = new Signal<int>(0);
        var runs = 0;

        var mounted = Renderer.Mount(() => Sx.Show(
            () => visible.Value,
            () => Sx.Text(text: () => { runs++; return probe.Value.ToString(); })));

        Renderer.Flush();

        for (var i = 0; i < 5; i++)
        {
            visible.Value = false;
            Renderer.Flush();
            visible.Value = true;
            Renderer.Flush();
        }

        // Leave it hidden: no branch remains, so no effect should react.
        visible.Value = false;
        Renderer.Flush();

        var before = runs;
        probe.Value = 7;
        Renderer.Flush();

        Assert.AreEqual(before, runs, $"{runs - before} effect(s) of a removed branch still ran.");

        mounted.Dispose();
    }

    /// <summary>
    /// Switching tabs repeatedly must not accumulate live effects. Only the first branch
    /// was owned, so every later one survived until the whole tree was torn down.
    /// </summary>
    [TestMethod]
    public void Switch_ReleasesEveryBranchItLeaves()
    {
        var tab = new Signal<string>("a");
        var probe = new Signal<int>(0);
        var runs = 0;

        var mounted = Renderer.Mount(() => Sx.Switch(
            tab,
            new Dictionary<string, Func<Node>>
            {
                ["a"] = () => Sx.Text(text: () => { runs++; return $"a{probe.Value}"; }),
                ["b"] = () => Sx.Text(text: () => { runs++; return $"b{probe.Value}"; }),
            }));

        Renderer.Flush();

        for (var i = 0; i < 5; i++)
        {
            tab.Value = "b";
            Renderer.Flush();
            tab.Value = "a";
            Renderer.Flush();
        }

        // Only the live branch may react to a write; the ten it replaced must be gone.
        // Exactly the live branch reacts; each branch it replaced must be gone.
        var before = runs;
        probe.Value = 9;
        Renderer.Flush();

        Assert.AreEqual(1, runs - before, $"{runs - before} effects reacted; only the live branch should.");

        mounted.Dispose();
    }

    /// <summary>
    /// A For host that leaves the tree must release its rows. The parent only detaches
    /// nodes, so this is what the Disposing hook exists for.
    /// </summary>
    [TestMethod]
    public void For_ReleasesRowsWhenItsHostLeavesTheTree()
    {
        var shown = new Signal<bool>(true);
        var probe = new Signal<int>(0);
        var runs = 0;

        var mounted = Renderer.Mount(() => Sx.Show(
            () => shown.Value,
            () => Sx.For(
                each: () => (IReadOnlyList<string>)["a", "b"],
                row: (item, _) => Sx.Text(text: () => { runs++; return $"{item}{probe.Value}"; }))));

        Renderer.Flush();
        Assert.IsTrue(runs > 0, "the rows should have rendered");

        shown.Value = false;
        Renderer.Flush();

        var before = runs;
        probe.Value = 3;
        Renderer.Flush();

        Assert.AreEqual(before, runs, $"{runs - before} row effect(s) survived their host leaving the tree.");

        mounted.Dispose();
    }
}
