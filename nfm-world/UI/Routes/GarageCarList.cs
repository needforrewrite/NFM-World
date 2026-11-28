using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI.ViewModels;

namespace NFMWorld.UI.Routes;

internal sealed class GarageCarList : View
{
    private string? _revealedCar;
    private object? _collections;
    private string? _query;
    private float? _thumbOffset;
    private int _columns;

    protected override bool OnPostLayout()
    {
        var columns = GarageView.ColumnsForWidth(TheGraphics.G.Viewport.X);
        if (GarageView.GridColumns != columns)
            GarageView.ColumnCount.Value = columns;

        return false;
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        // Scroll extents are current here, before the children paint.
        var identity = UiState.Garage.CurrentCar.Value?.FileName;
        var collections = UiState.Garage.Collections.Value;
        var query = UiState.Garage.SearchQuery.Value;
        var columns = GarageView.GridColumns;

        if (_revealedCar == identity && ReferenceEquals(_collections, collections) && _query == query && _columns == columns)

            return;

        if (!ReferenceEquals(_collections, collections) || _query != query)
            ScrollTop = 0;
        _collections = collections;
        _query = query;
        _revealedCar = identity;
        _columns = columns;
        FindCard(this, "Garage car " + identity)?.ScrollIntoView();
    }

    private static Component? FindCard(Node node, string name)
    {
        if (node is Component component && component.Name == name)

            return component;

        foreach (var child in node.VisualChildren)

            if (FindCard(child, name) is { } found)

                return found;

        return null;
    }

    private bool OnTrack(Vector2 point)
    {
        var origin = LayoutPaddingPosition;
        var size = LayoutPaddingSize;

        return ScrollableHeight > 0
               && point.X >= origin.X + size.X - 8
               && point.X <= origin.X + size.X
               && point.Y >= origin.Y
               && point.Y <= origin.Y + size.Y;
    }

    private float ThumbHeight => Math.Min(
        LayoutPaddingSize.Y,
        Math.Max(20, LayoutPaddingSize.Y * LayoutPaddingSize.Y / (LayoutPaddingSize.Y + ScrollableHeight))
    );

    protected override void OnMousePressed(MouseEvent e)
    {
        _thumbOffset = null;

        if (e.Button != MouseButton.Primary || !OnTrack(e.Position))

            return;

        var travel = LayoutPaddingSize.Y - ThumbHeight;
        var thumbTop = LayoutPaddingPosition.Y + ScrollTop / ScrollableHeight * travel;
        _thumbOffset = e.Position.Y >= thumbTop && e.Position.Y <= thumbTop + ThumbHeight
            ? e.Position.Y - thumbTop : ThumbHeight / 2;
        DragThumb(e.Position.Y);
        e.StopPropagation();
    }

    protected override void OnMouseDragged(MouseDragEvent e)
    {
        if (e.Button == (byte)MouseButton.Primary && _thumbOffset.HasValue && OnTrack(e.DragStart))
            DragThumb(e.Position.Y);
    }

    protected override void OnMouseReleased(MouseEvent e) => _thumbOffset = null;

    private void DragThumb(float mouseY)
    {
        var travel = LayoutPaddingSize.Y - ThumbHeight;
        if (travel > 0)
            ScrollTop = (mouseY - LayoutPaddingPosition.Y - _thumbOffset!.Value) / travel * ScrollableHeight;
    }
}
