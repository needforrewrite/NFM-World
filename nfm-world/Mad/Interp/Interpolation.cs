using NFMWorldMath;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

namespace NFMWorld.Interp;

public class Interpolation
{
    public static Vector3 InterpolateCoord(Vector3 current, Vector3 prev, float alpha)
    {
        if (current == prev) return current;
        return current * alpha + prev * (1.0f - alpha);
    }
    
    public static f64Vector3 InterpolateCoord(f64Vector3 current, f64Vector3 prev, fix64 alpha)
    {
        return current * alpha + prev * (fix64.One - alpha);
    }

    public static Euler InterpolateEuler(Euler current, Euler prev, float alpha)
    {
        return new Euler(
            AngleSingle.FromRadians(InterpolateAngle(current.Yaw.Radians, prev.Yaw.Radians, alpha)),
            AngleSingle.FromRadians(InterpolateAngle(current.Pitch.Radians, prev.Pitch.Radians, alpha)),
            AngleSingle.FromRadians(InterpolateAngle(current.Roll.Radians, prev.Roll.Radians, alpha))
        );
    }

    public static f64Euler InterpolateEuler(f64Euler current, f64Euler prev, fix64 alpha)
    {
        return new f64Euler(
            f64AngleSingle.FromRadians(InterpolateAngle(current.Yaw.Radians, prev.Yaw.Radians, alpha)),
            f64AngleSingle.FromRadians(InterpolateAngle(current.Pitch.Radians, prev.Pitch.Radians, alpha)),
            f64AngleSingle.FromRadians(InterpolateAngle(current.Roll.Radians, prev.Roll.Radians, alpha))
        );
    }

    public static float InterpolateAngle(float current, float prev, float alpha)
    {
        var d = ((current - prev) % (MathF.PI * 2) + MathF.PI * 2) % (MathF.PI * 2);
        if (d > MathF.PI) d -= MathF.PI * 2;;
        return prev + d * alpha;
    }

    public static fix64 InterpolateAngle(fix64 current, fix64 prev, fix64 alpha)
    {
        var d = ((current - prev) % (fix64.Pi * 2) + fix64.Pi * 2) % (fix64.Pi * 2);
        if (d > fix64.Pi) d -= fix64.Pi * 2;
        return prev + d * alpha;
    }
}