using System.Runtime.CompilerServices;
using Maxine.Extensions.Mathematics;

namespace NFMWorldMath;

public static class Extensions
{
    extension(AngleSingle angle)
    {
        public fix64 DegreesSFloat => (fix64)angle.Radians * fix64.RadToDeg;

        public static AngleSingle FromRadians(float radians) => Unsafe.As<float, AngleSingle>(ref radians);

        public static AngleSingle FromDegrees(float degrees)
            => Unsafe.BitCast<float, AngleSingle>(MathUtil.DegreesToRadians(degrees));

        public static AngleSingle FromDegrees(int degrees)
            => Unsafe.BitCast<float, AngleSingle>((float)(degrees * fix64.DegToRad));

        public static AngleSingle FromDegrees(fix64 degrees)
            => Unsafe.BitCast<float, AngleSingle>((float)(degrees * fix64.DegToRad));
    }

}