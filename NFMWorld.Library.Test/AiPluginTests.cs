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
using f64Vector3 = FixedMathSharp.Vector3d;

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
                }
            );
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

        var gamemode = plugin.CreateClient(
            new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo
                    {
                        PlayerName = "Human", CarName = "nfmm/radicalone", Color = default, IsBot = false, IsClientPlayer = true
                    }
                ]
            },
            new StubContext("empty")
        );

        Assert.AreEqual(id, gamemode.GamemodeId);
        plugin.Unload();
    }

    [TestMethod]
    public void BeginPvp_WithBot_PassesTheSandbox()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin.Load(
            "nfmm/pvp",
            "data/gamemodes/nfmm/pvp/gamemode.dll"
        );
        Assert.IsNotNull(plugin);
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));
            gamemode = plugin.CreateClient(
                new ClientGamemodeParameters
                {
                    Players =
                    [
                        new ClientSidePlayerInfo
                            { PlayerName = "Human", CarName = carName, Color = default, IsBot = false, IsClientPlayer = true },
                        new ClientSidePlayerInfo
                            { PlayerName = "ElStupido", CarName = carName, Color = default, IsBot = true, IsClientPlayer = false }
                    ]
                },
                new StubContext("empty")
            );

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
            "nfmm/timetrial",
            "data/gamemodes/nfmm/timetrial/gamemode.dll"
        );
        Assert.IsNotNull(plugin);
        BaseClientGamemode? gamemode = null;
        try
        {
            gamemode = plugin.CreateClient(
                new ClientGamemodeParameters
                {
                    Players =
                    [
                        new ClientSidePlayerInfo
                        {
                            PlayerName = "Human", CarName = "missing/checkpoint-free-test", Color = default,
                            IsBot = false, IsClientPlayer = true
                        }
                    ]
                },
                new StubContext(initialStage: new EmptyStage()),
                new Dictionary<string, object> { ["simulation"] = simulation }
            );
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
            Assert.IsTrue(
                botCar.Position.Z > startZ,
                $"the bot did not drive along its path (z {startZ} -> {botCar.Position.Z})"
            );

            // And it drives down the line, not sideways off the start: a fresh car reads as
            // airborne until its wheels settle, which used to count as a take-off and send it
            // chasing the other car instead of racing.
            Assert.IsTrue(
                FixedMathSharp.FixedMath.Abs(botCar.Position.X - startX)
                < FixedMathSharp.FixedMath.Abs(botCar.Position.Z - startZ),
                $"the bot veered off the line (x {startX} -> {botCar.Position.X}, z {startZ} -> {botCar.Position.Z})"
            );
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

                Assert.IsTrue(
                    dz > dx,
                    $"{gamemode.Players[i].Info.PlayerName} went sideways (dx {dx:0}, dz {dz:0}) - it is chasing a car, not the path"
                );
            }
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

    /// <summary>
    /// A closed rectangular path with three gates. Free play has no campaign stage number,
    /// so the generic driver has to clear its own lap: the old rubber-banding let it coast
    /// and stop aiming for seconds at a time, which this covers.
    /// </summary>
    [TestMethod]
    public void Classic_ClearsCheckpointsAroundAClosedPath()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            var stage = new LoopStage();
            gamemode = new DrivingGamemode(stage, carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            for (var i = 0; i < 4000 && botCar.TotalCheckpoint < 3; i++)
            {
                gamemode.GameTick();
                CheckPointHelper.HandleCheckPoint(stage, botCar);
            }

            Assert.IsTrue(
                botCar.TotalCheckpoint >= 3,
                $"the bot only reached checkpoint {botCar.TotalCheckpoint} of the closed path"
            );
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

    /// <summary>
    /// Starting past a gate without clearing it is the case the old path walk got wrong: it
    /// kept driving forwards because the nearest point had moved on. The driver has to turn
    /// back and clear the gate it still owes.
    /// </summary>
    [TestMethod]
    public void Classic_TurnsBackForAGateItPassedWithoutClearing()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            var stage = new LoopStage();
            gamemode = new DrivingGamemode(stage, carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            // The first gate is at (0, 6000); put the car well beyond it with the gate still owed.
            var start = botCar.Position;
            botCar.Position = new f64Vector3(start.X, start.Y, 11500);
            botCar.CurrentCheckpoint = 0;
            botCar.LastCheckpointNode = -1;
            botCar.TotalCheckpoint = 0;

            for (var i = 0; i < 1500 && botCar.Position.Z > 11000; i++)
            {
                gamemode.GameTick();
                CheckPointHelper.HandleCheckPoint(stage, botCar);
            }

            Assert.IsTrue(
                botCar.Position.Z <= 11000,
                $"the bot kept driving away from the gate it owed (z {start.Z:0} -> {botCar.Position.Z:0})"
            );
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

    /// <summary>
    /// Stage 1 puts a gate at z=28000 after the road node at z=29000. Tracking the nearest
    /// point in both directions picks the road node while the car is beside the gate, then
    /// aims at the gate behind it and the driver oscillates there.
    /// </summary>
    [TestMethod]
    public void Classic_DoesNotTurnBackWhereAGateFollowsARoadNodeSpatially()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            var stage = new OutOfOrderStage();
            gamemode = new DrivingGamemode(stage, carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            for (var i = 0; i < 1500 && botCar.Position.Z < 20000; i++)
            {
                gamemode.GameTick();
                CheckPointHelper.HandleCheckPoint(stage, botCar);
            }

            Assert.IsTrue(
                botCar.Position.Z >= 20000,
                $"the bot stalled at the gate that sits behind its road node (z {botCar.Position.Z:0})"
            );
            Assert.IsTrue(
                botCar.TotalCheckpoint >= 1,
                "the bot never cleared the out-of-order gate"
            );
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

    /// <summary>
    /// Snake Dance offsets its checkpoint gates sideways from the `)p` line. The driver must
    /// aim at the gate itself; turning in to the next road point drives past it.
    /// </summary>
    [TestMethod]
    public void Classic_ClearsLateralCheckpointsOffThePathLine()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            var stage = new SnakeStage();
            gamemode = new DrivingGamemode(stage, carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            for (var i = 0; i < 3000 && botCar.TotalCheckpoint < 3; i++)
            {
                gamemode.GameTick();
                CheckPointHelper.HandleCheckPoint(stage, botCar);
            }

            Assert.IsTrue(
                botCar.TotalCheckpoint >= 3,
                $"the bot only cleared {botCar.TotalCheckpoint} lateral checkpoints"
            );
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

    /// <summary>
    /// A closed `)po` fix road is not part of the lap: racing has to skip its points.
    /// </summary>
    [TestMethod]
    public void Classic_SkipsClosedFixRoadWhileRacing()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new FixRoadStage(), carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            for (var i = 0; i < 1500; i++)
                gamemode.GameTick();

            Assert.IsTrue(
                FixedMathSharp.FixedMath.Abs(botCar.Position.X) < 15000
                && FixedMathSharp.FixedMath.Abs(botCar.Position.Z) < 15000,
                $"the bot followed the fix road while racing (x {botCar.Position.X:0}, z {botCar.Position.Z:0})"
            );
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

    /// <summary>A damaged driver should turn onto an ahead, closed `)po` fix road.</summary>
    [TestMethod]
    public void Classic_EntersClosedFixRoadWhenDamaged()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new FixRoadStage(), carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);
            botCar.CarPhysics.DamagePoints = botCar.CarPhysics.Stat.Maxmag * 9 / 10;

            var advanced = false;
            for (var i = 0; i < 3000 && !advanced; i++)
            {
                gamemode.GameTick();
                // Past the lane entry and along it, not orbiting the `)po` node.
                advanced = botCar.Position.X > 15000 && botCar.Position.Z > 9000;
            }

            Assert.IsTrue(
                advanced,
                $"the damaged bot entered but did not advance down the fix road (x {botCar.Position.X:0}, z {botCar.Position.Z:0})"
            );
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

    /// <summary>
    /// A `)po` lane is enterable from either end. Entering at the closing marker must drive
    /// the lane backwards through the hoop, not turn around to the far entry.
    /// </summary>
    [TestMethod]
    public void Classic_EntersClosedFixRoadFromTheClosingMarker()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new FixRoadStage(), carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);
            botCar.Position = new f64Vector3(30000, botCar.Position.Y, 10000);
            botCar.CarPhysics.DamagePoints = botCar.CarPhysics.Stat.Maxmag * 9 / 10;

            var advanced = false;
            for (var i = 0; i < 2500 && !advanced; i++)
            {
                gamemode.GameTick();
                // Entered at the closing marker (z=12000) and drove back down the lane.
                advanced = botCar.Position.X > 25000 && botCar.Position.Z < 3000;
            }

            Assert.IsTrue(
                advanced,
                $"the damaged bot did not drive the lane in reverse (x {botCar.Position.X:0}, z {botCar.Position.Z:0})"
            );
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

    /// <summary>
    /// Older nfm2 tracks (Maximum Overfly) have one `)po` and no closing marker: the block
    /// runs to the end of the node list. It still must be skipped while racing.
    /// </summary>
    [TestMethod]
    public void Classic_SkipsOpenTrailingFixRoadWhileRacing()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new OpenFixRoadStage(), carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            for (var i = 0; i < 1500; i++)
                gamemode.GameTick();

            Assert.IsTrue(
                FixedMathSharp.FixedMath.Abs(botCar.Position.X) < 15000
                && FixedMathSharp.FixedMath.Abs(botCar.Position.Z) < 15000,
                $"the bot followed the open fix road while racing (x {botCar.Position.X:0}, z {botCar.Position.Z:0})"
            );
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
    public void Classic_EntersOpenTrailingFixRoadWhenDamaged()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new OpenFixRoadStage(), carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);
            botCar.CarPhysics.DamagePoints = botCar.CarPhysics.Stat.Maxmag * 9 / 10;

            var advanced = false;
            for (var i = 0; i < 3000 && !advanced; i++)
            {
                gamemode.GameTick();
                advanced = botCar.Position.X > 15000 && botCar.Position.Z > 9000;
            }

            Assert.IsTrue(
                advanced,
                $"the damaged bot entered but did not advance down the open fix road (x {botCar.Position.X:0}, z {botCar.Position.Z:0})"
            );
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

    /// <summary>
    /// Grapefruit appends a detached practice lane with plain `)p` tags after the lap. Those
    /// points must not become the next target after the final loop node.
    /// </summary>
    [TestMethod]
    public void Classic_IgnoresDetachedTrailingPathNodes()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new LoopStage(), carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            for (var i = 0; i < 2500; i++)
                gamemode.GameTick();

            Assert.IsTrue(
                FixedMathSharp.FixedMath.Abs(botCar.Position.X) < 15000
                && FixedMathSharp.FixedMath.Abs(botCar.Position.Z) < 15000,
                $"the bot followed the detached lane (x {botCar.Position.X:0}, z {botCar.Position.Z:0})"
            );
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

    /// <summary>
    /// Stage 1's first gate is followed by a gate eleven of twenty path nodes away. Treating
    /// "more than half a lap to the gate" as "I passed it" made the driver aim straight at
    /// the far gate and cut across the map instead of following the route between them.
    /// </summary>
    [TestMethod]
    public void Classic_FollowsTheRouteWhenTheNextGateIsMoreThanHalfALapAway()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            var stage = new WideGateStage();
            gamemode = new DrivingGamemode(stage, carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            // The far corner can only be reached by driving the top of the loop; a straight
            // line from the first gate to the second crosses the infield instead.
            var reachedFarCorner = false;
            for (var i = 0; i < 2500 && botCar.TotalCheckpoint < 2; i++)
            {
                gamemode.GameTick();
                CheckPointHelper.HandleCheckPoint(stage, botCar);

                if (botCar.Position.X > 9000 && botCar.Position.Z > 9000)
                    reachedFarCorner = true;
            }

            Assert.IsTrue(
                reachedFarCorner,
                $"the bot cut the infield instead of following the route (x {botCar.Position.X:0}, z {botCar.Position.Z:0})"
            );
            Assert.IsTrue(
                botCar.TotalCheckpoint >= 2,
                $"the bot only cleared {botCar.TotalCheckpoint} gates"
            );
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

    /// <summary>
    /// A damaged driver used to chase the fix approach point even after passing it, which
    /// turned it around and made it circle the hoop. It must keep racing to the next gate.
    /// </summary>
    [TestMethod]
    public void Classic_DoesNotTurnBackForAFixHoopBehindIt()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            var stage = new FixHoopStage();
            gamemode = new DrivingGamemode(stage, carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);

            // Badly damaged, so the driver wants to repair.
            botCar.CarPhysics.DamagePoints = botCar.CarPhysics.Stat.Maxmag * 9 / 10;

            for (var i = 0; i < 1500 && botCar.Position.Z < 15000; i++)
                gamemode.GameTick();

            Assert.IsTrue(
                botCar.Position.Z >= 15000,
                $"the bot turned back toward the fix approach (z {botCar.Position.Z:0})"
            );
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

    /// <summary>
    /// Pinning the car is the same problem as driving into a wall: no progress while the bot
    /// asks for gas. The classic driver must back out instead of pushing forever.
    /// </summary>
    [TestMethod]
    public void Classic_BacksOutWhenItCannotMove()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = Load("classic")!;
        BaseClientGamemode? gamemode = null;
        var carName = $"sandbox-test/{Guid.NewGuid():N}";
        try
        {
            var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
            BackendGameSparker.dynamic_models.Add(carName, (0, car));

            gamemode = new DrivingGamemode(new StraightStage(), carName);
            gamemode.Begin();

            var botCar = gamemode.Players[1].Car;
            Assert.IsNotNull(botCar);
            var pinned = botCar.Position;

            var reversed = false;
            for (var i = 0; i < 400 && !reversed; i++)
            {
                botCar.Position = pinned;
                botCar.CarPhysics.Mtouch = true;
                botCar.CarPhysics.Wtouch = true;
                gamemode.GameTick();
                reversed = botCar.Control.Down && !botCar.Control.Up;
            }

            Assert.IsTrue(reversed, "the bot kept asking for gas instead of backing out of a stalled position");
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
                    mesh,
                    new FixedMathSharp.Vector3d(0, 250, i * 1000),
                    f64Euler.Identity,
                    kind == AiNodeKind.CheckPoint ? PiecePlacementType.CheckPoint : PiecePlacementType.CollisionObject,
                    kind
                );
                // CreateDefaultObject keeps the kind on the placement; the runtime reads it off
                // the object, which BackendStage.LoadStageInternal copies across.
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
                if (kind == AiNodeKind.CheckPoint)
                    Checkpoints[Checkpoints.Count] = node;
            }

            // Close the loop near the start so the path trim sees a lap rather than a
            // detached tail (the checkpoint stays on the straight).
            var close = StageObject.CreateDefaultObject(
                mesh,
                new FixedMathSharp.Vector3d(0, 250, 1000),
                f64Euler.Identity,
                PiecePlacementType.CollisionObject,
                AiNodeKind.Road
            );
            close.Kind = AiNodeKind.Road;
            Nodes[Nodes.Count] = close;
        }
    }

    /// <summary>A closed rectangle, with a gate in the middle of three of its sides.</summary>
    private sealed class LoopStage : BackendStage
    {
        public LoopStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/loop");
            var points = new (int X, int Z, AiNodeKind Kind)[]
            {
                (0, 0, AiNodeKind.Road),
                (0, 6000, AiNodeKind.CheckPoint),
                (0, 12000, AiNodeKind.Road),
                (6000, 12000, AiNodeKind.CheckPoint),
                (12000, 12000, AiNodeKind.Road),
                (12000, 6000, AiNodeKind.CheckPoint),
                (12000, 0, AiNodeKind.Road),
                (6000, 0, AiNodeKind.Road),
                // Detached practice lane, like Grapefruit's trailing `)p` nodes.
                (40000, 40000, AiNodeKind.Road),
                (40000, 50000, AiNodeKind.Road)
            };

            foreach (var (x, z, kind) in points)
            {
                var node = StageObject.CreateDefaultObject(
                    mesh,
                    new FixedMathSharp.Vector3d(x, 250, z),
                    f64Euler.Identity,
                    kind == AiNodeKind.CheckPoint ? PiecePlacementType.CheckPoint : PiecePlacementType.CollisionObject,
                    kind
                );
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
                if (kind == AiNodeKind.CheckPoint)
                    Checkpoints[Checkpoints.Count] = node;
            }
        }
    }

    /// <summary>A straight whose first gate is listed after a road node that is further along.</summary>
    private sealed class OutOfOrderStage : BackendStage
    {
        public OutOfOrderStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/outoforder");
            var points = new (int Z, AiNodeKind Kind)[]
            {
                (0, AiNodeKind.Road),
                (6000, AiNodeKind.Road),
                (12000, AiNodeKind.Road),
                (11000, AiNodeKind.CheckPoint),
                (18000, AiNodeKind.Road),
                (24000, AiNodeKind.Road),
                (30000, AiNodeKind.CheckPoint),
                (32000, AiNodeKind.Road),
                (1000, AiNodeKind.Road)
            };

            foreach (var (z, kind) in points)
            {
                var node = StageObject.CreateDefaultObject(
                    mesh,
                    new FixedMathSharp.Vector3d(0, 250, z),
                    f64Euler.Identity,
                    kind == AiNodeKind.CheckPoint ? PiecePlacementType.CheckPoint : PiecePlacementType.CollisionObject,
                    kind
                );
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
                if (kind == AiNodeKind.CheckPoint)
                    Checkpoints[Checkpoints.Count] = node;
            }
        }
    }

    /// <summary>
    /// A rectangle whose second gate is six of ten nodes after the first, the same shape as
    /// stage 1's first-to-second gate spacing.
    /// </summary>
    private sealed class WideGateStage : BackendStage
    {
        public WideGateStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/widegate");
            var points = new (int X, int Z, AiNodeKind Kind)[]
            {
                (0, 0, AiNodeKind.Road),
                (0, 6000, AiNodeKind.Road),
                (0, 12000, AiNodeKind.CheckPoint),
                (6000, 12000, AiNodeKind.Road),
                (12000, 12000, AiNodeKind.Road),
                (12000, 9000, AiNodeKind.Road),
                (12000, 6000, AiNodeKind.Road),
                (12000, 3000, AiNodeKind.Road),
                (12000, 0, AiNodeKind.Road),
                (6000, 0, AiNodeKind.CheckPoint)
            };

            foreach (var (x, z, kind) in points)
            {
                // The bottom gate is crossed along X, so its box must be turned to face it.
                var rotation = kind == AiNodeKind.CheckPoint && z == 0
                    ? new f64Euler(f64AngleSingle.FromDegrees(90), default, default)
                    : f64Euler.Identity;

                var node = StageObject.CreateDefaultObject(
                    mesh,
                    new FixedMathSharp.Vector3d(x, 250, z),
                    rotation,
                    kind == AiNodeKind.CheckPoint ? PiecePlacementType.CheckPoint : PiecePlacementType.CollisionObject,
                    kind
                );
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
                if (kind == AiNodeKind.CheckPoint)
                    Checkpoints[Checkpoints.Count] = node;
            }
        }
    }

    /// <summary>A small loop plus a detached, closed `)po` fix road with an aerial hoop.</summary>
    private sealed class FixRoadStage : BackendStage
    {
        public FixRoadStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/fixroad");
            var points = new List<(int X, int Z, AiNodeKind Kind)>
            {
                (0, 0, AiNodeKind.Road),
                (0, 6000, AiNodeKind.Road),
                (6000, 6000, AiNodeKind.Road),
                (6000, 0, AiNodeKind.Road)
            };

            // Enough lap points that the fix block starts more than 16 path points ahead:
            // a critically damaged driver must still reach an explicit `)po` route.
            for (var i = 1; i <= 16; i++)
                points.Add((6000, i * 1000, AiNodeKind.Road));

            points.Add((30000, 0, AiNodeKind.FixRoadStart));
            points.Add((30000, 6000, AiNodeKind.Road));
            points.Add((30000, 12000, AiNodeKind.Road));
            points.Add((30000, 12000, AiNodeKind.FixRoadStart));

            foreach (var (x, z, kind) in points)
            {
                var node = StageObject.CreateDefaultObject(
                    mesh,
                    new FixedMathSharp.Vector3d(x, 250, z),
                    f64Euler.Identity,
                    PiecePlacementType.CollisionObject,
                    kind
                );
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
            }

            var hoop = StageObject.CreateDefaultObject(
                mesh,
                new FixedMathSharp.Vector3d(30000, -2000, 6000),
                f64Euler.Identity,
                PiecePlacementType.FixHoop,
                AiNodeKind.FixHoop
            );
            hoop.Kind = AiNodeKind.FixHoop;
            Nodes[Nodes.Count] = hoop;
            FixHoops[FixHoops.Count] = hoop;
        }
    }

    /// <summary>One leading `)po`, no closing marker: the fix block runs to the node list end.</summary>
    private sealed class OpenFixRoadStage : BackendStage
    {
        public OpenFixRoadStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/openfixroad");
            var points = new (int X, int Z, AiNodeKind Kind)[]
            {
                (0, 0, AiNodeKind.Road),
                (0, 6000, AiNodeKind.Road),
                (6000, 6000, AiNodeKind.Road),
                (6000, 0, AiNodeKind.Road),
                (30000, 0, AiNodeKind.FixRoadStart),
                (30000, 6000, AiNodeKind.Road),
                (30000, 9000, AiNodeKind.Ramp),
                (30000, 12000, AiNodeKind.Road)
            };

            foreach (var (x, z, kind) in points)
            {
                var node = StageObject.CreateDefaultObject(
                    mesh,
                    new FixedMathSharp.Vector3d(x, 250, z),
                    f64Euler.Identity,
                    PiecePlacementType.CollisionObject,
                    kind
                );
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
            }

            var hoop = StageObject.CreateDefaultObject(
                mesh,
                new FixedMathSharp.Vector3d(30000, -2000, 9000),
                f64Euler.Identity,
                PiecePlacementType.FixHoop,
                AiNodeKind.FixHoop
            );
            hoop.Kind = AiNodeKind.FixHoop;
            Nodes[Nodes.Count] = hoop;
            FixHoops[FixHoops.Count] = hoop;
        }
    }

    /// <summary>Snake-Dance-like: road nodes on a centre line, gates alternating to either side.</summary>
    private sealed class SnakeStage : BackendStage
    {
        public SnakeStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/snake");
            var points = new (int X, int Z, AiNodeKind Kind)[]
            {
                (0, 0, AiNodeKind.Road),
                (1520, 8000, AiNodeKind.Road),
                (3040, 11000, AiNodeKind.CheckPoint),
                (1520, 14000, AiNodeKind.Road),
                (0, 17000, AiNodeKind.CheckPoint),
                (1520, 20000, AiNodeKind.Road),
                (3040, 23000, AiNodeKind.CheckPoint),
                (1520, 26000, AiNodeKind.Road),
                (0, 29000, AiNodeKind.CheckPoint),
                (0, 1000, AiNodeKind.Road)
            };

            foreach (var (x, z, kind) in points)
            {
                var node = StageObject.CreateDefaultObject(
                    mesh,
                    new FixedMathSharp.Vector3d(x, 250, z),
                    f64Euler.Identity,
                    kind == AiNodeKind.CheckPoint ? PiecePlacementType.CheckPoint : PiecePlacementType.CollisionObject,
                    kind
                );
                node.Kind = kind;
                Nodes[Nodes.Count] = node;
                if (kind == AiNodeKind.CheckPoint)
                    Checkpoints[Checkpoints.Count] = node;
            }
        }
    }

    /// <summary>A straight path with a fix hoop on it, four nodes before the approach point.</summary>
    private sealed class FixHoopStage : BackendStage
    {
        public FixHoopStage()
        {
            var mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "test/fixhoop");
            for (var i = 0; i < 21; i++)
            {
                var node = StageObject.CreateDefaultObject(
                    mesh,
                    new FixedMathSharp.Vector3d(0, 250, i * 1000),
                    f64Euler.Identity,
                    PiecePlacementType.CollisionObject,
                    AiNodeKind.Road
                );
                node.Kind = AiNodeKind.Road;
                Nodes[Nodes.Count] = node;
            }

            var close = StageObject.CreateDefaultObject(
                mesh,
                new FixedMathSharp.Vector3d(0, 250, 1000),
                f64Euler.Identity,
                PiecePlacementType.CollisionObject,
                AiNodeKind.Road
            );
            close.Kind = AiNodeKind.Road;
            Nodes[Nodes.Count] = close;

            var hoop = StageObject.CreateDefaultObject(
                mesh,
                new FixedMathSharp.Vector3d(0, 250, 10000),
                f64Euler.Identity,
                PiecePlacementType.FixHoop,
                AiNodeKind.FixHoop
            );
            hoop.Kind = AiNodeKind.FixHoop;
            Nodes[Nodes.Count] = hoop;
            FixHoops[FixHoops.Count] = hoop;
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
            new StubContext(initialStage: stage)
        )
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
                players.Add(
                    new ClientSidePlayerInfo
                    {
                        PlayerName = $"Classic{i + 1}", CarName = carName, Color = default, IsBot = true, IsClientPlayer = false
                    }
                );
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
                    new ClientSidePlayerInfo
                    {
                        PlayerName = "Human", CarName = "nfmm/radicalone", Color = default, IsBot = false, IsClientPlayer = true
                    },
                    new ClientSidePlayerInfo
                    {
                        PlayerName = "ElStupido", CarName = "nfmm/audir8", Color = default, IsBot = true, IsClientPlayer = false
                    }
                ]
            },
            new StubContext()
        )
        {
        }

        public override string GamemodeId => "test/stub";
    }

    private sealed class EmptyStage : BackendStage
    {
    }

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
