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
/// The arcade hooks a gamemode arms before the physics step: a discrete upward jump and a held
/// horizontal boost. Both live in <see cref="CarPhysics.Drive"/> and are consumed there.
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
        Assert.IsTrue(car.CarPhysics.WasGrounded, "the fixture car must be resting on the ground");
        return car;
    }

    [TestMethod]
    public void JumpImpulse_WhileGrounded_SendsTheCarUpward()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = GroundedCar(stage);
        var physics = car.CarPhysics;
        var restingY = car.Position.Y;

        physics.JumpImpulse = 160;
        car.Drive(stage);

        // Y is positive downward, so a jump lowers the position value.
        Assert.IsTrue(car.Position.Y < restingY, $"expected to rise: {restingY} -> {car.Position.Y}");
        Assert.AreEqual(0L, physics.JumpImpulse.rawValue, "the impulse must be consumed by one step");
    }

    [TestMethod]
    public void JumpImpulse_WhileAirborne_DoesNothing()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        // Well above the ground plane and never stepped, so WasGrounded stays false.
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
        var physics = car.CarPhysics;
        Assert.IsFalse(physics.WasGrounded);

        physics.JumpImpulse = 160;
        var before = physics.SummedWheelVelocity.Y;
        car.Drive(stage);

        Assert.IsTrue(physics.SummedWheelVelocity.Y > before, "gravity only: the wheels keep falling");
    }

    [TestMethod]
    public void BoostThrust_WhileAirborne_AcceleratesAlongTheHeading()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = CreateCar();
        car.Position = new Vector3d(0, World.Ground - car.GroundAt - 20_000, 0);
        var physics = car.CarPhysics;
        var before = physics.SummedWheelVelocity;

        physics.BoostThrust = 12;
        car.Drive(stage);
        var after = physics.SummedWheelVelocity;

        var planarBefore = Fixed64.Sqrt(before.X * before.X + before.Z * before.Z);
        var planarAfter = Fixed64.Sqrt(after.X * after.X + after.Z * after.Z);
        Assert.IsTrue(planarAfter > planarBefore, $"boost must add planar speed: {planarBefore} -> {planarAfter}");
        Assert.AreEqual(0L, physics.BoostThrust.rawValue, "the thrust must be consumed by one step");
    }

    [TestMethod]
    public void BoostThrust_WhileGrounded_IsIgnored()
    {
        var stage = new BackendStage("arcade-hook", new StageLoader());
        var car = GroundedCar(stage);
        var physics = car.CarPhysics;

        physics.BoostThrust = 12;
        car.Drive(stage);

        // The hook must not fire on the ground, or traction would be fighting it every tick.
        Assert.AreEqual(0L, physics.BoostThrust.rawValue);
    }
}
