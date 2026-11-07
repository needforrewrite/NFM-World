using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Gameplay;

/// <summary>
/// Opens the lobby phase. Shared by the menu and the <c>connect</c> command so the browser
/// guard and ENet initialisation exist in one place.
/// </summary>
public static class LobbyConnection
{
    /// <summary>Joins a lobby, or returns false with a reason when this build cannot.</summary>
    public static bool TryOpen(string host, ushort port, out string error)
    {
        // ENet is a native library with no wasm build, so the browser cannot join at all.
        if (OperatingSystem.IsBrowser())
        {
            error = "Multiplayer is not available in the browser build.";
            return false;
        }

        ENetMultiplayer.Init();
        GameSparker.PushPhase(new LobbyPhase(new WebSocketMultiplayerClientTransport(host, port)));
        error = "";
        return true;
    }
}
