using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class LineMeshHelpers
{
    public const int VerticesPerLine = 8;
    public const int IndicesPerLine = 36;
    
    // vertices must contain 8 elements
    // indices must contain 36 elements
    public static void CreateLineMesh(Vector3 p0, Vector3 p1, int baseIndex, float halfThickness, in Span<Vector3> outVerts, in Span<int> outIndices)
    {
        var lineDir = Vector3.Normalize(p1 - p0);
        
        // Choose an initial perpendicular vector that's not parallel to lineDir
        var perpendicular = Math.Abs(lineDir.Y) > 0.99f ? Vector3.UnitX : Vector3.UnitY;

        var right = Vector3.Normalize(Vector3.Cross(lineDir, perpendicular)) * halfThickness;
        var up = Vector3.Normalize(Vector3.Cross(right, lineDir)) * halfThickness;
        var v0 = p0 - right - up;
        var v1 = p0 + right - up;
        var v2 = p0 + right + up;
        var v3 = p0 - right + up;
        var v4 = p1 - right - up;
        var v5 = p1 + right - up;
        var v6 = p1 + right + up;
        var v7 = p1 - right + up;
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