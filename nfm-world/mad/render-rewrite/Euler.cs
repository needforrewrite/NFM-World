using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public struct Euler(AngleSingle yaw, AngleSingle pitch, AngleSingle roll) : IEquatable<Euler>
{
    public AngleSingle Yaw { get; set; } = yaw;
    public AngleSingle Pitch { get; set; } = pitch;
    public AngleSingle Roll { get; set; } = roll;

    public AngleSingle Xz
    {
        get => Yaw;
        set => Yaw = value;
    }

    public AngleSingle Zy
    {
        get => Pitch;
        set => Pitch = value;
    }

    public AngleSingle Xy
    {
        get => Roll;
        set => Roll = value;
    }

    public static Euler Identity => new();

    public static Vector3 AxisYaw => Vector3.UnitX;
    public static Vector3 AxisPitch => Vector3.UnitY;
    public static Vector3 AxisRoll => Vector3.UnitZ;

    public Euler Wrap()
    {
        return new Euler(WrapSingle(Yaw), WrapSingle(Pitch), WrapSingle(Roll));
    }

    public Euler WrapPositive()
    {
        return new Euler(WrapSinglePositive(Yaw), WrapSinglePositive(Pitch), WrapSinglePositive(Roll));
    }
    
    /// <summary>
    /// Wraps this Stride.Core.Mathematics.AngleSingle to be in the range [0, 2π).
    /// </summary>
    private static AngleSingle WrapSinglePositive(AngleSingle radians)
    {
        float newangle = radians.Radians % MathUtil.TwoPi;

        if (newangle < 0.0)
            newangle += MathUtil.TwoPi;

        return AngleSingle.FromRadians(newangle);
    }

    private static float WrapSingle(float radians)
    {
        float newangle = MathF.IEEERemainder(radians, MathUtil.TwoPi);

        if (newangle <= -MathUtil.Pi)
            newangle += MathUtil.TwoPi;
        else if (newangle > MathUtil.Pi)
            newangle -= MathUtil.TwoPi;

        return newangle;
    }

    private static AngleSingle WrapSingle(AngleSingle radians)
    {
        float newangle = MathF.IEEERemainder(radians.Radians, MathUtil.TwoPi);

        if (newangle <= -MathUtil.Pi)
            newangle += MathUtil.TwoPi;
        else if (newangle > MathUtil.Pi)
            newangle -= MathUtil.TwoPi;

        return AngleSingle.FromRadians(newangle);
    }

    public static Euler operator +(Euler a, Euler b) =>
        new Euler(WrapSingle(a.Yaw + b.Yaw), WrapSingle(a.Pitch + b.Pitch), WrapSingle(a.Roll + b.Roll));

    public static Euler operator -(Euler a, Euler b) =>
        new Euler(WrapSingle(a.Yaw - b.Yaw), WrapSingle(a.Pitch - b.Pitch), WrapSingle(a.Roll - b.Roll));

    public static Euler operator *(Euler a, AngleSingle b) =>
        new Euler(WrapSingle(a.Yaw * b), WrapSingle(a.Pitch * b), WrapSingle(a.Roll * b));

    public static Euler operator *(AngleSingle a, Euler b) =>
        new Euler(WrapSingle(b.Yaw * a), WrapSingle(b.Pitch * a), WrapSingle(b.Roll * a));

    public static Euler operator /(Euler a, AngleSingle b) =>
        new Euler(WrapSingle(a.Yaw / b), WrapSingle(a.Pitch / b), WrapSingle(a.Roll / b));

    public static Euler operator -(Euler a) => new Euler(WrapSingle(-a.Yaw), WrapSingle(-a.Pitch), WrapSingle(-a.Roll));
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