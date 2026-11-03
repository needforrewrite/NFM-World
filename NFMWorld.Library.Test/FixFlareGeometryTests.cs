using System.Numerics;
using NFMWorldLibrary.Rendering;

namespace NFMWorld.Library.Test;

[TestClass]
public class FixFlareGeometryTests
{
    [TestMethod]
    public void ElectricEnvelopeContainsCarAcrossSizesPosesAndAnimationFrames()
    {
        (Vector3 Min, Vector3 Max)[] cars =
        [
            (new(-110, -90, -260), new(110, 60, 260)),
            (new(-200, -350, -440), new(200, 80, 440)),
            (new(-400, -30, -100), new(300, 90, 200))
        ];
        Span<Vector2> octagon = stackalloc Vector2[8];
        foreach (var (min, max) in cars)
        foreach (var distance in new[] { 1000f, 2500f, 10000f })
        for (var yaw = 0; yaw < 360; yaw += 30)
        foreach (var pitch in new[] { -1.1f, 0f, 1.1f })
        foreach (var roll in new[] { 0f, 1.2f })
        {
            var worldView = Matrix4x4.CreateFromYawPitchRoll(yaw * MathF.PI / 180, pitch, roll)
                * Matrix4x4.CreateTranslation(90, -40, -distance);
            Assert.IsTrue(FixFlareGeometry.TryFit(min, max, worldView, 50, true, out var center, out var halfSize));
            foreach (var rotation in new[] { -22f, 0f, 22f })
            {
                FixFlareGeometry.WriteOctagon(halfSize, rotation * MathF.PI / 180, octagon);
                for (var i = 0; i < 8; i++) octagon[i] *= 1 + (i % 3) * 0.03f;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? min.X : max.X,
                        (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
                    var view = Vector3.Transform(corner, worldView);
                    var projected = new Vector2(view.X, view.Y) * (-center.Z / -view.Z) - new Vector2(center.X, center.Y);
                    Assert.IsTrue(Covered(octagon, projected));
                }
            }
        }
    }

    [TestMethod]
    public void EffectTracksModelCenterAndScalesWithTheWholeCar()
    {
        var worldView = Matrix4x4.CreateTranslation(30, -20, -2000);
        Assert.IsTrue(FixFlareGeometry.TryFit(new(-100, -300, -200), new(100, 100, 200), worldView,
            50, false, out var center, out var halfSize));
        Assert.AreEqual(new Vector3(30, -120, -2000), center);
        Assert.AreEqual(new Vector2(100, 200), halfSize);
        Assert.IsTrue(FixFlareGeometry.TryFit(new(-200, -600, -400), new(200, 200, 400), worldView,
            50, false, out _, out var larger));
        Assert.AreEqual(halfSize * 2, larger);
    }

    [TestMethod]
    public void PerspectiveFitIncludesNearerEndsOfLongCars()
    {
        Assert.IsTrue(FixFlareGeometry.TryFit(new(-100, -100, -400), new(100, 100, 400),
            Matrix4x4.CreateTranslation(0, 0, -1000), 50, true, out _, out var size));
        Assert.IsTrue(size.X > 160 && size.Y > 160);
    }

    [TestMethod]
    public void CarsBehindCameraAreSkippedAndNearClippingStaysFinite()
    {
        Assert.IsFalse(FixFlareGeometry.TryFit(-Vector3.One, Vector3.One, Matrix4x4.CreateTranslation(0, 0, 100),
            50, true, out _, out _));
        Assert.IsTrue(FixFlareGeometry.TryFit(new(-100), new(100), Matrix4x4.CreateTranslation(0, 0, -120),
            50, true, out var center, out var halfSize));
        Assert.IsTrue(float.IsFinite(center.X) && float.IsFinite(center.Y)
            && float.IsFinite(halfSize.X) && float.IsFinite(halfSize.Y));
    }

    private static bool Covered(ReadOnlySpan<Vector2> polygon, Vector2 point)
    {
        for (var i = 1; i + 1 < polygon.Length; i++)
        {
            var a = Cross(polygon[i] - polygon[0], point - polygon[0]);
            var b = Cross(polygon[i + 1] - polygon[i], point - polygon[i]);
            var c = Cross(polygon[0] - polygon[i + 1], point - polygon[i + 1]);
            if ((a >= -0.001f && b >= -0.001f && c >= -0.001f)
                || (a <= 0.001f && b <= 0.001f && c <= 0.001f)) return true;
        }
        return false;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
