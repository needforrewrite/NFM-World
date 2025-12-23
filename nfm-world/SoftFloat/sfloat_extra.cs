using FixedMathSharp;

namespace SoftFloat;

public partial struct sfloat
{
    public static sfloat Pi { get; } = new sfloat(FixedMath.PI);
    public static sfloat HalfPi { get; } = new sfloat(FixedMath.PiOver2);
    public static sfloat TwoPi { get; } = new sfloat(FixedMath.TwoPI);
    public static sfloat PiOver4 { get; } = new sfloat(FixedMath.PiOver4);
    
    public static sfloat DegToRad { get; } = Pi / (sfloat)180.0f;
    public static sfloat RadToDeg { get; } = (sfloat)180.0f / Pi;

    public static bool operator <(sfloat f1, int f2) => f1 < (sfloat)f2;
    public static bool operator >(sfloat f1, int f2) => f1 > (sfloat)f2;
    public static bool operator <=(sfloat f1, int f2) => f1 <= (sfloat)f2;
    public static bool operator >=(sfloat f1, int f2) => f1 >= (sfloat)f2;
    public static bool operator <(int f1, sfloat f2) => (sfloat)f1 < f2;
    public static bool operator >(int f1, sfloat f2) => (sfloat)f1 > f2;
    public static bool operator >=(int f1, sfloat f2) => (sfloat)f1 >= f2;
    public static bool operator <=(int f1, sfloat f2) => (sfloat)f1 <= f2;
    public static sfloat operator +(sfloat f1, int f2) => f1 + (sfloat)f2;
    public static sfloat operator +(int f1, sfloat f2) => (sfloat)f1 + f2;
    public static sfloat operator -(sfloat f1, int f2) => f1 - (sfloat)f2;
    public static sfloat operator -(int f1, sfloat f2) => (sfloat)f1 - f2;
    public static sfloat operator *(sfloat f1, int f2) => f1 * (sfloat)f2;
    public static sfloat operator *(int f1, sfloat f2) => (sfloat)f1 * f2;
    public static sfloat operator /(sfloat f1, int f2) => f1 / (sfloat)f2;
    public static sfloat operator /(int f1, sfloat f2) => (sfloat)f1 / f2;
    public static sfloat operator %(sfloat f1, int f2) => f1 % (sfloat)f2;
    public static sfloat operator %(int f1, sfloat f2) => (sfloat)f1 % f2;
    
    public static bool operator <(sfloat f1, long f2) => f1 < (sfloat)f2;
    public static bool operator >(sfloat f1, long f2) => f1 > (sfloat)f2;
    public static bool operator <=(sfloat f1, long f2) => f1 <= (sfloat)f2;
    public static bool operator >=(sfloat f1, long f2) => f1 >= (sfloat)f2;
    public static bool operator <(long f1, sfloat f2) => (sfloat)f1 < f2;
    public static bool operator >(long f1, sfloat f2) => (sfloat)f1 > f2;
    public static bool operator >=(long f1, sfloat f2) => (sfloat)f1 >= f2;
    public static bool operator <=(long f1, sfloat f2) => (sfloat)f1 <= f2;
    public static sfloat operator +(sfloat f1, long f2) => f1 + (sfloat)f2;
    public static sfloat operator +(long f1, sfloat f2) => (sfloat)f1 + f2;
    public static sfloat operator -(sfloat f1, long f2) => f1 - (sfloat)f2;
    public static sfloat operator -(long f1, sfloat f2) => (sfloat)f1 - f2;
    public static sfloat operator *(sfloat f1, long f2) => f1 * (sfloat)f2;
    public static sfloat operator *(long f1, sfloat f2) => (sfloat)f1 * f2;
    public static sfloat operator /(sfloat f1, long f2) => f1 / (sfloat)f2;
    public static sfloat operator /(long f1, sfloat f2) => (sfloat)f1 / f2;
    public static sfloat operator %(sfloat f1, long f2) => f1 % (sfloat)f2;
    public static sfloat operator %(long f1, sfloat f2) => (sfloat)f1 % f2;

    public static sfloat operator --(sfloat f) => f + MinusOne;
    public static sfloat operator ++(sfloat f) => f + One;

    public static sfloat Sqrt(sfloat f) => new(FixedMath.Sqrt(f.Value));

    public static sfloat Acos(sfloat f) => new(FixedMath.Acos(f.Value));

    public static sfloat Atan2(sfloat a, sfloat b) => new(FixedMath.Atan2(a.Value, b.Value));

    public static sfloat Round(sfloat f) => new(FixedMath.Round(f.Value));

    public static sfloat Sin(sfloat f) => new(FixedMath.Sin(f.Value));

    public static sfloat Cos(sfloat f) => new(FixedMath.Cos(f.Value));

    public static sfloat Floor(sfloat f) => new(FixedMath.Floor(f.Value));

    public static sfloat Ceiling(sfloat f) => new(FixedMath.Ceiling(f.Value));

    public static sfloat Hypot(sfloat a, sfloat b) => Sqrt(a * a + b * b);

    public static sfloat Clamp(sfloat value, sfloat min, sfloat max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    
    public static bool WithinEpsilon(sfloat floatA, sfloat floatB) => Abs(floatA - floatB) < MachineEpsilonFloat;

    private static readonly sfloat MachineEpsilonFloat = GetMachineEpsilonFloat();

    /// <summary>
    /// Find the current machine's Epsilon for the float data type.
    /// (That is, the largest float, e,  where e == 0.0f is true.)
    /// </summary>
    private static sfloat GetMachineEpsilonFloat()
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
    public static sfloat Lerp(sfloat value1, sfloat value2, sfloat amount)
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
	public static sfloat Barycentric(
		sfloat value1,
		sfloat value2,
		sfloat value3,
		sfloat amount1,
		sfloat amount2
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
	public static sfloat CatmullRom(
		sfloat value1,
		sfloat value2,
		sfloat value3,
		sfloat value4,
		sfloat amount
	) {
		/* Using formula from http://www.mvps.org/directx/articles/catmull/
		 * Internally using doubles not to lose precision.
		 */
		sfloat amountSquared = amount * amount;
		sfloat amountCubed = amountSquared * amount;
		return (sfloat) (
			(sfloat)0.5f *
			(
				(((sfloat)2.0f * value2 + (value3 - value1) * amount) +
				 (((sfloat)2.0f * value1 - (sfloat)5.0f * value2 + (sfloat)4.0f * value3 - value4) * amountSquared) +
				 ((sfloat)3.0f * value2 - value1 - (sfloat)3.0f * value3 + value4) * amountCubed)
			)
		);
	}
}