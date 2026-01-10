using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// A sample struct to test Lua bindings for value types.
/// Structs should have copy-on-read semantics for value type behavior.
/// </summary>
[LuaVisible(Name = "Vec2")]
public struct SampleStruct
{
    // Fields
    public float X;
    public float Y;

    // Static property
    public static SampleStruct Zero => new(0, 0);
    public static SampleStruct One => new(1, 1);
    public static SampleStruct UnitX => new(1, 0);
    public static SampleStruct UnitY => new(0, 1);

    // Instance property (computed)
    public float Length => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared => X * X + Y * Y;

    // Parameterless constructor (default)
    public SampleStruct()
    {
        X = 0;
        Y = 0;
    }

    // Parameterized constructor
    public SampleStruct(float x, float y)
    {
        X = x;
        Y = y;
    }

    // Static method
    public static float Distance(SampleStruct a, SampleStruct b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Static method to create from angle
    public static SampleStruct FromAngle(float radians)
    {
        return new SampleStruct(MathF.Cos(radians), MathF.Sin(radians));
    }

    // Static method: dot product
    public static float Dot(SampleStruct a, SampleStruct b)
    {
        return a.X * b.X + a.Y * b.Y;
    }

    // Instance method
    public SampleStruct Normalized()
    {
        var len = Length;
        if (len == 0) return Zero;
        return new SampleStruct(X / len, Y / len);
    }

    // Instance method that would mutate (but returns new for struct)
    public SampleStruct Scale(float factor)
    {
        return new SampleStruct(X * factor, Y * factor);
    }

    // Instance method: set components (demonstrates struct mutation)
    public void Set(float x, float y)
    {
        X = x;
        Y = y;
    }

    // Operator overloads
    public static SampleStruct operator +(SampleStruct a, SampleStruct b)
    {
        return new SampleStruct(a.X + b.X, a.Y + b.Y);
    }

    public static SampleStruct operator -(SampleStruct a, SampleStruct b)
    {
        return new SampleStruct(a.X - b.X, a.Y - b.Y);
    }

    public static SampleStruct operator *(SampleStruct v, float scalar)
    {
        return new SampleStruct(v.X * scalar, v.Y * scalar);
    }

    public static SampleStruct operator /(SampleStruct v, float scalar)
    {
        return new SampleStruct(v.X / scalar, v.Y / scalar);
    }

    public static SampleStruct operator -(SampleStruct v)
    {
        return new SampleStruct(-v.X, -v.Y);
    }

    public static bool operator ==(SampleStruct a, SampleStruct b)
    {
        return a.X == b.X && a.Y == b.Y;
    }

    public static bool operator !=(SampleStruct a, SampleStruct b)
    {
        return !(a == b);
    }

    public override bool Equals(object? obj)
    {
        return obj is SampleStruct other && this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"Vec2({X}, {Y})";
    }
}
