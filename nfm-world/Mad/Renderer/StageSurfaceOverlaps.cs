using System.Collections.Immutable;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Rendering;

namespace NFMWorld;

internal static class StageSurfaceOverlaps
{
    public static Rad3dPoly[] Resolve(CoplanarSurfaceResolver resolver, int owner, Rad3dPoly[] polygons, Matrix world)
    {
        if (!Matrix.Invert(world, out var inverse)) return polygons;
        var normalMatrix = Matrix.Transpose(inverse);
        List<Rad3dPoly>? result = null;
        for (var i = 0; i < polygons.Length; i++)
        {
            var poly = polygons[i];
            if (poly.PolyType is PolyType.Glass or PolyType.Finish || poly.DecalOffset != 0 || poly.Triangles.IsDefaultOrEmpty)
            {
                result?.Add(poly);
                continue;
            }

            var points = new Vector3[poly.Points.Length];
            for (var j = 0; j < points.Length; j++) points[j] = Vector3.Transform(poly.Points[j], world);
            var surface = resolver.Resolve(
                owner,
                points,
                poly.Triangles.AsSpan(),
                Vector3.TransformNormal(poly.Normal, normalMatrix)
            );
            if (surface is null)
            {
                result?.Add(poly);
                continue;
            }

            if (result is null)
            {
                result = new List<Rad3dPoly>(polygons.Length);
                for (var j = 0; j < i; j++) result.Add(polygons[j]);
            }

            if (surface.Triangles.Length > 0)
            {
                for (var j = 0; j < surface.Points.Length; j++)
                    surface.Points[j] = Vector3.Transform(surface.Points[j], inverse);
                result.Add(
                    new Rad3dPoly(
                        poly.Color,
                        poly.ColNum,
                        poly.PolyType,
                        null,
                        poly.DecalOffset,
                        surface.Points,
                        ImmutableArray.CreateRange(surface.Triangles),
                        poly.Centroid,
                        poly.Normal
                    )
                );
            }

            if (poly.LineType is not { } lineType) continue;
            foreach (var edge in surface.Edges)
            {
                var a = Vector3.Transform(edge.Start, inverse);
                var b = Vector3.Transform(edge.End, inverse);
                result.Add(
                    new Rad3dPoly(
                        poly.Color,
                        poly.ColNum,
                        poly.PolyType,
                        lineType,
                        poly.DecalOffset,
                        [a, b],
                        [],
                        (a + b) * 0.5f,
                        poly.Normal
                    )
                );
            }
        }

        return result?.ToArray() ?? polygons;
    }
}
