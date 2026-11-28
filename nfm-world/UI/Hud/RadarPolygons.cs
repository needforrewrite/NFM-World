using System.Numerics;
using System.Runtime.CompilerServices;
using LibTessDotNet;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud;

internal static class RadarPolygons
{
    private static readonly ConditionalWeakTable<IReadOnlyList<MinimapPolygon>, Batch> Batches = new();

    public static bool Draw(
        IReadOnlyList<MinimapPolygon> polygons,
        float clientX,
        float clientZ,
        float sin,
        float cos,
        float centerX,
        float centerY,
        float size,
        float range
    )
    {
        if (polygons.Count == 0)

            return false;

        var extent = range * (MathF.Abs(sin) + MathF.Abs(cos));
        var batch = Batches.GetValue(polygons, static polygons => new Batch(polygons));
        var count = 0;
        var half = size / 2;

        foreach (var i in batch.Index.Query(clientX - extent, clientZ - extent, clientX + extent, clientZ + extent))
        {
            var polygon = polygons[i];
            var vertices = polygon.Vertices;

            if (vertices.Count < 3)

                continue;

            var triangles = batch.Triangles[i];
            var vertexCount = triangles?.Length ?? 3;
            batch.EnsureCapacity(count + vertexCount);
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;

            for (var j = 0; j < vertexCount; j++)
            {
                var vertex = triangles == null
                    ? new Vector2(vertices[j].X, vertices[j].Z)
                    : triangles[j];
                var p = RadarMap.ToRadar(vertex.X, vertex.Y, clientX, clientZ, sin, cos, centerX, centerY, size, range);
                minX = MathF.Min(minX, p.X);
                maxX = MathF.Max(maxX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxY = MathF.Max(maxY, p.Y);
                batch.Positions[count + j] = p;
            }

            if (maxX < centerX - half
                || minX > centerX + half
                ||
                maxY < centerY - half
                || minY > centerY + half)

                continue;

            var color = new Color(polygon.R, polygon.G, polygon.B, 255);
            batch.Colors.AsSpan(count, vertexCount).Fill(color);
            count += vertexCount;
        }

        if (count != 0)
            TheGraphics.G.FillTriangles(batch.Positions.AsSpan(0, count), batch.Colors.AsSpan(0, count));

        return true;
    }

    private sealed class Batch
    {
        public readonly RadarPolygonIndex Index;

        // Stage surfaces are already triangles. Tessellate exceptional polygons only once.
        public readonly Vector2[]?[] Triangles;
        public Vector2[] Positions = [];
        public Color[] Colors = [];

        public Batch(IReadOnlyList<MinimapPolygon> polygons)
        {
            Index = new RadarPolygonIndex(polygons);
            Triangles = new Vector2[polygons.Count][];
            for (var i = 0; i < polygons.Count; i++)
                if (polygons[i].Vertices.Count > 3)
                    Triangles[i] = Tessellate(polygons[i].Vertices);
        }

        public void EnsureCapacity(int count)
        {
            if (Positions.Length >= count)

                return;
            var capacity = Math.Max(count, Math.Max(Positions.Length * 2, 256));
            Array.Resize(ref Positions, capacity);
            Array.Resize(ref Colors, capacity);
        }

        private static Vector2[] Tessellate(IReadOnlyList<(float X, float Z)> vertices)
        {
            var contour = new ContourVertex[vertices.Count];
            for (var i = 0; i < vertices.Count; i++)
                contour[i] = new ContourVertex(new Vec3(vertices[i].X, vertices[i].Z, 0));
            var tess = new Tess();
            tess.AddContour(contour);
            tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3, null, new Vec3(0, 0, 1));
            var result = new Vector2[tess.ElementCount * 3];

            for (var i = 0; i < result.Length; i++)
            {
                var p = tess.Vertices[tess.Elements[i]].Position;
                result[i] = new Vector2(p.X, p.Y);
            }

            return result;
        }
    }
}
