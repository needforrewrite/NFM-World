using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using TightPack;

namespace NFMWorld.Library.Test;

[TestClass]
public class DamageCreditTests
{
    [TestMethod]
    public void WallImpact_DoesNotEraseTheCarThatCausedIt()
    {
        var physics = new CarPhysics(CarStats.Default, 1, false);
        physics.RecordDamageSource(0);
        physics.DamagePoints += 100;
        physics.RecordDamageSource(-1);

        Assert.AreEqual(0, physics.LastDamageFrom);
    }

    [TestMethod]
    public void LongFlight_AndFatalLanding_PreserveCreditUntilWasted()
    {
        var physics = new CarPhysics(CarStats.Default, 1, false);
        physics.RecordDamageSource(0);
        for (var tick = 0; tick < (int)(Physics.TargetTps * 60); tick++) physics.UpdateDamageCredit();
        Assert.AreEqual(0, physics.LastDamageFrom, "flight time must not expire a causal hit");

        physics.Mtouch = true;
        physics.DamagePoints = physics.Stat.Maxmag;
        physics.RecordDamageSource(-1);
        for (var tick = 0; tick < 8; tick++) physics.UpdateDamageCredit();
        physics.Wasted = true;
        physics.RecordDamageSource(2);
        Assert.AreEqual(0, physics.LastDamageFrom, "destruction delay and later contacts must not change the killer");
    }

    [TestMethod]
    public void RecoveredDriving_ClearsOldCreditBeforeAnUnrelatedCrash()
    {
        var physics = new CarPhysics(CarStats.Default, 1, false) { Mtouch = true };
        physics.RecordDamageSource(0);
        for (var tick = 0; tick < CarPhysics.DamageCreditRecoveryTicks; tick++) physics.UpdateDamageCredit();
        physics.DamagePoints += 100;
        physics.RecordDamageSource(-1);
        Assert.AreEqual(-1, physics.LastDamageFrom);
    }

    [TestMethod]
    public void TumblingOrFurtherDamage_InterruptsRecovery()
    {
        var physics = new CarPhysics(CarStats.Default, 1, false) { Mtouch = true, Pxy = 90 };
        physics.RecordDamageSource(0);
        for (var tick = 0; tick < CarPhysics.DamageCreditRecoveryTicks * 2; tick++) physics.UpdateDamageCredit();
        Assert.AreEqual(0, physics.LastDamageFrom);
        physics.Pxy = 0;
        for (var tick = 0; tick < CarPhysics.DamageCreditRecoveryTicks - 1; tick++) physics.UpdateDamageCredit();
        physics.DamagePoints++;
        physics.UpdateDamageCredit();
        Assert.AreEqual(0, physics.LastDamageFrom);
    }

    [TestMethod]
    public void GroundedSkid_DoesNotCountAsRecoveredDriving()
    {
        var physics = new CarPhysics(CarStats.Default, 1, false) { Mtouch = true, Skid = 1 };
        physics.RecordDamageSource(0);
        for (var tick = 0; tick < CarPhysics.DamageCreditRecoveryTicks * 2; tick++) physics.UpdateDamageCredit();
        Assert.AreEqual(0, physics.LastDamageFrom);
    }

    [TestMethod]
    public void NewAttacker_ReplacesCredit_AndRepairOrResetClearsIt()
    {
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "credit-test");
        var car = new BackendCar(rad, 1, 0, 0, false);
        var physics = car.CarPhysics;
        physics.RecordDamageSource(0);
        physics.RecordDamageSource(2);
        Assert.AreEqual(2, physics.LastDamageFrom);
        physics.FinishedFix();
        Assert.AreEqual(-1, physics.LastDamageFrom);
        physics.RecordDamageSource(0);
        car.ResetPosition();
        Assert.AreEqual(-1, physics.LastDamageFrom);
        physics.RecordDamageSource(1);
        Assert.AreEqual(-1, physics.LastDamageFrom, "a car cannot credit itself");
    }

    [TestMethod]
    public void OwnerCredit_RoundTripsAndReplacesRemotePrediction()
    {
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "credit-network-test");
        var owner = new BackendCar(rad, 1, 0, 0, false);
        var remote = new BackendCar(rad, 1, 0, 0, false);
        owner.CarPhysics.RecordDamageSource(0);
        owner.CarPhysics.Wasted = true;
        remote.CarPhysics.RecordDamageSource(2);
        var sent = PlayerState.CreateFrom(10, owner);
        var received = TightPackSerializer.Deserialize<PlayerState>(TightPackSerializer.Serialize(sent));

        PlayerState.ApplyTo(received, remote, 3);
        remote.CarPhysics.UpdateDamageCredit();

        Assert.IsTrue(remote.Wasted);
        Assert.AreEqual(0, remote.CarPhysics.LastDamageFrom);
        Assert.AreEqual(1, received.DamageSource, "index zero must survive the no-attacker encoding");
    }
}
