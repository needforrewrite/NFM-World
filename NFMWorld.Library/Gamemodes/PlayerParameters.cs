using Lua;

namespace NFMWorldLibrary.Gamemodes;

[LuaObject]
public partial class PlayerParameters
{
    [LuaMember("player_name")]
    public required string PlayerName { get; init; } = "Player";
    
    [LuaMember("car_name")]
    public required string CarName { get; init; } = "nfmm/radicalone";
    
    [LuaMember("color")]
    public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    
    [LuaMember("is_bot")]
    public required bool IsBot { get; init; } = false;
    
    [LuaMember("is_client_player")]
    public required bool IsClientPlayer { get; init; } = false;
    // team, isbot, etc
}