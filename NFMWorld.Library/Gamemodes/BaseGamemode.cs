using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sfx;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.Gamemodes;

public abstract class BaseGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData) : IGamemode
{
    protected readonly IGamemodeData GamemodeData = gamemodeData;

    public IReadOnlyList<PlayerParameters> Players => gamemodeParameters.Players;
    public UnlimitedArray<IInGameCar> CarsInRace => GamemodeData.CarsInRace;
    public BackendStage CurrentStage => GamemodeData.CurrentStage;
    public int NumPlayers => Players.Count;

    /// <summary>Per-frame HUD state pushed to the CEF overlay.</summary>
    public HudStateData HudState { get; protected set; } = new();

    /// <inheritdoc />
    public virtual RaceResults? GetResults() => null;

    /// <inheritdoc />
    public virtual void SetServerResults(RaceResults results) { }

    /// <inheritdoc />
    public virtual void OnServerEvent(ReadOnlySpan<byte> payload) { }

    /// <inheritdoc />
    public virtual void SetEventSender(Action<ReadOnlyMemory<byte>> sendToServer)
    {
        _sendToServer = sendToServer;
    }

    /// <summary>Send an event to the server. No-op if no sender is configured.</summary>
    protected void SendToServer(ReadOnlyMemory<byte> payload)
        => _sendToServer?.Invoke(payload);

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
        // HUD rendering moved to CEF — no per-tick HUD updates needed.
    }

    /// <summary>
    /// User-callable reset function that can be used to reset the gamemode state.
    /// </summary>
    public virtual void Reset()
    {

    }

    public virtual void KeyPressed(Key key, in Keys keys)
    {
    }

    public virtual void KeyReleased(Key key, in Keys keys)
    {
    }

    public virtual void KeyTyped(char character)
    {
    }

    public virtual void MousePressed(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    public virtual void MouseReleased(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    public virtual void MouseScrolled(int x, int y, int delta, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    public virtual void MouseMoved(int x, int y, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    public virtual void Render()
    {
    }

    [ClientOnly]
    private int _lastClientCheckpoint = 0;

    [ClientOnly]
    private int _lastCountdownTime = 0;

    [ClientOnly]
    private fix64 _lcarx;

    [ClientOnly]
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
        HudState.Damage = (float)car.CarPhysics.DamagePoints / CarsInRace[0].Stats.Maxmag;
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