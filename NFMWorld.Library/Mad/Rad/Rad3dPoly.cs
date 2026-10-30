using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using HoleyDiver;
using MemoryPack;
using NFMWorld.PluginSystem;
using NFMWorld.Sentry;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.CircularReference)]
[PluginVisible]
public readonly partial record struct Rad3dPoly
{
    public const int CurrentTriangulationVersion = 2;
    
    [JsonPropertyName("c"), MemoryPackOrder(0), PluginVisible]
    public Color3 Color { get; init; }

    [JsonPropertyName("colnum"), MemoryPackOrder(1), PluginVisible]
    public int? ColNum { get; init; }

    [JsonPropertyName("polyType"), MemoryPackOrder(2), PluginVisible]
    public PolyType PolyType { get; init; }

    [JsonPropertyName("lineType"), MemoryPackOrder(3), PluginVisible]
    public LineType? LineType { get; init; }

    [JsonPropertyName("decalOffset"), MemoryPackOrder(4)]
    public float DecalOffset { get; init; }

    [JsonPropertyName("p"), MemoryPackOrder(5)]
    public Vector3[] Points { get; init; }

    [JsonPropertyName("tri"), MemoryPackOrder(6)]
    public ImmutableArray<uint> Triangles { get; init; }

    [JsonPropertyName("cent"), MemoryPackOrder(7)]
    public Vector3 Centroid { get; init; }

    [JsonPropertyName("norm"), MemoryPackOrder(8)]
    public Vector3 Normal { get; init; }

    [JsonPropertyName("triVersion"), MemoryPackOrder(9)]
    public int TriangulationVersion { get; init; }
    
    [JsonPropertyName("triAlgorithm"), MemoryPackOrder(10)]
    public PolygonTriangulator.TriangulationAlgorithm TriangulationAlgorithm { get; init; } = PolygonTriangulator.TriangulationAlgorithm.Libtess;

    private readonly int _hashCode;

    [MemoryPackConstructor]
    public Rad3dPoly() : this(default, null, default, null, 0, [])
    {
    }

    public Rad3dPoly(
        Color3 Color,
        int? ColNum,
        PolyType PolyType,
        LineType? LineType,
        float DecalOffset,
        Vector3[] Points,
        ImmutableArray<uint>? Triangles = null,
        Vector3? Centroid = null,
        Vector3? Normal = null,
        PolygonTriangulator.TriangulationAlgorithm TriangulationAlgorithm = PolygonTriangulator.TriangulationAlgorithm.Libtess)
    {
        this.Color = Color;
        this.ColNum = ColNum;
        this.PolyType = PolyType;
        this.LineType = LineType;
        this.DecalOffset = DecalOffset;
        this.Points = Points;
        this.Triangles = Triangles ?? Triangulate(Points);
        this.Centroid = Centroid ?? PolygonTriangulator.ComputeCentroid(Points);
        this.Normal = Normal ?? GetNormal(Points, this.Centroid);
        this.TriangulationAlgorithm = TriangulationAlgorithm;
        TriangulationVersion = Triangles != null ? int.MaxValue : CurrentTriangulationVersion; // Manual triangulation = max value
        _hashCode = CalculateHashCode(Color, ColNum, PolyType, LineType, DecalOffset, Points, this.Triangles.AsSpan());
    }

    private static Vector3 GetNormal(Vector3[] verts, Vector3 centroid)
    {
        if (verts.Length < 3)
        {
            return new Vector3(0, 0, 1);
        }

        if (verts.Length == 3)
        {
            // Compute triangle normal
            var normal = Vector3.Normalize(Vector3.Cross(
                verts[1] - verts[0],
                verts[2] - verts[0]
            ));

            return normal;
        }
        
        return PolygonTriangulator.ComputeBestFitPlaneNormal(verts, centroid);
    }

    private ImmutableArray<uint> Triangulate(Vector3[] verts)
    {
        if (verts.Length <= 2)
        {
            return [];
        }
        
        if (verts.Length <= 3)
        {
            return [0, 1, 2];
        }

        var transaction = SentrySdk.StartTransaction($"TriangulatePoly_{TriangulationAlgorithm}", "rad3dpoly.triangulation");
        var arr = ImmutableCollectionsMarshal.AsImmutableArray(PolygonTriangulator.Triangulate(verts, TriangulationAlgorithm).Triangles);
        transaction.Finish();
        return arr;
    }

    public bool Equals(Rad3dPoly other)
    {
        if (!Color.Equals(other.Color)) return false;
        if (ColNum != other.ColNum) return false;
        if (PolyType != other.PolyType) return false;
        if (LineType != other.LineType) return false;
        if (!DecalOffset.Equals(other.DecalOffset)) return false;
        return Points.SequenceEqual(other.Points);
    }

    private static int CalculateHashCode(Color3 color, int? colNum, PolyType polyType, LineType? lineType, float decalOffset, ReadOnlySpan<Vector3> points, ReadOnlySpan<uint> triangles)
    {
        var hashCode = new HashCode();
        hashCode.Add(color);
        hashCode.Add(colNum);
        hashCode.Add(polyType);
        hashCode.Add(lineType);
        hashCode.Add(decalOffset);
        hashCode.Add(points.Length);
        foreach (var point in points)
        {
            hashCode.Add(point);
        }
        hashCode.Add(triangles.Length);
        foreach (var triangle in triangles)
        {
            hashCode.Add(triangle);
        }
        return hashCode.ToHashCode();
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public Rad3dPoly WithPoints(Vector3[] verts, ImmutableArray<uint>? tris)
    {
        return this with
        {
            Points = verts,
            Triangles = tris ?? Triangulate(verts),
            Centroid = PolygonTriangulator.ComputeCentroid(verts),
            Normal = GetNormal(verts, PolygonTriangulator.ComputeCentroid(verts))
        };
    }

    public Rad3dPoly SafeClone()
    {
        return this with { Points = [..Points] };
    }
}