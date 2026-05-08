using System.Text.Json.Serialization;
using MessagePack;

namespace NFMWorldLibrary.Mad.Rad;

[MessagePackObject]
public readonly record struct Rad3dPoly(
    [property: JsonPropertyName("c"), Key(0)] Color3 Color,
    [property: JsonPropertyName("colnum"), Key(1)] int? ColNum,
    [property: JsonPropertyName("polyType"), Key(2)] PolyType PolyType,
    [property: JsonPropertyName("lineType"), Key(3)] LineType? LineType,
    [property: JsonPropertyName("decalOffset"), Key(4)] float DecalOffset,
    [property: JsonPropertyName("p"), Key(5)] Vector3[] Points
)
{
    private readonly int _hashCode = CalculateHashCode(Color, ColNum, PolyType, LineType, DecalOffset, Points);

    public bool Equals(Rad3dPoly other)
    {
        if (!Color.Equals(other.Color)) return false;
        if (ColNum != other.ColNum) return false;
        if (PolyType != other.PolyType) return false;
        if (LineType != other.LineType) return false;
        if (!DecalOffset.Equals(other.DecalOffset)) return false;
        return Points.SequenceEqual(other.Points);
    }

    private static int CalculateHashCode(Color3 color, int? colNum, PolyType polyType, LineType? lineType, float decalOffset, ReadOnlySpan<Vector3> points)
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
        return hashCode.ToHashCode();
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }
}