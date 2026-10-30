using System.Diagnostics;
using MemoryPack;
using NFMWorldLibrary.Collision;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant)]
[method: MemoryPackConstructor]
public readonly partial record struct Rad3dCollisionHull([property: MemoryPackOrder(0)] f64Vector3[] Vertices, [property: MemoryPackOrder(1)] ushort[] Indices)
{
    private static readonly ConvexHullCalculator _calculator = new();
    
    private readonly int _hashCode = CalculateHashCode(Vertices, Indices);

    private static int CalculateHashCode(f64Vector3[] vertices, ushort[] indices)
    {
        var hashCode = new HashCode();
        foreach (var vertex in vertices)
        {
            hashCode.Add(vertex);
        }

        foreach (var index in indices)
        {
            hashCode.Add(index);
        }

        return hashCode.ToHashCode();
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public bool Equals(Rad3dCollisionHull other)
    {
        if (!Vertices.SequenceEqual(other.Vertices)) return false;
        return Indices.SequenceEqual(other.Indices);
    }
    
    public bool Equals(Rad3dCollisionHull? other)
    {
        if (other is null) return false;
        return Equals(other.Value);
    }

    // ReSharper disable once ConditionalTernaryEqualBranch
    public Rad3dCollisionHull(ReadOnlySpan<f64Vector3> hullVerts) : this(Parse(hullVerts) is var v ? v.Vertices : v.Vertices, v.Indices)
    {
    }

    private static (f64Vector3[] Vertices, ushort[] Indices) Parse(ReadOnlySpan<f64Vector3> hullVerts)
    {
        var stopwatch = Stopwatch.StartNew();
        var verts = new List<f64Vector3>(hullVerts.Length);
        var indices = new List<ushort>(hullVerts.Length / 3);
        _calculator.GenerateHull(hullVerts, false, verts, indices, null);
        
        Logging.Debug($"Convex hull generated with {verts.Count} vertices and {indices.Count / 3} triangles in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        
        return (verts.ToArray(), indices.ToArray());
    }
}