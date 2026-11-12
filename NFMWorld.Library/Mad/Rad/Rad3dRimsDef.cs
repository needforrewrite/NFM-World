using System.Text.Json.Serialization;
using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public readonly partial record struct Rad3dRimsDef(
    [property: JsonPropertyName("color"), TightPackOrder(0), PluginVisible]
    Color3 Color,
    [property: JsonPropertyName("size"), TightPackOrder(1), PluginVisible]
    float Size,
    [property: JsonPropertyName("depth"), TightPackOrder(2), PluginVisible]
    float Depth
);
