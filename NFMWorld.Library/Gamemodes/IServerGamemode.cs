namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Server-side gamemode contract. Runs on the Game Master / Worker,
/// receives client events, manages authoritative game state.
///
/// Unlike <see cref="IGamemode"/>, this does NOT involve rendering,
/// input handling, or physics simulation. It validates events and
/// drives the game state machine. In the single-path model it also runs
/// in-process on singleplayer via the local race host.
/// </summary>
public interface IServerGamemode
{
    /// <summary>Gamemode identifier (e.g., "nfmm/racing").</summary>
    string GamemodeId { get; }

    /// <summary>
    /// Called once when the race session is created, with the server-side
    /// data context the gamemode can read and broadcast through.
    /// </summary>
    void Begin(IServerGamemodeData data);

    /// <summary>
    /// Called when all players have loaded and the race is about to start.
    /// </summary>
    void StartRace();

    /// <summary>Called when the race session is disposed.</summary>
    void End();

    /// <summary>Called every server tick (~20 TPS) to advance game state.</summary>
    void GameTick();

    /// <summary>
    /// Called when a client event is received from a client.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Returns the current authoritative game state for broadcasting to clients.
    /// Return null if nothing has changed since the last call.
    /// </summary>
    GameStateSnapshot? GetStateSnapshot();
}
