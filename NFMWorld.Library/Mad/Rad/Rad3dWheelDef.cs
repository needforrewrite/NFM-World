using System.Text.Json.Serialization;
using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant), PluginVisible]
public readonly partial record struct Rad3dWheelDef(
    [property: JsonPropertyName("pos"), MemoryPackOrder(0), PluginVisible] f64Vector3 Position,
    [property: JsonPropertyName("rotates"), MemoryPackOrder(1), PluginVisible] int Rotates,
    [property: JsonPropertyName("w"), MemoryPackOrder(2), PluginVisible] fix64 Width,
    [property: JsonPropertyName("h"), MemoryPackOrder(3), PluginVisible] fix64 Height,
    [property: JsonPropertyName("polys"), MemoryPackOrder(4)] Rad3dPoly[]? Polys
)
{
    [MemoryPackIgnore]
    public int Sparkat { get; } = (int) fix64.Round((Height / (fix64)10f) * (fix64)24.0F);
    [MemoryPackIgnore]
    public int Ground { get; } = (int) fix64.Round(Position.Y + (fix64)13.0F * (Height / (fix64)10f) + 3);
}