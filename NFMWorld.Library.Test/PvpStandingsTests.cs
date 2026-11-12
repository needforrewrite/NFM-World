using TightPack;
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
    private static readonly Guid Dave = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    private static StageObject CreateCheckpoint(fix64 x)
        => StageObject.CreateDefaultObject(
            new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "standings-test"),
            new f64Vector3(x, World.Ground, 0),
            f64Euler.Identity,
            PiecePlacementType.CheckPoint,
            AiNodeKind.CheckPoint
        );

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

        public void BroadcastEvent(ReadOnlyMemory<byte> payload)
        {
        }
    }

    private static (BaseGamemodeFactory Factory, IServerGamemode Server, ServerContext Context) CreateRace(
        params Guid[] players
    ) => CreateRace("racing", players);

    private static (BaseGamemodeFactory Factory, IServerGamemode Server, ServerContext Context) CreateRace(
        string constraint,
        params Guid[] players
    )
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var factory = GamemodeFactory.Create("nfmm/pvp", new Dictionary<string, object> { ["constraint"] = constraint });
        var context = new ServerContext(CreateStage(laps: 3, checkpoints: 2));
        var server = factory.CreateServerGamemode(
            new ServerGamemodeParameters
            {
                Players = players.Select(id => new ServerSidePlayerInfo
                    {
                        Id = id, PlayerName = id.ToString()[..4], CarName = "test", Color = default
                    }
                ).ToList()
            },
            context
        )!;
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
        return TightPackSerializer.SerializeUntyped(type.BaseType!, Activator.CreateInstance(type, args));
    }

    /// <summary>
    /// Takes the player round every checkpoint of every lap so the race ends legitimately: the
    /// server rejects a claim that skips a checkpoint, so a single finish-line jump is refused.
    /// </summary>
    private static void DriveToFinish(
        BaseGamemodeFactory factory,
        IServerGamemode server,
        ServerContext context,
        Guid player,
        int laps,
        int checkpoints
    )
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
            server.OnClientEvent(
                player,
                SerializeEvent(
                    factory,
                    "CheckpointEvent",
                    (ushort)((crossed + 1) % checkpoints),
                    (byte)lap,
                    0,
                    0
                ).Span
            );
        }
    }

    private const int Checkpoints = 2;
    private const int Laps = 3;

    private static void Finish(BaseGamemodeFactory factory, IServerGamemode server, ServerContext context, Guid player)
        => DriveToFinish(factory, server, context, player, Laps, Checkpoints);

    /// <summary>
    /// A wasting race is decided by survival: once only one car is still alive the race is over,
    /// and the survivor wins even though nobody crossed a line.
    /// </summary>
    [TestMethod]
    public void WastingConstraint_EndsWhenOneCarIsLeftAndThatCarWins()
    {
        var (factory, server, context) = CreateRace("wasting", Alice, Bob, Carol);
        try
        {
            StartRacing(server);
            Wasted(server, context, Alice);
            Assert.IsNull(server.GetStateSnapshot(), "two cars are still alive");
            Wasted(server, context, Bob);

            var standings = Standings(server);
            Assert.AreEqual(Carol, standings[0].PlayerId, "the last car alive wins a wasting race");
            Assert.AreEqual(Bob, standings[1].PlayerId, "the newest death sorts ahead of the older one");
            Assert.AreEqual(Alice, standings[2].PlayerId);
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>A racing race ignores deaths: everyone can be out and the race still runs.</summary>
    [TestMethod]
    public void RacingConstraint_DoesNotEndWhenEveryCarIsWasted()
    {
        var (factory, server, context) = CreateRace("racing", Alice, Bob, Carol);
        try
        {
            StartRacing(server);
            Wasted(server, context, Alice);
            Wasted(server, context, Bob);
            Wasted(server, context, Carol);

            Assert.IsNull(server.GetStateSnapshot(), "a racing race has no survival condition");
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>
    /// The realistic revive: Bob is wasted and the server sees it on one tick, then he is pushed
    /// through a fix hoop and the next tick clears it. The death must not latch - two cars are
    /// alive again, so the race keeps going.
    /// </summary>
    [TestMethod]
    public void WastingConstraint_ACarRevivedAfterItsDeathWasSeenKeepsTheRaceAlive()
    {
        var (factory, server, context) = CreateRace("wasting", Alice, Bob, Carol, Dave);
        try
        {
            StartRacing(server);
            Wasted(server, context, Alice);
            Wasted(server, context, Bob);
            Assert.IsNull(server.GetStateSnapshot(), "Carol and Dave are still alive");

            Revived(server, context, Bob);

            // Alice is out but Bob is back, so three cars are racing.
            Assert.IsNull(server.GetStateSnapshot(), "Bob's death was cleared by the hoop");
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>
    /// A car can be wasted and pushed back through a fix hoop between two server ticks. The
    /// constraint reads the latest relayed state, so a car that is alive now keeps the race
    /// going - it must not latch a death it has already recovered from.
    /// </summary>
    [TestMethod]
    public void WastingConstraint_ReadsTheCurrentStateNotAnEarlierDeath()
    {
        var (factory, server, context) = CreateRace("wasting", Alice, Bob, Carol);
        try
        {
            StartRacing(server);
            Wasted(server, context, Alice);

            // One relay gap: Bob dies and is revived before the server ticks again.
            context.Wasted[Bob] = true;
            context.Wasted[Bob] = false;
            server.GameTick();

            Assert.IsNull(server.GetStateSnapshot(), "Bob is back on the road, so two cars are alive");
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>
    /// A client that quits drops its transport rather than sending ForfeitEvent. The GM turns
    /// that disconnect into OnClientDisconnected, which has to place the leaver last and end the
    /// race once nobody is left - otherwise it ticked an empty session forever.
    /// </summary>
    [TestMethod]
    public void DisconnectingTheOnlyPlayerEndsTheRace()
    {
        var (factory, server, context) = CreateRace(Alice);
        try
        {
            StartRacing(server);
            server.OnClientDisconnected(Alice);

            var standings = Standings(server);
            Assert.HasCount(1, standings, "the race ended, which only happens when its last player leaves");
            Assert.AreEqual(Alice, standings[0].PlayerId);
        }
        finally
        {
            factory.End();
        }
    }

    /// <summary>A leaver in a field of three is placed last and the race carries on.</summary>
    [TestMethod]
    public void DisconnectingOneOfSeveralPlayersKeepsTheRaceGoing()
    {
        var (factory, server, context) = CreateRace(Alice, Bob, Carol);
        try
        {
            StartRacing(server);
            server.OnClientDisconnected(Alice);

            Assert.IsNull(server.GetStateSnapshot(), "two players are still racing");
        }
        finally
        {
            factory.End();
        }
    }

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
    private static void OneCheckpoint(
        BaseGamemodeFactory factory,
        IServerGamemode server,
        ServerContext context,
        Guid player,
        ushort checkpoint = 0,
        byte lap = 0
    )
    {
        context.Positions[player] = context.CurrentStage.Checkpoints[checkpoint].Position;
        server.OnClientEvent(
            player,
            SerializeEvent(
                factory,
                "CheckpointEvent",
                (ushort)((checkpoint + 1) % Checkpoints),
                lap,
                0,
                0
            ).Span
        );
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
                0,
                0,
                0,
                player.Info.IsClientPlayer
            );
            car.TotalCheckpoint = (short)total;
            player.Car = car;
        }

        CheckPointHelper.CalculatePositions(stage, players);

        Assert.AreEqual(0, players[0].Car!.Placement, "more checkpoints is a better position");
        Assert.AreEqual(1, players[1].Car!.Placement);
    }
}
