using NFMWorld.PluginSystem;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend;

/// <summary>
/// Mutable data for the gamemode.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public partial interface IGamemodeContext
{
    BackendStage CurrentStage { get; }
    RaceState RaceState { get; }
    IClientCallbacks ClientCallbacks { get; }

    /// <summary>
    /// Sends an event to the server.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    /// <param name="payload"></param>
    void SendServerEvent(ReadOnlySpan<byte> payload);

    // TODO this maybe should be removed
    void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players);
}