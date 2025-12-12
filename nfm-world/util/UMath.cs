using System.Runtime.CompilerServices;
using Stride.Core.Mathematics;
using URandom = NFMWorld.Util.Random;

namespace NFMWorld.Util;

public static class UMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SafeAbs(int value)
    {
        return value == int.MinValue ? int.MaxValue : Math.Abs(value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Mod(int x, int m) {
        var r = x % m;
        return r<0 ? r + m : r;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Sin(int deg)
    {
        return float.Sin(deg * ((float)Math.PI / 180));
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Cos(int deg)
    {
        return float.Cos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (float Sin, float Cos) SinCos(int deg)
    {
        return float.SinCos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Sin(float deg)
    {
        return float.Sin(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Sin(AngleSingle angle)
    {
        return float.Sin(angle.Radians);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Cos(float deg)
    {
        return float.Cos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (float Sin, float Cos) SinCos(float deg)
    {
        return float.SinCos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Sin(SinCosFloat deg)
    {
        return deg.Sin;
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Cos(SinCosFloat deg)
    {
        return deg.Cos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Cos(AngleSingle angle)
    {
        return float.Cos(angle.Radians);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (float Sin, float Cos) SinCos(SinCosFloat deg)
    {
        return (deg.Sin, deg.Cos);
    }
    
    internal static bool RandomBoolean()
    {
        return URandom.Boolean();
    }

    private static readonly int[] Rand = [0, 0, 0];
    private static readonly bool[] Diup = [false, false, false];
    private static int _trn;
    private static int _cntrn;

    internal static float Random()
    {
        if (_cntrn == 0)
        {
            for (var i = 0; i < 3; i++)
            {
                Rand[i] = (int) (10.0f * URandom.Single());
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
    
    internal static void Rot(Span<int> a, Span<int> b, int offA, int offB, int angle, int len)
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
                a[i] = offA + (int) (oa * cos - ob * sin);
                b[i] = offB + (int) (oa * sin + ob * cos);
            }
        }
    }

    internal static void Rot(Span<int> a, Span<int> b, int offA, int offB, float angle, int len)
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
                a[i] = offA + (int) (oa * cos - ob * sin);
                b[i] = offB + (int) (oa * sin + ob * cos);
            }
        }
    }

    internal static void Rot(Span<int> a, Span<int> b, int offA, int offB, SinCosFloat angle, int len)
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
                a[i] = offA + (int) (oa * cos - ob * sin);
                b[i] = offB + (int) (oa * sin + ob * cos);
            }
        }
    }
    
    internal static void Rot(Span<float> a, Span<float> b, float offA, float offB, float angle, int len)
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
}