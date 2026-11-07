using System.Numerics;
using System.Reflection;
using FixedMathSharp;
using FixedMathSharp.Utility;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

[TestClass]
public class CrashSoundQueueTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ObstacleDamageReportsItsContactForLocalAndOtherCars(bool local)
    {
        var model = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "crash-test");
        var car = new BackendCar(model, 6, 0, 0, local);
        var played = new List<CrashSoundEvent>();
        car.CarPhysics.SfxPlayCrash += (_, sound) => played.Add(sound);
        var position = new Vector3(11, 12, 13);
        var registerDamage = typeof(CarPhysics).GetMethod("Regz", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Reflection fills optional parameters with nothing, so every parameter is passed.
        registerDamage.Invoke(car.CarPhysics,
            [1, (Fixed64)750, new ContO(car), new DeterministicRandom(1), 12, position, -1]);

        Assert.AreEqual(1, played.Count);
        Assert.AreEqual(CrashSoundContact.Obstacle(6, 1, 12), played[0].Contact);
        Assert.AreEqual(position, played[0].Position);
        Assert.AreEqual(0, played[0].Kind);
    }

    [TestMethod]
    public void BothCarsAndDamageAxesProduceOneSoundAtTheContact()
    {
        var queue = new CrashSoundQueue<object>();
        var first = new object();
        var second = new object();
        var forward = CrashSoundContact.Cars(0, 1, 2, 3);
        var reverse = CrashSoundContact.Cars(2, 3, 0, 1);
        Assert.AreEqual(forward, reverse);
        queue.Enqueue(first, new(forward, new(10, 20, 30), 50, 0));
        queue.Enqueue(second, new(reverse, new(10, 20, 30), 200, 0));
        queue.Enqueue(first, new(forward, new(10, 20, 30), -100, 0));
        var played = new List<(object Source, CrashSoundEvent Sound)>();

        queue.Flush((source, sound) => played.Add((source, sound)));

        Assert.AreEqual(1, played.Count);
        Assert.AreSame(second, played[0].Source);
        Assert.AreEqual(200f, played[0].Sound.Strength);
        Assert.AreEqual(new Vector3(10, 20, 30), played[0].Sound.Position);
    }

    [TestMethod]
    public void AnotherCarAndItsWallHitRemainSeparateContacts()
    {
        var queue = new CrashSoundQueue<object>();
        var player = new object();
        var otherCar = new object();
        var thirdCar = new object();
        queue.Enqueue(player, new(CrashSoundContact.Cars(0, 0, 1, 0), Vector3.Zero, 100, 0));
        queue.Enqueue(otherCar, new(CrashSoundContact.Obstacle(1, 0, 17), Vector3.One, 150, 0));
        queue.Enqueue(thirdCar, new(CrashSoundContact.Cars(2, 0, 3, 0), Vector3.UnitX, 200, 0));
        var played = new List<CrashSoundEvent>();

        queue.Flush((_, sound) => played.Add(sound));

        Assert.AreEqual(2, played.Count);
        Assert.IsFalse(played.Any(sound => sound.Contact.Surface == 17));
        for (var i = 0; i < CrashSpacingTicks; i++) queue.Flush((_, sound) => played.Add(sound));
        Assert.AreEqual(3, played.Count);
        Assert.AreEqual(3, played.Select(sound => sound.Contact).Distinct().Count());
    }

    [TestMethod]
    public void ManyPointsAndObstaclesCannotBuildAPlaybackBacklog()
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        var contacts = new[]
        {
            CrashSoundContact.Cars(0, 0, 1, 0),
            CrashSoundContact.Cars(0, 1, 1, 0),
            CrashSoundContact.Obstacle(0, 0, 10),
            CrashSoundContact.Obstacle(0, 0, 11)
        };
        foreach (var contact in contacts) queue.Enqueue(source, new(contact, Vector3.Zero, 100, 0));
        var count = 0;

        queue.Flush((_, _) => count++);

        Assert.AreEqual(1, count);
        for (var i = 0; i < CrashSpacingTicks * (contacts.Length - 1); i++) queue.Flush((_, _) => count++);
        Assert.AreEqual(2, count);
    }

    private static int CrashSpacingTicks => Math.Max(1, (int)MathF.Ceiling(Physics.TargetTps * 0.1f));

    [TestMethod]
    public void AdditionalContactsShareOneDelayedFollowupWithoutExtendingItsDelay()
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        var played = new List<CrashSoundEvent>();
        for (var point = 0; point < 3; point++)
            queue.Enqueue(source, new(CrashSoundContact.Cars(0, point / 4, 1, point % 4), Vector3.Zero, 100 + point, 0));
        queue.Flush((_, sound) => played.Add(sound));
        Assert.AreEqual(1, played.Count);

        for (var i = 1; i < CrashSpacingTicks; i++)
        {
            queue.Enqueue(source, new(CrashSoundContact.Cars(0, (3 + i) / 4, 1, (3 + i) % 4), Vector3.One, 200 + i, 0));
            queue.Flush((_, sound) => played.Add(sound));
            Assert.AreEqual(1, played.Count);
        }
        queue.Flush((_, sound) => played.Add(sound));
        Assert.AreEqual(2, played.Count);
        Assert.AreEqual(200f + CrashSpacingTicks - 1, played[1].Strength);
        for (var i = 0; i < CrashSpacingTicks * 4; i++) queue.Flush((_, sound) => played.Add(sound));
        Assert.AreEqual(2, played.Count);
    }

    [TestMethod]
    public void SustainedContactDoesNotRetriggerOrLeaveSoundsAfterSeparation()
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        var count = 0;
        for (var tick = 0; tick < 120; tick++)
        {
            for (var point = 0; point < 16; point++)
                queue.Enqueue(source, new(CrashSoundContact.Cars(0, point / 4, 1, point % 4), Vector3.Zero, 100 + point, 0));
            queue.Flush((_, _) => count++);
        }
        Assert.AreEqual(2, count);
        for (var i = 0; i < 120; i++) queue.Flush((_, _) => count++);
        Assert.AreEqual(2, count);

        queue.Enqueue(source, new(CrashSoundContact.Cars(0, 0, 1, 0), Vector3.Zero, 100, 0));
        queue.Flush((_, _) => count++);
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void DelayedContactDoesNotAccumulateDuplicateReports()
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        var first = new CrashSoundEvent(CrashSoundContact.Obstacle(0, 0, 10), Vector3.Zero, 100, 0);
        var second = new CrashSoundEvent(CrashSoundContact.Obstacle(0, 1, 10), Vector3.One, 100, -1);
        var played = new List<CrashSoundEvent>();
        queue.Enqueue(source, first);
        queue.Enqueue(source, second);
        queue.Flush((_, sound) => played.Add(sound));
        for (var i = 0; i < CrashSpacingTicks; i++)
        {
            queue.Enqueue(source, second);
            queue.Flush((_, sound) => played.Add(sound));
        }
        for (var i = 0; i < CrashSpacingTicks; i++) queue.Flush((_, sound) => played.Add(sound));
        Assert.AreEqual(2, played.Count);
        Assert.AreEqual(second, played[1]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RemovingACarOrStoppingTheStageDropsDelayedSounds(bool resetStage)
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        for (var point = 0; point < 2; point++)
            queue.Enqueue(source, new(CrashSoundContact.Cars(0, point, 1, 0), Vector3.Zero, 100, 0));
        var count = 0;
        queue.Flush((_, _) => count++);
        Assert.AreEqual(1, count);
        if (resetStage) queue.Clear();
        else queue.RemoveCar(1);
        for (var i = 0; i < CrashSpacingTicks * 2; i++)
            queue.Flush((_, _) => Assert.Fail("Removed cars and stopped stages must not play delayed crashes."));
    }

    [TestMethod]
    public void LandingWheelsPlayOncePerCarAndDoNotSuppressOtherCars()
    {
        var queue = new CrashSoundQueue<object>();
        var player = new object();
        var otherCar = new object();
        for (var wheel = 0; wheel < 4; wheel++)
        {
            queue.Enqueue(player, new(CrashSoundContact.Obstacle(0, wheel, 10), new(wheel, 0, 0), 100 + wheel, 1));
            queue.Enqueue(otherCar, new(CrashSoundContact.Obstacle(1, wheel, 10), Vector3.One, 100, 1));
        }
        var played = new List<(object Source, CrashSoundEvent Sound)>();

        queue.Flush((source, sound) => played.Add((source, sound)));

        Assert.AreEqual(2, played.Count);
        Assert.AreEqual(1, played.Count(entry => ReferenceEquals(entry.Source, player)));
        Assert.AreEqual(103f, played.Single(entry => ReferenceEquals(entry.Source, player)).Sound.Strength);
    }

    [TestMethod]
    public void StaggeredWheelLandingsShareCooldownWithoutBlockingWallCrashes()
    {
        var queue = new CrashSoundQueue<object>();
        var car = new object();
        var played = new List<CrashSoundEvent>();
        queue.Enqueue(car, new(CrashSoundContact.Obstacle(0, 0, -1), Vector3.Zero, 100, 1));
        queue.Flush((_, sound) => played.Add(sound));
        queue.Enqueue(car, new(CrashSoundContact.Obstacle(0, 2, 20), Vector3.One, 200, 1));
        queue.Enqueue(car, new(CrashSoundContact.Obstacle(0, 2, 21), Vector3.UnitX, 150, 0));
        queue.Flush((_, sound) => played.Add(sound));
        Assert.AreEqual(1, played.Count(sound => sound.Kind == 1));
        Assert.AreEqual(1, played.Count(sound => sound.Kind == 0));

        var cooldown = Math.Max(1, (int)(3 / Physics.PHYSICS_MULTIPLIER));
        for (var i = 0; i < cooldown; i++) queue.Flush((_, sound) => played.Add(sound));
        queue.Enqueue(car, new(CrashSoundContact.Obstacle(0, 3, -1), Vector3.Zero, 100, 1));
        queue.Flush((_, sound) => played.Add(sound));
        Assert.AreEqual(2, played.Count(sound => sound.Kind == 1));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RemovingOrResettingACarClearsItsLandingCooldown(bool resetStage)
    {
        var queue = new CrashSoundQueue<object>();
        var car = new object();
        var landing = new CrashSoundEvent(CrashSoundContact.Obstacle(0, 0, -1), Vector3.Zero, 100, 1);
        var count = 0;
        queue.Enqueue(car, landing);
        queue.Flush((_, _) => count++);
        if (resetStage) queue.Clear();
        else queue.RemoveCar(0);
        queue.Enqueue(car, landing);
        queue.Flush((_, _) => count++);
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void CarCrashTakesPriorityOverTireLandingAtTheSameContact()
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        var contact = CrashSoundContact.Cars(0, 0, 1, 0);
        queue.Enqueue(source, new(contact, Vector3.Zero, 500, 1));
        queue.Enqueue(source, new(contact, Vector3.Zero, 100, 0));
        queue.Enqueue(source, new(contact, Vector3.Zero, 600, 1));
        var played = new List<CrashSoundEvent>();

        queue.Flush((_, sound) => played.Add(sound));

        Assert.AreEqual(1, played.Count);
        Assert.AreEqual(0, played[0].Kind);
    }

    [TestMethod]
    public void ContactCooldownDoesNotBlockAnotherImpactByTheSameCar()
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        var impact = new CrashSoundEvent(CrashSoundContact.Cars(0, 0, 1, 0), Vector3.Zero, 100, 0);
        var count = 0;
        queue.Enqueue(source, impact);
        queue.Flush((_, _) => count++);
        queue.Enqueue(source, impact);
        queue.Enqueue(source, new(CrashSoundContact.Obstacle(1, 0, 7), Vector3.One, 100, 0));
        queue.Flush((_, _) => count++);
        Assert.AreEqual(1, count);
        for (var i = 1; i < CrashSpacingTicks; i++) queue.Flush((_, _) => count++);
        Assert.AreEqual(2, count);

        var cooldown = Math.Max(1, (int)(2 / Physics.PHYSICS_MULTIPLIER));
        for (var i = 0; i < cooldown; i++) queue.Flush((_, _) => count++);
        queue.Enqueue(source, impact);
        queue.Flush((_, _) => count++);
        for (var i = 0; i < CrashSpacingTicks; i++) queue.Flush((_, _) => count++);
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void RemovingACarOrStoppingTheStageDropsItsPendingSounds()
    {
        var queue = new CrashSoundQueue<object>();
        var source = new object();
        var impact = new CrashSoundEvent(CrashSoundContact.Cars(0, 0, 1, 0), Vector3.Zero, 100, 0);
        queue.Enqueue(source, impact);
        queue.RemoveCar(1);
        queue.Flush((_, _) => Assert.Fail("A removed car must not play queued sounds."));
        queue.Enqueue(source, impact);
        queue.Flush((_, _) => { });
        queue.Enqueue(source, impact);
        queue.Clear();
        queue.Flush((_, _) => Assert.Fail("Stopped stage must not play queued sounds."));
        queue.Enqueue(source, impact);
        var count = 0;
        queue.Flush((_, _) => count++);
        Assert.AreEqual(1, count);
    }
}
