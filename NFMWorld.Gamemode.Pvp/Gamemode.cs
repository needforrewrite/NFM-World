using JetBrains.Annotations;
using MemoryPack;
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
            var car = new BackendCar(player.Info, i, -500 + (400 * i), 0);
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
        // A spectator joins a race already under way, so the server's start event was broadcast
        // before it attached and will never arrive. Left counting down it sat on 3-2-1 forever,
        // and a gamemode that is not racing never drives a car - so the camera yaw Drive
        // computes (Cxz, StaticCameraXz) stayed at zero and the view faced the wrong way.
        _state = ClientPlayer is null ? GamemodeState.InProgress : GamemodeState.Countdown;
        _countdown = 3;
        _inner = GamemodeUtils.CountdownInterval;
        _clientTick = 0;
        _constraint = RaceConstraint.From(Config);
        HudState.StateText = null;
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
                        if (CheckPointHelper.HandleCheckPoint(CurrentStage, player.Car) && player == ClientPlayer)
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

                // A wasting-only race has no line to win: the last car alive wins instead, which
                // the server reports.
                if (winner is not null && _constraint.Racing)
                    Finish(winner);
                break;
            case GamemodeState.Finished:
                _physics.GameTick();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void OnServerEvent(ReadOnlySpan<byte> payload)
    {
        base.OnServerEvent(payload);
        var ev = MemoryPackSerializer.Deserialize<ServerToClientEvent>(payload);
        if (ev is CountdownGoEvent)
        {
            HudState.CountdownTimer = 0;
            _state = GamemodeState.InProgress;
        }
    }

    public override void OnServerRaceFinished(RaceResults results)
    {
        base.OnServerRaceFinished(results);
        Finish();
    }

    /// <summary>
    /// The race is over: every car coasts to a stop and the HUD stops advancing laps.
    /// </summary>
    private void Finish(ClientSidePlayer? winner = null)
    {
        if (_state == GamemodeState.Finished)
            return;

        _state = GamemodeState.Finished;
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
                ? "You finished first, nice job!"
                : $"{winner.Info.PlayerName} finished first, race over!";
            HudState.ClientWon = clientWon;
            HudState.ClientLost = !spectating && !clientWon;
        }
        else
        {
            HudState.StateText = "Finished!";
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

    public override void Begin()
    {
        base.Begin();
        _perPlayer.Clear();
        _inner = GamemodeUtils.ServerCountdownInterval;
        _countdown = 3;
        _state = ServerGamemodeState.WaitingToStart;
        _constraint = RaceConstraint.From(Config);

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
        }
    }

    /// <summary>
    /// Ends the race when the chosen constraint is satisfied: wasting ignores racing, so it ends
    /// once at most one car is still alive, and racing ignores deaths, so it ends on the line
    /// (handled in the checkpoint event).
    /// </summary>
    private void CheckConstraint()
    {
        // A solo session has nobody to waste; the last car alive is trivially the only car.
        if (!_constraint.Wasting || _perPlayer.Count <= 1)
            return;

        var alive = 0;
        foreach (var info in _perPlayer.Values)
        {
            if (!info.Forfeited && info.WastedAt < 0)
                alive++;
        }

        if (alive <= 1)
            EndRace();
    }

    private void EndRace()
    {
        if (_state == ServerGamemodeState.Finished)
            return;

        _state = ServerGamemodeState.Finished;
        CompleteRace();
    }

    /// <summary>
    /// Records when each car was last seen wasted, so the standings can order DNFs by who died
    /// most recently. Wasted is the owner's own report, relayed with the car state.
    /// </summary>
    private void TrackWasted()
    {
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
                    info.WastedAt = -1;
                    break;
            }
        }
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
                    .OrderBy(kv => StandingGroup(kv.Value))
                    .ThenBy(kv => StandingWithin(kv.Value))
                    .Select((kv, index) => new RaceStanding
                        {
                            PlayerId = kv.Key,
                            FinishPosition = index,
                            FinishTime = TimeSpan.Zero
                        }
                    )
            ]
        );
    }

    /// <summary>
    /// A client's connection went away. A player who quits drops its transport instead of
    /// sending ForfeitEvent, so this is that same departure arriving by another route.
    /// </summary>
    public override void OnClientDisconnected(Guid clientId) => Forfeit(clientId);

    /// <summary>Places a departing player last; the race ends only once every player has gone.</summary>
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
        var e = MemoryPackSerializer.Deserialize<ClientToServerEvent>(payload);

        // Leaving forfeits: the player is placed last and the race carries on for everyone
        // else. Without this the abandoned player stayed on the grid at their last relayed
        // position and the race could never end on its own.
        if (e is ForfeitEvent)
        {
            Forfeit(clientId);
            return;
        }

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

            // a racing constraint does not end the race on the line, so the ranking
            // is left to the wasting rule instead.
            if (lap >= CurrentStage.Nlaps && _constraint.Racing)
                EndRace();
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

[MemoryPackable]
[MemoryPackUnion(0, typeof(CheckpointEvent))]
[MemoryPackUnion(1, typeof(ForfeitEvent))]
public abstract partial record ClientToServerEvent;

[MemoryPackable]
public partial record CheckpointEvent(ushort Index, byte Lap, int Placement, int Tick) : ClientToServerEvent;

/// <summary>A player left the race before finishing.</summary>
[MemoryPackable]
public partial record ForfeitEvent : ClientToServerEvent;

[MemoryPackable]
[MemoryPackUnion(0, typeof(CountdownGoEvent))]
public abstract partial record ServerToClientEvent;

[MemoryPackable]
public partial record CountdownGoEvent : ServerToClientEvent;
