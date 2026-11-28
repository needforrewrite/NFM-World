using System.Numerics;
using LibTessDotNet;

namespace NFMWorld.UI.Hud.GadgetSkin;

/// <summary>
/// phy's <c>java.awt.geom.Area</c>: shapes added to and subtracted from one another, in phy's
/// coordinates. A translucent gauge built from several overlapping pieces has to be filled as one
/// outline, otherwise every overlap blends twice and reads darker than the rest.
/// </summary>
/// <remarks>
/// The result is a set of non-overlapping contours, outer rings positive and holes negative, so
/// the nonzero rule fills it exactly. It is built once from constant geometry.
/// </remarks>
internal sealed class ShapeRegion
{
    // Chord sagitta stays under 0.2 phy units at the largest radius used. Finer steps leave
    // hairline gaps where the fill solver meets long near-vertical edges of merged contours.
    private const float ArcStepDegrees = 8f;

    private Vector2[][] _contours = [];

    public IReadOnlyList<Vector2[]> Contours => _contours;

    public ShapeRegion Add(Vector2[] shape) => Combine(shape, subtract: false);

    public ShapeRegion Subtract(Vector2[] shape) => Combine(shape, subtract: true);

    private ShapeRegion Combine(Vector2[] shape, bool subtract)
    {
        var tess = new Tess();
        foreach (var contour in _contours)
            tess.AddContour(ToVertices(contour, reverse: false));

        // A positive contour adds one to the winding of what it encloses, a negative one removes
        // it. Existing contours already carry their winding; a caller's polygon can wind either way.
        var positive = SignedArea(shape) > 0;
        tess.AddContour(ToVertices(shape, reverse: positive == subtract));
        tess.Tessellate(WindingRule.Positive, ElementType.BoundaryContours, 3, null, new Vec3(0, 0, 1));

        var result = new Vector2[tess.ElementCount][];

        for (var i = 0; i < result.Length; i++)
        {
            var start = tess.Elements[i * 2];
            var count = tess.Elements[i * 2 + 1];
            var points = new Vector2[count];

            for (var j = 0; j < count; j++)
            {
                var p = tess.Vertices[start + j].Position;
                points[j] = new Vector2(p.X, p.Y);
            }

            result[i] = points;
        }

        _contours = result;

        return this;
    }

    private static ContourVertex[] ToVertices(Vector2[] points, bool reverse)
    {
        var vertices = new ContourVertex[points.Length];

        for (var i = 0; i < points.Length; i++)
        {
            var p = points[reverse ? points.Length - 1 - i : i];
            vertices[i] = new ContourVertex(new Vec3(p.X, p.Y, 0));
        }

        return vertices;
    }

    private static float SignedArea(Vector2[] points)
    {
        var area = 0f;

        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            area += a.X * b.Y - a.Y * b.X;
        }

        return area * 0.5f;
    }

    // ── Shapes, in phy's own parameterisation ─────────────────────────

    public static Vector2[] Rect(float x, float y, float w, float h) =>
        [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h)];

    public static Vector2[] Polygon(params float[] xy)
    {
        var points = new Vector2[xy.Length / 2];
        for (var i = 0; i < points.Length; i++)
            points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);

        return points;
    }

    public static Vector2[] Ellipse(float cx, float cy, float rx, float ry)
    {
        var count = (int)MathF.Ceiling(360f / ArcStepDegrees);
        var points = new Vector2[count];

        for (var i = 0; i < count; i++)
        {
            var a = (i + 0.5f) * MathF.Tau / count;
            points[i] = new Vector2(cx + rx * MathF.Cos(a), cy + ry * MathF.Sin(a));
        }

        return points;
    }

    /// <summary>
    /// <c>fillArc</c>: a pie wedge. Angles are Java's, in degrees counter-clockwise from 3 o'clock
    /// with y up, so the screen y is <c>cy − r·sin θ</c>.
    /// </summary>
    public static Vector2[] Pie(float cx, float cy, float rx, float ry, float startDeg, float sweepDeg)
        => ArcPoints(cx, cy, rx, ry, startDeg, sweepDeg, centre: true);

    /// <summary><c>Arc2D.CHORD</c>: the arc closed by the straight line between its ends.</summary>
    public static Vector2[] Chord(float cx, float cy, float rx, float ry, float startDeg, float sweepDeg)
        => ArcPoints(cx, cy, rx, ry, startDeg, sweepDeg, centre: false);

    private static Vector2[] ArcPoints(
        float cx,
        float cy,
        float rx,
        float ry,
        float startDeg,
        float sweepDeg,
        bool centre
    )
    {
        var segments = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(sweepDeg) / ArcStepDegrees));
        var points = new List<Vector2>(segments + 2);
        if (centre)
            points.Add(new Vector2(cx, cy));

        for (var i = 0; i <= segments; i++)
        {
            var a = (startDeg + sweepDeg * i / segments) * MathF.PI / 180f;
            points.Add(new Vector2(cx + rx * MathF.Cos(a), cy - ry * MathF.Sin(a)));
        }

        return points.ToArray();
    }

    public static Vector2[] RoundRect(float x, float y, float w, float h, float radius)
    {
        var points = new List<Vector2>();
        var steps = (int)MathF.Ceiling(90f / ArcStepDegrees);

        void Corner(float cx, float cy, float startDeg)
        {
            for (var i = 0; i <= steps; i++)
            {
                var a = (startDeg + 90f * i / steps) * MathF.PI / 180f;
                points.Add(new Vector2(cx + radius * MathF.Cos(a), cy + radius * MathF.Sin(a)));
            }
        }

        Corner(x + w - radius, y + radius, -90);
        Corner(x + w - radius, y + h - radius, 0);
        Corner(x + radius, y + h - radius, 90);
        Corner(x + radius, y + radius, 180);

        return points.ToArray();
    }
}
