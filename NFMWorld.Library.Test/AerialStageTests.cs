using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Rad;
using f64Euler = NFMWorldLibrary.FixedMath.f64Euler;
using f64Vector3 = FixedMathSharp.Vector3d;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

[TestClass]
public class AerialStageTests
{
    // BackendCar.Drive opens a Sentry transaction, and nothing else in this suite initializes it.
    [ClassInitialize]
    public static void InitSentry(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = "https://public@localhost/1";
                options.TracesSampleRate = 0;
            });
        }
        catch (InvalidOperationException)
        {
            // Another test class got there first; Sentry is a process-wide singleton.
        }
    }

    private static Rad3d CreateRad()
        => new([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "aerial-test");

    private static StageObject CreateCheckpoint(fix64 x, fix64 y, fix64 z, fix64 yaw = default)
    {
        var rotation = new f64Euler(f64AngleSingle.FromDegrees(yaw), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
        return StageObject.CreateDefaultObject(
            CreateRad(),
            new f64Vector3(x, y, z),
            rotation,
            PiecePlacementType.CheckPoint,
            AiNodeKind.CheckPoint);
    }

    private static (BackendStage Stage, BackendCar Car, StageObject LastCheckpoint) CreateSetup(
        bool aerial, fix64 lastCheckpointY, fix64 nextCheckpointY, int currentCheckpoint = 1) 
    {
        var stage = new BackendStage("aerial-test", new StageLoader { Aerial = aerial });
        var lastCheckpoint = CreateCheckpoint(100, lastCheckpointY, 200, 90);
        stage.Checkpoints.Add(lastCheckpoint);
        stage.Checkpoints.Add(CreateCheckpoint(300, nextCheckpointY, 400));

        var car = new BackendCar(CreateRad(), 0, 0, 0, isClientPlayer: false)
        {
            CurrentCheckpoint = (ushort)currentCheckpoint,
            TotalCheckpoint = currentCheckpoint
        };
        car.CarPhysics.OnGroundPlane = true;

        return (stage, car, lastCheckpoint);
    }

    [TestMethod]
    public void HandleAerialFall_LastCheckpointInAir_TeleportsToIt()
    {
        var (stage, car, lastCheckpoint) = CreateSetup(aerial: true, lastCheckpointY: -1000, nextCheckpointY: -2000);

        var teleported = CheckPointHelper.HandleAerialFall(stage, car);

        Assert.IsTrue(teleported);
        Assert.AreEqual((double)lastCheckpoint.Position.X, (double)car.Position.X, 1e-6);
        Assert.AreEqual((double)lastCheckpoint.Position.Z, (double)car.Position.Z, 1e-6);
        Assert.AreEqual((double)(lastCheckpoint.Position.Y - car.GroundAt), (double)car.Position.Y, 1e-6);
        Assert.AreEqual(90.0, (double)car.Rotation.Xz.Degrees, 0.001);
        Assert.IsFalse(car.CarPhysics.OnGroundPlane);
    }

    [TestMethod]
    public void HandleAerialFall_NextCheckpointOnGround_DoesNotTeleport()
    {
        var (stage, car, _) = CreateSetup(aerial: true, lastCheckpointY: -1000, nextCheckpointY: World.Ground);

        var teleported = CheckPointHelper.HandleAerialFall(stage, car);

        Assert.IsFalse(teleported);
        Assert.AreEqual(0.0, (double)car.Position.X, 1e-6);
    }

    [TestMethod]
    public void HandleAerialFall_LastCheckpointOnGround_DoesNotTeleport()
    {
        var (stage, car, _) = CreateSetup(aerial: true, lastCheckpointY: World.Ground, nextCheckpointY: -2000);

        var teleported = CheckPointHelper.HandleAerialFall(stage, car);

        Assert.IsFalse(teleported);
        Assert.AreEqual(0.0, (double)car.Position.X, 1e-6);
    }

    [TestMethod]
    public void HandleAerialFall_NoCheckpointTaken_DoesNotTeleport()
    {
        var (stage, car, _) = CreateSetup(
            aerial: true, lastCheckpointY: -1000, nextCheckpointY: -2000, currentCheckpoint: 0);

        var teleported = CheckPointHelper.HandleAerialFall(stage, car);

        Assert.IsFalse(teleported);
        Assert.AreEqual(0.0, (double)car.Position.X, 1e-6);
    }

    [TestMethod]
    public void HandleAerialFall_StageIsNotAerial_DoesNotTeleport()
    {
        var (stage, car, _) = CreateSetup(aerial: false, lastCheckpointY: -1000, nextCheckpointY: -2000);

        var teleported = CheckPointHelper.HandleAerialFall(stage, car);

        Assert.IsFalse(teleported);
        Assert.AreEqual(0.0, (double)car.Position.X, 1e-6);
    }

    [TestMethod]
    public void Drive_OnAerialStage_TeleportsFallenCarToLastCheckpoint()
    {
        var (stage, car, lastCheckpoint) = CreateSetup(aerial: true, lastCheckpointY: -1000, nextCheckpointY: -2000);

        car.Drive(stage);

        Assert.AreEqual((double)lastCheckpoint.Position.X, (double)car.Position.X, 1e-6);
        Assert.AreEqual((double)(lastCheckpoint.Position.Y - car.GroundAt), (double)car.Position.Y, 1e-6);
    }

    [TestMethod]
    public void StageLoader_ParsesAerialTag()
    {
        var root = Path.Combine(Path.GetTempPath(), "NFMWorld-aerial-tests", Guid.NewGuid().ToString("N"));
        var stages = Path.Combine(root, "data", "stages");
        Directory.CreateDirectory(stages);
        File.WriteAllText(Path.Combine(stages, "aerial_tag_test.txt"), "aerial()");
        File.WriteAllText(Path.Combine(stages, "aerial_tag_test_off.txt"), "snap(0,0,0)");

        try
        {
            TheVFS.VFS.MountDirectory(root);

            Assert.IsTrue(new StageLoader("aerial_tag_test").Aerial);
            Assert.IsFalse(new StageLoader("aerial_tag_test_off").Aerial);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
