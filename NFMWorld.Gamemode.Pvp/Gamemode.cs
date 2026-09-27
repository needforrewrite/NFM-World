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
                            SendServerEvent(new CheckpointEvent(player.Car.CurrentCheckpoint, player.Car.CurrentLap, player.Car.Placement, _clientTick));
                        }
                    }
                }

                _clientTick++;

                if (ClientPlayer.Car != null)
                {
                    UpdateHudAndSounds(ClientPlayer.Car);
                    HudState.Lap = ClientPlayer.Car.CurrentLap + 1;
                    HudState.TotalLaps = CurrentStage.Nlaps;
                    HudState.Position = ClientPlayer.Car.Placement + 1;
                    HudState.TotalRacers = Players.Count;
                }
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
}

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
                        BroadcastEvent(new CountdownGoEvent());
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
            var ps = _perPlayer[clientId];
            
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