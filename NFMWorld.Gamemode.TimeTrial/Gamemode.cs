using System.Diagnostics;
using JetBrains.Annotations;
using TightPack;
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
    private readonly Stopwatch _timer = new();
    private readonly bool _simulation;
    private readonly bool _multiplayer;

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
        _multiplayer = IsMultiplayer;
    }

    public override void Begin()
    {
        if (!ValidateStage()) return;
        base.Begin();
        SetupRun();
    }

    public override void Reset()
    {
        if (_multiplayer || !ValidateStage()) return;
        base.Reset();
        SetupRun();
    }

    private void SetupRun()
    {
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
        for (var i = 0; i < Players.Count; i++)
        {
            var player = Players[i];
            player.Car = new BackendCar(player.Info, i, 0, 0);
            if (player != ClientPlayer)
                ClientCallbacks.GetClientCarCallbacks(player.Car).AlphaOverride = 0.5f;
        }

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
        _countdown = Math.Max(_multiplayer ? 1 : 0, _countdown - 1);
        if (_countdown > 0)
            return;

        HudState.CountdownTimer = 0;
        _state = TimeTrialState.InProgress;
        _timer.Start();
    }

    public override void Forfeit()
    {
        if (ClientPlayer is not null)
            SendServerEvent<ClientToServerEvent>(new ForfeitEvent());
    }

    public override void OnServerEvent(ReadOnlySpan<byte> payload) => ApplyServerState(payload);

    public override void OnServerState(ReadOnlySpan<byte> payload) => ApplyServerState(payload);

    private void ApplyServerState(ReadOnlySpan<byte> payload)
    {
        if (!_multiplayer
            || _simulation
            || _state == TimeTrialState.Unavailable
            || TightPackSerializer.Deserialize<RaceStateEvent>(payload) is not { } state)
            return;

        _countdown = state.Countdown;
        _inner = GamemodeUtils.CountdownInterval;
        HudState.CountdownTimer = state.Countdown;
        if (state.InProgress && _state != TimeTrialState.Finished)
        {
            _state = TimeTrialState.InProgress;
            _timer.Start();
        }
        else if (!state.InProgress)
        {
            _state = TimeTrialState.Countdown;
        }
    }

    public override void OnServerRaceFinished(RaceResults results)
    {
        if (_state == TimeTrialState.Unavailable) return;
        _state = TimeTrialState.Finished;
        _timer.Stop();
        HudState.Finished = true;
        foreach (var player in Players)
        {
            if (player.Car is { } car)
                car.CarPhysics.Halted = true;
        }

        if (ClientPlayer?.Car is { } own && own.CurrentLap >= CurrentStage.Nlaps)
            RenderFinishedText();
        else
            HudState.StateText = "Finished!";
    }

    private void TickRemoteCars()
    {
        foreach (var player in Players)
        {
            if (player == ClientPlayer || player.IsFake || player.Car is not { } car)
                continue;

            FixHoopHelper.HandleFixHoops(CurrentStage, car);
            CheckPointHelper.HandleCheckPoint(CurrentStage, car);
            // Ghosts still drive against the stage, but never collide with another car.
            car.Drive(CurrentStage);
        }
    }

    private void TickInProgress()
    {
        TickRemoteCars();
        if (ClientPlayer?.Car is { } car)
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

            if (car.CurrentLap >= CurrentStage.Nlaps)
            {
                _state = TimeTrialState.Finished;
                car.CarPhysics.Halted = true;
                SendServerEvent<ClientToServerEvent>(new FinishedEvent());
                _timer.Stop();
            }
        }

        UpdatePosition();
        _tick++;
    }

    private void TickFinished()
    {
        TickRemoteCars();
        UpdatePosition();
        if (ClientPlayer?.Car is not { } car)
            return;
        car.Drive(CurrentStage);

        if (_written || car.CurrentLap < CurrentStage.Nlaps)
            return;

        _written = true;
        NotifyCompleted();
        RenderFinishedText();
    }

    /// <summary>
    /// Opens the local player's best-run and current-run recordings.
    /// </summary>
    private void SetupPlayer()
    {
        var previousBest = _best;
        _best = null;
        _current = null;
        HudState.Finished = false;
        HudState.ClientWon = false;
        HudState.ClientLost = false;

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

        if (_simulation || ClientPlayer?.Car is not { } car)
            return;

        _best = previousBest is not null && previousBest.CarName == car.Player.CarName
                && previousBest.StageName == CurrentStage.Path
            ? previousBest : SavedTimeTrial.Load(car.Player.CarName, CurrentStage.Path);
        _current = new SavedTimeTrial(car.Player.CarName, CurrentStage.Path, CurrentStage.StageLoader, car.Rad);
    }

    /// <summary>
    /// Adds a translucent copy of the player's car that replays the saved best run.
    /// </summary>
    private void SetupGhost()
    {
        if (_multiplayer || _simulation || _best is null || ClientPlayer?.Car is not { } source)
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
        if (_multiplayer)
        {
            if (Players.Count > 1)
                CheckPointHelper.CalculatePositions(CurrentStage, Players);
            HudState.TotalRacers = Players.Count;
            HudState.Position = ClientPlayer?.Car is { } own ? own.Placement + 1 : 0;
            return;
        }

        HudState.Position = 1;
        HudState.TotalRacers = 1;
        if (ClientPlayer?.Car is not { } car
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
    /// Transfers the completed run to the host. This is every finish, not only a personal
    /// best: the archive wants a ranking, and the host decides what may be submitted.
    /// </summary>
    private void NotifyCompleted()
    {
        if (_simulation || _current is null)
            return;

        var completed = _current;
        _current = null;
        var saveBest = _best is null || completed.IsFasterThan(_best);
        if (saveBest)
            _best = completed;
        ClientCallbacks.TimeTrialCompleted(completed, saveBest);
    }

    private void RenderFinishedText()
    {
        var totalMs = (long)_timer.Elapsed.TotalMilliseconds;

        var next = !_multiplayer ? "\nPress R to restart"
            : HudState.Finished ? "" : "\nWaiting for other players...";
        HudState.StateText =
            $"Finished! Time: {totalMs / 60000:D2}:{totalMs % 60000 / 1000:D2}.{totalMs % 1000:D3}{next}";
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

/// <summary>Each participant finishes their own trial; the session waits for everyone.</summary>
[UsedImplicitly]
public class TimeTrialServerGamemode(
    ServerGamemodeParameters parameters,
    IServerGamemodeContext context,
    string gamemodeId,
    Dictionary<string, object>? config = null
)
    : PluginServerGamemode(parameters, context, gamemodeId, config)
{
    private readonly Dictionary<Guid, TimeSpan?> _finishes = [];
    private readonly HashSet<Guid> _left = [];
    private readonly Stopwatch _timer = new();
    private TimeTrialState _state;
    private int _countdown;
    private int _inner;

    public override void Begin()
    {
        base.Begin();
        _state = TimeTrialState.NotStarted;
        _finishes.Clear();
        _left.Clear();
        _timer.Reset();
        foreach (var player in Players)
            _finishes[player.Id] = null;
    }

    public override void StartRace()
    {
        base.StartRace();
        _state = TimeTrialState.Countdown;
        _countdown = 3;
        _inner = GamemodeUtils.ServerCountdownInterval;
        if (Config is not null && Config.TryGetValue("simulation", out var value) && value is true)
            StartDriving();
    }

    public override ReadOnlyMemory<byte>? CaptureState()
    {
        if (_state == TimeTrialState.NotStarted)
            return null;
        return TightPackSerializer.Serialize(
            new RaceStateEvent(_state != TimeTrialState.Countdown, _state == TimeTrialState.Countdown ? _countdown : 0)
        );
    }

    public override void GameTick()
    {
        base.GameTick();
        if (_state != TimeTrialState.Countdown || --_inner > 0)
            return;

        _inner = GamemodeUtils.ServerCountdownInterval;
        if (--_countdown <= 0)
            StartDriving();
    }

    private void StartDriving()
    {
        _state = TimeTrialState.InProgress;
        _timer.Start();
        BroadcastEvent(new RaceStateEvent(true, 0));
    }

    public override void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        base.OnClientEvent(clientId, payload);
        switch (TightPackSerializer.Deserialize<ClientToServerEvent>(payload))
        {
            case ForfeitEvent:
                OnClientDisconnected(clientId);
                break;
            case FinishedEvent when _state == TimeTrialState.InProgress && CurrentStage.Checkpoints.Count > 0:
                if (!_finishes.TryGetValue(clientId, out var finish) || finish is not null || _left.Contains(clientId))
                    return;
                _finishes[clientId] = _timer.Elapsed;
                CompleteIfDone();
                break;
        }
    }

    public override void OnClientDisconnected(Guid clientId)
    {
        if (!_finishes.ContainsKey(clientId) || _state == TimeTrialState.Finished)
            return;
        _left.Add(clientId);
        CompleteIfDone();
    }

    private void CompleteIfDone()
    {
        if (_finishes.Any(p => p.Value is null && !_left.Contains(p.Key)))
            return;

        _state = TimeTrialState.Finished;
        _timer.Stop();
        FinishRace(
            _timer.Elapsed,
            _finishes.OrderBy(p => p.Value is null).ThenBy(p => p.Value)
                .Select((p, index) => new RaceStanding
                    {
                        PlayerId = p.Key,
                        FinishPosition = index,
                        FinishTime = p.Value
                    }
                ).ToArray()
        );
    }
}

[TightPackable]
[TightPackUnion(0, typeof(FinishedEvent))]
[TightPackUnion(1, typeof(ForfeitEvent))]
public abstract partial record ClientToServerEvent;

[TightPackable]
public partial record FinishedEvent() : ClientToServerEvent;

[TightPackable]
public partial record ForfeitEvent() : ClientToServerEvent;

[TightPackable]
public partial record RaceStateEvent(bool InProgress, int Countdown);
