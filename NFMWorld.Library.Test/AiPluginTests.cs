using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.DriverInterface;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary;
using NFMWorldLibrary.Plugins;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

[TestClass]
public class AiPluginTests
{
    

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
    public void RunAi_WithoutCar_DoesNothing()
    {
        var plugin = Load("el_stupido")!;
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
            Assert.AreEqual("el_stupido", bot.AiId);
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
