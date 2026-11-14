using System.Net;
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
    /// Joins the build's configured lobby. <c>connect</c> calls the overload with the host and
    /// scheme to reach a different one.
    /// </summary>
    public static bool TryOpen(out string error)
        => TryOpen(LobbyEndpoint.Host, LobbyEndpoint.Port, LobbyEndpoint.Insecure, out error);

    /// <summary>Loopback is the only host assumed to speak plain <c>ws://</c>.</summary>
    public static bool IsLocal(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);

    public static bool TryOpen(string host, ushort port, bool insecure, out string error)
    {
        // The browser has no UDP sockets, so the ENet race transport cannot work there.
        if (OperatingSystem.IsBrowser())
        {
            error = "Multiplayer is not available in the browser build.";
            return false;
        }

        if (!GameSparker.AccountManager.LoggedIn)
        {
            if (GameSparker.CurrentPhase is MainMenuPhase menu)
            {
                menu.RequireLobbySignIn(host, port, insecure);
                error = "";
                return true;
            }

            error = "Sign in from the main menu before opening multiplayer.";
            return false;
        }

        ENetMultiplayer.Init();
        GameSparker.PushPhase(new LobbyPhase(new WebSocketMultiplayerClientTransport(host, port, insecure)));
        error = "";
        return true;
    }
}
