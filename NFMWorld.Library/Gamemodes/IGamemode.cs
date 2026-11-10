using NFMWorld.DriverInterface;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Client-side gamemode contract. Implemented by gamemodes that drive
/// gameplay on the client (local physics, HUD, input) in both singleplayer
/// and multiplayer. Server-side logic lives in <see cref="IServerGamemode"/>.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public interface IGamemode
{
    /// <summary>Gamemode identifier (e.g., "nfmm/pvp").</summary>
    string GamemodeId { get; }

    /// <summary>Called once when the gamemode is created.</summary>
    void Begin();

    /// <summary>Called when the gamemode is torn down.</summary>
    void End();

    /// <summary>Called every game tick to advance the gamemode.</summary>
    void GameTick();

    /// <summary>User-callable reset used to (re)start the gamemode.</summary>
    void Reset();

    /// <summary>
    /// Called when the player leaves the race without finishing, so the gamemode can tell the
    /// host. The race keeps running for everyone still in it.
    /// </summary>
    void Forfeit();

    /// <summary>
    /// Called by the host when authoritative server results arrive
    /// (<c>S2C_GameFinished</c>).
    /// </summary>
    void SetServerResults(RaceResults results);

    /// <summary>
    /// Called when an <c>S2C_ServerEvent</c> is received from the server.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    void OnServerEvent(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Gets the player this client drives, and whose car it owns. Null when the client is
    /// watching a racing session it has no car in - nothing in that roster belongs to it.
    /// </summary>
    ClientSidePlayer? ClientPlayer { get; }

    // ── Client-only members ───────────────────────────────────────────

    void KeyPressed(Key key, in Keys keys);

    void KeyReleased(Key key, in Keys keys);

    void KeyTyped(char character);

    void MouseMoved(int x, int y, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    void MousePressed(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    void MouseReleased(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    void MouseScrolled(int x, int y, int delta, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    void Render();
}
