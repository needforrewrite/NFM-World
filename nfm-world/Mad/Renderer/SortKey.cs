using System.Runtime.CompilerServices;

namespace NFMWorld;

public enum RenderBucket : byte
{
    Sky,
    Ground,
    GroundPolys,
    Mountains,
    
    StagePieces,
    
    Flames,
    Dust,
    Chips,
    Sparks,
    FixHoopElectricity,
    
    FixFlare,
    
    Cars,
    
    CheckpointBounds,

    CollisionDebugMesh
}

/// <summary>
/// Sort key for ordering draws within a <see cref="RenderQueue"/>.
/// Packs a <see cref="RenderBucket"/> (2 bits) and a sort value (30 bits) into a single uint.
/// </summary>
public readonly struct SortKey(uint value) : IEquatable<SortKey>, IComparable<SortKey>
{
    public readonly uint Value = value;

    public RenderBucket Bucket => (RenderBucket)(Value >> 16);
    public ushort SortValue => (ushort)(Value & 0xFFFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SortKey Create(RenderBucket bucket, ushort sortValue = 0)
    {
        return new SortKey(((uint)bucket << 16) | ((uint)sortValue & 0xFFFF));
    }

    public int CompareTo(SortKey other) => Value.CompareTo(other.Value);

    public static bool operator <(SortKey a, SortKey b) => a.Value < b.Value;
    public static bool operator >(SortKey a, SortKey b) => a.Value > b.Value;
    public static bool operator <=(SortKey a, SortKey b) => a.Value <= b.Value;
    public static bool operator >=(SortKey a, SortKey b) => a.Value >= b.Value;

    public bool Equals(SortKey other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is SortKey other && Equals(other);
    public override int GetHashCode() => (int)Value;

    public static bool operator ==(SortKey left, SortKey right) => left.Equals(right);
    public static bool operator !=(SortKey left, SortKey right) => !left.Equals(right);
}
