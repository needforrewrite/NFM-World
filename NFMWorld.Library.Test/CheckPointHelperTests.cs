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
            new f64Vector3(x, World.Ground, 0), f64Euler.Identity,
            PiecePlacementType.CheckPoint, AiNodeKind.CheckPoint);

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
