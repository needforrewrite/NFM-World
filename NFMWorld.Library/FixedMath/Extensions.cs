using FixedMathSharp;

namespace NFMWorldLibrary.FixedMath;

public static class Extensions
{
    extension(FixedQuaternion quat)
    {
        // NFM yaw is inverted
        public static FixedQuaternion FromEuler(f64Euler euler)
        {
            return FixedQuaternion.FromEulerAnglesInDegrees(euler.Pitch.Degrees, -euler.Yaw.Degrees, euler.Roll.Degrees);
        }

        public f64Euler ToEuler()
        {
            var euler = quat.ToEulerAngles(); // pitch, yaw, roll
            return new f64Euler(f64AngleSingle.FromDegrees(-euler.Y), f64AngleSingle.FromDegrees(euler.X), f64AngleSingle.FromDegrees(euler.Z));
        }
    }
}