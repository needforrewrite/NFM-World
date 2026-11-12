using System.Text.Json.Serialization;
using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[TightPackable(GenerateType.VersionTolerant), TightPackAllowSerialize, PluginVisible]
public readonly partial record struct Rad3dWheelDef(
    [property: JsonPropertyName("pos"), TightPackOrder(0), PluginVisible]
    f64Vector3 Position,
    [property: JsonPropertyName("rotates"), TightPackOrder(1), PluginVisible]
    int Rotates,
    [property: JsonPropertyName("w"), TightPackOrder(2), PluginVisible]
    fix64 Width,
    [property: JsonPropertyName("h"), TightPackOrder(3), PluginVisible]
    fix64 Height,
    [property: JsonPropertyName("polys"), TightPackOrder(4)]
    Rad3dPoly[]? Polys
)
{
    [TightPackIgnore] public int Sparkat { get; } = (int)fix64.Round((Height / (fix64)10f) * (fix64)24.0F);
    [TightPackIgnore] public int Ground { get; } = (int)fix64.Round(Position.Y + (fix64)13.0F * (Height / (fix64)10f) + 3);
}
