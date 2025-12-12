using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public struct Euler(AngleSingle yaw, AngleSingle pitch, AngleSingle roll) : IEquatable<Euler>
{
    public AngleSingle Yaw { get; set; } = yaw;
    public AngleSingle Pitch { get; set; } = pitch;
    public AngleSingle Roll { get; set; } = roll;
    
    public AngleSingle Xz => Yaw;
    public AngleSingle Zy => Pitch;
    public AngleSingle Xy => Roll;

    public static Euler Identity => new();

    public static Vector3 AxisYaw => Vector3.UnitX;
    public static Vector3 AxisPitch => Vector3.UnitY;
    public static Vector3 AxisRoll => Vector3.UnitZ;
    
    private static float Wrap(float radians)
    {
        float newangle = MathF.IEEERemainder(radians, MathUtil.TwoPi);

        if (newangle <= -MathUtil.Pi)
            newangle += MathUtil.TwoPi;
        else if (newangle > MathUtil.Pi)
            newangle -= MathUtil.TwoPi;

        return newangle;
    }
    
    private static AngleSingle Wrap(AngleSingle radians)
    {
        float newangle = MathF.IEEERemainder(radians.Radians, MathUtil.TwoPi);

        if (newangle <= -MathUtil.Pi)
            newangle += MathUtil.TwoPi;
        else if (newangle > MathUtil.Pi)
            newangle -= MathUtil.TwoPi;

        return AngleSingle.FromRadians(newangle);
    }

    public static Euler operator +(Euler a, Euler b) => new Euler(Wrap(a.Yaw + b.Yaw), Wrap(a.Pitch + b.Pitch), Wrap(a.Roll + b.Roll));
    public static Euler operator -(Euler a, Euler b) => new Euler(Wrap(a.Yaw - b.Yaw), Wrap(a.Pitch - b.Pitch), Wrap(a.Roll - b.Roll));
    public static Euler operator *(Euler a, AngleSingle b) => new Euler(Wrap(a.Yaw * b), Wrap(a.Pitch * b), Wrap(a.Roll * b));
    public static Euler operator *(AngleSingle a, Euler b) => new Euler(Wrap(b.Yaw * a), Wrap(b.Pitch * a), Wrap(b.Roll * a));
    public static Euler operator /(Euler a, AngleSingle b) => new Euler(Wrap(a.Yaw / b), Wrap(a.Pitch / b), Wrap(a.Roll / b));
    public static Euler operator -(Euler a) => new Euler(Wrap(-a.Yaw), Wrap(-a.Pitch), Wrap(-a.Roll));
    public static bool operator ==(Euler a, Euler b) => a.Yaw == b.Yaw && a.Pitch == b.Pitch && a.Roll == b.Roll;
    public static bool operator !=(Euler a, Euler b) => !(a == b);

    public static Vector3 operator *(Euler rotation, Vector3 vector) => ((Quaternion)rotation) * vector;

    public static implicit operator Vector3(Euler euler)
        => new(euler.Yaw.Radians, euler.Pitch.Radians, euler.Roll.Radians);

    public static implicit operator Quaternion(Euler euler)
        => Quaternion.RotationYawPitchRoll(euler.Yaw.Radians, euler.Pitch.Radians, euler.Roll.Radians);
    
    public bool Equals(Euler other) => Yaw.Equals(other.Yaw) && Pitch.Equals(other.Pitch) && Roll.Equals(other.Roll);
    public override bool Equals(object? obj) => obj is Euler other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Yaw, Pitch, Roll);
}