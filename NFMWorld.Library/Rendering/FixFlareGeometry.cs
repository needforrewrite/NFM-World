using System.Numerics;

namespace NFMWorldLibrary.Rendering;

/// <summary>
/// Builds the fix flare's octagonal ellipsoid shell. The flare used to be a camera-facing
/// billboard, which the depth test then cut in a straight screen-space line wherever a road
/// piece crossed the car's centre depth; a closed shell gives every fragment its own depth.
/// </summary>
public static class FixFlareGeometry
{
    /// <summary>Facets around the vertical axis.</summary>
    public const int Sides = 8;

    /// <summary>Latitude rings between the poles (poles excluded).</summary>
    public const int LatitudeRings = 3;

    /// <summary>Top pole, <see cref="LatitudeRings"/> rings of <see cref="Sides"/> points, bottom pole.</summary>
    public const int ShellVertexCount = Sides * LatitudeRings + 2;

    /// <summary>Two pole fans plus (rings - 1) bands of quads.</summary>
    public const int ShellTriangleCount = 2 * Sides + 2 * Sides * (LatitudeRings - 1);

    /// <summary>
    /// Widens the shell vertically so it reads as a ball rather than a disc squashed onto the
    /// car's low hull: the vertical radius is never smaller than the car's half-width.
    /// </summary>
    public static Vector3 BallHalfSize(Vector3 halfSize) =>
        new(halfSize.X, MathF.Max(halfSize.Y, halfSize.X), halfSize.Z);

    /// <summary>
    /// Writes <see cref="ShellVertexCount"/> points of an ellipsoid centred on
    /// <paramref name="center"/> whose per-axis radii are <paramref name="halfSize"/> times
    /// <paramref name="inflation"/>. Inflation of 1 or more encloses the box.
    /// </summary>
    public static void WriteShell(Vector3 center, Vector3 halfSize, float inflation, Span<Vector3> points)
    {
        if (points.Length < ShellVertexCount)
            throw new ArgumentException($"A shell needs {ShellVertexCount} points.", nameof(points));

        var radius = halfSize * inflation;
        points[0] = center + new Vector3(0, radius.Y, 0);
        points[ShellVertexCount - 1] = center - new Vector3(0, radius.Y, 0);
        for (var ring = 0; ring < LatitudeRings; ring++)
        {
            var phi = MathF.PI / 2 - (ring + 1) * MathF.PI / (LatitudeRings + 1);
            var (sinPhi, cosPhi) = MathF.SinCos(phi);
            for (var i = 0; i < Sides; i++)
            {
                var theta = MathF.Tau * i / Sides;
                var (sinTheta, cosTheta) = MathF.SinCos(theta);
                points[1 + ring * Sides + i] = center
                                               + new Vector3(
                                                   radius.X * cosPhi * cosTheta,
                                                   radius.Y * sinPhi,
                                                   radius.Z * cosPhi * sinTheta
                                               );
            }
        }
    }

    /// <summary>
    /// Writes the triangle list for one shell (three indices per triangle) into
    /// <paramref name="indices"/>, referencing vertices starting at <paramref name="vertexOffset"/>.
    /// </summary>
    public static void WriteShellIndices(Span<ushort> indices, ushort vertexOffset)
    {
        var required = ShellTriangleCount * 3;
        if (indices.Length < required)
            throw new ArgumentException($"A shell needs {required} indices.", nameof(indices));

        var index = 0;
        for (var i = 0; i < Sides; i++)
        {
            var next = (i + 1) % Sides;
            indices[index++] = vertexOffset;
            indices[index++] = (ushort)(vertexOffset + 1 + i);
            indices[index++] = (ushort)(vertexOffset + 1 + next);
        }

        for (var ring = 0; ring < LatitudeRings - 1; ring++)
        {
            var lower = 1 + ring * Sides;
            var upper = lower + Sides;
            for (var i = 0; i < Sides; i++)
            {
                var next = (i + 1) % Sides;
                indices[index++] = (ushort)(vertexOffset + lower + i);
                indices[index++] = (ushort)(vertexOffset + lower + next);
                indices[index++] = (ushort)(vertexOffset + upper + i);
                indices[index++] = (ushort)(vertexOffset + lower + next);
                indices[index++] = (ushort)(vertexOffset + upper + next);
                indices[index++] = (ushort)(vertexOffset + upper + i);
            }
        }

        var bottomRing = 1 + (LatitudeRings - 1) * Sides;
        ushort bottom = (ushort)(vertexOffset + ShellVertexCount - 1);
        for (var i = 0; i < Sides; i++)
        {
            var next = (i + 1) % Sides;
            indices[index++] = bottom;
            indices[index++] = (ushort)(vertexOffset + bottomRing + next);
            indices[index++] = (ushort)(vertexOffset + bottomRing + i);
        }
    }
}
