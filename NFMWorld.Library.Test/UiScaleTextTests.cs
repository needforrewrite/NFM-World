using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards that the UI scale walk (<see cref="Component.NotifyUiScaleChanged"/>) reaches text
/// nodes, so they re-measure when the window changes size.
///
/// Text is sized by <c>scaleMul</c> (the current <c>G.Scale</c>) at measure time and drawn at
/// that same size, so a text node that is never re-measured keeps its old glyph size. Yoga
/// re-measures on its own only when the available width changes, so these tests use an
/// AUTO-SIZED root laid out at a constant available size: nothing about the box changes when
/// the scale does, and the text only follows the scale if the walk reaches it.
/// </summary>
[TestClass]
public class UiScaleTextTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
    }

    /// <summary>Auto-sized root — no width/height styles, so its box tracks the available size.</summary>
    private static View MakeRoot()
    {
        return new View { Name = "root" };
    }

    /// <summary>A label with text styles only — what a styled Sx.Text label actually gets.</summary>
    private static Text MakeLabel(bool sized)
    {
        var t = new Text { Name = "label" };
        if (sized)
        {
            t.Styles = t.Styles with { Width = 100, Height = 20 };
        }
        t.TextStyles = t.TextStyles with { FontSize = 12f };
        t.TextContent = "hello";
        return t;
    }

    private static float LaidOutFontSize(Text t)
    {
        Assert.IsTrue(t.LaidOutComplexText.HasValue, "text should have been measured");
        Assert.IsTrue(t.LaidOutComplexText.Value.Elements.Count > 0, "text should have laid out one element");
        return t.LaidOutComplexText.Value.Elements[0].FontSize ?? -1f;
    }

    [TestMethod]
    public void StyledText_MountedAtNonDefaultScale_RemeasuresWhenScaleReturnsToDefault()
    {
        IBackend.Backend.Graphics.Scale = 2f;
        var root = MakeRoot();
        root.LayoutAndRender(new LuaVector2(400, 400));

        // Mounted while the scale is already 2, and with no layout styles of its own: neither
        // the root's scale walk nor its own style assignment ever visits it.
        var text = MakeLabel(sized: false);
        root.AddChild(text);
        root.LayoutAndRender(new LuaVector2(400, 400));
        Assert.AreEqual(24f, LaidOutFontSize(text), 0.01f, "text is measured at 2x when created");

        IBackend.Backend.Graphics.Scale = 1f;
        root.LayoutAndRender(new LuaVector2(400, 400));

        Assert.AreEqual(12f, LaidOutFontSize(text), 0.01f,
            "the text must re-measure even though its available size never changed");
    }

    [TestMethod]
    public void SizedText_MountedAtNonDefaultScale_RemeasuresWhenScaleReturnsToDefault()
    {
        IBackend.Backend.Graphics.Scale = 2f;
        var root = MakeRoot();
        root.LayoutAndRender(new LuaVector2(400, 400));

        var text = MakeLabel(sized: true);
        root.AddChild(text);
        root.LayoutAndRender(new LuaVector2(400, 400));
        Assert.AreEqual(24f, LaidOutFontSize(text), 0.01f, "text is measured at 2x when created");

        IBackend.Backend.Graphics.Scale = 1f;
        root.LayoutAndRender(new LuaVector2(400, 400));

        Assert.AreEqual(12f, LaidOutFontSize(text), 0.01f, "text must re-measure at the new scale");
        Assert.AreEqual(100f, text.LayoutWidth, 0.01f, "the text's own box must rescale too");
    }

    [TestMethod]
    public void Text_MountedAtDefaultScale_RemeasuresWhenScaleChanges()
    {
        var root = MakeRoot();
        var text = MakeLabel(sized: false);
        root.AddChild(text);
        root.LayoutAndRender(new LuaVector2(400, 400));
        Assert.AreEqual(12f, LaidOutFontSize(text), 0.01f);

        IBackend.Backend.Graphics.Scale = 2f;
        root.LayoutAndRender(new LuaVector2(400, 400));

        Assert.AreEqual(24f, LaidOutFontSize(text), 0.01f, "text must grow with the scale");
    }
}
