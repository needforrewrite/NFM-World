using System.Numerics;

namespace NFMWorldLibrary.Rendering;

public sealed class CoplanarSurfaceResolver
{
    private const float PlaneTolerance = 0.01f;
    private const float MinimumArea = 0.0001f;
    private const float CellSize = 2048;
    private const int MaximumCells = 64;

    public readonly record struct Edge(Vector3 Start, Vector3 End);

    public sealed record Surface(Vector3[] Points, uint[] Triangles, Edge[] Edges);

    private readonly record struct PlaneKey(int X, int Y, int Z, long Distance);

    private readonly record struct Bounds(Vector2 Min, Vector2 Max)
    {
        public bool Overlaps(Bounds other) => Min.X < other.Max.X
                                              && Max.X > other.Min.X
                                              && Min.Y < other.Max.Y
                                              && Max.Y > other.Min.Y;
    }

    private readonly record struct Triangle(
        int Owner,
        Vector3 A,
        Vector3 B,
        Vector3 C,
        Vector3 Normal,
        Bounds Bounds
    );

    private sealed class Group
    {
        public readonly Dictionary<(int X, int Y), List<int>> Cells = [];
        public readonly List<int> Large = [];
        public readonly List<int> All = [];
    }

    private readonly Dictionary<PlaneKey, Group> _groups = [];
    private readonly List<Triangle> _triangles = [];
    private readonly HashSet<int> _candidates = [];
    private readonly List<int> _orderedCandidates = [];

    // Earlier owners retain the visible surface. Surfaces from the same piece never clip each other.
    public Surface? Resolve(int owner, ReadOnlySpan<Vector3> points, ReadOnlySpan<uint> indices, Vector3 normal)
    {
        var normalLengthSquared = normal.LengthSquared();
        if (points.Length < 3
            || indices.Length == 0
            || indices.Length % 3 != 0
            || !Finite(normal)
            || !float.IsFinite(normalLengthSquared)
            || normalLengthSquared < 1e-12f)
            return null;
        foreach (var index in indices)
            if (index >= points.Length)
                return null;
        normal = Vector3.Normalize(normal);
        var quantizedNormal = new Vector3(
            MathF.Round(normal.X * 1000),
            MathF.Round(normal.Y * 1000),
            MathF.Round(normal.Z * 1000)
        );
        var axis = DominantAxis(quantizedNormal);
        if (Component(normal, axis) < 0)
        {
            normal = -normal;
            quantizedNormal = -quantizedNormal;
        }

        var distance = Vector3.Dot(normal, points[0]);
        if (!float.IsFinite(distance)) return null;
        foreach (var point in points)
            if (!Finite(point)
                || Vector3.Abs(point).X > 1e12f
                || Vector3.Abs(point).Y > 1e12f
                || Vector3.Abs(point).Z > 1e12f
                || MathF.Abs(Vector3.Dot(normal, point) - distance) > PlaneTolerance)
                return null;

        var key = new PlaneKey(
            (int)quantizedNormal.X,
            (int)quantizedNormal.Y,
            (int)quantizedNormal.Z,
            (long)Math.Round(distance / (double)PlaneTolerance)
        );
        var bounds = GetBounds(points, axis);
        FindCandidates(key, bounds);
        _orderedCandidates.Clear();
        foreach (var index in _candidates)
        {
            var triangle = _triangles[index];
            if (triangle.Owner == owner
                || !bounds.Overlaps(triangle.Bounds)
                || Vector3.Dot(triangle.Normal, normal) < 0.999999f
                || MathF.Abs(Vector3.Dot(normal, triangle.A) - distance) > PlaneTolerance
                || MathF.Abs(Vector3.Dot(normal, triangle.B) - distance) > PlaneTolerance
                || MathF.Abs(Vector3.Dot(normal, triangle.C) - distance) > PlaneTolerance) continue;
            _orderedCandidates.Add(index);
        }

        _orderedCandidates.Sort();

        Surface? result = null;
        if (_orderedCandidates.Count > 0)
        {
            var outputPoints = new List<Vector3>();
            var outputIndices = new List<uint>();
            var changed = false;
            for (var i = 0; i < indices.Length; i += 3)
            {
                List<List<Vector3>> fragments =
                    [[points[(int)indices[i]], points[(int)indices[i + 1]], points[(int)indices[i + 2]]]];
                foreach (var candidate in _orderedCandidates)
                {
                    var triangle = _triangles[candidate];
                    var next = new List<List<Vector3>>();
                    foreach (var fragment in fragments)
                    {
                        if (!GetBounds(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(fragment), axis)
                                .Overlaps(triangle.Bounds))
                        {
                            next.Add(fragment);
                            continue;
                        }

                        changed |= Subtract(fragment, triangle, next);
                    }

                    fragments = next;
                    if (fragments.Count == 0) break;
                }

                foreach (var fragment in fragments)
                {
                    var start = (uint)outputPoints.Count;
                    outputPoints.AddRange(fragment);
                    for (uint j = 1; j + 1 < fragment.Count; j++)
                    {
                        outputIndices.Add(start);
                        outputIndices.Add(start + j);
                        outputIndices.Add(start + j + 1);
                    }
                }
            }

            if (changed) result = new Surface(outputPoints.ToArray(), outputIndices.ToArray(), VisibleEdges(points));
        }

        if (!_groups.TryGetValue(key, out var group)) _groups[key] = group = new Group();
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = points[(int)indices[i]];
            var b = points[(int)indices[i + 1]];
            var c = points[(int)indices[i + 2]];
            var winding = Vector3.Dot(Vector3.Cross(b - a, c - a), normal);
            if (MathF.Abs(winding) <= MinimumArea * 2) continue;
            if (winding < 0) (b, c) = (c, b);
            var triangleBounds = GetBounds([a, b, c], axis);
            var index = _triangles.Count;
            _triangles.Add(new Triangle(owner, a, b, c, normal, triangleBounds));
            AddToGroup(group, triangleBounds, index);
        }

        return result;
    }

    private static bool Subtract(List<Vector3> polygon, Triangle triangle, List<List<Vector3>> output)
    {
        var intersection = polygon;
        for (var edge = 0; edge < 3 && intersection.Count >= 3; edge++)
            intersection = Clip(intersection, triangle, edge, true);
        if (Area(intersection, triangle.Normal) <= MinimumArea)
        {
            output.Add(polygon);
            return false;
        }

        var inside = polygon;
        for (var edge = 0; edge < 3 && inside.Count >= 3; edge++)
        {
            var outside = Clip(inside, triangle, edge, false);
            if (Area(outside, triangle.Normal) > MinimumArea) output.Add(outside);
            inside = Clip(inside, triangle, edge, true);
        }

        return true;
    }

    private static List<Vector3> Clip(List<Vector3> polygon, Triangle triangle, int edge, bool inside)
    {
        var result = new List<Vector3>(polygon.Count + 1);
        if (polygon.Count == 0) return result;
        var previous = polygon[^1];
        var previousDistance = EdgeDistance(previous, triangle, edge) * (inside ? 1 : -1);
        foreach (var point in polygon)
        {
            var distance = EdgeDistance(point, triangle, edge) * (inside ? 1 : -1);
            if ((distance >= 0) != (previousDistance >= 0))
                result.Add(Vector3.Lerp(previous, point, Math.Clamp(previousDistance / (previousDistance - distance), 0, 1)));
            if (distance >= 0) result.Add(point);
            previous = point;
            previousDistance = distance;
        }

        return result;
    }

    private Edge[] VisibleEdges(ReadOnlySpan<Vector3> points)
    {
        var result = new List<Edge>();
        for (var i = 0; i < points.Length; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Length];
            List<(float Start, float End)> ranges = [(0, 1)];
            foreach (var candidate in _orderedCandidates)
            {
                var triangle = _triangles[candidate];
                var enter = 0f;
                var exit = 1f;
                for (var edge = 0; edge < 3 && enter < exit; edge++)
                {
                    var d0 = EdgeDistance(start, triangle, edge);
                    var d1 = EdgeDistance(end, triangle, edge);
                    if (d0 < 0 && d1 < 0)
                    {
                        exit = enter;
                        break;
                    }

                    if (d0 < 0) enter = MathF.Max(enter, d0 / (d0 - d1));
                    else if (d1 < 0) exit = MathF.Min(exit, d0 / (d0 - d1));
                }

                if (exit <= enter) continue;
                var next = new List<(float Start, float End)>();
                foreach (var range in ranges)
                {
                    if (exit <= range.Start || enter >= range.End) next.Add(range);
                    else
                    {
                        if (enter > range.Start) next.Add((range.Start, enter));
                        if (exit < range.End) next.Add((exit, range.End));
                    }
                }

                ranges = next;
                if (ranges.Count == 0) break;
            }

            foreach (var range in ranges)
            {
                var a = Vector3.Lerp(start, end, range.Start);
                var b = Vector3.Lerp(start, end, range.End);
                if (Vector3.DistanceSquared(a, b) > MinimumArea) result.Add(new Edge(a, b));
            }
        }

        return result.ToArray();
    }

    private void FindCandidates(PlaneKey key, Bounds bounds)
    {
        _candidates.Clear();
        for (var offset = -1; offset <= 1; offset++)
        {
            if (!_groups.TryGetValue(key with { Distance = key.Distance + offset }, out var group)) continue;
            var (minX, maxX, minY, maxY) = Cells(bounds);
            if (Large(minX, maxX, minY, maxY)) _candidates.UnionWith(group.All);
            else
            {
                _candidates.UnionWith(group.Large);
                for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                    if (group.Cells.TryGetValue((x, y), out var entries))
                        _candidates.UnionWith(entries);
            }
        }
    }

    private static void AddToGroup(Group group, Bounds bounds, int index)
    {
        group.All.Add(index);
        var (minX, maxX, minY, maxY) = Cells(bounds);
        if (Large(minX, maxX, minY, maxY))
        {
            group.Large.Add(index);
            return;
        }

        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        {
            if (!group.Cells.TryGetValue((x, y), out var entries)) group.Cells[(x, y)] = entries = [];
            entries.Add(index);
        }
    }

    private static (int MinX, int MaxX, int MinY, int MaxY) Cells(Bounds bounds)
        => ((int)MathF.Floor(bounds.Min.X / CellSize), (int)MathF.Floor(bounds.Max.X / CellSize),
            (int)MathF.Floor(bounds.Min.Y / CellSize), (int)MathF.Floor(bounds.Max.Y / CellSize));

    private static bool Large(int minX, int maxX, int minY, int maxY)
        => ((double)maxX - minX + 1) * ((double)maxY - minY + 1) > MaximumCells;

    private static Bounds GetBounds(ReadOnlySpan<Vector3> points, int axis)
    {
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        foreach (var point in points)
        {
            var projected = axis switch
            {
                0 => new Vector2(point.Y, point.Z), 1 => new Vector2(point.X, point.Z), _ => new Vector2(point.X, point.Y)
            };
            min = Vector2.Min(min, projected);
            max = Vector2.Max(max, projected);
        }

        return new Bounds(min, max);
    }

    private static float Area(List<Vector3> polygon, Vector3 normal)
    {
        if (polygon.Count < 3) return 0;
        var sum = Vector3.Zero;
        for (var i = 1; i + 1 < polygon.Count; i++)
            sum += Vector3.Cross(polygon[i] - polygon[0], polygon[i + 1] - polygon[0]);
        return MathF.Abs(Vector3.Dot(sum, normal)) * 0.5f;
    }

    private static float EdgeDistance(Vector3 point, Triangle triangle, int edge)
    {
        var a = edge switch { 0 => triangle.A, 1 => triangle.B, _ => triangle.C };
        var b = edge switch { 0 => triangle.B, 1 => triangle.C, _ => triangle.A };
        return Vector3.Dot(Vector3.Cross(b - a, point - a), triangle.Normal);
    }

    private static int DominantAxis(Vector3 normal)
        => MathF.Abs(normal.X) > MathF.Abs(normal.Y)
            ? (MathF.Abs(normal.X) > MathF.Abs(normal.Z) ? 0 : 2)
            : (MathF.Abs(normal.Y) > MathF.Abs(normal.Z) ? 1 : 2);

    private static float Component(Vector3 value, int axis) => axis switch { 0 => value.X, 1 => value.Y, _ => value.Z };
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
