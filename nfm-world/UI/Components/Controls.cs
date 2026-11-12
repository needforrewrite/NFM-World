using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor.Sx;
using MouseEvent = NFMWorld.ClayDom.Events.MouseEvent;
using MouseDragEvent = NFMWorld.ClayDom.Events.MouseDragEvent;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

/// <summary>
/// The settings controls. Ports of <c>data/uis/components/{checkbox,dropdown,slider,
/// toggleswitch,radio,textfield}.luau</c>.
/// </summary>
public static class Controls
{
    private sealed class DropdownView(Action dismiss) : View
    {
        public void Dismiss() => dismiss();
    }

    private sealed class DropdownListView : View
    {
        private const int RowHeight = 32;
        private readonly IReadOnlyList<string> _options;
        private readonly Func<int> _selected;
        private readonly DropdownOptionView[] _rows;
        private int _lastSelected = -1;

        public DropdownListView(IReadOnlyList<string> options, Func<int> selected, Action<int> onSelect, float maxHeight)
        {
            _options = options;
            _selected = selected;
            Styles = Sx.Styles(flexDirection: FlexDirection.Column, maxHeight: maxHeight, overflow: Overflow.Scroll);
            var content = new View
            {
                Styles = Sx.Styles(position: Position.Relative, height: options.Count * RowHeight, flexShrink: 0)
            };
            // Keep the full scroll extent while recycling only the visible rows and a buffer.
            _rows = new DropdownOptionView[Math.Min(options.Count, (int)Math.Ceiling(maxHeight / RowHeight) + 2)];
            for (var i = 0; i < _rows.Length; i++)
            {
                var row = new DropdownOptionView(onSelect);
                row.SetOption(i, options[i], i == selected());
                _rows[i] = row;
                content.AddChild(row);
            }

            AddChild(content);
        }

        protected override bool OnPostLayout()
        {
            var changed = base.OnPostLayout();
            var selected = _selected();
            if (_lastSelected != selected)
            {
                var top = selected * RowHeight * Math.Max(G.Scale, 0.01f);
                var bottom = top + RowHeight * Math.Max(G.Scale, 0.01f);
                var previous = ScrollTop;
                if (top < ScrollTop) ScrollTop = top;
                else if (bottom > ScrollTop + LayoutPaddingSize.Y) ScrollTop = bottom - LayoutPaddingSize.Y;
                changed |= previous != ScrollTop;
                if (top >= ScrollTop - 0.5f && bottom <= ScrollTop + LayoutPaddingSize.Y + 0.5f)
                    _lastSelected = selected;
            }

            var first = Math.Clamp(
                (int)(ScrollTop / (RowHeight * Math.Max(G.Scale, 0.01f))) - 1,
                0,
                Math.Max(0, _options.Count - _rows.Length)
            );
            for (var i = 0; i < _rows.Length; i++)
            {
                var index = first + i;
                changed |= _rows[i].SetOption(index, _options[index], index == selected);
            }

            return changed;
        }

        private sealed class DropdownOptionView : View
        {
            private readonly Text _text = new();
            private int _index = -1;
            private bool _selected;
            private bool _hovered;

            public DropdownOptionView(Action<int> onSelect)
            {
                Styles = Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    right: 0,
                    height: RowHeight,
                    overflow: Overflow.Hidden,
                    paddingTop: 7,
                    paddingBottom: 7,
                    paddingLeft: 12,
                    paddingRight: 12
                );
                MousePressed = e =>
                {
                    e.StopPropagation();
                    onSelect(_index);
                };
                MouseEntered = _ =>
                {
                    _hovered = true;
                    UpdateColors();
                };
                MouseLeft = _ =>
                {
                    _hovered = false;
                    UpdateColors();
                };
                AddChild(_text);
            }

            public bool SetOption(int index, string option, bool selected)
            {
                if (_index == index && _selected == selected) return false;
                if (_index != index)
                {
                    _hovered = false;
                    var style = Styles;
                    style.Top = index * RowHeight;
                    Styles = style;
                    _text.TextContent = option;
                }

                _index = index;
                _selected = selected;
                UpdateColors();
                return true;
            }

            private void UpdateColors()
            {
                var style = Styles;
                style.BackgroundColor = _selected ? Theme.AccentSoft
                    : _hovered ? new Color(255, 255, 255, 20) : Theme.Transparent;
                Styles = style;
                _text.TextStyles = Sx.TextStyles(
                    fontSize: 13,
                    foregroundColor: _selected ? Theme.Accent
                    : _hovered ? new Color(255, 255, 255) : new Color(255, 255, 255, 204)
                );
            }
        }
    }

    public static void DismissDropdowns(View root, Vector2 position)
    {
        var hit = FocusManager.HitTestChain(root, position);

        void Visit(Node node)
        {
            if (node is DropdownView dropdown)
            {
                var inside = hit.Any(target =>
                    {
                        for (Node? current = target; current != null; current = current.VisualParent)
                            if (ReferenceEquals(current, dropdown))
                                return true;
                        return false;
                    }
                );
                if (!inside) dropdown.Dismiss();
            }

            foreach (var child in node.VisualChildren)
                Visit(child);
        }

        Visit(root);
    }

    // ── Checkbox ──────────────────────────────────────────────────────

    public static Node Checkbox(Func<bool> checkedValue, Action onToggle, Func<string?>? label = null)
    {
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);

        var node = Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 8,
                gapRow: 8
            ),
            onmousedown: _ => onToggle(),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        width: 16,
                        height: 16,
                        flexShrink: 0,
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: focused.Value
                            ? new Color(205, 227, 255)
                            : checkedValue()
                                ? Theme.Accent
                                : hovered.Value
                                    ? new Color(255, 255, 255, 128)
                                    : new Color(255, 255, 255, 64),
                        backgroundColor: checkedValue()
                            ? Theme.Accent
                            : hovered.Value
                                ? new Color(255, 255, 255, 41)
                                : new Color(255, 255, 255, 20),
                        borderRadius: 3,
                        alignItems: Align.Center,
                        justifyContent: Justify.Center
                    ),
                    children: Sx.Show(
                        when: checkedValue,
                        then: () => Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: new Color(20, 20, 40)
                            ),
                            text: () => "✓"
                        )
                    )
                ),
                Sx.Show(
                    when: () => label is not null,
                    then: () => Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 13,
                            foregroundColor: checkedValue() ? new Color(255, 255, 255) : new Color(255, 255, 255, 178)
                        ),
                        text: () => label?.Invoke()
                    )
                )
            ]
        );
        return KeyboardNavigation.Bind(
            node,
            key =>
            {
                if (key is not (Key.Enter or Key.Space or Key.Left or Key.Right)) return false;
                if (key == Key.Left && !checkedValue() || key == Key.Right && checkedValue()) return true;
                onToggle();
                return true;
            },
            value => focused.Value = value
        );
    }

    // ── Slider ────────────────────────────────────────────────────────

    /// <summary>
    /// A horizontal 0..1 track. The drag handler maps the pointer's X across the track's
    /// laid-out width, which is why it reads the node's own layout rather than its own.
    /// </summary>
    public static Node Slider(
        Func<double> value,
        Action<double> onValue,
        double min,
        double max,
        double step,
        bool compact = false
    )
    {
        var thumbWidth = compact ? 10f : 14f;
        var thumbHeight = compact ? 10f : 14f;
        var trackHeight = compact ? 3f : 4f;
        var rowHeight = compact ? 16f : 20f;
        View? row = null;
        var focused = new Signal<bool>(false);

        double Progress() => max - min <= 0 ? 0 : Math.Clamp((value() - min) / (max - min), 0, 1);

        // Measured on every read rather than cached: the first layout has no width yet, and
        // the width changes with the window.
        float TrackWidth() => row is null ? 0f : Math.Max(0f, row.LayoutPaddingSize.X - thumbWidth);

        void SetFromPointer(MouseEvent e)
        {
            if (row is null)
                return;

            // Always re-read: the node is laid out before any press can reach it, but its
            // width changes with the window.
            var width = TrackWidth();
            if (width <= 0)
                return;

            var local = (e.Position.X - row.LayoutPaddingPosition.X - (thumbWidth / 2)) / width;
            var raw = min + (Math.Clamp(local, 0, 1) * (max - min));
            var stepped = Math.Round(raw / step) * step;
            onValue(Math.Clamp(stepped, min, max));
        }

        void SetFromDrag(MouseDragEvent e)
            => SetFromPointer(
                new MouseEvent(
                    e.Position,
                    NFMWorld.DriverInterface.MouseButton.Primary,
                    e.Buttons,
                    e.CtrlKey,
                    e.MetaKey,
                    e.ShiftKey,
                    System.Numerics.Vector2.Zero
                )
            );

        // The track owns both the fill and the handle, and is inset by half a thumb on each
        // side. Percentages on absolutely positioned children resolve against the parent's
        // padding box, so putting them here makes 0%..100% mean exactly the usable range -
        // the same range the pointer math below maps onto.
        var fill = Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                width: ((float)(Progress() * 100)).Percent(),
                height: 100.Percent(),
                backgroundColor: Theme.Accent
            )
        );

        var thumb = Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: ((float)(Progress() * 100)).Percent(),
                marginLeft: -thumbWidth / 2,
                top: (trackHeight - thumbHeight) / 2f,
                width: thumbWidth,
                height: thumbHeight,
                backgroundColor: new Color(255, 255, 255),
                borderTop: 2,
                borderBottom: 2,
                borderLeft: 2,
                borderRight: 2,
                borderColor: focused.Value ? new Color(255, 255, 255) : Theme.AccentBorder,
                borderRadius: thumbWidth / 2,
                zIndex: 1
            )
        );

        var track = Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                top: (rowHeight - trackHeight) / 2f,
                height: trackHeight,
                left: thumbWidth / 2,
                right: thumbWidth / 2,
                backgroundColor: new Color(255, 255, 255, 38),
                borderRadius: trackHeight / 2f,
                overflow: Overflow.Hidden
            ),
            children: new object?[] { fill, thumb }
        );

        row = Sx.View(
            style: () => Sx.Styles(
                flexGrow: 1,
                height: rowHeight,
                minWidth: compact ? 50 : 160,
                position: Position.Relative,
                marginLeft: compact ? 2 : 8,
                marginRight: compact ? 2 : 8,
                backgroundColor: focused.Value ? Theme.AccentSoft : Theme.Transparent,
                borderRadius: 4
            ),
            onmousedown: SetFromPointer,
            onmousedrag: SetFromDrag,
            children: new object?[] { track }
        );

        // The row is positioned, so it is the containing block for the track and thumb.
        return KeyboardNavigation.Bind(
            row,
            key =>
            {
                if (key is Key.Enter or Key.Space) return true;
                if (key is not (Key.Left or Key.Right or Key.Home or Key.End)) return false;
                var next = key == Key.Home ? min
                    : key == Key.End ? max
                    : Math.Round((value() + (key == Key.Left ? -step : step)) / step) * step;
                onValue(Math.Clamp(next, min, max));
                return true;
            },
            value => focused.Value = value
        );
    }

    // ── Dropdown ──────────────────────────────────────────────────────

    /// <summary>
    /// A value list that opens within its ancestors' visible scroll region.
    /// </summary>
    public static Node Dropdown(
        Func<IReadOnlyList<string>> options,
        Func<int> value,
        Action<int> onSelect,
        string unselectedLabel = "",
        float width = 240,
        bool compact = false
    )
    {
        var open = new Signal<bool>(false);
        var opensAbove = new Signal<bool>(false);
        var popupHeight = new Signal<float>(200);
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);
        var cursor = new Signal<int>(0);
        IReadOnlyList<string> openOptions = [];
        View? root = null;

        Node? popup = null;

        void Toggle()
        {
            if (open.Value)
            {
                open.Value = false;
                return;
            }

            if (root == null) return;

            openOptions = options().ToArray();
            cursor.Value = Math.Clamp(value(), 0, Math.Max(0, openOptions.Count - 1));

            var clip = root.ScissorRect;
            var position = root.LayoutBorderPosition;
            var size = root.LayoutBorderSize;
            var scale = Math.Max(G.Scale, 0.01f);
            var above = Math.Max(0, (position.Y - clip.Y) / scale - 4);
            var below = Math.Max(0, (clip.Y + clip.Height - position.Y - size.Y) / scale - 4);
            var desired = Math.Min(200, openOptions.Count * 32 + 2);
            opensAbove.Value = below < desired && above > below;
            popupHeight.Value = Math.Min(200, opensAbove.Value ? above : below);
            open.Value = true;
        }

        popup = Sx.Show(
            when: () => open.Value,
            then: () => Sx.View(
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    top: opensAbove.Value ? MeasurementMarginPosition.Undefined : 100.Percent(),
                    bottom: opensAbove.Value ? 100.Percent() : MeasurementMarginPosition.Undefined,
                    left: 0,
                    width: 100.Percent(),
                    maxHeight: popupHeight.Value,
                    flexDirection: FlexDirection.Column,
                    backgroundColor: new Color(20, 24, 32, 250),
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: new Color(255, 255, 255, 38),
                    borderRadius: 6,
                    overflow: Overflow.Hidden,
                    zIndex: 1000
                ),
                // The popup can extend outside its modal card; a click here belongs to
                // the dropdown, not to the backdrop underneath it.
                onmousedown: e => e.StopPropagation(),
                children: new DropdownListView(
                    openOptions,
                    () => cursor.Value,
                    index =>
                    {
                        onSelect(index);
                        open.Value = false;
                    },
                    Math.Max(0, popupHeight.Value - 2)
                )
            )
        );

        root = new DropdownView(() => open.Value = false)
        {
            Styles = Sx.Styles(
                position: Position.Relative,
                flexDirection: FlexDirection.Column,
                width: width,
                maxWidth: 100.Percent()
            )
        };
        root.AddChild(
            Sx.View(
                style: () => Sx.Styles(
                    width: 100.Percent(),
                    paddingTop: compact ? 2 : 8,
                    paddingBottom: compact ? 2 : 8,
                    paddingLeft: compact ? 6 : 12,
                    paddingRight: compact ? 6 : 12,
                    backgroundColor: new Color(22, 42, 80, 145),
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: open.Value || focused.Value
                        ? new Color(128, 170, 255)
                        : hovered.Value
                            ? new Color(205, 227, 255)
                            : new Color(128, 170, 255, 100),
                    borderRadius: 6,
                    flexDirection: FlexDirection.Row,
                    alignItems: Align.Center,
                    justifyContent: Justify.SpaceBetween,
                    gapColumn: 8,
                    gapRow: 8
                ),
                onmousedown: _ => Toggle(),
                onmouseenter: _ => hovered.Value = true,
                onmouseleave: _ => hovered.Value = false,
                children:
                [
                    Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: compact ? 10 : 13,
                            foregroundColor: open.Value ? new Color(255, 255, 255) : new Color(255, 255, 255, 204)
                        ),
                        text: () => SelectedLabel(options(), value(), unselectedLabel)
                    ),
                    Sx.Text(
                        style: () => Sx.TextStyles(fontSize: compact ? 9 : 12, foregroundColor: new Color(255, 255, 255, 128)),
                        text: () => "▾"
                    )
                ]
            )
        );
        root.AddChild(popup);

        return KeyboardNavigation.Bind(
            root,
            key =>
            {
                if (key == Key.Escape && open.Value)
                {
                    open.Value = false;
                    return true;
                }

                if (key is Key.Enter or Key.Space)
                {
                    if (!open.Value) Toggle();
                    else
                    {
                        if (cursor.Value >= 0 && cursor.Value < openOptions.Count) onSelect(cursor.Value);
                        open.Value = false;
                    }

                    return true;
                }

                if (open.Value && key is (Key.Up or Key.Down or Key.Left or Key.Right or Key.Home or Key.End))
                {
                    cursor.Value = key == Key.Home ? 0
                        : key == Key.End ? Math.Max(0, openOptions.Count - 1)
                        : Math.Clamp(cursor.Value + (key is Key.Up or Key.Left ? -1 : 1), 0, Math.Max(0, openOptions.Count - 1));
                    return true;
                }

                if (!open.Value && key is (Key.Left or Key.Right))
                {
                    var count = options().Count;
                    if (count > 0) onSelect(Math.Clamp(value() + (key == Key.Left ? -1 : 1), 0, count - 1));
                    return true;
                }

                return false;
            },
            value =>
            {
                focused.Value = value;
                if (!value) open.Value = false;
            }
        );
    }

    private static string SelectedLabel(IReadOnlyList<string> options, int value, string unselectedLabel)
        => value >= 0 && value < options.Count ? options[value] : unselectedLabel;

    // ── Text field ────────────────────────────────────────────────────

    public static Node TextField(
        Func<string> value,
        Action<string> onChange,
        Func<string?>? placeholder = null
    )
    {
        var focused = new Signal<bool>(false);
        var node = Sx.TextInput(
            style: () => Sx.Styles(
                paddingTop: 6,
                paddingBottom: 6,
                paddingLeft: 10,
                paddingRight: 10,
                backgroundColor: Theme.Surface,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: focused.Value ? Theme.AccentBorder : Theme.SurfaceBorder,
                borderRadius: 6,
                minWidth: 180
            ),
            text: value,
            onchange: onChange,
            placeholder: placeholder
        );
        return KeyboardNavigation.Bind(node, _ => false, value => focused.Value = value);
    }
}
