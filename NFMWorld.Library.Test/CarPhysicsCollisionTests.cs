using FixedMathSharp;
using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

/// <summary>
/// Vertical collision impulses (the dominant car's revlift and the victim's lift) are applied
/// once per contacting wheel pair, like the original game, so an overlapping wheel takes the
/// kick more than once. The orientation tilt that uneven kick used to cause is prevented in the
/// plane fit, which only reads wheels that are actually on a surface.
/// </summary>
[TestClass]
public class CarPhysicsCollisionTests
{
    private static readonly Dictionary<string, Rad3d> Rads = [];

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
                {
                    options.Dsn = "https://public@localhost/1";
                    options.TracesSampleRate = 0;
                }
            );
        }
        catch (InvalidOperationException)
        {
            // Another test class got there first; Sentry is a process-wide singleton.
        }

        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
    }

    private static BackendCar CreateCar(string name, int im, bool isClientPlayer)
    {
        if (!Rads.TryGetValue(name, out var rad))
            Rads[name] = rad = RadParser.ParseRad(TheVFS.VFS.ReadAllText($"data/models/nfmm/cars/{name}.rad"), name);
        var car = new BackendCar(rad, im, 0, 0, isClientPlayer);
        car.CarPhysics.Stat = car.Stats = car.Stats with { OffRoadGrip = Fixed64.One };
        return car;
    }

    private static BackendStage CreateStage()
    {
        var stage = new BackendStage("collision-test", new StageLoader(), throwOnError: true);
        Assert.AreEqual(1f, stage.Physics.Gravity);
        Assert.AreEqual(0, stage.Pieces.Count);
        return stage;
    }

    /// <summary>
    /// Stock applies the dominant car's revlift once per contacting wheel pair. A wheel that
    /// overlaps several of the other car's wheels therefore takes the kick several times (the
    /// overlap the original game produces too). The old once-per-wheel gate capped this to a
    /// single kick, which the per-tick vertical traction erased before it lifted - "cars glued
    /// to the ground". The tilt that made the gate tempting is prevented in the plane fit, which
    /// no longer reads airborne kicked wheels as terrain slope.
    /// </summary>
    [TestMethod]
    public void Collide_AppliesRevliftOncePerContactingWheelPair()
    {
        var stage = CreateStage();
        var heavy = CreateCar("drmonster", 0, isClientPlayer: false);
        var light = CreateCar("2000tornados", 1, isClientPlayer: true);

        ContO heavyConto = heavy;
        ContO lightConto = light;
        // The first Drive is what populates the effective (stage-adjusted) stats that Collide reads.
        heavy.Drive(stage);
        light.Drive(stage);

        // Heavy rear wheels overlap the light car's front wheels; drmonster's wide rear wheels
        // meet several of the light car's wheels at once (the overlap the gate used to flatten).
        lightConto.Z = -90;
        heavy.CarPhysics.Speed = 300;
        for (var i = 0; i < 4; i++) heavy.CarPhysics.Scz[i] = 300;

        var before = new Fixed64[4];
        for (var i = 0; i < 4; i++) before[i] = heavy.CarPhysics.Scy[i];

        heavy.Collide(light, stage);

        var impulse = (Fixed64)heavy.Stats.Revlift * (Fixed64)stage.Physics.Recoil;
        Assert.IsTrue(impulse > Fixed64.Zero, "the car under test has no revlift to measure");

        // Every wheel that contacts takes at least one kick, and an overlapping wheel takes more
        // than one - the stock per-pair behaviour the per-wheel gate removed.
        var drops = Enumerable.Range(0, 4).Select(i => before[i] - heavy.CarPhysics.Scy[i]).ToArray();
        Assert.IsTrue(
            drops.Any(d => d >= impulse * 2),
            "overlapping wheels did not take multiple per-pair revlift kicks; "
            + $"drops={string.Join(',', drops.Select(d => $"{d:0.00}"))}"
        );
    }

    /// <summary>
    /// The dominant car's revlift kicks its contacting wheel 4x harder than the rest. Off a
    /// surface the plane fit used to snap the car to those kicked wheel heights, pitching it to
    /// ~80° (nose or tail straight up) instead of the original's slight turn.
    /// </summary>
    [TestMethod]
    public void Collide_RevliftKickDoesNotStandTheDominantCarOnEnd()
    {
        var stage = CreateStage();
        var a = CreateCar("drmonster", 0, false);
        var v = CreateCar("2000tornados", 1, true);
        for (var t = 0; t < 90; t++)
        {
            a.Drive(stage);
            v.Drive(stage);
        }

        ((ContO)v).Z = ((ContO)a).Z - 90;
        ((ContO)v).X = ((ContO)a).X;
        for (var i = 0; i < 4; i++)
        {
            a.CarPhysics.Scz[i] = 300;
            v.CarPhysics.Scz[i] = 0;
        }

        a.CarPhysics.Speed = 300;
        var before = Enumerable.Range(0, 4).Select(w => a.CarPhysics.Scy[w]).ToArray();
        a.Collide(v, stage);
        var kick = string.Join(',', Enumerable.Range(0, 4).Select(w => $"{a.CarPhysics.Scy[w] - before[w]:0}"));
        var maxP = Fixed64.Zero;
        var maxR = Fixed64.Zero;
        for (var t = 0; t < 90; t++)
        {
            a.Drive(stage);
            var p = a.CarPhysics.Pzy;
            while (p > 180) p -= 360;
            while (p < -180) p += 360;
            var r = a.CarPhysics.Pxy;
            while (r > 180) r -= 360;
            while (r < -180) r += 360;
            maxP = Fixed64.Max(maxP, Fixed64.Abs(p));
            maxR = Fixed64.Max(maxR, Fixed64.Abs(r));
        }

        Assert.IsTrue(maxP < 45, $"revlift kick=[{kick}] pitched the car to {maxP:0.0}° (roll {maxR:0.0}°)");
    }

    /// <summary>
    /// A dominant car with <c>Lift</c> must lift the victim even when only the Z momentum branch
    /// fires (the victim's x-momentum exceeds the dominant car's, so the X branch is skipped).
    /// The lift impulse is outside the branches, so it must land on every contacting wheel no
    /// matter which branch fired. This is the "lifts barely anyone, but pushes fine" symptom:
    /// the wheels that only contact along z lost their lift impulse entirely.
    /// </summary>
    [TestMethod]
    public void Collide_DominantCarLiftsVictimEvenWhenOnlyZBranchFires()
    {
        var stage = CreateStage();
        var heavy = CreateCar("radicalone", 0, isClientPlayer: false); // lift(30)
        var light = CreateCar("2000tornados", 1, isClientPlayer: true); // lift(0)

        // The first Drive populates the effective (stage-adjusted) stats that Collide reads.
        heavy.Drive(stage);
        light.Drive(stage);

        var lift = (Fixed64)heavy.Stats.Lift;
        Assert.IsTrue(lift > Fixed64.Zero, "the dominant car has no lift to measure");

        // Rear-end geometry matching the test above: the light car sits behind the heavy one
        // (Z = -90), so the heavy car's rear wheels overlap the light car's rear wheels - the
        // two victim wheels (2 and 3) that contact.
        ((ContO)light).Z = -90;

        // Set the wheel momenta so the X branch does NOT fire and the Z branch DOES on the
        // contacting wheels:
        //   Scx momentum: |heavy 250 * 1.5| = 375 is not > |light 300 * 1.3| = 390 -> X skipped.
        //   Scz momentum: |heavy 150 * 1.5| = 225 is > |light 100 * 1.3| = 130   -> Z fires.
        // In the old code the Z branch's Revlift-damage gate and the victim's own Lift gate
        // shared one per-wheel counter; the Z branch consumed it and the Lift impulse was
        // silently skipped for wheels that only contact along z.
        heavy.CarPhysics.Speed = 350;
        light.CarPhysics.Speed = 100;
        for (var i = 0; i < 4; i++)
        {
            heavy.CarPhysics.Scx[i] = 250;
            heavy.CarPhysics.Scz[i] = 150;
            light.CarPhysics.Scx[i] = 300;
            light.CarPhysics.Scz[i] = 100;
        }

        var before = new Fixed64[4];
        for (var i = 0; i < 4; i++) before[i] = light.CarPhysics.Scy[i];

        heavy.Collide(light, stage);

        // The Z branch fired on the contacting victim wheels: they were pushed in +z by the
        // dominant car's Push (100 + 150 * 2 * 1 = 400, the impulse itself at the 300 clamp).
        Assert.AreEqual(400, light.CarPhysics.Scz[2], "Z branch did not fire on the victim");
        // The X branch was skipped on the same wheels: their lateral velocity is untouched. This
        // is the regression's setup - with the shared counter, the Z branch's Regy gate would
        // have consumed the lift gate, so the victim would not be lifted here.
        Assert.AreEqual(300, light.CarPhysics.Scx[2], "X branch fired; this is not a Z-only collision");
        // The lift impulse is applied per contacting pair, so every contacting wheel (2 and 3)
        // must have taken its kick even though only the Z branch fired for them.
        var liftedWheels = 0;
        for (var i = 0; i < 4; i++)
        {
            var drop = before[i] - light.CarPhysics.Scy[i];
            if (drop >= lift - (Fixed64)0.001f)
            {
                liftedWheels++;
            }
        }

        Assert.AreEqual(
            2,
            liftedWheels,
            $"only {liftedWheels}/2 contacting victim wheels were lifted; "
            + $"drops={string.Join(',', Enumerable.Range(0, 4).Select(w => $"{(before[w] - light.CarPhysics.Scy[w]):0.00}"))}"
        );
    }
}
