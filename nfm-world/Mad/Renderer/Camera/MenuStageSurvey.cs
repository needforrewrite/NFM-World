using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld;

public readonly record struct MenuRoadSegment(Vector3 Start, Vector3 End, int StartNode = 0)
{
    public float Length => Vector3.Distance(Start, End);
    public Vector3 Direction => Vector3.Normalize(End - Start);
}

internal sealed class MenuStageSurvey
{
    private readonly record struct Bounds(Vector3 Min, Vector3 Max);
    private readonly List<Bounds> _obstacles = [];
    public List<MenuRoadSegment> Roads { get; } = [];
    public List<MenuRoadSegment> Cruises { get; } = [];
    public Vector3 Center { get; }
    public float Radius { get; }
    public Vector3 BoundaryMin { get; }
    public Vector3 BoundaryMax { get; }
    private readonly Vector3 _courseMin;
    private readonly Vector3 _courseMax;

    public MenuStageSurvey(BackendStage stage)
    {
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        var localBounds = new Dictionary<Rad3d, Bounds>();
        foreach (var piece in stage.Pieces.OfType<StageObject>())
        {
            if (!localBounds.TryGetValue(piece.Rad, out var local))
            {
                var points = piece.Rad.Polys.SelectMany(poly => poly.Points).ToArray();
                if (points.Length == 0) continue;
                local = new Bounds(points.Aggregate(Vector3.Min), points.Aggregate(Vector3.Max));
                localBounds.Add(piece.Rad, local);
            }
            var rotation = Matrix.CreateFromEuler((Euler)piece.Rotation);
            var position = (Vector3)piece.Position;
            var low = new Vector3(float.MaxValue);
            var high = new Vector3(float.MinValue);
            for (var corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? local.Min.X : local.Max.X,
                    (corner & 2) == 0 ? local.Min.Y : local.Max.Y,
                    (corner & 4) == 0 ? local.Min.Z : local.Max.Z);
                point = Vector3.Transform(point, rotation) + position;
                low = Vector3.Min(low, point);
                high = Vector3.Max(high, point);
            }
            _obstacles.Add(new Bounds(low, high));
            if (piece.OriginalPlacement.IsWall) continue;
            minimum = Vector3.Min(minimum, low);
            maximum = Vector3.Max(maximum, high);
        }
        if (minimum.X == float.MaxValue)
        {
            minimum = new Vector3(-3000, 0, -3000);
            maximum = new Vector3(3000, 250, 3000);
        }
        var nodes = stage.Nodes.Select(node => (Vector3)node.Position).ToArray();
        if (nodes.Length > 0)
        {
            minimum = nodes.Aggregate(Vector3.Min) - new Vector3(1000, 0, 1000);
            maximum = nodes.Aggregate(Vector3.Max) + new Vector3(1000, 0, 1000);
        }
        _courseMin = minimum;
        _courseMax = maximum;
        var loader = stage.StageLoader;
        var hasBounds = loader.maxr > loader.maxl && loader.maxt > loader.maxb;
        BoundaryMin = hasBounds ? new Vector3(loader.maxl, minimum.Y, loader.maxb) : minimum;
        BoundaryMax = hasBounds ? new Vector3(loader.maxr, maximum.Y, loader.maxt) : maximum;
        Center = (minimum + maximum) * 0.5f;
        Radius = Math.Max(4000, (maximum - minimum).Length() * 0.5f);

        for (var i = 0; i < nodes.Length - 1; i++)
        {
            var segment = new MenuRoadSegment(nodes[i], nodes[i + 1], i);
            var horizontal = segment.End - segment.Start;
            horizontal.Y = 0;
            if (segment.Length is >= 2500 and <= 25000 && horizontal.LengthSquared() >= 1000 * 1000)
                Cruises.Add(segment);
        }
        for (var first = 0; first < nodes.Length - 1; first++)
        for (var last = first + 1; last < Math.Min(nodes.Length, first + 9); last++)
        {
            var road = new MenuRoadSegment(nodes[first], nodes[last], first);
            if (road.Length < 8000 || Math.Abs(road.Direction.Y) > 0.08f) continue;
            var previous = -1f;
            var straight = true;
            for (var i = first; i <= last; i++)
            {
                var along = Vector3.Dot(nodes[i] - road.Start, road.Direction);
                if (along < previous || Vector3.Distance(nodes[i], road.Start + road.Direction * along) > 350)
                {
                    straight = false;
                    break;
                }
                previous = along;
            }
            if (straight) Roads.Add(road);
        }
    }

    public bool InCourse(Vector3 point) => point.X >= _courseMin.X - 2000 && point.X <= _courseMax.X + 2000
        && point.Z >= _courseMin.Z - 2000 && point.Z <= _courseMax.Z + 2000;

    public float LocalCeiling(Vector3 from, Vector3 to, float padding)
    {
        var minimum = Vector3.Min(from, to) - new Vector3(padding);
        var maximum = Vector3.Max(from, to) + new Vector3(padding);
        var ceiling = 250f;
        foreach (var obstacle in _obstacles)
            if (obstacle.Max.X >= minimum.X && obstacle.Min.X <= maximum.X
                && obstacle.Max.Z >= minimum.Z && obstacle.Min.Z <= maximum.Z)
                ceiling = Math.Min(ceiling, obstacle.Min.Y);
        return ceiling;
    }

    public bool IsClear(Vector3 from, Vector3 to, float clearance)
    {
        if (Math.Max(from.Y, to.Y) > 250 - clearance) return false;
        foreach (var obstacle in _obstacles)
        {
            var padding = new Vector3(clearance);
            if (Intersects(from, to, obstacle.Min - padding, obstacle.Max + padding)) return false;
        }
        return true;
    }

    private static bool Intersects(Vector3 from, Vector3 to, Vector3 minimum, Vector3 maximum)
    {
        var direction = to - from;
        var entry = 0f;
        var exit = 1f;
        for (var axis = 0; axis < 3; axis++)
        {
            var origin = axis == 0 ? from.X : axis == 1 ? from.Y : from.Z;
            var delta = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            var low = axis == 0 ? minimum.X : axis == 1 ? minimum.Y : minimum.Z;
            var high = axis == 0 ? maximum.X : axis == 1 ? maximum.Y : maximum.Z;
            if (Math.Abs(delta) < 0.0001f)
            {
                if (origin < low || origin > high) return false;
                continue;
            }
            var a = (low - origin) / delta;
            var b = (high - origin) / delta;
            entry = Math.Max(entry, Math.Min(a, b));
            exit = Math.Min(exit, Math.Max(a, b));
            if (entry > exit) return false;
        }
        return true;
    }
}
