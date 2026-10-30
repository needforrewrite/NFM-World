using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.DriverInterface;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary;
using NFMWorldLibrary.Plugins;

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

    private sealed class StubContext : IGamemodeContext
    {
        // Lazy: building a stage at all reads stage data and reports failures through
        // Sentry, which these tests do not initialize.
        private BackendStage? _stage;

        public BackendStage CurrentStage => _stage ??= new BackendStage("test_stage");

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
