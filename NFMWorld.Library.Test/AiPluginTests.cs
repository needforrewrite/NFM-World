using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.DriverInterface;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary;
using NFMWorldLibrary.Plugins;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.FixedMath;

namespace NFMWorld.Library.Test;

[TestClass]
public class AiPluginTests
{
    // Driving a car opens a Sentry transaction, and nothing else in this suite initializes it.
    [ClassInitialize]
    public static void InitSentry(TestContext _)
    {
        try
        {
            NFMWorld.Sentry.SentrySdk.Init(options =>
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

    private static AiPlugin? Load(string id)
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        return AiPlugin.Load(id, $"data/ais/{id}/ai.dll");
    }

    [TestMethod]
    public void Load_ElStupido_IsAnAiPlugin()
    {
        var plugin = Load("el_stupido");

        Assert.IsNotNull(plugin);
        Assert.AreEqual("ElStupido", plugin.Name);
    }

    [TestMethod]
    public void Load_Classic_IsAnAiPlugin()
    {
        var plugin = Load("classic");

        Assert.IsNotNull(plugin);
        Assert.AreEqual("Classic", plugin.Name);
    }

    [TestMethod]
    public void Load_MissingAssembly_ReturnsNull()
    {
        Assert.IsNull(Load("nope"));
    }

    [TestMethod]
    public void Load_UnmarkedAssembly_ReturnsNull()
    {
        // The gamemode assembly is a valid plugin, just not an AI one.
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

        Assert.IsNull(AiPlugin.Load("nfmm/pvp", "data/gamemodes/nfmm/pvp/gamemode.dll"));
    }

    [TestMethod]
    public void Create_BindsThePlayer()
    {
        var plugin = Load("el_stupido")!;
        var gamemode = new StubGamemode();

        var player = gamemode.Players[1];
        var bot = plugin.Create(player, gamemode);

        Assert.AreSame(player, bot.Player);
        Assert.AreEqual("el_stupido", bot.AiId);

        plugin.Unload();
    }

    [TestMethod]
    [DataRow("el_stupido")]
    [DataRow("classic")]
    public void RunAi_WithoutCar_DoesNothing(string id)
    {
        var plugin = Load(id)!;
        var gamemode = new StubGamemode();
        var bot = plugin.Create(gamemode.Players[1], gamemode);

        // No car yet: the bot must no-op rather than throw.
        bot.RunAi();

        plugin.Unload();
    }

    /// <summary>
    /// Constructing the client gamemode calls the protected constructor of its abstract
    /// base through the sandbox, which no server-only race path exercises.
    /// </summary>
    [TestMethod]
    [DataRow("nfmm/pvp")]
    [DataRow("nfmm/timetrial")]
    public void CreateClient_PassesTheSandbox(string id)
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin.Load(id, $"data/gamemodes/{id}/gamemode.dll")!;

        var gamemode = plugin.CreateClient(new ClientGamemodeParameters
        {
            Players = [new ClientSidePlayerInfo { PlayerName = "Human", CarName = "nfmm/radicalone", Color = default, IsBot = false, IsClientPlayer = true }]
        }, new StubContext("empty"));

        Assert.AreEqual(id, gamemode.GamemodeId);
        plugin.Unload();
    }

    [TestMethod]
    public void BeginPvp_WithBot_PassesTheSandbox()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin.Load(
            "nfmm/pvp", "data/gamemodes/nfmm/pvp/gamemode.dll");
        Assert.IsNotNull(plugin);
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));
            gamemode = plugin.CreateClient(new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo { PlayerName = "Human", CarName = carName, Color = default, IsBot = false, IsClientPlayer = true },
                    new ClientSidePlayerInfo { PlayerName = "ElStupido", CarName = carName, Color = default, IsBot = true, IsClientPlayer = false }
                ]
            }, new StubContext("empty"));

            gamemode.Begin();

            var player = gamemode.Players[1];
            var bot = player.Bot as PluginAi;
            Assert.IsNotNull(player.Car);
            Assert.IsNotNull(bot);
            Assert.AreSame(player, bot.Player);
            Assert.AreEqual("classic", bot.AiId);

            // Tick past the countdown so the bot actually drives: constructing it proves
            // nothing about the host fields its RunAi reads through the sandbox.
            for (var i = 0; i < NFMWorldLibrary.Gamemodes.DotNet.GamemodeUtils.CountdownInterval * 5; i++)
                gamemode.GameTick();
        }
        finally
        {
            BackendGameSparker.dynamic_models.Remove(carName);
            try
            {
                gamemode?.End();
            }
            finally
            {
                plugin.Unload();
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TimeTrialWithoutCheckpointsDoesNotStartOrRestart(bool simulation)
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin.Load(
            "nfmm/timetrial", "data/gamemodes/nfmm/timetrial/gamemode.dll");
        Assert.IsNotNull(plugin);
        BaseClientGamemode? gamemode = null;
        try
        {
            gamemode = plugin.CreateClient(new ClientGamemodeParameters
            {
                Players = [new ClientSidePlayerInfo
                {
                    PlayerName = "Human", CarName = "missing/checkpoint-free-test", Color = default,
                    IsBot = false, IsClientPlayer = true
                }]
            }, new StubContext(initialStage: new EmptyStage()), new Dictionary<string, object> { ["simulation"] = simulation });
            gamemode.Begin();
            for (var i = 0; i < 10; i++) gamemode.GameTick();
            gamemode.KeyPressed(Key.R, default);
            gamemode.Reset();
            gamemode.GameTick();

            Assert.IsNull(gamemode.ClientPlayer.Car);
            Assert.AreEqual(0, gamemode.HudState.CountdownTimer);
            Assert.AreEqual(0, gamemode.HudState.LapTime);
            Assert.AreEqual(0, gamemode.HudState.Position);
            StringAssert.Contains(gamemode.HudState.StateText ?? "", "no checkpoints");
        }
        finally
        {
            try { gamemode?.End(); }
            finally { plugin.Unload(); }
        }
    }

    /// <summary>
    /// The classic AI is mostly physics reads, so a run where nothing is loaded proves
    /// nothing. This drives it over a hand-built straight with a checkpoint at the end.
    /// </summary>
    [TestMethod]
    public void Classic_DrivesTowardsTheNextCheckpoint_UnderTheSandbox()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            var stage = new StraightStage();
            gamemode = new DrivingGamemode(stage, carName);
            gamemode.Begin();

            Assert.IsNotNull(gamemode.Players[1].Bot);
            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            var startZ = botCar.Position.Z;
            var startX = botCar.Position.X;
            for (var i = 0; i < 300; i++)
                gamemode.GameTick();

            // The path runs along +Z, so a working driver is ahead of where it started.
            Assert.IsTrue(botCar.Position.Z > startZ,
                $"the bot did not drive along its path (z {startZ} -> {botCar.Position.Z})");

            // And it drives down the line, not sideways off the start: a fresh car reads as
            // airborne until its wheels settle, which used to count as a take-off and send it
            // chasing the other car instead of racing.
            Assert.IsTrue(FixedMathSharp.FixedMath.Abs(botCar.Position.X - startX) < FixedMathSharp.FixedMath.Abs(botCar.Position.Z - startZ),
                $"the bot veered off the line (x {startX} -> {botCar.Position.X}, z {startZ} -> {botCar.Position.Z})");
        }
        finally
        {
            BackendGameSparker.dynamic_models.Remove(carName);
            try { gamemode?.End(); }
            finally { plugin.Unload(); }
        }
    }

    /// <summary>
    /// A full grid, as free play now runs it: bots must race the path, not each other.
    /// </summary>
    [TestMethod]
    public void Classic_WithAGridOfBots_AllRaceThePath()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new StraightStage(), carName, bots: 4);
            gamemode.Begin();

            var starts = gamemode.Players.Select(p => p.Car!).ToArray();
            var startPositions = starts.Select(c => (c.Position.X, c.Position.Z)).ToArray();

            for (var i = 0; i < 300; i++)
                gamemode.GameTick();

            for (var i = 0; i < starts.Length; i++)
            {
                // The human has no driver in this harness; only the bots can be judged.
                if (!gamemode.Players[i].Info.IsBot)
                    continue;

                var botCar = starts[i];
                var (startX, startZ) = startPositions[i];
                var dx = FixedMathSharp.FixedMath.Abs(botCar.Position.X - startX);
                var dz = FixedMathSharp.FixedMath.Abs(botCar.Position.Z - startZ);

                Assert.IsTrue(dz > dx,
                    $"{gamemode.Players[i].Info.PlayerName} went sideways (dx {dx:0}, dz {dz:0}) - it is chasing a car, not the path");
            }
        }
        finally
        {
            BackendGameSparker.dynamic_models.Remove(carName);
            try { gamemode?.End(); }
            finally { plugin.Unload(); }
        }
    }

    /// <summary>A straight path along +Z with one checkpoint, built without touching stage files.</summary>
    private sealed class StraightStage : BackendStage
    {
        public StraightStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/path");
            for (var i = 0; i < 20; i++)
            {
                var kind = i == 19 ? AiNodeKind.CheckPoint : AiNodeKind.Road;
                var node = StageObject.CreateDefaultObject(
                    mesh, new FixedMathSharp.Vector3d(0, 250, i * 1000), f64Euler.Identity,
                    kind == AiNodeKind.CheckPoint ? PiecePlacementType.CheckPoint : PiecePlacementType.CollisionObject,
                    kind);
                // CreateDefaultObject keeps the kind on the placement; the runtime reads it off
                // the object, which BackendStage.LoadStageInternal copies across.
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
                if (kind == AiNodeKind.CheckPoint)
                    Checkpoints[Checkpoints.Count] = node;
            }
        }
    }

    /// <summary>Two cars on a given stage, one driven by the plugin under test.</summary>
    private sealed class DrivingGamemode : BaseClientGamemode
    {
        private readonly PhysicsController _physics;

        public DrivingGamemode(BackendStage stage, string carName, int bots = 1) : base(
            new ClientGamemodeParameters
            {
                Players = BuildPlayers(carName, bots)
            },
            new StubContext(initialStage: stage))
        {
            _physics = new PhysicsController(Players, CurrentStage);
        }

        public override string GamemodeId => "test/drive";

        private static ClientSidePlayerInfo[] BuildPlayers(string carName, int bots)
        {
            var players = new List<ClientSidePlayerInfo>
            {
                new() { PlayerName = "Human", CarName = carName, Color = default, IsBot = false, IsClientPlayer = true }
            };
            for (var i = 0; i < bots; i++)
            {
                players.Add(new ClientSidePlayerInfo
                {
                    PlayerName = $"Classic{i + 1}", CarName = carName, Color = default, IsBot = true, IsClientPlayer = false
                });
            }

            return players.ToArray();
        }

        public override void Begin()
        {
            base.Begin();
            for (var i = 0; i < Players.Count; i++)
            {
                var player = Players[i];
                // Same grid PvP uses, so the bots really are side by side at the start.
                player.Car = new BackendCar(player.Info, i, -500 + (400 * i), 0);
                if (player.Info.IsBot)
                    AttachBot("classic", player);
            }
        }

        public override void GameTick()
        {
            base.GameTick();
            _physics.GameTick();
        }
    }

    /// <summary>
    /// The minimum a gamemode has to provide for a bot: the player roster and the stage.
    /// </summary>
    private sealed class StubGamemode : BaseClientGamemode
    {
        public StubGamemode() : base(
            new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo { PlayerName = "Human", CarName = "nfmm/radicalone", Color = default, IsBot = false, IsClientPlayer = true },
                    new ClientSidePlayerInfo { PlayerName = "ElStupido", CarName = "nfmm/audir8", Color = default, IsBot = true, IsClientPlayer = false }
                ]
            },
            new StubContext())
        {
        }

        public override string GamemodeId => "test/stub";
    }

    private sealed class EmptyStage : BackendStage { }

    private sealed class StubContext(string stageName = "test_stage", BackendStage? initialStage = null) : IGamemodeContext
    {
        // Lazy: building a stage at all reads stage data and reports failures through
        // Sentry, which these tests do not initialize.
        private BackendStage? _stage = initialStage;

        public BackendStage CurrentStage => _stage ??= new BackendStage(stageName);

        public RaceState RaceState => RaceState.InProgress;

        public IClientCallbacks ClientCallbacks => new StubClientCallbacks();

        public void SendServerEvent(ReadOnlySpan<byte> payload)
        {
        }

        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        {
        }
    }

    private sealed class StubClientCallbacks : IClientCallbacks
    {
        public void ResetCheckpointGlow()
        {
        }

        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
        {
        }

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => new StubCarCallbacks();
    }

    private sealed class StubCarCallbacks : IClientCarCallbacks
    {
        public bool CastsShadow { get; set; }
        public bool? GetsShadowed { get; set; }
        public float? AlphaOverride { get; set; }
        public bool? Glow { get; set; }
        public bool? Finish { get; set; }
    }
}
