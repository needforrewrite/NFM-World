using JetBrains.Annotations;
using TightPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Hud;
using f64Vector3 = FixedMathSharp.Vector3d;

[assembly: Gamemode(
    Name = "PvP",
    Description = "A standard PvP free-for-all gamemode."
)]

[assembly: GamemodeConfigProperty(
    Name = "constraint",
    Type = typeof(string),
    Label = "Type",
    Description = "The type of race to play."
)]

[assembly: GamemodeConfigPropertyOption("constraint", Label = "Racing", Value = "racing")]
[assembly: GamemodeConfigPropertyOption("constraint", Label = "Wasting", Value = "wasting")]
[assembly: GamemodeConfigPropertyOption("constraint", Label = "Both", Value = "both")]

[assembly: GamemodeConfigProperty(
    Name = "laps",
    Type = typeof(string),
    Label = "Laps",
    Description = "Laps to finish when checkpoints are enabled.",
    VisibleWhenProperty = "constraint",
    VisibleWhenValue = "wasting",
    InvertVisibility = true
)]
[assembly: GamemodeConfigPropertyOption("laps", Label = "Stage default", Value = "stage")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "1", Value = "1")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "2", Value = "2")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "3", Value = "3")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "4", Value = "4")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "5", Value = "5")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "6", Value = "6")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "7", Value = "7")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "8", Value = "8")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "9", Value = "9")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "10", Value = "10")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "11", Value = "11")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "12", Value = "12")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "13", Value = "13")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "14", Value = "14")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "15", Value = "15")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "16", Value = "16")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "17", Value = "17")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "18", Value = "18")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "19", Value = "19")]
[assembly: GamemodeConfigPropertyOption("laps", Label = "20", Value = "20")]

[assembly: GamemodeConfigProperty(Name = "fixing", Type = typeof(string), Label = "Fix hoops", Description =
    "How many repairs each hoop provides.")]
[assembly: GamemodeConfigPropertyOption("fixing", Label = "Unlimited", Value = "unlimited")]
[assembly: GamemodeConfigPropertyOption("fixing", Label = "Limited", Value = "limited")]
[assembly: GamemodeConfigPropertyOption("fixing", Label = "No Fixing", Value = "none")]

[assembly: GamemodeConfigProperty(
    Name = "fix_limit",
    Type = typeof(int),
    Label = "Uses per hoop (1–10)",
    Description = "Shared repairs available at each fix hoop.",
    VisibleWhenProperty = "fixing",
    VisibleWhenValue = "limited"
)]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "3", Value = "3")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "1", Value = "1")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "2", Value = "2")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "4", Value = "4")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "5", Value = "5")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "6", Value = "6")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "7", Value = "7")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "8", Value = "8")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "9", Value = "9")]
[assembly: GamemodeConfigPropertyOption("fix_limit", Label = "10", Value = "10")]

[assembly: GamemodeConfigProperty(
    Name = "walls",
    Type = typeof(string),
    Label = "Converging walls",
    Description = "Minutes until the stage walls start closing in. Off by default."
)]

[assembly: GamemodeConfigPropertyOption("walls", Label = "Off", Value = "off")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "1 min", Value = "1")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "2 min", Value = "2")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "3 min", Value = "3")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "4 min", Value = "4")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "5 min", Value = "5")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "6 min", Value = "6")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "7 min", Value = "7")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "8 min", Value = "8")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "9 min", Value = "9")]
[assembly: GamemodeConfigPropertyOption("walls", Label = "10 min", Value = "10")]

namespace NFMWorld.Gamemode.Pvp;

internal static class PvpRules
{
    public static int FixesPerHoop(IReadOnlyDictionary<string, object>? config)
        => config?.GetValueOrDefault("fixing")?.ToString() switch
        {
            "limited" => int.TryParse(config?.GetValueOrDefault("fix_limit")?.ToString(), out var limit)
                         && limit is >= 1 and <= 10 ? limit : 3,
            "none" => 0,
            _ => -1
        };

    public static ushort Laps(BackendStage stage, RaceConstraint constraint, IReadOnlyDictionary<string, object>? config)
        => !constraint.Racing ? (ushort)0
            : ushort.TryParse(config?.GetValueOrDefault("laps")?.ToString(), out var laps) && laps is >= 1 and <= 20
                ? laps : stage.StageLoader.nlaps;
}

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
    private int _fixesPerHoop;
    private int[] _hoopContacts = [];
    private int[] _respawnTicks = [];

    private ConvergingWalls _walls = ConvergingWalls.Off;
    private bool _wallsBegun;

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

    /// <summary>PvP is a scored race, so the host's standard completion UI applies.</summary>
    public override bool UsesStandardRaceCompletion => true;
    public override bool RespawnsWastedCars => _constraint.Racing && !_constraint.Wasting;
    public override string? ActiveRaceWarning => _state == GamemodeState.InProgress && _wallsBegun
                                                 && ClientPlayer?.Car is { HasLeftRace: false } car
                                                 && !CurrentStage.Walls.IsInBounds(car.Position.X, car.Position.Z)
            ? "Get back inside the stage"
            : null;

    private void SetupCars()
    {
        for (var i = 0; i < Players.Count; i++)
        {
            var player = Players[i];

            var start = CurrentStage.GetStartTransform(i, Players.Count);

            var car = new BackendCar(player.Info, i, start.Position.X, start.Position.Z);
            car.Position = new f64Vector3(start.Position.X, start.Position.Y - car.GroundAt, start.Position.Z);
            car.Rotation = start.Rotation;
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
        _constraint = RaceConstraint.From(Config);
        CurrentStage.Nlaps = PvpRules.Laps(CurrentStage, _constraint, Config);
        _fixesPerHoop = PvpRules.FixesPerHoop(Config);
        for (var i = 0; i < CurrentStage.FixHoops.Count; i++)
            CurrentStage.SetFixHoopActive(i, _fixesPerHoop != 0);
        _hoopContacts = new int[Players.Count];
        Array.Fill(_hoopContacts, -1);
        _respawnTicks = new int[Players.Count];
        SetupCars();
        _state = GamemodeState.Countdown;
        _countdown = 3;
        _inner = GamemodeUtils.CountdownInterval;
        _clientTick = 0;
        _receivedFinish = false;
        _finishRevision = 0;
        _survivalFinish = false;
        _walls = ConvergingWalls.From(Config);
        _wallsBegun = false;
        HudState.StateText = null;
        HudState.ResultTitle = null;
        HudState.StateTextEndsAt = null;
        HudState.Finished = false;
        HudState.ClientWon = false;
        HudState.ClientLost = false;
        HudState.TotalLaps = CurrentStage.Nlaps;
        HudState.Lap = CurrentStage.Nlaps > 0 ? 1 : 0;
    }

    public override void End()
    {
        for (var i = 0; i < CurrentStage.FixHoops.Count; i++)
            CurrentStage.SetFixHoopActive(i, true);
        CurrentStage.Nlaps = CurrentStage.StageLoader.nlaps;
        base.End();
    }

    public override void Reset()
    {
        base.Reset();
        Begin();
    }

    public override void Forfeit() => SendServerEvent<ClientToServerEvent>(new ForfeitEvent());

    private void TickWalls()
    {
        if (!_walls.Enabled || !CurrentStage.Walls.Enabled) return;

        if (!_wallsBegun)
        {
            CurrentStage.Walls.Begin(_walls.Minutes, CurrentStage.StageLoader);
            _wallsBegun = true;
        }

        var elapsed = _clientTick / (double)Physics.TargetTps / 60.0;
        CurrentStage.Walls.Update(elapsed);
    }

    public override void ConfirmFinish()
    {
        if (_state == GamemodeState.Finished && ClientPlayer is not null)
            SendServerEvent<ClientToServerEvent>(new ConfirmFinishEvent(_finishRevision));
    }

    private bool Owns(ClientSidePlayer player)
        => ClientPlayer is { } client && (player == client || player.Info.IsBot
                                          && (player.Info.OwnerId == client.Info.Id || !IsMultiplayer && player.Info.OwnerId
                                              == Guid.Empty));

    private void TickFixHoop(ClientSidePlayer player, BackendCar car)
    {
        if (_fixesPerHoop < 0)
        {
            FixHoopHelper.HandleFixHoops(CurrentStage, car);
            return;
        }

        if (_fixesPerHoop == 0 || !Owns(player) || car.HasLeftRace) return;
        var hoop = FixHoopHelper.FindFixHoop(CurrentStage, car.Position);
        var previous = _hoopContacts[player.Index];
        _hoopContacts[player.Index] = hoop;
        if (hoop >= 0 && hoop != previous && !car.IsFixing && (car.Wasted || car.CarPhysics.DamagePoints > 0))
            SendServerEvent<ClientToServerEvent>(new UseFixHoopEvent(player.Index, hoop));
    }

    private void TickRespawn(int slot, BackendCar car)
    {
        if (!car.Wasted || car.HasLeftRace)
        {
            _respawnTicks[slot] = 0;
            return;
        }

        if (++_respawnTicks[slot] >= Math.Ceiling(3 * Physics.TargetTps))
        {
            _respawnTicks[slot] = 0;
            car.RespawnInPlace();
        }
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
                TickWalls();
                _physics.GameTick();

                var crossedSlots = 0;
                foreach (var player in Players)
                {
                    if (player.Car is not { } car) continue;
                    TickFixHoop(player, car);
                    if (!Owns(player)) continue;
                    if (RespawnsWastedCars) TickRespawn(player.Index, car);
                    if (_constraint.Racing && !car.Wasted && !car.HasLeftRace
                        && CheckPointHelper.HandleCheckPoint(CurrentStage, car))
                        crossedSlots |= 1 << player.Index;
                }

                // After the crossings, so a car that just took a checkpoint is ranked on it.
                // Nothing computed the order in a race, so every car stayed at Placement 0 and
                // the HUD showed the whole field as first. Only the client player's car is not
                // driven by this client's physics; the rest are, but a bot's own rank pass
                // writes the same permutation and the race must own the order, not the bots.
                if (Players.Count > 1)
                    CheckPointHelper.CalculatePositions(CurrentStage, Players);

                foreach (var player in Players)
                {
                    if ((crossedSlots & (1 << player.Index)) == 0 || player.Car is not { } crossedCar) continue;
                    var progress = new CheckpointEvent(
                        crossedCar.CurrentCheckpoint,
                        crossedCar.CurrentLap,
                        crossedCar.Placement,
                        _clientTick
                    );
                    SendServerEvent<ClientToServerEvent>(player.Info.IsBot
                        ? new BotCheckpointEvent(player.Index, progress) : progress);
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

                if (winner is not null && _constraint.Racing && !IsMultiplayer)
                    Finish(winner);
                break;
            case GamemodeState.Finished:
                _physics.GameTick();
                if (_survivalFinish)
                    foreach (var player in Players)
                        if (player.Car is { } car && Owns(player))
                            TickFixHoop(player, car);
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
            case WallWarningEvent warning:
                AnnounceRaceNotice(
                    warning.Seconds == 0 ? "The walls are closing in!" : $"{warning.Seconds} seconds before the walls close!",
                    warning.Seconds == 0 ? HudMessageKind.Danger : HudMessageKind.Warning,
                    4f
                );
                PlayWallTimerNotification();
                break;
            case FinishCheckpointSoundEvent checkpoint:
                PlayFinishCheckpointSound(checkpoint.Index);
                break;

            // The baseline. A race already under way must run its physics: no car is driven
            // otherwise, and the camera yaw Drive computes stays at zero.
            case RaceStateEvent state:
                _state = state.InProgress ? GamemodeState.InProgress : GamemodeState.Countdown;
                _countdown = state.Countdown;
                HudState.CountdownTimer = state.Countdown;
                break;
            case FixHoopSnapshotEvent snapshot:
                for (var i = 0; i < Math.Min(snapshot.Remaining.Length, CurrentStage.FixHoops.Count); i++)
                    if (snapshot.Remaining[i] == 0) CurrentStage.SetFixHoopActive(i, false);
                Apply(TightPackSerializer.Deserialize<ServerToClientEvent>(snapshot.RaceState));
                break;
            case FixHoopUsedEvent use:
                if (Players[use.RacerSlot].Car is { } repairedCar) repairedCar.Fix();
                if (use.Remaining == 0) CurrentStage.SetFixHoopActive(use.Hoop, false);
                AnnounceRaceNotice(use.Remaining == 0
                    ? "Fix hoops have expired!"
                    : $"Fix hoop uses left: {use.Remaining}");
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

        StandardRace.FinishHud(
            HudState,
            ClientPlayer,
            winner,
            wonMessage: wastedVictory
                ? "You won, all other cars have been wasted!"
                : lastCarStanding ? "You won, you're the last car standing!" : null,
            lostMessage: lastCarStanding ? $"{winner?.Info.PlayerName} is the last car standing, race over!" : null,
            wonTitle: wastedVictory ? "YOU WASTED 'EM!" : null
        );
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
        public int NextFixTick;

        /// <summary>Server tick the car was last seen wasted, or -1 while it is alive.</summary>
        public int WastedAt = -1;
    }

    private ServerGamemodeState _state;
    private readonly Dictionary<Guid, PerPlayerGamemodeInfo> _perPlayer = [];
    private int _inner;
    private int _countdown;

    /// <summary>Server ticks since the race started; DNF order is keyed on it.</summary>
    private int _serverTick;
    private static readonly int[] WallWarnings = [60, 30, 10, 0];
    private double _wallCountdownSeconds;
    private int _wallTicks;
    private int _nextWallWarning;

    private RaceConstraint _constraint = RaceConstraint.Both;

    private int[] _remainingFixes = [];
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
        _wallCountdownSeconds = CurrentStage.Walls.Enabled ? ConvergingWalls.From(Config).Minutes * 60 : 0;
        _nextWallWarning = 0;
        CurrentStage.Nlaps = PvpRules.Laps(CurrentStage, _constraint, Config);
        var fixesPerHoop = PvpRules.FixesPerHoop(Config);
        _remainingFixes = fixesPerHoop > 0 ? new int[CurrentStage.FixHoops.Count] : [];
        Array.Fill(_remainingFixes, fixesPerHoop);
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

        ServerToClientEvent state = _state == ServerGamemodeState.Finished
            ? new RaceFinishedEvent(_winnerSlot, _wastedVictory, _lastCarStanding) { Revision = _finishRevision }
            : new RaceStateEvent(
                InProgress: _state == ServerGamemodeState.InProgress,
                Countdown: _state == ServerGamemodeState.Countdown ? _countdown : 0
            );
        var payload = TightPackSerializer.Serialize(state);
        return _remainingFixes.Length == 0 ? payload
            : TightPackSerializer.Serialize<ServerToClientEvent>(new FixHoopSnapshotEvent(payload, _remainingFixes));
    }

    public override void StartRace()
    {
        base.StartRace();
        _state = ServerGamemodeState.Countdown;
        _countdown = 3;
        _inner = GamemodeUtils.ServerCountdownInterval;
        _serverTick = 0;
        _wallTicks = 0;
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
                        BroadcastWallWarnings();
                    }
                }

                break;
            case ServerGamemodeState.InProgress:
                _serverTick++;
                _wallTicks++;
                BroadcastWallWarnings();
                TrackWasted();
                CheckConstraint();
                break;
            case ServerGamemodeState.Finished when _provisional:
                _serverTick++;
                TryResumeRace();
                break;
        }
    }

    private void BroadcastWallWarnings()
    {
        if (_wallCountdownSeconds <= 0 || _nextWallWarning == WallWarnings.Length) return;

        var remaining = _wallCountdownSeconds - _wallTicks / TicksPerSecond;
        while (_nextWallWarning < WallWarnings.Length && remaining <= WallWarnings[_nextWallWarning])
        {
            var seconds = WallWarnings[_nextWallWarning++];
            if (seconds <= _wallCountdownSeconds)
                BroadcastEvent<ServerToClientEvent>(new WallWarningEvent(seconds));
        }
    }

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
    /// The match lobby's live order follows validated checkpoint progress and the latest positions.
    /// </summary>
    public override RaceLiveState? GetLiveState()
    {
        if (_state is ServerGamemodeState.WaitingToStart or ServerGamemodeState.Countdown)
            return null;

        return new RaceLiveState
        {
            Standings =
            [
                .. _perPlayer
                .OrderBy(kv => LiveOrder(kv.Key, kv.Value))
                .Select((kv, index) =>
                    {
                        var player = Players.FirstOrDefault(candidate => candidate.Id == kv.Key);
                        return new RaceLiveStanding
                        {
                            PlayerId = kv.Key,
                            PlayerName = player.PlayerName ?? "Player",
                            CarName = player.CarName ?? "",
                            Color = player.Color,
                            IsBot = player.IsBot,
                            Position = index,
                            Lap = kv.Value.Lap,
                            Checkpoint = kv.Value.Checkpoint,
                            Wasted = kv.Value.WastedAt >= 0,
                            Forfeited = kv.Value.Forfeited
                        };
                    }
                )
            ]
        };
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

    private (int Group, int Progress, double Distance, int TieBreak) LiveOrder(Guid playerId, PerPlayerGamemodeInfo info)
    {
        if (StandingGroup(info) != 0) return (1, 0, 0, StandingWithin(info));
        if (!_constraint.Racing) return (0, 0, 0, info.Position);

        var checkpoints = CurrentStage.Checkpoints;
        var progress = info.Lap * checkpoints.Count + info.Checkpoint;
        if (info.Checkpoint >= checkpoints.Count || GetPlayerPosition(playerId) is not { } position)
            return (0, -progress, double.PositiveInfinity, info.Position);

        var target = checkpoints[info.Checkpoint].Position;
        var dx = (double)position.X - (double)target.X;
        var dz = (double)position.Z - (double)target.Z;
        return (0, -progress, dx * dx + dz * dz, info.Position);
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

        if (e is UseFixHoopEvent use && (_state == ServerGamemodeState.InProgress || _provisional))
        {
            UseFixHoop(clientId, use);
            return;
        }

        if (_state != ServerGamemodeState.InProgress || !_constraint.Racing) return;
        if (e is CheckpointEvent progress) RecordCheckpoint(clientId, progress);
        if (e is BotCheckpointEvent bot) RecordCheckpoint(OwnedRacer(clientId, bot.RacerSlot), bot.Progress);
    }

    private void RecordCheckpoint(Guid racer, CheckpointEvent progress)
    {
        if (racer == Guid.Empty || !_perPlayer.TryGetValue(racer, out var info) || info.Forfeited
            || !IsPlausibleProgress(racer, info, progress.Index, progress.Lap)) return;

        info.Lap = progress.Lap;
        info.Checkpoint = progress.Index;
        info.Position = progress.Placement;
        if (progress.Lap == CurrentStage.Nlaps - 1 && CurrentStage.Checkpoints.Count > 1
            && progress.Index == CurrentStage.Checkpoints.Count - 1 && Random.Shared.Next(100) == 0)
            BroadcastEvent<ServerToClientEvent>(new FinishCheckpointSoundEvent((ushort)(progress.Index - 1)));

        if (info.Lap >= CurrentStage.Nlaps) EndRace(racer);
    }

    private Guid OwnedRacer(Guid sender, int slot)
    {
        if ((uint)slot >= (uint)Players.Count) return Guid.Empty;
        var racer = Players[slot];
        return racer.Id == sender || racer.IsBot && racer.OwnerId == sender ? racer.Id : Guid.Empty;
    }

    private void UseFixHoop(Guid sender, UseFixHoopEvent use)
    {
        if ((uint)use.Hoop >= (uint)_remainingFixes.Length || _remainingFixes[use.Hoop] == 0) return;
        var racer = OwnedRacer(sender, use.RacerSlot);
        if (racer == Guid.Empty || !_perPlayer.TryGetValue(racer, out var info) || info.Forfeited
            || _serverTick < info.NextFixTick || GetPlayerPosition(racer) is not { } position
            || !FixHoopHelper.IsInRange(CurrentStage.FixHoops[use.Hoop], position, 1500)) return;

        info.NextFixTick = _serverTick + GamemodeUtils.ServerTps / 2;
        var remaining = --_remainingFixes[use.Hoop];
        BroadcastEvent<ServerToClientEvent>(new FixHoopUsedEvent(use.RacerSlot, use.Hoop, remaining));
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
[TightPackUnion(3, typeof(UseFixHoopEvent))]
[TightPackUnion(4, typeof(BotCheckpointEvent))]
public abstract partial record ClientToServerEvent;

[TightPackable]
public partial record CheckpointEvent(ushort Index, byte Lap, int Placement, int Tick) : ClientToServerEvent;

[TightPackable]
public partial record BotCheckpointEvent(int RacerSlot, CheckpointEvent Progress) : ClientToServerEvent;

[TightPackable]
public partial record UseFixHoopEvent(int RacerSlot, int Hoop) : ClientToServerEvent;

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
[TightPackUnion(4, typeof(FixHoopUsedEvent))]
[TightPackUnion(5, typeof(FixHoopSnapshotEvent))]
[TightPackUnion(6, typeof(WallWarningEvent))]
[TightPackUnion(7, typeof(FinishCheckpointSoundEvent))]
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

[TightPackable]
public partial record FixHoopUsedEvent(int RacerSlot, int Hoop, int Remaining) : ServerToClientEvent;

[TightPackable]
public partial record FixHoopSnapshotEvent(byte[] RaceState, int[] Remaining) : ServerToClientEvent;

[TightPackable]
public partial record WallWarningEvent(int Seconds) : ServerToClientEvent;

[TightPackable]
public partial record FinishCheckpointSoundEvent(ushort Index) : ServerToClientEvent;
