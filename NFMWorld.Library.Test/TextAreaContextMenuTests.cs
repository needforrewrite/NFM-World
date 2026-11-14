using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards <see cref="TextArea"/>'s right-click menu and the editing commands it shares with the
/// keyboard. The menu is the same <see cref="TextEditContextMenu"/> a <see cref="TextInput"/>
/// uses; what differs is the geometry, because a text area is a scroll container.
/// </summary>
[TestClass]
public class TextAreaContextMenuTests
{
    private string _clipboard = "";
    private Func<string>? _previousGet;
    private Action<string>? _previousSet;

    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();

        _previousGet = TextInput.DefaultGetClipboardText;
        _previousSet = TextInput.DefaultSetClipboardText;
        TextInput.DefaultGetClipboardText = () => _clipboard;
        TextInput.DefaultSetClipboardText = value => _clipboard = value;
    }

    [TestCleanup]
    public void Cleanup()
    {
        TextInput.DefaultGetClipboardText = _previousGet;
        TextInput.DefaultSetClipboardText = _previousSet;
        FocusManager.ClearFocus();
    }

    /// <summary>An editor tall enough for the menu, holding more lines than it can show.</summary>
    private static View AreaRoot(out TextArea area)
    {
        const float width = 600f;
        const float height = 400f;

        var root = new View { Name = "root" };
        root.Styles = root.Styles with { Width = width, Height = height };

        area = new TextArea { Name = "area" };
        area.Styles = area.Styles with
        {
            Position = Position.Absolute,
            Left = 0,
            Top = 0,
            Width = width,
            Height = 300,
        };
        area.SetTextExternal(string.Join("\n", Enumerable.Range(0, 60).Select(i => $"line {i}")));
        root.AddChild(area);
        root.LayoutAndRender(new Vector2(width, height));
        area.Focus();
        return root;
    }

    /// <summary>The menu is the editor's last <see cref="View"/> child (the text block is first).</summary>
    private static View Menu(TextArea area) => area.VisualChildren.OfType<View>().Last();

    private static void RightPress(View root, Vector2 at)
        => root.DispatchMousePressed(
            new BaseMouseEvent(at, MouseButton.Secondary, MouseButtons.Secondary, false, false, false)
        );

    private static void ClickAt(View root, Vector2 at)
    {
        root.DispatchMousePressed(new BaseMouseEvent(at, MouseButton.Primary, MouseButtons.Primary, false, false, false));
        root.DispatchMouseReleased(new BaseMouseEvent(at, MouseButton.Primary, MouseButtons.None, false, false, false));
    }

    /// <summary>Clicks a row by index (Cut, Copy, Paste, Select All) after laying the menu out.</summary>
    private static void ClickMenuRow(View root, TextArea area, int rowIndex)
    {
        var menu = Menu(area);
        root.LayoutAndRender(new Vector2(600, 400));
        var row = (Component)menu.Children[rowIndex];
        ClickAt(root, row.LayoutPaddingPosition + row.LayoutPaddingSize / 2);
    }

    private static void Press(TextArea area, Key key, Keys held = default)
        => area.DispatchKeyPressed(new KeyboardEvent(key, key, held));

    private static Keys Ctrl => default(Keys) | Key.LControlKey;
    private static Keys Shift => default(Keys) | Key.LShiftKey;

    [TestMethod]
    public void SecondaryPress_OpensTheMenu_AndKeepsFocus()
    {
        var root = AreaRoot(out var area);

        RightPress(root, new Vector2(300, 100));

        Assert.IsTrue(Menu(area).IsDisplayed, "the menu must be open");
        Assert.IsTrue(area.IsFocused, "the editor must keep focus");
    }

    /// <summary>
    /// A press with no selection places the caret where the user pointed, so Paste lands there.
    /// </summary>
    [TestMethod]
    public void SecondaryPress_WithoutSelection_MovesTheCaretToTheClick()
    {
        var root = AreaRoot(out var area);
        Press(area, Key.Home, Ctrl);
        Assert.AreEqual(0, area.CursorIndex);

        RightPress(root, new Vector2(area.LayoutPaddingPosition.X + 40, area.LayoutPaddingPosition.Y + 40));

        Assert.IsTrue(area.CursorIndex > 0, "the caret should follow the click");
        Assert.IsNull(area.SelectionAnchor);
    }

    /// <summary>
    /// An existing selection survives the right-click - that is what makes Cut/Copy usable from
    /// the menu without re-selecting.
    /// </summary>
    [TestMethod]
    public void SecondaryPress_KeepsAnExistingSelection()
    {
        var root = AreaRoot(out var area);
        Press(area, Key.A, Ctrl);
        var anchor = area.SelectionAnchor;
        var cursor = area.CursorIndex;

        RightPress(root, new Vector2(300, 100));

        Assert.AreEqual(anchor, area.SelectionAnchor);
        Assert.AreEqual(cursor, area.CursorIndex);
    }

    [TestMethod]
    public void MenuCopyRow_CopiesTheSelection()
    {
        var root = AreaRoot(out var area);
        Press(area, Key.A, Ctrl);
        RightPress(root, new Vector2(300, 100));

        ClickMenuRow(root, area, rowIndex: 1);

        Assert.AreEqual(area.Value, _clipboard);
        Assert.IsFalse(Menu(area).IsDisplayed);
    }

    [TestMethod]
    public void MenuCutRow_RemovesTheSelection()
    {
        var root = AreaRoot(out var area);
        Press(area, Key.A, Ctrl);
        var original = area.Value;
        RightPress(root, new Vector2(300, 100));

        ClickMenuRow(root, area, rowIndex: 0);

        Assert.AreEqual(original, _clipboard);
        Assert.AreEqual("", area.Value);
    }

    [TestMethod]
    public void MenuPasteRow_InsertsTheClipboard()
    {
        var root = AreaRoot(out var area);
        Press(area, Key.A, Ctrl);
        _clipboard = "pasted";

        RightPress(root, new Vector2(300, 100));
        ClickMenuRow(root, area, rowIndex: 2);

        Assert.AreEqual("pasted", area.Value);
    }

    /// <summary>
    /// A menu edit is a real edit: it goes on the undo stack, so Ctrl+Z reverts a paste made
    /// from the menu exactly as it reverts a typed one.
    /// </summary>
    [TestMethod]
    public void MenuPaste_IsUndoable()
    {
        var root = AreaRoot(out var area);
        area.SetTextExternal("original");
        Press(area, Key.A, Ctrl);
        _clipboard = "PASTED";

        RightPress(root, new Vector2(300, 100));
        ClickMenuRow(root, area, rowIndex: 2);
        Assert.AreEqual("PASTED", area.Value);

        Press(area, Key.Z, Ctrl);

        Assert.AreEqual("original", area.Value);
    }

    [TestMethod]
    public void MenuSelectAllRow_SelectsEverything()
    {
        var root = AreaRoot(out var area);
        Assert.IsNull(area.SelectionAnchor);
        RightPress(root, new Vector2(300, 100));

        ClickMenuRow(root, area, rowIndex: 3);

        Assert.AreEqual(0, area.SelectionAnchor);
        Assert.AreEqual(area.Value.Length, area.CursorIndex);
    }

    /// <summary>Copy without a selection must not touch the clipboard, and leaves the menu open.</summary>
    [TestMethod]
    public void MenuCopyRow_WithoutSelection_DoesNothing()
    {
        var root = AreaRoot(out var area);
        _clipboard = "sentinel";
        RightPress(root, new Vector2(300, 100));

        ClickMenuRow(root, area, rowIndex: 1);

        Assert.AreEqual("sentinel", _clipboard);
        Assert.IsTrue(Menu(area).IsDisplayed, "a disabled row leaves the menu open");
    }

    [TestMethod]
    public void Escape_ClosesTheMenu()
    {
        var root = AreaRoot(out var area);
        RightPress(root, new Vector2(300, 100));
        Assert.IsTrue(Menu(area).IsDisplayed);

        Press(area, Key.Escape);

        Assert.IsFalse(Menu(area).IsDisplayed);
    }

    [TestMethod]
    public void PrimaryPress_ClosesTheMenu()
    {
        var root = AreaRoot(out var area);
        RightPress(root, new Vector2(300, 100));
        Assert.IsTrue(Menu(area).IsDisplayed);

        ClickAt(root, new Vector2(300, 200));

        Assert.IsFalse(Menu(area).IsDisplayed);
    }

    /// <summary>
    /// A text area is a scroll container, so the menu has to be anchored to the viewport rather
    /// than to the scrolled content. The observable contract is
    /// <see cref="Component.IgnoresScrollOffset"/>, which the paint and scroll-extent passes read;
    /// <see cref="IgnoresScrollOffset_KeepsAPopupOutOfTheScrollExtent"/> covers what it does.
    /// </summary>
    [TestMethod]
    public void Menu_IgnoresTheEditorScrollOffset()
    {
        AreaRoot(out var area);

        Assert.IsTrue(((Component)Menu(area)).IgnoresScrollOffset);
    }

    /// <summary>
    /// The behaviour behind that flag: an absolutely positioned child past the content's end
    /// extends the scroll extent unless it ignores the scroll offset.
    /// </summary>
    [TestMethod]
    public void IgnoresScrollOffset_KeepsAPopupOutOfTheScrollExtent()
    {
        var plain = ScrollerWithPopup(ignoresScroll: false, out var plainScroller);
        var pinned = ScrollerWithPopup(ignoresScroll: true, out var pinnedScroller);

        // The 300px content alone accounts for 200 of scrollable space in a 100px box; the popup
        // at y=350 adds another 90 only when it is not pinned.
        Assert.AreEqual(290f, plainScroller.ScrollableHeight, 1f, "a plain absolute child past the end extends it");
        Assert.AreEqual(200f, pinnedScroller.ScrollableHeight, 1f, "a pinned popup adds no scrollable space");

        // Same tree, so the difference is only the flag.
        Assert.AreEqual(plain.LayoutBorderPosition, pinned.LayoutBorderPosition);
    }

    /// <summary>A 100px-tall scroller holding 300px of content and one popup at y=350.</summary>
    private static View ScrollerWithPopup(bool ignoresScroll, out View scroller)
    {
        var root = new View { Name = "root" };
        root.Styles = root.Styles with { Width = 200, Height = 100 };

        scroller = new View { Name = "scroller" };
        scroller.Styles = scroller.Styles with
        {
            Position = Position.Absolute,
            Left = 0,
            Top = 0,
            Width = 200,
            Height = 100,
            Overflow = Overflow.Scroll,
        };

        var content = new View { Name = "content" };
        content.Styles = content.Styles with { Width = 200, Height = 300 };

        var popup = new View { Name = "popup", IgnoresScrollOffset = ignoresScroll };
        popup.Styles = popup.Styles with
        {
            Position = Position.Absolute,
            Left = 10,
            Top = 350,
            Width = 50,
            Height = 40,
        };

        scroller.AddChild(content);
        scroller.AddChild(popup);
        root.AddChild(scroller);
        root.LayoutAndRender(new Vector2(200, 100));
        return popup;
    }

    /// <summary>
    /// A pinned popup paints at the same place however far the container is scrolled, while a
    /// plain absolute child slides with the content. The popup has to stay inside the visible
    /// region: a child clipped out of view is not painted at all, so its paint origin is stale.
    /// </summary>
    [TestMethod]
    public void IgnoresScrollOffset_DoesNotMoveWithScroll()
    {
        Assert.AreEqual(60f, PopupTopAfterScrolling(ignoresScroll: true), 0.5f, "a pinned popup stays put");
        Assert.AreEqual(40f, PopupTopAfterScrolling(ignoresScroll: false), 0.5f, "a plain child slides with the content");
    }

    /// <summary>Scroller scrolled by 20 with a popup at content y=60; returns the popup's paint Y.</summary>
    private static float PopupTopAfterScrolling(bool ignoresScroll)
    {
        var root = new View { Name = "root" };
        root.Styles = root.Styles with { Width = 200, Height = 100 };

        var scroller = new View { Name = "scroller" };
        scroller.Styles = scroller.Styles with
        {
            Position = Position.Absolute,
            Left = 0,
            Top = 0,
            Width = 200,
            Height = 100,
            Overflow = Overflow.Scroll,
        };
        var content = new View { Name = "content" };
        content.Styles = content.Styles with { Width = 200, Height = 300 };
        var popup = new View { Name = "popup", IgnoresScrollOffset = ignoresScroll };
        popup.Styles = popup.Styles with
        {
            Position = Position.Absolute,
            Left = 10,
            Top = 60,
            Width = 50,
            Height = 20,
        };
        scroller.AddChild(content);
        scroller.AddChild(popup);
        root.AddChild(scroller);

        root.LayoutAndRender(new Vector2(200, 100));
        scroller.ScrollTop = 20;
        root.LayoutAndRender(new Vector2(200, 100));
        return popup.LayoutBorderPosition.Y;
    }

    /// <summary>
    /// A pinned popup must still be painted once its container is scrolled. The off-screen cull
    /// has to test the same origin the paint uses, or a viewport-anchored popup is culled as
    /// "above the viewport" by the scroll offset and disappears - while every layout assertion
    /// still passes.
    /// </summary>
    [TestMethod]
    public void IgnoresScrollOffset_PopupStaysPaintedAfterScrolling()
    {
        var root = new View { Name = "root" };
        root.Styles = root.Styles with { Width = 200, Height = 100 };

        var scroller = new View { Name = "scroller" };
        scroller.Styles = scroller.Styles with
        {
            Position = Position.Absolute,
            Left = 0,
            Top = 0,
            Width = 200,
            Height = 100,
            Overflow = Overflow.Scroll,
        };
        var content = new View { Name = "content" };
        content.Styles = content.Styles with { Width = 200, Height = 300 };
        var popup = new View { Name = "popup", IgnoresScrollOffset = true };
        popup.Styles = popup.Styles with
        {
            Position = Position.Absolute,
            Left = 10,
            Top = 10,
            Width = 50,
            Height = 40,
        };
        scroller.AddChild(content);
        scroller.AddChild(popup);
        root.AddChild(scroller);

        root.LayoutAndRender(new Vector2(200, 100));
        Assert.IsTrue(popup.ScissorRect.Width > 0f, "the popup must be painted while unscrolled");

        scroller.ScrollTop = 80;
        root.LayoutAndRender(new Vector2(200, 100));

        // The cull leaves a skipped node with an empty clip, which is what stops it painting and
        // absorbing presses, so ScissorRect is the public view of the whole contract.
        Assert.IsTrue(popup.ScissorRect.Width > 0f, "the popup must still be painted after the container scrolls");
    }

    /// <summary>
    /// The menu is clamped into the editor's visible region, so opening it near the bottom edge
    /// does not put it half outside.
    /// </summary>
    [TestMethod]
    public void ContextMenu_StaysInsideTheEditor()
    {
        var root = AreaRoot(out var area);
        var bottom = area.LayoutPaddingPosition.Y + area.LayoutPaddingSize.Y;

        RightPress(root, new Vector2(300, bottom - 5));
        root.LayoutAndRender(new Vector2(600, 400));

        var menu = Menu(area);
        Assert.IsTrue(menu.IsDisplayed);
        var menuBottom = menu.LayoutBorderPosition.Y + menu.LayoutBorderSize.Y;
        Assert.IsTrue(menuBottom <= bottom + 1, $"menu bottom {menuBottom} escaped the editor ({bottom})");
        Assert.IsTrue(menu.LayoutBorderPosition.Y >= area.LayoutPaddingPosition.Y, "the menu must not sit above the editor");
    }

    /// <summary>
    /// The keyboard shortcuts drive the same commands the menu does, so the two cannot drift.
    /// </summary>
    [TestMethod]
    public void KeyboardShortcuts_StillWork()
    {
        var root = AreaRoot(out var area);

        Press(area, Key.A, Ctrl);
        Press(area, Key.C, Ctrl);
        Assert.AreEqual(area.Value, _clipboard, "Ctrl+A then Ctrl+C copies the document");

        // Select the first character, cut it, then paste over the (now empty) selection start.
        Press(area, Key.Home, Ctrl);
        Press(area, Key.Right, Shift);
        Press(area, Key.X, Ctrl);
        Assert.AreEqual(0, area.CursorIndex, "the caret sits where the cut happened");

        _clipboard = "Z";
        Press(area, Key.V, Ctrl);
        // The first character was the "l" of "line 0", so pasting there gives "Zine 0".
        Assert.IsTrue(area.Value.StartsWith("Zine 0", StringComparison.Ordinal), area.Value[..Math.Min(20, area.Value.Length)]);
    }
}
