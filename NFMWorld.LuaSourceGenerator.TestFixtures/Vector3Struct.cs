using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// A more complex struct for testing nested types and cross-type operations.
/// </summary>
[LuaVisible(Name = "Vec3")]
public struct Vector3Struct
{
    public float X;
    public float Y;
    public float Z;

    public static Vector3Struct Zero => new(0, 0, 0);
    public static Vector3Struct One => new(1, 1, 1);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);

    public Vector3Struct()
    {
        X = Y = Z = 0;
    }

    public Vector3Struct(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    // Cross product
    public static Vector3Struct Cross(Vector3Struct a, Vector3Struct b)
    {
        return new Vector3Struct(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );
    }

    public static float Dot(Vector3Struct a, Vector3Struct b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    public Vector3Struct Normalized()
    {
        var len = Length;
        if (len == 0) return Zero;
        return new Vector3Struct(X / len, Y / len, Z / len);
    }

    // Get XY as SampleStruct (Vec2)
    public SampleStruct ToVec2()
    {
        return new SampleStruct(X, Y);
    }

    // Create from Vec2 with Z component
    public static Vector3Struct FromVec2(SampleStruct v, float z)
    {
        return new Vector3Struct(v.X, v.Y, z);
    }

    public static Vector3Struct operator +(Vector3Struct a, Vector3Struct b)
    {
        return new Vector3Struct(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public static Vector3Struct operator -(Vector3Struct a, Vector3Struct b)
    {
        return new Vector3Struct(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    public static Vector3Struct operator *(Vector3Struct v, float scalar)
    {
        return new Vector3Struct(v.X * scalar, v.Y * scalar, v.Z * scalar);
    }

    public static Vector3Struct operator -(Vector3Struct v)
    {
        return new Vector3Struct(-v.X, -v.Y, -v.Z);
    }

    public override string ToString()
    {
        return $"Vec3({X}, {Y}, {Z})";
    }
}
