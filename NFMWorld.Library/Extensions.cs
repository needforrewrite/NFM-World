using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FixedMathSharp.Utility;
using Maxine.Extensions.Mathematics;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;
using Steamworks;
using Steamworks.Data;

namespace NFMWorldLibrary;

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
        public fix64 DegreesSFloat => (fix64)angle.Radians * fix64.RadToDeg;

        public static AngleSingle FromRadians(float radians) => Unsafe.As<float, AngleSingle>(ref radians);

        public static AngleSingle FromDegrees(float degrees)
            => Unsafe.BitCast<float, AngleSingle>(MathUtil.DegreesToRadians(degrees));

        public static AngleSingle FromDegrees(int degrees)
            => Unsafe.BitCast<float, AngleSingle>((float)(degrees * fix64.DegToRad));

        public static AngleSingle FromDegrees(fix64 degrees)
            => Unsafe.BitCast<float, AngleSingle>((float)(degrees * fix64.DegToRad));
    }

    extension(System.Numerics.Vector3 vector3)
    {
        public Vector3 ToXna() => new(vector3.X, vector3.Y, vector3.Z);
    }

    extension(Maxine.Extensions.Mathematics.Vector3 vector3)
    {
        public static Maxine.Extensions.Mathematics.Vector3 RotateAround(in Maxine.Extensions.Mathematics.Vector3 source,
            in Maxine.Extensions.Mathematics.Vector3 target, in Maxine.Extensions.Mathematics.Vector3 axis, AngleSingle angle)
            => Maxine.Extensions.Mathematics.Vector3.RotateAround(in source, in target, in axis, angle.Radians);

        public static Maxine.Extensions.Mathematics.Vector3 Abs(Maxine.Extensions.Mathematics.Vector3 vector) =>
            new(MathF.Abs(vector.X), MathF.Abs(vector.Y), MathF.Abs(vector.Z));

        public static Maxine.Extensions.Mathematics.Vector3 FromSpan(ReadOnlySpan<float> span)
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
        public Color3 Snap(Color3 snap)
        {
            var r = (short)(color3[0] + color3[0] * (snap[0] / 100.0F));
            if (r > 255) r = 255;
            if (r < 0) r = 0;
            var g = (short)(color3[1] + color3[1] * (snap[1] / 100.0F));
            if (g > 255) g = 255;
            if (g < 0) g = 0;
            var b = (short)(color3[2] + color3[2] * (snap[2] / 100.0F));
            if (b > 255) b = 255;
            if (b < 0) b = 0;

            return new Color3(r, g, b);
        }
    }

    extension(Matrix matrix)
    {
        public static Matrix CreateFromEuler(Euler euler)
        {
            // NFM rotation order: yaw-pitch-roll

            Span<float> te =
            [
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

    extension(Maxine.Extensions.Mathematics.Quaternion quat)
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
    extension(Maxine.Extensions.Mathematics.Matrix matrix)
    {
        public static Maxine.Extensions.Mathematics.Matrix CreateFromEuler(Euler euler)
        {
            // NFM rotation order: yaw-pitch-roll

            Span<float> te =
            [
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

            return new Maxine.Extensions.Mathematics.Matrix(
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
    }

    extension(ref DeterministicRandom random)
    {
        public fix64 NextF64() => new(random.NextFixed6401());
        public fix64 NextF64(fix64 maxExclusive) => new(random.NextFixed64(maxExclusive.Value));

        public fix64 NextF64(fix64 minInclusive, fix64 maxExclusive) =>
            new(random.NextFixed64(minInclusive.Value, maxExclusive.Value));
    }

    private const double Factor = 0.7;

    extension(Color color)
    {
        public static Color GetHSBColor(float hue, float saturation, float brightness)
        {
            var v = Colors.HSBtoRGB(hue, saturation, brightness);
            return new Color(v.r, v.g, v.b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RGBtoHSB(int r, int g, int b, out float hue, out float saturation, out float brightness)
        {
            Colors.RGBtoHSB(r, g, b, out hue, out saturation, out brightness);
        }

        public Color Darker()
        {
            return new Color(
                Math.Max((int)(color.R * Factor), 0),
                Math.Max((int)(color.G * Factor), 0),
                Math.Max((int)(color.B * Factor), 0),
                color.A
            );
        }

        public Color Brighter()
        {
            var r = color.R;
            var g = color.G;
            var b = color.B;
            var alpha = color.A;

            /* From 2D group:
             * 1. black.brighter() should return grey
             * 2. applying brighter to blue will always return blue, brighter
             * 3. non pure color (non zero rgb) will eventually return white
             */
            const int i = (int)(1.0 / (1.0 - Factor));
            if (r == 0 && g == 0 && b == 0)
            {
                return new Color(i, i, i, alpha);
            }

            if (r is > 0 and < i)
            {
                r = i;
            }

            if (g is > 0 and < i)
            {
                g = i;
            }

            if (b is > 0 and < i)
            {
                b = i;
            }

            return new Color(Math.Min((int)(r / Factor), 255),
                Math.Min((int)(g / Factor), 255),
                Math.Min((int)(b / Factor), 255),
                alpha);
        }

        public int GetRGB()
        {
            var packed = 0;
            packed |= (byte)(color.B & 0xFF);
            packed |= (byte)((color.G & 0xFF) >> 8);
            packed |= (byte)((color.R & 0xFF) >> 16);
            packed |= (byte)((color.A & 0xFF) >> 24);
            return packed;
        }

        /// <summary>
        /// Restricts a value to be within a specified range.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="result">When the method completes, contains the clamped value.</param>
        public static void Clamp(ref readonly Color value, ref readonly Color min, ref readonly Color max,
            out Color result)
        {
            byte alpha = value.A;
            alpha = (alpha > max.A) ? max.A : alpha;
            alpha = (alpha < min.A) ? min.A : alpha;

            byte red = value.R;
            red = (red > max.R) ? max.R : red;
            red = (red < min.R) ? min.R : red;

            byte green = value.G;
            green = (green > max.G) ? max.G : green;
            green = (green < min.G) ? min.G : green;

            byte blue = value.B;
            blue = (blue > max.B) ? max.B : blue;
            blue = (blue < min.B) ? min.B : blue;

            result = new Color(red, green, blue, alpha);
        }

        /// <summary>
        /// Restricts a value to be within a specified range.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The clamped value.</returns>
        public static Color Clamp(Color value, Color min, Color max)
        {
            Clamp(ref value, ref min, ref max, out var result);
            return result;
        }

        /// <summary>
        /// Performs a linear interpolation between two colors.
        /// </summary>
        /// <param name="start">Start color.</param>
        /// <param name="end">End color.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <param name="result">When the method completes, contains the linear interpolation of the two colors.</param>
        /// <remarks>
        /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned.
        /// </remarks>
        public static void Lerp(ref readonly Color start, ref readonly Color end, float amount, out Color result)
        {
            result = new Color(
                MathUtil.Lerp(start.R, end.R, amount),
                MathUtil.Lerp(start.G, end.G, amount),
                MathUtil.Lerp(start.B, end.B, amount),
                MathUtil.Lerp(start.A, end.A, amount)
            );
        }

        /// <summary>
        /// Performs a linear interpolation between two colors.
        /// </summary>
        /// <param name="start">Start color.</param>
        /// <param name="end">End color.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <returns>The linear interpolation of the two colors.</returns>
        /// <remarks>
        /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned.
        /// </remarks>
        public static Color Lerp(Color start, Color end, float amount)
        {
            Lerp(ref start, ref end, amount, out var result);
            return result;
        }

        /// <summary>
        /// Performs a cubic interpolation between two colors.
        /// </summary>
        /// <param name="start">Start color.</param>
        /// <param name="end">End color.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <param name="result">When the method completes, contains the cubic interpolation of the two colors.</param>
        public static void SmoothStep(ref readonly Color start, ref readonly Color end, float amount, out Color result)
        {
            amount = MathUtil.SmoothStep(amount);
            Lerp(in start, in end, amount, out result);
        }

        /// <summary>
        /// Performs a cubic interpolation between two colors.
        /// </summary>
        /// <param name="start">Start color.</param>
        /// <param name="end">End color.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <returns>The cubic interpolation of the two colors.</returns>
        public static Color SmoothStep(Color start, Color end, float amount)
        {
            SmoothStep(ref start, ref end, amount, out var result);
            return result;
        }

        public void Deconstruct(out byte R, out byte G, out byte B, out byte A)
        {
            R = color.R;
            G = color.G;
            B = color.B;
            A = color.A;
        }
    }
    
    extension(Connection connection)
    {
        public unsafe Result SendMessage<T>(Span<T> data, SendType sendType = SendType.Reliable)
            where T : unmanaged
        {
            fixed (T* ptr = data)
                return connection.SendMessage((IntPtr) ptr, data.AsBytes().Length, sendType);
        }

        public unsafe Result SendMessage<T>(ReadOnlySpan<T> data, SendType sendType = SendType.Reliable)
            where T : unmanaged
        {
            fixed (T* ptr = data)
                return connection.SendMessage((IntPtr) ptr, data.AsBytes().Length, sendType);
        }
    }

    extension(Encoding encoding)
    {
        public unsafe string GetString(byte* bytes)
        {
            var length = 0;
            while (bytes[length] != 0) length++;
            return encoding.GetString(bytes, length);
        }
    }
}