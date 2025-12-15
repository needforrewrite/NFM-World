using HoleyDiver;

namespace NFMWorld.Mad;

public class MeshHelpers
{
    internal static PolygonTriangulator.TriangulationResult TriangulateIfNeeded(System.Numerics.Vector3[] verts)
    {
        if (verts.Length <= 3)
        {
            // Compute triangle normal
            var normal = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(
                verts[1] - verts[0],
                verts[2] - verts[0]
            ));
            
            // Compute centroid
            var centroid = System.Numerics.Vector3.Zero;
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