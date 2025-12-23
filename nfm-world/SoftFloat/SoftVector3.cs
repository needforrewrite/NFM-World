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
public struct SoftVector3 : IEquatable<SoftVector3>
{
	#region Public Static Properties

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 0, 0, 0.
	/// </summary>
	public static SoftVector3 Zero => zero;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 1, 1, 1.
	/// </summary>
	public static SoftVector3 One => one;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 1, 0, 0.
	/// </summary>
	public static SoftVector3 UnitX => unitX;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 0, 1, 0.
	/// </summary>
	public static SoftVector3 UnitY => unitY;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 0, 0, 1.
	/// </summary>
	public static SoftVector3 UnitZ => unitZ;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 0, 1, 0.
	/// </summary>
	public static SoftVector3 Up => up;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 0, -1, 0.
	/// </summary>
	public static SoftVector3 Down => down;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 1, 0, 0.
	/// </summary>
	public static SoftVector3 Right => right;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components -1, 0, 0.
	/// </summary>
	public static SoftVector3 Left => left;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 0, 0, -1.
	/// </summary>
	public static SoftVector3 Forward => forward;

	/// <summary>
	/// Returns a <see cref="SoftVector3"/> with components 0, 0, 1.
	/// </summary>
	public static SoftVector3 Backward => backward;

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
	private static SoftVector3 zero = new SoftVector3((sfloat)0f, (sfloat)0f, (sfloat)0f);
	private static SoftVector3 one = new SoftVector3((sfloat)1f, (sfloat)1f, (sfloat)1f);
	private static SoftVector3 unitX = new SoftVector3((sfloat)1f, (sfloat)0f, (sfloat)0f);
	private static SoftVector3 unitY = new SoftVector3((sfloat)0f, (sfloat)1f, (sfloat)0f);
	private static SoftVector3 unitZ = new SoftVector3((sfloat)0f, (sfloat)0f, (sfloat)1f);
	private static SoftVector3 up = new SoftVector3((sfloat)0f, (sfloat)1f, (sfloat)0f);
	private static SoftVector3 down = new SoftVector3((sfloat)0f, (sfloat)(-1f), (sfloat)0f);
	private static SoftVector3 right = new SoftVector3((sfloat)1f, (sfloat)0f, (sfloat)0f);
	private static SoftVector3 left = new SoftVector3((sfloat)(-1f), (sfloat)0f, (sfloat)0f);
	private static SoftVector3 forward = new SoftVector3((sfloat)0f, (sfloat)0f, (sfloat)(-1f));
	private static SoftVector3 backward = new SoftVector3((sfloat)0f, (sfloat)0f, (sfloat)1f);

	#endregion

	#region Public Fields

	/// <summary>
	/// The x coordinate of this <see cref="SoftVector3"/>.
	/// </summary>
	public sfloat X;

	/// <summary>
	/// The y coordinate of this <see cref="SoftVector3"/>.
	/// </summary>
	public sfloat Y;

	/// <summary>
	/// The z coordinate of this <see cref="SoftVector3"/>.
	/// </summary>
	public sfloat Z;

	#endregion

	#region Public Constructors

	/// <summary>
	/// Constructs a 3d vector with X, Y and Z from three values.
	/// </summary>
	/// <param name="x">The x coordinate in 3d-space.</param>
	/// <param name="y">The y coordinate in 3d-space.</param>
	/// <param name="z">The z coordinate in 3d-space.</param>
	public SoftVector3(sfloat x, sfloat y, sfloat z)
	{
		this.X = x;
		this.Y = y;
		this.Z = z;
	}

	/// <summary>
	/// Constructs a 3d vector with X, Y and Z set to the same value.
	/// </summary>
	/// <param name="value">The x, y and z coordinates in 3d-space.</param>
	public SoftVector3(sfloat value)
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
	public SoftVector3(Vector2 value, sfloat z)
	{
		this.X = (sfloat)value.X;
		this.Y = (sfloat)value.Y;
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
		return (obj is SoftVector3) && Equals((SoftVector3) obj);
	}

	/// <summary>
	/// Compares whether current instance is equal to specified <see cref="SoftVector3"/>.
	/// </summary>
	/// <param name="other">The <see cref="SoftVector3"/> to compare.</param>
	/// <returns><c>true</c> if the instances are equal; <c>false</c> otherwise.</returns>
	public bool Equals(SoftVector3 other)
	{
		return (	X == other.X &&
				Y == other.Y &&
				Z == other.Z	);
	}

	/// <summary>
	/// Gets the hash code of this <see cref="SoftVector3"/>.
	/// </summary>
	/// <returns>Hash code of this <see cref="SoftVector3"/>.</returns>
	public override int GetHashCode()
	{
		return X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode();
	}

	/// <summary>
	/// Returns the length of this <see cref="SoftVector3"/>.
	/// </summary>
	/// <returns>The length of this <see cref="SoftVector3"/>.</returns>
	public sfloat Length()
	{
		return (sfloat) sfloat.Sqrt((X * X) + (Y * Y) + (Z * Z));
	}

	/// <summary>
	/// Returns the squared length of this <see cref="SoftVector3"/>.
	/// </summary>
	/// <returns>The squared length of this <see cref="SoftVector3"/>.</returns>
	public sfloat LengthSquared()
	{
		return (X * X) + (Y * Y) + (Z * Z);
	}

	/// <summary>
	/// Turns this <see cref="SoftVector3"/> to a unit vector with the same direction.
	/// </summary>
	public void Normalize()
	{
		sfloat factor = (sfloat)1.0f / sfloat.Sqrt(
			(X * X) +
			(Y * Y) +
			(Z * Z)
		);
		X *= factor;
		Y *= factor;
		Z *= factor;
	}

	/// <summary>
	/// Returns a <see cref="String"/> representation of this <see cref="SoftVector3"/> in the format:
	/// {X:[<see cref="X"/>] Y:[<see cref="Y"/>] Z:[<see cref="Z"/>]}
	/// </summary>
	/// <returns>A <see cref="String"/> representation of this <see cref="SoftVector3"/>.</returns>
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
		if (	
			X.IsNaN() ||
			Y.IsNaN() ||
			Z.IsNaN()	)
		{
			throw new InvalidOperationException("SoftVector3 contains NaNs!");
		}
	}

	#endregion

	#region Public Static Methods

	/// <summary>
	/// Performs vector addition on <paramref name="value1"/> and <paramref name="value2"/>.
	/// </summary>
	/// <param name="value1">The first vector to add.</param>
	/// <param name="value2">The second vector to add.</param>
	/// <returns>The result of the vector addition.</returns>
	public static SoftVector3 Add(SoftVector3 value1, SoftVector3 value2)
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
	public static void Add(ref SoftVector3 value1, ref SoftVector3 value2, out SoftVector3 result)
	{
		result.X = value1.X + value2.X;
		result.Y = value1.Y + value2.Y;
		result.Z = value1.Z + value2.Z;
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
	private static sfloat Barycentric(
		sfloat value1,
		sfloat value2,
		sfloat value3,
		sfloat amount1,
		sfloat amount2
	) {
		return value1 + (value2 - value1) * amount1 + (value3 - value1) * amount2;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains the cartesian coordinates of a vector specified in barycentric coordinates and relative to 3d-triangle.
	/// </summary>
	/// <param name="value1">The first vector of 3d-triangle.</param>
	/// <param name="value2">The second vector of 3d-triangle.</param>
	/// <param name="value3">The third vector of 3d-triangle.</param>
	/// <param name="amount1">Barycentric scalar <c>b2</c> which represents a weighting factor towards second vector of 3d-triangle.</param>
	/// <param name="amount2">Barycentric scalar <c>b3</c> which represents a weighting factor towards third vector of 3d-triangle.</param>
	/// <returns>The cartesian translation of barycentric coordinates.</returns>
	public static SoftVector3 Barycentric(
		SoftVector3 value1,
		SoftVector3 value2,
		SoftVector3 value3,
		sfloat amount1,
		sfloat amount2
	) {
		return new SoftVector3(
			Barycentric(value1.X, value2.X, value3.X, amount1, amount2),
			Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2),
			Barycentric(value1.Z, value2.Z, value3.Z, amount1, amount2)
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains the cartesian coordinates of a vector specified in barycentric coordinates and relative to 3d-triangle.
	/// </summary>
	/// <param name="value1">The first vector of 3d-triangle.</param>
	/// <param name="value2">The second vector of 3d-triangle.</param>
	/// <param name="value3">The third vector of 3d-triangle.</param>
	/// <param name="amount1">Barycentric scalar <c>b2</c> which represents a weighting factor towards second vector of 3d-triangle.</param>
	/// <param name="amount2">Barycentric scalar <c>b3</c> which represents a weighting factor towards third vector of 3d-triangle.</param>
	/// <param name="result">The cartesian translation of barycentric coordinates as an output parameter.</param>
	public static void Barycentric(
		ref SoftVector3 value1,
		ref SoftVector3 value2,
		ref SoftVector3 value3,
		sfloat amount1,
		sfloat amount2,
		out SoftVector3 result
	) {
		result.X = Barycentric(value1.X, value2.X, value3.X, amount1, amount2);
		result.Y = Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2);
		result.Z = Barycentric(value1.Z, value2.Z, value3.Z, amount1, amount2);
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
	private static sfloat CatmullRom(
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
	
	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains CatmullRom interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector in interpolation.</param>
	/// <param name="value2">The second vector in interpolation.</param>
	/// <param name="value3">The third vector in interpolation.</param>
	/// <param name="value4">The fourth vector in interpolation.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>The result of CatmullRom interpolation.</returns>
	public static SoftVector3 CatmullRom(
		SoftVector3 value1,
		SoftVector3 value2,
		SoftVector3 value3,
		SoftVector3 value4,
		sfloat amount
	) {
		return new SoftVector3(
			CatmullRom(value1.X, value2.X, value3.X, value4.X, amount),
			CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount),
			CatmullRom(value1.Z, value2.Z, value3.Z, value4.Z, amount)
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains CatmullRom interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector in interpolation.</param>
	/// <param name="value2">The second vector in interpolation.</param>
	/// <param name="value3">The third vector in interpolation.</param>
	/// <param name="value4">The fourth vector in interpolation.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <param name="result">The result of CatmullRom interpolation as an output parameter.</param>
	public static void CatmullRom(
		ref SoftVector3 value1,
		ref SoftVector3 value2,
		ref SoftVector3 value3,
		ref SoftVector3 value4,
		sfloat amount,
		out SoftVector3 result
	) {
		result.X = CatmullRom(value1.X, value2.X, value3.X, value4.X, amount);
		result.Y = CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount);
		result.Z = CatmullRom(value1.Z, value2.Z, value3.Z, value4.Z, amount);
	}

	/// <summary>
	/// Clamps the specified value within a range.
	/// </summary>
	/// <param name="value1">The value to clamp.</param>
	/// <param name="min">The min value.</param>
	/// <param name="max">The max value.</param>
	/// <returns>The clamped value.</returns>
	public static SoftVector3 Clamp(SoftVector3 value1, SoftVector3 min, SoftVector3 max)
	{
		return new SoftVector3(
			sfloat.Clamp(value1.X, min.X, max.X),
			sfloat.Clamp(value1.Y, min.Y, max.Y),
			sfloat.Clamp(value1.Z, min.Z, max.Z)
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
		ref SoftVector3 value1,
		ref SoftVector3 min,
		ref SoftVector3 max,
		out SoftVector3 result
	) {
		result.X = sfloat.Clamp(value1.X, min.X, max.X);
		result.Y = sfloat.Clamp(value1.Y, min.Y, max.Y);
		result.Z = sfloat.Clamp(value1.Z, min.Z, max.Z);
	}

	/// <summary>
	/// Computes the cross product of two vectors.
	/// </summary>
	/// <param name="vector1">The first vector.</param>
	/// <param name="vector2">The second vector.</param>
	/// <returns>The cross product of two vectors.</returns>
	public static SoftVector3 Cross(SoftVector3 vector1, SoftVector3 vector2)
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
	public static void Cross(ref SoftVector3 vector1, ref SoftVector3 vector2, out SoftVector3 result)
	{
		sfloat x = vector1.Y * vector2.Z - vector2.Y * vector1.Z;
		sfloat y = -(vector1.X * vector2.Z - vector2.X * vector1.Z);
		sfloat z = vector1.X * vector2.Y - vector2.X * vector1.Y;
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
	public static sfloat Distance(SoftVector3 vector1, SoftVector3 vector2)
	{
		sfloat result;
		DistanceSquared(ref vector1, ref vector2, out result);
		return (sfloat) sfloat.Sqrt(result);
	}

	/// <summary>
	/// Returns the distance between two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="result">The distance between two vectors as an output parameter.</param>
	public static void Distance(ref SoftVector3 value1, ref SoftVector3 value2, out sfloat result)
	{
		DistanceSquared(ref value1, ref value2, out result);
		result = (sfloat) sfloat.Sqrt(result);
	}

	/// <summary>
	/// Returns the squared distance between two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <returns>The squared distance between two vectors.</returns>
	public static sfloat DistanceSquared(SoftVector3 value1, SoftVector3 value2)
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
		ref SoftVector3 value1,
		ref SoftVector3 value2,
		out sfloat result
	) {
		result = (
			(value1.X - value2.X) * (value1.X - value2.X) +
			(value1.Y - value2.Y) * (value1.Y - value2.Y) +
			(value1.Z - value2.Z) * (value1.Z - value2.Z)
		);
	}

	/// <summary>
	/// Divides the components of a <see cref="SoftVector3"/> by the components of another <see cref="SoftVector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Divisor <see cref="SoftVector3"/>.</param>
	/// <returns>The result of dividing the vectors.</returns>
	public static SoftVector3 Divide(SoftVector3 value1, SoftVector3 value2)
	{
		value1.X /= value2.X;
		value1.Y /= value2.Y;
		value1.Z /= value2.Z;
		return value1;
	}

	/// <summary>
	/// Divides the components of a <see cref="SoftVector3"/> by the components of another <see cref="SoftVector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Divisor <see cref="SoftVector3"/>.</param>
	/// <param name="result">The result of dividing the vectors as an output parameter.</param>
	public static void Divide(ref SoftVector3 value1, ref SoftVector3 value2, out SoftVector3 result)
	{
		result.X = value1.X / value2.X;
		result.Y = value1.Y / value2.Y;
		result.Z = value1.Z / value2.Z;
	}

	/// <summary>
	/// Divides the components of a <see cref="SoftVector3"/> by a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Divisor scalar.</param>
	/// <returns>The result of dividing a vector by a scalar.</returns>
	public static SoftVector3 Divide(SoftVector3 value1, sfloat value2)
	{
		value1.X /= value2;
		value1.Y /= value2;
		value1.Z /= value2;
		return value1;
	}

	/// <summary>
	/// Divides the components of a <see cref="SoftVector3"/> by a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Divisor scalar.</param>
	/// <param name="result">The result of dividing a vector by a scalar as an output parameter.</param>
	public static void Divide(ref SoftVector3 value1, sfloat value2, out SoftVector3 result)
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
	public static sfloat Dot(SoftVector3 vector1, SoftVector3 vector2)
	{
		return vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
	}

	/// <summary>
	/// Returns a dot product of two vectors.
	/// </summary>
	/// <param name="vector1">The first vector.</param>
	/// <param name="vector2">The second vector.</param>
	/// <param name="result">The dot product of two vectors as an output parameter.</param>
	public static void Dot(ref SoftVector3 vector1, ref SoftVector3 vector2, out sfloat result)
	{
		result = (
			(vector1.X * vector2.X) +
			(vector1.Y * vector2.Y) +
			(vector1.Z * vector2.Z)
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains hermite spline interpolation.
	/// </summary>
	/// <param name="value1">The first position vector.</param>
	/// <param name="tangent1">The first tangent vector.</param>
	/// <param name="value2">The second position vector.</param>
	/// <param name="tangent2">The second tangent vector.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>The hermite spline interpolation vector.</returns>
	public static SoftVector3 Hermite(
		SoftVector3 value1,
		SoftVector3 tangent1,
		SoftVector3 value2,
		SoftVector3 tangent2,
		sfloat amount
	) {
		SoftVector3 result = new SoftVector3();
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
	public static sfloat Hermite(
		sfloat value1,
		sfloat tangent1,
		sfloat value2,
		sfloat tangent2,
		sfloat amount
	) {
		/* All transformed to double not to lose precision
		 * Otherwise, for high numbers of param:amount the result is NaN instead
		 * of Infinity.
		 */
		sfloat v1 = value1, v2 = value2, t1 = tangent1, t2 = tangent2, s = amount;
		sfloat result;
		sfloat sCubed = s * s * s;
		sfloat sSquared = s * s;

		if (sfloat.WithinEpsilon(amount, (sfloat)0f))
		{
			result = value1;
		}
		else if (sfloat.WithinEpsilon(amount, (sfloat)1f))
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

		return (sfloat) result;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains hermite spline interpolation.
	/// </summary>
	/// <param name="value1">The first position vector.</param>
	/// <param name="tangent1">The first tangent vector.</param>
	/// <param name="value2">The second position vector.</param>
	/// <param name="tangent2">The second tangent vector.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <param name="result">The hermite spline interpolation vector as an output parameter.</param>
	public static void Hermite(
		ref SoftVector3 value1,
		ref SoftVector3 tangent1,
		ref SoftVector3 value2,
		ref SoftVector3 tangent2,
		sfloat amount,
		out SoftVector3 result
	) {
		result.X = Hermite(value1.X, tangent1.X, value2.X, tangent2.X, amount);
		result.Y = Hermite(value1.Y, tangent1.Y, value2.Y, tangent2.Y, amount);
		result.Z = Hermite(value1.Z, tangent1.Z, value2.Z, tangent2.Z, amount);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains linear interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="amount">Weighting value(between 0.0 and 1.0).</param>
	/// <returns>The result of linear interpolation of the specified vectors.</returns>
	public static SoftVector3 Lerp(SoftVector3 value1, SoftVector3 value2, sfloat amount)
	{
		return new SoftVector3(
			sfloat.Lerp(value1.X, value2.X, amount),
			sfloat.Lerp(value1.Y, value2.Y, amount),
			sfloat.Lerp(value1.Z, value2.Z, amount)
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains linear interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="amount">Weighting value(between 0.0 and 1.0).</param>
	/// <param name="result">The result of linear interpolation of the specified vectors as an output parameter.</param>
	public static void Lerp(
		ref SoftVector3 value1,
		ref SoftVector3 value2,
		sfloat amount,
		out SoftVector3 result
	) {
		result.X = sfloat.Lerp(value1.X, value2.X, amount);
		result.Y = sfloat.Lerp(value1.Y, value2.Y, amount);
		result.Z = sfloat.Lerp(value1.Z, value2.Z, amount);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a maximal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <returns>The <see cref="SoftVector3"/> with maximal values from the two vectors.</returns>
	public static SoftVector3 Max(SoftVector3 value1, SoftVector3 value2)
	{
		return new SoftVector3(
			sfloat.Max(value1.X, value2.X),
			sfloat.Max(value1.Y, value2.Y),
			sfloat.Max(value1.Z, value2.Z)
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a maximal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="result">The <see cref="SoftVector3"/> with maximal values from the two vectors as an output parameter.</param>
	public static void Max(ref SoftVector3 value1, ref SoftVector3 value2, out SoftVector3 result)
	{
		result.X = sfloat.Max(value1.X, value2.X);
		result.Y = sfloat.Max(value1.Y, value2.Y);
		result.Z = sfloat.Max(value1.Z, value2.Z);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a minimal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <returns>The <see cref="SoftVector3"/> with minimal values from the two vectors.</returns>
	public static SoftVector3 Min(SoftVector3 value1, SoftVector3 value2)
	{
		return new SoftVector3(
			sfloat.Min(value1.X, value2.X),
			sfloat.Min(value1.Y, value2.Y),
			sfloat.Min(value1.Z, value2.Z)
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a minimal values from the two vectors.
	/// </summary>
	/// <param name="value1">The first vector.</param>
	/// <param name="value2">The second vector.</param>
	/// <param name="result">The <see cref="SoftVector3"/> with minimal values from the two vectors as an output parameter.</param>
	public static void Min(ref SoftVector3 value1, ref SoftVector3 value2, out SoftVector3 result)
	{
		result.X = sfloat.Min(value1.X, value2.X);
		result.Y = sfloat.Min(value1.Y, value2.Y);
		result.Z = sfloat.Min(value1.Z, value2.Z);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a multiplication of two vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/>.</param>
	/// <returns>The result of the vector multiplication.</returns>
	public static SoftVector3 Multiply(SoftVector3 value1, SoftVector3 value2)
	{
		value1.X *= value2.X;
		value1.Y *= value2.Y;
		value1.Z *= value2.Z;
		return value1;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a multiplication of <see cref="SoftVector3"/> and a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="scaleFactor">Scalar value.</param>
	/// <returns>The result of the vector multiplication with a scalar.</returns>
	public static SoftVector3 Multiply(SoftVector3 value1, sfloat scaleFactor)
	{
		value1.X *= scaleFactor;
		value1.Y *= scaleFactor;
		value1.Z *= scaleFactor;
		return value1;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a multiplication of <see cref="SoftVector3"/> and a scalar.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="scaleFactor">Scalar value.</param>
	/// <param name="result">The result of the multiplication with a scalar as an output parameter.</param>
	public static void Multiply(ref SoftVector3 value1, sfloat scaleFactor, out SoftVector3 result)
	{
		result.X = value1.X * scaleFactor;
		result.Y = value1.Y * scaleFactor;
		result.Z = value1.Z * scaleFactor;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a multiplication of two vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/>.</param>
	/// <param name="result">The result of the vector multiplication as an output parameter.</param>
	public static void Multiply(ref SoftVector3 value1, ref SoftVector3 value2, out SoftVector3 result)
	{
		result.X = value1.X * value2.X;
		result.Y = value1.Y * value2.Y;
		result.Z = value1.Z * value2.Z;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains the specified vector inversion.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/>.</param>
	/// <returns>The result of the vector inversion.</returns>
	public static SoftVector3 Negate(SoftVector3 value)
	{
		value = new SoftVector3(-value.X, -value.Y, -value.Z);
		return value;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains the specified vector inversion.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/>.</param>
	/// <param name="result">The result of the vector inversion as an output parameter.</param>
	public static void Negate(ref SoftVector3 value, out SoftVector3 result)
	{
		result.X = -value.X;
		result.Y = -value.Y;
		result.Z = -value.Z;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a normalized values from another vector.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/>.</param>
	/// <returns>Unit vector.</returns>
	public static SoftVector3 Normalize(SoftVector3 value)
	{
		sfloat factor = (sfloat)1.0f / (sfloat) sfloat.Sqrt(
			(value.X * value.X) +
			(value.Y * value.Y) +
			(value.Z * value.Z)
		);
		return new SoftVector3(
			value.X * factor,
			value.Y * factor,
			value.Z * factor
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a normalized values from another vector.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/>.</param>
	/// <param name="result">Unit vector as an output parameter.</param>
	public static void Normalize(ref SoftVector3 value, out SoftVector3 result)
	{
		sfloat factor = (sfloat)1.0f / (sfloat) sfloat.Sqrt(
			(value.X * value.X) +
			(value.Y * value.Y) +
			(value.Z * value.Z)
		);
		result.X = value.X * factor;
		result.Y = value.Y * factor;
		result.Z = value.Z * factor;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains reflect vector of the given vector and normal.
	/// </summary>
	/// <param name="vector">Source <see cref="SoftVector3"/>.</param>
	/// <param name="normal">Reflection normal.</param>
	/// <returns>Reflected vector.</returns>
	public static SoftVector3 Reflect(SoftVector3 vector, SoftVector3 normal)
	{
		/* I is the original array.
		 * N is the normal of the incident plane.
		 * R = I - (2 * N * ( DotProduct[ I,N] ))
		 */
		SoftVector3 reflectedVector;
		// Inline the dotProduct here instead of calling method
		sfloat dotProduct = ((vector.X * normal.X) + (vector.Y * normal.Y)) +
					(vector.Z * normal.Z);
		reflectedVector.X = vector.X - ((sfloat)2.0f * normal.X) * dotProduct;
		reflectedVector.Y = vector.Y - ((sfloat)2.0f * normal.Y) * dotProduct;
		reflectedVector.Z = vector.Z - ((sfloat)2.0f * normal.Z) * dotProduct;

		return reflectedVector;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains reflect vector of the given vector and normal.
	/// </summary>
	/// <param name="vector">Source <see cref="SoftVector3"/>.</param>
	/// <param name="normal">Reflection normal.</param>
	/// <param name="result">Reflected vector as an output parameter.</param>
	public static void Reflect(ref SoftVector3 vector, ref SoftVector3 normal, out SoftVector3 result)
	{
		/* I is the original array.
		 * N is the normal of the incident plane.
		 * R = I - (2 * N * ( DotProduct[ I,N] ))
		 */

		// Inline the dotProduct here instead of calling method.
		sfloat dotProduct = ((vector.X * normal.X) + (vector.Y * normal.Y)) +
					(vector.Z * normal.Z);
		result.X = vector.X - ((sfloat)2.0f * normal.X) * dotProduct;
		result.Y = vector.Y - ((sfloat)2.0f * normal.Y) * dotProduct;
		result.Z = vector.Z - ((sfloat)2.0f * normal.Z) * dotProduct;

	}

	/// <summary>
	/// Interpolates between two values using a cubic equation.
	/// </summary>
	/// <param name="value1">Source value.</param>
	/// <param name="value2">Source value.</param>
	/// <param name="amount">Weighting value.</param>
	/// <returns>Interpolated value.</returns>
	private static sfloat SmoothStep(sfloat value1, sfloat value2, sfloat amount)
	{
		/* It is expected that 0 < amount < 1.
		 * If amount < 0, return value1.
		 * If amount > 1, return value2.
		 */
		sfloat result = sfloat.Clamp(amount, (sfloat)0f, (sfloat)1f);
		result = Hermite(value1, (sfloat)0f, value2, (sfloat)0f, result);

		return result;
	}
	
	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains cubic interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/>.</param>
	/// <param name="amount">Weighting value.</param>
	/// <returns>Cubic interpolation of the specified vectors.</returns>
	public static SoftVector3 SmoothStep(SoftVector3 value1, SoftVector3 value2, sfloat amount)
	{
		return new SoftVector3(
			SmoothStep(value1.X, value2.X, amount),
			SmoothStep(value1.Y, value2.Y, amount),
			SmoothStep(value1.Z, value2.Z, amount)
		);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains cubic interpolation of the specified vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/>.</param>
	/// <param name="amount">Weighting value.</param>
	/// <param name="result">Cubic interpolation of the specified vectors as an output parameter.</param>
	public static void SmoothStep(
		ref SoftVector3 value1,
		ref SoftVector3 value2,
		sfloat amount,
		out SoftVector3 result
	) {
		result.X = SmoothStep(value1.X, value2.X, amount);
		result.Y = SmoothStep(value1.Y, value2.Y, amount);
		result.Z = SmoothStep(value1.Z, value2.Z, amount);
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains subtraction of on <see cref="SoftVector3"/> from a another.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/>.</param>
	/// <returns>The result of the vector subtraction.</returns>
	public static SoftVector3 Subtract(SoftVector3 value1, SoftVector3 value2)
	{
		value1.X -= value2.X;
		value1.Y -= value2.Y;
		value1.Z -= value2.Z;
		return value1;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains subtraction of on <see cref="SoftVector3"/> from a another.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/>.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/>.</param>
	/// <param name="result">The result of the vector subtraction as an output parameter.</param>
	public static void Subtract(ref SoftVector3 value1, ref SoftVector3 value2, out SoftVector3 result)
	{
		result.X = value1.X - value2.X;
		result.Y = value1.Y - value2.Y;
		result.Z = value1.Z - value2.Z;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a transformation of 3d-vector by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="position">Source <see cref="SoftVector3"/>.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <returns>Transformed <see cref="SoftVector3"/>.</returns>
	public static SoftVector3 Transform(SoftVector3 position, Matrix matrix)
	{
		Transform(ref position, ref matrix, out position);
		return position;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a transformation of 3d-vector by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="position">Source <see cref="SoftVector3"/>.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="result">Transformed <see cref="SoftVector3"/> as an output parameter.</param>
	public static void Transform(
		ref SoftVector3 position,
		ref Matrix matrix,
		out SoftVector3 result
	) {
		sfloat x = (
			(position.X * (sfloat)matrix.M11) +
			(position.Y * (sfloat)matrix.M21) +
			(position.Z * (sfloat)matrix.M31) +
			(sfloat)matrix.M41
		);
		sfloat y = (
			(position.X * (sfloat)matrix.M12) +
			(position.Y * (sfloat)matrix.M22) +
			(position.Z * (sfloat)matrix.M32) +
			(sfloat)matrix.M42
		);
		sfloat z = (
			(position.X * (sfloat)matrix.M13) +
			(position.Y * (sfloat)matrix.M23) +
			(position.Z * (sfloat)matrix.M33) +
			(sfloat)matrix.M43
		);
		result.X = x;
		result.Y = y;
		result.Z = z;
	}

	/// <summary>
	/// Apply transformation on all vectors within array of <see cref="SoftVector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	public static void Transform(
		SoftVector3[] sourceArray,
		ref Matrix matrix,
		SoftVector3[] destinationArray
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
			SoftVector3 position = sourceArray[i];
			destinationArray[i] = new SoftVector3(
				(position.X * (sfloat)matrix.M11) + (position.Y * (sfloat)matrix.M21) +
					(position.Z * (sfloat)matrix.M31) + (sfloat)matrix.M41,
				(position.X * (sfloat)matrix.M12) + (position.Y * (sfloat)matrix.M22) +
					(position.Z * (sfloat)matrix.M32) + (sfloat)matrix.M42,
				(position.X * (sfloat)matrix.M13) + (position.Y * (sfloat)matrix.M23) +
					(position.Z * (sfloat)matrix.M33) + (sfloat)matrix.M43
			);
		}
	}

	/// <summary>
	/// Apply transformation on vectors within array of <see cref="SoftVector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="sourceIndex">The starting index of transformation in the source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	/// <param name="destinationIndex">The starting index in the destination array, where the first <see cref="SoftVector3"/> should be written.</param>
	/// <param name="length">The number of vectors to be transformed.</param>
	public static void Transform(
		SoftVector3[] sourceArray,
		int sourceIndex,
		ref Matrix matrix,
		SoftVector3[] destinationArray,
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
			SoftVector3 position = sourceArray[sourceIndex + i];
			destinationArray[destinationIndex + i] = new SoftVector3(
				(position.X * (sfloat)matrix.M11) + (position.Y * (sfloat)matrix.M21) +
					(position.Z * (sfloat)matrix.M31) + (sfloat)matrix.M41,
				(position.X * (sfloat)matrix.M12) + (position.Y * (sfloat)matrix.M22) +
					(position.Z * (sfloat)matrix.M32) + (sfloat)matrix.M42,
				(position.X * (sfloat)matrix.M13) + (position.Y * (sfloat)matrix.M23) +
					(position.Z * (sfloat)matrix.M33) + (sfloat)matrix.M43
			);
		}
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a transformation of 3d-vector by the specified <see cref="Quaternion"/>, representing the rotation.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/>.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <returns>Transformed <see cref="SoftVector3"/>.</returns>
	public static SoftVector3 Transform(SoftVector3 value, Quaternion rotation)
	{
		SoftVector3 result;
		Transform(ref value, ref rotation, out result);
		return result;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a transformation of 3d-vector by the specified <see cref="Quaternion"/>, representing the rotation.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/>.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <param name="result">Transformed <see cref="SoftVector3"/> as an output parameter.</param>
	public static void Transform(
		ref SoftVector3 value,
		ref Quaternion rotation,
		out SoftVector3 result
	) {
		sfloat x = (sfloat)2f * ((sfloat)rotation.Y * value.Z - (sfloat)rotation.Z * value.Y);
		sfloat y = (sfloat)2f * ((sfloat)rotation.Z * value.X - (sfloat)rotation.X * value.Z);
		sfloat z = (sfloat)2f * ((sfloat)rotation.X * value.Y - (sfloat)rotation.Y * value.X);

		result.X = value.X + x * (sfloat)rotation.W + ((sfloat)rotation.Y * z - (sfloat)rotation.Z * y);
		result.Y = value.Y + y * (sfloat)rotation.W + ((sfloat)rotation.Z * x - (sfloat)rotation.X * z);
		result.Z = value.Z + z * (sfloat)rotation.W + ((sfloat)rotation.X * y - (sfloat)rotation.Y * x);
	}

	/// <summary>
	/// Apply transformation on all vectors within array of <see cref="SoftVector3"/> by the specified <see cref="Quaternion"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <param name="destinationArray">Destination array.</param>
	public static void Transform(
		SoftVector3[] sourceArray,
		ref Quaternion rotation,
		SoftVector3[] destinationArray
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
			SoftVector3 position = sourceArray[i];

			sfloat x = (sfloat)2f * ((sfloat)rotation.Y * position.Z - (sfloat)rotation.Z * position.Y);
			sfloat y = (sfloat)2f * ((sfloat)rotation.Z * position.X - (sfloat)rotation.X * position.Z);
			sfloat z = (sfloat)2f * ((sfloat)rotation.X * position.Y - (sfloat)rotation.Y * position.X);

			destinationArray[i] = new SoftVector3(
				position.X + x * (sfloat)rotation.W + ((sfloat)rotation.Y * z - (sfloat)rotation.Z * y),
				position.Y + y * (sfloat)rotation.W + ((sfloat)rotation.Z * x - (sfloat)rotation.X * z),
				position.Z + z * (sfloat)rotation.W + ((sfloat)rotation.X * y - (sfloat)rotation.Y * x)
			);
		}
	}

	/// <summary>

	/// Apply transformation on vectors within array of <see cref="SoftVector3"/> by the specified <see cref="Quaternion"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="sourceIndex">The starting index of transformation in the source array.</param>
	/// <param name="rotation">The <see cref="Quaternion"/> which contains rotation transformation.</param>
	/// <param name="destinationArray">Destination array.</param>
	/// <param name="destinationIndex">The starting index in the destination array, where the first <see cref="SoftVector3"/> should be written.</param>
	/// <param name="length">The number of vectors to be transformed.</param>
	public static void Transform(
		SoftVector3[] sourceArray,
		int sourceIndex,
		ref Quaternion rotation,
		SoftVector3[] destinationArray,
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
			SoftVector3 position = sourceArray[sourceIndex + i];

			sfloat x = 2 * ((sfloat)rotation.Y * position.Z - (sfloat)rotation.Z * position.Y);
			sfloat y = 2 * ((sfloat)rotation.Z * position.X - (sfloat)rotation.X * position.Z);
			sfloat z = 2 * ((sfloat)rotation.X * position.Y - (sfloat)rotation.Y * position.X);

			destinationArray[destinationIndex + i] = new SoftVector3(
				position.X + x * (sfloat)rotation.W + ((sfloat)rotation.Y * z - (sfloat)rotation.Z * y),
				position.Y + y * (sfloat)rotation.W + ((sfloat)rotation.Z * x - (sfloat)rotation.X * z),
				position.Z + z * (sfloat)rotation.W + ((sfloat)rotation.X * y - (sfloat)rotation.Y * x)
			);
		}
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a transformation of the specified normal by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="normal">Source <see cref="SoftVector3"/> which represents a normal vector.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <returns>Transformed normal.</returns>
	public static SoftVector3 TransformNormal(SoftVector3 normal, Matrix matrix)
	{
		TransformNormal(ref normal, ref matrix, out normal);
		return normal;
	}

	/// <summary>
	/// Creates a new <see cref="SoftVector3"/> that contains a transformation of the specified normal by the specified <see cref="Matrix"/>.
	/// </summary>
	/// <param name="normal">Source <see cref="SoftVector3"/> which represents a normal vector.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="result">Transformed normal as an output parameter.</param>
	public static void TransformNormal(
		ref SoftVector3 normal,
		ref Matrix matrix,
		out SoftVector3 result
	) {
		sfloat x = (normal.X * (sfloat)matrix.M11) + (normal.Y * (sfloat)matrix.M21) + (normal.Z * (sfloat)matrix.M31);
		sfloat y = (normal.X * (sfloat)matrix.M12) + (normal.Y * (sfloat)matrix.M22) + (normal.Z * (sfloat)matrix.M32);
		sfloat z = (normal.X * (sfloat)matrix.M13) + (normal.Y * (sfloat)matrix.M23) + (normal.Z * (sfloat)matrix.M33);
		result.X = x;
		result.Y = y;
		result.Z = z;
	}

	/// <summary>
	/// Apply transformation on all normals within array of <see cref="SoftVector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	public static void TransformNormal(
		SoftVector3[] sourceArray,
		ref Matrix matrix,
		SoftVector3[] destinationArray
	) {
		Debug.Assert(
			destinationArray.Length >= sourceArray.Length,
			"The destination array is smaller than the source array."
		);

		for (int i = 0; i < sourceArray.Length; i += 1)
		{
			SoftVector3 normal = sourceArray[i];
			destinationArray[i].X = (normal.X * (sfloat)matrix.M11) + (normal.Y * (sfloat)matrix.M21) + (normal.Z * (sfloat)matrix.M31);
			destinationArray[i].Y = (normal.X * (sfloat)matrix.M12) + (normal.Y * (sfloat)matrix.M22) + (normal.Z * (sfloat)matrix.M32);
			destinationArray[i].Z = (normal.X * (sfloat)matrix.M13) + (normal.Y * (sfloat)matrix.M23) + (normal.Z * (sfloat)matrix.M33);
		}
	}

	/// <summary>
	/// Apply transformation on normals within array of <see cref="SoftVector3"/> by the specified <see cref="Matrix"/> and places the results in an another array.
	/// </summary>
	/// <param name="sourceArray">Source array.</param>
	/// <param name="sourceIndex">The starting index of transformation in the source array.</param>
	/// <param name="matrix">The transformation <see cref="Matrix"/>.</param>
	/// <param name="destinationArray">Destination array.</param>
	/// <param name="destinationIndex">The starting index in the destination array, where the first <see cref="SoftVector3"/> should be written.</param>
	/// <param name="length">The number of normals to be transformed.</param>
	public static void TransformNormal(
		SoftVector3[] sourceArray,
		int sourceIndex,
		ref Matrix matrix,
		SoftVector3[] destinationArray,
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
			SoftVector3 normal = sourceArray[i + sourceIndex];
			destinationArray[i + destinationIndex].X = (
				(normal.X * (sfloat)matrix.M11) +
				(normal.Y * (sfloat)matrix.M21) +
				(normal.Z * (sfloat)matrix.M31)
			);
			destinationArray[i + destinationIndex].Y = (
				(normal.X * (sfloat)matrix.M12) +
				(normal.Y * (sfloat)matrix.M22) +
				(normal.Z * (sfloat)matrix.M32)
			);
			destinationArray[i + destinationIndex].Z = (
				(normal.X * (sfloat)matrix.M13) +
				(normal.Y * (sfloat)matrix.M23) +
				(normal.Z * (sfloat)matrix.M33)
			);
		}
	}

	#endregion

	#region Public Static Operators

	/// <summary>
	/// Compares whether two <see cref="SoftVector3"/> instances are equal.
	/// </summary>
	/// <param name="value1"><see cref="SoftVector3"/> instance on the left of the equal sign.</param>
	/// <param name="value2"><see cref="SoftVector3"/> instance on the right of the equal sign.</param>
	/// <returns><c>true</c> if the instances are equal; <c>false</c> otherwise.</returns>
	public static bool operator ==(SoftVector3 value1, SoftVector3 value2)
	{
		return (	value1.X == value2.X &&
				value1.Y == value2.Y &&
				value1.Z == value2.Z	);
	}

	/// <summary>
	/// Compares whether two <see cref="SoftVector3"/> instances are not equal.
	/// </summary>
	/// <param name="value1"><see cref="SoftVector3"/> instance on the left of the not equal sign.</param>
	/// <param name="value2"><see cref="SoftVector3"/> instance on the right of the not equal sign.</param>
	/// <returns><c>true</c> if the instances are not equal; <c>false</c> otherwise.</returns>
	public static bool operator !=(SoftVector3 value1, SoftVector3 value2)
	{
		return !(value1 == value2);
	}

	/// <summary>
	/// Adds two vectors.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/> on the left of the add sign.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/> on the right of the add sign.</param>
	/// <returns>Sum of the vectors.</returns>
	public static SoftVector3 operator +(SoftVector3 value1, SoftVector3 value2)
	{
		value1.X += value2.X;
		value1.Y += value2.Y;
		value1.Z += value2.Z;
		return value1;
	}

	/// <summary>
	/// Inverts values in the specified <see cref="SoftVector3"/>.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/> on the right of the sub sign.</param>
	/// <returns>Result of the inversion.</returns>
	public static SoftVector3 operator -(SoftVector3 value)
	{
		value = new SoftVector3(-value.X, -value.Y, -value.Z);
		return value;
	}

	/// <summary>
	/// Subtracts a <see cref="SoftVector3"/> from a <see cref="SoftVector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/> on the left of the sub sign.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/> on the right of the sub sign.</param>
	/// <returns>Result of the vector subtraction.</returns>
	public static SoftVector3 operator -(SoftVector3 value1, SoftVector3 value2)
	{
		value1.X -= value2.X;
		value1.Y -= value2.Y;
		value1.Z -= value2.Z;
		return value1;
	}

	/// <summary>
	/// Multiplies the components of two vectors by each other.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/> on the left of the mul sign.</param>
	/// <param name="value2">Source <see cref="SoftVector3"/> on the right of the mul sign.</param>
	/// <returns>Result of the vector multiplication.</returns>
	public static SoftVector3 operator *(SoftVector3 value1, SoftVector3 value2)
	{
		value1.X *= value2.X;
		value1.Y *= value2.Y;
		value1.Z *= value2.Z;
		return value1;
	}

	/// <summary>
	/// Multiplies the components of vector by a scalar.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/> on the left of the mul sign.</param>
	/// <param name="scaleFactor">Scalar value on the right of the mul sign.</param>
	/// <returns>Result of the vector multiplication with a scalar.</returns>
	public static SoftVector3 operator *(SoftVector3 value, sfloat scaleFactor)
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
	/// <param name="value">Source <see cref="SoftVector3"/> on the right of the mul sign.</param>
	/// <returns>Result of the vector multiplication with a scalar.</returns>
	public static SoftVector3 operator *(sfloat scaleFactor, SoftVector3 value)
	{
		value.X *= scaleFactor;
		value.Y *= scaleFactor;
		value.Z *= scaleFactor;
		return value;
	}

	/// <summary>
	/// Divides the components of a <see cref="SoftVector3"/> by the components of another <see cref="SoftVector3"/>.
	/// </summary>
	/// <param name="value1">Source <see cref="SoftVector3"/> on the left of the div sign.</param>
	/// <param name="value2">Divisor <see cref="SoftVector3"/> on the right of the div sign.</param>
	/// <returns>The result of dividing the vectors.</returns>
	public static SoftVector3 operator /(SoftVector3 value1, SoftVector3 value2)
	{
		value1.X /= value2.X;
		value1.Y /= value2.Y;
		value1.Z /= value2.Z;
		return value1;
	}

	/// <summary>
	/// Divides the components of a <see cref="SoftVector3"/> by a scalar.
	/// </summary>
	/// <param name="value">Source <see cref="SoftVector3"/> on the left of the div sign.</param>
	/// <param name="divider">Divisor scalar on the right of the div sign.</param>
	/// <returns>The result of dividing a vector by a scalar.</returns>
	public static SoftVector3 operator /(SoftVector3 value, sfloat divider)
	{
		value.X /= divider;
		value.Y /= divider;
		value.Z /= divider;
		return value;
	}

	#endregion
	
	public static explicit operator Vector3(SoftVector3 value)
		=> new((float)value.X, (float)value.Y, (float)value.Z);
	public static explicit operator SoftVector3(Vector3 value)
		=> new((sfloat)value.X, (sfloat)value.Y, (sfloat)value.Z);
}