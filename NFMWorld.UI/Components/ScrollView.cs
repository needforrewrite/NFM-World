using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;

namespace NFMWorld.Reactor;

public class ScrollView : View
{
    private bool _dragging;
    private bool _horizontal;
    private float _grabOffset;
    private Vector2 _dragStart;

    protected override void OnMousePressed(MouseEvent e)
    {
        base.OnMousePressed(e);
        _dragging = false;

        if (e.Button != MouseButton.Primary || Styles.Overflow != Overflow.Scroll)

            return;
        var origin = LayoutPaddingPosition;
        var size = LayoutPaddingSize;

        if (ScrollableHeight > 0
            && e.Position.X >= origin.X + size.X - 6
            && e.Position.X <= origin.X + size.X
            && e.Position.Y >= origin.Y
            && e.Position.Y <= origin.Y + size.Y)
            _horizontal = false;
        else if (ScrollableWidth > 0
                 && e.Position.Y >= origin.Y + size.Y - 6
                 && e.Position.Y <= origin.Y + size.Y
                 && e.Position.X >= origin.X
                 && e.Position.X <= origin.X + size.X)
            _horizontal = true;
        else

            return;

        var (start, track, extent, offset) = ScrollbarMetrics();
        var thumb = Math.Max(track * track / (track + extent), 20f);
        var position = _horizontal ? e.Position.X : e.Position.Y;
        var thumbStart = start + offset / extent * (track - thumb);
        _grabOffset = position >= thumbStart && position <= thumbStart + thumb
            ? position - thumbStart : thumb / 2;
        _dragging = true;
        _dragStart = e.Position;
        DragScrollbar(e.Position);
        e.StopPropagation();
    }

    protected override void OnMouseDragged(MouseDragEvent e)
    {
        if (!e.Buttons.HasFlag(MouseButtons.Primary) || e.DragStart != _dragStart)
            _dragging = false;
        if (_dragging)
            DragScrollbar(e.Position);
        else
            base.OnMouseDragged(e);
    }

    protected override void OnMouseReleased(MouseEvent e)
    {
        _dragging = false;
        base.OnMouseReleased(e);
    }

    private (float Start, float Track, float Extent, float Offset) ScrollbarMetrics()
        => _horizontal
            ? (LayoutPaddingPosition.X, LayoutPaddingSize.X, ScrollableWidth, ScrollLeft)
            : (LayoutPaddingPosition.Y, LayoutPaddingSize.Y, ScrollableHeight, ScrollTop);

    private void DragScrollbar(Vector2 position)
    {
        var (start, track, extent, _) = ScrollbarMetrics();

        if (extent <= 0 || track <= 0)

            return;
        var travel = track - Math.Max(track * track / (track + extent), 20f);

        if (travel <= 0)

            return;
        var cursor = _horizontal ? position.X : position.Y;
        var offset = Math.Clamp((cursor - start - _grabOffset) / travel, 0, 1) * extent;
        if (_horizontal)
            ScrollLeft = offset;
        else
            ScrollTop = offset;
    }
}
