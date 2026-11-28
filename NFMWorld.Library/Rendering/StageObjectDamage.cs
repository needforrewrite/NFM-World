using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Rendering;

public static class StageObjectDamage
{
    public static Rad3dPoly[] Deform(StageObject obj)
    {
        if (obj.Damage == 0 || obj.Durability == 0)

            return obj.Rad.Polys;
        var fraction = Math.Clamp((float)obj.Damage / obj.Durability, 0, 1);
        var amplitude = Math.Clamp(obj.MaxRadius * 0.12f, 8, 120);
        var inverseRotation = Matrix.Transpose(Matrix.CreateFromEuler((Euler)obj.Rotation));
        var push = Vector3.TransformNormal((Vector3)obj.Deformation / obj.Durability, inverseRotation) * amplitude;
        var result = new Rad3dPoly[obj.Rad.Polys.Length];

        for (var i = 0; i < result.Length; i++)
        {
            var original = obj.Rad.Polys[i];
            var points = new Vector3[original.Points.Length];

            for (var j = 0; j < points.Length; j++)
            {
                var point = original.Points[j];
                var seed = unchecked((uint)obj.DestructionId * 747796405u
                                     ^ (uint)BitConverter.SingleToInt32Bits(point.X)
                                     ^ (uint)BitConverter.SingleToInt32Bits(point.Y) * 2891336453u
                                     ^ (uint)BitConverter.SingleToInt32Bits(point.Z) * 277803737u);

                float Noise()
                {
                    seed = unchecked(seed * 1664525u + 1013904223u);

                    return (seed >> 8) / 16777215f;
                }

                var weight = 0.25f + Noise() * 0.75f;
                var jitter = new Vector3(Noise() - 0.5f, Noise() - 0.5f, Noise() - 0.5f);
                points[j] = point + push * weight + jitter * (amplitude * fraction * 0.45f);
            }

            result[i] = original.WithPoints(points, original.Triangles);
        }

        return result;
    }
}
