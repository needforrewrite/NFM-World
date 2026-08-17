using nfm_world_library.Lua;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

/// <summary>
/// Mutable data for the gamemode.
/// </summary>
[LuaVisible]
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

    void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players);
}