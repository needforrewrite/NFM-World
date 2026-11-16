using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using f64Euler = NFMWorldLibrary.FixedMath.f64Euler;
using f64Vector3 = FixedMathSharp.Vector3d;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

[TestClass]
public class CheckPointHelperTests
{
    private static readonly string CarName = $"checkpoint-helper/{Guid.NewGuid():N}";

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 0)]
    [DataRow(2, 0)]
    [DataRow(0, 1)]
    public void CalculatePositions_UsesTheNextUnclearedCheckpoint(int next, int lap)
    {
        var stage = new BackendStage("checkpoint-helper", new StageLoader { nlaps = 4 });
        stage.Checkpoints.Add(Checkpoint(10_000));
        stage.Checkpoints.Add(Checkpoint(0));
        stage.Checkpoints.Add(Checkpoint(20_000));
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        var players = new[] { Player(0, rad, next + lap * 3), Player(1, rad, next + lap * 3) };
        var target = stage.Checkpoints[next].Position;
        var following = stage.Checkpoints[(next + 1) % 3].Position;
        for (var i = 0; i < players.Length; i++)
        {
            players[i].Car!.CurrentCheckpoint = (ushort)next;
            players[i].Car!.CurrentLap = (byte)lap;
        }

        players[0].Car!.Position = target + (following - target) / (fix64)10;
        players[1].Car!.Position = target + (following - target) * (fix64)0.9;

        CheckPointHelper.CalculatePositions(stage, players);

        Assert.AreEqual(0, players[0].Car!.Placement, "closer to the uncleared gate must lead");
        Assert.AreEqual(1, players[1].Car!.Placement);
        CheckPointHelper.CalculatePositions(stage, players.Reverse().ToArray());
        Assert.AreEqual(0, players[0].Car!.Placement, "roster order must not change unequal distances");
    }

    [TestMethod]
    public void CalculatePositions_RemainsCorrectWhenBothCarsCrossTheCheckpoint()
    {
        var stage = new BackendStage("checkpoint-helper", new StageLoader { nlaps = 4 });
        stage.Checkpoints.Add(Checkpoint(10_000));
        stage.Checkpoints.Add(Checkpoint(0));
        stage.Checkpoints.Add(Checkpoint(20_000));
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        var players = new[] { Player(0, rad, 0), Player(1, rad, 0) };
        var leader = players[0].Car!;
        var follower = players[1].Car!;
        leader.Position = new f64Vector3(9_000, World.Ground, 0);
        follower.Position = new f64Vector3(1_000, World.Ground, 0);
        CheckPointHelper.CalculatePositions(stage, players);
        Assert.AreEqual(0, leader.Placement);

        leader.Position = stage.Checkpoints[0].Position;
        Assert.IsTrue(CheckPointHelper.HandleCheckPoint(stage, leader));
        CheckPointHelper.CalculatePositions(stage, players);
        Assert.AreEqual(0, leader.Placement, "checkpoint count takes priority over distance");

        leader.Position = new f64Vector3(9_000, World.Ground, 0);
        follower.Position = stage.Checkpoints[0].Position;
        Assert.IsTrue(CheckPointHelper.HandleCheckPoint(stage, follower));
        CheckPointHelper.CalculatePositions(stage, players);
        Assert.AreEqual(0, leader.Placement, "once progress is equal, compare distance to the new next gate");
        Assert.AreEqual(1, follower.Placement);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(2, 0)]
    [DataRow(0, 1)]
    [DataRow(1, 2)]
    public void PlayerState_RestoresRemoteCheckpointAndLapProgress(int checkpoint, int lap)
    {
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        var source = Player(0, rad, checkpoint + lap * 3).Car!;
        source.CurrentCheckpoint = (ushort)checkpoint;
        source.CurrentLap = (byte)lap;
        var remote = Player(1, rad, 99).Car!;
        var state = PlayerState.CreateFrom(123, source);

        PlayerState.ApplyTo(state, remote, 3);

        Assert.AreEqual(source.CurrentCheckpoint, remote.CurrentCheckpoint);
        Assert.AreEqual(source.CurrentLap, remote.CurrentLap);
        Assert.AreEqual(source.TotalCheckpoint, remote.TotalCheckpoint);
    }

    [TestMethod]
    public void CalculatePositions_WithoutCheckpoints_KeepsTiesInRosterOrder()
    {
        var stage = new BackendStage("checkpoint-helper", new StageLoader());
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        var players = new[] { Player(0, rad, 0), Player(1, rad, 0), Player(2, rad, 0) };

        CheckPointHelper.CalculatePositions(stage, players);

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, players.Select(p => p.Car!.Placement).ToArray());
    }

    [TestMethod]
    public void CalculatePositions_RanksWastedCarsBehindLiveOnes()
    {
        var stage = new BackendStage("checkpoint-helper", new StageLoader { nlaps = 1 });
        for (var i = 0; i < 3; i++)
            stage.Checkpoints.Add(Checkpoint(100_000 + i * 20_000));

        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        var players = new[]
        {
            Player(0, rad, totalCheckpoint: 10, wasted: true),
            Player(1, rad, totalCheckpoint: 6),
            Player(2, rad, totalCheckpoint: 5)
        };

        CheckPointHelper.CalculatePositions(stage, players);

        Assert.AreEqual(0, players[1].Car!.Placement, "the live car with the most progress leads");
        Assert.AreEqual(1, players[2].Car!.Placement);
        Assert.AreEqual(2, players[0].Car!.Placement, "a wasted car ranks behind every live car");
    }

    private static StageObject Checkpoint(fix64 x)
        => StageObject.CreateDefaultObject(
            new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName),
            new f64Vector3(x, World.Ground, 0),
            f64Euler.Identity,
            PiecePlacementType.CheckPoint,
            AiNodeKind.CheckPoint
        );

    private static ClientSidePlayer Player(int index, Rad3d rad, int totalCheckpoint, bool wasted = false)
    {
        var info = new ClientSidePlayerInfo
        {
            PlayerName = $"player{index}",
            CarName = rad.FileName,
            Color = new Color3(255, 0, 0),
            IsBot = false,
            IsClientPlayer = index == 0
        };
        var player = new ClientSidePlayer(info, index)
        {
            Car = new BackendCar(rad, index, 0, 0, false) { TotalCheckpoint = totalCheckpoint }
        };
        player.Car.CarPhysics.Wasted = wasted;
        return player;
    }
}
