using System.Numerics;
using System.Runtime.CompilerServices;
using NFMWorld.Lua;

namespace NFMWorldLibrary.Util;

[LuaVisible]
public partial record struct LuaVector2([property: LuaName] float X, [property: LuaName] float Y)
{
    public static implicit operator Vector2(LuaVector2 vec2) => new(vec2.X, vec2.Y);
    public static implicit operator LuaVector2(Vector2 vec2) => new(vec2.X, vec2.Y);

    public static LuaVector2 operator -(LuaVector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) - Unsafe.BitCast<LuaVector2, Vector2>(right));
    public static LuaVector2 operator +(LuaVector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) + Unsafe.BitCast<LuaVector2, Vector2>(right));
    public static LuaVector2 operator *(LuaVector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) * Unsafe.BitCast<LuaVector2, Vector2>(right));
    public static LuaVector2 operator /(LuaVector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) / Unsafe.BitCast<LuaVector2, Vector2>(right));

    public static LuaVector2 operator -(LuaVector2 left, Vector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) - right);
    public static LuaVector2 operator +(LuaVector2 left, Vector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) + right);
    public static LuaVector2 operator *(LuaVector2 left, Vector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) * right);
    public static LuaVector2 operator /(LuaVector2 left, Vector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(Unsafe.BitCast<LuaVector2, Vector2>(left) / right);
    
    public static LuaVector2 operator -(Vector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(left - Unsafe.BitCast<LuaVector2, Vector2>(right));
    public static LuaVector2 operator +(Vector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(left + Unsafe.BitCast<LuaVector2, Vector2>(right));
    public static LuaVector2 operator *(Vector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(left * Unsafe.BitCast<LuaVector2, Vector2>(right));
    public static LuaVector2 operator /(Vector2 left, LuaVector2 right) => Unsafe.BitCast<Vector2, LuaVector2>(left / Unsafe.BitCast<LuaVector2, Vector2>(right));
}

[LuaVisible]
public readonly partial record struct LuaVector3(
    [property: LuaName] float X,
    [property: LuaName] float Y,
    [property: LuaName] float Z)
{
    public static implicit operator Vector3(LuaVector3 vec3) => new(vec3.X, vec3.Y, vec3.Z);
    public static implicit operator LuaVector3(Vector3 vec3) => new(vec3.X, vec3.Y, vec3.Z);
    
    public static LuaVector3 operator -(LuaVector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) - Unsafe.BitCast<LuaVector3, Vector3>(right));
    public static LuaVector3 operator +(LuaVector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) + Unsafe.BitCast<LuaVector3, Vector3>(right));
    public static LuaVector3 operator *(LuaVector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) * Unsafe.BitCast<LuaVector3, Vector3>(right));
    public static LuaVector3 operator /(LuaVector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) / Unsafe.BitCast<LuaVector3, Vector3>(right));

    public static LuaVector3 operator -(LuaVector3 left, Vector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) - right);
    public static LuaVector3 operator +(LuaVector3 left, Vector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) + right);
    public static LuaVector3 operator *(LuaVector3 left, Vector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) * right);
    public static LuaVector3 operator /(LuaVector3 left, Vector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(Unsafe.BitCast<LuaVector3, Vector3>(left) / right);
    
    public static LuaVector3 operator -(Vector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(left - Unsafe.BitCast<LuaVector3, Vector3>(right));
    public static LuaVector3 operator +(Vector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(left + Unsafe.BitCast<LuaVector3, Vector3>(right));
    public static LuaVector3 operator *(Vector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(left * Unsafe.BitCast<LuaVector3, Vector3>(right));
    public static LuaVector3 operator /(Vector3 left, LuaVector3 right) => Unsafe.BitCast<Vector3, LuaVector3>(left / Unsafe.BitCast<LuaVector3, Vector3>(right));
}
