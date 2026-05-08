using System.Text.Json.Serialization;
using MessagePack;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Mad.Rad;

[MessagePackObject]
public readonly record struct Rad3dBoxDef(
    [property: JsonPropertyName("xy"), Key(0)] int Xy,
    [property: JsonPropertyName("zy"), Key(1)] int Zy,
    [property: JsonPropertyName("rad"), Key(2)] f64Vector3 Radius,
    [property: JsonPropertyName("t"), Key(3)] f64Vector3 Translation,
    [property: JsonPropertyName("skid"), Key(4)] int Skid,
    [property: JsonPropertyName("damage"), Key(5)] int Damage,
    [property: JsonPropertyName("notwall"), Key(6)] bool NotWall,
    [property: JsonPropertyName("c"), Key(7)] Color3 Color
);