using System.Runtime.CompilerServices;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Util;

public static class UMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SafeAbsUnsafe(int value)
    {
        return value == int.MinValue ? int.MaxValue : Math.Abs(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mod(int x, int m)
    {
        var r = x % m;
        return r < 0 ? r + m : r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinUnsafe(int deg)
    {
        return float.Sin(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CosUnsafe(int deg)
    {
        return float.Cos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (float Sin, float Cos) SinCos(int deg)
    {
        return float.SinCos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinUnsafe(float deg)
    {
        return float.Sin(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(AngleSingle angle)
    {
        return float.Sin(angle.Radians);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CosUnsafe(float deg)
    {
        return float.Cos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (float Sin, float Cos) SinCos(float deg)
    {
        return float.SinCos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(SinCosFloat deg)
    {
        return deg.Sin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(SinCosFloat deg)
    {
        return deg.Cos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(AngleSingle angle)
    {
        return float.Cos(angle.Radians);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (float Sin, float Cos) SinCos(SinCosFloat deg)
    {
        return (deg.Sin, deg.Cos);
    }

    public static bool RandomBoolean()
    {
        return URandom.Boolean();
    }

    private static readonly int[] Rand = [0, 0, 0];
    private static readonly bool[] Diup = [false, false, false];
    private static int _trn;
    private static int _cntrn;

    public static float Random()
    {
        if (_cntrn == 0)
        {
            for (var i = 0; i < 3; i++)
            {
                Rand[i] = (int)(10.0f * URandom.Single());
                Diup[i] = URandom.Boolean();
            }
            _cntrn = 20;
        }
        else
        {
            _cntrn--;
        }
        for (var i = 0; i < 3; i++)
        {
            if (Diup[i])
            {
                Rand[i]++;
                if (Rand[i] == 10)
                {
                    Rand[i] = 0;
                }
            }
            else
            {
                Rand[i]--;
                if (Rand[i] == -1)
                {
                    Rand[i] = 9;
                }
            }
        }

        _trn++;
        if (_trn == 3)
        {
            _trn = 0;
        }
        return Rand[_trn] / 10.0F;
    }

    public static void Rot(Span<int> a, Span<int> b, int offA, int offB, int angle, int len)
    {
        if (angle != 0)
        {
            var (sin, cos) = SinCos(angle);

            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var oa = (pa - offA);
                var ob = (pb - offB);
                a[i] = offA + (int)(oa * cos - ob * sin);
                b[i] = offB + (int)(oa * sin + ob * cos);
            }
        }
    }

    public static void Rot(Span<int> a, Span<int> b, int offA, int offB, float angle, int len)
    {
        if (angle != 0)
        {
            var (sin, cos) = SinCos(angle);

            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var oa = (pa - offA);
                var ob = (pb - offB);
                a[i] = offA + (int)(oa * cos - ob * sin);
                b[i] = offB + (int)(oa * sin + ob * cos);
            }
        }
    }

    public static void Rot(Span<int> a, Span<int> b, int offA, int offB, SinCosFloat angle, int len)
    {
        if (angle != 0)
        {
            var cos = angle.Cos;
            var sin = angle.Sin;

            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var oa = (pa - offA);
                var ob = (pb - offB);
                a[i] = offA + (int)(oa * cos - ob * sin);
                b[i] = offB + (int)(oa * sin + ob * cos);
            }
        }
    }

    public static void Rot(Span<float> a, Span<float> b, float offA, float offB, float angle, int len)
    {
        if (angle != 0)
        {
            var (sin, cos) = SinCos(angle);

            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var oa = (pa - offA);
                var ob = (pb - offB);
                a[i] = offA + (oa * cos - ob * sin);
                b[i] = offB + (oa * sin + ob * cos);
            }
        }
    }

    public static float InverseLerp(float a, float b, float value)
    {
        return (value - a) / (b - a);
    }

    public static int Py(int x1, int x2, int y1, int y2)
    {
        return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
    }

    public static fix64 Py(fix64 x1, fix64 x2, fix64 y1, fix64 y2)
    {
        return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
    }

    public static float Py(float x1, float x2, float y1, float y2)
    {
        return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static fix64 Sin(fix64 deg)
    {
        var sin = FixedTrigLUT.SinDeg(deg);
        return sin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static fix64 Cos(fix64 deg)
    {
        var cos = FixedTrigLUT.CosDeg(deg);
        return cos;
    }

    public static bool EqEpsilon(fix64 a, fix64 b)
    {
        var epsilon = (fix64)0.00001F;
        return fix64.Abs(a - b) < epsilon;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static fix64 Sin(int deg)
    {
        return Sin((fix64)deg);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static fix64 Sin(float deg)
    {
        return Sin((fix64)deg);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static fix64 Cos(int deg)
    {
        return Cos((fix64)deg);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static fix64 Cos(float deg)
    {
        return Cos((fix64)deg);
    }

    public static void Rot(Span<fix64> a, Span<fix64> b, fix64 offA, fix64 offB, fix64 angle, int len)
    {
        if (angle != 0)
        {
            FixedTrigLUT.SinCosDeg(angle, out var sin, out var cos);
            
            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var oa = (pa - offA);
                var ob = (pb - offB);
                a[i] = offA + (oa * cos - ob * sin);
                b[i] = offB + (oa * sin + ob * cos);
            }
        }
    }

    public static int SafeAbs(int value) => value >= 0 ? value : (value == int.MinValue ? int.MaxValue : -value);
    public static fix64 SafeAbs(fix64 value) => value >= 0 ? value : (value == fix64.MinValue ? fix64.MaxValue : -value);

    public static int Rpy(fix64 x1, fix64 x2, fix64 y1, fix64 y2, fix64 z1, fix64 z2)
    {
        return (int)((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2) + (z1 - z2) * (z1 - z2));
    }

    public static fix64 Hypot3(fix64 x, fix64 y, fix64 z)
    {
        return fix64.Sqrt(x * x + y * y + z * z);
    }

    public static fix64 dAcos(fix64 a)
    {
        return fix64.Acos(a) * fix64.RadToDeg;
    }

    public static fix64 dAtan2(fix64 y, fix64 x)
    {
        return fix64.Atan2(y, x) * fix64.RadToDeg;
    }

    public static fix64 QuantizeTowardsZero(fix64 value, fix64 step)
    {
        // Scale by step size
        fix64 scaled = value / step;

        // Truncate towards zero
        fix64 truncated = scaled > 0 ? fix64.Floor(scaled) : fix64.Ceiling(scaled);

        // Scale back
        return truncated * step;
    }
}