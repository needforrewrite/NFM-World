using MemoryPack;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.FixedMath;
using f64Euler = NFMWorldLibrary.FixedMath.f64Euler;
using f64Vector3 = FixedMathSharp.Vector3d;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

/// <summary>
/// How a race orders its standings. Running cars go by race position; everyone who was wasted
/// or forfeited sorts behind them, newest death first. Nothing recorded who died when before,
/// so a DNF had no position at all.
/// </summary>
[TestClass]
public class PvpStandingsTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static StageObject CreateCheckpoint(fix64 x)
        => StageObject.CreateDefaultObject(
            new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "standings-test"),
            new f64Vector3(x, World.Ground, 0), f64Euler.Identity,
            PiecePlacementType.CheckPoint, AiNodeKind.CheckPoint);

    private static BackendStage CreateStage(ushort laps, int checkpoints)
    {
        var stage = new BackendStage("standings-test", new StageLoader { nlaps = laps });
        for (var i = 0; i < checkpoints; i++)
            stage.Checkpoints.Add(CreateCheckpoint(100_000 + i * 20_000));
        return stage;
    }

    private sealed class ServerContext(BackendStage stage) : IServerGamemodeContext
    {
        public BackendStage CurrentStage { get; } = stage;
        public Dictionary<Guid, bool?> Wasted { get; } = [];
        public Dictionary<Guid, f64Vector3> Positions { get; } = [];
        public f64Vector3? GetPlayerPosition(Guid playerId)
            => Positions.TryGetValue(playerId, out var p) ? p : null;
        public bool? IsPlayerWasted(Guid playerId)
            => Wasted.TryGetValue(playerId, out var w) ? w : null;
        public void BroadcastEvent(ReadOnlyMemory<byte> payload) { }
    }

    private static (BaseGamemodeFactory Factory, IServerGamemode Server, ServerContext Context) CreateRace(
        params Guid[] players)
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var factory = GamemodeFactory.Create("nfmm/pvp", new Dictionary<string, object> { ["constraint"] = "racing" });
        var context = new ServerContext(CreateStage(laps: 3, checkpoints: 2));
        var server = factory.CreateServerGamemode(new ServerGamemodeParameters
        {
            Players = players.Select(id => new ServerSidePlayerInfo
            {
                Id = id, PlayerName = id.ToString()[..4], CarName = "test", Color = default
            }).ToList()
        }, context)!;
        server.Begin();
        server.StartRace();
        return (factory, server, context);
    }

    /// <summary>Runs the countdown out so the server gamemode is actually racing.</summary>
    private static void StartRacing(IServerGamemode server)
    {
        for (var i = 0; i < GamemodeUtils.ServerCountdownInterval * 4; i++)
            server.GameTick();
    }

    private static RaceStanding[] Standings(IServerGamemode server)
        => server.GetStateSnapshot()!.Value.Results!.Value.Standings;

    private static void Revived(IServerGamemode server, ServerContext context, Guid player)
    {
        context.Wasted[player] = false;
        server.GameTick();
    }

    /// <summary>Wastes a car and lets the server tick so the death is stamped.</summary>
    private static void Wasted(IServerGamemode server, ServerContext context, Guid player)
    {
        context.Wasted[player] = true;
        server.GameTick();
    }

    /// <summary>
    /// The event types live in the plugin assembly, which the test does not reference, so they
    /// are built through the factory's loaded types - serialized through the shared base type,
    /// which is what the union header comes from.
    /// </summary>
    private static ReadOnlyMemory<byte> SerializeEvent(BaseGamemodeFactory factory, string name, params object[] args)
    {
        var type = ((NativeGamemodeFactory)factory).Plugin.Assembly.GetExportedTypes()
            .Single(t => t.Name == name);
        return MemoryPackSerializer.Serialize(type.BaseType!, Activator.CreateInstance(type, args));
    }

    /// <summary>
    /// Takes the player round every checkpoint of every lap so the race ends legitimately: the
    /// server rejects a claim that skips a checkpoint, so a single finish-line jump is refused.
    /// </summary>
    private static void DriveToFinish(BaseGamemodeFactory factory, IServerGamemode server,
        ServerContext context, Guid player, int laps, int checkpoints)
    {
        // The event carries the checkpoint being headed for next, which wraps to 0 when a lap
        // completes, plus the lap just completed - the same convention the client sends.
        for (var total = 0; total < laps * checkpoints; total++)
        {
            var crossed = total % checkpoints;
            // A lap counts as completed the moment its last checkpoint is crossed.
            var lap = (total + 1) / checkpoints;
            // The server corroborates each claim against the claimant's relayed position.
            context.Positions[player] = context.CurrentStage.Checkpoints[crossed].Position;
            server.OnClientEvent(player, SerializeEvent(factory, "CheckpointEvent",
                (ushort)((crossed + 1) % checkpoints), (byte)lap, 0, 0).Span);
        }
    }

    private const int Checkpoints = 2;
    private const int Laps = 3;

    private static void Finish(BaseGamemodeFactory factory, IServerGamemode server, ServerContext context, Guid player)
        => DriveToFinish(factory, server, context, player, Laps, Checkpoints);

    [TestMethod]
    public void WastedPlayersSortBehindTheRunning()
    {
        var (factory, server, context) = CreateRace(Alice, Bob, Carol);
        try
        {
            StartRacing(server);
            Wasted(server, context, Alice);
            Wasted(server, context, Bob);
            Finish(factory, server, context, Carol);

            var standings = Standings(server);
            Assert.AreEqual(Carol, standings[0].PlayerId, "the finisher takes first");
            Assert.AreEqual(Bob, standings[1].PlayerId, "the most recent death is the better DNF");
            Assert.AreEqual(Alice, standings[2].PlayerId, "the earlier death is last");
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>
    /// A winner's standing is the order of the list, not the position they held on the grid: the
    /// race ends when the first car finishes, so everyone behind it is ranked by how far they got.
    /// </summary>
    [TestMethod]
    public void RunningCarsAreRankedByProgressBehindTheWinner()
    {
        var (factory, server, context) = CreateRace(Alice, Bob, Carol);
        try
        {
            StartRacing(server);

            // Bob gets further than Carol before Alice finishes.
            OneCheckpoint(factory, server, context, Bob);
            OneCheckpoint(factory, server, context, Bob);
            OneCheckpoint(factory, server, context, Carol);
            Finish(factory, server, context, Alice);

            var standings = Standings(server);
            Assert.AreEqual(Alice, standings[0].PlayerId);
            Assert.AreEqual(Bob, standings[1].PlayerId, "second most progress is second place");
            Assert.AreEqual(Carol, standings[2].PlayerId);
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>Claims one checkpoint, at the position the server checks the claim against.</summary>
    private static void OneCheckpoint(BaseGamemodeFactory factory, IServerGamemode server,
        ServerContext context, Guid player, ushort checkpoint = 0, byte lap = 0)
    {
        context.Positions[player] = context.CurrentStage.Checkpoints[checkpoint].Position;
        server.OnClientEvent(player, SerializeEvent(factory, "CheckpointEvent",
            (ushort)((checkpoint + 1) % Checkpoints), lap, 0, 0).Span);
    }

    /// <summary>
    /// Being revived by a fix hoop takes a car out of the DNF set: it is running again, so it
    /// sorts with the running rather than with the dead.
    /// </summary>
    [TestMethod]
    public void RevivedCarIsNoLongerADnf()
    {
        var (factory, server, context) = CreateRace(Alice, Bob);
        try
        {
            StartRacing(server);
            Wasted(server, context, Alice);
            Revived(server, context, Alice);
            Wasted(server, context, Bob);
            Finish(factory, server, context, Alice);

            var standings = Standings(server);
            Assert.AreEqual(Alice, standings[0].PlayerId, "a revived car can still win");
            Assert.AreEqual(Bob, standings[1].PlayerId);
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>
    /// Placement is what the HUD's position column reads, and nothing computed it in a race, so
    /// the whole field showed as first.
    /// </summary>
    [TestMethod]
    public void CalculatePositions_RanksByCheckpointProgress()
    {
        var stage = CreateStage(laps: 2, checkpoints: 2);
        var players = new List<ClientSidePlayer>
        {
            new(new ClientSidePlayerInfo("a", "test", default, isBot: false, isClientPlayer: true), 0),
            new(new ClientSidePlayerInfo("b", "test", default, isBot: true, isClientPlayer: false), 1)
        };

        foreach (var (player, total) in new[] { (players[0], 3), (players[1], 1) })
        {
            var car = new BackendCar(
                new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "test"),
                0, 0, 0, player.Info.IsClientPlayer);
            car.TotalCheckpoint = (short)total;
            player.Car = car;
        }

        CheckPointHelper.CalculatePositions(stage, players);

        Assert.AreEqual(0, players[0].Car!.Placement, "more checkpoints is a better position");
        Assert.AreEqual(1, players[1].Car!.Placement);
    }
}
