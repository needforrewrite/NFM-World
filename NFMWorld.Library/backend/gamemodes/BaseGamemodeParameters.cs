using nfm_world_library.Lua;

namespace nfm_world_library.backend.gamemodes;

[LuaVisible]
public class BaseGamemodeParameters
{
    public int PlayerCarIndex { get; init; }
    public IReadOnlyList<PlayerParameters> Players { get; init; }
}

public class PlayerParameters
{
    public string PlayerName { get; init; } = "Player";
    public string CarName { get; init; } = "nfmm/radicalone";
    public Color3 Color { get; init; } = new Color3(255, 0, 0);
    public bool IsBot { get; init; } = false;
    // team, isbot, etc
}