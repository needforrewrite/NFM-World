using System.Diagnostics.CodeAnalysis;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Mad.Collision;

public static class CollisionExtensions
{
    extension(f64Vector3 vec)
    {
        public f64Vector3 RotateZy(fix64 zy) {
            var a = zy * fix64.DegToRad;
            return new f64Vector3(
                vec.X,
                vec.Y * fix64.Cos(a) + vec.Z * -fix64.Sin(a),
                vec.Y * fix64.Sin(a) + vec.Z * fix64.Cos(a)
            );
        }

        public f64Vector3 RotateXz(fix64 xz) {
            var a = -xz * fix64.DegToRad;
            return new f64Vector3(
                vec.X * fix64.Cos(a) + vec.Z * fix64.Sin(a),
                vec.Y,
                vec.X * -fix64.Sin(a) + vec.Z * fix64.Cos(a)
            );
        }

        public (fix64 x1, fix64 x2) Coefficients(f64Vector3 v1, f64Vector3 v2) {
            // A^T A terms
            var a11 = f64Vector3.Dot(v1, v1);
            var a12 = f64Vector3.Dot(v1, v2);
            var a22 = f64Vector3.Dot(v2, v2);

            // A^T b terms
            var b1 = f64Vector3.Dot(v1, vec);
            var b2 = f64Vector3.Dot(v2, vec);

            var det = a11 * a22 - a12 * a12;
            if (fix64.Abs(det) < (fix64)1e-9)
            {
                ThrowArgumentException();
            }

            var x1 = ( a22 * b1 - a12 * b2) / det;
            var x2 = (-a12 * b1 + a11 * b2) / det;

            return (x1, x2);

            [DoesNotReturn]
            static void ThrowArgumentException()
            {
                throw new ArgumentException("v1 and v2 are linearly dependent");
            }
        }
    }
}