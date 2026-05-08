using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NFMWorldLibrary.FixedMath;

public struct f64AngleSingle : IComparable, IComparable<f64AngleSingle>, IEquatable<f64AngleSingle>, ISpanFormattable
{
    /// <summary>
    /// A value that specifies the size of a single degree.
    /// </summary>
    public static fix64 Degree { get; } = (fix64)0.002777777777777778f;

    /// <summary>
    /// A value that specifies the size of a single minute.
    /// </summary>
    public static fix64 Minute { get; } = (fix64)0.000046296296296296f;

    /// <summary>
    /// A value that specifies the size of a single second.
    /// </summary>
    public static fix64 Second { get; } = (fix64)0.000000771604938272f;

    /// <summary>
    /// A value that specifies the size of a single radian.
    /// </summary>
    public static fix64 Radian { get; } = (fix64)0.159154943091895336f;

    /// <summary>
    /// A value that specifies the size of a single milliradian.
    /// </summary>
    public static fix64 Milliradian { get; } = (fix64)0.0001591549431f;

    /// <summary>
    /// A value that specifies the size of a single gradian.
    /// </summary>
    public static fix64 Gradian { get; } = (fix64)0.0025f;

    /// <summary>
    /// Initializes a new instance of the <see cref="f64AngleSingle"/> struct with the
    /// given unit dependant angle and unit type.
    /// </summary>
    /// <param name="angle">A unit dependant measure of the angle.</param>
    /// <param name="type">The type of unit the angle argument is.</param>
    private f64AngleSingle(fix64 angle)
    {
        Degrees = angle;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="f64AngleSingle"/> struct using the
    /// arc length formula (θ = s/r).
    /// </summary>
    /// <param name="arcLength">The measure of the arc.</param>
    /// <param name="radius">The radius of the circle.</param>
    public f64AngleSingle(fix64 arcLength, fix64 radius)
    {
        Radians = arcLength / radius;
    }

    public static f64AngleSingle FromRadians(fix64 radians)
    {
        return new f64AngleSingle(radians * fix64.RadToDeg);
    }
    
    public static f64AngleSingle FromDegrees(fix64 degrees)
    {
        return new f64AngleSingle(degrees);
    }

    /// <summary>
    /// Wraps this Stride.Core.Mathematics.AngleSingle to be in the range [π, -π].
    /// </summary>
    public void Wrap()
    {
        fix64 newangle = fix64.IEEERemainder(Degrees, 360);

        if (newangle <= -180)
            newangle += 360;
        else if (newangle > 180)
            newangle -= 360;

        Degrees = newangle;
    }

    /// <summary>
    /// Wraps this Stride.Core.Mathematics.AngleSingle to be in the range [0, 2π).
    /// </summary>
    public void WrapPositive()
    {
        fix64 newangle = Degrees % 360;

        if (newangle < 0)
            newangle += 360;

        Degrees = newangle;
    }

    /// <summary>
    /// Gets or sets the total number of degrees this Stride.Core.Mathematics.AngleSingle represents.
    /// </summary>
    public fix64 Degrees { get; set; }

    /// <summary>
    /// Gets or sets the minutes component of the degrees this Stride.Core.Mathematics.AngleSingle represents.
    /// When setting the minutes, if the value is in the range (-60, 60) the whole degrees are
    /// not changed; otherwise, the whole degrees may be changed. Fractional values may set
    /// the seconds component.
    /// </summary>
    public fix64 Minutes
    {
        readonly get
        {
            fix64 degrees = Degrees;

            if (degrees < 0)
            {
                fix64 degreesfloor = fix64.Ceiling(degrees);
                return (degrees - degreesfloor) * 60;
            }
            else
            {
                fix64 degreesfloor = fix64.Floor(degrees);
                return (degrees - degreesfloor) * 60;
            }
        }
        set
        {
            fix64 degrees = Degrees;
            fix64 degreesfloor = fix64.Floor(degrees);

            degreesfloor += value / 60;
            Degrees = degreesfloor;
        }
    }

    /// <summary>
    /// Gets or sets the seconds of the degrees this Stride.Core.Mathematics.AngleSingle represents.
    /// When setting te seconds, if the value is in the range (-60, 60) the whole minutes
    /// or whole degrees are not changed; otherwise, the whole minutes or whole degrees
    /// may be changed.
    /// </summary>
    public fix64 Seconds
    {
        readonly get
        {
            fix64 degrees = Degrees;

            if (degrees < 0)
            {
                fix64 degreesfloor = fix64.Ceiling(degrees);

                fix64 minutes = (degrees - degreesfloor) * 60;
                fix64 minutesfloor = fix64.Ceiling(minutes);

                return (minutes - minutesfloor) * 60;
            }
            else
            {
                fix64 degreesfloor = fix64.Floor(degrees);

                fix64 minutes = (degrees - degreesfloor) * 60;
                fix64 minutesfloor = fix64.Floor(minutes);

                return (minutes - minutesfloor) * 60;
            }
        }
        set
        {
            fix64 degrees = Degrees;
            fix64 degreesfloor = fix64.Floor(degrees);

            fix64 minutes = (degrees - degreesfloor) * 60;
            fix64 minutesfloor = fix64.Floor(minutes);

            minutesfloor += value / 60;
            degreesfloor += minutesfloor / 60;
            Degrees = degreesfloor;
        }
    }

    /// <summary>
    /// Gets or sets the total number of radians this Stride.Core.Mathematics.AngleSingle represents.
    /// </summary>
    public fix64 Radians
    {
        readonly get => Degrees * fix64.DegToRad;
        set => Degrees = value * fix64.RadToDeg;
    }

    /// <summary>
    /// Gets a System.Boolean that determines whether this Stride.Core.Mathematics.Angle
    /// is a right angle (i.e. 90° or π/2).
    /// </summary>
    public readonly bool IsRight => Degrees == 90;

    /// <summary>
    /// Gets a System.Boolean that determines whether this Stride.Core.Mathematics.Angle
    /// is a straight angle (i.e. 180° or π).
    /// </summary>
    public readonly bool IsStraight => Degrees == 180;

    /// <summary>
    /// Gets a System.Boolean that determines whether this Stride.Core.Mathematics.Angle
    /// is a full rotation angle (i.e. 360° or 2π).
    /// </summary>
    public readonly bool IsFullRotation => Degrees == 360;

    /// <summary>
    /// Gets a System.Boolean that determines whether this Stride.Core.Mathematics.Angle
    /// is an oblique angle (i.e. is not 90° or a multiple of 90°).
    /// </summary>
    public readonly bool IsOblique => WrapPositive(this).Radians != 90;

    /// <summary>
    /// Gets a System.Boolean that determines whether this Stride.Core.Mathematics.Angle
    /// is an acute angle (i.e. less than 90° but greater than 0°).
    /// </summary>
    public readonly bool IsAcute => Degrees > 0 && Degrees < 90;

    /// <summary>
    /// Gets a System.Boolean that determines whether this Stride.Core.Mathematics.Angle
    /// is an obtuse angle (i.e. greater than 90° but less than 180°).
    /// </summary>
    public readonly bool IsObtuse => Degrees > 90 && Degrees < 180;

    /// <summary>
    /// Gets a System.Boolean that determines whether this Stride.Core.Mathematics.Angle
    /// is a reflex angle (i.e. greater than 180° but less than 360°).
    /// </summary>
    public readonly bool IsReflex => Degrees > 180 && Degrees < 360;

    /// <summary>
    /// Gets a Stride.Core.Mathematics.AngleSingle instance that complements this angle (i.e. the two angles add to 90°).
    /// </summary>
    public readonly f64AngleSingle Complement => new f64AngleSingle(90 - Degrees);

    /// <summary>
    /// Gets a Stride.Core.Mathematics.AngleSingle instance that supplements this angle (i.e. the two angles add to 180°).
    /// </summary>
    public readonly f64AngleSingle Supplement => new f64AngleSingle(180 - Degrees);

    /// <summary>
    /// Wraps the Stride.Core.Mathematics.AngleSingle given in the value argument to be in the range [π, -π].
    /// </summary>
    /// <param name="value">A Stride.Core.Mathematics.AngleSingle to wrap.</param>
    /// <returns>The Stride.Core.Mathematics.AngleSingle that is wrapped.</returns>
    public static f64AngleSingle Wrap(f64AngleSingle value)
    {
        value.Wrap();
        return value;
    }

    /// <summary>
    /// Wraps the Stride.Core.Mathematics.AngleSingle given in the value argument to be in the range [0, 2π).
    /// </summary>
    /// <param name="value">A Stride.Core.Mathematics.AngleSingle to wrap.</param>
    /// <returns>The Stride.Core.Mathematics.AngleSingle that is wrapped.</returns>
    public static f64AngleSingle WrapPositive(f64AngleSingle value)
    {
        value.WrapPositive();
        return value;
    }

    /// <summary>
    /// Compares two Stride.Core.Mathematics.AngleSingle instances and returns the smaller angle.
    /// </summary>
    /// <param name="left">The first Stride.Core.Mathematics.AngleSingle instance to compare.</param>
    /// <param name="right">The second Stride.Core.Mathematics.AngleSingle instance to compare.</param>
    /// <returns>The smaller of the two given Stride.Core.Mathematics.AngleSingle instances.</returns>
    public static f64AngleSingle Min(f64AngleSingle left, f64AngleSingle right)
    {
        if (left.Radians < right.Radians)
            return left;

        return right;
    }

    /// <summary>
    /// Compares two Stride.Core.Mathematics.AngleSingle instances and returns the greater angle.
    /// </summary>
    /// <param name="left">The first Stride.Core.Mathematics.AngleSingle instance to compare.</param>
    /// <param name="right">The second Stride.Core.Mathematics.AngleSingle instance to compare.</param>
    /// <returns>The greater of the two given Stride.Core.Mathematics.AngleSingle instances.</returns>
    public static f64AngleSingle Max(f64AngleSingle left, f64AngleSingle right)
    {
        if (left.Radians > right.Radians)
            return left;

        return right;
    }

    /// <summary>
    /// Adds two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The first object to add.</param>
    /// <param name="right">The second object to add.</param>
    /// <returns>The value of the two objects added together.</returns>
    public static f64AngleSingle Add(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians + right.Radians);
    }

    /// <summary>
    /// Subtracts two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The first object to subtract.</param>
    /// <param name="right">The second object to subtract.</param>
    /// <returns>The value of the two objects subtracted.</returns>
    public static f64AngleSingle Subtract(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians - right.Radians);
    }

    /// <summary>
    /// Multiplies two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The first object to multiply.</param>
    /// <param name="right">The second object to multiply.</param>
    /// <returns>The value of the two objects multiplied together.</returns>
    public static f64AngleSingle Multiply(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians * right.Radians);
    }

    /// <summary>
    /// Divides two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The numerator object.</param>
    /// <param name="right">The denominator object.</param>
    /// <returns>The value of the two objects divided.</returns>
    public static f64AngleSingle Divide(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians / right.Radians);
    }

    /// <summary>
    /// Gets a new Stride.Core.Mathematics.AngleSingle instance that represents the zero angle (i.e. 0°).
    /// </summary>
    public static f64AngleSingle ZeroAngle => new f64AngleSingle(fix64.Zero);

    /// <summary>
    /// Gets a new Stride.Core.Mathematics.AngleSingle instance that represents the right angle (i.e. 90° or π/2).
    /// </summary>
    public static f64AngleSingle RightAngle => new f64AngleSingle(fix64.HalfPi);

    /// <summary>
    /// Gets a new Stride.Core.Mathematics.AngleSingle instance that represents the straight angle (i.e. 180° or π).
    /// </summary>
    public static f64AngleSingle StraightAngle => new f64AngleSingle(fix64.Pi);

    /// <summary>
    /// Gets a new Stride.Core.Mathematics.AngleSingle instance that represents the full rotation angle (i.e. 360° or 2π).
    /// </summary>
    public static f64AngleSingle FullRotationAngle => new f64AngleSingle(fix64.TwoPi);

    /// <summary>
    /// Returns a System.Boolean that indicates whether the values of two Stride.Core.Mathematics.Angle
    /// objects are equal.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>True if the left and right parameters have the same value; otherwise, false.</returns>
    public static bool operator ==(f64AngleSingle left, f64AngleSingle right)
    {
        return left.Radians == right.Radians;
    }

    /// <summary>
    /// Returns a System.Boolean that indicates whether the values of two Stride.Core.Mathematics.Angle
    /// objects are not equal.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>True if the left and right parameters do not have the same value; otherwise, false.</returns>
    public static bool operator !=(f64AngleSingle left, f64AngleSingle right)
    {
        return left.Radians != right.Radians;
    }

    /// <summary>
    /// Returns a System.Boolean that indicates whether a Stride.Core.Mathematics.Angle
    /// object is less than another Stride.Core.Mathematics.AngleSingle object.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>True if left is less than right; otherwise, false.</returns>
    public static bool operator <(f64AngleSingle left, f64AngleSingle right)
    {
        return left.Radians < right.Radians;
    }

    /// <summary>
    /// Returns a System.Boolean that indicates whether a Stride.Core.Mathematics.Angle
    /// object is greater than another Stride.Core.Mathematics.AngleSingle object.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>True if left is greater than right; otherwise, false.</returns>
    public static bool operator >(f64AngleSingle left, f64AngleSingle right)
    {
        return left.Radians > right.Radians;
    }

    /// <summary>
    /// Returns a System.Boolean that indicates whether a Stride.Core.Mathematics.Angle
    /// object is less than or equal to another Stride.Core.Mathematics.AngleSingle object.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>True if left is less than or equal to right; otherwise, false.</returns>
    public static bool operator <=(f64AngleSingle left, f64AngleSingle right)
    {
        return left.Radians <= right.Radians;
    }

    /// <summary>
    /// Returns a System.Boolean that indicates whether a Stride.Core.Mathematics.Angle
    /// object is greater than or equal to another Stride.Core.Mathematics.AngleSingle object.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>True if left is greater than or equal to right; otherwise, false.</returns>
    public static bool operator >=(f64AngleSingle left, f64AngleSingle right)
    {
        return left.Radians >= right.Radians;
    }

    /// <summary>
    /// Returns the value of the Stride.Core.Mathematics.AngleSingle operand. (The sign of
    /// the operand is unchanged.)
    /// </summary>
    /// <param name="value">A Stride.Core.Mathematics.AngleSingle object.</param>
    /// <returns>The value of the value parameter.</returns>
    public static f64AngleSingle operator +(f64AngleSingle value)
    {
        return value;
    }

    /// <summary>
    /// Returns the negated value of the Stride.Core.Mathematics.AngleSingle operand.
    /// </summary>
    /// <param name="value">A Stride.Core.Mathematics.AngleSingle object.</param>
    /// <returns>The negated value of the value parameter.</returns>
    public static f64AngleSingle operator -(f64AngleSingle value)
    {
        return new f64AngleSingle(-value.Radians);
    }

    /// <summary>
    /// Adds two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The first object to add.</param>
    /// <param name="right">The second object to add.</param>
    /// <returns>The value of the two objects added together.</returns>
    public static f64AngleSingle operator +(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians + right.Radians);
    }

    /// <summary>
    /// Subtracts two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The first object to subtract</param>
    /// <param name="right">The second object to subtract.</param>
    /// <returns>The value of the two objects subtracted.</returns>
    public static f64AngleSingle operator -(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians - right.Radians);
    }

    /// <summary>
    /// Multiplies two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The first object to multiply.</param>
    /// <param name="right">The second object to multiply.</param>
    /// <returns>The value of the two objects multiplied together.</returns>
    public static f64AngleSingle operator *(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians * right.Radians);
    }

    /// <summary>
    /// Divides two Stride.Core.Mathematics.AngleSingle objects and returns the result.
    /// </summary>
    /// <param name="left">The numerator object.</param>
    /// <param name="right">The denominator object.</param>
    /// <returns>The value of the two objects divided.</returns>
    public static f64AngleSingle operator /(f64AngleSingle left, f64AngleSingle right)
    {
        return new f64AngleSingle(left.Radians / right.Radians);
    }

    /// <summary>
    /// Compares this instance to a specified object and returns an integer that
    /// indicates whether the value of this instance is less than, equal to, or greater
    /// than the value of the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns>
    /// A signed integer that indicates the relationship of the current instance
    /// to the obj parameter. If the value is less than zero, the current instance
    /// is less than the other. If the value is zero, the current instance is equal
    /// to the other. If the value is greater than zero, the current instance is
    /// greater than the other.
    /// </returns>
    public readonly int CompareTo(object? other)
    {
        if (other == null)
            return 1;

        if (other is not f64AngleSingle single)
            throw new ArgumentException("Argument must be of type Angle.", nameof(other));

        fix64 degrees = single.Degrees;

        if (Degrees > degrees)
            return 1;

        if (Degrees < degrees)
            return -1;

        return 0;
    }

    /// <summary>
    /// Compares this instance to a second Stride.Core.Mathematics.AngleSingle and returns
    /// an integer that indicates whether the value of this instance is less than,
    /// equal to, or greater than the value of the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns>
    /// A signed integer that indicates the relationship of the current instance
    /// to the obj parameter. If the value is less than zero, the current instance
    /// is less than the other. If the value is zero, the current instance is equal
    /// to the other. If the value is greater than zero, the current instance is
    /// greater than the other.
    /// </returns>
    public readonly int CompareTo(f64AngleSingle other)
    {
        if (this.Degrees > other.Degrees)
            return 1;

        if (this.Degrees < other.Degrees)
            return -1;

        return 0;
    }

    /// <summary>
    /// Returns a value that indicates whether the current instance and a specified
    /// Stride.Core.Mathematics.AngleSingle object have the same value.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns>
    /// Returns true if this Stride.Core.Mathematics.AngleSingle object and another have the same value;
    /// otherwise, false.
    /// </returns>
    public readonly bool Equals(f64AngleSingle other)
    {
        return this == other;
    }

    /// <summary>
    /// Returns a <see cref="string"/> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents this instance.
    /// </returns>
    public override readonly string ToString() => $"{this}";

    /// <summary>
    /// Returns a <see cref="string"/> that represents this instance.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="formatProvider">The format provider.</param>
    /// <returns>
    /// A <see cref="string"/> that represents this instance.
    /// </returns>
    public readonly string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format, IFormatProvider? formatProvider)
    {
        var handler = new DefaultInterpolatedStringHandler(1, 1, formatProvider);
        handler.AppendFormatted(Radians * fix64.RadToDeg, format ?? "0.##");
        handler.AppendLiteral("°");
        return handler.ToStringAndClear();
    }

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var format1 = format.Length > 0 ? format.ToString() : "0.##";
        var handler = new MemoryExtensions.TryWriteInterpolatedStringHandler(1, 1, destination, provider, out _);
        handler.AppendFormatted(Radians * fix64.RadToDeg, format1);
        handler.AppendLiteral("°");
        return destination.TryWrite(ref handler, out charsWritten);
    }

    /// <summary>
    /// Returns a hash code for this Stride.Core.Mathematics.AngleSingle instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Radians);
    }

    /// <summary>
    /// Returns a value that indicates whether the current instance and a specified
    /// object have the same value.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>
    /// Returns true if the obj parameter is a Stride.Core.Mathematics.AngleSingle object or a type
    /// capable of implicit conversion to a Stride.Core.Mathematics.AngleSingle value, and
    /// its value is equal to the value of the current Stride.Core.Mathematics.Angle
    /// object; otherwise, false.
    /// </returns>
    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is f64AngleSingle angleSingle && Equals(angleSingle);
    }
}
