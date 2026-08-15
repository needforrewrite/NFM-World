using nfm_world_library.Lua;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Lua-facing view over the gamemode's player list. Generic
/// <c>UnlimitedArray&lt;T&gt;</c> constructed types only get opaque Lua stubs,
/// so scripts read players through this wrapper instead.
/// </summary>
[LuaVisible]
public sealed partial class LuaPlayers
{
    private readonly ObservableUnlimitedArray<ClientSidePlayer> _players;

    [LuaHidden]
    public LuaPlayers(ObservableUnlimitedArray<ClientSidePlayer> players)
        => _players = players;

    [LuaName("count")]
    public int Count => _players.Count;

    /// <summary>Returns the player at <paramref name="index"/>, or nil when out of range.</summary>
    [LuaName("get")]
    public ClientSidePlayer? Get(int index)
        => index >= 0 && index < _players.Count ? _players[index] : null;

    /// <summary>Indexer so scripts can write <c>players[0]</c>.</summary>
    public ClientSidePlayer? this[int index]
    {
        get => Get(index);
        set => _players[index] = value ?? throw new ArgumentNullException(nameof(value));
    }
}
