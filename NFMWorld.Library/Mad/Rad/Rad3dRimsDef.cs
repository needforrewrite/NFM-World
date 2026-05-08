using System.Text.Json.Serialization;
using MessagePack;

namespace NFMWorldLibrary.Rad;

[MessagePackObject]
public readonly record struct Rad3dRimsDef(
    [property: JsonPropertyName("color"), Key(0)] Color3 Color,
    [property: JsonPropertyName("size"), Key(1)] float Size,
    [property: JsonPropertyName("depth"), Key(2)] float Depth
);