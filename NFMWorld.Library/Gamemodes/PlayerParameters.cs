using Lua;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Gamemodes;

[LuaVisible]
public partial class PlayerParameters
{
    [LuaName("player_name")]
    public required string PlayerName { get; init; } = "Player";
    
    [LuaName("car_name")]
    public required string CarName { get; init; } = "nfmm/radicalone";
    
    [LuaName("color")]
    public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    
    [LuaName("is_bot")]
    public required bool IsBot { get; init; } = false;
    
    [LuaName("is_client_player")]
    public required bool IsClientPlayer { get; init; } = false;
    // team, isbot, etc
}