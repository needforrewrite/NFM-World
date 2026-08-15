using nfm_world_library.Lua;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Lua-facing view over <see cref="IServerGamemodeData"/>. Player IDs are
/// exposed as strings since Lua has no Guid type.
/// </summary>
[LuaVisible]
public sealed partial class LuaServerData
{
    private readonly IServerGamemodeData _data;

    [LuaHidden]
    public LuaServerData(IServerGamemodeData data)
        => _data = data;

    [LuaName("playerCount")]
    public int PlayerCount => _data.PlayerIds.Count;

    [LuaName("playerId")]
    public string? PlayerId(int index)
        => index >= 0 && index < _data.PlayerIds.Count ? _data.PlayerIds[index].ToString() : null;

    [LuaName("playerName")]
    public string? PlayerName(int index)
        => _data.PlayerInfos.TryGetValue((byte)index, out var info) ? info.Name : null;

    [LuaName("playerVehicle")]
    public string? PlayerVehicle(int index)
        => _data.PlayerInfos.TryGetValue((byte)index, out var info) ? info.Vehicle : null;

    [LuaName("playerPosition")]
    public f64Vector3? PlayerPosition(string playerId)
        => Guid.TryParse(playerId, out var guid) ? _data.GetPlayerPosition(guid) : null;
}
