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
        var before = physics.ForwardSpeed;

        physics.AddForwardThrust(12);
        car.Drive(stage);

        Assert.IsTrue(physics.ForwardSpeed > before, $"thrust must add forward speed: {before} -> {physics.ForwardSpeed}");
    }

    [TestMethod]
    public void AddForwardThrust_PitchedUp_HasAnUpwardComponent()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
        car.CarPhysics.Pzy = 30; // nose pitched up (Y is down, so positive Pzy is nose-up)
        var beforeY = car.CarPhysics.SummedWheelVelocity.Y;

        car.CarPhysics.AddForwardThrust(100);
        var afterY = car.CarPhysics.SummedWheelVelocity.Y;

        Assert.IsTrue(afterY < beforeY, "a nose-up thrust must lift (Y decreases)");
    }

    [TestMethod]
    public void AddLocalImpulse_AppliesTowardsTheCarsRight()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
        // Facing +Z (yaw 0): the follow camera looks +Z from behind, so screen-right is +X.
        var beforeX = car.CarPhysics.SummedWheelVelocity.X;

        car.CarPhysics.AddLocalImpulse(50, 0);

        Assert.IsTrue(car.CarPhysics.SummedWheelVelocity.X > beforeX, "right kick must push towards +X at yaw 0");
    }

    [TestMethod]
    public void AddLocalImpulse_UpPushesAgainstGravity()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
        var beforeY = car.CarPhysics.SummedWheelVelocity.Y;

        car.CarPhysics.AddLocalImpulse(0, 50);

        // Y is positive downward, so "up" must decrease Y.
        Assert.IsTrue(car.CarPhysics.SummedWheelVelocity.Y < beforeY, "an up kick must push towards -Y");
    }

    /// <summary>
    /// Air heading must turn the way the player reads it. The follow camera sits behind the car
    /// looking +Z, so screen-right is world +X; a positive turn must swing the nose towards +X.
    /// Yaw increases the other way, which is what made this read inverted.
    /// </summary>
    [TestMethod]
    public void TurnYaw_Positive_TurnsTowardsScreenRight()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
        var noseBefore = car.CarPhysics.NoseDirection;

        car.CarPhysics.TurnYaw(30);
        var noseAfter = car.CarPhysics.NoseDirection;

        // At yaw 0 the nose is +Z; turning right swings it towards +X.
        Assert.IsTrue(noseAfter.X > noseBefore.X, $"a right turn must swing the nose towards +X: {noseBefore} -> {noseAfter}");
    }

    /// <summary>
    /// The two kick axes must not be swapped or mirrored. Before this was pinned, the x axis
    /// was inverted (left+jump sent the car right).
    /// </summary>
    [TestMethod]
    public void AddLocalImpulse_KickAxesAreIndependent()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());

        static BackendCar Airborne()
        {
            var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
            var car = new BackendCar(rad, 0, 0, 0, isClientPlayer: true);
            car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
            return car;
        }

        var up = Airborne();
        var right = Airborne();
        up.CarPhysics.AddLocalImpulse(0, 200);
        right.CarPhysics.AddLocalImpulse(200, 0);
        up.Drive(stage);
        right.Drive(stage);

        Assert.IsTrue(right.Position.X > 0, "a right kick at yaw 0 must move towards +X");
        Assert.AreEqual(0, up.Position.X.rawValue, "an up kick must not move the car sideways");
        Assert.IsTrue(up.Position.Y < World.Ground - up.GroundAt - 19_000, "an up kick must lift the car");
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
