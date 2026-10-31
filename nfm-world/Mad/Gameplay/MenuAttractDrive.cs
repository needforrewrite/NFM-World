using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

internal sealed class MenuAttractDrive : IDisposable
{
    private readonly ObservableUnlimitedArray<BackendCar> _sceneCars;
    private AttractGamemode? _gamemode;

    public MenuAttractDrive(ClientStage stage, ObservableUnlimitedArray<BackendCar> sceneCars, MenuRoadSegment road)
    {
        _sceneCars = sceneCars;
        if (stage.Backend.Nodes.Count < 2 || stage.Backend.Checkpoints.Count == 0
            || !BackendGameSparker.cars.TryGetValue(Collection.NFMM, out var models)) return;
        var random = new System.Random();
        var selected = models.Where(model => model.Wheels.Length > 0)
            .OrderBy(_ => random.Next()).Take(6).ToArray();
        if (selected.Length < 2) return;
        var nextCheckpoint = 0;
        while (nextCheckpoint < stage.Backend.Checkpoints.Count - 1
            && stage.Backend.Nodes.IndexOf(stage.Backend.Checkpoints[nextCheckpoint]) <= road.StartNode)
            nextCheckpoint++;
        var side = Vector3.Normalize(new Vector3(road.Direction.Z, 0, -road.Direction.X));
        var heading = -MathF.Atan2(road.Direction.X, road.Direction.Z) * 180 / MathF.PI;
        var players = selected.Select((model, i) => new ClientSidePlayerInfo($"Attract {i + 1}",
            model.FileName, default, true, i == 0)).ToArray();
        _gamemode = new AttractGamemode(new ClientGamemodeParameters { Players = players }, new AttractContext(stage));
        try
        {
            foreach (var player in _gamemode.Players)
            {
                var i = player.Index;
                var lateral = (i % 2 == 0 ? -290 : 290) + random.Next(-110, 111);
                var along = 600 + (i / 2) * 750 + random.Next(-150, 151);
                var point = road.Start + road.Direction * along + side * lateral;
                var car = new BackendCar(player.Info, i, (fix64)point.X, (fix64)point.Z);
                car.Position = new f64Vector3((fix64)point.X, (fix64)point.Y - car.GroundAt, (fix64)point.Z);
                car.CurrentCheckpoint = (ushort)nextCheckpoint;
                car.TotalCheckpoint = nextCheckpoint;
                car.LastCheckpointNode = Math.Max(-1, road.StartNode - 1);
                car.Rotation = new f64Euler(f64AngleSingle.FromDegrees((fix64)(heading + random.Next(-5, 6))),
                    f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                player.Car = car;
                sceneCars.Add(car);
                if (stage.GetCarVisual(car).Sfx is { } sfx)
                    sfx.Mute = true;
            }
            _gamemode.Begin();
            if (_gamemode.Players.All(player => player.Bot == null))
                Dispose();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Tick()
    {
        _gamemode?.GameTick();
    }

    public void Dispose()
    {
        var gamemode = _gamemode;
        _gamemode = null;
        if (gamemode == null) return;
        foreach (var player in gamemode.Players)
        {
            if (player.Car is { } car)
                _sceneCars.Remove(car);
            player.Car = null;
        }
        gamemode.End();
        gamemode.Players.Clear();
    }

    private sealed class AttractGamemode : BaseClientGamemode
    {
        private readonly PhysicsController _physics;
        public override string GamemodeId => "menu/attract";

        public AttractGamemode(ClientGamemodeParameters parameters, IGamemodeContext context) : base(parameters, context)
        {
            _physics = new PhysicsController(Players, CurrentStage);
        }

        public override void Begin()
        {
            foreach (var player in Players)
                AttachBot("el_stupido", player);
        }

        public override void GameTick()
        {
            CheckPointHelper.CalculatePositions(CurrentStage, Players);
            _physics.GameTick();
            foreach (var player in Players)
            {
                if (player.Car is not { } car) continue;
                CheckPointHelper.HandleCheckPoint(CurrentStage, car);
                FixHoopHelper.HandleFixHoops(CurrentStage, car);
            }
        }
    }

    private sealed class AttractContext(ClientStage stage) : IGamemodeContext, IClientCallbacks
    {
        public BackendStage CurrentStage => stage.Backend;
        public RaceState RaceState => NFMWorldLibrary.RaceState.InProgress;
        public IClientCallbacks ClientCallbacks => this;
        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => stage.GetCarVisual(car);
        public void ResetCheckpointGlow() { }
        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish) { }
        public void SendServerEvent(ReadOnlySpan<byte> payload) { }
        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players) { }
    }
}
