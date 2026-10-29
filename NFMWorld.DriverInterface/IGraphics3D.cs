using System.Numerics;
using Maxine.Extensions.Mathematics;

namespace NFMWorld.DriverInterface.DriverInterface;

/// <summary>
/// Immediate-mode world-space triangles. Calls only append to a CPU batch; it is uploaded and
/// drawn once per frame, after the scene and before 2D. Triangles are two-sided, depth-tested,
/// and alpha-blended when any vertex alpha is below 255.
/// </summary>
public interface IGraphics3D
{
    /// <summary>One color for the whole mesh. <paramref name="indices"/> index into <paramref name="positions"/>, three per triangle.</summary>
    void DrawTriangles(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, Color color);

    /// <summary>Per-vertex colors; <paramref name="colors"/> must be the same length as <paramref name="positions"/>.</summary>
    void DrawTriangles(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Color> colors, ReadOnlySpan<int> indices);

    /// <summary>
    /// Segments between index pairs, drawn with the game's outline shader: constant screen-space width
    /// (the outline thickness setting), depth-tested, fogged like the world. Alpha is ignored.
    /// </summary>
    void DrawLines(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, Color color);

    /// <summary>Per-vertex colors, interpolated along each segment.</summary>
    void DrawLines(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Color> colors, ReadOnlySpan<int> indices);
}
