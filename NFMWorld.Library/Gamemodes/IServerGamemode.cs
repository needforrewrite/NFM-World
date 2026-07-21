using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Server-side gamemode contract. Runs on the Game Master / Worker,
/// receives client events, manages authoritative game state.
///
/// Unlike <see cref="IGamemode"/>, this does NOT involve rendering,
/// input handling, or physics simulation. It validates events and
/// drives the game state machine.
/// </summary>
public interface IServerGamemode
{
    /// <summary>Gamemode identifier (e.g., "nfmm/pvp-racing").</summary>
    string GamemodeId { get; }

    /// <summary>Called once when the race session is created.</summary>
    void Begin(IServerGamemodeContext context);

    /// <summary>
    /// Called when all players have loaded and the race is about to start.
    /// Fires at the same moment <see cref="S2C_RaceCanStart"/> is broadcast,
    /// so the server countdown is synchronized with client countdowns.
    /// </summary>
    void StartRace();

    /// <summary>Called when the race session is disposed.</summary>
    void End();

    /// <summary>Called every server tick (~20 TPS) to advance game state.</summary>
    void GameTick();

    /// <summary>
    /// Called when a <see cref="C2S_ClientEvent"/> is received from a client.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Returns the current authoritative game state for broadcasting to clients.
    /// Return null if nothing has changed since the last call.
    /// </summary>
    GameStateSnapshot? GetStateSnapshot();

    /// <summary>
    /// Called by the host to inject a broadcast callback.
    /// The server gamemode calls this to send events to all clients.
    /// The payload should be a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    void SetEventBroadcaster(Action<ReadOnlyMemory<byte>> broadcast);
}
