using Lua;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Gamemodes;

[LuaVisible]
public partial class PlayerParameters
{
    [LuaName("playerName")]
    public required string PlayerName { get; init; } = "Player";
    
    [LuaName("carName")]
    public required string CarName { get; init; } = "nfmm/radicalone";
    
    [LuaName("color")]
    public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    
    [LuaName("isBot")]
    public required bool IsBot { get; init; } = false;
    
    [LuaName("isClientPlayer")]
    public required bool IsClientPlayer { get; init; } = false;
    // team, isbot, etc
}