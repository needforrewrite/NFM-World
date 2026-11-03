using System.Numerics;

namespace NFMWorldLibrary.Rendering;

public static class FixFlareGeometry
{
    public static bool TryFit(Vector3 min, Vector3 max, Matrix4x4 worldView, float near,
        bool perspective, out Vector3 center, out Vector2 halfSize)
    {
        center = Vector3.Transform((min + max) * 0.5f, worldView);
        halfSize = Vector2.Zero;
        var depth = -center.Z;
        if (!float.IsFinite(depth) || depth <= near) return false;
        var screenMin = new Vector2(float.PositiveInfinity);
        var screenMax = new Vector2(float.NegativeInfinity);
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.X : max.X,
                (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            var view = Vector3.Transform(corner, worldView);
            var scale = perspective ? depth / MathF.Max(near, -view.Z) : 1;
            var projected = new Vector2(view.X, view.Y) * scale;
            if (!float.IsFinite(projected.X) || !float.IsFinite(projected.Y)) return false;
            screenMin = Vector2.Min(screenMin, projected);
            screenMax = Vector2.Max(screenMax, projected);
        }
        var middle = (screenMin + screenMax) * 0.5f;
        center = new Vector3(middle, -depth);
        halfSize = Vector2.Max((screenMax - screenMin) * 0.5f, Vector2.One);
        return true;
    }

    public static void WriteOctagon(Vector2 halfSize, float rotation, Span<Vector2> points)
    {
        if (points.Length < 8) throw new ArgumentException("An octagon needs eight points.", nameof(points));
        var (sin, cos) = MathF.SinCos(rotation);
        var radius = new Vector2(MathF.Abs(cos) * halfSize.X + MathF.Abs(sin) * halfSize.Y,
            MathF.Abs(sin) * halfSize.X + MathF.Abs(cos) * halfSize.Y) * 1.3f;
        var bevel = MathF.Min(radius.X, radius.Y) * 0.4f;
        points[0] = new(-radius.X, -radius.Y + bevel);
        points[1] = new(-radius.X + bevel, -radius.Y);
        points[2] = new(radius.X - bevel, -radius.Y);
        points[3] = new(radius.X, -radius.Y + bevel);
        points[4] = new(radius.X, radius.Y - bevel);
        points[5] = new(radius.X - bevel, radius.Y);
        points[6] = new(-radius.X + bevel, radius.Y);
        points[7] = new(-radius.X, radius.Y - bevel);
        for (var i = 0; i < 8; i++)
        {
            var point = points[i];
            points[i] = new Vector2(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
        }
    }
}
