using FixedMathSharp;
using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

/// <summary>
/// A dominant car hitting a light one must not take its vertical collision impulse more than
/// once per wheel per collision. The impulse is subtracted from <see cref="CarPhysics.Scy"/> for
/// every contacting wheel pair, so a wheel that meets several of the other car's wheels used to
/// end up far lower than its neighbours; the plane-fit then read that as a tilted surface and
/// pitched the car onto its nose or tail.
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

    [TestMethod]
    public void Collide_DominantCarTakesOneVerticalImpulsePerWheel()
    {
        var stage = CreateStage();
        var heavy = CreateCar("drmonster", 0, isClientPlayer: false);
        var light = CreateCar("2000tornados", 1, isClientPlayer: true);

        ContO heavyConto = heavy;
        ContO lightConto = light;
        // The first Drive is what populates the effective (stage-adjusted) stats that Collide reads.
        heavy.Drive(stage);
        light.Drive(stage);

        // Heavy rear wheels (Z=-110) overlap the light car's front wheels (Z=+11), so several
        // wheel pairs are in contact at once; the heavy car is moving, the light one is still.
        lightConto.Z = -90;
        heavy.CarPhysics.Speed = 300;
        for (var i = 0; i < 4; i++) heavy.CarPhysics.Scz[i] = 300;

        var before = new Fixed64[4];
        for (var i = 0; i < 4; i++) before[i] = heavy.CarPhysics.Scy[i];

        heavy.Collide(light, stage);

        // One impulse is Revlift * recoil; the stock stage has recoil 1.
        var impulse = (Fixed64)heavy.Stats.Revlift * (Fixed64)stage.Physics.Recoil;
        Assert.IsTrue(impulse > Fixed64.Zero, "the car under test has no revlift to measure");

        for (var i = 0; i < 4; i++)
        {
            var drop = before[i] - heavy.CarPhysics.Scy[i];
            Assert.IsTrue(
                drop <= impulse + (Fixed64)0.001f,
                $"wheel {i} took {drop:0.00} of vertical impulse, more than one {impulse:0.00} kick: "
                + $"Scy={string.Join(',', Enumerable.Range(0, 4).Select(w => $"{heavy.CarPhysics.Scy[w]:0.00}"))}"
            );
        }
    }

    /// <summary>
    /// A dominant car with <c>Lift</c> must lift the victim even when only the Z momentum branch
    /// fires (the victim's x-momentum exceeds the dominant car's, so the X branch is skipped).
    /// The revlift damage gate and the victim's own <c>Lift</c> gate used to share one per-wheel
    /// counter; a Z-branch fire consumed it and the <c>Lift</c> impulse was silently skipped.
    /// This is exactly the "lifts barely anyone, but pushes fine" symptom: the victim's wheels
    /// that only contact along z (the typical rear- or side-hit case) lost their lift impulse
    /// because the branch that fired for the shove had already spent the gate.
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
        Assert.AreEqual((Fixed64)400, light.CarPhysics.Scz[2], "Z branch did not fire on the victim");
        // The X branch was skipped on the same wheels: their lateral velocity is untouched. This
        // is the regression's setup - with the shared counter, the Z branch's Regy gate would
        // have consumed the lift gate, so the victim would not be lifted here.
        Assert.AreEqual((Fixed64)300, light.CarPhysics.Scx[2], "X branch fired; this is not a Z-only collision");
        var liftedWheels = 0;
        for (var i = 0; i < 4; i++)
        {
            var drop = before[i] - light.CarPhysics.Scy[i];
            if (drop >= lift - (Fixed64)0.001f)
            {
                liftedWheels++;
            }

            // No wheel may take more than one lift impulse.
            Assert.IsTrue(
                drop <= lift + (Fixed64)0.001f,
                $"wheel {i} took {drop:0.00} of lift, more than one {lift:0.00} kick: "
                + $"Scy={string.Join(',', Enumerable.Range(0, 4).Select(w => $"{light.CarPhysics.Scy[w]:0.00}"))}"
            );
        }

        Assert.AreEqual(
            2,
            liftedWheels,
            $"only {liftedWheels}/2 contacting victim wheels were lifted; the Z branch's "
            + "Revlift-damage gate consumed the shared lift counter. "
            + $"drops={string.Join(',', Enumerable.Range(0, 4).Select(w => $"{(before[w] - light.CarPhysics.Scy[w]):0.00}"))}"
        );
    }
}
