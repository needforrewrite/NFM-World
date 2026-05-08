using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.FixedMath;

public struct f64Euler(f64AngleSingle yaw, f64AngleSingle pitch, f64AngleSingle roll) : IEquatable<f64Euler>
{
    public f64AngleSingle Yaw { get; set; } = yaw;
    public f64AngleSingle Pitch { get; set; } = pitch;
    public f64AngleSingle Roll { get; set; } = roll;

    public f64AngleSingle Xz
    {
        get => Yaw;
        set => Yaw = value;
    }

    public f64AngleSingle Zy
    {
        get => Pitch;
        set => Pitch = value;
    }

    public f64AngleSingle Xy
    {
        get => Roll;
        set => Roll = value;
    }

    public static f64Euler Identity => new();

    public static f64Vector3 AxisYaw => f64Vector3.UnitX;
    public static f64Vector3 AxisPitch => f64Vector3.UnitY;
    public static f64Vector3 AxisRoll => f64Vector3.UnitZ;

    public f64Euler Wrap()
    {
        return new f64Euler(WrapSingle(Yaw), WrapSingle(Pitch), WrapSingle(Roll));
    }

    public f64Euler WrapPositive()
    {
        return new f64Euler(WrapSinglePositive(Yaw), WrapSinglePositive(Pitch), WrapSinglePositive(Roll));
    }
    
    /// <summary>
    /// Wraps this Stride.Core.Mathematics.AngleSingle to be in the range [0, 2π).
    /// </summary>
    private static f64AngleSingle WrapSinglePositive(f64AngleSingle radians)
    {
        var newangle = radians.Radians % fix64.TwoPi;

        if (newangle < 0)
            newangle += fix64.TwoPi;

        return f64AngleSingle.FromRadians(newangle);
    }

    private static fix64 WrapSingle(fix64 radians)
    {
        var newangle = fix64.IEEERemainder(radians, fix64.TwoPi);

        if (newangle <= -fix64.Pi)
            newangle += fix64.TwoPi;
        else if (newangle > fix64.Pi)
            newangle -= fix64.TwoPi;

        return newangle;
    }

    private static f64AngleSingle WrapSingle(f64AngleSingle radians)
    {
        var newangle = fix64.IEEERemainder(radians.Radians, fix64.TwoPi);

        if (newangle <= -fix64.Pi)
            newangle += fix64.TwoPi;
        else if (newangle > fix64.Pi)
            newangle -= fix64.TwoPi;

        return f64AngleSingle.FromRadians(newangle);
    }

    public static f64Euler operator +(f64Euler a, f64Euler b) =>
        new f64Euler(WrapSingle(a.Yaw + b.Yaw), WrapSingle(a.Pitch + b.Pitch), WrapSingle(a.Roll + b.Roll));

    public static f64Euler operator -(f64Euler a, f64Euler b) =>
        new f64Euler(WrapSingle(a.Yaw - b.Yaw), WrapSingle(a.Pitch - b.Pitch), WrapSingle(a.Roll - b.Roll));

    public static f64Euler operator *(f64Euler a, f64AngleSingle b) =>
        new f64Euler(WrapSingle(a.Yaw * b), WrapSingle(a.Pitch * b), WrapSingle(a.Roll * b));

    public static f64Euler operator *(f64AngleSingle a, f64Euler b) =>
        new f64Euler(WrapSingle(b.Yaw * a), WrapSingle(b.Pitch * a), WrapSingle(b.Roll * a));

    public static f64Euler operator /(f64Euler a, f64AngleSingle b) =>
        new f64Euler(WrapSingle(a.Yaw / b), WrapSingle(a.Pitch / b), WrapSingle(a.Roll / b));

    public static f64Euler operator -(f64Euler a) => new f64Euler(WrapSingle(-a.Yaw), WrapSingle(-a.Pitch), WrapSingle(-a.Roll));
    public static bool operator ==(f64Euler a, f64Euler b) => a.Yaw == b.Yaw && a.Pitch == b.Pitch && a.Roll == b.Roll;
    public static bool operator !=(f64Euler a, f64Euler b) => !(a == b);

    public static Vector3 operator *(f64Euler rotation, Vector3 vector) => Vector3.Transform(vector, rotation);

    public static implicit operator f64Vector3(f64Euler euler)
        => new(euler.Yaw.Radians, euler.Pitch.Radians, euler.Roll.Radians);

    public static explicit operator Vector3(f64Euler euler)
        => new((float)euler.Yaw.Radians, (float)euler.Pitch.Radians, (float)euler.Roll.Radians);

    public static implicit operator Quaternion(f64Euler euler)
        => Quaternion.CreateFromYawPitchRoll((float)euler.Yaw.Radians, (float)euler.Pitch.Radians, (float)euler.Roll.Radians);

    public bool Equals(f64Euler other) => Yaw.Equals(other.Yaw) && Pitch.Equals(other.Pitch) && Roll.Equals(other.Roll);
    public override bool Equals(object? obj) => obj is f64Euler other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Yaw, Pitch, Roll);
    
    public static explicit operator Euler(f64Euler euler)
        => new Euler(AngleSingle.FromRadians((float)euler.Yaw.Radians), AngleSingle.FromRadians((float)euler.Pitch.Radians), AngleSingle.FromRadians((float)euler.Roll.Radians));
}