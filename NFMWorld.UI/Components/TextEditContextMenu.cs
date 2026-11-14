using System.Numerics;
using Maxine.Extensions.Mathematics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using SxApi = NFMWorld.Reactor.Sx.Sx;

namespace NFMWorld.Reactor;

/// <summary>
/// The right-click menu shared by <see cref="TextInput"/> and <see cref="TextArea"/>:
/// Cut / Copy / Paste / Select All.
/// </summary>
/// <remarks>
/// Built as an absolutely positioned child of its owning control, so it follows that control
/// through any move. It is deliberately ignorant of the owner's editing model - the four
/// callbacks and the two capability probes are the whole contract, which is what lets a
/// single-line field and a multiline editor share one implementation.
///
/// It stays out of layout while closed: <see cref="Component.IsDisplayed"/> is false for
/// <see cref="Display.None"/>, and both the paint pass and <c>UpdateScrollExtent</c> skip
/// undisplayed children, so a closed menu contributes no box and no scroll extent.
/// </remarks>
internal sealed class TextEditContextMenu : View
{
    internal const float Width = 150f;
    internal const float RowHeight = 24f;
    internal const float Padding = 4f;
    internal const float Border = 1f;

    /// <summary>Four rows, with the padding and both borders, in unscaled units.</summary>
    internal const float Height = 4 * RowHeight + 2 * Padding + 2 * Border;

    private readonly Func<bool> _hasSelection;
    private readonly Func<bool> _hasText;
    private readonly Func<bool> _canPaste;
    private readonly MenuRow _cut;
    private readonly MenuRow _copy;
    private readonly MenuRow _paste;
    private readonly MenuRow _selectAll;

    public bool IsOpen { get; private set; }

    /// <param name="hasSelection">Whether Cut and Copy are offered.</param>
    /// <param name="hasText">Whether Select All is offered.</param>
    /// <param name="canPaste">Whether a clipboard is wired.</param>
    public TextEditContextMenu(
        Func<bool> hasSelection,
        Func<bool> hasText,
        Func<bool> canPaste,
        Action cut,
        Action copy,
        Action paste,
        Action selectAll
    )
    {
        _hasSelection = hasSelection;
        _hasText = hasText;
        _canPaste = canPaste;

        Styles = SxApi.Styles(
            position: Position.Absolute,
            display: Display.None,
            flexDirection: FlexDirection.Column,
            width: Width,
            paddingTop: Padding,
            paddingBottom: Padding,
            backgroundColor: new Color(18, 24, 38, 250),
            borderTop: Border,
            borderBottom: Border,
            borderLeft: Border,
            borderRight: Border,
            borderColor: new Color(255, 255, 255, 40),
            borderRadius: 6,
            zIndex: 1000
        );
        IsFocusable = false;

        // Anchored to the control's viewport, not its content: without this the menu would
        // slide with a scrolling owner (TextArea) and add its height to that owner's scroll
        // extent.
        IgnoresScrollOffset = true;

        // The menu is a child of the control, so a press on it would otherwise reach the
        // control's own handler and re-open (or re-place) the menu under the pointer.
        MousePressed = e => e.StopPropagation();

        _cut = AddRow("Cut", cut);
        _copy = AddRow("Copy", copy);
        _paste = AddRow("Paste", paste);
        _selectAll = AddRow("Select All", selectAll);
    }

    /// <param name="left">Unscaled offset from the owner's containing-block origin.</param>
    /// <param name="top">Unscaled offset from the owner's containing-block origin.</param>
    public void Show(float left, float top)
    {
        Refresh();
        Styles = Styles with
        {
            Display = Display.Flex,
            Left = MeasurementMarginPosition.Point(left),
            Top = MeasurementMarginPosition.Point(top),
        };
        IsOpen = true;
    }

    public void Hide()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Styles = Styles with { Display = Display.None };
        _cut.SetHover(false);
        _copy.SetHover(false);
        _paste.SetHover(false);
        _selectAll.SetHover(false);
    }

    /// <summary>Recomputes the enabled state; cheap, so an open menu can track edits.</summary>
    public void Refresh()
    {
        var hasSelection = _hasSelection();
        _cut.SetEnabled(hasSelection);
        _copy.SetEnabled(hasSelection);
        _paste.SetEnabled(_canPaste());
        _selectAll.SetEnabled(_hasText());
    }

    /// <summary>
    /// Opens the menu at a screen-space press position, kept fully inside the owner's visible
    /// region so a field near the bottom of a dialog does not open a menu that runs off it.
    /// </summary>
    /// <param name="owner">The control the menu is a child of.</param>
    /// <param name="screenPosition">The press position, in window pixels.</param>
    /// <remarks>
    /// Absolute children are laid out relative to the owner's padding box, and every
    /// <see cref="Styles"/> length is multiplied by <see cref="G.Scale"/>, so both the press
    /// position and the visible region are converted into that unscaled space. The owner must
    /// have <see cref="Component.IgnoresScrollOffset"/> set, or a scrolling owner would offset
    /// the menu by its scroll amount when painting it.
    /// </remarks>
    public void ShowAt(Component owner, Vector2 screenPosition)
    {
        var scale = Math.Max(G.Scale, 0.01f);
        var origin = owner.LayoutPaddingPosition;
        var clip = owner.ScissorRect;

        var visibleLeft = (clip.X - origin.X) / scale;
        var visibleTop = (clip.Y - origin.Y) / scale;
        var visibleWidth = clip.Width / scale;
        var visibleHeight = clip.Height / scale;

        var maxLeft = visibleLeft + visibleWidth - Width;
        var maxTop = visibleTop + visibleHeight - Height;

        Show(
            Math.Clamp((screenPosition.X - origin.X) / scale, visibleLeft, Math.Max(visibleLeft, maxLeft)),
            Math.Clamp((screenPosition.Y - origin.Y) / scale, visibleTop, Math.Max(visibleTop, maxTop))
        );
    }

    private MenuRow AddRow(string label, Action action)
    {
        var row = new MenuRow(label, () =>
            {
                action();
                Hide();
            }
        );
        AddChild(row);
        return row;
    }

    private sealed class MenuRow : View
    {
        private readonly Text _label;
        private readonly Action _action;
        private bool _enabled = true;
        private bool _hovered;

        public MenuRow(string label, Action action)
        {
            _action = action;
            Styles = SxApi.Styles(
                height: RowHeight,
                paddingLeft: 10,
                paddingRight: 10,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center
            );
            IsFocusable = false;

            _label = new Text
            {
                TextContent = label,
                TextStyles = SxApi.TextStyles(
                    fontSize: 12,
                    foregroundColor: new Color(255, 255, 255, 220)
                ),
            };
            AddChild(_label);

            MouseEntered = _ => SetHover(true);
            MouseLeft = _ => SetHover(false);
            MousePressed = e =>
            {
                // The menu owns the press: a disabled row still swallows it, so a click on
                // the menu never falls through to the field underneath.
                e.StopPropagation();
                if (e.Button == MouseButton.Primary && _enabled)
                    _action();
            };
        }

        public void SetEnabled(bool enabled)
        {
            if (_enabled == enabled)
                return;

            _enabled = enabled;
            if (!enabled)
                _hovered = false;

            _label.TextStyles = SxApi.TextStyles(
                fontSize: 12,
                foregroundColor: enabled
                    ? new Color(255, 255, 255, 220)
                    : new Color(255, 255, 255, 90)
            );
            Styles = Styles with
            {
                BackgroundColor = _hovered ? new Color(255, 255, 255, 24) : null,
            };
        }

        public void SetHover(bool hovered)
        {
            if (!_enabled || _hovered == hovered)
                return;

            _hovered = hovered;
            Styles = Styles with
            {
                BackgroundColor = hovered ? new Color(255, 255, 255, 24) : null,
            };
        }
    }
}
