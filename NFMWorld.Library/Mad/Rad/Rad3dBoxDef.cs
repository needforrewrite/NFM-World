using System.Text.Json.Serialization;
using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant)]
[PluginVisible]
public readonly partial record struct Rad3dBoxDef(
    [property: JsonPropertyName("xy"), MemoryPackOrder(0), PluginVisible] int Xy,
    [property: JsonPropertyName("zy"), MemoryPackOrder(1), PluginVisible] int Zy,
    [property: JsonPropertyName("rad"), MemoryPackOrder(2), PluginVisible] f64Vector3 Radius,
    [property: JsonPropertyName("t"), MemoryPackOrder(3), PluginVisible] f64Vector3 Translation,
    [property: JsonPropertyName("skid"), MemoryPackOrder(4), PluginVisible] SurfaceType SurfaceType,
    [property: JsonPropertyName("damage"), MemoryPackOrder(5), PluginVisible] int Damage,
    [property: JsonPropertyName("notwall"), MemoryPackOrder(6), PluginVisible] bool NotWall,
    [property: JsonPropertyName("c"), MemoryPackOrder(7), PluginVisible] Color3 Color,
    // ReSharper disable once InconsistentNaming
    [property: JsonIgnore, MemoryPackOrder(8)] float _deprecated_TractionMultiplier = 1f,
    [property: JsonPropertyName("gripmul"), MemoryPackOrder(9), PluginVisible] fix64? TractionMultiplier = null
);