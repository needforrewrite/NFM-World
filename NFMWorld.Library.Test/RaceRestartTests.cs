using MemoryPack;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class RaceRestartTests
{
    private static ClientGamemodeParameters CreateParameters() => new()
    {
        Players =
        [
            new ClientSidePlayerInfo(
                "test",
                "nfmm/radicalone",
                new Color3(255, 0, 0),
                isBot: false,
                isClientPlayer: true
            )
        ]
    };

    [TestMethod]
    public void ClientResetClearsResultsAndNotifiesTheRace()
    {
        var context = new ResetContext();
        var client = new TestGamemode(context);
        client.SetServerResults(
            new RaceResults
            {
                GamemodeId = client.GamemodeId,
                RaceDuration = TimeSpan.Zero,
                Standings = []
            }
        );

        client.Reset();

        Assert.IsNull(client.Results);
        Assert.AreEqual(RaceState.InProgress, context.RaceState);
        Assert.AreEqual(1, context.Resets);
    }

    [TestMethod]
    public void LocalTimeTrialCanFinishAgainAfterRestart()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var factory = GamemodeFactory.Create("nfmm/timetrial");
        try
        {
            var stage = new StageLoader { nlaps = 1 };
            stage.pieces.Add(
                new PiecePlacement(
                    PiecePlacementType.CheckPoint,
                    new Rad3d([], castsShadow: false, fileName: "restart-checkpoint"),
                    default,
                    default
                )
            );
            using var host = LocalRaceHost.Create("restart-test", factory, CreateParameters(), stage);
            var plugin = ((NativeGamemodeFactory)factory).Plugin;
            var finishedEvent = plugin.Assembly.GetExportedTypes().Single(t => t.Name == "FinishedEvent");
            var payload = MemoryPackSerializer.Serialize(finishedEvent.BaseType!, Activator.CreateInstance(finishedEvent));
            var finishes = 0;
            host.GameFinished += _ => finishes++;
            host.Start();
            for (var i = 0; i < GamemodeUtils.ServerCountdownInterval * 3; i++)
                host.Update();
            host.SendServerEvent(payload);
            host.Update();
            host.Update();
            Assert.AreEqual(1, finishes);

            host.Restart();
            host.Update();
            Assert.AreEqual(1, finishes, "The previous run's results must be cleared.");
            for (var i = 0; i < GamemodeUtils.ServerCountdownInterval * 3; i++)
                host.Update();
            host.SendServerEvent(payload);
            host.Update();
            host.Update();
            Assert.AreEqual(2, finishes, "The restarted run must report its own finish exactly once.");
        }
        finally
        {
            factory.End();
        }
    }

    private sealed class TestGamemode(ResetContext context) : BaseClientGamemode(CreateParameters(), context)
    {
        public override string GamemodeId => "test/reset";
    }

    private sealed class ResetContext : IGamemodeContext, IClientCallbacks
    {
        public BackendStage CurrentStage => throw new NotSupportedException();
        public RaceState RaceState { get; private set; } = RaceState.Finished;
        public IClientCallbacks ClientCallbacks => this;
        public int Resets { get; private set; }

        public void RaceRestarted()
        {
            RaceState = RaceState.InProgress;
            Resets++;
        }

        public void ResetCheckpointGlow()
        {
        }

        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
        {
        }

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => throw new NotSupportedException();

        public void SendServerEvent(ReadOnlySpan<byte> payload)
        {
        }

        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        {
        }
    }
}
