using FixedMathSharp;

namespace SoftFloat;

public partial struct fix64
{
    public static fix64 Pi { get; } = new fix64(FixedMath.PI);
    public static fix64 HalfPi { get; } = new fix64(FixedMath.PiOver2);
    public static fix64 TwoPi { get; } = new fix64(FixedMath.TwoPI);
    public static fix64 PiOver4 { get; } = new fix64(FixedMath.PiOver4);
    
    public static fix64 DegToRad { get; } = new(FixedMath.Deg2Rad);
    public static fix64 RadToDeg { get; } = new(FixedMath.Rad2Deg);

    public static bool operator <(fix64 f1, int f2) => f1 < (fix64)f2;
    public static bool operator >(fix64 f1, int f2) => f1 > (fix64)f2;
    public static bool operator <=(fix64 f1, int f2) => f1 <= (fix64)f2;
    public static bool operator >=(fix64 f1, int f2) => f1 >= (fix64)f2;
    public static bool operator <(int f1, fix64 f2) => (fix64)f1 < f2;
    public static bool operator >(int f1, fix64 f2) => (fix64)f1 > f2;
    public static bool operator >=(int f1, fix64 f2) => (fix64)f1 >= f2;
    public static bool operator <=(int f1, fix64 f2) => (fix64)f1 <= f2;
    public static fix64 operator +(fix64 f1, int f2) => f1 + (fix64)f2;
    public static fix64 operator +(int f1, fix64 f2) => (fix64)f1 + f2;
    public static fix64 operator -(fix64 f1, int f2) => f1 - (fix64)f2;
    public static fix64 operator -(int f1, fix64 f2) => (fix64)f1 - f2;
    public static fix64 operator *(fix64 f1, int f2) => f1 * (fix64)f2;
    public static fix64 operator *(int f1, fix64 f2) => (fix64)f1 * f2;
    public static fix64 operator /(fix64 f1, int f2) => f1 / (fix64)f2;
    public static fix64 operator /(int f1, fix64 f2) => (fix64)f1 / f2;
    public static fix64 operator %(fix64 f1, int f2) => f1 % (fix64)f2;
    public static fix64 operator %(int f1, fix64 f2) => (fix64)f1 % f2;
    
    public static bool operator <(fix64 f1, long f2) => f1 < (fix64)f2;
    public static bool operator >(fix64 f1, long f2) => f1 > (fix64)f2;
    public static bool operator <=(fix64 f1, long f2) => f1 <= (fix64)f2;
    public static bool operator >=(fix64 f1, long f2) => f1 >= (fix64)f2;
    public static bool operator <(long f1, fix64 f2) => (fix64)f1 < f2;
    public static bool operator >(long f1, fix64 f2) => (fix64)f1 > f2;
    public static bool operator >=(long f1, fix64 f2) => (fix64)f1 >= f2;
    public static bool operator <=(long f1, fix64 f2) => (fix64)f1 <= f2;
    public static fix64 operator +(fix64 f1, long f2) => f1 + (fix64)f2;
    public static fix64 operator +(long f1, fix64 f2) => (fix64)f1 + f2;
    public static fix64 operator -(fix64 f1, long f2) => f1 - (fix64)f2;
    public static fix64 operator -(long f1, fix64 f2) => (fix64)f1 - f2;
    public static fix64 operator *(fix64 f1, long f2) => f1 * (fix64)f2;
    public static fix64 operator *(long f1, fix64 f2) => (fix64)f1 * f2;
    public static fix64 operator /(fix64 f1, long f2) => f1 / (fix64)f2;
    public static fix64 operator /(long f1, fix64 f2) => (fix64)f1 / f2;
    public static fix64 operator %(fix64 f1, long f2) => f1 % (fix64)f2;
    public static fix64 operator %(long f1, fix64 f2) => (fix64)f1 % f2;

    public static fix64 operator --(fix64 f) => f + MinusOne;
    public static fix64 operator ++(fix64 f) => f + One;

    public static fix64 Sqrt(fix64 f) => new(FixedMath.Sqrt(f.Value));

    public static fix64 Acos(fix64 f) => new(FixedMath.Acos(f.Value));

    public static fix64 Atan2(fix64 a, fix64 b) => new(FixedMath.Atan2(a.Value, b.Value));

    public static fix64 Round(fix64 f) => new(FixedMath.Round(f.Value));

    public static fix64 Sin(fix64 f) => new(FixedMath.Sin(f.Value));

    public static fix64 Cos(fix64 f) => new(FixedMath.Cos(f.Value));

    public static fix64 Floor(fix64 f) => new(FixedMath.Floor(f.Value));

    public static fix64 Ceiling(fix64 f) => new(FixedMath.Ceiling(f.Value));

    public static fix64 Hypot(fix64 a, fix64 b) => Sqrt(a * a + b * b);

    public static fix64 Clamp(fix64 value, fix64 min, fix64 max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    
    public static bool WithinEpsilon(fix64 floatA, fix64 floatB) => Abs(floatA - floatB) < MachineEpsilonFloat;

    private static readonly fix64 MachineEpsilonFloat = GetMachineEpsilonFloat();

    /// <summary>
    /// Find the current machine's Epsilon for the float data type.
    /// (That is, the largest float, e,  where e == 0.0f is true.)
    /// </summary>
    private static fix64 GetMachineEpsilonFloat()
    {
	    return new(Fixed64.Epsilon);
    }
    /// <summary>
    /// Linearly interpolates between two values.
    /// </summary>
    /// <param name="value1">Source value.</param>
    /// <param name="value2">Source value.</param>
    /// <param name="amount">
    /// Value between 0 and 1 indicating the weight of value2.
    /// </param>
    /// <returns>Interpolated value.</returns>
    /// <remarks>
    /// This method performs the linear interpolation based on the following formula.
    /// <c>value1 + (value2 - value1) * amount</c>
    /// Passing amount a value of 0 will cause value1 to be returned, a value of 1 will
    /// cause value2 to be returned.
    /// </remarks>
    public static fix64 Lerp(fix64 value1, fix64 value2, fix64 amount)
    {
        return value1 + (value2 - value1) * amount;
    }
	
	/// <summary>
	/// Returns the Cartesian coordinate for one axis of a point that is defined by a
	/// given triangle and two normalized barycentric (areal) coordinates.
	/// </summary>
	/// <param name="value1">
	/// The coordinate on one axis of vertex 1 of the defining triangle.
	/// </param>
	/// <param name="value2">
	/// The coordinate on the same axis of vertex 2 of the defining triangle.
	/// </param>
	/// <param name="value3">
	/// The coordinate on the same axis of vertex 3 of the defining triangle.
	/// </param>
	/// <param name="amount1">
	/// The normalized barycentric (areal) coordinate b2, equal to the weighting factor
	/// for vertex 2, the coordinate of which is specified in value2.
	/// </param>
	/// <param name="amount2">
	/// The normalized barycentric (areal) coordinate b3, equal to the weighting factor
	/// for vertex 3, the coordinate of which is specified in value3.
	/// </param>
	/// <returns>
	/// Cartesian coordinate of the specified point with respect to the axis being used.
	/// </returns>
	public static fix64 Barycentric(
		fix64 value1,
		fix64 value2,
		fix64 value3,
		fix64 amount1,
		fix64 amount2
	) {
		return value1 + (value2 - value1) * amount1 + (value3 - value1) * amount2;
	}

	/// <summary>
	/// Performs a Catmull-Rom interpolation using the specified positions.
	/// </summary>
	/// <param name="value1">The first position in the interpolation.</param>
	/// <param name="value2">The second position in the interpolation.</param>
	/// <param name="value3">The third position in the interpolation.</param>
	/// <param name="value4">The fourth position in the interpolation.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>A position that is the result of the Catmull-Rom interpolation.</returns>
	public static fix64 CatmullRom(
		fix64 value1,
		fix64 value2,
		fix64 value3,
		fix64 value4,
		fix64 amount
	) {
		/* Using formula from http://www.mvps.org/directx/articles/catmull/
		 * Internally using doubles not to lose precision.
		 */
		fix64 amountSquared = amount * amount;
		fix64 amountCubed = amountSquared * amount;
		return (fix64) (
			(fix64)0.5f *
			(
				(((fix64)2.0f * value2 + (value3 - value1) * amount) +
				 (((fix64)2.0f * value1 - (fix64)5.0f * value2 + (fix64)4.0f * value3 - value4) * amountSquared) +
				 ((fix64)3.0f * value2 - value1 - (fix64)3.0f * value3 + value4) * amountCubed)
			)
		);
	}
}