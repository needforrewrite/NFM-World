using System.Text.Json.Serialization;
using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[TightPackable(GenerateType.VersionTolerant), TightPackAllowSerialize]
[PluginVisible]
public readonly partial record struct Rad3dBoxDef(
    [property: JsonPropertyName("xy"), TightPackOrder(0), PluginVisible]
    int Xy,
    [property: JsonPropertyName("zy"), TightPackOrder(1), PluginVisible]
    int Zy,
    [property: JsonPropertyName("rad"), TightPackOrder(2), PluginVisible]
    f64Vector3 Radius,
    [property: JsonPropertyName("t"), TightPackOrder(3), PluginVisible]
    f64Vector3 Translation,
    [property: JsonPropertyName("skid"), TightPackOrder(4), PluginVisible]
    SurfaceType SurfaceType,
    [property: JsonPropertyName("damage"), TightPackOrder(5), PluginVisible]
    int Damage,
    [property: JsonPropertyName("notwall"), TightPackOrder(6), PluginVisible]
    bool NotWall,
    [property: JsonPropertyName("c"), TightPackOrder(7), PluginVisible]
    Color3 Color,
    // ReSharper disable once InconsistentNaming
    [property: JsonIgnore, TightPackOrder(8)]
    float _deprecated_TractionMultiplier = 1f,
    [property: JsonPropertyName("gripmul"), TightPackOrder(9), PluginVisible]
    fix64? TractionMultiplier = null
);
