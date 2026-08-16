using System.Text.Json.Serialization;
using Lua;
using MemoryPack;
using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant)]
[LuaVisible]
public readonly partial record struct Rad3dBoxDef(
    [property: JsonPropertyName("xy"), MemoryPackOrder(0), LuaName("xy")] int Xy,
    [property: JsonPropertyName("zy"), MemoryPackOrder(1), LuaName("zy")] int Zy,
    [property: JsonPropertyName("rad"), MemoryPackOrder(2), LuaName("radius")] f64Vector3 Radius,
    [property: JsonPropertyName("t"), MemoryPackOrder(3), LuaName("translation")] f64Vector3 Translation,
    [property: JsonPropertyName("skid"), MemoryPackOrder(4), LuaName("surfaceType")] SurfaceType SurfaceType,
    [property: JsonPropertyName("damage"), MemoryPackOrder(5), LuaName("damage")] int Damage,
    [property: JsonPropertyName("notwall"), MemoryPackOrder(6), LuaName("notWall")] bool NotWall,
    [property: JsonPropertyName("c"), MemoryPackOrder(7), LuaName("color")] Color3 Color,
    // ReSharper disable once InconsistentNaming
    [property: JsonIgnore, MemoryPackOrder(8)] float _deprecated_TractionMultiplier = 1f,
    [property: JsonPropertyName("gripmul"), MemoryPackOrder(9), LuaName("tractionMultiplier")] fix64? TractionMultiplier = null
);