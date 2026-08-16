using System.Text.Json.Serialization;
using MemoryPack;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant), LuaVisible]
public readonly partial record struct Rad3dRimsDef(
    [property: JsonPropertyName("color"), MemoryPackOrder(0), LuaName] Color3 Color,
    [property: JsonPropertyName("size"), MemoryPackOrder(1), LuaName] float Size,
    [property: JsonPropertyName("depth"), MemoryPackOrder(2), LuaName] float Depth
);