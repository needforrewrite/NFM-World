namespace SoftFloat;

public partial struct sfloat
{
    public static sfloat DEG_TO_RAD { get; } = ((sfloat)Math.PI / (sfloat)180);
    public static sfloat RAD_TO_DEG { get; } = ((sfloat)180 / (sfloat)Math.PI);

    public static bool operator <(sfloat f1, int f2) => f1 < (sfloat)f2;
    public static bool operator >(sfloat f1, int f2) => f1 > (sfloat)f2;
    public static bool operator <=(sfloat f1, int f2) => f1 <= (sfloat)f2;
    public static bool operator >=(sfloat f1, int f2) => f1 >= (sfloat)f2;
    public static bool operator <(int f1, sfloat f2) => (sfloat)f1 < f2;
    public static bool operator >(int f1, sfloat f2) => (sfloat)f1 > f2;
    public static bool operator >=(int f1, sfloat f2) => (sfloat)f1 >= f2;
    public static bool operator <=(int f1, sfloat f2) => (sfloat)f1 <= f2;
    public static sfloat operator +(sfloat f1, int f2) => f1 + (sfloat)f2;
    public static sfloat operator +(int f1, sfloat f2) => (sfloat)f1 + f2;
    public static sfloat operator -(sfloat f1, int f2) => f1 - (sfloat)f2;
    public static sfloat operator -(int f1, sfloat f2) => (sfloat)f1 - f2;
    public static sfloat operator *(sfloat f1, int f2) => f1 * (sfloat)f2;
    public static sfloat operator *(int f1, sfloat f2) => (sfloat)f1 * f2;
    public static sfloat operator /(sfloat f1, int f2) => f1 / (sfloat)f2;
    public static sfloat operator /(int f1, sfloat f2) => (sfloat)f1 / f2;
    public static sfloat operator %(sfloat f1, int f2) => f1 % (sfloat)f2;
    public static sfloat operator %(int f1, sfloat f2) => (sfloat)f1 % f2;
    
    public static bool operator <(sfloat f1, long f2) => f1 < (sfloat)f2;
    public static bool operator >(sfloat f1, long f2) => f1 > (sfloat)f2;
    public static bool operator <=(sfloat f1, long f2) => f1 <= (sfloat)f2;
    public static bool operator >=(sfloat f1, long f2) => f1 >= (sfloat)f2;
    public static bool operator <(long f1, sfloat f2) => (sfloat)f1 < f2;
    public static bool operator >(long f1, sfloat f2) => (sfloat)f1 > f2;
    public static bool operator >=(long f1, sfloat f2) => (sfloat)f1 >= f2;
    public static bool operator <=(long f1, sfloat f2) => (sfloat)f1 <= f2;
    public static sfloat operator +(sfloat f1, long f2) => f1 + (sfloat)f2;
    public static sfloat operator +(long f1, sfloat f2) => (sfloat)f1 + f2;
    public static sfloat operator -(sfloat f1, long f2) => f1 - (sfloat)f2;
    public static sfloat operator -(long f1, sfloat f2) => (sfloat)f1 - f2;
    public static sfloat operator *(sfloat f1, long f2) => f1 * (sfloat)f2;
    public static sfloat operator *(long f1, sfloat f2) => (sfloat)f1 * f2;
    public static sfloat operator /(sfloat f1, long f2) => f1 / (sfloat)f2;
    public static sfloat operator /(long f1, sfloat f2) => (sfloat)f1 / f2;
    public static sfloat operator %(sfloat f1, long f2) => f1 % (sfloat)f2;
    public static sfloat operator %(long f1, sfloat f2) => (sfloat)f1 % f2;

    public static sfloat operator --(sfloat f) => f + MinusOne;
    public static sfloat operator ++(sfloat f) => f + One;

    public static sfloat Sqrt(sfloat f)
    {
        return libm.sqrtf(f);
    }

    public static sfloat Acos(sfloat f)
    {
        return libm.acosf(f);
    }

    public static sfloat Atan2(sfloat a, sfloat b)
    {
        return libm.atan2f(a, b);
    }

    public static sfloat Round(sfloat f)
    {
        return libm.roundf(f);
    }

    public static sfloat Sin(sfloat f)
    {
        return libm.sinf(f);
    }

    public static sfloat Cos(sfloat f)
    {
        return libm.cosf(f);
    }

    public static sfloat Floor(sfloat f)
    {
        return libm.floorf(f);
    }

    public static sfloat Ceil(sfloat f)
    {
        return libm.ceilf(f);
    }

    public static sfloat Hypot(sfloat a, sfloat b)
    {
        return libm.hypotf(a, b);
    }
}