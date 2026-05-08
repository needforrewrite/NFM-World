using NFMWorldLibrary;

namespace NFMWorld;

public class LineMeshHelpers
{
    public const int VerticesPerLine = 4;
    public const int IndicesPerLine = 6;
    
    // vertices must contain 4 elements
    // indices must contain 6 elements
    //
    // Side encoding (shader decodes with abs/sign):
    //   +1 = endpoint A, offset +1
    //   -1 = endpoint A, offset -1
    //   +2 = endpoint B, offset +1
    //   -2 = endpoint B, offset -1
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
            SentrySdk.CaptureMessage("Degenerate line in LineMeshHelpers.CreateLineMesh", SentryLevel.Error);
            Logging.Error($"Degenerate line!!!!\n{System.Environment.StackTrace}");
        }
        
        outVerts[0] = new LineMesh.LineMeshVertexAttribute(p0, p1, -1f, normal, centroid, color, decalOffset);
        outVerts[1] = new LineMesh.LineMeshVertexAttribute(p0, p1,  1f, normal, centroid, color, decalOffset);
        outVerts[2] = new LineMesh.LineMeshVertexAttribute(p0, p1, -2f, normal, centroid, color, decalOffset);
        outVerts[3] = new LineMesh.LineMeshVertexAttribute(p0, p1,  2f, normal, centroid, color, decalOffset);
        // One quad (two triangles)
        ReadOnlySpan<int> indices =
        [
            baseIndex, baseIndex + 1, baseIndex + 3,
            baseIndex, baseIndex + 3, baseIndex + 2
        ];
        indices.CopyTo(outIndices);
    }
}