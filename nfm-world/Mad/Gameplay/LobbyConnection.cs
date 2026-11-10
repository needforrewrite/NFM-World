using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Gameplay;

/// <summary>
/// Opens the lobby phase. Shared by the menu and the <c>connect</c> command so the browser
/// guard and transport initialisation exist in one place.
/// </summary>
public static class LobbyConnection
{
    /// <summary>
    /// Joins the build's configured lobby. <c>connect</c> uses the other overload to reach a
    /// different one.
    /// </summary>
    public static bool TryOpen(out string error)
        => TryOpen(LobbyEndpoint.Host, LobbyEndpoint.Port, LobbyEndpoint.Insecure, out error);

    /// <summary>
    /// Joins a lobby over plain <c>ws://</c>. The session token travels on this connection, so
    /// this is for a lobby on this machine or a trusted network.
    /// </summary>
    public static bool TryOpen(string host, ushort port, out string error)
        => TryOpen(host, port, insecure: true, out error);

    public static bool TryOpen(string host, ushort port, bool insecure, out string error)
    {
        // The browser has no UDP sockets, so the ENet race transport cannot work there.
        if (OperatingSystem.IsBrowser())
        {
            error = "Multiplayer is not available in the browser build.";
            return false;
        }

        ENetMultiplayer.Init();
        GameSparker.PushPhase(new LobbyPhase(new WebSocketMultiplayerClientTransport(host, port, insecure)));
        error = "";
        return true;
    }
}
