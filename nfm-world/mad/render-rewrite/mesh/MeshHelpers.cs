using HoleyDiver;

namespace NFMWorld.Mad;

public class MeshHelpers
{
    internal static PolygonTriangulator.TriangulationResult TriangulateIfNeeded(Vector3[] verts)
    {
        if (verts.Length <= 2)
        {
            return new PolygonTriangulator.TriangulationResult
            {
                PlaneNormal = Vector3.Zero,
                Centroid = verts.Length == 0 ? Vector3.Zero : verts[0],
                Triangles = [],
                RegionCount = 1
            };
        }
        
        if (verts.Length <= 3)
        {
            // Compute triangle normal
            var normal = Vector3.Normalize(Vector3.Cross(
                verts[1] - verts[0],
                verts[2] - verts[0]
            ));
            
            // Compute centroid
            var centroid = Vector3.Zero;
            foreach (var v in verts)
            {
                centroid += v;
            }
            centroid /= verts.Length;

            return new PolygonTriangulator.TriangulationResult
            {
                PlaneNormal = normal,
                Centroid = centroid,
                Triangles = verts.Length == 3 ? [0, 1, 2] : [],
                RegionCount = 1
            };
        }
        
        return PolygonTriangulator.Triangulate(verts);
    }
}