using System.Diagnostics.CodeAnalysis;
using nfm_world_library.SoftFloat;
using nfm_world_library.util;

namespace nfm_world_library.mad.collision;

public static class CollisionExtensions
{
    extension(f64Vector3 vec)
    {
        public f64Vector3 RotateZy(fix64 zy) {
            return new f64Vector3(
                vec.X,
                vec.Y * UMath.Cos(zy) + vec.Z * -UMath.Sin(zy),
                vec.Y * UMath.Sin(zy) + vec.Z * UMath.Cos(zy)
            );
        }

        public f64Vector3 RotateXz(fix64 xz) {
            var a = -xz;
            return new f64Vector3(
                vec.X * UMath.Cos(a) + vec.Z * UMath.Sin(a),
                vec.Y,
                vec.X * -UMath.Sin(a) + vec.Z * UMath.Cos(a)
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
                return default;
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