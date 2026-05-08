using System.Text.Json.Serialization;
using MessagePack;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Rad;

[MessagePackObject]
public readonly record struct Rad3dWheelDef(
    [property: JsonPropertyName("pos"), Key(0)] f64Vector3 Position,
    [property: JsonPropertyName("rotates"), Key(1)] int Rotates,
    [property: JsonPropertyName("w"), Key(2)] fix64 Width,
    [property: JsonPropertyName("h"), Key(3)] fix64 Height
)
{
    [IgnoreMember]
    public int Sparkat { get; } = (int) fix64.Round((Height / (fix64)10f) * (fix64)24.0F);
    [IgnoreMember]
    public int Ground { get; } = (int) fix64.Round(Position.Y + (fix64)13.0F * (Height / (fix64)10f));
}