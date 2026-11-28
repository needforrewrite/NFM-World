using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class EditorControlsTests
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
    public void Modal_ReopeningCreatesLiveFieldsAndReleasesClosedFields()
    {
        var open = new Signal<bool>(false);
        var value = new Signal<string>("first");
        var fontSize = new Signal<float>(11);
        TextInput? field = null;
        using var mounted = Renderer.Mount(() => EditorControls.Modal(
            () => open.Value,
            () => EditorControls.DialogPanel(() =>
            [
                field = EditorControls.Field(
                    () => value.Value,
                    text => value.Value = text,
                    textStyle: () => Sx.TextStyles(fontSize: fontSize.Value)
                )
            ])
        )
        );
        Assert.IsNull(field);
        open.Value = true;
        Renderer.Flush();
        Assert.IsNotNull(field);
        var first = field;
        open.Value = false;
        Renderer.Flush();
        value.Value = "second";
        fontSize.Value = 14;
        Renderer.Flush();
        Assert.AreEqual("first", first.Text);
        Assert.AreEqual(11, first.TextStyles.FontSize);
        open.Value = true;
        Renderer.Flush();
        Assert.AreNotSame(first, field);
        Assert.AreEqual("second", field.Text);
        value.Value = "third";
        fontSize.Value = 16;
        Renderer.Flush();
        Assert.AreEqual("third", field.Text);
        Assert.AreEqual("first", first.Text);
        Assert.AreEqual(16, field.TextStyles.FontSize);
        Assert.AreEqual(11, first.TextStyles.FontSize);
    }

    [TestMethod]
    [DataRow(EditorTabAppearance.Flat)]
    [DataRow(EditorTabAppearance.Rounded)]
    public void DocumentTab_ClosingDoesNotSelectTheDocument(EditorTabAppearance appearance)
    {
        var selections = 0;
        var closes = 0;
        using var mounted = Renderer.Mount(() => Sx.View(
            style: () => Sx.Styles(width: 400, height: 100, alignItems: Align.FlexStart),
            children: EditorControls.DocumentTab(
                () => "Document",
                () => false,
                () => selections++,
                () => closes++,
                appearance,
                style: () => Sx.Styles(height: 32)
            )
        )
        );
        var root = (View)mounted.Root;
        root.LayoutAndRender(new Vector2(400, 100));
        var tab = (View)root.Children.Single();
        var close = tab.Children.OfType<View>().First();
        if (appearance == EditorTabAppearance.Rounded)
            close = tab.Children.OfType<View>().Skip(1).First();
        var center = close.LayoutBorderPosition + close.LayoutBorderSize / 2;
        root.DispatchMousePressed(new BaseMouseEvent(center, MouseButton.Primary, MouseButtons.Primary, false, false, false));
        Assert.AreEqual(1, closes);
        Assert.AreEqual(0, selections, "The close press must not bubble to the document tab.");
    }
}
