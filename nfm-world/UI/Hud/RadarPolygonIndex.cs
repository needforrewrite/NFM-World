using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud;

// A spatial hierarchy over the original paint order. Queries return sorted polygon indices.
internal sealed class RadarPolygonIndex
{
    private const int LeafSize = 16;
    private readonly Bounds[] _bounds;
    private readonly int[] _indices;
    private readonly int[] _visible;
    private readonly List<Node> _nodes = [];
    private int _visibleCount;

    private readonly record struct Bounds(float MinX, float MinZ, float MaxX, float MaxZ)
    {
        public bool Intersects(Bounds other) => MaxX >= other.MinX && MinX <= other.MaxX
                                                && MaxZ >= other.MinZ && MinZ <= other.MaxZ;

        public Bounds Union(Bounds other) => new(
            MathF.Min(MinX, other.MinX),
            MathF.Min(MinZ, other.MinZ),
            MathF.Max(MaxX, other.MaxX),
            MathF.Max(MaxZ, other.MaxZ)
        );
    }

    private readonly record struct Node(Bounds Bounds, int Start, int Count, int Left, int Right);

    public RadarPolygonIndex(IReadOnlyList<MinimapPolygon> polygons)
    {
        _bounds = new Bounds[polygons.Count];
        _indices = new int[polygons.Count];
        _visible = new int[polygons.Count];
        for (var i = 0; i < polygons.Count; i++)
        {
            _indices[i] = i;
            var bounds = new Bounds(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity
            );
            foreach (var (x, z) in polygons[i].Vertices)
                bounds = bounds.Union(new Bounds(x, z, x, z));
            _bounds[i] = bounds;
        }

        if (_indices.Length != 0) Build(0, _indices.Length);
    }

    private int Build(int start, int count)
    {
        var bounds = _bounds[_indices[start]];
        for (var i = start + 1; i < start + count; i++) bounds = bounds.Union(_bounds[_indices[i]]);
        var node = _nodes.Count;
        _nodes.Add(default);
        if (count <= LeafSize)
        {
            _nodes[node] = new Node(bounds, start, count, -1, -1);
            return node;
        }

        var byX = bounds.MaxX - bounds.MinX >= bounds.MaxZ - bounds.MinZ;
        Array.Sort(_indices, start, count, Comparer<int>.Create((a, b) =>
            {
                var x = _bounds[a];
                var y = _bounds[b];
                return (byX ? x.MinX + x.MaxX : x.MinZ + x.MaxZ)
                    .CompareTo(byX ? y.MinX + y.MaxX : y.MinZ + y.MaxZ);
            }
        )
        );
        var leftCount = count / 2;
        var left = Build(start, leftCount);
        var right = Build(start + leftCount, count - leftCount);
        _nodes[node] = new Node(bounds, start, count, left, right);
        return node;
    }

    public ReadOnlySpan<int> Query(float minX, float minZ, float maxX, float maxZ)
    {
        _visibleCount = 0;
        if (_nodes.Count != 0) Query(0, new Bounds(minX, minZ, maxX, maxZ));
        Array.Sort(_visible, 0, _visibleCount);
        return _visible.AsSpan(0, _visibleCount);
    }

    private void Query(int index, Bounds area)
    {
        var node = _nodes[index];
        if (!node.Bounds.Intersects(area)) return;
        if (node.Left >= 0)
        {
            Query(node.Left, area);
            Query(node.Right, area);
            return;
        }

        for (var i = node.Start; i < node.Start + node.Count; i++)
        {
            var polygon = _indices[i];
            if (_bounds[polygon].Intersects(area)) _visible[_visibleCount++] = polygon;
        }
    }
}
