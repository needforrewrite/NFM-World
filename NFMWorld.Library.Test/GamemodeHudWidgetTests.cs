using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// The gamemode HUD hook is data, not nodes: a plugin declares an anchor, a label and a 0..1
/// accessor, and the host builds the widget. This checks the plugin actually declares it, so a
/// gamemode's custom UI is not silently missing.
/// </summary>
[TestClass]
public class GamemodeHudWidgetTests
{
    private sealed class StubContext(string stageName = "widget-test") : IGamemodeContext
    {
        private BackendStage? _stage;

        public BackendStage CurrentStage => _stage ??= new BackendStage(stageName);
        public RaceState RaceState => RaceState.InProgress;
        public IClientCallbacks ClientCallbacks { get; } = new StubClientCallbacks();

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

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => throw new NotSupportedException();
    }

    [TestMethod]
    public void RocketGamemode_DeclaresABoostWidget()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin.Load(
            "nfmm/rocket",
            "data/gamemodes/nfmm/rocket/gamemode.dll"
        );
        Assert.IsNotNull(plugin);
        BaseClientGamemode? gamemode = null;
        var carName = $"widget-test/{Guid.NewGuid():N}";
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
                        {
                            PlayerName = "Human", CarName = carName, Color = default, IsBot = false, IsClientPlayer = true
                        }
                    ]
                },
                new StubContext()
            );

            gamemode.Begin();
            var widgets = gamemode.GetHudWidgets();

            Assert.IsNotNull(widgets);
            var boost = widgets.Single(w => w.Label == "BOOST");
            Assert.AreEqual(GamemodeHudAnchor.TopLeft, boost.Anchor);
            // The accessor is live and normalised: it reports a 0..1 fill.
            var value = boost.Value();
            Assert.IsTrue(value is >= 0 and <= 1, $"boost fill must be normalised, was {value}");
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
}
