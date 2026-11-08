using System.Numerics;
using NFMWorldLibrary.Rendering;

namespace NFMWorld.Library.Test;

[TestClass]
public class FixFlareGeometryTests
{
    [TestMethod]
    public void ShellPointsLieOnTheInflatedEllipsoid()
    {
        (Vector3 Min, Vector3 Max)[] cars =
        [
            (new(-110, -90, -260), new(110, 60, 260)),
            (new(-200, -350, -440), new(200, 80, 440)),
            (new(-400, -30, -100), new(300, 90, 200))
        ];
        Span<Vector3> shell = stackalloc Vector3[FixFlareGeometry.ShellVertexCount];
        foreach (var (min, max) in cars)
        foreach (var inflation in new[] { 1.3f, 1.67f })
        {
            var center = (min + max) * 0.5f;
            var halfSize = (max - min) * 0.5f;
            FixFlareGeometry.WriteShell(center, halfSize, inflation, shell);

            for (var i = 0; i < shell.Length; i++)
            {
                var normalized = (shell[i] - center) / (halfSize * inflation);
                Assert.AreEqual(
                    1f,
                    normalized.Length(),
                    0.0001f,
                    $"Shell point {i} is not on the inflated ellipsoid."
                );
            }
        }
    }

    [TestMethod]
    public void ShellReachesBeyondTheCarOnEveryAxis()
    {
        Span<Vector3> shell = stackalloc Vector3[FixFlareGeometry.ShellVertexCount];
        var center = new Vector3(10, -20, 30);
        var halfSize = new Vector3(100, 50, 200);
        const float inflation = 1.3f;
        FixFlareGeometry.WriteShell(center, halfSize, inflation, shell);

        var extent = Vector3.Zero;
        for (var i = 0; i < shell.Length; i++)
            extent = Vector3.Max(extent, Vector3.Abs(shell[i] - center));

        Assert.AreEqual(halfSize.X * inflation, extent.X, 0.0001f);
        Assert.AreEqual(halfSize.Y * inflation, extent.Y, 0.0001f);
        Assert.AreEqual(halfSize.Z * inflation, extent.Z, 0.0001f);
        Assert.AreEqual(0f, Vector3.Distance(center + new Vector3(0, 65, 0), shell[0]), 0.0001f);
        Assert.AreEqual(0f, Vector3.Distance(center - new Vector3(0, 65, 0), shell[^1]), 0.0001f);
    }

    [TestMethod]
    public void ShellIndicesStayInsideTheirOwnVertexRange()
    {
        var perShell = FixFlareGeometry.ShellTriangleCount * 3;
        var indices = new ushort[perShell * 2];
        FixFlareGeometry.WriteShellIndices(indices, 0);
        FixFlareGeometry.WriteShellIndices(indices.AsSpan(perShell), FixFlareGeometry.ShellVertexCount);

        for (var shell = 0; shell < 2; shell++)
        {
            var offset = shell * FixFlareGeometry.ShellVertexCount;
            for (var i = shell * perShell; i < (shell + 1) * perShell; i += 3)
            {
                var a = indices[i];
                var b = indices[i + 1];
                var c = indices[i + 2];
                Assert.IsTrue(a >= offset && a < offset + FixFlareGeometry.ShellVertexCount);
                Assert.IsTrue(b >= offset && b < offset + FixFlareGeometry.ShellVertexCount);
                Assert.IsTrue(c >= offset && c < offset + FixFlareGeometry.ShellVertexCount);
                Assert.IsTrue(a != b && b != c && a != c, $"Degenerate triangle at {i}.");
            }
        }
    }

    [TestMethod]
    public void BallHalfSizeKeepsTheFlareAtLeastAsTallAsWide()
    {
        (Vector3 HalfSize, float ExpectedHeight)[] cars =
        [
            (new(110, 75, 260), 110),
            (new(200, 80, 440), 200),
            (new(60, 100, 90), 100)
        ];
        foreach (var (halfSize, expectedHeight) in cars)
        {
            var ball = FixFlareGeometry.BallHalfSize(halfSize);
            Assert.AreEqual(halfSize.X, ball.X, 0.0001f);
            Assert.AreEqual(halfSize.Z, ball.Z, 0.0001f);
            Assert.AreEqual(expectedHeight, ball.Y, 0.0001f);
            Assert.IsTrue(ball.Y >= ball.X, "The flare must not be squashed below its width.");
        }
    }

    [TestMethod]
    public void ShellRejectsUndersizedBuffers()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            FixFlareGeometry.WriteShell(Vector3.Zero, Vector3.One, 1, new Vector3[FixFlareGeometry.ShellVertexCount - 1])
        );
        Assert.ThrowsExactly<ArgumentException>(() =>
            FixFlareGeometry.WriteShellIndices(new ushort[FixFlareGeometry.ShellTriangleCount * 3 - 1], 0)
        );
    }
}
