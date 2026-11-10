using MemoryPack;
using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using f64Euler = NFMWorldLibrary.FixedMath.f64Euler;
using f64Vector3 = FixedMathSharp.Vector3d;

namespace NFMWorld.Library.Test;

[TestClass]
public class TimeTrialMultiplayerTests
{
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [ClassInitialize]
    public static void InitSentry(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
                {
                    options.Dsn = "https://public@localhost/1";
                    options.TracesSampleRate = 0;
                }
            );
        }
        catch (InvalidOperationException)
        {
            // Sentry is shared with the other physics tests.
        }
    }

    private static BackendStage CreateStage(int checkpoints = 2)
    {
        var stage = new BackendStage("timetrial-test", new StageLoader { nlaps = 1 });
        for (var i = 0; i < checkpoints; i++)
            stage.Checkpoints.Add(
                StageObject.CreateDefaultObject(
                    new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "checkpoint"),
                    new f64Vector3(100_000 + i * 20_000, World.Ground, 0),
                    f64Euler.Identity,
                    PiecePlacementType.CheckPoint,
                    AiNodeKind.CheckPoint
                )
            );
        return stage;
    }

    private static BaseGamemodeFactory CreateFactory(bool simulation = false)
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        return GamemodeFactory.Create("nfmm/timetrial", new Dictionary<string, object> { ["simulation"] = simulation });
    }

    private static byte[] Event(BaseGamemodeFactory factory, string name, params object[] args)
    {
        var type = ((NativeGamemodeFactory)factory).Plugin.Assembly.GetExportedTypes().Single(t => t.Name == name);
        var instance = Activator.CreateInstance(type, args);
        return MemoryPackSerializer.Serialize(name == "RaceStateEvent" ? type : type.BaseType!, instance);
    }

    private sealed class CarCallbacks : IClientCarCallbacks
    {
        public bool CastsShadow { get; set; }
        public bool? GetsShadowed { get; set; }
        public float? AlphaOverride { get; set; }
        public bool? Glow { get; set; }
        public bool? Finish { get; set; }
    }

    private sealed class ClientContext(BackendStage stage, bool multiplayer) : IGamemodeContext, IClientCallbacks
    {
        public BackendStage CurrentStage => stage;
        public RaceState RaceState => RaceState.InProgress;
        public bool IsMultiplayer => multiplayer;
        public IClientCallbacks ClientCallbacks => this;
        public Dictionary<BackendCar, CarCallbacks> Cars { get; } = [];
        public List<byte[]> Sent { get; } = [];
        public int Restarts { get; private set; }
        public void RaceRestarted() => Restarts++;

        public void ResetCheckpointGlow()
        {
        }

        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
        {
        }

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car)
        {
            if (!Cars.TryGetValue(car, out var callbacks))
                Cars[car] = callbacks = new CarCallbacks();
            return callbacks;
        }

        public void SendServerEvent(ReadOnlySpan<byte> payload) => Sent.Add(payload.ToArray());

        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        {
        }
    }

    private sealed class ClientRace : IDisposable
    {
        private readonly string _carName = $"timetrial-test/{Guid.NewGuid():N}";
        public BaseGamemodeFactory Factory { get; }
        public BaseClientGamemode Client { get; }
        public ClientContext Context { get; }

        public ClientRace(int localSlot = 1, bool simulation = false, int checkpoints = 2, bool multiplayer = true)
        {
            BackendGameSparker.dynamic_models.Add(
                _carName,
                (0, new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], _carName))
            );
            Factory = CreateFactory(simulation);
            Context = new ClientContext(CreateStage(checkpoints), multiplayer);
            Client = (BaseClientGamemode)Factory.CreateGameMode(
                new ClientGamemodeParameters
                {
                    Players = Enumerable.Range(0, multiplayer ? 2 : 1).Select(i => new ClientSidePlayerInfo(
                            $"Player {i}",
                            _carName,
                            new Color3(255, 0, 0),
                            isBot: false,
                            isClientPlayer: i == localSlot
                        )
                    ).ToList()
                },
                Context
            );
            Client.Begin();
        }

        public void Start() => Client.OnServerState(Event(Factory, "RaceStateEvent", true, 0));

        public void Dispose()
        {
            Client.End();
            Factory.End();
            BackendGameSparker.dynamic_models.Remove(_carName);
        }
    }

    private sealed class ServerContext : IServerGamemodeContext
    {
        public BackendStage CurrentStage { get; } = CreateStage();
        public List<byte[]> Broadcasts { get; } = [];
        public f64Vector3? GetPlayerPosition(Guid playerId) => null;
        public bool? IsPlayerWasted(Guid playerId) => null;
        public void BroadcastEvent(ReadOnlyMemory<byte> payload) => Broadcasts.Add(payload.ToArray());
    }

    private static IServerGamemode CreateServer(BaseGamemodeFactory factory, ServerContext context)
    {
        var server = factory.CreateServerGamemode(
            new ServerGamemodeParameters
            {
                Players = new List<ServerSidePlayerInfo>
                {
                    new() { Id = FirstId, PlayerName = "first", CarName = "test", Color = default },
                    new() { Id = SecondId, PlayerName = "second", CarName = "test", Color = default }
                }
            },
            context
        )!;
        server.Begin();
        return server;
    }

    private static void StartServer(IServerGamemode server)
    {
        server.StartRace();
        for (var i = 0; i < GamemodeUtils.ServerCountdownInterval * 3; i++)
            server.GameTick();
    }

    [TestMethod]
    public void Begin_ConstructsEveryRosterCarAndUsesTheActualLocalSlot()
    {
        using var race = new ClientRace();
        var client = race.Client;
        Assert.AreSame(client.Players[1], client.ClientPlayer);
        Assert.IsNotNull(client.Players[0].Car, "relayed states can arrive before the first game tick");
        Assert.IsNotNull(client.Players[1].Car);
        Assert.AreEqual(1, client.ClientPlayer!.Car!.CarPhysics.Im);
        Assert.IsFalse(client.Players[0].IsFake, "a real opponent must stay in the real-player roster");
        Assert.IsFalse(client.Players[1].IsFake);
        Assert.AreEqual(0.5f, race.Context.Cars[client.Players[0].Car!].AlphaOverride);
        Assert.IsFalse(race.Context.Cars.ContainsKey(client.ClientPlayer.Car), "the local car stays opaque");
        Assert.AreEqual(0, race.Context.Restarts, "initialization must not restart the host");
        var relayedPosition = new f64Vector3(15_000, World.Ground, 20_000);
        client.Players[0].Car!.Position = relayedPosition;
        client.GameTick();
        Assert.AreEqual(relayedPosition, client.Players[0].Car!.Position, "the first tick must not erase a received state");
    }

    [TestMethod]
    public void RemoteGhost_AdvancesIndependentlyAfterTheLocalPlayerFinishes()
    {
        using var race = new ClientRace(simulation: true);
        var own = race.Client.ClientPlayer!.Car!;
        var remote = race.Client.Players[0].Car!;
        own.CurrentLap = 1;
        remote.Control.Up = true;

        race.Client.GameTick();
        Assert.IsTrue(own.CarPhysics.Halted);
        Assert.IsFalse(remote.CarPhysics.Halted, "the other player is still racing");
        Assert.AreEqual(1, race.Context.Sent.Count);
        var position = remote.Position;
        for (var i = 0; i < 20; i++)
            race.Client.GameTick();

        Assert.AreNotEqual(position, remote.Position);
        Assert.IsFalse(remote.CarPhysics.Halted);
        Assert.AreEqual(1, race.Context.Sent.Count, "finish is reported once, by the local slot only");
        StringAssert.Contains(race.Client.HudState.StateText ?? "", "Waiting for other players");
        Assert.IsFalse((race.Client.HudState.StateText ?? "").Contains("Press R"));
    }

    [TestMethod]
    public void OverlappingGhost_DoesNotChangeTheLocalCarsPhysics()
    {
        using var multiplayer = new ClientRace(localSlot: 0, simulation: true);
        using var solo = new ClientRace(localSlot: 0, simulation: true, multiplayer: false);
        var own = multiplayer.Client.ClientPlayer!.Car!;
        var alone = solo.Client.ClientPlayer!.Car!;
        own.Control.Up = alone.Control.Up = true;
        multiplayer.Client.Players[1].Car!.Control.Up = true;
        for (var i = 0; i < 20; i++)
        {
            multiplayer.Client.GameTick();
            solo.Client.GameTick();
            Assert.AreEqual(alone.Position, own.Position);
            Assert.AreEqual(alone.CarPhysics.DamagePoints, own.CarPhysics.DamagePoints);
        }
    }

    [TestMethod]
    public void OnlineReset_DoesNotReplaceCarsOrRestartTheHost()
    {
        using var race = new ClientRace();
        var car = race.Client.ClientPlayer!.Car;
        race.Client.Reset();
        Assert.AreSame(car, race.Client.ClientPlayer.Car);
        Assert.AreEqual(0, race.Context.Restarts);
    }

    [TestMethod]
    public void SpectatorBaseline_StartsRemotePhysicsWithoutCreatingALocalPlayer()
    {
        using var race = new ClientRace(localSlot: -1);
        Assert.IsNull(race.Client.ClientPlayer);
        race.Start();
        var car = race.Client.Players[0].Car!;
        car.Control.Up = true;
        var position = car.Position;
        for (var i = 0; i < 20; i++)
            race.Client.GameTick();
        race.Client.Forfeit();
        Assert.AreEqual(0, race.Client.HudState.CountdownTimer);
        Assert.AreNotEqual(position, car.Position);
        Assert.IsEmpty(race.Context.Sent, "a spectator neither finishes nor forfeits a player");
        Assert.IsFalse(race.Client.HudState.ClientWon);
        Assert.IsFalse(race.Client.HudState.ClientLost);
    }

    [TestMethod]
    public void CountdownBaseline_RetainsTheServersCountUntilGo()
    {
        using var race = new ClientRace();
        race.Client.OnServerState(Event(race.Factory, "RaceStateEvent", false, 2));
        race.Client.GameTick();
        Assert.AreEqual(2, race.Client.HudState.CountdownTimer);
        var own = race.Client.ClientPlayer!.Car!;
        own.CurrentLap = 1;
        for (var i = 0; i < GamemodeUtils.CountdownInterval * 4; i++)
            race.Client.GameTick();
        Assert.IsEmpty(race.Context.Sent, "the client must wait for the server's go event");
        race.Start();
        race.Client.GameTick();
        Assert.AreEqual(0, race.Client.HudState.CountdownTimer);
        Assert.HasCount(1, race.Context.Sent);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NoCheckpoints_RemainsUnavailableWithoutConstructingCars(bool simulation)
    {
        using var race = new ClientRace(simulation: simulation, checkpoints: 0);
        race.Start();
        race.Client.Reset();
        race.Client.GameTick();
        Assert.IsTrue(race.Client.Players.All(p => p.Car is null));
        Assert.AreEqual(0, race.Client.HudState.Position);
        Assert.AreEqual(0, race.Client.HudState.CountdownTimer);
        StringAssert.Contains(race.Client.HudState.StateText ?? "", "no checkpoints");
    }

    [TestMethod]
    public void FirstFinish_LeavesTheOtherPlayerRacingAndIgnoresDuplicateOrUnknownFinishers()
    {
        var factory = CreateFactory();
        var server = CreateServer(factory, new ServerContext());
        try
        {
            StartServer(server);
            var finish = Event(factory, "FinishedEvent");
            server.OnClientEvent(FirstId, finish);
            server.OnClientEvent(FirstId, finish);
            server.OnClientEvent(Guid.NewGuid(), finish);
            Assert.IsFalse(server.GetStateSnapshot() is { IsFinished: true });

            server.OnClientEvent(SecondId, finish);
            var results = server.GetStateSnapshot()?.Results;
            Assert.IsNotNull(results);
            CollectionAssert.AreEqual(new[] { FirstId, SecondId }, results.Value.Standings.Select(p => p.PlayerId).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1 }, results.Value.Standings.Select(p => p.FinishPosition).ToArray());
        }
        finally
        {
            server.End();
            factory.End();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FinishAndDeparture_EndOnlyAfterBothAreDone(bool disconnect)
    {
        var factory = CreateFactory();
        var server = CreateServer(factory, new ServerContext());
        try
        {
            StartServer(server);
            if (disconnect)
                server.OnClientDisconnected(FirstId);
            else
                server.OnClientEvent(FirstId, Event(factory, "ForfeitEvent"));
            Assert.IsFalse(server.GetStateSnapshot() is { IsFinished: true });
            server.OnClientEvent(FirstId, Event(factory, "FinishedEvent"));
            Assert.IsFalse(server.GetStateSnapshot() is { IsFinished: true });

            server.OnClientEvent(SecondId, Event(factory, "FinishedEvent"));
            var results = server.GetStateSnapshot()?.Results;
            Assert.IsNotNull(results);
            Assert.AreEqual(SecondId, results.Value.Standings[0].PlayerId, "finishers rank ahead of departures");
            Assert.AreEqual(FirstId, results.Value.Standings[1].PlayerId);
            Assert.IsNull(results.Value.Standings[1].FinishTime);
        }
        finally
        {
            server.End();
            factory.End();
        }
    }

    [TestMethod]
    public void EveryoneDisconnecting_EndsTheSession()
    {
        var factory = CreateFactory();
        var server = CreateServer(factory, new ServerContext());
        try
        {
            StartServer(server);
            server.OnClientDisconnected(FirstId);
            server.OnClientDisconnected(SecondId);
            Assert.IsTrue(server.GetStateSnapshot() is { IsFinished: true });
        }
        finally
        {
            server.End();
            factory.End();
        }
    }

    [TestMethod]
    public void ServerBaseline_IsAbsentBeforeStartAndStartsALateSpectatorAfterCountdown()
    {
        using var race = new ClientRace(localSlot: -1);
        var context = new ServerContext();
        var server = CreateServer(race.Factory, context);
        try
        {
            Assert.IsNull(server.CaptureState());
            server.StartRace();
            Assert.IsNotNull(server.CaptureState());
            for (var i = 0; i < GamemodeUtils.ServerCountdownInterval; i++)
                server.GameTick();
            race.Client.OnServerState(server.CaptureState()!.Value.Span);
            Assert.AreEqual(2, race.Client.HudState.CountdownTimer);
            for (var i = 0; i < GamemodeUtils.ServerCountdownInterval * 2; i++)
                server.GameTick();
            Assert.HasCount(1, context.Broadcasts);
            race.Client.OnServerState(server.CaptureState()!.Value.Span);
            race.Client.GameTick();
            Assert.AreEqual(0, race.Client.HudState.CountdownTimer);
        }
        finally
        {
            server.End();
        }
    }
}
