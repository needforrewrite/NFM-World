using JetBrains.Annotations;
using FixedMathSharp;
using NFMWorldLibrary;
using TightPack;
using fix64 = FixedMathSharp.Fixed64;
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
    // Held actions, written by the host from the "Gamemode Action N" bindings.
    private const int JumpAction = 0;
    private const int BoostAction = 1;
    private const int RollLeftAction = 2;
    private const int RollRightAction = 3;

    /// <summary>
    /// Feel tuning for the arcade layer, as instance fields so the dev console (and, later,
    /// config) can adjust them live. Defaults are the values the mode shipped with.
    /// </summary>
    public class RocketTuning
    {
        /// <summary>Upward impulse per jump, in physics units.</summary>
        public double JumpImpulse = 160;

        /// <summary>Degrees per tick of air yaw while a heading key is held, scaled by the car's Airs stat.</summary>
        public double AirYawRate = 1;

        /// <summary>The top speed boost thrust asymptotes to, in physics units per tick.</summary>
        public double BoostSpeed = 220;

        /// <summary>The top speed boost thrust asymptotes to in the air, in physics units per tick.</summary>
        public double BoostSpeedInAir = 70;

        /// <summary>How hard boost pushes while below its top speed, as a fraction of the gap.</summary>
        public double BoostAcceleration = 20;

        /// <summary>How hard boost pushes while below its top speed in the air, as a fraction of the gap.</summary>
        public double BoostAccelerationInAir = 5;

        /// <summary>Boost tank capacity.</summary>
        public double MaxBoost = 200;

        /// <summary>Boost refilled per second while not boosting.</summary>
        public double BoostRegenPerSecond = 6;

        /// <summary>Boost spent per second while boosting.</summary>
        public double BoostDrainPerSecond = 22;

        /// <summary>Boost tank at race start.</summary>
        public double BoostStart = 100;
    }

    public RocketTuning Tuning { get; } = new();

    private RocketState _state;
    private int _countdownInner;
    private int _countdown;
    private int _clientTick;
    private bool _receivedFinish;
    private int _finishRevision;

    private PhysicsController _physics = null!;
    private readonly UnlimitedArray<CarState> _cars = new();

    /// <summary>All the per-car arcade state, one object per player indexed by slot.</summary>
    private sealed class CarState
    {
        public float Boost;
        public bool JumpHeld;
        public int JumpsLeft = 2;
        public bool StoredLeft;
        public bool StoredRight;

        /// <summary>
        /// Latched while the tank is not empty. Without it, an empty tank with the button held
        /// regens one tick's worth and spends it the next, toggling every tick - which restarts
        /// the boost loop sound 63 times a second and reads as a drum roll.
        /// </summary>
        public bool Boosting;
    }

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
        HudState.StateText = null;
        HudState.ResultTitle = null;
        HudState.StateTextEndsAt = null;
        HudState.Finished = false;
        HudState.ClientWon = false;
        HudState.ClientLost = false;
        SetupCars();
    }

    /// <summary>Boost exhaust, jump sounds. Client-only; null in headless/server use.</summary>
    private RocketFx? _fx;

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
                foreach (var player in Players)
                {
                    ArmArcadeInputs(player);
                }

                _physics.GameTick();

                var clientCrossed = false;
                foreach (var player in Players)
                {
                    if (player.Car is not { } car) continue;
                    TidyArcadeInputs(player);
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
                    _clientBoost = _cars[ClientPlayer.Index].Boost;
                    HudState.Lap = Math.Min(ownCar.CurrentLap + 1, CurrentStage.Nlaps);
                    HudState.Position = ownCar.Placement + 1;
                }

                // FX step: boosting flags per player, then particles advance. Drawn in Render().
                if (_fx is { } fx)
                {
                    var boosting = new bool[Players.Count];
                    for (var i = 0; i < Players.Count; i++)
                        boosting[i] = _cars[i].Boosting;
                    fx.GameTick(boosting);
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

    private void TidyArcadeInputs(ClientSidePlayer player)
    {
        var car = player.Car!;
        var physics = car.CarPhysics;
        var control = car.Control;
        var state = _cars[player.Index];

        if (state.StoredLeft)
        {
            control.Left = true;
            state.StoredLeft = false;
        }

        if (state.StoredRight)
        {
            control.Right = true;
            state.StoredRight = false;
        }
    }

    /// <summary>
    /// The arcade layer. Action 1 jumps - once from the ground and once more in the air; the
    /// airborne jump is directional from the steering keys. Actions 3/4 steer the car's yaw in
    /// the air (Rocket-League heading control) rather than rolling it. Action 2 boosts along the
    /// nose, capped at a top speed.
    /// </summary>
    private void ArmArcadeInputs(ClientSidePlayer player)
    {
        var car = player.Car!;
        var physics = car.CarPhysics;
        var control = car.Control;
        var state = _cars[player.Index];
        // Captured before the yaw block below clears the bits, so an airborne jump can still be
        // steered on the same tick its keys are consumed for heading.
        var steerInput = (X: (control.Right ? fix64.One : fix64.Zero) - (control.Left ? fix64.One : fix64.Zero),
                          Y: (control.Up ? fix64.One : fix64.Zero) - (control.Down ? fix64.One : fix64.Zero));

        // Air heading: yaw left/right, the Q/E-style controls mapped onto heading instead of roll.
        if (physics.StuntState is 2 or -1)
        {
            var yawRate = (fix64)(Tuning.AirYawRate * (double)car.Stats.Airs);
            if (control.Left)
            {
                state.StoredLeft = true;
                control.Left = false;
                physics.TurnYaw(-yawRate);
            }

            if (control.Right)
            {
                state.StoredRight = true;
                control.Right = false;
                physics.TurnYaw(yawRate);
            }

            if (control.GamemodeAction(RollLeftAction))
            {
                control.Left = true;
            }

            if (control.GamemodeAction(RollRightAction))
            {
                control.Right = true;
            }
        }
        // air heading (not stunting)
        else if (!physics.Grounded)
        {
            var yawRate = (fix64)(Tuning.AirYawRate * (double)car.Stats.Airs);
            if (control.Left)
            {
                state.StoredLeft = true;
                control.Left = false;
                physics.TurnYaw(-yawRate);
            }

            if (control.Right)
            {
                state.StoredRight = true;
                control.Right = false;
                physics.TurnYaw(yawRate);
            }

            if (control.GamemodeAction(RollLeftAction))
            {
                control.Left = true;
            }

            if (control.GamemodeAction(RollRightAction))
            {
                control.Right = true;
            }
        }

        var jump = control.GamemodeAction(JumpAction);
        var fromGround = physics.Grounded;
        if (fromGround)
            state.JumpsLeft = 2;

        if (jump && !state.JumpHeld && state.JumpsLeft > 0)
        {
            state.JumpsLeft--;
            var impulse = (fix64)Tuning.JumpImpulse;
            var steer = fromGround ? (X: fix64.Zero, Y: fix64.Zero) : steerInput;
            if (steer.X == 0 && steer.Y == 0)
                physics.AddImpulse(0, -impulse, 0);
            else
                physics.AddLocalImpulse(steer.X * impulse, steer.Y * impulse);

            _fx?.PlayJump(car);
        }

        state.JumpHeld = jump;

        var perTickDrain = (float)((Tuning.BoostDrainPerSecond) / Physics.TargetTps);
        var perTickRegen = (float)((Tuning.BoostRegenPerSecond * (car.Stats.Powerloss / 3500000f)) / Physics.TargetTps);
        var wantsBoost = control.GamemodeAction(BoostAction) && state.Boost > 0;
        // Latch: once the tank runs dry the thrust stays off until the button is released, so
        // regen cannot flicker it back on at one-tick intervals - that restarts the boost loop
        // sound 63 times a second and reads as a drum roll.
        if (!wantsBoost)
            state.Boosting = false;
        else if (!state.Boosting)
            state.Boosting = true;

        if (state.Boosting)
        {
            state.Boost = MathF.Max(0, state.Boost - perTickDrain);
            // Proportional to the gap to the top speed: thrust fades out as the top speed is
            // reached, so boosting into a slope or off a jump never compounds.
            var topSpeed = !physics.Grounded ? Tuning.BoostSpeedInAir : (Tuning.BoostSpeed * car.Stats.Swits[2] / 250f);
            var gap = topSpeed - (double)physics.ForwardSpeed;
            if (gap > 0 && topSpeed > 0)
                physics.AddForwardThrust(
                    (fix64)((!physics.Grounded ? Tuning.BoostAccelerationInAir : Tuning.BoostAcceleration) * gap / topSpeed)
                );
            if (state.Boost <= 0)
                state.Boosting = false;
        }
        else
        {
            state.Boost = MathF.Min((float)Tuning.MaxBoost, state.Boost + perTickRegen);
        }
    }

    /// <summary>
    /// The host flushes the immediate-mode 3D batch right after this returns, so the exhaust
    /// drawn here lands in the same frame.
    /// </summary>
    public override void Render() => _fx?.Render();

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

        StandardRace.FinishHud(HudState, ClientPlayer, winner);
    }

    /// <summary>
    /// The HUD widget hook: a boost meter is mounted by the default skins at the top-left
    /// anchor. The widget is data (label + accessor); the host builds the node.
    /// </summary>
    public override IReadOnlyList<GamemodeHudWidget>? GetHudWidgets() =>
    [
        new(GamemodeHudAnchor.TopLeft, "BOOST", () => _clientBoost / (float)Tuning.MaxBoost)
    ];

    private float _clientBoost;

    private void SetupCars()
    {
        for (var i = 0; i < Players.Count; i++)
        {
            var player = Players[i];
            var (x, z) = CurrentStage.GetStartPosition(i);
            player.Car = new BackendCar(player.Info, i, x, z) { CurrentCheckpoint = 0, CurrentLap = 0 };
            player.Car.CarPhysics.Stat = player.Car.CarPhysics.Stat with { Airc = 0 };
            player.Car.CarPhysics.Stat = player.Car.CarPhysics.Stat with { Airs = 1 };
            player.Car.CarPhysics.IsHyperglidingEnabled = false;
            _cars[player.Index] = new CarState { Boost = (float)Tuning.BoostStart };
        }

        // FX needs the cars and the (client-only) IBackend. Construction throws when the backend
        // is not installed (headless/server) - the only expected failure. Anything else is logged,
        // because a broken FX setup otherwise looks identical to "this gamemode has no effects".
        _fx?.Dispose();
        _fx = null;
        if (ClientPlayer is null)
            return;

        try
        {
            _fx = new RocketFx(Players.Select(p => p.Car!));
        }
        catch (InvalidOperationException)
        {
            // IBackend not installed: no client graphics/audio.
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
