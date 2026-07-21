using NFMWorld.DriverInterface;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.Gamemodes;

public interface IGamemode
{
    public IReadOnlyList<PlayerParameters> Players { get; }
    public UnlimitedArray<IInGameCar> CarsInRace { get; }
    public BackendStage CurrentStage { get; }
    public int NumPlayers { get; }

    public void Begin();
    public void End();
    public void GameTick();
    public void Reset();

    /// <summary>
    /// Returns the race results if the race has finished, or null otherwise.
    /// Fired by <see cref="BaseRacePhase.RaceFinished"/> when the race ends.
    /// </summary>
    public RaceResults? GetResults();

    /// <summary>
    /// Called by the host when <see cref="S2C_GameFinished"/> is received
    /// with authoritative server results. Default is a no-op for singleplayer.
    /// </summary>
    public void SetServerResults(RaceResults results);

    /// <summary>
    /// Called when a <see cref="S2C_ServerEvent"/> is received from the server.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// Default is a no-op for singleplayer gamemodes.
    /// </summary>
    public void OnServerEvent(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Called by the host to inject a callback for sending events to the server.
    /// The payload should be a MemoryPack-serialized gamemode-specific event.
    /// Default is a no-op for singleplayer gamemodes.
    /// </summary>
    public void SetEventSender(Action<ReadOnlyMemory<byte>> sendToServer);

    #region Client

    /// <summary>
    /// Invoked when a key is pressed.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="keys">The state of all keys.</param>
    [ClientOnly]
    public void KeyPressed(Key key, in Keys keys);

    /// <summary>
    /// Invoked when a key is released.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="keys">The state of all keys.</param>
    [ClientOnly]
    public void KeyReleased(Key key, in Keys keys);

    /// <summary>
    /// Invoked when a key character is typed.
    /// </summary>
    /// <param name="character">The character that was typed.</param>
    [ClientOnly]
    public void KeyTyped(char character);

    /// <summary>
    /// Invoked when the mouse is moved.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    [ClientOnly]
    public void MouseMoved(int x, int y, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    /// <summary>
    /// Invoked when a mouse button is pressed.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="button">The button that was pressed.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    [ClientOnly]
    public void MousePressed(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    /// <summary>
    /// Invoked when a mouse button is released.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="button">The button that was released.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    [ClientOnly]
    public void MouseReleased(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    /// <summary>
    /// Invoked when a mouse button is released.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="delta">The delta Y change.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    [ClientOnly]
    public void MouseScrolled(int x, int y, int delta, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey);

    [ClientOnly]
    public void Render();

    #endregion
}