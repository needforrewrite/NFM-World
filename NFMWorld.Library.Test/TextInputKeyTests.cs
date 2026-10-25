using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

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
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
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
}
