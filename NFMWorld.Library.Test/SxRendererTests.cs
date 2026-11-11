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
                var node = Sx.Text(
                    text: () =>
                    {
                        commits++;
                        return name.Value;
                    }
                );

                return node;
            }
        );

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
                return Sx.Styles(borderColor: active.Value ? new Maxine.Extensions.Mathematics.Color(79, 195, 247) : null);
            }
        )
        );

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
            }
        )
        );

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
                row: (item, _) => Sx.Text(text: () => item)
            )
        );

        var host = (View)mounted.Root;

        List<string?> Order() => host.VisualChildren.OfType<Text>()
            .Select(t => t.Children.OfType<TextNode>().FirstOrDefault()?.Text)
            .ToList();

        CollectionAssert.AreEqual(new[] { "trials", "challenges", "back" }, Order());

        page.Value = "play";
        Renderer.Flush();

        CollectionAssert.AreEqual(
            new[] { "single", "multi", "back" },
            Order(),
            "the reused BACK row kept its old position"
        );

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
        var card = Sx.Styles(paddingTop: 16, paddingLeft: 20);
        var caller = Sx.Styles(flexDirection: FlexDirection.Column, marginBottom: 16);

        var merged = card.Over(caller);

        Assert.AreEqual(16f, (float)(merged.PaddingTop.PointValue ?? 0), "the card's padding survived");
        Assert.AreEqual(20f, (float)(merged.PaddingLeft.PointValue ?? 0));
        Assert.AreEqual(FlexDirection.Column, merged.FlexDirection);
        Assert.AreEqual(16f, (float)(merged.MarginBottom.PointValue ?? 0));
    }

    [TestMethod]
    public void TextStyles_Over_LeavesUnsetFieldsAlone()
    {
        var baseStyle = Sx.TextStyles(fontSize: 48, fontStyle: FontStyle.Bold);
        var overlay = Sx.TextStyles(foregroundColor: new Maxine.Extensions.Mathematics.Color(1, 2, 3));

        var merged = baseStyle.Over(overlay);

        Assert.AreEqual(48f, merged.FontSize);
        Assert.AreEqual(FontStyle.Bold, merged.FontStyle);
        Assert.AreEqual(new Maxine.Extensions.Mathematics.Color(1, 2, 3), merged.ForegroundColor);
    }

    [TestMethod]
    public void TextStyles_Resolved_AppliesDefaults()
    {
        var resolved = Sx.TextStyles().Resolved;

        Assert.AreEqual(12f, resolved.FontSize);
        Assert.AreEqual(FontFamily.NotoSans, resolved.FontFamily);
        Assert.AreEqual(new Maxine.Extensions.Mathematics.Color(255, 255, 255), resolved.ForegroundColor);
    }

    [TestMethod]
    public void Styles_Over_LeavesUnsetFieldsAlone()
    {
        var baseline = Sx.Styles(flexDirection: FlexDirection.Column, alignItems: Align.FlexStart, paddingTop: 8);
        var overlay = Sx.Styles(backgroundColor: new Maxine.Extensions.Mathematics.Color(1, 2, 3));

        var merged = baseline.Over(overlay);

        Assert.AreEqual(FlexDirection.Column, merged.FlexDirection);
        Assert.AreEqual(Align.FlexStart, merged.AlignItems);
        Assert.AreEqual(8f, (float)(merged.PaddingTop.PointValue ?? 0));
        Assert.IsNotNull(merged.BackgroundColor);
    }

    [TestMethod]
    public void Styles_Resolved_AppliesWebDefaults()
    {
        var resolved = Sx.Styles().Resolved;

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
                () => Sx.Text(text: () => "off")
            )
        );

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
                () => Sx.Text(
                    text: () =>
                    {
                        runs++;
                        return probe.Value.ToString();
                    }
                )
            )
        );

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
                    ["a"] = () => Sx.Text(
                        text: () =>
                        {
                            runs++;
                            return $"a{probe.Value}";
                        }
                    ),
                    ["b"] = () => Sx.Text(
                        text: () =>
                        {
                            runs++;
                            return $"b{probe.Value}";
                        }
                    ),
                }
            )
        );

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
    /// A helper that returns a list of nodes can be passed as one child. Before this it threw
    /// inside the component's effect, and the signal machinery swallowed the exception, so the
    /// subtree failed to mount with no error anywhere - the lobby's create dialog was blank.
    /// </summary>
    [TestMethod]
    public void CollectionsOfChildren_AreFlattened()
    {
        var mounted = Renderer.Mount(() => Sx.View(
                children:
                [
                    new List<Node>
                    {
                        Sx.Text(text: () => "one"),
                        Sx.Text(text: () => "two"),
                    }
                ]
            )
        );

        var texts = ((View)mounted.Root).VisualChildren.OfType<Text>()
            .Select(t => t.Children.OfType<TextNode>().FirstOrDefault()?.Text)
            .ToList();

        CollectionAssert.AreEqual(new[] { "one", "two" }, texts);

        mounted.Dispose();
    }

    [TestMethod]
    public void LazyDialog_ReopenCreatesFreshControls_AndOwnsTheirEffects()
    {
        var open = new Signal<bool>(false);
        var probe = new Signal<int>(0);
        var factoryRuns = 0;
        var effectRuns = 0;
        Text? control = null;

        Node Dialog(Func<Node[]> content) => Sx.Show(
            () => open.Value,
            () => Sx.View(children: [.. content()])
        );

        using var mounted = Renderer.Mount(() => Dialog(() =>
            {
                factoryRuns++;
                control = Sx.Text(text: () =>
                    {
                        effectRuns++;
                        return probe.Value.ToString();
                    }
                );
                return [control];
            }
        )
        );
        Renderer.Flush();

        Assert.AreEqual(0, factoryRuns);
        Assert.AreEqual(0, effectRuns);
        Assert.IsNull(control);

        open.Value = true;
        Renderer.Flush();
        Assert.AreEqual(1, factoryRuns);
        Assert.IsNotNull(control);
        var firstControl = control;
        var before = effectRuns;
        probe.Value = 1;
        Renderer.Flush();
        Assert.AreEqual(before + 1, effectRuns);
        Assert.AreEqual("1", firstControl.Children.OfType<TextNode>().Single().Text);

        open.Value = false;
        Renderer.Flush();
        before = effectRuns;
        probe.Value = 2;
        Renderer.Flush();
        Assert.AreEqual(before, effectRuns);
        Assert.AreEqual(1, factoryRuns);

        open.Value = true;
        Renderer.Flush();
        Assert.AreEqual(2, factoryRuns);
        Assert.IsNotNull(control);
        Assert.AreNotSame(firstControl, control);
        Assert.AreEqual("2", control.Children.OfType<TextNode>().Single().Text);
        before = effectRuns;
        probe.Value = 3;
        Renderer.Flush();
        Assert.AreEqual(before + 1, effectRuns);
        Assert.AreEqual("3", control.Children.OfType<TextNode>().Single().Text);
        Assert.AreEqual("1", firstControl.Children.OfType<TextNode>().Single().Text);

        mounted.Dispose();
        before = effectRuns;
        probe.Value = 4;
        Renderer.Flush();
        Assert.AreEqual(before, effectRuns);
        Assert.AreEqual(2, factoryRuns);
    }

    [TestMethod]
    public void DropdownMenu_ReactiveRowsMountSwitchAndExecute()
    {
        IBackend.Backend = new DummyBackend();
        var menu = new Signal<string?>(null);
        var invoked = "";
        View? rows = null;

        Node Action(string label) => Sx.View(
            onmousedown: _ => { invoked = label; menu.Value = null; },
            children: Sx.Text(text: () => label)
        );

        using var mounted = Renderer.Mount(() => Sx.Show(
            () => menu.Value is not null,
            () => Sx.View(children: (Func<object?>)(() => rows = Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Column),
                children: menu.Value switch
                {
                    "File" => new object[] { Action("New"), Action("Open") },
                    "Edit" => new object[] { Action("Undo") },
                    _ => new object[] { Action("Top down") }
                }
            ))
            )
        )
        );

        foreach (var (name, expected) in new[] { ("File", 2), ("Edit", 1), ("View", 1) })
        {
            menu.Value = name;
            Renderer.Flush();
            Assert.IsNotNull(rows);
            Assert.HasCount(expected, rows.VisualChildren);
        }

        var action = (View)rows!.VisualChildren[0];
        action.MousePressed!.Invoke(default);
        Renderer.Flush();
        Assert.AreEqual("Top down", invoked);
        Assert.IsNull(menu.Value);

        menu.Value = "File";
        Renderer.Flush();
        Assert.HasCount(2, rows!.VisualChildren);
    }

    [TestMethod]
    public void DropdownMenu_KeyedRowsSurviveHoverAndRemountOnMenuChange()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearHover();
        var menu = new Signal<string>("File");
        var builds = 0;
        var normal = new Maxine.Extensions.Mathematics.Color(20, 30, 40);
        var highlight = new Maxine.Extensions.Mathematics.Color(70, 80, 90);
        View? rows = null;

        View Action(string label)
        {
            var hovered = new Signal<bool>(false);
            return Sx.View(
                name: () => label,
                style: () => Sx.Styles(
                    width: 100,
                    height: 30,
                    flexShrink: 0,
                    backgroundColor: hovered.Value ? highlight : normal
                ),
                onmouseenter: _ => hovered.Value = true,
                onmouseleave: _ => hovered.Value = false
            );
        }

        using var mounted = Renderer.Mount(() => Sx.View(
            style: () => Sx.Styles(width: 200, height: 200),
            children: Sx.For(
                each: () => new[] { menu.Value },
                row: (page, _) =>
                {
                    builds++;
                    return rows = Sx.View(
                        style: () => Sx.Styles(width: 100, flexDirection: FlexDirection.Column),
                        children: page == "File"
                            ? new object[] { Action("New"), Action("Open") }
                            : new object[] { Action("Undo") }
                    );
                }
            )
        )
        );
        var root = (View)mounted.Root;

        void Move(float x, float y)
        {
            FocusManager.DispatchMouseMove(root, new NFMWorld.ClayDom.Events.BaseMouseMoveEvent(
                new System.Numerics.Vector2(x, y),
                default,
                false,
                false,
                false
            )
            );
            Renderer.Flush();
            root.LayoutAndRender(new System.Numerics.Vector2(200, 200));
        }

        try
        {
            root.LayoutAndRender(new System.Numerics.Vector2(200, 200));
            Assert.IsNotNull(rows);
            var originalRows = rows;
            var first = (View)rows.VisualChildren[0];
            var second = (View)rows.VisualChildren[1];
            CollectionAssert.AreEqual(new[] { "New", "Open" }, rows.VisualChildren.Cast<View>().Select(n => n.Name).ToArray());

            Move(50, 15);
            Assert.AreSame(originalRows, rows);
            Assert.AreSame(first, rows.VisualChildren[0]);
            Assert.IsTrue(first.IsHovered);
            Assert.IsFalse(second.IsHovered);
            Assert.AreEqual(highlight, first.Styles.BackgroundColor);
            Assert.AreEqual(normal, second.Styles.BackgroundColor);

            Move(50, 45);
            Assert.AreSame(originalRows, rows);
            Assert.AreSame(first, rows.VisualChildren[0]);
            Assert.AreSame(second, rows.VisualChildren[1]);
            Assert.IsFalse(first.IsHovered);
            Assert.IsTrue(second.IsHovered);
            Assert.AreEqual(normal, first.Styles.BackgroundColor);
            Assert.AreEqual(highlight, second.Styles.BackgroundColor);

            Move(150, 100);
            Assert.AreSame(originalRows, rows);
            Assert.AreSame(second, rows.VisualChildren[1]);
            Assert.IsFalse(first.IsHovered);
            Assert.IsFalse(second.IsHovered);
            Assert.AreEqual(normal, first.Styles.BackgroundColor);
            Assert.AreEqual(normal, second.Styles.BackgroundColor);
            Assert.AreEqual(1, builds);

            menu.Value = "Edit";
            Renderer.Flush();
            root.LayoutAndRender(new System.Numerics.Vector2(200, 200));
            Assert.AreNotSame(originalRows, rows);
            CollectionAssert.AreEqual(new[] { "Undo" }, rows.VisualChildren.Cast<View>().Select(n => n.Name).ToArray());
            Move(50, 15);
            Assert.IsTrue(((View)rows.VisualChildren[0]).IsHovered);
            Assert.AreEqual(highlight, ((View)rows.VisualChildren[0]).Styles.BackgroundColor);
            Assert.AreEqual(2, builds);

            Move(150, 100);
            menu.Value = "File";
            Renderer.Flush();
            Assert.AreNotSame(originalRows, rows);
            Assert.AreNotSame(first, rows.VisualChildren[0]);
            CollectionAssert.AreEqual(new[] { "New", "Open" }, rows.VisualChildren.Cast<View>().Select(n => n.Name).ToArray());
            Assert.AreEqual(3, builds);
        }
        finally
        {
            FocusManager.ClearHover();
        }
    }

    [TestMethod]
    public void ReactiveControlList_WrappedInView_MountsAndSwitchesModes()
    {
        var open = new Signal<bool>(false);
        var hasOptions = new Signal<bool>(true);
        View? controls = null;

        List<Node> Options() => hasOptions.Value
            ? [Sx.Text(text: () => "Race constraint"), Sx.Text(text: () => "Racing")]
            : [];

        using var mounted = Renderer.Mount(() => Sx.Show(
                () => open.Value,
                () => Sx.View(
                    children:
                    [
                        (Func<object?>)(() => controls = Sx.View(children: [.. Options()]))
                    ]
                )
            )
        );

        open.Value = true;
        Renderer.Flush();
        Assert.IsNotNull(controls);
        Assert.HasCount(2, controls.VisualChildren);

        hasOptions.Value = false;
        Renderer.Flush();
        Assert.IsEmpty(controls.VisualChildren);

        hasOptions.Value = true;
        Renderer.Flush();
        Assert.HasCount(2, controls.VisualChildren);
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
                    row: (item, _) => Sx.Text(
                        text: () =>
                        {
                            runs++;
                            return $"{item}{probe.Value}";
                        }
                    )
                )
            )
        );

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
