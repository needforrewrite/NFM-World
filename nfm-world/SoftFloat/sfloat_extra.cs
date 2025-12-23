namespace SoftFloat;

public partial struct sfloat
{
    public static sfloat Pi { get; } = FromRaw(libm.pi);
    public static sfloat HalfPi { get; } = FromRaw(libm.half_pi);
    public static sfloat TwoPi { get; } = FromRaw(libm.two_pi);
    public static sfloat PiOver4 { get; } = FromRaw(libm.pi_over_4);
    public static sfloat PiTimes3Over4 { get; } = FromRaw(libm.pi_times_3_over_4);
    
    public static sfloat DegToRad { get; } = Pi / (sfloat)180.0f;
    public static sfloat RadToDeg { get; } = (sfloat)180.0f / Pi;

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

    public static sfloat Clamp(sfloat value, sfloat min, sfloat max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    
    public static bool WithinEpsilon(sfloat floatA, sfloat floatB)
    {
        return Abs(floatA - floatB) < MachineEpsilonFloat;
    }
    
    private static readonly sfloat MachineEpsilonFloat = GetMachineEpsilonFloat();

    /// <summary>
    /// Find the current machine's Epsilon for the float data type.
    /// (That is, the largest float, e,  where e == 0.0f is true.)
    /// </summary>
    private static sfloat GetMachineEpsilonFloat()
    {
        sfloat machineEpsilon = (sfloat)1.0f;
        sfloat comparison;

        /* Keep halving the working value of machineEpsilon until we get a number that
         * when added to 1.0f will still evaluate as equal to 1.0f.
         */
        do
        {
            machineEpsilon *= (sfloat)0.5f;
            comparison = (sfloat)1.0f + machineEpsilon;
        }
        while (comparison > (sfloat)1.0f);

        return machineEpsilon;
    }
    /// <summary>
    /// Linearly interpolates between two values.
    /// </summary>
    /// <param name="value1">Source value.</param>
    /// <param name="value2">Source value.</param>
    /// <param name="amount">
    /// Value between 0 and 1 indicating the weight of value2.
    /// </param>
    /// <returns>Interpolated value.</returns>
    /// <remarks>
    /// This method performs the linear interpolation based on the following formula.
    /// <c>value1 + (value2 - value1) * amount</c>
    /// Passing amount a value of 0 will cause value1 to be returned, a value of 1 will
    /// cause value2 to be returned.
    /// </remarks>
    public static sfloat Lerp(sfloat value1, sfloat value2, sfloat amount)
    {
        return value1 + (value2 - value1) * amount;
    }
}