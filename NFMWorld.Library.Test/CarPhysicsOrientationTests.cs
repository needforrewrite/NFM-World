using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class CarPhysicsOrientationTests
{
    private static readonly (int Roll, int Pitch)[] Cases =
    [
        (0, 0), (25, 40), (70, 115), (120, 30), (160, 150), (200, 230), (270, 75),
        (89, 40), (91, 40), (269, 40), (271, 40)
    ];

    [TestMethod]
    [DataRow(0)]
    [DataRow(45)]
    [DataRow(90)]
    [DataRow(180)]
    [DataRow(270)]
    public void FitTerrainAngles_ReconstructsRegularAndInvertedOrientations(int yawDegrees)
    {
        foreach (var (roll, pitch) in Cases)
        {
            AssertPose(yawDegrees, roll, pitch);
            AssertPose(yawDegrees, roll + 180, pitch + 180);
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(45)]
    [DataRow(180)]
    [DataRow(270)]
    public void FitTerrainAngles_StaysContinuousAcrossRegularAndInvertedRoll(int yawDegrees)
    {
        foreach (var pitchDegrees in new[] { 40, 115, 230 })
        foreach (var startRoll in new[] { 60, 240 })
        {
            var pitch = (Fixed64)pitchDegrees;
            var fittedRoll = (Fixed64)startRoll;
            var fittedPitch = pitch;
            var endRoll = startRoll + 60;

            for (var rollDegrees = startRoll; rollDegrees <= endRoll; rollDegrees += 2)
            {
                var normal = Up((Fixed64)rollDegrees, pitch, (Fixed64)yawDegrees);
                var result = CarPhysics.FitTerrainAngles(normal, (Fixed64)yawDegrees, fittedRoll, fittedPitch);
                Assert.IsTrue(Fixed64.Abs(result.Pxy - rollDegrees) < (Fixed64)0.1f,
                    $"roll discontinuity for yaw={yawDegrees}, pitch={pitchDegrees}, roll={rollDegrees}: {result.Pxy}");
                Assert.IsTrue(Fixed64.Abs(result.Pzy - pitch) < (Fixed64)0.1f,
                    $"pitch discontinuity for yaw={yawDegrees}, pitch={pitchDegrees}, roll={rollDegrees}: {result.Pzy}");
                fittedRoll = result.Pxy;
                fittedPitch = result.Pzy;
            }
        }
    }

    [TestMethod]
    public void FitTerrainAngles_PreservesLocalYawAtGimbalLock()
    {
        foreach (var yawDegrees in new[] { 0, 45, 180, 270 })
        foreach (var rollDegrees in new[] { 82, 90, 98, 262, 270, 278 })
        {
            const int pitchDegrees = 40;
            const int localYawDegrees = 57;
            var result = CarPhysics.FitTerrainAngles(
                Up((Fixed64)rollDegrees, (Fixed64)pitchDegrees, (Fixed64)yawDegrees),
                (Fixed64)yawDegrees,
                (Fixed64)rollDegrees,
                (Fixed64)localYawDegrees);

            Assert.IsTrue(result.GimbalLocked,
                $"expected gimbal lock for yaw={yawDegrees}, roll={rollDegrees}");
            Assert.AreEqual((Fixed64)localYawDegrees, result.Pzy,
                $"local yaw changed for yaw={yawDegrees}, roll={rollDegrees}");
        }
    }

    private static void AssertPose(int yawDegrees, int rollDegrees, int pitchDegrees)
    {
        var yaw = (Fixed64)yawDegrees;
        var roll = (Fixed64)rollDegrees;
        var pitch = (Fixed64)pitchDegrees;
        var normal = Up(roll, pitch, yaw);
        var (fitRoll, fitPitch, _) = CarPhysics.FitTerrainAngles(normal, yaw, roll, pitch);
        var fittedNormal = Up(fitRoll, fitPitch, yaw);

        Assert.IsTrue(Fixed64.Abs(fitRoll - roll) < (Fixed64)0.1f,
            $"roll mismatch for yaw={yawDegrees}, roll={rollDegrees}, pitch={pitchDegrees}: {fitRoll}");
        Assert.IsTrue(Fixed64.Abs(fitPitch - pitch) < (Fixed64)0.1f,
            $"pitch mismatch for yaw={yawDegrees}, roll={rollDegrees}, pitch={pitchDegrees}: {fitPitch}");
        Assert.IsTrue((fittedNormal - normal).Magnitude < (Fixed64)0.01f,
            $"normal mismatch for yaw={yawDegrees}, roll={rollDegrees}, pitch={pitchDegrees}");
    }

    private static Vector3d Up(Fixed64 roll, Fixed64 pitch, Fixed64 yaw)
    {
        var sinRoll = UMath.Sin(roll);
        var cosRoll = UMath.Cos(roll);
        var sinPitch = UMath.Sin(pitch);
        var cosPitch = UMath.Cos(pitch);
        var sinYaw = UMath.Sin(yaw);
        var cosYaw = UMath.Cos(yaw);
        return new Vector3d(
            sinRoll * cosYaw + cosRoll * sinPitch * sinYaw,
            -cosRoll * cosPitch,
            sinRoll * sinYaw - cosRoll * sinPitch * cosYaw);
    }
}
