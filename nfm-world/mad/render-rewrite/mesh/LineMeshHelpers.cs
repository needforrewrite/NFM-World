using Stride.Core.Mathematics;
using Color = Microsoft.Xna.Framework.Color;

namespace NFMWorld.Mad;

public class LineMeshHelpers
{
    public const int VerticesPerLine = 8;
    public const int IndicesPerLine = 36;
    
    // vertices must contain 8 elements
    // indices must contain 36 elements
    public static void CreateLineMesh(
        Vector3 p0,
        Vector3 p1,
        int baseIndex,
        Vector3 normal,
        Vector3 centroid,
        Color color,
        float decalOffset,
        in Span<LineMesh.LineMeshVertexAttribute> outVerts,
        in Span<int> outIndices
    )
    {
        var lineDir = Vector3.Normalize(p1 - p0);
        
        if (lineDir == Vector3.Zero)
        {
            Console.WriteLine($"Degenerate line!!!!\n{System.Environment.StackTrace}");
        }
        
        // Choose an initial perpendicular vector that's not parallel to lineDir
        var perpendicular = Math.Abs(lineDir.Y) > 0.99f ? Vector3.UnitX : Vector3.UnitY;

        var right = Vector3.Normalize(Vector3.Cross(lineDir, perpendicular));
        var up = Vector3.Normalize(Vector3.Cross(right, lineDir));
        var v0 = new LineMesh.LineMeshVertexAttribute(p0, normal, centroid, color, decalOffset, -right, -up);
        var v1 = new LineMesh.LineMeshVertexAttribute(p0, normal, centroid, color, decalOffset, right, -up);
        var v2 = new LineMesh.LineMeshVertexAttribute(p0, normal, centroid, color, decalOffset, right, up);
        var v3 = new LineMesh.LineMeshVertexAttribute(p0, normal, centroid, color, decalOffset, -right, up);
        var v4 = new LineMesh.LineMeshVertexAttribute(p1, normal, centroid, color, decalOffset, -right, -up);
        var v5 = new LineMesh.LineMeshVertexAttribute(p1, normal, centroid, color, decalOffset, right, -up);
        var v6 = new LineMesh.LineMeshVertexAttribute(p1, normal, centroid, color, decalOffset, right, up);
        var v7 = new LineMesh.LineMeshVertexAttribute(p1, normal, centroid, color, decalOffset, -right, up);
        outVerts[0] = v0;
        outVerts[1] = v1;
        outVerts[2] = v2;
        outVerts[3] = v3;
        outVerts[4] = v4;
        outVerts[5] = v5;
        outVerts[6] = v6;
        outVerts[7] = v7;
        // Two triangles per quad face
        ReadOnlySpan<int> indices =
        [
            baseIndex, baseIndex + 1, baseIndex + 2,
            baseIndex, baseIndex + 2, baseIndex + 3,
            baseIndex + 4, baseIndex + 6, baseIndex + 5,
            baseIndex + 4, baseIndex + 7, baseIndex + 6,
            baseIndex + 3, baseIndex + 2, baseIndex + 6,
            baseIndex + 3, baseIndex + 6, baseIndex + 7,
            baseIndex, baseIndex + 5, baseIndex + 1,
            baseIndex, baseIndex + 4, baseIndex + 5,
            baseIndex + 1, baseIndex + 5, baseIndex + 6,
            baseIndex + 1, baseIndex + 6, baseIndex + 2,
            baseIndex + 4, baseIndex + 0, baseIndex + 3,
            baseIndex + 4, baseIndex + 3, baseIndex + 7
        ];
        indices.CopyTo(outIndices);
    }
}