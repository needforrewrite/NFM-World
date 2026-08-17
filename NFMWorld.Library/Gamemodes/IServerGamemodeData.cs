using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;

namespace NFMWorldLibrary.Gamemodes;

public interface IServerGamemodeData
{
    /// <summary>The stage being raced on (checkpoints, lap count, geometry).</summary>
    BackendStage CurrentStage { get; }

    /// <summary>
    /// Gets the latest relayed position for a player, or null if not yet received.
    /// Position data flows from <see cref="C2S_PlayerState"/> relay.
    /// </summary>
    f64Vector3? GetPlayerPosition(Guid playerId);
    
    /// <summary>
    /// Broadcast a payload to all clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    void BroadcastEvent(ReadOnlyMemory<byte> payload);
}