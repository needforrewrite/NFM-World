using System.Numerics;
using Maxine.Extensions.Mathematics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.PluginSystem;
using SxApi = NFMWorld.Reactor.Sx.Sx;

namespace NFMWorld.Reactor;

/// <summary>
/// A multiline text editor: a scrollable monospace text block with a caret, selection,
/// keyboard editing and pointer placement.
/// </summary>
/// <remarks>
/// The text is one <see cref="Text"/> child so the existing rich-text layout handles the
/// newlines; the caret and selection are drawn by an overlay child positioned from that
/// same layout, so they cannot drift from the glyphs.
/// </remarks>
public partial class TextArea : View
{
    private readonly EditableText _text;

    private string _value = "";
    private int _cursor;
    private int? _selectionAnchor;
    private bool _dragging;
    private int _clickCount;
    private long _lastClickTimeMs;
    private int _lastClickIndex;
    private float _blinkTimer;
    private bool _caretVisible = true;

    // Only scroll to the caret when it actually moved; doing it every layout would fight
    // wheel scrolling while the editor is focused.
    private bool _scrollCaretIntoView = true;
    private bool _wasFocused;
    private List<LineInfo>? _lineCache;

    private bool _draggingScrollbar;
    private bool _horizontalScrollbar;
    private float _scrollbarGrabOffset;

    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];
    private bool _suppressHistory;
    private bool _lastEditWasTyping;
    private long _lastEditTimestamp;

    private readonly record struct HistoryEntry(string Text, int Cursor, int? Anchor);

    private const float BlinkPeriodMs = 530f;
    private const long ClickIntervalMs = 500;
    private const int TabSpaces = 4;
    private const float ScrollbarWidth = 6f;
    private const float MinimumThumb = 20f;
    private const float GlyphTopRatio = 0.2f;
    private const int MaxHistoryEntries = 200;
    private const long TypingCoalesceMs = 700;

    public Action<string>? TextChanged { get; set; }
    public Func<string>? GetClipboardText { get; set; }
    public Action<string>? SetClipboardText { get; set; }

    /// <summary>Raised just before a user edit changes the text. The host can snapshot it.</summary>
    public Action? EditStarted { get; set; }

    /// <summary>
    /// When false, the control keeps no undo stack of its own; the host owns the document
    /// history. Used by the model editor's code view so structural edits share one stack.
    /// </summary>
    public bool UseLocalHistory { get; set; } = true;

    public string Value => _value;
    public int CursorIndex => _cursor;
    public int? SelectionAnchor => _selectionAnchor;

    public void SetCaret(int index, int? anchor)
    {
        _cursor = Math.Clamp(index, 0, _value.Length);
        _selectionAnchor = anchor is { } a ? Math.Clamp(a, 0, _value.Length) : null;
        _scrollCaretIntoView = true;
        ResetBlink();
    }

    private TextStyles _textStyles = new()
    {
        FontFamily = FontFamily.NotoSans,
        FontSize = 13,
        ForegroundColor = new Color(220, 232, 255),
    };

    public TextStyles TextStyles
    {
        get => _textStyles;
        set
        {
            if (_textStyles == value)
                return;

            _textStyles = value;
            _lineCache = null;
            if (_text != null)
                _text.TextStyles = value;
        }
    }

    public TextArea()
    {
        Styles = SxApi.Styles(
            position: Position.Relative,
            flexDirection: FlexDirection.Column,
            overflow: Overflow.Scroll,
            paddingTop: 8,
            paddingBottom: 8,
            paddingLeft: 10,
            paddingRight: 10,
            backgroundColor: new Color(6, 12, 24, 210),
            borderTop: 1,
            borderBottom: 1,
            borderLeft: 1,
            borderRight: 1,
            borderColor: new Color(128, 170, 255, 80),
            borderRadius: 6);

        IsFocusable = true;

        _text = new EditableText(this)
        {
            IsFocusable = false,
            Styles = SxApi.Styles(flexShrink: 0, alignSelf: Align.FlexStart),
            TextStyles = _textStyles,
        };
        AddChild(_text);
    }

    // ── Text / caret model ─────────────────────────────────────────────

    /// <summary>Loads text from outside (tab switch, reload). The caret goes to the top.</summary>
    public void SetTextExternal(string text)
    {
        if (_value == text)
            return;

        _value = text;
        _text.TextContent = text;
        _lineCache = null;
        _cursor = 0;
        _selectionAnchor = null;
        _scrollCaretIntoView = true;
        _undo.Clear();
        _redo.Clear();
        _lastEditWasTyping = false;
        ScrollTop = 0;
        ScrollLeft = 0;
        ResetBlink();
    }

    private void SetValueFromUser(string text)
    {
        if (_value == text)
            return;

        EditStarted?.Invoke();

        _value = text;
        _text.TextContent = text;
        _lineCache = null;
        _cursor = Math.Clamp(_cursor, 0, text.Length);
        if (_selectionAnchor is { } anchor)
            _selectionAnchor = Math.Clamp(anchor, 0, text.Length);
        _scrollCaretIntoView = true;
        TextChanged?.Invoke(text);
        ResetBlink();
    }

    private (int Start, int End)? Selection
    {
        get
        {
            if (_selectionAnchor is not { } anchor || anchor == _cursor)
                return null;
            return (Math.Min(anchor, _cursor), Math.Max(anchor, _cursor));
        }
    }

    private void DeleteSelection()
    {
        if (Selection is not { } range)
            return;

        SetValueFromUser(_value[..range.Start] + _value[range.End..]);
        _cursor = range.Start;
        _selectionAnchor = null;
    }

    private void PushHistory(bool typing)
    {
        if (_suppressHistory || !UseLocalHistory)
            return;

        var now = Environment.TickCount64;
        if (typing && _lastEditWasTyping && now - _lastEditTimestamp <= TypingCoalesceMs)
        {
            _lastEditTimestamp = now;
            return;
        }

        _undo.Add(new HistoryEntry(_value, _cursor, _selectionAnchor));
        if (_undo.Count > MaxHistoryEntries)
            _undo.RemoveAt(0);
        _redo.Clear();
        _lastEditWasTyping = typing;
        _lastEditTimestamp = now;
    }

    private void Undo()
    {
        if (_suppressHistory || _undo.Count == 0)
            return;

        _redo.Add(new HistoryEntry(_value, _cursor, _selectionAnchor));
        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        ApplyHistory(entry);
        _lastEditWasTyping = false;
    }

    private void Redo()
    {
        if (_suppressHistory || _redo.Count == 0)
            return;

        _undo.Add(new HistoryEntry(_value, _cursor, _selectionAnchor));
        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        ApplyHistory(entry);
        _lastEditWasTyping = false;
    }

    private void ApplyHistory(HistoryEntry entry)
    {
        _suppressHistory = true;
        try
        {
            _value = entry.Text;
            _text.TextContent = entry.Text;
            _lineCache = null;
            _cursor = Math.Clamp(entry.Cursor, 0, entry.Text.Length);
            _selectionAnchor = entry.Anchor is { } anchor
                ? Math.Clamp(anchor, 0, entry.Text.Length)
                : null;
            _scrollCaretIntoView = true;
            TextChanged?.Invoke(entry.Text);
        }
        finally
        {
            _suppressHistory = false;
        }

        ResetBlink();
    }

    private void MoveCaret(int index, bool extend)
    {
        index = Math.Clamp(index, 0, _value.Length);
        if (extend)
            _selectionAnchor ??= _cursor;
        else
            _selectionAnchor = null;

        _cursor = index;
        _scrollCaretIntoView = true;
        _lastEditWasTyping = false;
        ResetBlink();
    }

    private void ResetBlink()
    {
        _blinkTimer = 0;
        _caretVisible = true;
    }

    // ── Layout helpers ─────────────────────────────────────────────────

    private readonly record struct LineInfo(int Start, int End, ComplexTextMetrics.PositionedRichText? Element, IFontMetrics? Metrics);

    private List<LineInfo> GetLines()
    {
        if (_lineCache is not null)
            return _lineCache;

        var elements = _text.LaidOutComplexText?.Elements;
        var lines = new List<LineInfo>();
        var start = 0;
        var line = 0;

        for (var i = 0; i <= _value.Length; i++)
        {
            if (i < _value.Length && _value[i] != '\n')
                continue;

            ComplexTextMetrics.PositionedRichText? element = elements is not null && line < elements.Count
                ? elements[line]
                : null;
            var metrics = element is { } e ? G.GetFontMetrics(e.Font) : null;
            lines.Add(new LineInfo(start, i, element, metrics));
            start = i + 1;
            line++;
        }

        _lineCache = lines;
        return lines;
    }

    private float LineHeight()
    {
        var elements = _text.LaidOutComplexText?.Elements;
        if (elements is { Count: > 0 })
            return G.GetFontMetrics(elements[0].Font).LineHeight;

        var resolved = _textStyles.Resolved;
        return G.GetFontMetrics(new Font(
            resolved.FontFamily!.Value,
            resolved.FontStyle!.Value,
            resolved.FontSize!.Value * G.Scale)).LineHeight;
    }

    private float MeasurePrefix(ComplexTextMetrics.PositionedRichText? element, IFontMetrics? metrics, int length)
    {
        if (element is not { } e || metrics is null || length <= 0)
            return 0f;

        return metrics.MeasureText(e.Text.AsSpan(0, Math.Min(length, e.Text.Length))).X;
    }

    private (int Line, int Column) LineColumnOf(int index)
    {
        var lines = GetLines();
        if (lines.Count == 0)
            return (0, 0);

        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (index >= lines[i].Start || i == 0)
                return (i, Math.Clamp(index - lines[i].Start, 0, lines[i].End - lines[i].Start));
        }

        return (0, 0);
    }

    private int IndexOf(int line, int column)
    {
        var lines = GetLines();
        if (lines.Count == 0)
            return 0;

        var info = lines[Math.Clamp(line, 0, lines.Count - 1)];
        return info.Start + Math.Clamp(column, 0, info.End - info.Start);
    }

    private float CaretX(LineInfo line, int column)
        => (line.Element?.Position.X ?? 0) + MeasurePrefix(line.Element, line.Metrics, column);

    private int IndexForPoint(float x, float y)
    {
        var lines = GetLines();
        if (lines.Count == 0)
            return 0;

        var lineHeight = Math.Max(LineHeight(), 1f);
        // Text draws each line's baseline one LineHeight below its layout top, so the
        // glyph band starts a fraction of a line into the layout cell. Match the click
        // to that band or the descender of a line selects the next one.
        var line = Math.Clamp((int)((y - lineHeight * GlyphTopRatio) / lineHeight), 0, lines.Count - 1);
        var info = lines[line];
        var target = Math.Max(0f, x - (info.Element?.Position.X ?? 0));
        var text = info.Element?.Text ?? "";
        var metrics = info.Metrics;
        if (metrics is null)
            return info.Start;

        var best = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i <= text.Length; i++)
        {
            var width = metrics.MeasureText(text.AsSpan(0, i)).X;
            var distance = Math.Abs(width - target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return info.Start + best;
    }

    private void EnsureCaretVisible()
    {
        var lines = GetLines();
        if (lines.Count == 0)
            return;

        var (line, column) = LineColumnOf(_cursor);
        var info = lines[line];
        var lineHeight = LineHeight();
        var top = (info.Element?.Position.Y ?? 0) + lineHeight * GlyphTopRatio;
        var bottom = top + lineHeight;
        var viewHeight = LayoutContentSize.Y;
        var viewWidth = LayoutContentSize.X;

        if (top < ScrollTop)
            ScrollTop = top;
        else if (bottom > ScrollTop + viewHeight)
            ScrollTop = bottom - viewHeight;

        var x = CaretX(info, column);
        if (x < ScrollLeft)
            ScrollLeft = x;
        else if (x > ScrollLeft + viewWidth)
            ScrollLeft = x - viewWidth + 1;
    }

    // ── Keyboard ───────────────────────────────────────────────────────

    protected override void OnKeyTyped(KeyboardTypingEvent @event)
    {
        var c = @event.KeyChar;
        if (c is '\r' or '\n' || char.IsControl(c))
            return;

        PushHistory(typing: true);
        DeleteSelection();
        var index = Math.Clamp(_cursor, 0, _value.Length);
        SetValueFromUser(_value[..index] + c + _value[index..]);
        _cursor = index + 1;
        _selectionAnchor = null;
    }

    public override void OnKeyPressed(KeyboardEvent @event)
    {
        var shift = @event.Keys.ShiftKey;
        var ctrl = @event.Keys.ControlKey;

        switch (@event.KeyCode)
        {
            case Key.Left when ctrl:
                MoveCaret(PreviousWord(_cursor), shift);
                break;
            case Key.Right when ctrl:
                MoveCaret(NextWord(_cursor), shift);
                break;
            case Key.Left:
                MoveCaret(_cursor - 1, shift);
                break;
            case Key.Right:
                MoveCaret(_cursor + 1, shift);
                break;
            case Key.Up:
                MoveVertical(-1, shift);
                break;
            case Key.Down:
                MoveVertical(1, shift);
                break;
            case Key.PageUp:
                MoveVertical(-Math.Max(1, (int)(LayoutContentSize.Y / Math.Max(LineHeight(), 1f)) - 1), shift);
                break;
            case Key.PageDown:
                MoveVertical(Math.Max(1, (int)(LayoutContentSize.Y / Math.Max(LineHeight(), 1f)) - 1), shift);
                break;
            case Key.Home when ctrl:
                MoveCaret(0, shift);
                break;
            case Key.End when ctrl:
                MoveCaret(_value.Length, shift);
                break;
            case Key.Home:
                MoveCaret(IndexOf(LineColumnOf(_cursor).Line, 0), shift);
                break;
            case Key.End:
            {
                var (line, _) = LineColumnOf(_cursor);
                var lines = GetLines();
                MoveCaret(IndexOf(line, lines.Count > 0 ? lines[line].End - lines[line].Start : 0), shift);
                break;
            }
            case Key.Back:
                if (Selection is not null)
                {
                    PushHistory(typing: true);
                    DeleteSelection();
                    break;
                }
                if (_cursor > 0)
                {
                    PushHistory(typing: true);
                    var index = Math.Min(_cursor, _value.Length);
                    SetValueFromUser(_value[..(index - 1)] + _value[index..]);
                    _cursor = index - 1;
                }
                break;
            case Key.Delete:
                if (Selection is not null)
                {
                    PushHistory(typing: true);
                    DeleteSelection();
                    break;
                }
                if (_cursor < _value.Length)
                {
                    PushHistory(typing: true);
                    SetValueFromUser(_value[.._cursor] + _value[(_cursor + 1)..]);
                }
                break;
            case Key.Enter:
                PushHistory(typing: false);
                DeleteSelection();
                var insertAt = Math.Clamp(_cursor, 0, _value.Length);
                SetValueFromUser(_value[..insertAt] + "\n" + _value[insertAt..]);
                _cursor = insertAt + 1;
                _selectionAnchor = null;
                break;
            case Key.Tab:
                PushHistory(typing: false);
                DeleteSelection();
                var tabAt = Math.Clamp(_cursor, 0, _value.Length);
                var spaces = new string(' ', TabSpaces);
                SetValueFromUser(_value[..tabAt] + spaces + _value[tabAt..]);
                _cursor = tabAt + spaces.Length;
                _selectionAnchor = null;
                break;
            case Key.Z when ctrl && shift:
                if (UseLocalHistory) Redo();
                break;
            case Key.Z when ctrl:
                if (UseLocalHistory) Undo();
                break;
            case Key.Y when ctrl:
                if (UseLocalHistory) Redo();
                break;
            case Key.A when ctrl:
                _selectionAnchor = 0;
                _cursor = _value.Length;
                break;
            case Key.C when ctrl:
                if (Selection is { } copy)
                    SetClipboardText?.Invoke(_value[copy.Start..copy.End]);
                break;
            case Key.X when ctrl:
                if (Selection is { } cut)
                {
                    SetClipboardText?.Invoke(_value[cut.Start..cut.End]);
                    PushHistory(typing: false);
                    DeleteSelection();
                    _lastEditWasTyping = false;
                }
                break;
            case Key.V when ctrl:
                if (GetClipboardText?.Invoke() is { } pasted)
                {
                    pasted = pasted.Replace("\r\n", "\n").Replace('\r', '\n');
                    PushHistory(typing: false);
                    DeleteSelection();
                    var pasteAt = Math.Clamp(_cursor, 0, _value.Length);
                    SetValueFromUser(_value[..pasteAt] + pasted + _value[pasteAt..]);
                    _cursor = pasteAt + pasted.Length;
                    _selectionAnchor = null;
                    _lastEditWasTyping = false;
                }
                break;
        }
    }

    private void MoveVertical(int delta, bool extend)
    {
        var (line, column) = LineColumnOf(_cursor);
        var lines = GetLines();
        var target = Math.Clamp(line + delta, 0, lines.Count - 1);
        MoveCaret(IndexOf(target, column), extend);
    }

    private int PreviousWord(int index)
    {
        while (index > 0 && !char.IsWhiteSpace(_value[index - 1])) index--;
        while (index > 0 && char.IsWhiteSpace(_value[index - 1])) index--;
        return index;
    }

    private int NextWord(int index)
    {
        while (index < _value.Length && !char.IsWhiteSpace(_value[index])) index++;
        while (index < _value.Length && char.IsWhiteSpace(_value[index])) index++;
        return index;
    }

    // ── Mouse ──────────────────────────────────────────────────────────

    protected override void OnMousePressed(MouseEvent @event)
    {
        if (@event.Button != MouseButton.Primary)
            return;

        if (TryBeginScrollbarDrag(@event.Position))
            return;

        var index = IndexForPoint(LocalTextX(@event.Position), LocalTextY(@event.Position));
        var now = Environment.TickCount64;
        _clickCount = now - _lastClickTimeMs <= ClickIntervalMs && Math.Abs(index - _lastClickIndex) <= 1
            ? _clickCount + 1
            : 1;
        _lastClickTimeMs = now;
        _lastClickIndex = index;

        if (_clickCount >= 2)
        {
            SelectWord(index);
        }
        else
        {
            MoveCaret(index, @event.ShiftKey);
        }

        _lastEditWasTyping = false;
        _dragging = true;
        ResetBlink();
    }

    protected override void OnMouseReleased(MouseEvent @event)
    {
        _dragging = false;
        _draggingScrollbar = false;
    }

    [ClientOnly]
    protected override void OnMouseDragged(MouseDragEvent @event)
    {
        // A release outside this node never reaches OnMouseReleased (release dispatch is
        // hit-tested at the release point), so stop on the first button-less drag.
        if (!@event.Buttons.HasFlag(MouseButtons.Primary))
        {
            _dragging = false;
            _draggingScrollbar = false;
            return;
        }

        if (_draggingScrollbar)
        {
            DragScrollbar(@event.Position);
            return;
        }

        if (!_dragging)
            return;

        _clickCount = 1;
        var index = IndexForPoint(LocalTextX(@event.Position), LocalTextY(@event.Position));
        _selectionAnchor ??= _cursor;
        _cursor = index;
        _lastEditWasTyping = false;
        ResetBlink();
    }

    private bool TryBeginScrollbarDrag(Vector2 position)
    {
        var origin = LayoutPaddingPosition;
        var size = LayoutPaddingSize;

        if (ScrollableHeight > 0f && size.Y > 0f)
        {
            var trackLeft = origin.X + size.X - ScrollbarWidth;
            if (position.X >= trackLeft && position.X <= origin.X + size.X
                && position.Y >= origin.Y && position.Y <= origin.Y + size.Y)
            {
                var track = size.Y;
                var content = track + ScrollableHeight;
                var thumb = Math.Max(track * track / content, MinimumThumb);
                var travel = track - thumb;
                var thumbTop = origin.Y + (travel <= 0f ? 0f : ScrollTop / ScrollableHeight * travel);

                _draggingScrollbar = true;
                _horizontalScrollbar = false;
                _dragging = false;
                _scrollCaretIntoView = false;
                _wasFocused = true;

                if (position.Y >= thumbTop && position.Y <= thumbTop + thumb)
                {
                    _scrollbarGrabOffset = position.Y - thumbTop;
                }
                else
                {
                    _scrollbarGrabOffset = thumb / 2f;
                    SetScrollTopFromThumb(position.Y - _scrollbarGrabOffset, origin.Y, travel);
                }

                return true;
            }
        }

        if (ScrollableWidth > 0f && size.X > 0f)
        {
            var trackTop = origin.Y + size.Y - ScrollbarWidth;
            if (position.Y >= trackTop && position.Y <= origin.Y + size.Y
                && position.X >= origin.X && position.X <= origin.X + size.X)
            {
                var track = size.X;
                var content = track + ScrollableWidth;
                var thumb = Math.Max(track * track / content, MinimumThumb);
                var travel = track - thumb;
                var thumbLeft = origin.X + (travel <= 0f ? 0f : ScrollLeft / ScrollableWidth * travel);

                _draggingScrollbar = true;
                _horizontalScrollbar = true;
                _dragging = false;
                _scrollCaretIntoView = false;
                _wasFocused = true;

                if (position.X >= thumbLeft && position.X <= thumbLeft + thumb)
                {
                    _scrollbarGrabOffset = position.X - thumbLeft;
                }
                else
                {
                    _scrollbarGrabOffset = thumb / 2f;
                    SetScrollLeftFromThumb(position.X - _scrollbarGrabOffset, origin.X, travel);
                }

                return true;
            }
        }

        return false;
    }

    private void DragScrollbar(Vector2 position)
    {
        var origin = LayoutPaddingPosition;
        var size = LayoutPaddingSize;

        if (_horizontalScrollbar)
        {
            var track = size.X;
            var content = track + ScrollableWidth;
            var thumb = Math.Max(track * track / content, MinimumThumb);
            SetScrollLeftFromThumb(position.X - _scrollbarGrabOffset, origin.X, track - thumb);
        }
        else
        {
            var track = size.Y;
            var content = track + ScrollableHeight;
            var thumb = Math.Max(track * track / content, MinimumThumb);
            SetScrollTopFromThumb(position.Y - _scrollbarGrabOffset, origin.Y, track - thumb);
        }
    }

    private void SetScrollTopFromThumb(float thumbTop, float origin, float travel)
    {
        if (ScrollableHeight <= 0f || travel <= 0f)
            return;

        var local = Math.Clamp(thumbTop - origin, 0f, travel);
        ScrollTop = local / travel * ScrollableHeight;
    }

    private void SetScrollLeftFromThumb(float thumbLeft, float origin, float travel)
    {
        if (ScrollableWidth <= 0f || travel <= 0f)
            return;

        var local = Math.Clamp(thumbLeft - origin, 0f, travel);
        ScrollLeft = local / travel * ScrollableWidth;
    }

    // The text block starts at this view's content origin and is shifted by the live
    // scroll offset. Reading the Text child's own position would bake in the scroll from
    // the previous paint, so a click right after a wheel event would land on a wrong line.
    private float LocalTextX(Vector2 position) => position.X - (LayoutContentPosition.X - ScrollLeft);

    private float LocalTextY(Vector2 position) => position.Y - (LayoutContentPosition.Y - ScrollTop);

    private void SelectWord(int index)
    {
        var start = Math.Clamp(index, 0, _value.Length);
        var end = start;
        while (start > 0 && !char.IsWhiteSpace(_value[start - 1])) start--;
        while (end < _value.Length && !char.IsWhiteSpace(_value[end])) end++;
        _selectionAnchor = start;
        _cursor = end;
        _scrollCaretIntoView = true;
    }

    // ── Frame ──────────────────────────────────────────────────────────

    protected override void GameTick()
    {
        base.GameTick();
        _blinkTimer += FrameDeltaMs;
        if (_blinkTimer >= BlinkPeriodMs)
        {
            _blinkTimer -= BlinkPeriodMs;
            _caretVisible = !_caretVisible;
        }
    }

    protected override void OnScaleChanged()
    {
        base.OnScaleChanged();
        _lineCache = null;
    }

    protected override bool OnPostLayout()
    {
        var changed = base.OnPostLayout();

        if (IsFocused && !_wasFocused)
            _scrollCaretIntoView = true;
        _wasFocused = IsFocused;

        if (IsFocused && _scrollCaretIntoView)
        {
            _scrollCaretIntoView = false;
            EnsureCaretVisible();
        }

        return changed;
    }

    // ── Caret / selection drawing ──────────────────────────────────────

    /// <summary>
    /// The text itself, drawing the caret and selection after the glyphs. Keeping them in
    /// this node (rather than an absolutely-positioned sibling) matters: an inset-0
    /// overlay extending past the content box counts towards the scroll extent, which
    /// produced permanent scrollbars and clipped the last line.
    /// </summary>
    private sealed class EditableText(TextArea owner) : Text
    {
        [ClientOnly]
        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            if (LaidOutComplexText is not { } text)
            {
                owner.DrawCaretAndSelection(position);
                return;
            }

            var resolved = TextStyles.Resolved;
            var basePosition = position;
            ComplexTextMetrics.AlignBounds(text.Size, (int)size.X, (int)size.Y,
                resolved.HorizontalAlignment!.Value, resolved.VerticalAlignment!.Value,
                ref basePosition.X, ref basePosition.Y);

            var clip = ClipRect;
            var lineHeight = text.Elements.Count > 0
                ? G.GetFontMetrics(text.Elements[0].Font).LineHeight
                : (resolved.FontSize ?? 12f) * G.Scale;

            foreach (var element in text.Elements)
            {
                var elementTop = basePosition.Y + element.Position.Y;
                // Draw only lines intersecting the editor's clip; a long model is
                // thousands of lines and every DrawString is otherwise paid per frame.
                if (clip is { } c && (elementTop + lineHeight * 1.5f < c.Y || elementTop > c.Y + c.Height))
                    continue;

                G.SetFont(element.Font with { Size = element.FontSize ?? resolved.FontSize!.Value });

                if ((element.Background ?? Styles.BackgroundColor) is { } background)
                {
                    G.SetColor(background);
                    G.FillRect((int)basePosition.X, (int)(basePosition.Y + element.Position.Y),
                        (int)element.Size.X, (int)element.Size.Y);
                }

                float yOff = 0;
                if (resolved.VerticalAlignment == TextVerticalAlignment.Center)
                    yOff = G.GetFontMetrics().LineHeight / 2.0f;
                else if (resolved.VerticalAlignment == TextVerticalAlignment.Top)
                    yOff = G.GetFontMetrics().LineHeight;

                var x = (int)(basePosition.X + element.Position.X);
                var y = (int)(basePosition.Y + element.Position.Y + yOff);

                G.SetColor(element.Foreground ?? resolved.ForegroundColor!.Value);
                if ((element.Stroke ?? resolved.StrokeColor) is { } stroke)
                    G.DrawStringOutlined(element.Text, x, y, stroke, 1f);
                else
                    G.DrawString(element.Text, x, y);
            }

            owner.DrawCaretAndSelection(position);
        }
    }

    [ClientOnly]
    private void DrawCaretAndSelection(Vector2 position)
    {
        if (!IsFocused && Selection is null)
            return;

        var lines = GetLines();
        if (lines.Count == 0)
            return;

        var lineHeight = LineHeight();

        if (Selection is { } selection)
        {
            G.SetColor(new Color(100, 180, 255, 110));
            var startLine = LineColumnOf(selection.Start).Line;
            var endLine = LineColumnOf(selection.End).Line;

            for (var i = startLine; i <= endLine && i < lines.Count; i++)
            {
                var info = lines[i];
                var from = Math.Max(selection.Start, info.Start) - info.Start;
                var to = Math.Min(selection.End, info.End) - info.Start;
                if (to <= from && i >= endLine)
                    continue;

                var x1 = CaretX(info, from);
                var x2 = CaretX(info, Math.Max(to, from + (to <= from ? 1 : 0)));
                var y = (info.Element?.Position.Y ?? i * lineHeight) + lineHeight * GlyphTopRatio;
                if (ClipRect is { } clip && (position.Y + y + lineHeight < clip.Y || position.Y + y > clip.Y + clip.Height))
                    continue;
                G.FillRect(
                    (int)(position.X + x1),
                    (int)(position.Y + y),
                    Math.Max(1, (int)(x2 - x1)),
                    (int)Math.Max(1f, lineHeight));
            }
        }

        if (!IsFocused || !_caretVisible)
            return;

        var (line, column) = LineColumnOf(_cursor);
        if (line >= lines.Count)
            return;

        var caretLine = lines[line];
        var caretX = CaretX(caretLine, column);
        var caretY = (caretLine.Element?.Position.Y ?? line * lineHeight) + lineHeight * GlyphTopRatio;
        G.SetColor(new Color(235, 244, 255));
        G.FillRect(
            (int)(position.X + caretX),
            (int)(position.Y + caretY),
            1,
            Math.Max(1, (int)lineHeight));
    }
}
