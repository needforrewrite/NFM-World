using System.Numerics;
using NFMWorldLibrary.Rendering;

namespace NFMWorld.Library.Test;

[TestClass]
public class CoplanarSurfaceResolverTests
{
    private static readonly uint[] Quad = [0, 1, 2, 0, 2, 3];
    private static Vector3[] Rectangle(float x, float y, float width, float height, float z = 0)
        => [new(x, y, z), new(x + width, y, z), new(x + width, y + height, z), new(x, y + height, z)];

    private static double Area(CoplanarSurfaceResolver.Surface surface)
    {
        Assert.AreEqual(0, surface.Triangles.Length % 3);
        double area = 0;
        for (var i = 0; i < surface.Triangles.Length; i += 3)
        {
            var a = surface.Points[surface.Triangles[i]];
            var b = surface.Points[surface.Triangles[i + 1]];
            var c = surface.Points[surface.Triangles[i + 2]];
            area += Vector3.Cross(b - a, c - a).Length() * 0.5;
        }
        return area;
    }

    [TestMethod]
    public void DisjointAndEdgeTouchingSurfacesRemainUnmodified()
    {
        var resolver = new CoplanarSurfaceResolver();
        Assert.IsNull(resolver.Resolve(0, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ));
        Assert.IsNull(resolver.Resolve(1, Rectangle(20, 0, 10, 10), Quad, Vector3.UnitZ));
        Assert.IsNull(resolver.Resolve(2, Rectangle(10, 0, 10, 10), Quad, Vector3.UnitZ));
        Assert.IsNull(resolver.Resolve(3, Rectangle(100000, 0, 10, 10), Quad, Vector3.UnitZ));
    }

    [TestMethod]
    public void ParallelSeparatedAndIntersectingPlanesAreNotClipped()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(0, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ);
        Assert.IsNull(resolver.Resolve(1, Rectangle(0, 0, 10, 10, 0.05f), Quad, Vector3.UnitZ));
        Vector3[] vertical = [new(0, 5, -5), new(10, 5, -5), new(10, 5, 5), new(0, 5, 5)];
        Assert.IsNull(resolver.Resolve(2, vertical, Quad, Vector3.UnitY));
    }

    [TestMethod]
    public void IdenticalSurfacesHaveOnlyOneVisibleOwnerEvenWithReversedNormals()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(0, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ);
        var result = resolver.Resolve(1, Rectangle(0, 0, 10, 10), Quad, -Vector3.UnitZ * 0.01f)!;
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Triangles.Length);
        Assert.AreEqual(0, result.Edges.Length);
    }

    [TestMethod]
    public void PartialOverlapTrimsOnlyCoveredAreaAndOriginalOutlines()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(0, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ);
        var result = resolver.Resolve(1, Rectangle(5, 0, 10, 10), Quad, Vector3.UnitZ)!;
        Assert.IsNotNull(result);
        Assert.AreEqual(50d, Area(result), 0.001);
        Assert.AreEqual(20d, result.Edges.Sum(edge => (double)Vector3.Distance(edge.Start, edge.End)), 0.001);
        Assert.IsTrue(result.Points.All(point => point.X >= 10 - 0.001f && point.Z == 0));
    }

    [TestMethod]
    public void OverlappingBoundsDoNotClipNonOverlappingTriangles()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(0, [new(0, 0, 0), new(10, 0, 0), new(0, 10, 0)], [0, 1, 2], Vector3.UnitZ);
        Assert.IsNull(resolver.Resolve(1, [new(9, 9, 0), new(9, 2, 0), new(2, 9, 0)], [0, 1, 2], Vector3.UnitZ));
    }

    [TestMethod]
    public void FacesOfTheSamePieceDoNotCutEachOther()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(7, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ);
        Assert.IsNull(resolver.Resolve(7, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ));
    }

    [TestMethod]
    public void MultipleOccludersAndInteriorOverlapsPreserveVisibleArea()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(0, Rectangle(2, 2, 2, 2), Quad, Vector3.UnitZ);
        resolver.Resolve(1, Rectangle(6, 6, 2, 2), Quad, Vector3.UnitZ);
        var result = resolver.Resolve(2, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ)!;
        Assert.IsNotNull(result);
        Assert.AreEqual(92d, Area(result), 0.001);
        Assert.AreEqual(40d, result.Edges.Sum(edge => (double)Vector3.Distance(edge.Start, edge.End)), 0.001);
    }

    [TestMethod]
    public void LargeSurfacesUseBoundedSpatialIndexAndStillClipCorrectly()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(0, Rectangle(-100000, -100000, 200000, 200000), Quad, Vector3.UnitZ);
        var result = resolver.Resolve(1, Rectangle(1234, 4321, 100, 100), Quad, Vector3.UnitZ)!;
        Assert.IsNotNull(result);
        Assert.AreEqual(0d, Area(result), 0.01);
    }

    [TestMethod]
    public void RotatedPlanesAndCameraIndependentWorldCoordinatesRetainCoverage()
    {
        foreach (var angle in new[] { 0f, 0.4f, 1.2f, 1.5707963f })
        {
            var rotation = Matrix4x4.CreateRotationX(angle) * Matrix4x4.CreateRotationY(angle);
            var normal = Vector3.TransformNormal(Vector3.UnitZ, rotation);
            var first = Rectangle(0, 0, 10, 10).Select(point => Vector3.Transform(point, rotation)).ToArray();
            var second = Rectangle(5, 0, 10, 10).Select(point => Vector3.Transform(point, rotation)).ToArray();
            var resolver = new CoplanarSurfaceResolver();
            resolver.Resolve(0, first, Quad, normal);
            var result = resolver.Resolve(1, second, Quad, normal)!;
            Assert.IsNotNull(result);
            Assert.AreEqual(50d, Area(result), 0.01);
        }
    }

    [TestMethod]
    public void RandomizedRectangleClippingMatchesAnalyticIntersectionArea()
    {
        var random = new Random(1234);
        for (var i = 0; i < 300; i++)
        {
            var x = (float)(random.NextDouble() * 20 - 5);
            var y = (float)(random.NextDouble() * 20 - 5);
            var width = (float)(random.NextDouble() * 10 + 5);
            var height = (float)(random.NextDouble() * 10 + 5);
            var intersectionWidth = MathF.Max(0, MathF.Min(10, x + width) - MathF.Max(0, x));
            var intersectionHeight = MathF.Max(0, MathF.Min(10, y + height) - MathF.Max(0, y));
            var expected = width * height - intersectionWidth * intersectionHeight;
            var resolver = new CoplanarSurfaceResolver();
            resolver.Resolve(0, Rectangle(0, 0, 10, 10), Quad, Vector3.UnitZ);
            var result = resolver.Resolve(1, Rectangle(x, y, width, height), Quad, Vector3.UnitZ);
            Assert.AreEqual(expected, result is null ? width * height : Area(result), 0.002);
        }
    }

    [TestMethod]
    public void ThousandsOfDisjointCoplanarPiecesRetainTheirOriginalSurfaces()
    {
        var resolver = new CoplanarSurfaceResolver();
        for (var i = 0; i < 5000; i++)
            Assert.IsNull(resolver.Resolve(i, Rectangle((i % 100) * 1000, (i / 100) * 1000, 900, 900), Quad, Vector3.UnitZ));
    }

    [TestMethod]
    public void ConcavePolygonsKeepTheirTriangulationAndClippedOuterEdges()
    {
        var resolver = new CoplanarSurfaceResolver();
        resolver.Resolve(0, Rectangle(0.5f, 0.5f, 1, 1), Quad, Vector3.UnitZ);
        Vector3[] points = [new(0, 0, 0), new(4, 0, 0), new(4, 1, 0), new(1, 1, 0), new(1, 4, 0), new(0, 4, 0)];
        uint[] triangles = [0, 1, 2, 0, 2, 3, 0, 3, 5, 3, 4, 5];
        var result = resolver.Resolve(1, points, triangles, Vector3.UnitZ)!;
        Assert.IsNotNull(result);
        Assert.AreEqual(6.25d, Area(result), 0.001);
        Assert.AreEqual(15d, result.Edges.Sum(edge => (double)Vector3.Distance(edge.Start, edge.End)), 0.001);
    }
}
