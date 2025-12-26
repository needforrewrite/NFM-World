// Collision rewrite by Phyrexian
// Ported to C# using fixe-point math for determinism

using SoftFloat;

namespace NFMWorld.Mad.Collision;

public class C_Vector3(fix64 x, fix64 y, fix64 z)
{
    public fix64 x = x, y = y, z = z;

    public C_Vector3 Scale(fix64 a) {
        return new C_Vector3(
            x * a,
            y * a,
            z * a
        );
    }

    public C_Vector3 Translate(C_Vector3 t) {
        return new C_Vector3(
            x + t.x,
            y + t.y,
            z + t.z
        );
    }

    public C_Vector3 RotateZY(fix64 zy) {
        fix64 a = zy * fix64.DegToRad;
        return new C_Vector3(
            x,
            y * fix64.Cos(a) + z * -fix64.Sin(a),
            y * fix64.Sin(a) + z * fix64.Cos(a)
        );
    }

    public C_Vector3 RotateXZ(fix64 xz) {
        fix64 a = -xz * fix64.DegToRad;
        return new C_Vector3(
            x * fix64.Cos(a) + z * fix64.Sin(a),
            y,
            x * -fix64.Sin(a) + z * fix64.Cos(a)
        );
    }

    public fix64 Dot(C_Vector3 v) {
        return x * v.x + y * v.y + z * v.z;
    }

    public fix64 Length() {
        return fix64.Sqrt(x * x + y * y + z * z);
    }

    public fix64[] Coefficients(C_Vector3 v1, C_Vector3 v2) {
        // A^T A terms
        fix64 a11 = v1.Dot(v1);
        fix64 a12 = v1.Dot(v2);
        fix64 a22 = v2.Dot(v2);

        // A^T b terms
        fix64 b1 = v1.Dot(this);
        fix64 b2 = v2.Dot(this);

        fix64 det = a11 * a22 - a12 * a12;
        if (fix64.Abs(det) < (fix64)1e-9f) {
            throw new ArgumentException("v1 and v2 are linearly dependent");
        }

        fix64 x1 = ( a22 * b1 - a12 * b2) / det;
        fix64 x2 = (-a12 * b1 + a11 * b2) / det;

        return [x1, x2];
    }

    public override string ToString() {
        return "C_Vector3{" +
                "x=" + x +
                ", y=" + y +
                ", z=" + z +
                '}';
    }
}