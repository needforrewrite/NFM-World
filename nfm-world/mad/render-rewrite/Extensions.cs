using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Stride.Core.Mathematics;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace NFMWorld.Mad;

public static class Extensions
{
    extension<T>(List<T> list)
    {
        public ref T GetValueRef(Index index)
        {
            return ref CollectionsMarshal.AsSpan(list)[index];
        }

        public ref T GetValueRef(int index)
        {
            return ref CollectionsMarshal.AsSpan(list)[index];
        }
    }

    extension(AngleSingle angle)
    {
        public static AngleSingle FromRadians(float radians) => Unsafe.As<float, AngleSingle>(ref radians);

        public static AngleSingle FromDegrees(float degrees)
            => Unsafe.BitCast<float, AngleSingle>(MathUtil.DegreesToRadians(degrees));
    }

    extension(Vector3 vector3)
    {
        public static Vector3 RotateAround(in Vector3 source, in Vector3 target, in Vector3 axis, AngleSingle angle)
            => Vector3.RotateAround(in source, in target, in axis, angle.Radians);
        
        public static Vector3 Abs(Vector3 vector) => new(MathF.Abs(vector.X), MathF.Abs(vector.Y), MathF.Abs(vector.Z));
        
        public static Vector3 FromSpan(ReadOnlySpan<float> span)
            => new(span[0], span[1], span[2]);

        public THREE.Vector3 ToTHREE() => new(vector3.X, vector3.Y, vector3.Z);
    }

    extension(THREE.Vector3 vector3)
    {
        public Vector3 ToStride() => new(vector3.X, vector3.Y, vector3.Z);
    }

    extension(THREE.Euler euler)
    {
        public Euler ToMaxine() =>
            new(
                -AngleSingle.FromRadians(euler.Y),
                AngleSingle.FromRadians(euler.X),
                AngleSingle.FromRadians(euler.Z)
            ); // TODO is this correct?
    }

    extension(Int3 int3)
    {
        public static Int3 FromSpan(ReadOnlySpan<int> span)
            => new(span[0], span[1], span[2]);
    }

    extension(Color3 color3)
    {
        public static Color3 FromSpan(ReadOnlySpan<short> span)
            => new(span[0], span[1], span[2]);
    }
}