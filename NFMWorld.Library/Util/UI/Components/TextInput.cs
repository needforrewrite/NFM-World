using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Lua;
using NFMWorld.Reactor;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using MouseButton = NFMWorld.DriverInterface.MouseButton;

namespace NFMWorld.Reactor;

/// <summary>
/// A single-line text input component. Contains a <see cref="Reactor.Text"/> child for text
/// layout and measurement, plus a cursor overlay child rendered on top for
/// the blinking cursor and selection highlight.
/// </summary>
[LuaVisible]
public partial class TextInput : Component
{
    // ── Internal children ──────────────────────────────────────────

    private readonly Text _text;
    private readonly CursorOverlay _cursorOverlay;

    // ── Cursor & selection state ──────────────────────────────────

    /// <summary>Character index where the cursor sits (0 = before first char).</summary>
    private int _cursorIndex;

    /// <summary>
    /// Selection anchor index. When non-null, text between <see cref="_selectionAnchor"/>
    /// and <see cref="_cursorIndex"/> is selected.
    /// </summary>
    private int? _selectionAnchor;

    /// <summary>Whether a mouse drag-selection is in progress.</summary>
    private bool _isDragging;

    // ── Double/triple click ───────────────────────────────────────

    /// <summary>Number of consecutive clicks within <see cref="ClickIntervalMs"/>.</summary>
    private int _clickCount;

    /// <summary>Ticks (ms) of the most recent primary click.</summary>
    private long _lastClickTimeMs;

    /// <summary>Character index of the most recent primary click (position proximity).</summary>
    private int _lastClickCharIndex;

    // ── Cursor blink ──────────────────────────────────────────────

    /// <summary>Blink period for the cursor in milliseconds.</summary>
    private const float CursorBlinkPeriodMs = 530f;

    /// <summary>Max gap (ms) between consecutive clicks to count as a double/triple click.</summary>
    private const long ClickIntervalMs = 500;

    private float _cursorBlinkTimer;
    private bool _cursorVisible;

    // ── Styled properties ──────────────────────────────────────────

    public TextStyles TextStyles
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                _text.TextStyles = TextStyles;
                OnTextInvalidated(false);
            }
        }
    } = new();

    public TextInputStyles TextInputStyles
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnTextInvalidated(false);
            }
        }
    } = new();

    /// <summary>
    /// Placeholder text shown when <see cref="Text"/> is empty and the input is not focused.
    /// </summary>
    [LuaName]
    public string Placeholder
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnTextInvalidated(false);
            }
        }
    } = "";

    /// <summary>
    /// Raised when the user presses Enter. The current <see cref="Text"/> is passed as the argument.
    /// </summary>
    public Action<string>? Submitted { get; set; }

    /// <summary>
    /// Raised whenever the text content changes (typing, backspace, delete, paste, programmatic set).
    /// The new text value is passed as the argument.
    /// </summary>
    public Action<string>? TextChanged { get; set; }

    // ── Proxied TextRun properties ─────────────────────────────────

    /// <inheritdoc cref="Reactor.Text.TextContent"/>
    [LuaName]
    public string? Text
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                _text.TextContent = value;
            }
        }
    }

    public void SetText(string text)
    {
        Text = text;
        OnTextInvalidated(false);
    }

    public void SetTextFromUserInput(string text)
    {
        Text = text;
        OnTextInvalidated();
    }

    // ── Constructor ───────────────────────────────────────────────

    public TextInput()
    {
        // Create the internal text/cursor children first: the style property
        // setters below call OnTextInvalidated(), which dereferences `_text`.
        _text = new Text { IsFocusable = false };
        _cursorOverlay = new CursorOverlay(this);

        Styles = Styles with
        {
            BorderColor = new Color(255, 255, 255, 255),
            BackgroundColor = new Color(20, 20, 30, 255),
        };

        // Make this node the containing block for its absolutely-positioned
        // CursorOverlay child. This flag lives on the Yoga node (NOT in Styles), so it
        // survives the fresh Styles assignment Lua applies when setting props (which
        // resets Position to Static). Without a containing block, the overlay's inset-0
        // box resolves against a higher ancestor and the cursor/selection are drawn at
        // screen-global coordinates instead of inside the input.
        AlwaysFormsContainingBlock = true;

        TextInputStyles = TextInputStyles with
        {
            CursorColor = new Color(255, 255, 255, 255),
            SelectionColor = new Color(100, 180, 255, 128),
            PlaceholderColor = new Color(128, 128, 128, 255),
        };

        TextStyles = TextStyles with
        {
            ForegroundColor = new Color(255, 255, 255),
            FontFamily = FontFamily.DroidSans,
            FontSize = 12f,
            FontStyle = FontStyle.Plain,
            HorizontalAlignment = TextHorizontalAlignment.Left,
            VerticalAlignment = TextVerticalAlignment.Top
        };

        IsFocusable = true;

        NodeInternal.InsertChild(_text.Contents, 0);
        _text.VisualParent = this;

        NodeInternal.InsertChild(_cursorOverlay.Contents, 1);
        _cursorOverlay.VisualParent = this;

        _visualChildren = [_text, _cursorOverlay];
        VisualChildren = new ReadOnlyLuaArray<Node>(_visualChildren);
    }

    // ── Visual children (void element — no external children) ─────

    private readonly Node[] _visualChildren;
    public override ReadOnlyLuaArray<Node> VisualChildren { get; }

    // ── Text invalidation ─────────────────────────────────────────

    private void OnTextInvalidated(bool isFromUserInput = true)
    {
        var len = (Text ?? "").Length;
        if (_cursorIndex > len)
            _cursorIndex = len;
        _selectionAnchor = null;

        // Reset cursor blink so it is visible immediately after typing
        _cursorBlinkTimer = 0;
        _cursorVisible = true;

        if (isFromUserInput)
            TextChanged?.Invoke(Text ?? "");

        // Draw placeholder when empty and not focused
        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder))
        {
            _text.TextStyles = _text.TextStyles with { ForegroundColor = TextInputStyles.PlaceholderColor };
            _text.TextContent = Placeholder;
        }
        else
        {
            _text.TextStyles = _text.TextStyles with { ForegroundColor = TextStyles.ForegroundColor };
            _text.TextContent = Text;
        }
    }

    // ── Selection helpers ──────────────────────────────────────────

    /// <summary>Gets the start and end (inclusive-exclusive) of the current selection, or null.</summary>
    private (int start, int end)? GetSelectionRange()
    {
        if (_selectionAnchor is not { } anchor || anchor == _cursorIndex)
            return null;

        var start = Math.Min(anchor, _cursorIndex);
        var end = Math.Max(anchor, _cursorIndex);
        return (start, end);
    }

    /// <summary>Returns the current text, or empty string if null.</summary>
    private string CurrentText => Text ?? "";

    /// <summary>Clears any active selection.</summary>
    private void ClearSelection()
    {
        _selectionAnchor = null;
    }

    /// <summary>Deletes the selected range if any, returns true if something was deleted.</summary>
    private bool DeleteSelection()
    {
        var sel = GetSelectionRange();
        if (sel is not { } range)
            return false;

        var t = CurrentText;
        SetTextFromUserInput(t[..range.start] + t[range.end..]);
        _cursorIndex = range.start;
        ClearSelection();
        return true;
    }

    // ── Cursor position from character index ───────────────────────

    /// <summary>
    /// Returns the x-offset (in screen pixels) for the given character index.
    /// Uses the internal <see cref="_text"/> laid-out text for accurate measurement.
    /// </summary>
    /// <remarks>
    /// Laid-out positions are already in screen pixels: text is measured with a font whose
    /// size has <see cref="G.Scale"/> baked in (<see cref="Text.RelayoutText"/> passes it as
    /// the measure scale), which is also why <see cref="Text.RenderContent"/> draws them
    /// without scaling. Scaling them again here would push the cursor out by a factor of
    /// <see cref="G.Scale"/>.
    /// </remarks>
    private float GetCursorXForCharIndex(int charIndex)
    {
        var laidOut = _text.LaidOutComplexText;
        if (laidOut is not { } container || container.Elements.Count == 0)
            return 0;

        var targetIdx = Math.Clamp(charIndex, 0, CurrentText.Length);
        var accumIdx = 0;

        foreach (var elem in container.Elements)
        {
            var elemLen = elem.Text.Length;
            if (accumIdx + elemLen <= targetIdx)
            {
                accumIdx += elemLen;
                continue;
            }

            // Cursor is within this element
            var charsBefore = targetIdx - accumIdx;
            if (charsBefore <= 0)
                return elem.Position.X;

            var fontMetrics = G.GetFontMetrics(elem.Font);
            var measured = fontMetrics.MeasureText(elem.Text.AsSpan(..charsBefore));
            return elem.Position.X + measured.X;
        }

        // Cursor is at the very end — after the last element
        if (container.Elements.Count > 0)
        {
            var last = container.Elements[^1];
            var fontMetrics = G.GetFontMetrics(last.Font);
            var measured = fontMetrics.MeasureText(last.Text);
            return last.Position.X + measured.X;
        }

        return 0;
    }

    /// <summary>
    /// Returns the character index closest to a given x-offset (in screen pixels).
    /// </summary>
    private int GetCharIndexForCursorX(float cursorX)
    {
        var laidOut = _text.LaidOutComplexText;
        if (laidOut is not { } container || container.Elements.Count == 0)
            return 0;

        var accumIdx = 0;

        foreach (var elem in container.Elements)
        {
            var elemStartX = elem.Position.X;
            var fontMetrics = G.GetFontMetrics(elem.Font);
            var elemWidth = fontMetrics.MeasureText(elem.Text).X;

            if (cursorX < elemStartX)
                return accumIdx;

            if (cursorX >= elemStartX && cursorX <= elemStartX + elemWidth)
            {
                // Clicked within this element — find closest character
                var relX = cursorX - elemStartX;
                var bestIdx = 0;
                var bestDist = float.MaxValue;
                for (var i = 0; i <= elem.Text.Length; i++)
                {
                    var charX = fontMetrics.MeasureText(elem.Text.AsSpan(..i)).X;
                    var dist = Math.Abs(charX - relX);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                    }
                }
                return accumIdx + bestIdx;
            }

            accumIdx += elem.Text.Length;
        }

        // Clicked past all elements — cursor at end
        return accumIdx;
    }

    // ── Keyboard input ─────────────────────────────────────────────

    protected override void OnKeyTyped(KeyboardTypingEvent @event)
    {
        var c = @event.KeyChar;

        // Backspace
        if (c == '\b')
        {
            HandleBackspace();
            return;
        }

        // Enter / Return — submit
        if (c is '\r' or '\n')
        {
            HandleSubmit();
            return;
        }

        // Ignore other control characters
        if (char.IsControl(c))
            return;

        // Replace selection or insert at cursor
        DeleteSelection();
        var t2 = CurrentText;
        var idx = Math.Min(_cursorIndex, t2.Length);
        SetTextFromUserInput(t2[..idx] + c + t2[idx..]);
        _cursorIndex = idx + 1;
    }

    /// <remarks>
    /// SDL only reports Backspace as a key press, never as text input, so this is the path
    /// that actually runs; the '\b' branch above covers platforms that do synthesize it.
    /// </remarks>
    public override void OnKeyPressed(KeyboardEvent @event)
    {
        var shift = @event.Keys.ShiftKey;
        var ctrl = @event.Keys.ControlKey;
        var key = @event.KeyCode;

        switch (key)
        {
            case Key.Left:
                HandleArrow(-1, shift, ctrl);
                break;
            case Key.Right:
                HandleArrow(+1, shift, ctrl);
                break;
            case Key.Home:
                HandleHome(shift);
                break;
            case Key.End:
                HandleEnd(shift);
                break;
            case Key.Back:
                HandleBackspace();
                break;
            case Key.Delete:
                HandleDelete();
                break;
            case Key.Enter:
                HandleSubmit();
                break;
        }
    }

    private void HandleBackspace()
    {
        if (DeleteSelection())
            return;

        if (_cursorIndex > 0)
        {
            var t = CurrentText;
            var ci = Math.Min(_cursorIndex, t.Length);
            SetTextFromUserInput(t[..(ci - 1)] + t[ci..]);
            _cursorIndex = ci - 1;
        }
    }

    private void HandleSubmit()
    {
        Submitted?.Invoke(Text ?? "");
    }

    private void HandleArrow(int direction, bool shift, bool ctrl)
    {
        var len = CurrentText.Length;

        if (ctrl)
        {
            // Word-skip: move to next/previous word boundary
            var target = _cursorIndex;
            if (direction < 0)
            {
                while (target > 0 && char.IsWhiteSpace(CurrentText[target - 1]))
                    target--;
                while (target > 0 && !char.IsWhiteSpace(CurrentText[target - 1]))
                    target--;
            }
            else
            {
                while (target < len && !char.IsWhiteSpace(CurrentText[target]))
                    target++;
                while (target < len && char.IsWhiteSpace(CurrentText[target]))
                    target++;
            }
            MoveCursorTo(target, shift);
        }
        else
        {
            MoveCursorTo(_cursorIndex + direction, shift);
        }
    }

    private void HandleHome(bool shift)
    {
        MoveCursorTo(0, shift);
    }

    private void HandleEnd(bool shift)
    {
        MoveCursorTo(CurrentText.Length, shift);
    }

    private void HandleDelete()
    {
        if (DeleteSelection())
            return;

        var t = CurrentText;
        var idx = Math.Min(_cursorIndex, t.Length);
        if (idx < t.Length)
        {
            SetTextFromUserInput(t[..idx] + t[(idx + 1)..]);
        }
    }

    private void MoveCursorTo(int index, bool extendSelection)
    {
        index = Math.Clamp(index, 0, CurrentText.Length);

        if (extendSelection)
        {
            _selectionAnchor ??= _cursorIndex;
        }
        else
        {
            ClearSelection();
        }

        _cursorIndex = index;

        // Reset blink so the cursor is visible immediately
        _cursorBlinkTimer = 0;
        _cursorVisible = true;
    }

    /// <summary>Selects the word containing (or adjacent to) the given character index.</summary>
    private void SelectWord(int charIndex)
    {
        var t = CurrentText;
        var idx = Math.Clamp(charIndex, 0, t.Length);
        int start = idx, end = idx;
        while (start > 0 && !char.IsWhiteSpace(t[start - 1]))
            start--;
        while (end < t.Length && !char.IsWhiteSpace(t[end]))
            end++;

        _selectionAnchor = start;
        _cursorIndex = end;
        _cursorBlinkTimer = 0;
        _cursorVisible = true;
    }

    /// <summary>Selects all text (triple-click).</summary>
    private void SelectAll()
    {
        _selectionAnchor = 0;
        _cursorIndex = CurrentText.Length;
        _cursorBlinkTimer = 0;
        _cursorVisible = true;
    }

    // ── Mouse input ────────────────────────────────────────────────

    [ClientOnly]
    protected override void OnMousePressed(MouseEvent @event)
    {
        if (@event.Button != MouseButton.Primary)
            return;

        var shift = @event.ShiftKey;
        // RelativePosition is padding-box-relative; convert to content-box-relative X
        var charIdx = GetCharIndexForCursorX(@event.RelativePosition.X - LayoutPaddingLeft);

        // Count consecutive clicks (same spot, within the interval) for double/triple click.
        var now = Environment.TickCount64;
        var withinTime = now - _lastClickTimeMs <= ClickIntervalMs;
        var sameSpot = Math.Abs(charIdx - _lastClickCharIndex) <= 1;
        _clickCount = withinTime && sameSpot ? _clickCount + 1 : 1;
        _lastClickTimeMs = now;
        _lastClickCharIndex = charIdx;

        if (_clickCount >= 3)
        {
            SelectAll();
        }
        else if (_clickCount == 2)
        {
            SelectWord(charIdx);
        }
        else
        {
            MoveCursorTo(charIdx, shift);
        }

        _isDragging = true;
    }

    protected override void OnMouseReleased(MouseEvent @event)
    {
        _isDragging = false;
    }

    [ClientOnly]
    protected override void OnMouseDragged(MouseDragEvent @event)
    {
        if (!_isDragging)
            return;

        _clickCount = 1; // a drag is not part of a multi-click sequence

        // RelativePosition is padding-box-relative; convert to content-box-relative X
        var charIdx = GetCharIndexForCursorX(@event.RelativePosition.X - LayoutPaddingLeft);
        // Always extend selection during drag
        _selectionAnchor ??= _cursorIndex;
        _cursorIndex = Math.Clamp(charIdx, 0, CurrentText.Length);
        _cursorBlinkTimer = 0;
        _cursorVisible = true;
    }

    // ── Cursor blink ───────────────────────────────────────────────

    protected override void GameTick()
    {
        _cursorBlinkTimer += 1000f / Physics.TargetTps; // approximate per-frame delta
        if (_cursorBlinkTimer >= CursorBlinkPeriodMs)
        {
            _cursorBlinkTimer -= CursorBlinkPeriodMs;
            _cursorVisible = !_cursorVisible;
        }
    }

    // ── Cursor overlay (inner class) ───────────────────────────────

    /// <summary>
    /// Absolutely-positioned child that renders the cursor line and selection highlight
    /// on top of the text content. Rendered last in the child order so it appears above
    /// the <see cref="Text"/> child's text.
    /// </summary>
    private sealed class CursorOverlay : Component
    {
        private readonly TextInput _owner;

        public CursorOverlay(TextInput owner)
        {
            _owner = owner;
            Styles = Styles with
            {
                Position = Position.Absolute,
                Top = MeasurementMarginPosition.Point(0),
                Left = MeasurementMarginPosition.Point(0),
                Right = MeasurementMarginPosition.Point(0),
                Bottom = MeasurementMarginPosition.Point(0),
            };
            IsFocusable = false;
        }

        [ClientOnly]
        protected override void RenderContent(LuaVector2 position, LuaVector2 size)
        {
            if (!_owner.IsFocused)
                return;

            // The overlay is absolutely positioned at the padding box edge,
            // but text starts at the content box edge (after padding).
            // Offset by the owner's padding to align with the text content.
            var baseX = position.X + _owner.LayoutPaddingLeft;
            var contentTop = position.Y + _owner.LayoutPaddingTop;
            var contentBottom = position.Y + size.Y - _owner.LayoutPaddingBottom;

            // ── Draw selection highlight ────────────────────────
            var sel = _owner.GetSelectionRange();
            if (sel is { } range)
            {
                var selStartX = _owner.GetCursorXForCharIndex(range.start);
                var selEndX = _owner.GetCursorXForCharIndex(range.end);

                G.SetColor(_owner.TextInputStyles.SelectionColor);
                G.FillRect(
                    (int)(baseX + selStartX), (int)contentTop,
                    (int)(selEndX - selStartX), (int)(contentBottom - contentTop));
            }

            // ── Draw cursor ─────────────────────────────────────
            if (_owner._cursorVisible)
            {
                var cursorX = _owner.GetCursorXForCharIndex(_owner._cursorIndex);

                G.SetColor(_owner.TextInputStyles.CursorColor);
                var cursorTop = contentTop;
                var cursorBottom = contentBottom;
                G.DrawLine(
                    (int)(baseX + cursorX), (int)cursorTop,
                    (int)(baseX + cursorX), (int)cursorBottom);
            }
        }
    }
}

public struct TextInputStyles() : IEquatable<TextInputStyles>
{
    public Color CursorColor = new Color(255, 255, 255, 255);
    public Color SelectionColor = new Color(100, 180, 255, 128);
    public Color PlaceholderColor = new Color(128, 128, 128, 255);

    public bool Equals(TextInputStyles other)
    {
        return CursorColor.Equals(other.CursorColor) && SelectionColor.Equals(other.SelectionColor) && PlaceholderColor.Equals(other.PlaceholderColor);
    }

    public override bool Equals(object? obj)
    {
        return obj is TextInputStyles other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CursorColor, SelectionColor, PlaceholderColor);
    }

    public static bool operator ==(TextInputStyles left, TextInputStyles right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TextInputStyles left, TextInputStyles right)
    {
        return !left.Equals(right);
    }
}