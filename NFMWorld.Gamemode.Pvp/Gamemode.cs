using JetBrains.Annotations;
using TightPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Helpers;

[assembly: Gamemode(
    Name = "PvP",
    Description = "A standard PvP free-for-all gamemode."
)]

[assembly: GamemodeConfigProperty(
    Name = "constraint",
    Type = typeof(string),
    Label = "Race constraint",
    Description = "The type of race to play."
)]

[assembly: GamemodeConfigPropertyOption("constraint", Label = "Racing", Value = "racing")]
[assembly: GamemodeConfigPropertyOption("constraint", Label = "Wasting", Value = "wasting")]
[assembly: GamemodeConfigPropertyOption("constraint", Label = "Both", Value = "both")]

namespace NFMWorld.Gamemode.Pvp;

internal enum GamemodeState
{
    Countdown,
    InProgress,
    Finished
}

internal enum ServerGamemodeState
{
    WaitingToStart,
    Countdown,
    InProgress,
    Finished
}

[UsedImplicitly]
public class PvpGamemode : PluginGamemode
{
    private GamemodeState _state;
    private int _countdown;
    private int _inner;
    private int _clientTick;
    private bool _receivedFinish;
    private int _finishRevision;
    private bool _survivalFinish;

    private RaceConstraint _constraint = RaceConstraint.Both;

    private PhysicsController _physics;

    public PvpGamemode(
        ClientGamemodeParameters clientGamemodeParameters,
        IGamemodeContext gamemodeContext,
        string gamemodeId,
        Dictionary<string, object>? config = null
    ) : base(clientGamemodeParameters, gamemodeContext, gamemodeId, config)
    {
        _physics = new PhysicsController(Players, CurrentStage);
    }

    private void SetupCars()
    {
        for (var i = 0; i < Players.Count; i++)
        {
            var player = Players[i];

            var (x, z) = CurrentStage.GetStartPosition(i);

            var car = new BackendCar(player.Info, i, x, z);
            player.Car = car;

            car.CurrentCheckpoint = 0;
            car.CurrentLap = 0;

            // Only the client that owns a bot drives it; every other client sees the
            // state its owner relays. An ownerless bot (singleplayer) is driven locally.
            var localId = ClientPlayer?.Info.Id;
            if (player.Info.IsBot
                && (player.Info.OwnerId == Guid.Empty || player.Info.OwnerId == localId))
                AttachBot("classic", player);
        }
    }

    public override void Begin()
    {
        base.Begin();
        SetupCars();
        _state = GamemodeState.Countdown;
        _countdown = 3;
        _inner = GamemodeUtils.CountdownInterval;
        _clientTick = 0;
        _receivedFinish = false;
        _finishRevision = 0;
        _survivalFinish = false;
        _constraint = RaceConstraint.From(Config);
        HudState.StateText = null;
        HudState.ResultTitle = null;
        HudState.StateTextEndsAt = null;
        HudState.Finished = false;
        HudState.ClientWon = false;
        HudState.ClientLost = false;
    }

    public override void Reset()
    {
        base.Reset();
        Begin();
    }

    public override void Forfeit() => SendServerEvent<ClientToServerEvent>(new ForfeitEvent());

    public override void ConfirmFinish()
    {
        if (_state == GamemodeState.Finished && ClientPlayer is not null)
            SendServerEvent<ClientToServerEvent>(new ConfirmFinishEvent(_finishRevision));
    }

    public override void GameTick()
    {
        base.GameTick();
        switch (_state)
        {
            case GamemodeState.Countdown:
                _inner--;
                HudState.CountdownTimer = _countdown;
                if (_inner <= 0)
                {
                    _inner = GamemodeUtils.CountdownInterval;
                    _countdown--;
                }

                break;
            case GamemodeState.InProgress:
                _physics.GameTick();

                var clientCrossed = false;
                foreach (var player in Players)
                {
                    if (player.Car != null)
                    {
                        FixHoopHelper.HandleFixHoops(CurrentStage, player.Car);
                        var localProgress = player == ClientPlayer
                                            || player.Info.IsBot && (player.Info.OwnerId == Guid.Empty
                                                                     || player.Info.OwnerId == ClientPlayer?.Info.Id);
                        if (localProgress && CheckPointHelper.HandleCheckPoint(CurrentStage, player.Car) && player
                            == ClientPlayer)
                            clientCrossed = true;
                    }
                }

                // After the crossings, so a car that just took a checkpoint is ranked on it.
                // Nothing computed the order in a race, so every car stayed at Placement 0 and
                // the HUD showed the whole field as first. Only the client player's car is not
                // driven by this client's physics; the rest are, but a bot's own rank pass
                // writes the same permutation and the race must own the order, not the bots.
                if (Players.Count > 1)
                    CheckPointHelper.CalculatePositions(CurrentStage, Players);

                if (clientCrossed && ClientPlayer?.Car is { } crossedCar)
                {
                    SendServerEvent<ClientToServerEvent>(
                        new CheckpointEvent(
                            crossedCar.CurrentCheckpoint,
                            crossedCar.CurrentLap,
                            crossedCar.Placement,
                            _clientTick
                        )
                    );
                }

                _clientTick++;

                // A spectator has no car of its own; it still gets the lap count and field size.
                if (ClientPlayer?.Car is { } ownCar)
                {
                    UpdateHudAndSounds(ownCar);
                    HudState.Lap = Math.Min(ownCar.CurrentLap + 1, CurrentStage.Nlaps);
                    HudState.Position = ownCar.Placement + 1;
                }

                HudState.TotalLaps = CurrentStage.Nlaps;
                HudState.TotalRacers = Players.Count;

                // The first car across the line ends the race for everyone. Waiting for the
                // client to finish let a bot complete every lap while the race carried on.
                ClientSidePlayer? winner = null;
                foreach (var player in Players)
                {
                    if (player.Car is { } car && car.CurrentLap >= CurrentStage.Nlaps)
                    {
                        winner = player;
                        break;
                    }
                }

                if (winner is not null && _constraint.Racing)
                    Finish(winner);
                break;
            case GamemodeState.Finished:
                _physics.GameTick();
                if (_survivalFinish)
                    foreach (var player in Players)
                        if (player.Car is { } car && (player.Info.IsClientPlayer
                                                      || player.Info.IsBot && (player.Info.OwnerId == Guid.Empty || player.Info
                                                          .OwnerId == ClientPlayer?.Info.Id)))
                            FixHoopHelper.HandleFixHoops(CurrentStage, car);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

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
                    finish.WastedVictory,
                    finish.LastCarStanding,
                    authoritative: true
                );
                break;
            case RaceResumedEvent:
                _state = GamemodeState.InProgress;
                _receivedFinish = false;
                _survivalFinish = false;
                Results = null;
                HudState.Finished = false;
                HudState.ClientWon = false;
                HudState.ClientLost = false;
                HudState.ResultTitle = null;
                HudState.StateText = null;
                HudState.StateTextEndsAt = null;
                foreach (var player in Players)
                    if (player.Car is { } car) car.CarPhysics.Halted = false;
                break;
            case CountdownGoEvent:
                HudState.CountdownTimer = 0;
                _state = GamemodeState.InProgress;
                break;

            // The baseline. A race already under way must run its physics: no car is driven
            // otherwise, and the camera yaw Drive computes stays at zero.
            case RaceStateEvent state:
                _state = state.InProgress ? GamemodeState.InProgress : GamemodeState.Countdown;
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
            ? Players.FirstOrDefault(player => player.Info.Id != Guid.Empty && player.Info.Id == first.PlayerId) : null;
        if (_state != GamemodeState.Finished) Finish(winner);
    }

    /// <summary>
    /// The race is over: every car coasts to a stop and the HUD stops advancing laps.
    /// </summary>
    private void Finish(
        ClientSidePlayer? winner = null,
        bool wastedVictory = false,
        bool lastCarStanding = false,
        bool authoritative = false
    )
    {
        if (_state == GamemodeState.Finished && !authoritative)
            return;

        _state = GamemodeState.Finished;
        _survivalFinish = lastCarStanding;
        foreach (var player in Players)
        {
            if (player.Car is { } car)
                car.CarPhysics.Halted = true;
        }

        if (winner is not null)
        {
            // A spectator has no horse in this race: nobody won or lost for them.
            var spectating = ClientPlayer is null;
            var clientWon = !spectating && winner.Info.IsClientPlayer;
            HudState.StateText = clientWon
                ? wastedVictory ? "You won, all other cars have been wasted!"
                    : lastCarStanding ? "You won, you're the last car standing!" : "You finished first, nice job!"
                : lastCarStanding ? $"{winner.Info.PlayerName} is the last car standing, race over!"
                    : $"{winner.Info.PlayerName} finished first, race over!";
            HudState.ClientWon = clientWon;
            HudState.ClientLost = !spectating && !clientWon;
            HudState.ResultTitle = spectating ? "RACE OVER!"
                : clientWon ? wastedVictory ? "YOU WASTED 'EM!" : "YOU WON!" : "YOU LOST!";
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

        // The announcer stops ticking with the race, so its lines would otherwise stay up.
        HudState.StatusMessage = null;
        HudState.StuntMessage = null;
    }
}

[UsedImplicitly]
public class PvpServerGamemode(
    ServerGamemodeParameters parameters,
    IServerGamemodeContext context,
    string gamemodeId,
    Dictionary<string, object>? config = null
)
    : PluginServerGamemode(parameters, context, gamemodeId, config)
{
    private class PerPlayerGamemodeInfo
    {
        public byte Lap;
        public ushort Checkpoint;
        public int Position;
        public bool Forfeited;

        /// <summary>Server tick the car was last seen wasted, or -1 while it is alive.</summary>
        public int WastedAt = -1;
    }

    private ServerGamemodeState _state;
    private readonly Dictionary<Guid, PerPlayerGamemodeInfo> _perPlayer = [];
    private int _inner;
    private int _countdown;

    /// <summary>Server ticks since the race started; DNF order is keyed on it.</summary>
    private int _serverTick;

    private RaceConstraint _constraint = RaceConstraint.Both;

    private int _winnerSlot = -1;
    private bool _wastedVictory;
    private bool _lastCarStanding;
    private bool _provisional;
    private int _finishRevision;

    public override void Begin()
    {
        base.Begin();
        _perPlayer.Clear();
        _inner = GamemodeUtils.ServerCountdownInterval;
        _countdown = 3;
        _state = ServerGamemodeState.WaitingToStart;
        _constraint = RaceConstraint.From(Config);
        _winnerSlot = -1;
        _wastedVictory = false;
        _lastCarStanding = false;
        _provisional = false;
        _finishRevision = 0;

        foreach (var player in Players)
        {
            _perPlayer[player.Id] = new PerPlayerGamemodeInfo
            {
                Lap = 0,
                Checkpoint = 0,
                Position = 0
            };
        }
    }

    public override ReadOnlyMemory<byte>? CaptureState()
    {
        // Before the race starts nothing has been broadcast, so there is nothing to catch up on.
        // A baseline here would carry the server's not-yet-started state and overwrite the
        // client's own countdown, leaving a fresh race with no countdown on screen.
        if (_state == ServerGamemodeState.WaitingToStart)
            return null;

        if (_state == ServerGamemodeState.Finished)
            return TightPackSerializer.Serialize<ServerToClientEvent>(
                new RaceFinishedEvent(_winnerSlot, _wastedVictory, _lastCarStanding)
                {
                    Revision = _finishRevision
                }
            );

        return TightPackSerializer.Serialize<ServerToClientEvent>(
            new RaceStateEvent(
                InProgress: _state is ServerGamemodeState.InProgress or ServerGamemodeState.Finished,
                Countdown: _state == ServerGamemodeState.Countdown ? _countdown : 0
            )
        );
    }

    public override void StartRace()
    {
        base.StartRace();
        _state = ServerGamemodeState.Countdown;
        _countdown = 3;
        _inner = GamemodeUtils.ServerCountdownInterval;
        _serverTick = 0;
    }

    public override void GameTick()
    {
        base.GameTick();
        switch (_state)
        {
            case ServerGamemodeState.Countdown:
                _inner--;
                if (_inner <= 0)
                {
                    _inner = GamemodeUtils.ServerCountdownInterval;
                    _countdown--;
                    if (_countdown <= 0)
                    {
                        _state = ServerGamemodeState.InProgress;
                        BroadcastEvent<ServerToClientEvent>(new CountdownGoEvent());
                    }
                }

                break;
            case ServerGamemodeState.InProgress:
                _serverTick++;
                TrackWasted();
                CheckConstraint();
                break;
            case ServerGamemodeState.Finished when _provisional:
                _serverTick++;
                TryResumeRace();
                break;
        }
    }

    /// <summary>
    /// Survival ends every PvP race; the constraint only controls a checkpoint victory.
    /// </summary>
    private void CheckConstraint()
    {
        // A solo session has nobody to waste; the last car alive is trivially the only car.
        if (_perPlayer.Count <= 1)
            return;

        var alive = 0;
        foreach (var info in _perPlayer.Values)
        {
            if (!info.Forfeited && info.WastedAt < 0)
                alive++;
        }

        if (alive <= 1)
        {
            var survivor = _perPlayer.FirstOrDefault(player => !player.Value.Forfeited && player.Value.WastedAt < 0);
            EndRace(alive == 1 ? survivor.Key : null, lastCarStanding: true);
        }
    }

    private void EndRace(Guid? winner = null, bool lastCarStanding = false)
    {
        if (_state == ServerGamemodeState.Finished)
            return;

        _state = ServerGamemodeState.Finished;
        _winnerSlot = -1;
        for (var slot = 0; slot < Players.Count; slot++)
            if (winner == Players[slot].Id)
            {
                _winnerSlot = slot;
                break;
            }

        _lastCarStanding = lastCarStanding;
        _provisional = lastCarStanding && _perPlayer.Values.Any(info => !info.Forfeited && info.WastedAt >= 0);
        _finishRevision++;
        _wastedVictory = lastCarStanding && winner.HasValue
                         && _perPlayer.All(
                             player => player.Key == winner || !player.Value.Forfeited && player.Value.WastedAt >= 0
                         );
        BroadcastEvent<ServerToClientEvent>(
            new RaceFinishedEvent(_winnerSlot, _wastedVictory, _lastCarStanding)
            {
                Revision = _finishRevision
            }
        );
        CompleteRace();
    }

    private bool TryResumeRace()
    {
        if (!TrackWasted()) return false;
        _state = ServerGamemodeState.InProgress;
        _provisional = false;
        _winnerSlot = -1;
        _wastedVictory = false;
        _lastCarStanding = false;
        ResumeRace();
        BroadcastEvent<ServerToClientEvent>(new RaceResumedEvent());
        return true;
    }

    /// <summary>
    /// Records when each car was last seen wasted, so the standings can order DNFs by who died
    /// most recently. Wasted is the owner's own report, relayed with the car state.
    /// </summary>
    private bool TrackWasted()
    {
        var revived = false;
        foreach (var (id, info) in _perPlayer)
        {
            if (info.Forfeited)
                continue;

            switch (IsPlayerWasted(id))
            {
                case true when info.WastedAt < 0:
                    info.WastedAt = _serverTick;
                    break;
                // Revived (pushed through a fix hoop): back in the running, and it is no longer
                // a DNF unless it dies again later.
                case false:
                    revived |= info.WastedAt >= 0;
                    info.WastedAt = -1;
                    break;
            }
        }

        return revived;
    }

    /// <summary>
    /// Finishes the race with a canonical standing order: finishers by position, then everyone
    /// still alive but unfinished, then DNFs newest-death first. A player who was revived is
    /// alive again, so they sort with the running rather than the dead.
    /// </summary>
    public void CompleteRace()
    {
        FinishRace(
            TimeSpan.Zero,
            [
                .. _perPlayer
                .OrderBy(kv => _winnerSlot >= 0 && Players[_winnerSlot].Id == kv.Key ? -1 : StandingGroup(kv.Value))
                .ThenBy(kv => StandingWithin(kv.Value))
                .Select((kv, index) => new RaceStanding
                        {
                            PlayerId = kv.Key,
                            FinishPosition = index,
                            FinishTime = kv.Value.Forfeited || kv.Value.WastedAt >= 0 ? null : TimeSpan.Zero
                        }
                    )
            ],
            provisional: _provisional
        );
    }

    /// <summary>
    /// A client's connection went away. A player who quits drops its transport instead of
    /// sending ForfeitEvent, so this is that same departure arriving by another route.
    /// </summary>
    public override void OnClientDisconnected(Guid clientId)
    {
        Forfeit(clientId);
        foreach (var player in Players)
            if (player.IsBot && player.OwnerId == clientId) Forfeit(player.Id);
        if (_provisional && Players.Where(player => !player.IsBot).All(player => _perPlayer[player.Id].Forfeited))
        {
            _provisional = false;
            CompleteRace();
        }
    }

    /// <summary>Records a departure; the last remaining car wins.</summary>
    private void Forfeit(Guid clientId)
    {
        if (!_perPlayer.TryGetValue(clientId, out var forfeiter) || forfeiter.Forfeited)
            return;

        forfeiter.Forfeited = true;
        forfeiter.WastedAt = _serverTick;
        forfeiter.Position = Players.Count;

        // Nobody left to race: end it rather than ticking an empty session forever.
        if (_perPlayer.Values.All(p => p.Forfeited))
            EndRace();
        else if (_state == ServerGamemodeState.InProgress)
            CheckConstraint();
    }

    /// <summary>Running cars are placed by checkpoint progress; the dead and departed sort after them.</summary>
    private static int StandingGroup(PerPlayerGamemodeInfo info)
        => info.Forfeited || info.WastedAt >= 0 ? 1 : 0;

    /// <summary>
    /// Within a group: running cars by race position, lower first; DNFs by death time, most
    /// recent first, so the tick is negated to sort that way ascending.
    /// </summary>
    private static int StandingWithin(PerPlayerGamemodeInfo info)
        => info.Forfeited || info.WastedAt >= 0 ? -Math.Max(info.WastedAt, 0) : info.Position;

    public override void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        base.OnClientEvent(clientId, payload);
        var e = TightPackSerializer.Deserialize<ClientToServerEvent>(payload);

        if (e is ConfirmFinishEvent confirmation)
        {
            if (_state == ServerGamemodeState.Finished && _provisional
                && confirmation.Revision == _finishRevision
                && Players.Any(player => player.Id == clientId && !player.IsBot)
                && _perPlayer.TryGetValue(clientId, out var info) && !info.Forfeited
                && !TryResumeRace())
            {
                _provisional = false;
                CompleteRace();
            }

            return;
        }

        // Leaving forfeits: the player is placed last and the race carries on for everyone
        // else. Without this the abandoned player stayed on the grid at their last relayed
        // position and the race could never end on its own.
        if (e is ForfeitEvent)
        {
            Forfeit(clientId);
            return;
        }

        if (_state == ServerGamemodeState.Finished) return;

        if (e is CheckpointEvent { Index: var cp, Lap: var lap, Placement: var placement })
        {
            if (!_perPlayer.TryGetValue(clientId, out var ps) || ps.Forfeited)
                return;

            // The client's numbers are only a hint. Accept one only when it advances the
            // race by exactly one checkpoint and the player's reported position is
            // actually at the checkpoint they claim - otherwise a client could declare
            // itself the winner by sending a large lap number.
            if (!IsPlausibleProgress(clientId, ps, cp, lap))
                return;

            ps.Lap = lap;
            ps.Checkpoint = cp;
            ps.Position = placement;

            // Wasting-only races ignore lap completion.
            if (lap >= CurrentStage.Nlaps && _constraint.Racing)
                EndRace(clientId);
        }
    }

    /// <summary>
    /// True when the claim advances the player by exactly one checkpoint and their last
    /// relayed position is actually at the checkpoint they claim to have reached.
    /// </summary>
    private bool IsPlausibleProgress(
        Guid playerId,
        PerPlayerGamemodeInfo ps,
        ushort claimedCheckpoint,
        byte claimedLap
    )
    {
        var checkpointCount = CurrentStage.Checkpoints.Count;
        if (checkpointCount == 0)
            return false;

        var currentTotal = ps.Checkpoint + ps.Lap * checkpointCount;
        var claimedTotal = claimedCheckpoint + claimedLap * checkpointCount;

        if (claimedTotal != currentTotal + 1)
            return false;

        if (claimedCheckpoint >= checkpointCount || claimedLap > CurrentStage.Nlaps)
            return false;

        // Without a relayed position yet there is nothing to corroborate the claim against.
        if (GetPlayerPosition(playerId) is not { } position)
            return false;

        // The client reports the checkpoint it is now heading for, so the crossing that sent
        // this event was the one before it - wrapping back to the last checkpoint on a lap.
        var crossed = claimedCheckpoint == 0 ? checkpointCount - 1 : claimedCheckpoint - 1;
        var checkpoint = CurrentStage.Checkpoints[crossed].Position;

        // A generous radius: this only has to reject a player who is nowhere near the
        // checkpoint they claim, not to police the exact line.
        const float radiusSq = 5000f * 5000f;
        var dx = (float)position.X - (float)checkpoint.X;
        var dz = (float)position.Z - (float)checkpoint.Z;

        return dx * dx + dz * dz <= radiusSq;
    }
}

[TightPackable]
[TightPackUnion(0, typeof(CheckpointEvent))]
[TightPackUnion(1, typeof(ForfeitEvent))]
[TightPackUnion(2, typeof(ConfirmFinishEvent))]
public abstract partial record ClientToServerEvent;

[TightPackable]
public partial record CheckpointEvent(ushort Index, byte Lap, int Placement, int Tick) : ClientToServerEvent;

/// <summary>A player left the race before finishing.</summary>
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

/// <summary>
/// The race's phase and how long the countdown has left. Sent as the baseline to a client that
/// attached mid-race, which is how a spectator learns the countdown already finished instead of
/// waiting for a start event that was broadcast before it connected.
/// </summary>
[TightPackable]
public partial record RaceStateEvent(bool InProgress, int Countdown) : ServerToClientEvent;

[TightPackable]
public partial record RaceFinishedEvent(int WinnerSlot, bool WastedVictory, bool LastCarStanding) : ServerToClientEvent
{
    public int Revision { get; set; }
}

[TightPackable]
public partial record RaceResumedEvent : ServerToClientEvent;
