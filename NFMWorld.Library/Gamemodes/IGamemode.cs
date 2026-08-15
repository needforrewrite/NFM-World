using NFMWorld.DriverInterface;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Client-side gamemode contract. Implemented by gamemodes that drive
/// gameplay on the client (local physics, HUD, input) in both singleplayer
/// and multiplayer. Server-side logic lives in <see cref="IServerGamemode"/>.
/// </summary>
public interface IGamemode
{
    /// <summary>Called once when the gamemode is created.</summary>
    void Begin();

    /// <summary>Called when the gamemode is torn down.</summary>
    void End();

    /// <summary>Called every game tick to advance the gamemode.</summary>
    void GameTick();

    /// <summary>User-callable reset used to (re)start the gamemode.</summary>
    void Reset();

    /// <summary>Returns the race results if the race has finished, or null otherwise.</summary>
    RaceResults? GetResults();

    /// <summary>
    /// Called by the host when authoritative server results arrive
    /// (<c>S2C_GameFinished</c>). Default is a no-op for singleplayer.
    /// </summary>
    void SetServerResults(RaceResults results);

    /// <summary>
    /// Called when an <c>S2C_ServerEvent</c> is received from the server.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    void OnServerEvent(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Called by the host to inject a callback for sending events to the server.
    /// The payload should be a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    void SetEventSender(Action<ReadOnlyMemory<byte>> sendToServer);

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
