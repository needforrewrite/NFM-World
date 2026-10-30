using System.Numerics;
using Microsoft.UI.Reactor.Layout;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards the NFM -> Yoga enum conversions in <see cref="Conversions"/> (Util/UI/Enums.cs).
/// Those mappings are raw casts, so every NFM enum must be value-identical to its Reactor
/// Yoga counterpart from the member they both share. The NFM <see cref="Justify"/> enum
/// used to be missing `Auto` (which FlexJustify has at 0), so Justify.Center cast to
/// FlexJustify.FlexStart and justify-content: center never centered vertically. These
/// tests pin every mapping so that class of bug can't silently return.
/// </summary>
[TestClass]
public class EnumYogaMappingTests
{
    [TestMethod]
    public void Justify_IsValueIdenticalTo_FlexJustify()
    {
        Assert.AreEqual((int)FlexJustify.Auto, (int)Justify.Auto);
        Assert.AreEqual((int)FlexJustify.FlexStart, (int)Justify.FlexStart);
        Assert.AreEqual((int)FlexJustify.Center, (int)Justify.Center);
        Assert.AreEqual((int)FlexJustify.FlexEnd, (int)Justify.FlexEnd);
        Assert.AreEqual((int)FlexJustify.SpaceBetween, (int)Justify.SpaceBetween);
        Assert.AreEqual((int)FlexJustify.SpaceAround, (int)Justify.SpaceAround);
        Assert.AreEqual((int)FlexJustify.SpaceEvenly, (int)Justify.SpaceEvenly);
    }

    [TestMethod]
    public void Align_IsValueIdenticalTo_FlexAlign()
    {
        Assert.AreEqual((int)FlexAlign.Auto, (int)Align.Auto);
        Assert.AreEqual((int)FlexAlign.FlexStart, (int)Align.FlexStart);
        Assert.AreEqual((int)FlexAlign.Center, (int)Align.Center);
        Assert.AreEqual((int)FlexAlign.FlexEnd, (int)Align.FlexEnd);
        Assert.AreEqual((int)FlexAlign.Stretch, (int)Align.Stretch);
        Assert.AreEqual((int)FlexAlign.Baseline, (int)Align.Baseline);
        Assert.AreEqual((int)FlexAlign.SpaceBetween, (int)Align.SpaceBetween);
        Assert.AreEqual((int)FlexAlign.SpaceAround, (int)Align.SpaceAround);
        Assert.AreEqual((int)FlexAlign.SpaceEvenly, (int)Align.SpaceEvenly);
        Assert.AreEqual((int)FlexAlign.Start, (int)Align.Start);
        Assert.AreEqual((int)FlexAlign.End, (int)Align.End);
    }

    [TestMethod]
    public void Direction_IsValueIdenticalTo_FlexLayoutDirection()
    {
        Assert.AreEqual((int)FlexLayoutDirection.Inherit, (int)Direction.Inherit);
        Assert.AreEqual((int)FlexLayoutDirection.LTR, (int)Direction.Ltr);
        Assert.AreEqual((int)FlexLayoutDirection.RTL, (int)Direction.Rtl);
    }

    [TestMethod]
    public void FlexDirection_IsValueIdenticalTo_FlexDirection()
    {
        Assert.AreEqual((int)Microsoft.UI.Reactor.Layout.FlexDirection.Column, (int)NFMWorld.Reactor.FlexDirection.Column);
        Assert.AreEqual((int)Microsoft.UI.Reactor.Layout.FlexDirection.ColumnReverse, (int)NFMWorld.Reactor.FlexDirection.ColumnReverse);
        Assert.AreEqual((int)Microsoft.UI.Reactor.Layout.FlexDirection.Row, (int)NFMWorld.Reactor.FlexDirection.Row);
        Assert.AreEqual((int)Microsoft.UI.Reactor.Layout.FlexDirection.RowReverse, (int)NFMWorld.Reactor.FlexDirection.RowReverse);
    }

    [TestMethod]
    public void Position_IsValueIdenticalTo_FlexPositionType()
    {
        Assert.AreEqual((int)FlexPositionType.Static, (int)Position.Static);
        Assert.AreEqual((int)FlexPositionType.Relative, (int)Position.Relative);
        Assert.AreEqual((int)FlexPositionType.Absolute, (int)Position.Absolute);
    }

    [TestMethod]
    public void Wrap_IsValueIdenticalTo_FlexWrap()
    {
        Assert.AreEqual((int)FlexWrap.NoWrap, (int)Wrap.NoWrap);
        Assert.AreEqual((int)FlexWrap.Wrap, (int)Wrap.Wrap);
        Assert.AreEqual((int)FlexWrap.WrapReverse, (int)Wrap.WrapReverse);
    }

    [TestMethod]
    public void Overflow_IsValueIdenticalTo_YogaOverflow()
    {
        Assert.AreEqual((int)YogaOverflow.Visible, (int)Overflow.Visible);
        Assert.AreEqual((int)YogaOverflow.Hidden, (int)Overflow.Hidden);
        Assert.AreEqual((int)YogaOverflow.Scroll, (int)Overflow.Scroll);
    }

    [TestMethod]
    public void Display_IsValueIdenticalTo_YogaDisplay()
    {
        Assert.AreEqual((int)YogaDisplay.Flex, (int)Display.Flex);
        Assert.AreEqual((int)YogaDisplay.None, (int)Display.None);
        Assert.AreEqual((int)YogaDisplay.Contents, (int)Display.Contents);
        Assert.AreEqual((int)YogaDisplay.Grid, (int)Display.Grid);
    }

    [TestMethod]
    public void BoxSizing_IsValueIdenticalTo_YogaBoxSizing()
    {
        Assert.AreEqual((int)YogaBoxSizing.BorderBox, (int)BoxSizing.BorderBox);
        Assert.AreEqual((int)YogaBoxSizing.ContentBox, (int)BoxSizing.ContentBox);
    }

    [TestMethod]
    public void NodeType_IsValueIdenticalTo_YogaNodeType()
    {
        Assert.AreEqual((int)YogaNodeType.Default, (int)NodeType.Default);
        Assert.AreEqual((int)YogaNodeType.Text, (int)NodeType.Text);
    }

    // ------------------------------------------------------------------ layout regression

    [TestMethod]
    public void JustifyContentCenter_VerticallyCentersChildren()
    {
        // A full-height column with justify-content:center must center its children
        // vertically. Regression for Justify missing `Auto` (cast to FlexStart).
        var root = new View { Name = "root" };
        var container = new View { Name = "container" };
        container.Styles = container.Styles with
        {
            FlexDirection = NFMWorld.Reactor.FlexDirection.Column,
            JustifyContent = Justify.Center,
            AlignItems = Align.Center,
            Position = Position.Absolute,
            Top = 0f,
            Bottom = 0f,
            Left = 0f,
            Right = 0f,
        };

        var a = new View { Name = "a" };
        a.Styles = a.Styles with { Height = 100 };
        var b = new View { Name = "b" };
        b.Styles = b.Styles with { Height = 200 };

        root.AddChild(container);
        container.AddChild(a);
        container.AddChild(b);

        IBackend.Backend = new DummyBackend();

        root.LayoutAndRender(new Vector2(800, 600));

        // 600 viewport, 300 content (100+200) => first starts at 150, second at 250.
        Assert.AreEqual(600f, container.LayoutHeight, 0.5f, "container must fill the viewport");
        Assert.AreEqual(150f, a.LayoutY, 0.5f, "first child top should be centered");
        Assert.AreEqual(250f, b.LayoutY, 0.5f, "second child top should be centered");
    }
}
