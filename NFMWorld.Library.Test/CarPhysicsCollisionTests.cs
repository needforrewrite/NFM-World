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
}
