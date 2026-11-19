using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using FixedMathSharp;

namespace NFMWorld.Library.Test;

/// <summary>
/// The generic physics facilities a gamemode uses for arcade behaviour: a world impulse, a
/// forward thrust, the per-tick step events, and the resolved ground-contact flag. None of them
/// knows what a "jump" or a "boost" is; that stays in the gamemode.
/// </summary>
[TestClass]
public class CarPhysicsArcadeHookTests
{
    private static readonly string CarName = $"arcade-hook/{Guid.NewGuid():N}";

    [ClassInitialize]
    public static void InitSentry(TestContext _)
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
        }
    }

    private static BackendCar CreateCar()
    {
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        return new BackendCar(rad, 0, 0, 0, isClientPlayer: true);
    }

    /// <summary>A car resting on the world ground plane: one step settles contact.</summary>
    private static BackendCar GroundedCar(BackendStage stage)
    {
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt, 0);
        for (var i = 0; i < 8; i++)
            car.Drive(stage);
        Assert.IsTrue(car.CarPhysics.Grounded, "the fixture car must be resting on the ground");
        return car;
    }

    [TestMethod]
    public void AddImpulse_WhileGrounded_SendsTheCarUpward()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = GroundedCar(stage);
        var restingY = car.Position.Y;

        // Y is positive downward, so a negative impulse is upward.
        car.CarPhysics.AddImpulse(0, -160, 0);
        car.Drive(stage);

        Assert.IsTrue(car.Position.Y < restingY, $"expected to rise: {restingY} -> {car.Position.Y}");
    }

    [TestMethod]
    public void AddForwardThrust_InTheAir_AcceleratesAlongTheHeading()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
        var physics = car.CarPhysics;
        var before = physics.SummedWheelVelocity;

        physics.AddForwardThrust(12);
        car.Drive(stage);
        var after = physics.SummedWheelVelocity;

        var planarBefore = Fixed64.Sqrt(before.X * before.X + before.Z * before.Z);
        var planarAfter = Fixed64.Sqrt(after.X * after.X + after.Z * after.Z);
        Assert.IsTrue(planarAfter > planarBefore, $"thrust must add planar speed: {planarBefore} -> {planarAfter}");
    }

    [TestMethod]
    public void Grounded_IsFalseInTheAirAndTrueAfterLanding()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);

        car.Drive(stage);
        Assert.IsFalse(car.CarPhysics.Grounded, "a car in the air is not grounded");

        var grounded = GroundedCar(stage);
        Assert.IsTrue(grounded.CarPhysics.Grounded);
    }

    [TestMethod]
    public void PreStepAndPostStep_FireOnceEachPerDrive()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = GroundedCar(stage);
        var pre = 0;
        var post = 0;
        var impulsesAtPre = 0;
        car.CarPhysics.PreStep += physics =>
        {
            pre++;
            // The event runs before this step's physics, so an impulse armed here applies now.
            physics.AddImpulse(0, -160, 0);
            impulsesAtPre++;
        };
        car.CarPhysics.PostStep += _ => post++;

        var restingY = car.Position.Y;
        car.Drive(stage);

        Assert.AreEqual(1, pre);
        Assert.AreEqual(1, post);
        Assert.AreEqual(1, impulsesAtPre);
        Assert.IsTrue(car.Position.Y < restingY, "an impulse armed in PreStep must apply to that same step");
    }
}
