using System.Text.Json.Serialization;
using FixedMathSharp;
using MemoryPack;
using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Rad;

// init properties aren't compatible with CircularReference, so can't use record
[MemoryPackable(GenerateType.CircularReference), LuaVisible]
public sealed partial class Rad3d(
    Color3[] colors,
    CarStats stats,
    Rad3dWheelDef[] wheels,
    Rad3dRimsDef? rims,
    Rad3dBoxDef[] boxes,
    Rad3dPoly[] polys,
    bool castsShadow,
    LuaVector2[] atp,
    string fileName = "hogan rewish",
    SrcRad3dCollisionMesh? collisionMesh = null,
    SrcRad3dCollisionHull? collisionHull = null,
    Rad3dAttachmentLine[]? atLines = null
)
{
    [MemoryPackIgnore, LuaName] public int MaxRadius { get; } = CalculateMaxRadius(polys);

    [JsonPropertyName("colors"), MemoryPackOrder(0), LuaName]
    public LuaArray<Color3> Colors { get; set; } = colors;

    [JsonPropertyName("stats"), MemoryPackOrder(1), LuaName]
    public CarStats Stats { get; set; } = stats;

    [JsonPropertyName("wheels"), MemoryPackOrder(2), LuaName]
    public LuaArray<Rad3dWheelDef> Wheels { get; set; } = wheels;

    [JsonPropertyName("rims"), MemoryPackOrder(3), LuaName]
    public Rad3dRimsDef? Rims { get; set; } = rims;

    [JsonPropertyName("boxes"), MemoryPackOrder(4), LuaName]
    public LuaArray<Rad3dBoxDef> Boxes { get; set; } = boxes;

    [JsonPropertyName("polys"), MemoryPackOrder(5), LuaName]
    public LuaArray<Rad3dPoly> Polys { get; set; } = polys;

    [JsonPropertyName("shadow"), MemoryPackOrder(6), LuaName]
    public bool CastsShadow { get; set; } = castsShadow;

    [JsonPropertyName("atp"), MemoryPackOrder(7), LuaName]
    public LuaArray<LuaVector2> Atp { get; set; } = atp;

    [JsonPropertyName("fileName"), MemoryPackOrder(8), LuaName]
    public string FileName { get; set; } = fileName;

    [JsonPropertyName("collisionMesh"), MemoryPackOrder(9)]
    public SrcRad3dCollisionMesh? CollisionMesh { get; set; } = collisionMesh;

    [JsonPropertyName("collisionHull"), MemoryPackOrder(10)]
    public SrcRad3dCollisionHull? CollisionHull { get; set; } = collisionHull;

    [JsonPropertyName("atLines"), MemoryPackOrder(11), LuaName]
    public LuaArray<Rad3dAttachmentLine>? AtLines { get; set; } = atLines != null ? new LuaArray<Rad3dAttachmentLine>(atLines) : null;

    private readonly int _hashCode = CalculateHashCode(colors, stats, wheels, rims, boxes, polys, castsShadow, atp, collisionMesh, collisionHull, atLines);
    private readonly int _visualHashCode = CalculateVisualHashCode(colors, wheels, rims, polys, castsShadow);

    private static int CalculateMaxRadius(Rad3dPoly[] polys)
    {
        var maxR = 0;
        foreach (var poly in polys)
        foreach (var point in poly.Points)
        {
            var rad = (int) float.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);
            if (rad > maxR)
            {
                maxR = rad;
            }
        }

        return maxR;
    }

    [MemoryPackConstructor]
    private Rad3d() : this([], default, [], null, [], [], false, [])
    {
    }

    public bool Equals(Rad3d? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!Colors.SequenceEqual(other.Colors)) return false;
        if (!Stats.Equals(other.Stats)) return false;
        if (!Wheels.SequenceEqual(other.Wheels)) return false;
        if (!Nullable.Equals(Rims, other.Rims)) return false;
        if (!Boxes.SequenceEqual(other.Boxes)) return false;
        if (!Polys.SequenceEqual(other.Polys)) return false;
        if (CastsShadow != other.CastsShadow) return false;
        if (!Atp.SequenceEqual(other.Atp)) return false;
        if (CollisionMesh != null && !CollisionMesh.Equals(other.CollisionMesh)) return false;
        if (CollisionMesh == null && other.CollisionMesh != null) return false;
        if (CollisionHull != null && !CollisionHull.Equals(other.CollisionHull)) return false;
        if (CollisionHull == null && other.CollisionHull != null) return false;
        if (AtLines != null && !AtLines.SequenceEqual(other.AtLines)) return false;
        if (AtLines == null && other.AtLines != null) return false;
        return true;
    }

    private static int CalculateHashCode(
        Color3[] colors,
        CarStats stats,
        Rad3dWheelDef[] wheels,
        Rad3dRimsDef? rims,
        Rad3dBoxDef[] boxes,
        Rad3dPoly[] polys,
        bool castsShadow,
        LuaVector2[] atp,
        SrcRad3dCollisionMesh? colMesh,
        SrcRad3dCollisionHull? colHull,
        Rad3dAttachmentLine[]? atLines
    )
    {
        var hashCode = new HashCode();
        hashCode.Add(colors.Length);
        foreach (var color in colors)
        {
            hashCode.Add(color);
        }
        hashCode.Add(stats);
        hashCode.Add(wheels.Length);
        foreach (var wheel in wheels)
        {
            hashCode.Add(wheel);
        }
        hashCode.Add(rims);
        hashCode.Add(boxes.Length);
        foreach (var box in boxes)
        {
            hashCode.Add(box);
        }
        hashCode.Add(polys.Length);
        foreach (var poly in polys)
        {
            hashCode.Add(poly);
        }
        hashCode.Add(castsShadow);
        hashCode.Add(atp.Length);
        foreach (var at in atp)
        {
            hashCode.Add(at);
        }

        if (colMesh != null)
        {
            hashCode.Add(colMesh);
        }
        if (colHull != null)
        {
            hashCode.Add(colHull);
        }
        if (atLines != null)
        {
            hashCode.Add(atLines.Length);
            foreach (var atLine in atLines)
            {
                hashCode.Add(atLine);
            }
        }
        return hashCode.ToHashCode();
    }
    
    private static int CalculateVisualHashCode(Color3[] colors, Rad3dWheelDef[] wheels, Rad3dRimsDef? rims, Rad3dPoly[] polys, bool castsShadow)
    {
        var hashCode = new HashCode();
        hashCode.Add(colors.Length);
        foreach (var color in colors)
        {
            hashCode.Add(color);
        }
        hashCode.Add(wheels.Length);
        foreach (var wheel in wheels)
        {
            hashCode.Add(wheel);
        }
        hashCode.Add(rims);
        hashCode.Add(polys.Length);
        foreach (var poly in polys)
        {
            hashCode.Add(poly);
        }
        hashCode.Add(castsShadow);
        return hashCode.ToHashCode();
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public Rad3d(Rad3dPoly[] polys, bool castsShadow, string fileName) : this([], new CarStats(), [], null, [], polys, castsShadow, [], fileName)
    {
    }

    public class VisualEqualityComparer : IEqualityComparer<Rad3d>
    {
        public static VisualEqualityComparer Instance { get; } = new();
        
        public bool Equals(Rad3d? x, Rad3d? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            if (!x.Colors.SequenceEqual(y.Colors)) return false;
            if (!x.Wheels.SequenceEqual(y.Wheels)) return false;
            if (!Nullable.Equals(x.Rims, y.Rims)) return false;
            if (!x.Polys.SequenceEqual(y.Polys)) return false;
            if (x.CastsShadow != y.CastsShadow) return false;
            return true;
        }

        public int GetHashCode(Rad3d obj)
        {
            return obj._visualHashCode;
        }
    }
}

[LuaVisible]
public partial record struct LuaVector2([property: LuaName] float X, [property: LuaName] float Y);