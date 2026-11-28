using TightPack;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.FixedMath;
using f64Euler = NFMWorldLibrary.FixedMath.f64Euler;
using f64Vector3 = FixedMathSharp.Vector3d;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

/// <summary>
/// A Rocket race is scored exactly like PvP: the server validates checkpoint claims and the
/// first car round every lap wins. The arcade layer must not have quietly made it a sandbox
/// that never finishes.
/// </summary>
[TestClass]
public class RocketCompletionTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static StageObject CreateCheckpoint(fix64 x)
        => StageObject.CreateDefaultObject(
            new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "rocket-test"),
            new f64Vector3(x, World.Ground, 0),
            f64Euler.Identity,
            PiecePlacementType.CheckPoint,
            AiNodeKind.CheckPoint
        );

    private static BackendStage CreateStage(ushort laps, int checkpoints)
    {
        var stage = new BackendStage("rocket-test", new StageLoader { nlaps = laps });
        for (var i = 0; i < checkpoints; i++)
            stage.Checkpoints.Add(CreateCheckpoint(100_000 + i * 20_000));

        return stage;
    }

    private sealed class ServerContext(BackendStage stage) : IServerGamemodeContext
    {
        public BackendStage CurrentStage { get; } = stage;
        public Dictionary<Guid, f64Vector3> Positions { get; } = [];

        public f64Vector3? GetPlayerPosition(Guid playerId)
            => Positions.TryGetValue(playerId, out var p) ? p : null;

        public bool? IsPlayerWasted(Guid playerId) => false;

        public void BroadcastEvent(ReadOnlyMemory<byte> payload)
        {
        }
    }

    private static (BaseGamemodeFactory Factory, IServerGamemode Server, ServerContext Context) CreateRace(
        ushort laps,
        int checkpoints,
        params Guid[] players
    )
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var factory = GamemodeFactory.Create("nfmm/rocket");
        var context = new ServerContext(CreateStage(laps, checkpoints));
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

    private static void StartRacing(IServerGamemode server)
    {
        for (var i = 0; i < GamemodeUtils.ServerCountdownInterval * 4; i++)
            server.GameTick();
    }

    private static ReadOnlyMemory<byte> SerializeEvent(BaseGamemodeFactory factory, string name, params object[] args)
    {
        var type = ((NativeGamemodeFactory)factory).Plugin.Assembly.GetExportedTypes()
            .Single(t => t.Name == name);

        return TightPackSerializer.SerializeUntyped(type.BaseType!, Activator.CreateInstance(type, args));
    }

    /// <summary>Reports a crossing the way the client does: the checkpoint now headed for.</summary>
    private static void ReportCrossing(
        BaseGamemodeFactory factory,
        IServerGamemode server,
        ServerContext context,
        Guid player,
        int crossed,
        int checkpointCount,
        byte lap
    )
    {
        context.Positions[player] = context.CurrentStage.Checkpoints[crossed].Position;
        server.OnClientEvent(
            player,
            SerializeEvent(factory, "CheckpointEvent", (ushort)((crossed + 1) % checkpointCount), lap, 0, 0).Span
        );
    }

    /// <summary>The race ends on the final-lap crossing, scored the standard way.</summary>
    [TestMethod]
    public void RocketRace_FinishingTheFinalLap_EndsWithTheStandardStandings()
    {
        var (factory, server, context) = CreateRace(laps: 2, checkpoints: 2, Alice, Bob);

        try
        {
            StartRacing(server);

            // Alice completes both laps; Bob never moves. The event carries the checkpoint
            // being headed for next (wrapping to 0 on lap completion) plus the lap finished.
            ReportCrossing(factory, server, context, Alice, crossed: 0, checkpointCount: 2, lap: 0);
            ReportCrossing(factory, server, context, Alice, crossed: 1, checkpointCount: 2, lap: 1);
            ReportCrossing(factory, server, context, Alice, crossed: 0, checkpointCount: 2, lap: 1);
            ReportCrossing(factory, server, context, Alice, crossed: 1, checkpointCount: 2, lap: 2);
            var snapshot = server.GetStateSnapshot();
            Assert.IsTrue(snapshot is { IsFinished: true }, "the race must end when the winner crosses the line");
            var standings = snapshot!.Value.Results!.Value.Standings;
            Assert.AreEqual(Alice, standings[0].PlayerId, "the finisher is first");
            Assert.AreEqual(Bob, standings[1].PlayerId, "the still-running car sorts behind the finisher");
        }
        finally
        {
            factory.End();
        }
    }

    [TestMethod]
    public void RocketRace_ASkippedCheckpointIsRefused()
    {
        var (factory, server, context) = CreateRace(laps: 2, checkpoints: 3, Alice);

        try
        {
            StartRacing(server);

            // Claiming the last checkpoint directly from the start must be refused.
            context.Positions[Alice] = context.CurrentStage.Checkpoints[2].Position;
            server.OnClientEvent(
                Alice,
                SerializeEvent(factory, "CheckpointEvent", (ushort)0, (byte)1, 0, 0).Span
            );

            Assert.IsNull(server.GetStateSnapshot(), "an implausible finish must not end the race");
        }
        finally
        {
            factory.End();
        }
    }
}
