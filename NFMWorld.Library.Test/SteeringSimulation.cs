using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

// Match NFMWorld.Library's global using aliases
using fix64 = FixedMathSharp.Fixed64;
using f64Vector3 = FixedMathSharp.Vector3d;

namespace NFMWorld.Library.Test;

/// <summary>
/// Minimal car for physics simulation — implements only what ContO and Mad.Drive actually read.
/// </summary>
class SimCar : IInGameCar
{
    // ITransform
    public f64Vector3 Position { get; set; }
    public FixedQuaternion Rotation { get; set; } = FixedQuaternion.Identity;
    public IReadOnlyList<ITransform> ChildTransforms => [];
    public ITransform? Parent => null;

    // ICar
    public Rad3d Rad => null!;
    public CarStats Stats => CarStats.Default;
    public int GroundAt { get; }
    public int MaxRadius => 60;
    public f64Euler WheelAngle { get; set; }
    public f64Euler TurningWheelAngle { get; set; }
    public IReadOnlyList<Rad3dWheelDef> Wheels { get; }

    // IInGameCar
    public Mad Mad { get; }
    public Control Control { get; } = new Control();
    public ushort currentCheckpoint { get; set; }
    public byte currentLap { get; set; }
    public int totalCheckpoint { get; set; }
    public int lastCheckpointNode { get; set; } = -1;
    public int placement { get; set; }
    public bool Wasted => false;
    public BaseAi? Bot { get; set; }

    public event DamageFunc? DamagedX;
    public event RoofDamageFunc? DamagedY;
    public event DamageFunc? DamagedZ;
    public event SparkFunc? Sparked;
    public event DustFunc? Dusted;

    public void AddDust(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround) { }
    public void Spark(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround) { }
    public void DamageX(CarStats stat, int wheelnum, fix64 amount) { }
    public void DamageY(CarStats stat, int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash) { }
    public void DamageZ(CarStats stat, int wheelnum, fix64 amount) { }
    public void Drive(IStage stage) { }
    public void Collide(IInGameCar otherCar) { }
    public void ResetPosition() { }

    public SimCar()
    {
        // Ground = (int)(Position.Y + 13 * Height / 10) = (int)(70 + 13) = 83
        const int groundAt = 83;
        GroundAt = groundAt;

        // Car starts at World.Ground - GroundAt = 250 - 83 = 167
        Position = new f64Vector3(fix64.Zero, (fix64)(World.Ground - groundAt), fix64.Zero);

        // 4 wheels in local space: front-left, front-right, rear-left, rear-right
        Wheels = [
            new Rad3dWheelDef(new f64Vector3((fix64)(-25), (fix64)70, (fix64)(-45)), 1, (fix64)10, (fix64)10, null),
            new Rad3dWheelDef(new f64Vector3((fix64)(+25), (fix64)70, (fix64)(-45)), 1, (fix64)10, (fix64)10, null),
            new Rad3dWheelDef(new f64Vector3((fix64)(-25), (fix64)70, (fix64)(+45)), 1, (fix64)10, (fix64)10, null),
            new Rad3dWheelDef(new f64Vector3((fix64)(+25), (fix64)70, (fix64)(+45)), 1, (fix64)10, (fix64)10, null),
        ];

        Mad = new Mad(CarStats.Default, 0, false);
    }
}

/// <summary>
/// Stage that returns no collidables — car rolls on the flat World.Ground plane.
/// </summary>
class EmptyStage : IStage
{
    public ReadOnlySpan<CollisionShapeRef> RetrievePointCollidables(fix64 x, fix64 z) =>
        ReadOnlySpan<CollisionShapeRef>.Empty;

    public IReadOnlyList<ITransform> pieces => [];
    public IReadOnlyList<IAiNode> nodes => [];
    public IReadOnlyList<IAiNode> checkpoints => [];
    public IReadOnlyList<IAiNode> fixHoops => [];
    public ushort nlaps => 1;
    public ITransform CreateObject(string objectName, int x, int y, int z, int xz) => null!;
}

[TestClass]
public class SteeringSimulation
{
    /// <summary>
    /// Drives straight for 60 ticks then steers left for 240 ticks.
    /// Prints per-tick state to diagnose whether the yaw rotation propagates into
    /// actual lateral position change.
    ///
    /// Expected outcome: after ~120 ticks of left-steering at speed, car.Position.X
    /// should deviate significantly from 0 (negative X = left turn in this coordinate system).
    /// </summary>
    [TestMethod]
    public void SimulateForwardThenSteerLeft()
    {
        World.IsHyperglidingEnabled = true;

        var stats = CarStats.Default; // Tornado Shark stats
        var car = new SimCar();
        var mad = new Mad(stats, 0, false);

        // Prevent NullReferenceException in SfxPlaySkid invocation inside Mad.Drive
        mad.SfxPlaySkid += (_, _) => { };

        var stage = new EmptyStage();

        Console.WriteLine("Tick | X       Z       Y    | Speed   Wxz   Yaw°  | Vx[0]   Vz[0]  | Wtouch Mtouch");
        Console.WriteLine(new string('-', 100));

        fix64 initialX = car.Position.X;

        for (int tick = 0; tick < 300; tick++)
        {
            bool steerLeft = tick >= 60; // straight for first 60, then steer
            var control = new Control { Up = true, Left = steerLeft };

            mad.Drive(control, new ContO(car), stage);

            if (tick < 10 || tick % 10 == 0 || tick == 59 || tick == 60 || tick == 61)
            {
                var pos = car.Position;

                // Compute heading from mad.CarRotation (the physics rotation)
                var localFwd = mad.CarRotation * new f64Vector3(fix64.Zero, fix64.Zero, fix64.One);
                var yawRad = fix64.Atan2(localFwd.X, localFwd.Z);
                float yawDeg = (float)(yawRad * (fix64)57.2957795f);

                // Print raw quaternion W to confirm if rotation is happening
                var q = mad.CarRotation;
                Console.WriteLine(
                    $"[{tick,3}] | " +
                    $"X={pos.X,7:F1} Z={pos.Z,7:F1} Y={pos.Y,6:F1} | " +
                    $"Spd={mad.Speed,6:F1} Wxz={car.TurningWheelAngle.Xz.Degrees,5:F1} Yaw={yawDeg,7:F2}° | " +
                    $"Wtouch={mad.Wtouch} Q=({(float)q.X:F4},{(float)q.Y:F4},{(float)q.Z:F4},{(float)q.W:F4})");
            }
        }

        var finalPos = car.Position;
        Console.WriteLine();
        Console.WriteLine($"Final position: X={finalPos.X:F3}, Z={finalPos.Z:F3}");
        Console.WriteLine($"Final Speed: {mad.Speed:F3}");
        Console.WriteLine($"Final Yaw from rotation: {(float)(fix64.Atan2((mad.CarRotation * new f64Vector3(fix64.Zero, fix64.Zero, fix64.One)).X, (mad.CarRotation * new f64Vector3(fix64.Zero, fix64.Zero, fix64.One)).Z) * (fix64)57.2957795f):F2}°");

        // If steering works, the car's X position should have changed significantly from 0
        // (negative X for left turn in X-right, Z-forward coordinate system)
        // This assertion is intentionally loose — any deviation > 5 units proves steering works.
        // If this fails with X ≈ 0, steering is broken in physics.
        Assert.AreNotEqual(initialX, finalPos.X, "Car X position should change when steering left");
    }
}
