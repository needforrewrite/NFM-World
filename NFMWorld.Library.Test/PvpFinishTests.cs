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
using NFMWorldLibrary.FixedMath;
using f64Euler = NFMWorldLibrary.FixedMath.f64Euler;
using f64Vector3 = FixedMathSharp.Vector3d;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

/// <summary>
/// A PvP race has to end on its final lap. Both halves used to miss it: the server rejected the
/// checkpoint event that completes the last lap, and the client had no finish of its own, so the
/// car kept driving and the lap counter kept climbing.
/// </summary>
[TestClass]
public class PvpFinishTests
{
    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Driving a car opens a Sentry transaction, and nothing else in this suite initializes it.
    [ClassInitialize]
    public static void InitSentry(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = "https://public@localhost/1";
                options.TracesSampleRate = 0;
            });
        }
        catch (InvalidOperationException)
        {
            // Another test class got there first; Sentry is a process-wide singleton.
        }
    }

    private static StageObject CreateCheckpoint(fix64 x)
        => StageObject.CreateDefaultObject(
            new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "pvp-test"),
            new f64Vector3(x, World.Ground, 0), f64Euler.Identity,
            PiecePlacementType.CheckPoint, AiNodeKind.CheckPoint);

    private static BackendStage CreateStage(ushort laps, int checkpoints)
    {
        var stage = new BackendStage("pvp-test", new StageLoader { nlaps = laps });
        for (var i = 0; i < checkpoints; i++)
            stage.Checkpoints.Add(CreateCheckpoint(100_000 + i * 20_000));
        return stage;
    }

    private static BaseGamemodeFactory CreateFactory()
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        return GamemodeFactory.Create("nfmm/pvp", new Dictionary<string, object> { ["constraint"] = "racing" });
    }

    private static ReadOnlyMemory<byte> SerializeEvent(BaseGamemodeFactory factory, string name, params object[] args)
    {
        var type = ((NativeGamemodeFactory)factory).Plugin.Assembly.GetExportedTypes()
            .Single(t => t.Name == name);
        return MemoryPackSerializer.Serialize(type.BaseType!, Activator.CreateInstance(type, args));
    }

    private sealed class ServerContext(BackendStage stage) : IServerGamemodeContext
    {
        public BackendStage CurrentStage { get; } = stage;
        public f64Vector3? Position { get; set; }
        public f64Vector3? GetPlayerPosition(Guid playerId) => Position;
        public void BroadcastEvent(ReadOnlyMemory<byte> payload) { }
    }

    /// <summary>
    /// Relays what the client sends after crossing <paramref name="crossed"/>: the event carries
    /// the checkpoint being headed for next, which wraps to 0 when a lap completes, plus the lap
    /// just completed.
    /// </summary>
    private static void ReportCrossing(BaseGamemodeFactory factory, IServerGamemode server, ServerContext context,
        int crossed, int checkpointCount, byte lap)
    {
        context.Position = context.CurrentStage.Checkpoints[crossed].Position;
        server.OnClientEvent(PlayerId, SerializeEvent(factory, "CheckpointEvent",
            (ushort)((crossed + 1) % checkpointCount), lap, 0, 0).Span);
    }

    private static (BaseGamemodeFactory Factory, IServerGamemode Server, ServerContext Context) CreateRace(
        ushort laps, int checkpoints)
    {
        var factory = CreateFactory();
        var context = new ServerContext(CreateStage(laps, checkpoints));
        var server = factory.CreateServerGamemode(new ServerGamemodeParameters
        {
            // A concrete List, as the real hosts pass: the sandbox cannot enumerate the
            // compiler's single-element collection-expression type.
            Players = new List<ServerSidePlayerInfo>
            {
                new() { Id = PlayerId, PlayerName = "test", CarName = "test", Color = default }
            }
        }, context)!;
        server.Begin();
        server.StartRace();
        return (factory, server, context);
    }

    [TestMethod]
    public void FinalLapCheckpoint_FinishesTheRace()
    {
        var (factory, server, context) = CreateRace(laps: 2, checkpoints: 2);
        try
        {
            ReportCrossing(factory, server, context, crossed: 0, checkpointCount: 2, lap: 0);
            ReportCrossing(factory, server, context, crossed: 1, checkpointCount: 2, lap: 1);
            ReportCrossing(factory, server, context, crossed: 0, checkpointCount: 2, lap: 1);
            Assert.IsFalse(server.GetStateSnapshot() is { IsFinished: true }, "one lap is still to go");

            ReportCrossing(factory, server, context, crossed: 1, checkpointCount: 2, lap: 2);

            Assert.IsTrue(server.GetStateSnapshot() is { IsFinished: true },
                "crossing the last checkpoint on the final lap has to finish the race");
        }
        finally
        {
            factory.End();
        }
    }

    [TestMethod]
    public void CheckpointClaimFarFromTheCheckpoint_IsRejected()
    {
        var (factory, server, context) = CreateRace(laps: 1, checkpoints: 2);
        try
        {
            context.Position = new f64Vector3(900_000, World.Ground, 900_000);
            server.OnClientEvent(PlayerId, SerializeEvent(factory, "CheckpointEvent", (ushort)1, (byte)0, 0, 0).Span);

            Assert.IsFalse(server.GetStateSnapshot() is { IsFinished: true });
        }
        finally
        {
            factory.End();
        }
    }

    private sealed class ClientContext(BackendStage stage) : IGamemodeContext, IClientCallbacks
    {
        public BackendStage CurrentStage { get; } = stage;
        public RaceState RaceState => RaceState.InProgress;
        public IClientCallbacks ClientCallbacks => this;

        public void RaceRestarted() { }
        public void ResetCheckpointGlow() { }
        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish) { }
        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => throw new NotSupportedException();
        public void SendServerEvent(ReadOnlySpan<byte> payload) { }
        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players) { }
    }

    /// <summary>
    /// The client's own finish. Without it a free-play race never ends from the cockpit: the car
    /// keeps driving and the lap counter keeps climbing past the stage's lap count.
    /// </summary>
    [TestMethod]
    public void ClientPassingItsFinalLap_StopsTheRace()
    {
        var factory = CreateFactory();
        var carName = $"pvp-test/{Guid.NewGuid():N}";
        BaseClientGamemode? client = null;
        try
        {
            BackendGameSparker.dynamic_models.Add(carName,
                (0, new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName)));

            client = (BaseClientGamemode)factory.CreateGameMode(new ClientGamemodeParameters
            {
                Players = [new ClientSidePlayerInfo(carName, carName, new Color3(255, 0, 0),
                    isBot: false, isClientPlayer: true)]
            }, new ClientContext(CreateStage(laps: 3, checkpoints: 2)));
            client.Begin();
            client.OnServerEvent(SerializeEvent(factory, "CountdownGoEvent").Span);

            client.ClientPlayer.Car!.CurrentLap = 3;
            client.GameTick();

            Assert.AreEqual(3, client.HudState.Lap, "the lap counter must stop at the stage's lap count");
            Assert.IsTrue(client.ClientPlayer.Car.CarPhysics.Halted, "the car stops driving once the race is over");
            Assert.IsTrue(client.HudState.Finished);
            Assert.IsTrue(client.HudState.ClientWon);
            Assert.IsFalse(client.HudState.ClientLost);
            StringAssert.Contains(client.HudState.StateText ?? "", "You finished first");
        }
        finally
        {
            BackendGameSparker.dynamic_models.Remove(carName);
            client?.End();
            factory.End();
        }
    }

    /// <summary>
    /// A bot crossing the line has to end the race. The client-only finish check let every
    /// bot complete the race while the player kept driving.
    /// </summary>
    [TestMethod]
    public void BotPassingItsFinalLap_EndsTheRaceForTheClient()
    {
        var factory = CreateFactory();
        var carName = $"pvp-test/{Guid.NewGuid():N}";
        BaseClientGamemode? client = null;
        try
        {
            BackendGameSparker.dynamic_models.Add(carName,
                (0, new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName)));

            client = (BaseClientGamemode)factory.CreateGameMode(new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo(carName, carName, new Color3(255, 0, 0),
                        isBot: false, isClientPlayer: true),
                    new ClientSidePlayerInfo("Classic", carName, new Color3(0, 0, 255),
                        isBot: true, isClientPlayer: false)
                ]
            }, new ClientContext(CreateStage(laps: 1, checkpoints: 2)));
            client.Begin();
            client.OnServerEvent(SerializeEvent(factory, "CountdownGoEvent").Span);

            var bot = client.Players[1];
            Assert.IsNotNull(bot.Car);
            bot.Car.CurrentLap = 1;
            client.GameTick();

            Assert.IsTrue(client.HudState.Finished, "a bot finishing has to end the race");
            Assert.IsTrue(client.HudState.ClientLost);
            Assert.IsFalse(client.HudState.ClientWon);
            StringAssert.Contains(client.HudState.StateText ?? "", "Classic finished first, race over!");
            Assert.IsTrue(client.ClientPlayer.Car!.CarPhysics.Halted, "the client car stops with the race");
        }
        finally
        {
            BackendGameSparker.dynamic_models.Remove(carName);
            client?.End();
            factory.End();
        }
    }
}
