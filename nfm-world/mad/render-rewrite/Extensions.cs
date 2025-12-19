using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using Microsoft.Xna.Framework.Graphics;
using Stride.Core.Mathematics;
using Color = Microsoft.Xna.Framework.Color;

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

        public AngleSingle ToWrapped()
        {
            var x = angle;
            x.Wrap();
            return x;
        }
        public AngleSingle ToWrappedPositive()
        {
            var x = angle;
            x.WrapPositive();
            return x;
        }
    }

    extension(System.Numerics.Vector3 vector3)
    {
        public Vector3 ToXna() => new(vector3.X, vector3.Y, vector3.Z);
    }

    extension(Stride.Core.Mathematics.Vector3 vector3)
    {
        public static Stride.Core.Mathematics.Vector3 RotateAround(in Stride.Core.Mathematics.Vector3 source, in Stride.Core.Mathematics.Vector3 target, in Stride.Core.Mathematics.Vector3 axis, AngleSingle angle)
            => Stride.Core.Mathematics.Vector3.RotateAround(in source, in target, in axis, angle.Radians);
        
        public static Stride.Core.Mathematics.Vector3 Abs(Stride.Core.Mathematics.Vector3 vector) => new(MathF.Abs(vector.X), MathF.Abs(vector.Y), MathF.Abs(vector.Z));
        
        public static Stride.Core.Mathematics.Vector3 FromSpan(ReadOnlySpan<float> span)
            => new(span[0], span[1], span[2]);
        
        public Vector3 ToXna() => new(vector3.X, vector3.Y, vector3.Z);
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

        public Color3 Snap(Color3 snap)
        {
            var r = (short) (color3[0] + color3[0] * (snap[0] / 100.0F));
            if (r > 255) r = 255;
            if (r < 0) r = 0;
            var g = (short) (color3[1] + color3[1] * (snap[1] / 100.0F));
            if (g > 255) g = 255;
            if (g < 0) g = 0;
            var b = (short) (color3[2] + color3[2] * (snap[2] / 100.0F));
            if (b > 255) b = 255;
            if (b < 0) b = 0;

            return new Color3(r, g, b);
        }
        
        public Color ToXna()
            =>
                new(
                    (byte)Math.Clamp(color3.R, (short)0, (short)255),
                    (byte)Math.Clamp(color3.G, (short)0, (short)255),
                    (byte)Math.Clamp(color3.B, (short)0, (short)255)
                );

        public Vector3 ToXnaVector3()
            => new(color3.R / 255.0f, color3.G / 255.0f, color3.B / 255.0f);
        
        public Stride.Core.Mathematics.Vector3 ToVector3()
            => new(color3.R / 255.0f, color3.G / 255.0f, color3.B / 255.0f);
    }

    extension(Matrix matrix)
    {
        public static Matrix CreateFromEuler(Euler euler)
        {
            // NFM rotation order: yaw-pitch-roll

            Span<float> te = [
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            ];

            float x = euler.Pitch.Radians, y = -euler.Yaw.Radians, z = euler.Roll.Radians;
            float a = MathF.Cos(x), b = MathF.Sin(x);
            float c = MathF.Cos(y), d = MathF.Sin(y);
            float e = MathF.Cos(z), f = MathF.Sin(z);

            {

                float ce = c * e, cf = c * f, de = d * e, df = d * f;

                te[0] = ce + df * b;
                te[4] = de * b - cf;
                te[8] = a * d;

                te[1] = a * f;
                te[5] = a * e;
                te[9] = -b;

                te[2] = cf * b - de;
                te[6] = df + ce * b;
                te[10] = a * c;

            }

            // bottom row
            te[3] = 0;
            te[7] = 0;
            te[11] = 0;

            // last column
            te[12] = 0;
            te[13] = 0;
            te[14] = 0;
            te[15] = 1;

            return new Matrix(
                te[0],
                te[1],
                te[2],
                te[3],
                te[4],
                te[5],
                te[6],
                te[7],
                te[8],
                te[9],
                te[10],
                te[11],
                te[12],
                te[13],
                te[14],
                te[15]
            );
        }
    }

    extension(Vector2 vector2)
    {
        public Vector2 ToXna()
            => new(vector2.X, vector2.Y);
    }

    extension(Stride.Core.Mathematics.Quaternion quat)
    {
        public Quaternion ToXna()
            => new(quat.X, quat.Y, quat.Z, quat.W);
    }

    extension<T>(Span2D<T> span2D)
    {
        public static Span2D<T> Create(Span<T> span, int height, int width)
        {
            if (height * width != span.Length)
                throw new ArgumentException("Span length does not match the provided dimensions.");
            
            return Span2D<T>.DangerousCreate(ref span[0], height, width, 0);
        }
    }
}

public static class Extensions2
{
    extension(Stride.Core.Mathematics.Matrix matrix)
    {
        public static Stride.Core.Mathematics.Matrix CreateFromEuler(Euler euler)
        {
            // NFM rotation order: yaw-pitch-roll

            Span<float> te = [
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            ];

            float x = euler.Pitch.Radians, y = -euler.Yaw.Radians, z = euler.Roll.Radians;
            float a = MathF.Cos(x), b = MathF.Sin(x);
            float c = MathF.Cos(y), d = MathF.Sin(y);
            float e = MathF.Cos(z), f = MathF.Sin(z);

            {

                float ce = c * e, cf = c * f, de = d * e, df = d * f;

                te[0] = ce + df * b;
                te[4] = de * b - cf;
                te[8] = a * d;

                te[1] = a * f;
                te[5] = a * e;
                te[9] = -b;

                te[2] = cf * b - de;
                te[6] = df + ce * b;
                te[10] = a * c;

            }

            // bottom row
            te[3] = 0;
            te[7] = 0;
            te[11] = 0;

            // last column
            te[12] = 0;
            te[13] = 0;
            te[14] = 0;
            te[15] = 1;

            return new Stride.Core.Mathematics.Matrix(
                te[0],
                te[1],
                te[2],
                te[3],
                te[4],
                te[5],
                te[6],
                te[7],
                te[8],
                te[9],
                te[10],
                te[11],
                te[12],
                te[13],
                te[14],
                te[15]
            );
        }
    }

    extension(Vector3 vector3)
    {
        public Span<float> AsSpan()
            => MemoryMarshal.CreateSpan(ref vector3.X, 3);
        
        public static Vector3 FromSpan(ReadOnlySpan<float> span)
            => new(span[0], span[1], span[2]);

        public static Vector3 RotateAroundPivot(in Vector3 point, in Vector3 pivot, in Matrix matrix)
        {
            var dir = point - pivot; // get point direction relative to pivot
            dir = Vector3.Transform(dir, matrix); // rotate it
            return dir + pivot; // calculate rotated point
        }
    }
}