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
    Unavailable,
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

    private readonly Stopwatch _timer = new();
    private readonly bool _simulation;

    private TimeTrialState _state;
    private int _countdown;
    private int _inner;
    private int _tick;
    private int? _ghostFinishTick;
    private int? _ghostIndex;
    private bool _written;

    private SavedTimeTrial? _best;
    private SavedTimeTrial? _current;

    public TimeTrialGamemode(
        ClientGamemodeParameters clientGamemodeParameters,
        IGamemodeContext gamemodeContext,
        string gamemodeId,
        Dictionary<string, object>? config = null
    )
        : base(clientGamemodeParameters, gamemodeContext, gamemodeId, config)
    {
        _simulation = Config is not null && Config.TryGetValue("simulation", out var value) && value is true;
    }

    public override void Begin()
    {
        if (!ValidateStage()) return;
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
        if (!ValidateStage()) return;
        base.Reset();

        _tick = 0;
        _ghostFinishTick = null;
        _ghostIndex = null;
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
        UpdatePosition();
    }

    public override void GameTick()
    {
        if (_state == TimeTrialState.Unavailable) return;
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

    private bool ValidateStage()
    {
        if (CurrentStage.Checkpoints.Count > 0) return true;
        _state = TimeTrialState.Unavailable;
        _timer.Reset();
        HudState.CountdownTimer = 0;
        HudState.LapTime = 0;
        HudState.Position = 0;
        HudState.TotalRacers = 0;
        HudState.StateText = "Time trial unavailable: this stage has no checkpoints.";
        HudState.StateTextEndsAt = null;
        return false;
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
            if (CheckPointHelper.HandleCheckPoint(CurrentStage, car))
                _current?.RecordSplit((long)_timer.Elapsed.TotalMilliseconds);

            car.Drive(CurrentStage);

            UpdateHudAndSounds(car);
            HudState.Lap = Math.Min(car.CurrentLap + 1, CurrentStage.Nlaps);
            HudState.LapTime = (int)_timer.Elapsed.TotalMilliseconds;

            _current?.RecordTick(car);
            ApplyGhost();
            UpdatePosition();

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
        HudState.LapTime = 0;
        HudState.Speed = 0;
        HudState.CountdownTimer = _simulation ? 0 : _countdown;
        HudState.Position = 1;
        HudState.TotalRacers = 1;
        HudState.StateText = "";
        HudState.StateTextEndsAt = null;
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
        _ghostIndex = ghostIndex;

        ClientCallbacks.GetClientCarCallbacks(ghostCar).AlphaOverride = 0.5f;
    }

    private void ApplyGhost()
    {
        if (_best is null || _ghostIndex is not { } index || index >= Players.Count || _ghostFinishTick is not null)
            return;

        if (Players[index].Car is not { } ghostCar)
            return;

        if (_best.GetTick(_tick) is { } controls)
            ghostCar.Control.Decode(controls);

        FixHoopHelper.HandleFixHoops(CurrentStage, ghostCar);
        CheckPointHelper.HandleCheckPoint(CurrentStage, ghostCar);
        ghostCar.Drive(CurrentStage);
        if (ghostCar.CurrentLap >= CurrentStage.Nlaps)
        {
            _ghostFinishTick = _tick;
            ghostCar.CarPhysics.Halted = true;
        }
    }

    private void UpdatePosition()
    {
        HudState.Position = 1;
        HudState.TotalRacers = 1;
        if (Players[PlayerCarIndex].Car is not { } car
            || _ghostIndex is not { } index
            || index >= Players.Count
            || Players[index].Car is not { } ghost) return;
        HudState.TotalRacers = 2;
        if (_ghostFinishTick is { } finished)
            HudState.Position = car.CurrentLap >= CurrentStage.Nlaps && finished == _tick ? 1 : 2;
        else if (car.CurrentLap < CurrentStage.Nlaps)
        {
            if (car.TotalCheckpoint != ghost.TotalCheckpoint)
                HudState.Position = car.TotalCheckpoint > ghost.TotalCheckpoint ? 1 : 2;
            else if (car.CurrentCheckpoint < CurrentStage.Checkpoints.Count)
            {
                var next = CurrentStage.Checkpoints[car.CurrentCheckpoint].Position;
                var dx = (double)(car.Position.X - next.X);
                var dz = (double)(car.Position.Z - next.Z);
                var ghostDx = (double)(ghost.Position.X - next.X);
                var ghostDz = (double)(ghost.Position.Z - next.Z);
                HudState.Position = dx * dx + dz * dz <= ghostDx * ghostDx + ghostDz * ghostDz ? 1 : 2;
            }
        }

        car.Placement = HudState.Position - 1;
        ghost.Placement = 1 - car.Placement;
    }

    /// <summary>
    /// Saves the current run when it beats the previous best (or there is none).
    /// </summary>
    private void SaveIfBest()
    {
        if (_current is null)
            return;

        if (_best is null || _current.IsFasterThan(_best))
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
    Dictionary<string, object>? config = null
)
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

        FinishRace(
            TimeSpan.Zero,
            [
                new RaceStanding
                {
                    PlayerId = clientId,
                    FinishPosition = 0,
                    FinishTime = TimeSpan.Zero
                }
            ]
        );
    }
}

[MemoryPackable]
[MemoryPackUnion(0, typeof(FinishedEvent))]
public abstract partial record ClientToServerEvent;

[MemoryPackable]
public partial record FinishedEvent() : ClientToServerEvent;

[MemoryPackable]
public partial record RaceStartedEvent();
