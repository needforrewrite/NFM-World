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

    private PhysicsController _physics;

    public PvpGamemode(ClientGamemodeParameters clientGamemodeParameters,
        IGamemodeContext gamemodeContext,
        string gamemodeId,
        Dictionary<string, object>? config = null) : base(clientGamemodeParameters, gamemodeContext, gamemodeId, config)
    {
        _physics = new PhysicsController(Players, CurrentStage);
        // TODO enforce constraint
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

            if (player.Info.IsBot)
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
                
                foreach (var player in Players)
                {
                    if (player.Car != null)
                    {
                        FixHoopHelper.HandleFixHoops(CurrentStage, player.Car);
                        if (CheckPointHelper.HandleCheckPoint(CurrentStage, player.Car) && player == ClientPlayer)
                        {
                            SendServerEvent<ClientToServerEvent>(new CheckpointEvent(player.Car.CurrentCheckpoint, player.Car.CurrentLap, player.Car.Placement, _clientTick));
                        }
                    }
                }

                _clientTick++;

                if (ClientPlayer.Car != null)
                {
                    UpdateHudAndSounds(ClientPlayer.Car);
                    HudState.Lap = Math.Min(ClientPlayer.Car.CurrentLap + 1, CurrentStage.Nlaps);
                    HudState.TotalLaps = CurrentStage.Nlaps;
                    HudState.Position = ClientPlayer.Car.Placement + 1;
                    HudState.TotalRacers = Players.Count;
                }

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

                if (winner is not null)
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
            var clientWon = winner.Info.IsClientPlayer;
            HudState.StateText = clientWon
                ? "You finished first, nice job!"
                : $"{winner.Info.PlayerName} finished first, race over!";
            HudState.ClientWon = clientWon;
            HudState.ClientLost = !clientWon;
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
    Dictionary<string, object>? config = null)
    : PluginServerGamemode(parameters, context, gamemodeId, config)
{
    private class PerPlayerGamemodeInfo
    {
        public byte Lap;
        public ushort Checkpoint;
        public int Position;
    }

    private ServerGamemodeState _state;
    private readonly Dictionary<Guid, PerPlayerGamemodeInfo> _perPlayer = [];
    private int _inner;
    private int _countdown;

    public override void Begin()
    {
        base.Begin();
        _perPlayer.Clear();
        _inner = GamemodeUtils.CountdownInterval;
        _countdown = 3;
        _state = ServerGamemodeState.WaitingToStart;
        
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
        _inner = GamemodeUtils.CountdownInterval;
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
                    _inner = GamemodeUtils.CountdownInterval;
                    _countdown--;
                    if (_countdown <= 0)
                    {
                        _state = ServerGamemodeState.InProgress;
                        BroadcastEvent<ServerToClientEvent>(new CountdownGoEvent());
                    }
                }
                break;
        }
    }

    public void CompleteRace()
    {
        FinishRace(TimeSpan.Zero, [
            ..Players.Select(p => new RaceStanding
            {
                PlayerId = p.Id,
                FinishPosition = _perPlayer[p.Id].Position,
                FinishTime = TimeSpan.Zero
            })
        ]);
    }

    public override void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        base.OnClientEvent(clientId, payload);
        var e = MemoryPackSerializer.Deserialize<ClientToServerEvent>(payload);
        if (e is CheckpointEvent { Index: var cp, Lap: var lap, Placement: var placement })
        {
            if (!_perPlayer.TryGetValue(clientId, out var ps))
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

            if (lap >= CurrentStage.Nlaps && _state != ServerGamemodeState.Finished)
            {
                _state = ServerGamemodeState.Finished;
                CompleteRace();
            }
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
        byte claimedLap)
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
public abstract partial record ClientToServerEvent;

[MemoryPackable]
public partial record CheckpointEvent(ushort Index, byte Lap, int Placement, int Tick) : ClientToServerEvent;

[MemoryPackable]
[MemoryPackUnion(0, typeof(CountdownGoEvent))]
public abstract partial record ServerToClientEvent;

[MemoryPackable]
public partial record CountdownGoEvent : ServerToClientEvent;