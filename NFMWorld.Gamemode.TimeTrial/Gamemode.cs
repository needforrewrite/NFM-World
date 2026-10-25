using System.Diagnostics;
using JetBrains.Annotations;
using MemoryPack;
using NFMWorld.DriverInterface;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Helpers;

[assembly: Gamemode(
    Name = "Time Trial",
    Description = "Race a time trial against ghosts."
)]

namespace NFMWorld.Gamemode.TimeTrial;

internal enum TimeTrialState
{
    NotStarted,
    Countdown,
    InProgress,
    Finished
}

/// <summary>
/// Client-side time trial. Drives the player car, replays the saved best run as a
/// translucent ghost, and records the run so it can be saved if it sets a new best.
/// </summary>
[UsedImplicitly]
public class TimeTrialGamemode : PluginGamemode
{
    private const int PlayerCarIndex = 0;
    private const int GhostCarIndex = 1;

    private readonly Stopwatch _timer = new();
    private readonly bool _simulation;

    private TimeTrialState _state;
    private int _countdown;
    private int _inner;
    private int _tick;
    private bool _written;

    private SavedTimeTrial? _best;
    private SavedTimeTrial? _current;

    public TimeTrialGamemode(
        ClientGamemodeParameters clientGamemodeParameters,
        IGamemodeContext gamemodeContext,
        string gamemodeId,
        Dictionary<string, object>? config = null)
        : base(clientGamemodeParameters, gamemodeContext, gamemodeId, config)
    {
        _simulation = Config is not null && Config.TryGetValue("simulation", out var value) && value is true;
    }

    public override void Begin()
    {
        base.Begin();
        _state = TimeTrialState.NotStarted;
        HudState.Lap = 1;
        HudState.TotalLaps = CurrentStage.Nlaps;
        HudState.Position = 1;
        HudState.TotalRacers = 1;
        HudState.StateText = "";
    }

    public override void Reset()
    {
        base.Reset();

        _tick = 0;
        _written = false;
        _timer.Reset();

        if (_simulation)
        {
            _state = TimeTrialState.InProgress;
            _timer.Start();
        }
        else
        {
            _state = TimeTrialState.Countdown;
            _countdown = 3;
            _inner = GamemodeUtils.CountdownInterval;
        }

        RemoveFakePlayers();
        SetupPlayer();
        SetupGhost();
    }

    public override void GameTick()
    {
        base.GameTick();

        switch (_state)
        {
            case TimeTrialState.NotStarted:
                Reset();
                break;
            case TimeTrialState.Countdown:
                TickCountdown();
                break;
            case TimeTrialState.InProgress:
                TickInProgress();
                break;
            case TimeTrialState.Finished:
                TickFinished();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);
        if (key == Key.R)
            Reset();
    }

    private void TickCountdown()
    {
        _inner--;
        HudState.CountdownTimer = _countdown;

        if (_inner > 0)
            return;

        _inner = GamemodeUtils.CountdownInterval;
        _countdown--;
        if (_countdown > 0)
            return;

        HudState.CountdownTimer = 0;
        _state = TimeTrialState.InProgress;
        _timer.Start();
    }

    private void TickInProgress()
    {
        if (Players[PlayerCarIndex].Car is { } car)
        {
            FixHoopHelper.HandleFixHoops(CurrentStage, car);
            CheckPointHelper.HandleCheckPoint(CurrentStage, car);

            car.Drive(CurrentStage);

            UpdateHudAndSounds(car);
            HudState.Lap = car.CurrentLap + 1;
            HudState.LapTime = (int)_timer.Elapsed.TotalMilliseconds;

            _current?.RecordTick(car);
            ApplyGhost();

            if (car.CurrentLap >= CurrentStage.Nlaps)
            {
                _state = TimeTrialState.Finished;
                car.CarPhysics.Halted = true;
                SendServerEvent<ClientToServerEvent>(new FinishedEvent());
                _timer.Stop();
            }
        }

        _tick++;
    }

    private void TickFinished()
    {
        if (Players[PlayerCarIndex].Car is { } car)
            car.Drive(CurrentStage);

        if (_written)
            return;

        _written = true;
        SaveIfBest();
        RenderFinishedText();
    }

    /// <summary>
    /// Creates the player's car and opens the best-run and current-run recordings.
    /// </summary>
    private void SetupPlayer()
    {
        var player = Players[PlayerCarIndex];
        var car = new BackendCar(player.Info, PlayerCarIndex, 0, 0);
        player.Car = car;

        car.CurrentCheckpoint = 0;
        car.CurrentLap = 0;

        HudState.Lap = 1;
        HudState.TotalLaps = CurrentStage.Nlaps;
        HudState.StateText = "";
        ClientCallbacks.ResetCheckpointGlow();

        if (_simulation)
            return;

        _best = SavedTimeTrial.Load(car.Player.CarName, CurrentStage.Path);
        _current = new SavedTimeTrial(car.Player.CarName, CurrentStage.Path, CurrentStage.StageLoader, car.Rad);
    }

    /// <summary>
    /// Adds a translucent copy of the player's car that replays the saved best run.
    /// </summary>
    private void SetupGhost()
    {
        if (_simulation || _best is null || Players[PlayerCarIndex].Car is not { } source)
            return;

        var ghostIndex = Players.Count;
        var ghostCar = new BackendCar(source, ghostIndex, false) { CurrentLap = 0 };
        Players.Add(new ClientSidePlayer(source.Player, ghostIndex, isFake: true) { Car = ghostCar });

        ClientCallbacks.GetClientCarCallbacks(ghostCar).AlphaOverride = 0.5f;
    }

    private void ApplyGhost()
    {
        if (_best is null || Players.Count <= GhostCarIndex)
            return;

        if (Players[GhostCarIndex].Car is not { } ghostCar)
            return;

        if (_best.GetTick(_tick) is { } controls)
            ghostCar.Control.Decode(controls);

        ghostCar.Drive(CurrentStage);
    }

    /// <summary>
    /// Saves the current run when it beats the previous best (or there is none).
    /// </summary>
    private void SaveIfBest()
    {
        if (_current is null)
            return;

        if (_best is null ||
            (_current.Splits.SplitTimes.Count > 0 &&
             _current.GetSplitDiff(_best, _current.Splits.SplitTimes.Count - 1) < 0))
        {
            _current.Save();
        }
    }

    private void RenderFinishedText()
    {
        var totalMs = (long)_timer.Elapsed.TotalMilliseconds;

        HudState.StateText =
            $"Finished! Time: {totalMs / 60000:D2}:{totalMs % 60000 / 1000:D2}.{totalMs % 1000:D3}\nPress R to restart";
        HudState.StateTextEndsAt = null;
    }

    private void RemoveFakePlayers()
    {
        for (var i = Players.Count - 1; i >= 0; i--)
        {
            if (Players[i].IsFake)
                Players.RemoveAt(i);
        }
    }
}

/// <summary>
/// Server-side time trial. The client reports the finished race and the server
/// ends it, crediting the sender with the single standing.
/// </summary>
[UsedImplicitly]
public class TimeTrialServerGamemode(
    ServerGamemodeParameters parameters,
    IServerGamemodeContext context,
    string gamemodeId,
    Dictionary<string, object>? config = null)
    : PluginServerGamemode(parameters, context, gamemodeId, config)
{
    public override void StartRace()
    {
        base.StartRace();
        BroadcastEvent(new RaceStartedEvent());
    }

    public override void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        base.OnClientEvent(clientId, payload);

        if (MemoryPackSerializer.Deserialize<ClientToServerEvent>(payload) is not FinishedEvent)
            return;

        FinishRace(TimeSpan.Zero, [
            new RaceStanding
            {
                PlayerId = clientId,
                FinishPosition = 0,
                FinishTime = TimeSpan.Zero
            }
        ]);
    }
}

[MemoryPackable]
[MemoryPackUnion(0, typeof(FinishedEvent))]
public abstract partial record ClientToServerEvent;

[MemoryPackable]
public partial record FinishedEvent() : ClientToServerEvent;

[MemoryPackable]
public partial record RaceStartedEvent();
