using System.Text;
using Hexa.NET.ImGui;
using NFMWorld.DriverInterface;
using NFMWorld.Platform.SDL3;
using NFMWorldLibrary;

namespace NFMWorld;

internal sealed class DevConsoleOutput
{
    private readonly record struct Line(int Start, int End, Vector4 Color);

    private (string message, string level)[] _logs = [];
    private int[] _logStarts = [];
    private byte[] _text = [];
    private readonly List<Line> _lines = [];
    private float _wrapWidth;
    private float _fontSize;
    private int _anchor;
    private int _caret;
    private bool _hasFocus;
    private bool _dragging;

    public void Blur()
    {
        _hasFocus = false;
        _dragging = false;
        _anchor = _caret;
    }

    public unsafe void Render()
    {
        var available = ImGui.GetContentRegionAvail();
        var width = Math.Max(1f, available.X);
        var fontSize = ImGui.GetFontSize();
        if (RefreshLogs() || width != _wrapWidth || fontSize != _fontSize)
        {
            _wrapWidth = width;
            _fontSize = fontSize;
            WrapLines();
        }

        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##OutputText", new Vector2(width, Math.Max(available.Y, _lines.Count * lineHeight)));
        var drawList = ImGui.GetWindowDrawList();
        var clipTop = ImGui.GetClipRectMin(drawList).Y;
        var clipBottom = ImGui.GetClipRectMax(drawList).Y;
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsItemClicked())
        {
            _hasFocus = true;
            _dragging = true;
            _anchor = _caret = HitTest(mouse - origin, lineHeight);
        }

        if (_dragging)
        {
            _dragging = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            if (_dragging)
            {
                var selectionPoint = new Vector2(mouse.X, Math.Clamp(mouse.Y, clipTop, clipBottom));
                _caret = HitTest(selectionPoint - origin, lineHeight);
                if (mouse.Y < clipTop)
                    ImGui.SetScrollY(ImGui.GetScrollY() - lineHeight);
                else if (mouse.Y > clipBottom)
                    ImGui.SetScrollY(ImGui.GetScrollY() + lineHeight);
            }
        }

        if (ImGui.IsItemHovered() || _dragging)
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);

        if (_hasFocus && ImGui.IsWindowFocused())
        {
            var keys = SdlWindow.GetKeyboardState();
            var shortcut = OperatingSystem.IsMacOS()
                ? keys[Key.LWin] || keys[Key.RWin]
                : keys[Key.LControlKey] || keys[Key.RControlKey];
            if (shortcut && ImGui.IsKeyPressed(ImGuiKey.A, false))
            {
                _anchor = 0;
                _caret = _text.Length;
            }

            if (shortcut && _anchor != _caret && ImGui.IsKeyPressed(ImGuiKey.C, false))
            {
                var selection = _text.AsSpan(Math.Min(_anchor, _caret), Math.Abs(_anchor - _caret));
                SdlWindow.SetClipboardText(Encoding.UTF8.GetString(selection));
            }
        }

        var selectionStart = Math.Min(_anchor, _caret);
        var selectionEnd = Math.Max(_anchor, _caret);
        var first = Math.Max(0, (int)((clipTop - origin.Y) / lineHeight));
        var last = Math.Min(_lines.Count, (int)MathF.Ceiling((clipBottom - origin.Y) / lineHeight));
        var highlight = ImGui.GetColorU32(ImGuiCol.TextSelectedBg);
        fixed (byte* text = _text)
        {
            for (var i = first; i < last; i++)
            {
                var line = _lines[i];
                var position = origin + new Vector2(0, i * lineHeight);
                var start = Math.Max(selectionStart, line.Start);
                var end = Math.Min(selectionEnd, line.End);
                if (start < end)
                {
                    drawList.AddRectFilled(
                        position + new Vector2(Measure(text + line.Start, text + start), 0),
                        position + new Vector2(Measure(text + line.Start, text + end), lineHeight),
                        highlight
                    );
                }

                if (line.Start < line.End)
                    drawList.AddText(position, ImGui.GetColorU32(line.Color), text + line.Start, text + line.End);
            }
        }

        if (_anchor == _caret && !_dragging && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1f);
    }

    private bool RefreshLogs()
    {
        var logs = NfmwLoggerProvider.GetLogs();
        if (logs.SequenceEqual(_logs)) return false;

        var next = logs.ToArray();
        var overlap = Math.Min(_logs.Length, next.Length);
        while (overlap > 0 && !_logs.AsSpan(_logs.Length - overlap).SequenceEqual(next.AsSpan(0, overlap)))
            overlap--;

        var removed = overlap == 0 ? _text.Length : _logStarts[_logs.Length - overlap];
        _anchor = Math.Max(0, _anchor - removed);
        _caret = Math.Max(0, _caret - removed);
        _logs = next;

        _logStarts = new int[next.Length + 1];
        for (var i = 0; i < next.Length; i++)
            _logStarts[i + 1] = _logStarts[i] + Encoding.UTF8.GetByteCount(next[i].message) + 1;

        _text = new byte[Math.Max(0, _logStarts[^1] - 1)];
        for (var i = 0; i < next.Length; i++)
        {
            var length = Encoding.UTF8.GetBytes(next[i].message, _text.AsSpan(_logStarts[i]));
            var end = _logStarts[i] + length;
            if (end < _text.Length) _text[end] = (byte)'\n';
        }

        return true;
    }

    private unsafe void WrapLines()
    {
        _lines.Clear();
        var font = ImGui.GetFont();
        fixed (byte* text = _text)
        {
            for (var i = 0; i < _logs.Length; i++)
            {
                var start = _logStarts[i];
                var logEnd = _logStarts[i + 1] - 1;
                var color = _logs[i].level switch
                {
                    "warning" => new Vector4(1f, 1f, 0f, 1f),
                    "error" => new Vector4(1f, 0.4f, 0.4f, 1f),
                    "info" => new Vector4(0.34f, 0.8f, 1f, 1f),
                    "debug" => new Vector4(0.612f, 1f, 0f, 1f),
                    _ => Vector4.One
                };

                while (start <= logEnd)
                {
                    var newline = Array.IndexOf(_text, (byte)'\n', start, logEnd - start);
                    var end = newline < 0 ? logEnd : newline;
                    if (end > start && _text[end - 1] == (byte)'\r') end--;
                    do
                    {
                        var wrap = start == end ? end
                            : (int)(font.CalcWordWrapPosition(_fontSize, text + start, text + end, _wrapWidth) - text);
                        wrap = Math.Clamp(wrap, Math.Min(start + 1, end), end);
                        while (wrap < end && (_text[wrap] & 0xC0) == 0x80) wrap++;
                        _lines.Add(new Line(start, wrap, color));
                        start = wrap;
                        while (start < end && _text[start] is (byte)' ' or (byte)'\t') start++;
                    } while (start < end);

                    start = newline < 0 ? logEnd + 1 : newline + 1;
                }
            }
        }
    }

    private unsafe int HitTest(Vector2 point, float lineHeight)
    {
        if (_lines.Count == 0 || point.Y < 0) return 0;
        var index = (int)(point.Y / lineHeight);
        if (index >= _lines.Count) return _text.Length;

        var line = _lines[index];
        fixed (byte* text = _text)
        {
            var position = line.Start;
            var left = 0f;
            while (position < line.End)
            {
                var next = position + 1;
                while (next < line.End && (_text[next] & 0xC0) == 0x80) next++;
                var right = Measure(text + line.Start, text + next);
                if (point.X < (left + right) / 2f) return position;
                position = next;
                left = right;
            }

            return line.End;
        }
    }

    private static unsafe float Measure(byte* start, byte* end)
        => start == end ? 0f : ImGui.CalcTextSize(start, end).X;
}
