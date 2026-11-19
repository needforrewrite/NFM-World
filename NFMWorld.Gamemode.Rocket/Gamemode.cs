using JetBrains.Annotations;
using TightPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Util;

[assembly: Gamemode(
    Name = "Rocket League",
    Description = "Standard racing with jump, boost and aerial control."
)]

namespace NFMWorld.Gamemode.Rocket;

internal enum RocketState
{
    Countdown,
    InProgress,
    Finished
}

/// <summary>
/// A standard race with an arcade layer on top. Completion is the shared standard-race path
/// (checkpoints, laps, the first car across the line), so a rocket race is scored and finished
/// exactly like PvP; the difference is only what the cars can do.
/// </summary>
[UsedImplicitly]
public sealed class RocketGamemode : PluginGamemode
{
    // Action 1 jumps (grounded only), action 2 boosts while held. Both are read from
    // Control.GamemodeActions, which the host fills from the "Gamemode Action N" bindings.
    private const int JumpAction = 0;
    private const int BoostAction = 1;

    // Arcade tuning, in per-tick units against the stock gravity.
    private const int JumpImpulse = 160;
    private const int BoostThrust = 12;
    private const float MaxBoost = 200;
    private const float BoostRegenPerSecond = 6;
    private const float BoostDrainPerSecond = 22;
    private const float BoostStart = 100;

    private RocketState _state;
    private int _countdownInner;
    private int _countdown;
    private int _clientTick;
    private bool _receivedFinish;
    private int _finishRevision;

    private PhysicsController _physics = null!;
    private readonly Dictionary<BackendCar, float> _boost = [];
    private readonly Dictionary<BackendCar, bool> _jumpHeld = [];

    public RocketGamemode(
        ClientGamemodeParameters clientGamemodeParameters,
        IGamemodeContext gamemodeContext,
        string gamemodeId,
        Dictionary<string, object>? config = null
    ) : base(clientGamemodeParameters, gamemodeContext, gamemodeId, config)
    {
    }

    /// <summary>Rocket races finish the standard way, so the host's completion UI applies.</summary>
    public override bool UsesStandardRaceCompletion => true;

    public override void Begin()
    {
        base.Begin();
        _physics = new PhysicsController(Players, CurrentStage);
        _state = RocketState.Countdown;
        _countdown = 3;
        _countdownInner = GamemodeUtils.CountdownInterval;
        _clientTick = 0;
        _receivedFinish = false;
        _finishRevision = 0;
        _boost.Clear();
        _jumpHeld.Clear();
        HudState.StateText = null;
        HudState.ResultTitle = null;
        HudState.StateTextEndsAt = null;
        HudState.Finished = false;
        HudState.ClientWon = false;
        HudState.ClientLost = false;
        SetupCars();
    }

    public override void Reset()
    {
        base.Reset();
        Begin();
    }

    public override void Forfeit() => SendServerEvent<ClientToServerEvent>(new ForfeitEvent());

    public override void ConfirmFinish()
    {
        if (_state == RocketState.Finished && ClientPlayer is not null)
            SendServerEvent<ClientToServerEvent>(new ConfirmFinishEvent(_finishRevision));
    }

    public override void GameTick()
    {
        base.GameTick();
        switch (_state)
        {
            case RocketState.Countdown:
                HudState.CountdownTimer = _countdown;
                if (StandardRace.TickCountdown(ref _countdownInner, ref _countdown, GamemodeUtils.CountdownInterval))
                {
                    _state = RocketState.InProgress;
                    HudState.CountdownTimer = 0;
                }

                break;

            case RocketState.InProgress:
                _physics.GameTick();

                var clientCrossed = false;
                foreach (var player in Players)
                {
                    if (player.Car is not { } car) continue;
                    ArmArcadeInputs(car);
                    FixHoopHelper.HandleFixHoops(CurrentStage, car);
                    if (!IsLocallySimulated(player)) continue;
                    if (CheckPointHelper.HandleCheckPoint(CurrentStage, car) && player == ClientPlayer)
                        clientCrossed = true;
                }

                if (Players.Count > 1)
                    CheckPointHelper.CalculatePositions(CurrentStage, Players);

                if (clientCrossed && ClientPlayer?.Car is { } crossedCar)
                    SendServerEvent<ClientToServerEvent>(
                        new CheckpointEvent(
                            crossedCar.CurrentCheckpoint,
                            crossedCar.CurrentLap,
                            crossedCar.Placement,
                            _clientTick
                        )
                    );

                _clientTick++;

                if (ClientPlayer?.Car is { } ownCar)
                {
                    UpdateHudAndSounds(ownCar);
                    _clientBoost = _boost.TryGetValue(ownCar, out var fuel) ? fuel : 0;
                    HudState.Lap = Math.Min(ownCar.CurrentLap + 1, CurrentStage.Nlaps);
                    HudState.Position = ownCar.Placement + 1;
                }

                HudState.TotalLaps = CurrentStage.Nlaps;
                HudState.TotalRacers = Players.Count;

                foreach (var player in Players)
                {
                    if (player.Car is { } car && car.CurrentLap >= CurrentStage.Nlaps)
                    {
                        Finish(player);
                        break;
                    }
                }

                break;

            case RocketState.Finished:
                _physics.GameTick();
                break;
        }
    }

    /// <summary>
    /// The arcade layer: a grounded jump on the rising edge of action 1, and a held forward
    /// thrust on action 2 that spends boost. Both go through the generic CarPhysics facilities,
    /// so nothing here is special-cased by the engine.
    /// </summary>
    private void ArmArcadeInputs(BackendCar car)
    {
        var physics = car.CarPhysics;
        var control = car.Control;

        var jump = control.GamemodeAction(JumpAction);
        _jumpHeld.TryGetValue(car, out var held);
        if (jump && !held && physics.Grounded)
            physics.AddImpulse(0, -JumpImpulse, 0);
        _jumpHeld[car] = jump;

        var boost = _boost.TryGetValue(car, out var fuel) ? fuel : BoostStart;
        var perTickDrain = BoostDrainPerSecond / NFMWorldLibrary.Physics.TargetTps;
        var perTickRegen = BoostRegenPerSecond / NFMWorldLibrary.Physics.TargetTps;
        if (control.GamemodeAction(BoostAction) && boost > 0)
        {
            boost = MathF.Max(0, boost - perTickDrain);
            physics.AddForwardThrust(BoostThrust);
        }
        else
        {
            boost = MathF.Min(MaxBoost, boost + perTickRegen);
        }

        _boost[car] = boost;
    }

    private bool IsLocallySimulated(ClientSidePlayer player)
        => player.Info.IsClientPlayer
           || player.Info.IsBot
           && (player.Info.OwnerId == Guid.Empty || player.Info.OwnerId == ClientPlayer?.Info.Id);

    public override void OnServerEvent(ReadOnlySpan<byte> payload)
    {
        base.OnServerEvent(payload);
        Apply(TightPackSerializer.Deserialize<ServerToClientEvent>(payload));
    }

    public override void OnServerState(ReadOnlySpan<byte> payload)
    {
        base.OnServerState(payload);
        Apply(TightPackSerializer.Deserialize<ServerToClientEvent>(payload));
    }

    private void Apply(ServerToClientEvent? ev)
    {
        switch (ev)
        {
            case RaceFinishedEvent finish:
                _receivedFinish = true;
                _finishRevision = finish.Revision;
                Finish(
                    finish.WinnerSlot >= 0 && finish.WinnerSlot < Players.Count ? Players[finish.WinnerSlot] : null,
                    authoritative: true
                );
                break;
            case CountdownGoEvent:
                HudState.CountdownTimer = 0;
                _state = RocketState.InProgress;
                break;
            case RaceStateEvent state:
                _state = state.InProgress ? RocketState.InProgress : RocketState.Countdown;
                _countdown = state.Countdown;
                HudState.CountdownTimer = state.Countdown;
                break;
        }
    }

    public override void OnServerRaceFinished(RaceResults results)
    {
        base.OnServerRaceFinished(results);
        if (_receivedFinish) return;
        var first = results.Standings.FirstOrDefault(standing => standing.FinishPosition == 0);
        var winner = first.FinishTime.HasValue
            ? Players.FirstOrDefault(player => player.Info.Id != Guid.Empty && player.Info.Id == first.PlayerId)
            : null;
        if (_state != RocketState.Finished) Finish(winner);
    }

    private void Finish(ClientSidePlayer? winner = null, bool authoritative = false)
    {
        if (_state == RocketState.Finished && !authoritative)
            return;

        _state = RocketState.Finished;
        foreach (var player in Players)
            if (player.Car is { } car)
                car.CarPhysics.Halted = true;

        if (winner is not null)
        {
            var spectating = ClientPlayer is null;
            var clientWon = !spectating && winner.Info.IsClientPlayer;
            HudState.StateText = clientWon
                ? "You finished first, nice job!"
                : $"{winner.Info.PlayerName} finished first, race over!";
            HudState.ClientWon = clientWon;
            HudState.ClientLost = !spectating && !clientWon;
            HudState.ResultTitle = spectating ? "RACE OVER!" : clientWon ? "YOU WON!" : "YOU LOST!";
        }
        else
        {
            HudState.StateText = "All cars are out, race over!";
            HudState.ClientWon = false;
            HudState.ClientLost = ClientPlayer is not null;
            HudState.ResultTitle = ClientPlayer is null ? "RACE OVER!" : "YOU LOST!";
        }

        HudState.Finished = true;
        HudState.StateTextEndsAt = null;
        HudState.StatusMessage = null;
        HudState.StuntMessage = null;
    }

    /// <summary>
    /// The HUD widget hook: a boost meter is mounted by the default skins at the top-left
    /// anchor. The widget is data (label + accessor); the host builds the node.
    /// </summary>
    public override IReadOnlyList<GamemodeHudWidget>? GetHudWidgets() =>
    [
        new(GamemodeHudAnchor.TopLeft, "BOOST", () => _clientBoost / MaxBoost)
    ];

    private float _clientBoost;

    private void SetupCars()
    {
        for (var i = 0; i < Players.Count; i++)
        {
            var player = Players[i];
            var (x, z) = CurrentStage.GetStartPosition(i);
            player.Car = new BackendCar(player.Info, i, x, z) { CurrentCheckpoint = 0, CurrentLap = 0 };
            _boost[player.Car] = BoostStart;
            _jumpHeld[player.Car] = false;
        }
    }
}

internal enum RocketServerState
{
    WaitingToStart,
    Countdown,
    InProgress,
    Finished
}

/// <summary>
/// The server half runs the standard completion: countdown, checkpoint claims validated
/// through <see cref="StandardRace.IsPlausibleCheckpoint"/>, standings through
/// <see cref="StandardRace.BuildStandings"/>. It has no physics - the cars live on clients.
/// </summary>
[UsedImplicitly]
public sealed class RocketServerGamemode(
    ServerGamemodeParameters parameters,
    IServerGamemodeContext context,
    string gamemodeId,
    Dictionary<string, object>? config = null
) : PluginServerGamemode(parameters, context, gamemodeId, config)
{
    private sealed class PerPlayerGamemodeInfo
    {
        public RaceProgress Progress = null!;
    }

    private RocketServerState _state;
    private readonly Dictionary<Guid, PerPlayerGamemodeInfo> _perPlayer = [];
    private int _inner;
    private int _countdown;
    private int _serverTick;
    private int _winnerSlot = -1;
    private bool _provisional;
    private int _finishRevision;

    public override void Begin()
    {
        base.Begin();
        _perPlayer.Clear();
        _inner = GamemodeUtils.ServerCountdownInterval;
        _countdown = 3;
        _state = RocketServerState.WaitingToStart;
        _winnerSlot = -1;
        _provisional = false;
        _finishRevision = 0;

        foreach (var player in Players)
        {
            _perPlayer[player.Id] = new PerPlayerGamemodeInfo
            {
                Progress = new RaceProgress { PlayerId = player.Id }
            };
        }
    }

    public override void StartRace()
    {
        base.StartRace();
        _state = RocketServerState.Countdown;
        _countdown = 3;
        _inner = GamemodeUtils.ServerCountdownInterval;
        _serverTick = 0;
    }

    public override void GameTick()
    {
        base.GameTick();
        switch (_state)
        {
            case RocketServerState.Countdown:
                if (StandardRace.TickCountdown(ref _inner, ref _countdown, GamemodeUtils.ServerCountdownInterval))
                {
                    _state = RocketServerState.InProgress;
                    BroadcastEvent<ServerToClientEvent>(new CountdownGoEvent());
                }

                break;
            case RocketServerState.InProgress:
                _serverTick++;
                foreach (var (id, info) in _perPlayer)
                    info.Progress.TrackWasted(IsPlayerWasted(id), _serverTick);
                break;
        }
    }

    public override ReadOnlyMemory<byte>? CaptureState()
    {
        if (_state == RocketServerState.WaitingToStart)
            return null;

        if (_state == RocketServerState.Finished)
            return TightPackSerializer.Serialize<ServerToClientEvent>(
                new RaceFinishedEvent(_winnerSlot) { Revision = _finishRevision }
            );

        return TightPackSerializer.Serialize<ServerToClientEvent>(
            new RaceStateEvent(
                InProgress: _state == RocketServerState.InProgress,
                Countdown: _state == RocketServerState.Countdown ? _countdown : 0
            )
        );
    }

    public override void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        base.OnClientEvent(clientId, payload);
        var e = TightPackSerializer.Deserialize<ClientToServerEvent>(payload);

        if (e is ConfirmFinishEvent confirmation)
        {
            if (_state == RocketServerState.Finished && _provisional
                && confirmation.Revision == _finishRevision
                && Players.Any(player => player.Id == clientId && !player.IsBot)
                && _perPlayer.TryGetValue(clientId, out var info) && !info.Progress.Forfeited
                && !TryResumeRace())
            {
                _provisional = false;
                CompleteRace();
            }

            return;
        }

        if (e is ForfeitEvent)
        {
            Forfeit(clientId);
            return;
        }

        if (e is CheckpointEvent { Index: var cp, Lap: var lap, Placement: var placement })
        {
            if (!_perPlayer.TryGetValue(clientId, out var ps) || ps.Progress.Forfeited
                || _state == RocketServerState.Finished)
                return;

            var progress = ps.Progress;
            if (!StandardRace.IsPlausibleCheckpoint(
                    cp,
                    lap,
                    progress.Checkpoint,
                    progress.Lap,
                    CurrentStage.Checkpoints.Count,
                    CurrentStage.Nlaps,
                    GetPlayerPosition(clientId),
                    CurrentStage.Checkpoints
                ))
                return;

            progress.Lap = lap;
            progress.Checkpoint = cp;
            progress.Position = placement;

            if (lap >= CurrentStage.Nlaps)
                EndRace(clientId);
        }
    }

    public override void OnClientDisconnected(Guid clientId)
    {
        Forfeit(clientId);
        foreach (var player in Players)
            if (player.IsBot && player.OwnerId == clientId) Forfeit(player.Id);
        if (_provisional && Players.Where(player => !player.IsBot).All(player => _perPlayer[player.Id].Progress.Forfeited))
        {
            _provisional = false;
            CompleteRace();
        }
    }

    private void Forfeit(Guid clientId)
    {
        if (!_perPlayer.TryGetValue(clientId, out var forfeiter) || forfeiter.Progress.Forfeited)
            return;

        forfeiter.Progress.Forfeited = true;
        forfeiter.Progress.WastedAt = _serverTick;
        forfeiter.Progress.Position = Players.Count;

        if (_perPlayer.Values.All(p => p.Progress.Forfeited))
            EndRace();
    }

    private void EndRace(Guid? winner = null)
    {
        if (_state == RocketServerState.Finished)
            return;

        _state = RocketServerState.Finished;
        _winnerSlot = -1;
        for (var slot = 0; slot < Players.Count; slot++)
            if (winner == Players[slot].Id)
            {
                _winnerSlot = slot;
                break;
            }

        _finishRevision++;
        BroadcastEvent<ServerToClientEvent>(
            new RaceFinishedEvent(_winnerSlot) { Revision = _finishRevision }
        );
        CompleteRace();
    }

    private bool TryResumeRace()
    {
        var revived = false;
        foreach (var (id, info) in _perPlayer)
            revived |= info.Progress.TrackWasted(IsPlayerWasted(id), _serverTick);

        if (!revived) return false;

        _state = RocketServerState.InProgress;
        _provisional = false;
        _winnerSlot = -1;
        ResumeRace();
        BroadcastEvent<ServerToClientEvent>(new RaceResumedEvent());
        return true;
    }

    private void CompleteRace()
    {
        FinishRace(
            TimeSpan.Zero,
            StandardRace.BuildStandings(
                Players,
                _perPlayer.Values.Select(v => v.Progress).ToArray(),
                _winnerSlot >= 0 ? Players[_winnerSlot].Id : null
            ),
            provisional: _provisional
        );
    }
}

[TightPackable]
[TightPackUnion(0, typeof(CheckpointEvent))]
[TightPackUnion(1, typeof(ForfeitEvent))]
[TightPackUnion(2, typeof(ConfirmFinishEvent))]
public abstract partial record ClientToServerEvent;

[TightPackable]
public partial record CheckpointEvent(ushort Index, byte Lap, int Placement, int Tick) : ClientToServerEvent;

[TightPackable]
public partial record ForfeitEvent : ClientToServerEvent;

[TightPackable]
public partial record ConfirmFinishEvent(int Revision) : ClientToServerEvent;

[TightPackable]
[TightPackUnion(0, typeof(CountdownGoEvent))]
[TightPackUnion(1, typeof(RaceStateEvent))]
[TightPackUnion(2, typeof(RaceFinishedEvent))]
[TightPackUnion(3, typeof(RaceResumedEvent))]
public abstract partial record ServerToClientEvent;

[TightPackable]
public partial record CountdownGoEvent : ServerToClientEvent;

[TightPackable]
public partial record RaceStateEvent(bool InProgress, int Countdown) : ServerToClientEvent;

[TightPackable]
public partial record RaceFinishedEvent(int WinnerSlot) : ServerToClientEvent
{
    public int Revision { get; set; }
}

[TightPackable]
public partial record RaceResumedEvent : ServerToClientEvent;
