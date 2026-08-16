using MemoryPack;
using nfm_world_library.Lua;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant)]
[method: MemoryPackConstructor]
public readonly partial record struct SrcRad3dCollisionMesh([property: MemoryPackOrder(0)] f64Vector3[] Vertices, [property: MemoryPackOrder(1)] ushort[] Indices)
{
    [MemoryPackIgnore] public (f64Vector3 min, f64Vector3 max)[] Aabb { get; } = CalculateAabb(Vertices, Indices);

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

    private static (f64Vector3 min, f64Vector3 max)[] CalculateAabb(ReadOnlySpan<f64Vector3> vertices, ReadOnlySpan<ushort> indices)
    {
        var aabbs = new (f64Vector3 min, f64Vector3 max)[indices.Length / 3];
        for (var i = 0; i < indices.Length; i += 3)
        {
            ref readonly var v0 = ref vertices[indices[i]];
            ref readonly var v1 = ref vertices[indices[i + 1]];
            ref readonly var v2 = ref vertices[indices[i + 2]];

            aabbs[i / 3] = TriangleMesh.ComputeAABB(v0, v1, v2);
        }

        return aabbs;
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public bool Equals(SrcRad3dCollisionMesh other)
    {
        if (!Vertices.SequenceEqual(other.Vertices)) return false;
        return Indices.SequenceEqual(other.Indices);
    }
    
    public bool Equals(SrcRad3dCollisionMesh? other)
    {
        if (other is null) return false;
        return Equals(other.Value);
    }
}