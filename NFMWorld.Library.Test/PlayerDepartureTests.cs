using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Rad;
using TightPack;

namespace NFMWorld.Library.Test;

[TestClass]
public class PlayerDepartureTests
{
    private static BackendCar CreateCar()
        => new(new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "departure-test"), 0, 0, 0, false);

    [TestMethod]
    public void Departure_WastesTheCarAndCancelsRepairs()
    {
        var car = CreateCar();
        car.Control.Up = true;
        car.Fix();
        car.MarkDeparted(PlayerDepartureReason.TimedOut);
        var fixes = 0;
        car.Fixed += () => fixes++;
        car.Fix();
        car.CarPhysics.FinishedFix();
        car.ResetPosition();

        Assert.IsTrue(car.HasLeftRace);
        Assert.IsTrue(car.Wasted);
        Assert.IsFalse(car.Control.Up);
        Assert.IsFalse(car.CarPhysics.Newcar);
        Assert.IsTrue(car.CarPhysics.DamagePoints >= car.Stats.Maxmag);
        Assert.AreEqual(0, fixes);
    }

    [TestMethod]
    public void AlreadyWastedDeparture_RejectsHoopsAndLateHealthyState()
    {
        var car = CreateCar();
        var healthy = PlayerState.CreateFrom(10, car);
        car.CarPhysics.Wasted = true;
        car.MarkDeparted(PlayerDepartureReason.Disconnected);
        var stage = new TestStage();
        stage.FixHoops.Add(new StageObject(car.Rad) { Position = car.Position });

        Assert.IsFalse(FixHoopHelper.HandleFixHoops(stage, car));
        PlayerState.ApplyTo(healthy, car, 1);
        Assert.IsTrue(car.Wasted);
        Assert.IsTrue(car.CarPhysics.DamagePoints >= car.Stats.Maxmag);
    }

    [TestMethod]
    public void ConnectedWastedCar_CanStillUseFixHoops()
    {
        var car = CreateCar();
        car.CarPhysics.Wasted = true;
        var stage = new TestStage();
        stage.FixHoops.Add(new StageObject(car.Rad) { Position = car.Position });
        Assert.IsTrue(FixHoopHelper.HandleFixHoops(stage, car));
        car.CarPhysics.FinishedFix();
        Assert.IsFalse(car.Wasted);
    }

    [TestMethod]
    public void DeparturePacket_RoundTripsBothReasons()
    {
        foreach (var reason in Enum.GetValues<PlayerDepartureReason>())
        {
            var packet = new S2C_PlayerLeft { PlayerId = Guid.NewGuid(), Reason = reason, Announce = true };
            var result = TightPackSerializer.Deserialize<S2C_PlayerLeft>(TightPackSerializer.Serialize(in packet));
            Assert.AreEqual(packet.PlayerId, result.PlayerId);
            Assert.AreEqual(reason, result.Reason);
            Assert.IsTrue(result.Announce);
        }

        Assert.AreEqual(
            "Human has left the game (disconnected)",
            PlayerDeparture.Message("Human", PlayerDepartureReason.Disconnected)
        );
        Assert.AreEqual("Human has left the game (timed out)", PlayerDeparture.Message("Human", PlayerDepartureReason.TimedOut));
    }

    private sealed class TestStage : BackendStage;
}
