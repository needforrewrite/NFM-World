using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

/// <summary>
/// Which car a lock key picks. The same resolver drives the HUD arrow and the spectate camera,
/// so a locked car has to be the one car in both, and a wasted car is never picked at all.
/// </summary>
[TestClass]
public class LockTargetTests
{
    private static BackendCar CreateCar()
        => new(new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "lock-test"), 0, 0, 0, false);

    private static ClientSidePlayer Player(int index, BackendCar? car)
    {
        var player = new ClientSidePlayer(
            new ClientSidePlayerInfo($"p{index}", "test", default, isBot: false, isClientPlayer: false),
            index
        );
        player.Car = car;
        return player;
    }

    [TestMethod]
    public void LockedCar_ReturnsTheCarAtTheLockedPlayerIndex()
    {
        var first = CreateCar();
        var second = CreateCar();
        var opponents = new[] { Player(1, first), Player(2, second) };

        Assert.AreSame(second, LockTargetHelper.LockedCar(opponents, 2));
    }

    [TestMethod]
    public void LockedCar_IsNullWhenTheLockIsNotSet()
    {
        var opponents = new[] { Player(1, CreateCar()) };

        Assert.IsNull(
            LockTargetHelper.LockedCar(opponents, -1),
            "no lock means the caller falls back to the nearest car"
        );
    }

    [TestMethod]
    public void LockedCar_IgnoresAWastedCar()
    {
        var wasted = CreateCar();
        wasted.CarPhysics.Wasted = true;
        var opponents = new[] { Player(1, wasted) };

        Assert.IsNull(
            LockTargetHelper.LockedCar(opponents, 1),
            "a wasted car cannot be followed - the lock must fall through to a live car"
        );
    }
}
