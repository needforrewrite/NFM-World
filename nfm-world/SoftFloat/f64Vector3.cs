using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Xna.Framework.Design;

namespace SoftFloat;

/// <summary>
/// Describes a 3D-vector.
/// </summary>
[Serializable]
[TypeConverter(typeof(Vector3Converter))]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct f64Vector3 : IEquatable<f64Vector3>
{
	#region Public Static Properties

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 0, 0, 0.
	/// </summary>
	public static f64Vector3 Zero => zero;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 1, 1, 1.
	/// </summary>
	public static f64Vector3 One => one;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 1, 0, 0.
	/// </summary>
	public static f64Vector3 UnitX => unitX;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 0, 1, 0.
	/// </summary>
	public static f64Vector3 UnitY => unitY;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 0, 0, 1.
	/// </summary>
	public static f64Vector3 UnitZ => unitZ;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 0, 1, 0.
	/// </summary>
	public static f64Vector3 Up => up;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 0, -1, 0.
	/// </summary>
	public static f64Vector3 Down => down;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 1, 0, 0.
	/// </summary>
	public static f64Vector3 Right => right;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components -1, 0, 0.
	/// </summary>
	public static f64Vector3 Left => left;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 0, 0, -1.
	/// </summary>
	public static f64Vector3 Forward => forward;

	/// <summary>
	/// Returns a <see cref="f64Vector3"/> with components 0, 0, 1.
	/// </summary>
	public static f64Vector3 Backward => backward;

	#endregion

	#region Internal Properties

	internal string DebugDisplayString =>
		string.Concat(
			X.ToString(), " ",
			Y.ToString(), " ",
			Z.ToString()
		);

	#endregion

	#region Private Static Fields

	// These are NOT readonly, for weird performance reasons -flibit
	private static f64Vector3 zero = new f64Vector3((fix64)0f, (fix64)0f, (fix64)0f);
	private static f64Vector3 one = new f64Vector3((fix64)1f, (fix64)1f, (fix64)1f);
	private static f64Vector3 unitX = new f64Vector3((fix64)1f, (fix64)0f, (fix64)0f);
	private static f64Vector3 unitY = new f64Vector3((fix64)0f, (fix64)1f, (fix64)0f);
	private static f64Vector3 unitZ = new f64Vector3((fix64)0f, (fix64)0f, (fix64)1f);
	private static f64Vector3 up = new f64Vector3((fix64)0f, (fix64)1f, (fix64)0f);
	private static f64Vector3 down = new f64Vector3((fix64)0f, (fix64)(-1f), (fix64)0f);
	private static f64Vector3 right = new f64Vector3((fix64)1f, (fix64)0f, (fix64)0f);
	private static f64Vector3 left = new f64Vector3((fix64)(-1f), (fix64)0f, (fix64)0f);
	private static f64Vector3 forward = new f64Vector3((fix64)0f, (fix64)0f, (fix64)(-1f));
	private static f64Vector3 backward = new f64Vector3((fix64)0f, (fix64)0f, (fix64)1f);

	#endregion

	#region Public Fields

	/// <summary>
	/// The x coordinate of this <see cref="f64Vector3"/>.
	/// </summary>
	public fix64 X;

	/// <summary>
	/// The y coordinate of this <see cref="f64Vector3"/>.
	/// </summary>
	public fix64 Y;

	/// <summary>
	/// The z coordinate of this <see cref="f64Vector3"/>.
	/// </summary>
	public fix64 Z;

	#endregion

	#region Public Constructors

	/// <summary>
	/// Constructs a 3d vector with X, Y and Z from three values.
	/// </summary>
	/// <param name="x">The x coordinate in 3d-space.</param>
	/// <param name="y">The y coordinate in 3d-space.</param>
	/// <param name="z">The z coordinate in 3d-space.</param>
	public f64Vector3(fix64 x, fix64 y, fix64 z)
	{
		this.X = x;
		this.Y = y;
		this.Z = z;
	}

	/// <summary>
	/// Constructs a 3d vector with X, Y and Z set to the same value.
	/// </summary>
	/// <param name="value">The x, y and z coordinates in 3d-space.</param>
	public f64Vector3(fix64 value)
	{
		this.X = value;
		this.Y = value;
		this.Z = value;
	}

	/// <summary>
	/// Constructs a 3d vector with X, Y from <see cref="Vector2"/> and Z from a scalar.
	/// </summary>
	/// <param name="value">The x and y coordinates in 3d-space.</param>
	/// <param name="z">The z coordinate in 3d-space.</param>
	public f64Vector3(Vector2 value, fix64 z)
	{
		this.X = (fix64)value.X;
		this.Y = (fix64)value.Y;
		this.Z = z;
	}

	#endregion

	#region Public Methods

	/// <summary>
	/// Compares whether current instance is equal to specified <see cref="Object"/>.
	/// </summary>
	/// <param name="obj">The <see cref="Object"/> to compare.</param>
	/// <returns><c>true</c> if the instances are equal; <c>false</c> otherwise.</returns>
	public override bool Equals(object obj)
	{
		return (obj is f64Vector3) && Equals((f64Vector3) obj);
	}

	/// <summary>
	/// Compares whether current instance is equal to specified <see cref="f64Vector3"/>.
	/// </summary>
	/// <param name="other">The <see cref="f64Vector3"/> to compare.</param>
	/// <returns><c>true</c> if the instances are equal; <c>false</c> otherwise.</returns>
	public bool Equals(f64Vector3 other)
	{
		return (	X == other.X &&
				Y == other.Y &&
				Z == other.Z	);
	}

	/// <summary>
	/// Gets the hash code of this <see cref="f64Vector3"/>.
	/// </summary>
	/// <returns>Hash code of this <see cref="f64Vector3"/>.</returns>
	public override int GetHashCode()
	{
		return X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode();
	}

	/// <summary>
	/// Returns the length of this <see cref="f64Vector3"/>.
	/// </summary>
	/// <returns>The length of this <see cref="f64Vector3"/>.</returns>
	public fix64 Length()
	{
		return (fix64) fix64.Sqrt((X * X) + (Y * Y) + (Z * Z));
	}

	/// <summary>
	/// Returns the squared length of this <see cref="f64Vector3"/>.
	/// </summary>
	/// <returns>The squared length of this <see cref="f64Vector3"/>.</returns>
	public fix64 LengthSquared()
	{
		return (X * X) + (Y * Y) + (Z * Z);
	}

	/// <summary>
	/// Turns this <see cref="f64Vector3"/> to a unit vector with the same direction.
	/// </summary>
	public void Normalize()
	{
		fix64 factor = (fix64)1.0f / fix64.Sqrt(
			(X * X) +
			(Y * Y) +
			(Z * Z)
		);
		X *= factor;
		Y *= factor;
		Z *= factor;
	}

	/// <summary>
	/// Returns a <see cref="String"/> representation of this <see cref="f64Vector3"/> in the format:
	/// {X:[<see cref="X"/>] Y:[<see cref="Y"/>] Z:[<see cref="Z"/>]}
	/// </summary>
	/// <returns>A <see cref="String"/> representation of this <see cref="f64Vector3"/>.</returns>
	public override string ToString()
	{
		StringBuilder sb = new StringBuilder(32);
		sb.Append("{X:");
		sb.Append(this.X);
		sb.Append(" Y:");
		sb.Append(this.Y);
		sb.Append(" Z:");
		sb.Append(this.Z);
		sb.Append("}");
		return sb.ToString();
	}

	#endregion

	#region Internal Methods

	[Conditional("DEBUG")]
	internal void CheckForNaNs()
	{
	}

	#endregion

	#region Public Static Methods

	/// <summary>
	/// Performs vector addition on <paramref name="value1"/> and <paramref name="value2"/>.
	/// </summary>
	/// <param name="value1">The first vector to add.</param>
	/// <param name="value2">The second vector to add.</param>
	/// <returns>The result of the vector addition.</returns>
	public static f64Vector3 Add(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X += value2.X;
		value1.Y += value2.Y;
		value1.Z += value2.Z;
		return value1;
	}

	/// <summary>
	/// Performs vector addition on <paramref name="value1"/> and
	/// <paramref name="value2"/>, storing the result of the
	/// addition in <paramref name="result"/>.
	/// </summary>
	/// <param name="value1">The first vector to add.</param>
	/// <param name="value2">The second vector to add.</param>
	/// <param name="result">The result of the vector addition.</param>
	public static void Add(ref f64Vector3 value1, ref f64Vector3 value2, out f64Vector3 result)
	{
		result.X = value1.X + value2.X;
		result.Y = value1.Y + value2.Y;
		result.Z = value1.Z + value2.Z;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains the cartesian coordinates of a vector specified in barycentric coordinates and relative to 3d-triangle.
	/// </summary>
	/// <param name="value1">The first vector of 3d-triangle.</param>
	/// <param name="value2">The second vector of 3d-triangle.</param>
	/// <param name="value3">The third vector of 3d-triangle.</param>
	/// <param name="amount1">Barycentric scalar <c>b2</c> which represents a weighting factor towards second vector of 3d-triangle.</param>
	/// <param name="amount2">Barycentric scalar <c>b3</c> which represents a weighting factor towards third vector of 3d-triangle.</param>
	/// <returns>The cartesian translation of barycentric coordinates.</returns>
	public static f64Vector3 Barycentric(
		f64Vector3 value1,
		f64Vector3 value2,
		f64Vector3 value3,
		fix64 amount1,
		fix64 amount2
	) {
		return new f64Vector3(
			fix64.Barycentric(value1.X, value2.X, value3.X, amount1, amount2),
			fix64.Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2),
			fix64.Barycentric(value1.Z, value2.Z, value3.Z, amount1, amount2)
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains the cartesian coordinates of a vector specified in barycentric coordinates and relative to 3d-triangle.
	/// </summary>
	/// <param name="value1">The first vector of 3d-triangle.</param>
	/// <param name="value2">The second vector of 3d-triangle.</param>
	/// <param name="value3">The third vector of 3d-triangle.</param>
	/// <param name="amount1">Barycentric scalar <c>b2</c> which represents a weighting factor towards second vector of 3d-triangle.</param>
	/// <param name="amount2">Barycentric scalar <c>b3</c> which represents a weighting factor towards third vector of 3d-triangle.</param>
	/// <param name="result">The cartesian translation of barycentric coordinates as an output parameter.</param>
	public static void Barycentric(
		ref f64Vector3 value1,
		ref f64Vector3 value2,
		ref f64Vector3 value3,
		fix64 amount1,
		fix64 amount2,
		out f64Vector3 result
	) {
		result.X = fix64.Barycentric(value1.X, value2.X, value3.X, amount1, amount2);
		result.Y = fix64.Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2);
		result.Z = fix64.Barycentric(value1.Z, value2.Z, value3.Z, amount1, amount2);
	}
	
	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains CatmullRom interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector in interpolation.</param>
	/// <param name="value2">The second vector in interpolation.</param>
	/// <param name="value3">The third vector in interpolation.</param>
	/// <param name="value4">The fourth vector in interpolation.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>The result of CatmullRom interpolation.</returns>
	public static f64Vector3 CatmullRom(
		f64Vector3 value1,
		f64Vector3 value2,
		f64Vector3 value3,
		f64Vector3 value4,
		fix64 amount
	) {
		return new f64Vector3(
			fix64.CatmullRom(value1.X, value2.X, value3.X, value4.X, amount),
			fix64.CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount),
			fix64.CatmullRom(value1.Z, value2.Z, value3.Z, value4.Z, amount)
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains CatmullRom interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector in interpolation.</param>
	/// <param name="value2">The second vector in interpolation.</param>
	/// <param name="value3">The third vector in interpolation.</param>
	/// <param name="value4">The fourth vector in interpolation.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <param name="result">The result of CatmullRom interpolation as an output parameter.</param>
	public static void CatmullRom(
		ref f64Vector3 value1,
		ref f64Vector3 value2,
		ref f64Vector3 value3,
		ref f64Vector3 value4,
		fix64 amount,
		out f64Vector3 result
	) {
		result.X = fix64.CatmullRom(value1.X, value2.X, value3.X, value4.X, amount);
		result.Y = fix64.CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount);
		result.Z = fix64.CatmullRom(value1.Z, value2.Z, value3.Z, value4.Z, amount);
	}

	/// <summary>
	/// Clamps the specified value within a range.
	/// </summary>
	/// <param name="value1">The value to clamp.</param>
	/// <param name="min">The min value.</param>
	/// <param name="max">The max value.</param>
	/// <returns>The clamped value.</returns>
	public static f64Vector3 Clamp(f64Vector3 value1, f64Vector3 min, f64Vector3 max)
	{
		return new f64Vector3(
			fix64.Clamp(value1.X, min.X, max.X),
			fix64.Clamp(value1.Y, min.Y, max.Y),
			fix64.Clamp(value1.Z, min.Z, max.Z)
		);
	}

	/// <summary>
	/// Clamps the specified value within a range.
	/// </summary>
	/// <param name="value1">The value to clamp.</param>
	/// <param name="min">The min value.</param>
	/// <param name="max">The max value.</param>
	/// <param name="result">The clamped value as an output parameter.</param>
	public static void Clamp(
		ref f64Vector3 value1,
		ref f64Vector3 min,
		ref f64Vector3 max,
		out f64Vector3 result
	) {
		result.X = fix64.Clamp(value1.X, min.X, max.X);
		result.Y = fix64.Clamp(value1.Y, min.Y, max.Y);
		result.Z = fix64.Clamp(value1.Z, min.Z, max.Z);
	}

	/// <summary>
	/// Computes the cross product of two vectors.
	/// </summary>
	/// <param name="vector1">The first vector.</param>
	/// <param name="vector2">The second vector.</param>
	/// <returns>The cross product of two vectors.</returns>
	public static f64Vector3 Cross(f64Vector3 vector1, f64Vector3 vector2)
	{
		Cross(ref vector1, ref vector2, out vector1);
		return vector1;
	}

	/// <summary>
	/// Computes the cross product of two vectors.
	/// </summary>
	/// <param name="vector1">The first vector.</param>
	/// <param name="vector2">The second vector.</param>
	/// <param name="result">The cross product of two vectors as an output parameter.</param>
	public static void Cross(ref f64Vector3 vector1, ref f64Vector3 vector2, out f64Vector3 result)
	{
		fix64 x = vector1.Y * vector2.Z - vector2.Y * vector1.Z;
		fix64 y = -(vector1.X * vector2.Z - vector2.X * vector1.Z);
		fix64 z = vector1.X * vector2.Y - vector2.X * vector1.Y;
		result.X = x;
		result.Y = y;
		result.Z = z;
	}

	/// <summary>
	/// Returns the distance between two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <returns>The distance between two vectors.</returns>
	public static fix64 Distance(f64Vector3 vector1, f64Vector3 vector2)
	{
		fix64 result;
		DistanceSquared(ref vector1, ref vector2, out result);
		return (fix64) fix64.Sqrt(result);
	}

	/// <summary>
	/// Returns the distance between two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="result">The distance between two vectors as an output parameter.</param>
	public static void Distance(ref f64Vector3 value1, ref f64Vector3 value2, out fix64 result)
	{
		DistanceSquared(ref value1, ref value2, out result);
		result = (fix64) fix64.Sqrt(result);
	}

	/// <summary>
	/// Returns the squared distance between two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <returns>The squared distance between two vectors.</returns>
	public static fix64 DistanceSquared(f64Vector3 value1, f64Vector3 value2)
	{
		return (
			(value1.X - value2.X) * (value1.X - value2.X) +
			(value1.Y - value2.Y) * (value1.Y - value2.Y) +
			(value1.Z - value2.Z) * (value1.Z - value2.Z)
		);
	}

	/// <summary>
	/// Returns the squared distance between two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="result">The squared distance between two vectors as an output parameter.</param>
	public static void DistanceSquared(
		ref f64Vector3 value1,
		ref f64Vector3 value2,
		out fix64 result
	) {
		result = (
			(value1.X - value2.X) * (value1.X - value2.X) +
			(value1.Y - value2.Y) * (value1.Y - value2.Y) +
			(value1.Z - value2.Z) * (value1.Z - value2.Z)
		);
	}

	/// <summary>
	/// Divides the components of a <see cref="f64Vector3"/> by the components of another <see cref="f64Vector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Divisor <see cref="f64Vector3"/>.</param>
	/// <returns>The result of dividing the vectors.</returns>
	public static f64Vector3 Divide(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X /= value2.X;
		value1.Y /= value2.Y;
		value1.Z /= value2.Z;
		return value1;
	}

	/// <summary>
	/// Divides the components of a <see cref="f64Vector3"/> by the components of another <see cref="f64Vector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Divisor <see cref="f64Vector3"/>.</param>
	/// <param name="result">The result of dividing the vectors as an output parameter.</param>
	public static void Divide(ref f64Vector3 value1, ref f64Vector3 value2, out f64Vector3 result)
	{
		result.X = value1.X / value2.X;
		result.Y = value1.Y / value2.Y;
		result.Z = value1.Z / value2.Z;
	}

	/// <summary>
	/// Divides the components of a <see cref="f64Vector3"/> by a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Divisor scalar.</param>
	/// <returns>The result of dividing a vector by a scalar.</returns>
	public static f64Vector3 Divide(f64Vector3 value1, fix64 value2)
	{
		value1.X /= value2;
		value1.Y /= value2;
		value1.Z /= value2;
		return value1;
	}

	/// <summary>
	/// Divides the components of a <see cref="f64Vector3"/> by a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Divisor scalar.</param>
	/// <param name="result">The result of dividing a vector by a scalar as an output parameter.</param>
	public static void Divide(ref f64Vector3 value1, fix64 value2, out f64Vector3 result)
	{
		result.X = value1.X / value2;
		result.Y = value1.Y / value2;
		result.Z = value1.Z / value2;
	}

	/// <summary>
	/// Returns a dot product of two vectors.
	/// </summary>
	/// <param name="vector1">The first vector.</param>
	/// <param name="vector2">The second vector.</param>
	/// <returns>The dot product of two vectors.</returns>
	public static fix64 Dot(f64Vector3 vector1, f64Vector3 vector2)
	{
		return vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
	}

	/// <summary>
	/// Returns a dot product of two vectors.
	/// </summary>
	/// <param name="vector1">The first vector.</param>
	/// <param name="vector2">The second vector.</param>
	/// <param name="result">The dot product of two vectors as an output parameter.</param>
	public static void Dot(ref f64Vector3 vector1, ref f64Vector3 vector2, out fix64 result)
	{
		result = (
			(vector1.X * vector2.X) +
			(vector1.Y * vector2.Y) +
			(vector1.Z * vector2.Z)
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains hermite spline interpolation.
	/// </summary>
	/// <param name="value1">The first position vector.</param>
	/// <param name="tangent1">The first tangent vector.</param>
	/// <param name="value2">The second position vector.</param>
	/// <param name="tangent2">The second tangent vector.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>The hermite spline interpolation vector.</returns>
	public static f64Vector3 Hermite(
		f64Vector3 value1,
		f64Vector3 tangent1,
		f64Vector3 value2,
		f64Vector3 tangent2,
		fix64 amount
	) {
		f64Vector3 result = new f64Vector3();
		Hermite(ref value1, ref tangent1, ref value2, ref tangent2, amount, out result);
		return result;
	}

	/// <summary>
	/// Performs a Hermite spline interpolation.
	/// </summary>
	/// <param name="value1">Source position.</param>
	/// <param name="tangent1">Source tangent.</param>
	/// <param name="value2">Source position.</param>
	/// <param name="tangent2">Source tangent.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>The result of the Hermite spline interpolation.</returns>
	public static fix64 Hermite(
		fix64 value1,
		fix64 tangent1,
		fix64 value2,
		fix64 tangent2,
		fix64 amount
	) {
		/* All transformed to double not to lose precision
		 * Otherwise, for high numbers of param:amount the result is NaN instead
		 * of Infinity.
		 */
		fix64 v1 = value1, v2 = value2, t1 = tangent1, t2 = tangent2, s = amount;
		fix64 result;
		fix64 sCubed = s * s * s;
		fix64 sSquared = s * s;

		if (fix64.WithinEpsilon(amount, (fix64)0f))
		{
			result = value1;
		}
		else if (fix64.WithinEpsilon(amount, (fix64)1f))
		{
			result = value2;
		}
		else
		{
			result = (
				((2 * v1 - 2 * v2 + t2 + t1) * sCubed) +
				((3 * v2 - 3 * v1 - 2 * t1 - t2) * sSquared) +
				(t1 * s) +
				v1
			);
		}

		return (fix64) result;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains hermite spline interpolation.
	/// </summary>
	/// <param name="value1">The first position vector.</param>
	/// <param name="tangent1">The first tangent vector.</param>
	/// <param name="value2">The second position vector.</param>
	/// <param name="tangent2">The second tangent vector.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <param name="result">The hermite spline interpolation vector as an output parameter.</param>
	public static void Hermite(
		ref f64Vector3 value1,
		ref f64Vector3 tangent1,
		ref f64Vector3 value2,
		ref f64Vector3 tangent2,
		fix64 amount,
		out f64Vector3 result
	) {
		result.X = Hermite(value1.X, tangent1.X, value2.X, tangent2.X, amount);
		result.Y = Hermite(value1.Y, tangent1.Y, value2.Y, tangent2.Y, amount);
		result.Z = Hermite(value1.Z, tangent1.Z, value2.Z, tangent2.Z, amount);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains linear interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="amount">Weighting value(between 0.0 and 1.0).</param>
	/// <returns>The result of linear interpolation of the specified vectors.</returns>
	public static f64Vector3 Lerp(f64Vector3 value1, f64Vector3 value2, fix64 amount)
	{
		return new f64Vector3(
			fix64.Lerp(value1.X, value2.X, amount),
			fix64.Lerp(value1.Y, value2.Y, amount),
			fix64.Lerp(value1.Z, value2.Z, amount)
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains linear interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="amount">Weighting value(between 0.0 and 1.0).</param>
	/// <param name="result">The result of linear interpolation of the specified vectors as an output parameter.</param>
	public static void Lerp(
		ref f64Vector3 value1,
		ref f64Vector3 value2,
		fix64 amount,
		out f64Vector3 result
	) {
		result.X = fix64.Lerp(value1.X, value2.X, amount);
		result.Y = fix64.Lerp(value1.Y, value2.Y, amount);
		result.Z = fix64.Lerp(value1.Z, value2.Z, amount);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a maximal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <returns>The <see cref="f64Vector3"/> with maximal values from the two vectors.</returns>
	public static f64Vector3 Max(f64Vector3 value1, f64Vector3 value2)
	{
		return new f64Vector3(
			fix64.Max(value1.X, value2.X),
			fix64.Max(value1.Y, value2.Y),
			fix64.Max(value1.Z, value2.Z)
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a maximal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="result">The <see cref="f64Vector3"/> with maximal values from the two vectors as an output parameter.</param>
	public static void Max(ref f64Vector3 value1, ref f64Vector3 value2, out f64Vector3 result)
	{
		result.X = fix64.Max(value1.X, value2.X);
		result.Y = fix64.Max(value1.Y, value2.Y);
		result.Z = fix64.Max(value1.Z, value2.Z);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a minimal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <returns>The <see cref="f64Vector3"/> with minimal values from the two vectors.</returns>
	public static f64Vector3 Min(f64Vector3 value1, f64Vector3 value2)
	{
		return new f64Vector3(
			fix64.Min(value1.X, value2.X),
			fix64.Min(value1.Y, value2.Y),
			fix64.Min(value1.Z, value2.Z)
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a minimal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="result">The <see cref="f64Vector3"/> with minimal values from the two vectors as an output parameter.</param>
	public static void Min(ref f64Vector3 value1, ref f64Vector3 value2, out f64Vector3 result)
	{
		result.X = fix64.Min(value1.X, value2.X);
		result.Y = fix64.Min(value1.Y, value2.Y);
		result.Z = fix64.Min(value1.Z, value2.Z);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a multiplication of two vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/>.</param>
	/// <returns>The result of the vector multiplication.</returns>
	public static f64Vector3 Multiply(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X *= value2.X;
		value1.Y *= value2.Y;
		value1.Z *= value2.Z;
		return value1;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a multiplication of <see cref="f64Vector3"/> and a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="scaleFactor">Scalar value.</param>
	/// <returns>The result of the vector multiplication with a scalar.</returns>
	public static f64Vector3 Multiply(f64Vector3 value1, fix64 scaleFactor)
	{
		value1.X *= scaleFactor;
		value1.Y *= scaleFactor;
		value1.Z *= scaleFactor;
		return value1;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a multiplication of <see cref="f64Vector3"/> and a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="scaleFactor">Scalar value.</param>
	/// <param name="result">The result of the multiplication with a scalar as an output parameter.</param>
	public static void Multiply(ref f64Vector3 value1, fix64 scaleFactor, out f64Vector3 result)
	{
		result.X = value1.X * scaleFactor;
		result.Y = value1.Y * scaleFactor;
		result.Z = value1.Z * scaleFactor;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a multiplication of two vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/>.</param>
	/// <param name="result">The result of the vector multiplication as an output parameter.</param>
	public static void Multiply(ref f64Vector3 value1, ref f64Vector3 value2, out f64Vector3 result)
	{
		result.X = value1.X * value2.X;
		result.Y = value1.Y * value2.Y;
		result.Z = value1.Z * value2.Z;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains the specified vector inversion.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/>.</param>
	/// <returns>The result of the vector inversion.</returns>
	public static f64Vector3 Negate(f64Vector3 value)
	{
		value = new f64Vector3(-value.X, -value.Y, -value.Z);
		return value;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains the specified vector inversion.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/>.</param>
	/// <param name="result">The result of the vector inversion as an output parameter.</param>
	public static void Negate(ref f64Vector3 value, out f64Vector3 result)
	{
		result.X = -value.X;
		result.Y = -value.Y;
		result.Z = -value.Z;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a normalized values from another vector.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/>.</param>
	/// <returns>Unit vector.</returns>
	public static f64Vector3 Normalize(f64Vector3 value)
	{
		fix64 factor = (fix64)1.0f / (fix64) fix64.Sqrt(
			(value.X * value.X) +
			(value.Y * value.Y) +
			(value.Z * value.Z)
		);
		return new f64Vector3(
			value.X * factor,
			value.Y * factor,
			value.Z * factor
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a normalized values from another vector.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/>.</param>
	/// <param name="result">Unit vector as an output parameter.</param>
	public static void Normalize(ref f64Vector3 value, out f64Vector3 result)
	{
		fix64 factor = (fix64)1.0f / (fix64) fix64.Sqrt(
			(value.X * value.X) +
			(value.Y * value.Y) +
			(value.Z * value.Z)
		);
		result.X = value.X * factor;
		result.Y = value.Y * factor;
		result.Z = value.Z * factor;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains reflect vector of the given vector and normal.
	/// </summary>
	/// <param name="vector">Source <see cref="f64Vector3"/>.</param>
	/// <param name="normal">Reflection normal.</param>
	/// <returns>Reflected vector.</returns>
	public static f64Vector3 Reflect(f64Vector3 vector, f64Vector3 normal)
	{
		/* I is the original array.
		 * N is the normal of the incident plane.
		 * R = I - (2 * N * ( DotProduct[ I,N] ))
		 */
		f64Vector3 reflectedVector;
		// Inline the dotProduct here instead of calling method
		fix64 dotProduct = ((vector.X * normal.X) + (vector.Y * normal.Y)) +
					(vector.Z * normal.Z);
		reflectedVector.X = vector.X - ((fix64)2.0f * normal.X) * dotProduct;
		reflectedVector.Y = vector.Y - ((fix64)2.0f * normal.Y) * dotProduct;
		reflectedVector.Z = vector.Z - ((fix64)2.0f * normal.Z) * dotProduct;

		return reflectedVector;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains reflect vector of the given vector and normal.
	/// </summary>
	/// <param name="vector">Source <see cref="f64Vector3"/>.</param>
	/// <param name="normal">Reflection normal.</param>
	/// <param name="result">Reflected vector as an output parameter.</param>
	public static void Reflect(ref f64Vector3 vector, ref f64Vector3 normal, out f64Vector3 result)
	{
		/* I is the original array.
		 * N is the normal of the incident plane.
		 * R = I - (2 * N * ( DotProduct[ I,N] ))
		 */

		// Inline the dotProduct here instead of calling method.
		fix64 dotProduct = ((vector.X * normal.X) + (vector.Y * normal.Y)) +
					(vector.Z * normal.Z);
		result.X = vector.X - ((fix64)2.0f * normal.X) * dotProduct;
		result.Y = vector.Y - ((fix64)2.0f * normal.Y) * dotProduct;
		result.Z = vector.Z - ((fix64)2.0f * normal.Z) * dotProduct;

	}

	/// <summary>
	/// Interpolates between two values using a cubic equation.
	/// </summary>
	/// <param name="value1">Source value.</param>
	/// <param name="value2">Source value.</param>
	/// <param name="amount">Weighting value.</param>
	/// <returns>Interpolated value.</returns>
	private static fix64 SmoothStep(fix64 value1, fix64 value2, fix64 amount)
	{
		/* It is expected that 0 < amount < 1.
		 * If amount < 0, return value1.
		 * If amount > 1, return value2.
		 */
		fix64 result = fix64.Clamp(amount, (fix64)0f, (fix64)1f);
		result = Hermite(value1, (fix64)0f, value2, (fix64)0f, result);

		return result;
	}
	
	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains cubic interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/>.</param>
	/// <param name="amount">Weighting value.</param>
	/// <returns>Cubic interpolation of the specified vectors.</returns>
	public static f64Vector3 SmoothStep(f64Vector3 value1, f64Vector3 value2, fix64 amount)
	{
		return new f64Vector3(
			SmoothStep(value1.X, value2.X, amount),
			SmoothStep(value1.Y, value2.Y, amount),
			SmoothStep(value1.Z, value2.Z, amount)
		);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains cubic interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/>.</param>
	/// <param name="amount">Weighting value.</param>
	/// <param name="result">Cubic interpolation of the specified vectors as an output parameter.</param>
	public static void SmoothStep(
		ref f64Vector3 value1,
		ref f64Vector3 value2,
		fix64 amount,
		out f64Vector3 result
	) {
		result.X = SmoothStep(value1.X, value2.X, amount);
		result.Y = SmoothStep(value1.Y, value2.Y, amount);
		result.Z = SmoothStep(value1.Z, value2.Z, amount);
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains subtraction of on <see cref="f64Vector3"/> from a another.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/>.</param>
	/// <returns>The result of the vector subtraction.</returns>
	public static f64Vector3 Subtract(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X -= value2.X;
		value1.Y -= value2.Y;
		value1.Z -= value2.Z;
		return value1;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains subtraction of on <see cref="f64Vector3"/> from a another.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/>.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/>.</param>
	/// <param name="result">The result of the vector subtraction as an output parameter.</param>
	public static void Subtract(ref f64Vector3 value1, ref f64Vector3 value2, out f64Vector3 result)
	{
		result.X = value1.X - value2.X;
		result.Y = value1.Y - value2.Y;
		result.Z = value1.Z - value2.Z;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a transformation of 3d-vector by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="position">Source <see cref="f64Vector3"/>.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <returns>Transformed <see cref="f64Vector3"/>.</returns>
	public static f64Vector3 Transform(f64Vector3 position, Matrix matrix)
	{
		Transform(ref position, ref matrix, out position);
		return position;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a transformation of 3d-vector by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="position">Source <see cref="f64Vector3"/>.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="result">Transformed <see cref="f64Vector3"/> as an output parameter.</param>
	public static void Transform(
		ref f64Vector3 position,
		ref Matrix matrix,
		out f64Vector3 result
	) {
		fix64 x = (
			(position.X * (fix64)matrix.M11) +
			(position.Y * (fix64)matrix.M21) +
			(position.Z * (fix64)matrix.M31) +
			(fix64)matrix.M41
		);
		fix64 y = (
			(position.X * (fix64)matrix.M12) +
			(position.Y * (fix64)matrix.M22) +
			(position.Z * (fix64)matrix.M32) +
			(fix64)matrix.M42
		);
		fix64 z = (
			(position.X * (fix64)matrix.M13) +
			(position.Y * (fix64)matrix.M23) +
			(position.Z * (fix64)matrix.M33) +
			(fix64)matrix.M43
		);
		result.X = x;
		result.Y = y;
		result.Z = z;
	}

	/// <summary>
	/// Apply transformation on all vectors within array of <see cref="f64Vector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	public static void Transform(
		f64Vector3[] sourceArray,
		ref Matrix matrix,
		f64Vector3[] destinationArray
	) {
		Debug.Assert(
			destinationArray.Length >= sourceArray.Length,
			"The destination array is smaller than the source array."
		);

		/* TODO: Are there options on some platforms to implement
		 * a vectorized version of this?
		 */

		for (int i = 0; i < sourceArray.Length; i += 1)
		{
			f64Vector3 position = sourceArray[i];
			destinationArray[i] = new f64Vector3(
				(position.X * (fix64)matrix.M11) + (position.Y * (fix64)matrix.M21) +
					(position.Z * (fix64)matrix.M31) + (fix64)matrix.M41,
				(position.X * (fix64)matrix.M12) + (position.Y * (fix64)matrix.M22) +
					(position.Z * (fix64)matrix.M32) + (fix64)matrix.M42,
				(position.X * (fix64)matrix.M13) + (position.Y * (fix64)matrix.M23) +
					(position.Z * (fix64)matrix.M33) + (fix64)matrix.M43
			);
		}
	}

	/// <summary>
	/// Apply transformation on vectors within array of <see cref="f64Vector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="sourceIndex">The starting index of transformation in the source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	/// <param name="destinationIndex">The starting index in the destination array, where the first <see cref="f64Vector3"/> should be written.</param>
	/// <param name="length">The number of vectors to be transformed.</param>
	public static void Transform(
		f64Vector3[] sourceArray,
		int sourceIndex,
		ref Matrix matrix,
		f64Vector3[] destinationArray,
		int destinationIndex,
		int length
	) {
		Debug.Assert(
			sourceArray.Length - sourceIndex >= length,
			"The source array is too small for the given sourceIndex and length."
		);
		Debug.Assert(
			destinationArray.Length - destinationIndex >= length,
			"The destination array is too small for " +
			"the given destinationIndex and length."
		);

		/* TODO: Are there options on some platforms to implement a
		 * vectorized version of this?
		 */

		for (int i = 0; i < length; i += 1)
		{
			f64Vector3 position = sourceArray[sourceIndex + i];
			destinationArray[destinationIndex + i] = new f64Vector3(
				(position.X * (fix64)matrix.M11) + (position.Y * (fix64)matrix.M21) +
					(position.Z * (fix64)matrix.M31) + (fix64)matrix.M41,
				(position.X * (fix64)matrix.M12) + (position.Y * (fix64)matrix.M22) +
					(position.Z * (fix64)matrix.M32) + (fix64)matrix.M42,
				(position.X * (fix64)matrix.M13) + (position.Y * (fix64)matrix.M23) +
					(position.Z * (fix64)matrix.M33) + (fix64)matrix.M43
			);
		}
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a transformation of 3d-vector by the specified <see cref="Quaternion"/>, representing the rotation.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/>.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <returns>Transformed <see cref="f64Vector3"/>.</returns>
	public static f64Vector3 Transform(f64Vector3 value, Quaternion rotation)
	{
		f64Vector3 result;
		Transform(ref value, ref rotation, out result);
		return result;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a transformation of 3d-vector by the specified <see cref="Quaternion"/>, representing the rotation.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/>.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <param name="result">Transformed <see cref="f64Vector3"/> as an output parameter.</param>
	public static void Transform(
		ref f64Vector3 value,
		ref Quaternion rotation,
		out f64Vector3 result
	) {
		fix64 x = (fix64)2f * ((fix64)rotation.Y * value.Z - (fix64)rotation.Z * value.Y);
		fix64 y = (fix64)2f * ((fix64)rotation.Z * value.X - (fix64)rotation.X * value.Z);
		fix64 z = (fix64)2f * ((fix64)rotation.X * value.Y - (fix64)rotation.Y * value.X);

		result.X = value.X + x * (fix64)rotation.W + ((fix64)rotation.Y * z - (fix64)rotation.Z * y);
		result.Y = value.Y + y * (fix64)rotation.W + ((fix64)rotation.Z * x - (fix64)rotation.X * z);
		result.Z = value.Z + z * (fix64)rotation.W + ((fix64)rotation.X * y - (fix64)rotation.Y * x);
	}

	/// <summary>
	/// Apply transformation on all vectors within array of <see cref="f64Vector3"/> by the specified <see cref="Quaternion"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <param name="destinationArray">Destination array.</param>
	public static void Transform(
		f64Vector3[] sourceArray,
		ref Quaternion rotation,
		f64Vector3[] destinationArray
	) {
		Debug.Assert(
			destinationArray.Length >= sourceArray.Length,
			"The destination array is smaller than the source array."
		);

		/* TODO: Are there options on some platforms to implement
		 * a vectorized version of this?
		 */

		for (int i = 0; i < sourceArray.Length; i += 1)
		{
			f64Vector3 position = sourceArray[i];

			fix64 x = (fix64)2f * ((fix64)rotation.Y * position.Z - (fix64)rotation.Z * position.Y);
			fix64 y = (fix64)2f * ((fix64)rotation.Z * position.X - (fix64)rotation.X * position.Z);
			fix64 z = (fix64)2f * ((fix64)rotation.X * position.Y - (fix64)rotation.Y * position.X);

			destinationArray[i] = new f64Vector3(
				position.X + x * (fix64)rotation.W + ((fix64)rotation.Y * z - (fix64)rotation.Z * y),
				position.Y + y * (fix64)rotation.W + ((fix64)rotation.Z * x - (fix64)rotation.X * z),
				position.Z + z * (fix64)rotation.W + ((fix64)rotation.X * y - (fix64)rotation.Y * x)
			);
		}
	}

	/// <summary>

	/// Apply transformation on vectors within array of <see cref="f64Vector3"/> by the specified <see cref="Quaternion"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="sourceIndex">The starting index of transformation in the source array.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <param name="destinationArray">Destination array.</param>
	/// <param name="destinationIndex">The starting index in the destination array, where the first <see cref="f64Vector3"/> should be written.</param>
	/// <param name="length">The number of vectors to be transformed.</param>
	public static void Transform(
		f64Vector3[] sourceArray,
		int sourceIndex,
		ref Quaternion rotation,
		f64Vector3[] destinationArray,
		int destinationIndex,
		int length
	) {
		Debug.Assert(
			sourceArray.Length - sourceIndex >= length,
			"The source array is too small for the given sourceIndex and length."
		);
		Debug.Assert(
			destinationArray.Length - destinationIndex >= length,
			"The destination array is too small for the " +
			"given destinationIndex and length."
		);

		/* TODO: Are there options on some platforms to implement
		 * a vectorized version of this?
		 */

		for (int i = 0; i < length; i += 1)
		{
			f64Vector3 position = sourceArray[sourceIndex + i];

			fix64 x = 2 * ((fix64)rotation.Y * position.Z - (fix64)rotation.Z * position.Y);
			fix64 y = 2 * ((fix64)rotation.Z * position.X - (fix64)rotation.X * position.Z);
			fix64 z = 2 * ((fix64)rotation.X * position.Y - (fix64)rotation.Y * position.X);

			destinationArray[destinationIndex + i] = new f64Vector3(
				position.X + x * (fix64)rotation.W + ((fix64)rotation.Y * z - (fix64)rotation.Z * y),
				position.Y + y * (fix64)rotation.W + ((fix64)rotation.Z * x - (fix64)rotation.X * z),
				position.Z + z * (fix64)rotation.W + ((fix64)rotation.X * y - (fix64)rotation.Y * x)
			);
		}
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a transformation of the specified normal by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="normal">Source <see cref="f64Vector3"/> which represents a normal vector.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <returns>Transformed normal.</returns>
	public static f64Vector3 TransformNormal(f64Vector3 normal, Matrix matrix)
	{
		TransformNormal(ref normal, ref matrix, out normal);
		return normal;
	}

	/// <summary>
	/// Creates a new <see cref="f64Vector3"/> that contains a transformation of the specified normal by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="normal">Source <see cref="f64Vector3"/> which represents a normal vector.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="result">Transformed normal as an output parameter.</param>
	public static void TransformNormal(
		ref f64Vector3 normal,
		ref Matrix matrix,
		out f64Vector3 result
	) {
		fix64 x = (normal.X * (fix64)matrix.M11) + (normal.Y * (fix64)matrix.M21) + (normal.Z * (fix64)matrix.M31);
		fix64 y = (normal.X * (fix64)matrix.M12) + (normal.Y * (fix64)matrix.M22) + (normal.Z * (fix64)matrix.M32);
		fix64 z = (normal.X * (fix64)matrix.M13) + (normal.Y * (fix64)matrix.M23) + (normal.Z * (fix64)matrix.M33);
		result.X = x;
		result.Y = y;
		result.Z = z;
	}

	/// <summary>
	/// Apply transformation on all normals within array of <see cref="f64Vector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	public static void TransformNormal(
		f64Vector3[] sourceArray,
		ref Matrix matrix,
		f64Vector3[] destinationArray
	) {
		Debug.Assert(
			destinationArray.Length >= sourceArray.Length,
			"The destination array is smaller than the source array."
		);

		for (int i = 0; i < sourceArray.Length; i += 1)
		{
			f64Vector3 normal = sourceArray[i];
			destinationArray[i].X = (normal.X * (fix64)matrix.M11) + (normal.Y * (fix64)matrix.M21) + (normal.Z * (fix64)matrix.M31);
			destinationArray[i].Y = (normal.X * (fix64)matrix.M12) + (normal.Y * (fix64)matrix.M22) + (normal.Z * (fix64)matrix.M32);
			destinationArray[i].Z = (normal.X * (fix64)matrix.M13) + (normal.Y * (fix64)matrix.M23) + (normal.Z * (fix64)matrix.M33);
		}
	}

	/// <summary>
	/// Apply transformation on normals within array of <see cref="f64Vector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="sourceIndex">The starting index of transformation in the source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	/// <param name="destinationIndex">The starting index in the destination array, where the first <see cref="f64Vector3"/> should be written.</param>
	/// <param name="length">The number of normals to be transformed.</param>
	public static void TransformNormal(
		f64Vector3[] sourceArray,
		int sourceIndex,
		ref Matrix matrix,
		f64Vector3[] destinationArray,
		int destinationIndex,
		int length
	) {
		if (sourceArray == null)
		{
			throw new ArgumentNullException("sourceArray");
		}
		if (destinationArray == null)
		{
			throw new ArgumentNullException("destinationArray");
		}
		if ((sourceIndex + length) > sourceArray.Length)
		{
			throw new ArgumentException(
				"the combination of sourceIndex and " +
				"length was greater than sourceArray.Length"
			);
		}
		if ((destinationIndex + length) > destinationArray.Length)
		{
			throw new ArgumentException(
				"destinationArray is too small to " +
				"contain the result"
			);
		}

		for (int i = 0; i < length; i += 1)
		{
			f64Vector3 normal = sourceArray[i + sourceIndex];
			destinationArray[i + destinationIndex].X = (
				(normal.X * (fix64)matrix.M11) +
				(normal.Y * (fix64)matrix.M21) +
				(normal.Z * (fix64)matrix.M31)
			);
			destinationArray[i + destinationIndex].Y = (
				(normal.X * (fix64)matrix.M12) +
				(normal.Y * (fix64)matrix.M22) +
				(normal.Z * (fix64)matrix.M32)
			);
			destinationArray[i + destinationIndex].Z = (
				(normal.X * (fix64)matrix.M13) +
				(normal.Y * (fix64)matrix.M23) +
				(normal.Z * (fix64)matrix.M33)
			);
		}
	}

	#endregion

	#region Public Static Operators

	/// <summary>
	/// Compares whether two <see cref="f64Vector3"/> instances are equal.
	/// </summary>
	/// <param name="value1"><see cref="f64Vector3"/> instance on the left of the equal sign.</param>
	/// <param name="value2"><see cref="f64Vector3"/> instance on the right of the equal sign.</param>
	/// <returns><c>true</c> if the instances are equal; <c>false</c> otherwise.</returns>
	public static bool operator ==(f64Vector3 value1, f64Vector3 value2)
	{
		return (	value1.X == value2.X &&
				value1.Y == value2.Y &&
				value1.Z == value2.Z	);
	}

	/// <summary>
	/// Compares whether two <see cref="f64Vector3"/> instances are not equal.
	/// </summary>
	/// <param name="value1"><see cref="f64Vector3"/> instance on the left of the not equal sign.</param>
	/// <param name="value2"><see cref="f64Vector3"/> instance on the right of the not equal sign.</param>
	/// <returns><c>true</c> if the instances are not equal; <c>false</c> otherwise.</returns>
	public static bool operator !=(f64Vector3 value1, f64Vector3 value2)
	{
		return !(value1 == value2);
	}

	/// <summary>
	/// Adds two vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/> on the left of the add sign.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/> on the right of the add sign.</param>
	/// <returns>Sum of the vectors.</returns>
	public static f64Vector3 operator +(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X += value2.X;
		value1.Y += value2.Y;
		value1.Z += value2.Z;
		return value1;
	}

	/// <summary>
	/// Inverts values in the specified <see cref="f64Vector3"/>.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/> on the right of the sub sign.</param>
	/// <returns>Result of the inversion.</returns>
	public static f64Vector3 operator -(f64Vector3 value)
	{
		value = new f64Vector3(-value.X, -value.Y, -value.Z);
		return value;
	}

	/// <summary>
	/// Subtracts a <see cref="f64Vector3"/> from a <see cref="f64Vector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/> on the left of the sub sign.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/> on the right of the sub sign.</param>
	/// <returns>Result of the vector subtraction.</returns>
	public static f64Vector3 operator -(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X -= value2.X;
		value1.Y -= value2.Y;
		value1.Z -= value2.Z;
		return value1;
	}

	/// <summary>
	/// Multiplies the components of two vectors by each other.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/> on the left of the mul sign.</param>
	/// <param name="value2">Source <see cref="f64Vector3"/> on the right of the mul sign.</param>
	/// <returns>Result of the vector multiplication.</returns>
	public static f64Vector3 operator *(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X *= value2.X;
		value1.Y *= value2.Y;
		value1.Z *= value2.Z;
		return value1;
	}

	/// <summary>
	/// Multiplies the components of vector by a scalar.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/> on the left of the mul sign.</param>
	/// <param name="scaleFactor">Scalar value on the right of the mul sign.</param>
	/// <returns>Result of the vector multiplication with a scalar.</returns>
	public static f64Vector3 operator *(f64Vector3 value, fix64 scaleFactor)
	{
		value.X *= scaleFactor;
		value.Y *= scaleFactor;
		value.Z *= scaleFactor;
		return value;
	}

	/// <summary>
	/// Multiplies the components of vector by a scalar.
	/// </summary>
	/// <param name="scaleFactor">Scalar value on the left of the mul sign.</param>
	/// <param name="value">Source <see cref="f64Vector3"/> on the right of the mul sign.</param>
	/// <returns>Result of the vector multiplication with a scalar.</returns>
	public static f64Vector3 operator *(fix64 scaleFactor, f64Vector3 value)
	{
		value.X *= scaleFactor;
		value.Y *= scaleFactor;
		value.Z *= scaleFactor;
		return value;
	}

	/// <summary>
	/// Divides the components of a <see cref="f64Vector3"/> by the components of another <see cref="f64Vector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="f64Vector3"/> on the left of the div sign.</param>
	/// <param name="value2">Divisor <see cref="f64Vector3"/> on the right of the div sign.</param>
	/// <returns>The result of dividing the vectors.</returns>
	public static f64Vector3 operator /(f64Vector3 value1, f64Vector3 value2)
	{
		value1.X /= value2.X;
		value1.Y /= value2.Y;
		value1.Z /= value2.Z;
		return value1;
	}

	/// <summary>
	/// Divides the components of a <see cref="f64Vector3"/> by a scalar.
	/// </summary>
	/// <param name="value">Source <see cref="f64Vector3"/> on the left of the div sign.</param>
	/// <param name="divider">Divisor scalar on the right of the div sign.</param>
	/// <returns>The result of dividing a vector by a scalar.</returns>
	public static f64Vector3 operator /(f64Vector3 value, fix64 divider)
	{
		value.X /= divider;
		value.Y /= divider;
		value.Z /= divider;
		return value;
	}

	#endregion
	
	public static explicit operator Vector3(f64Vector3 value)
		=> new((float)value.X, (float)value.Y, (float)value.Z);
	public static explicit operator f64Vector3(Vector3 value)
		=> new((fix64)value.X, (fix64)value.Y, (fix64)value.Z);
}