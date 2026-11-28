using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using TightPack;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class PvpFinishPresentationTests
{
    [TestMethod]
    [DataRow(0, true, true, "YOU WASTED 'EM!")]
    [DataRow(0, false, false, "YOU WON!")]
    [DataRow(0, false, true, "YOU WON!")]
    [DataRow(1, false, false, "YOU LOST!")]
    [DataRow(-1, false, true, "YOU LOST!")]
    public void AuthoritativeOutcomeUsesRosterSlotsAndSurvivesTheResultsPacket(
        int winner,
        bool wasted,
        bool lastCarStanding,
        string title
    )
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var carName = $"finish-test/{Guid.NewGuid():N}";
        var factory = GamemodeFactory.Create("nfmm/pvp");
        BaseClientGamemode? client = null;

        try
        {
            BackendGameSparker.dynamic_models.Add(
                carName,
                (0, new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName))
            );
            client = (BaseClientGamemode)factory.CreateGameMode(new ClientGamemodeParameters
            {
                Players = [new ClientSidePlayerInfo("Human", carName, default, false, true),
                    new ClientSidePlayerInfo("Opponent", carName, default, false, false)]
            }, BackendGamemodeContext.Create("empty", new StageLoader { nlaps = 1, Name = "Test stage" }));
            client.Begin();
            var type = ((NativeGamemodeFactory)factory).Plugin.Assembly.GetExportedTypes()
                .Single(t => t.Name == "RaceFinishedEvent");
            client.OnServerEvent(
                TightPackSerializer.SerializeUntyped(
                    type.BaseType!,
                    Activator.CreateInstance(type, winner, wasted, lastCarStanding)
                )
            );
            Assert.AreEqual(title, client.HudState.ResultTitle);
            Assert.AreEqual(winner == 0, client.HudState.ClientWon);
            Assert.IsTrue(client.HudState.Finished);
            Assert.IsNull(client.HudState.StateTextEndsAt);
            var description = client.HudState.StateText;
            if (winner == 0 && lastCarStanding && !wasted)
                StringAssert.Contains(description!, "last car standing");
            client.SetServerResults(new RaceResults
            {
                Standings = [new RaceStanding { PlayerId = Guid.NewGuid(), FinishPosition = 0, FinishTime = TimeSpan.Zero }],
                RaceDuration = TimeSpan.Zero, GamemodeId = "nfmm/pvp"
            });
            Assert.AreEqual(title, client.HudState.ResultTitle);
            Assert.AreEqual(description, client.HudState.StateText);

            if (lastCarStanding)
            {
                client.Players[0].Car!.CurrentLap = 2;
                var resumeType = ((NativeGamemodeFactory)factory).Plugin.Assembly.GetExportedTypes()
                    .Single(t => t.Name == "RaceResumedEvent");
                client.OnServerEvent(
                    TightPackSerializer.SerializeUntyped(resumeType.BaseType!, Activator.CreateInstance(resumeType))
                );
                Assert.IsFalse(client.HudState.Finished);
                Assert.IsFalse(client.HudState.ClientWon);
                Assert.IsFalse(client.HudState.ClientLost);
                Assert.IsNull(client.HudState.ResultTitle);
                Assert.IsNull(client.HudState.StateText);
                Assert.IsNull(client.Results);
                Assert.IsTrue(client.Players.All(player => !player.Car!.CarPhysics.Halted));
                Assert.AreEqual(2, client.Players[0].Car!.CurrentLap);
            }
        }
        finally
        {
            try
            {
                client?.End();
            }
            finally
            {
                BackendGameSparker.dynamic_models.Remove(carName);
                factory.End();
            }
        }
    }
}
