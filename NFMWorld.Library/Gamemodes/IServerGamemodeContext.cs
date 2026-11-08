using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Multiplayer.Packets.C2S;

namespace NFMWorldLibrary.Gamemodes;

[PluginVisible(Visible.IncludingAllMembersOfType)]
public interface IServerGamemodeContext
{
    /// <summary>The stage being raced on (checkpoints, lap count, geometry).</summary>
    BackendStage CurrentStage { get; }

    /// <summary>
    /// Gets the latest relayed position for a player, or null if not yet received.
    /// Position data flows from <see cref="C2S_PlayerState"/> relay.
    /// </summary>
    f64Vector3? GetPlayerPosition(Guid playerId);

    /// <summary>
    /// Whether the last relayed state for a player says their car is wasted, or null before the
    /// first state arrives. Wasted comes from the car's owner, the same relay as its position.
    /// </summary>
    bool? IsPlayerWasted(Guid playerId);
    
    /// <summary>
    /// Broadcast a payload to all clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    void BroadcastEvent(ReadOnlyMemory<byte> payload);
}