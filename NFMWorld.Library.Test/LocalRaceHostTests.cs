using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class LocalRaceHostTests
{
    [TestMethod]
    public void ClientOnlyTimeTrial_StartsAndRestartsWithoutLoadingStageFiles()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var factory = GamemodeFactory.Create("nfmm/timetrial");
        try
        {
            Assert.IsFalse(factory.HasServerGamemode);
            using var host = LocalRaceHost.Create($"missing/{Guid.NewGuid():N}", factory,
                new ClientGamemodeParameters
                {
                    Players = [new ClientSidePlayerInfo("test", "unused/car", new Color3(255, 0, 0),
                        isBot: false, isClientPlayer: true)]
                });
            var starts = 0;
            host.RaceCanStart += () => starts++;

            host.Start();
            host.Update();
            host.Restart();

            Assert.AreEqual(2, starts);
        }
        finally
        {
            factory.End();
        }
    }
}
