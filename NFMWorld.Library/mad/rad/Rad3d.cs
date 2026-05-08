using System.Text.Json.Serialization;
using MessagePack;

namespace NFMWorldLibrary.Rad;

[MessagePackObject]
[method: SerializationConstructor]
public sealed record Rad3d(
    [property: JsonPropertyName("colors"), Key(0)] Color3[] Colors,
    [property: JsonPropertyName("stats"), Key(1)] CarStats Stats,
    [property: JsonPropertyName("wheels"), Key(2)] Rad3dWheelDef[] Wheels,
    [property: JsonPropertyName("rims"), Key(3)] Rad3dRimsDef? Rims,
    [property: JsonPropertyName("boxes"), Key(4)] Rad3dBoxDef[] Boxes,
    [property: JsonPropertyName("polys"), Key(5)] Rad3dPoly[] Polys,
    [property: JsonPropertyName("shadow"), Key(6)] bool CastsShadow,
    [property: JsonPropertyName("atp"), Key(7)] Vector2[] Atp,
    [property: JsonPropertyName("fileName"), Key(8)] string FileName = "hogan rewish"
)
{
    [IgnoreMember] public int MaxRadius { get; } = CalculateMaxRadius(Polys);

    private readonly int _hashCode = CalculateHashCode(Colors, Stats, Wheels, Rims, Boxes, Polys, CastsShadow, Atp);
    private readonly int _visualHashCode = CalculateVisualHashCode(Colors, Wheels, Rims, Polys, CastsShadow);

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
        return Atp.Equals(other.Atp);
    }

    private static int CalculateHashCode(
        Color3[] colors,
        CarStats stats,
        Rad3dWheelDef[] wheels,
        Rad3dRimsDef? rims,
        Rad3dBoxDef[] boxes,
        Rad3dPoly[] polys,
        bool castsShadow,
        Vector2[] atp
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