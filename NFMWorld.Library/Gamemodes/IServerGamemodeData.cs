using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;

namespace NFMWorldLibrary.Gamemodes;

public interface IServerGamemodeData
{
    /// <summary>The stage being raced on (checkpoints, lap count, geometry).</summary>
    BackendStage CurrentStage { get; }

    /// <summary>Ordered list of player IDs in this race.</summary>
    IReadOnlyList<Guid> PlayerIds { get; }

    /// <summary>Map of player index → player info (names, vehicles, etc.).</summary>
    IReadOnlyDictionary<byte, PlayerInfo> PlayerInfos { get; }

    /// <summary>
    /// Gets the latest relayed position for a player, or null if not yet received.
    /// Position data flows from <see cref="C2S_PlayerState"/> relay.
    /// </summary>
    f64Vector3? GetPlayerPosition(Guid playerId);
    
    void BroadcastEvent(ReadOnlyMemory<byte> payload);
}
