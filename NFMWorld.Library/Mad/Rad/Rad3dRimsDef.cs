using System.Text.Json.Serialization;
using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant), PluginVisible]
public readonly partial record struct Rad3dRimsDef(
    [property: JsonPropertyName("color"), MemoryPackOrder(0), PluginVisible] Color3 Color,
    [property: JsonPropertyName("size"), MemoryPackOrder(1), PluginVisible] float Size,
    [property: JsonPropertyName("depth"), MemoryPackOrder(2), PluginVisible] float Depth
);