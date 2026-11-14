using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards the key handling of <see cref="TextInput"/>.
///
/// SDL reports Backspace and Enter only as key presses, never as text input, so the
/// <c>'\b'</c> and <c>'\r'/'\n'</c> branches in <see cref="TextInput.OnKeyTyped"/> never run on
/// this platform — the OnKeyPressed cases are the path that actually edits text.
/// </summary>
[TestClass]
public class TextInputKeyTests
{
    private string _clipboard = "";
    private Func<string>? _previousGet;
    private Action<string>? _previousSet;

    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();

        // Every field reads the process-wide clipboard, so each test installs its own and the
        // previous one is restored afterwards - otherwise a fake clipboard leaks into the next.
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

    /// <summary>A focused input whose text was typed character by character, so the cursor sits at the end.</summary>
    private static TextInput FocusedInput(string initial)
    {
        var input = new TextInput { Name = "input" };
        input.Focus();
        foreach (var c in initial)
        {
            input.DispatchKeyTyped(new KeyboardTypingEvent(c));
        }

        return input;
    }

    private static void Press(TextInput input, Key key, Keys held = default)
    {
        input.DispatchKeyPressed(new KeyboardEvent(key, key, held));
    }

    [TestMethod]
    public void BackspaceKey_RemovesCharacterBeforeCursor()
    {
        var input = FocusedInput("abc");

        Press(input, Key.Back);

        Assert.AreEqual("ab", input.Text);
    }

    [TestMethod]
    public void BackspaceKey_AtStartOfText_LeavesTextUnchanged()
    {
        var input = FocusedInput("abc");
        Press(input, Key.Home);

        Press(input, Key.Back);

        Assert.AreEqual("abc", input.Text);
    }

    [TestMethod]
    public void BackspaceKey_WithSelection_DeletesOnlySelection()
    {
        var input = FocusedInput("abc");
        // Select the whole word, then backspace over it.
        Press(input, Key.Home);
        Press(input, Key.End, default(Keys) | Key.ShiftKey);

        Press(input, Key.Back);

        Assert.AreEqual("", input.Text);
    }

    [TestMethod]
    public void EnterKey_SubmitsCurrentText()
    {
        var input = FocusedInput("hello");
        string? submitted = null;
        input.Submitted = value => submitted = value;

        Press(input, Key.Enter);

        Assert.AreEqual("hello", submitted);
    }

    [TestMethod]
    public void DeleteKey_RemovesCharacterAfterCursor()
    {
        var input = FocusedInput("abc");
        Press(input, Key.Home);

        Press(input, Key.Delete);

        Assert.AreEqual("bc", input.Text);
    }

    [TestMethod]
    public void UnfocusedInput_IgnoresBackspace()
    {
        var input = FocusedInput("abc");
        input.Blur();

        Press(input, Key.Back);

        Assert.AreEqual("abc", input.Text);
    }

    /// <summary>
    /// A masked field must not draw the secret. The value in <see cref="TextInput.Text"/> is the
    /// real one, so only the inner text is masked.
    /// </summary>
    [TestMethod]
    public void MaskCharacter_HidesTheValueButKeepsIt()
    {
        var input = new TextInput { Name = "password", MaskCharacter = '*' };
        input.Focus();
        foreach (var c in "hunter2")
            input.DispatchKeyTyped(new KeyboardTypingEvent(c));

        Assert.AreEqual("hunter2", input.Text, "the real value must survive");
        Assert.AreEqual("*******", DisplayedText(input));
    }

    [TestMethod]
    public void MaskCharacter_ShowsThePlaceholderWhenEmpty()
    {
        var input = new TextInput { Name = "password", MaskCharacter = '*', Placeholder = "Password" };

        Assert.AreEqual("Password", DisplayedText(input));
    }

    private static string? DisplayedText(TextInput input)
        => ((Text)input.VisualChildren[0]).Children.OfType<TextNode>().FirstOrDefault()?.Text;

    // ── Clipboard shortcuts ────────────────────────────────────────

    private static Keys Ctrl => default(Keys) | Key.LControlKey;
    private static Keys Shift => default(Keys) | Key.LShiftKey;

    [TestMethod]
    public void CtrlA_SelectsEverything_AndCtrlC_CopiesIt()
    {
        var input = FocusedInput("hello");

        Press(input, Key.A, Ctrl);
        Press(input, Key.C, Ctrl);

        Assert.AreEqual("hello", ReadClipboard());
    }

    [TestMethod]
    public void CtrlX_CutsTheSelection_AndLeavesTheRest()
    {
        var input = FocusedInput("hello world");

        Press(input, Key.A, Ctrl);
        Press(input, Key.X, Ctrl);

        Assert.AreEqual("", input.Text, "select-all then cut empties the field");
        Assert.AreEqual("hello world", ReadClipboard());
    }

    [TestMethod]
    public void CtrlV_PastesAtTheCursor_AndStripsLineBreaks()
    {
        var input = FocusedInput("ab");
        Press(input, Key.Home);

        SetClipboard("X\r\nY");
        Press(input, Key.V, Ctrl);

        Assert.AreEqual("XYab", input.Text);
    }

    [TestMethod]
    public void CtrlV_ReplacesTheSelection()
    {
        var input = FocusedInput("abc");
        Press(input, Key.Home);
        Press(input, Key.Right, Shift);

        SetClipboard("Z");
        Press(input, Key.V, Ctrl);

        Assert.AreEqual("Zbc", input.Text);
    }

    /// <summary>
    /// Cut and Copy do nothing without a selection, and must not throw when no clipboard is
    /// wired at all - a test host or an embedded field has none.
    /// </summary>
    [TestMethod]
    public void CtrlC_WithNoSelectionAndNoClipboard_IsANoOp()
    {
        TextInput.DefaultGetClipboardText = null;
        TextInput.DefaultSetClipboardText = null;
        var input = FocusedInput("abc");

        Press(input, Key.C, Ctrl);
        Press(input, Key.X, Ctrl);
        Press(input, Key.V, Ctrl);

        Assert.AreEqual("abc", input.Text);
    }

    // ── Context menu ───────────────────────────────────────────────

    /// <summary>
    /// A secondary press opens the menu and keeps the field focused: right-clicking is how a
    /// user reaches Paste without losing what they were editing.
    /// </summary>
    [TestMethod]
    public void SecondaryPress_OpensTheContextMenu_AndKeepsTheFieldFocused()
    {
        var root = MenuRoot(out var input);
        input.Focus();
        Type(input, "hello");

        RightPress(root, input);

        Assert.IsTrue(Menu(input).IsDisplayed, "the menu must be open");
        Assert.IsTrue(input.IsFocused, "the field must keep focus");
        Assert.AreEqual("hello", input.Text);
    }

    [TestMethod]
    public void ClickingPasteInTheMenu_PastesAndClosesIt()
    {
        var root = MenuRoot(out var input);
        input.Focus();
        SetClipboard("pasted");
        RightPress(root, input);

        ClickMenuRow(root, input, rowIndex: 2);

        Assert.AreEqual("pasted", input.Text);
        Assert.IsFalse(Menu(input).IsDisplayed, "the menu closes after an action");
    }

    /// <summary>
    /// Copy is offered only when there is a selection. A disabled row swallows the click
    /// without acting, so the clipboard and the field are untouched and the menu stays open.
    /// </summary>
    [TestMethod]
    public void MenuCopyRow_DoesNothingWithoutASelection()
    {
        var root = MenuRoot(out var input);
        input.Focus();
        SetClipboard("sentinel");
        RightPress(root, input);

        ClickMenuRow(root, input, rowIndex: 1);

        Assert.AreEqual("sentinel", ReadClipboard(), "nothing was copied");
        Assert.IsTrue(Menu(input).IsDisplayed, "a disabled row leaves the menu open");
    }

    [TestMethod]
    public void MenuCopyRow_CopiesTheSelection()
    {
        var root = MenuRoot(out var input);
        input.Focus();
        Type(input, "hello");
        Press(input, Key.A, Ctrl);
        RightPress(root, input);

        ClickMenuRow(root, input, rowIndex: 1);

        Assert.AreEqual("hello", ReadClipboard());
        Assert.IsFalse(Menu(input).IsDisplayed);
    }

    [TestMethod]
    public void Escape_ClosesTheContextMenu()
    {
        var root = MenuRoot(out var input);
        input.Focus();
        RightPress(root, input);
        Assert.IsTrue(Menu(input).IsDisplayed);

        Press(input, Key.Escape);

        Assert.IsFalse(Menu(input).IsDisplayed);
    }

    /// <summary>
    /// The menu is anchored inside the input, so it stays within the visible region: a field at
    /// the bottom of the window must not open a menu that runs off the edge.
    /// </summary>
    [TestMethod]
    public void ContextMenu_StaysInsideTheVisibleRegion()
    {
        var root = MenuRoot(out var input, height: 300, inputTop: 268);
        input.Focus();

        RightPress(root, input);
        // The menu's box only exists once it has been laid out: opening it after the last
        // layout pass leaves stale (zero) geometry behind.
        root.LayoutAndRender(new Vector2(400, 300));

        var menu = Menu(input);
        Assert.IsTrue(menu.IsDisplayed);
        var bottom = menu.LayoutBorderPosition.Y + menu.LayoutBorderSize.Y;
        Assert.IsTrue(bottom <= 300 + 1, $"menu bottom {bottom} escaped the viewport");
        Assert.IsTrue(menu.LayoutBorderPosition.Y >= 0, "the menu must not be pushed above the window");
    }

    /// <summary>A press on the field itself closes the menu, so it never floats over an edit.</summary>
    [TestMethod]
    public void PrimaryPressOnTheField_ClosesTheContextMenu()
    {
        var root = MenuRoot(out var input);
        input.Focus();
        RightPress(root, input);
        Assert.IsTrue(Menu(input).IsDisplayed);

        ClickAt(root, input.LayoutPaddingPosition + input.LayoutPaddingSize / 2);

        Assert.IsFalse(Menu(input).IsDisplayed);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static View MenuRoot(out TextInput input, float height = 300, float inputTop = 20)
    {
        const float width = 400f;
        var root = new View { Name = "root" };
        root.Styles = root.Styles with { Width = width, Height = height };

        input = new TextInput { Name = "input" };
        input.Styles = input.Styles with
        {
            Position = Position.Absolute,
            Left = 20,
            Top = inputTop,
            Width = 300,
            Height = 30,
            PaddingLeft = 8,
            PaddingRight = 8,
        };
        root.AddChild(input);
        root.LayoutAndRender(new Vector2(width, height));
        return root;
    }

    /// <summary>The menu is the input's only <see cref="View"/> child.</summary>
    private static View Menu(TextInput input) => input.VisualChildren.OfType<View>().Single();

    private static void Type(TextInput input, string text)
    {
        foreach (var c in text)
            input.DispatchKeyTyped(new KeyboardTypingEvent(c));
    }

    private static void RightPress(View root, TextInput input)
    {
        var at = input.LayoutPaddingPosition + input.LayoutPaddingSize / 2;
        root.DispatchMousePressed(new BaseMouseEvent(at, MouseButton.Secondary, MouseButtons.Secondary, false, false, false));
        root.DispatchMouseReleased(new BaseMouseEvent(at, MouseButton.Secondary, MouseButtons.None, false, false, false));
    }

    /// <summary>
    /// Clicks a row by index (Cut, Copy, Paste, Select All), after laying out the opened menu so
    /// the row has a real position - the same path a user's click takes.
    /// </summary>
    private static void ClickMenuRow(View root, TextInput input, int rowIndex)
    {
        var menu = Menu(input);
        root.LayoutAndRender(new Vector2(400, 300));
        var row = (Component)menu.Children[rowIndex];
        ClickAt(root, row.LayoutPaddingPosition + row.LayoutPaddingSize / 2);
    }

    private static void ClickAt(View root, Vector2 at)
    {
        root.DispatchMousePressed(new BaseMouseEvent(at, MouseButton.Primary, MouseButtons.Primary, false, false, false));
        root.DispatchMouseReleased(new BaseMouseEvent(at, MouseButton.Primary, MouseButtons.None, false, false, false));
    }

    private string ReadClipboard() => _clipboard;

    private void SetClipboard(string value) => _clipboard = value;
}
