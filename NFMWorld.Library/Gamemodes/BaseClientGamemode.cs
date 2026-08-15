using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sfx;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.Gamemodes;

public abstract class BaseClientGamemode : IGamemode
{
    protected internal GamemodeParameters GamemodeParameters { get; }
    protected internal IGamemodeData GamemodeData { get; }

    public ObservableUnlimitedArray<ClientSidePlayer> Players { get; }
    public BackendStage CurrentStage => GamemodeData.CurrentStage;
    public int NumPlayers => Players.Count;
    
    /// <summary>
    /// Returns the race results if the race has finished, or null otherwise.
    /// </summary>
    public RaceResults? Results { get; protected internal set; }

    /// <summary>Per-frame HUD state for the client player.</summary>
    public HudStateData HudState { get; protected internal set; } = new();
    
    public ClientSidePlayer ClientPlayer { get; }

    protected BaseClientGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData)
    {
        GamemodeParameters = gamemodeParameters;
        GamemodeData = gamemodeData;
        Players = new ObservableUnlimitedArray<ClientSidePlayer>();
        foreach (var (parameters, idx) in gamemodeParameters.Players.Select((p, idx) => (p, idx)))
            Players.Add(new ClientSidePlayer(parameters, idx));
        ClientPlayer = Players.Single(p => p.Parameters.IsClientPlayer);
    }

    /// <summary>
    /// Called when an event is received from the server.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    public virtual void OnServerEvent(ReadOnlySpan<byte> payload) { }
    
    /// <summary>
    /// Called when the race is finished server-side.
    /// </summary>
    public virtual void OnServerRaceFinished(RaceResults results) { }

    /// <inheritdoc />
    public virtual RaceResults? GetResults() => Results;

    /// <inheritdoc />
    public virtual void SetServerResults(RaceResults results)
    {
        Results = results;
        OnServerRaceFinished(results);
    }

    /// <inheritdoc />
    public virtual void SetEventSender(Action<ReadOnlyMemory<byte>> sendToServer)
        => _sendToServer = sendToServer;

    /// <summary>
    /// Sends a gamemode-specific event to the server using the sender injected
    /// by the host, falling back to <see cref="IGamemodeData.SendServerEvent"/>.
    /// </summary>
    protected void SendServerEvent(ReadOnlySpan<byte> payload)
    {
        if (_sendToServer is { } send)
            send(payload.ToArray());
        else
            GamemodeData.SendServerEvent(payload);
    }

    private Action<ReadOnlyMemory<byte>>? _sendToServer;

    /// <summary>
    /// Called to awake the gamemode.
    /// </summary>
    public virtual void Begin()
    {

    }

    /// <summary>
    /// Called to deinitialize the gamemode.
    /// </summary>
    public virtual void End()
    {

    }

    /// <summary>
    /// Called every game tick to update the gamemode as long as all players have loaded.
    /// </summary>
    public virtual void GameTick()
    {
    }

    /// <summary>
    /// User-callable reset function that can be used to reset the gamemode state.
    /// </summary>
    public virtual void Reset()
    {
    }

    /// <summary>
    /// Invoked when a key is pressed.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="keys">The state of all keys.</param>
    public virtual void KeyPressed(Key key, in Keys keys)
    {
    }

    /// <summary>
    /// Invoked when a key is released.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="keys">The state of all keys.</param>
    public virtual void KeyReleased(Key key, in Keys keys)
    {
    }

    /// <summary>
    /// Invoked when a key character is typed.
    /// </summary>
    /// <param name="character">The character that was typed.</param>
    public virtual void KeyTyped(char character)
    {
    }

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
    public virtual void MousePressed(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

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
    public virtual void MouseReleased(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

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
    public virtual void MouseScrolled(int x, int y, int delta, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    /// <summary>
    /// Invoked when the mouse is moved.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MouseMoved(int x, int y, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    public virtual void Render()
    {
    }

    private int _lastClientCheckpoint = 0;
    private int _lastCountdownTime = 0;
    private fix64 _lcarx;
    private fix64 _lcarz;

    /// <summary>
    /// Convenience function to reset HUD state, visual FX and sounds.
    /// </summary>
    protected virtual void ClientReset()
    {
        GamemodeData.ClientCallbacks.ResetCheckpointGlow();

        HudState = new HudStateData { Lap = 1, TotalLaps = CurrentStage.nlaps };
        IBackend.Backend.StopAllSounds();
    }

    /// <summary>
    /// Convenience function to update HUD state and play sounds based on the given car's state.
    /// </summary>
    /// <param name="car">The client car</param>
    protected virtual void UpdateHudAndSounds(IInGameCar car)
    {
        var diffx = (float)(car.Position.X - _lcarx);
        var diffz  = (float)(car.Position.Z - _lcarz);
        _lcarx = car.Position.X;
        _lcarz = car.Position.Z;

        HudState.Speed = MathF.Sqrt(diffx * diffx + diffz * diffz);
        HudState.Lap = car.CurrentLap + 1;
        HudState.Damage = (float)car.CarPhysics.DamagePoints / ClientPlayer.Car?.Stats.Maxmag ?? 100;
        HudState.Power = (float)car.CarPhysics.Power / 100f;

        if (car.CurrentCheckpoint != _lastClientCheckpoint)
        {
            _lastClientCheckpoint = car.CurrentCheckpoint;
            SfxLibrary.checkpoint?.Play();
        }

        GamemodeData.ClientCallbacks.UpdateCheckpointGlow(
            car.CurrentCheckpoint,
            car.CurrentCheckpoint == CurrentStage.checkpoints.Count - 1 && car.CurrentLap == CurrentStage.nlaps - 1
        );
    }

    /// <summary>
    /// Updates the countdown timer and plays the corresponding sound.
    /// </summary>
    /// <param name="countdownTime">The current countdown time.</param>
    protected virtual void UpdateCountdown(int countdownTime)
    {
        if (countdownTime != _lastCountdownTime)
        {
            _lastCountdownTime = countdownTime;
            SfxLibrary.countdown[countdownTime].Play();
        }

        HudState.CountdownTimer = countdownTime;
    }
}