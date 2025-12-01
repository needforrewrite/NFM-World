using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using NFMWorld.Mad;

namespace NFMWorld.Util;

public readonly record struct SinCosFloat : IFloatingPointIeee754<SinCosFloat>, IComparisonOperators<SinCosFloat, float, bool>, IComparisonOperators<SinCosFloat, int, bool>
{
    public readonly float Value;
    public readonly float Sin;
    public readonly float Cos;

    public SinCosFloat(float Value)
    {
        this.Value = Value;
        (Sin, Cos) = float.SinCos(Value * ((float)Math.PI / 180));
    }

    public static implicit operator float(SinCosFloat scf) => scf.Value;
    public static implicit operator SinCosFloat(float value) => new(value);
    
    public int CompareTo(object? obj)
    {
        return obj is SinCosFloat other ? CompareTo(other) : throw new ArgumentException("Object is not a SinCosFloat.");
    }

    public int CompareTo(SinCosFloat other)
    {
        return Value.CompareTo(other.Value);
    }

    public static SinCosFloat Pow(SinCosFloat x, SinCosFloat y)
    {
        return MathF.Pow(x.Value, y.Value);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return Value.ToString(format, formatProvider);
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        return Value.TryFormat(destination, out charsWritten, format, provider);
    }

    public static SinCosFloat Parse(string s, IFormatProvider? provider)
    {
        return float.Parse(s, provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SinCosFloat result)
    {
        if (float.TryParse(s, provider, out float value))
        {
            result = value;
            return true;
        }
        result = default;
        return false;
    }

    public static SinCosFloat Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return float.Parse(s, provider);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SinCosFloat result)
    {
        if (float.TryParse(s, provider, out float value))
        {
            result = value;
            return true;
        }
        result = default;
        return false;
    }

    public static SinCosFloat operator +(SinCosFloat left, SinCosFloat right)
    {
        return left.Value + right.Value;
    }

    public static SinCosFloat AdditiveIdentity => 0f;
    
    public static bool operator >(SinCosFloat left, SinCosFloat right)
    {
        return left.Value > right.Value;
    }

    public static bool operator >=(SinCosFloat left, SinCosFloat right)
    {
        return left.Value >= right.Value;
    }

    public static bool operator <(SinCosFloat left, SinCosFloat right)
    {
        return left.Value < right.Value;
    }

    public static bool operator <=(SinCosFloat left, SinCosFloat right)
    {
        return left.Value <= right.Value;
    }

    public static SinCosFloat operator --(SinCosFloat value)
    {
        return value.Value - 1f;
    }

    public static SinCosFloat operator /(SinCosFloat left, SinCosFloat right)
    {
        return left.Value / right.Value;
    }

    public static SinCosFloat operator ++(SinCosFloat value)
    {
        return value.Value + 1f;
    }

    public static SinCosFloat MultiplicativeIdentity => 1f;
    
    public static SinCosFloat operator *(SinCosFloat left, SinCosFloat right)
    {
        return left.Value * right.Value;
    }

    public static SinCosFloat operator -(SinCosFloat left, SinCosFloat right)
    {
        return left.Value - right.Value;
    }

    public static SinCosFloat operator -(SinCosFloat value)
    {
        return -value.Value;
    }

    public static SinCosFloat operator +(SinCosFloat value)
    {
        return +value.Value;
    }

    public static SinCosFloat Abs(SinCosFloat value)
    {
        return MathF.Abs(value.Value);
    }

    public static bool IsCanonical(SinCosFloat value)
    {
        return true;
    }

    public static bool IsComplexNumber(SinCosFloat value)
    {
        return false;
    }

    public static bool IsEvenInteger(SinCosFloat value)
    {
        return float.IsEvenInteger(value.Value);
    }

    public static bool IsFinite(SinCosFloat value)
    {
        return float.IsFinite(value.Value);
    }

    public static bool IsImaginaryNumber(SinCosFloat value)
    {
        return false;
    }

    public static bool IsInfinity(SinCosFloat value)
    {
        return float.IsInfinity(value.Value);
    }

    public static bool IsInteger(SinCosFloat value)
    {
        return float.IsInteger(value.Value);
    }

    public static bool IsNaN(SinCosFloat value)
    {
        return float.IsNaN(value.Value);
    }

    public static bool IsNegative(SinCosFloat value)
    {
        return float.IsNegative(value.Value);
    }

    public static bool IsNegativeInfinity(SinCosFloat value)
    {
        return float.IsNegativeInfinity(value.Value);
    }

    public static bool IsNormal(SinCosFloat value)
    {
        return float.IsNormal(value.Value);
    }

    public static bool IsOddInteger(SinCosFloat value)
    {
        return float.IsOddInteger(value.Value);
    }

    public static bool IsPositive(SinCosFloat value)
    {
        return float.IsPositive(value.Value);
    }

    public static bool IsPositiveInfinity(SinCosFloat value)
    {
        return float.IsPositiveInfinity(value.Value);
    }

    public static bool IsRealNumber(SinCosFloat value)
    {
        return float.IsRealNumber(value.Value);
    }

    public static bool IsSubnormal(SinCosFloat value)
    {
        return float.IsSubnormal(value.Value);
    }

    public static bool IsZero(SinCosFloat value)
    {
        return value.Value == 0;
    }

    public static SinCosFloat MaxMagnitude(SinCosFloat x, SinCosFloat y)
    {
        return float.MaxMagnitude(x.Value, y.Value);
    }

    public static SinCosFloat MaxMagnitudeNumber(SinCosFloat x, SinCosFloat y)
    {
        return float.MaxMagnitudeNumber(x.Value, y.Value);
    }

    public static SinCosFloat MinMagnitude(SinCosFloat x, SinCosFloat y)
    {
        return float.MinMagnitude(x.Value, y.Value);
    }

    public static SinCosFloat MinMagnitudeNumber(SinCosFloat x, SinCosFloat y)
    {
        return float.MinMagnitudeNumber(x.Value, y.Value);
    }

    public static SinCosFloat Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
    {
        return float.Parse(s, style, provider);
    }

    public static SinCosFloat Parse(string s, NumberStyles style, IFormatProvider? provider)
    {
        return float.Parse(s, style, provider);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out float result)
        where TOther : INumberBase<TOther>
    {
        // In order to reduce overall code duplication and improve the inlinabilty of these
        // methods for the corelib types we have `ConvertFrom` handle the same sign and
        // `ConvertTo` handle the opposite sign. However, since there is an uneven split
        // between signed and unsigned types, the one that handles unsigned will also
        // handle `Decimal`.
        //
        // That is, `ConvertFrom` for `float` will handle the other signed types and
        // `ConvertTo` will handle the unsigned types

        if (typeof(TOther) == typeof(double))
        {
            double actualValue = (double)(object)value;
            result = (float)actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(Half))
        {
            Half actualValue = (Half)(object)value;
            result = (float)actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(short))
        {
            short actualValue = (short)(object)value;
            result = actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(int))
        {
            int actualValue = (int)(object)value;
            result = actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(long))
        {
            long actualValue = (long)(object)value;
            result = actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(Int128))
        {
            Int128 actualValue = (Int128)(object)value;
            result = (float)actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(nint))
        {
            nint actualValue = (nint)(object)value;
            result = actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(sbyte))
        {
            sbyte actualValue = (sbyte)(object)value;
            result = actualValue;
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }
    public static bool TryConvertFromChecked<TOther>(TOther value, out SinCosFloat result) where TOther : INumberBase<TOther>
    {
        if (TryConvertFrom(value, out float floatResult))
        {
            result = floatResult;
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryConvertFromSaturating<TOther>(TOther value, out SinCosFloat result) where TOther : INumberBase<TOther>
    {
        if (TryConvertFrom(value, out float floatResult))
        {
            result = floatResult;
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryConvertFromTruncating<TOther>(TOther value, out SinCosFloat result) where TOther : INumberBase<TOther>
    {
        if (TryConvertFrom(value, out float floatResult))
        {
            result = floatResult;
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryConvertToChecked<TOther>(SinCosFloat value, [MaybeNullWhen(false)] out TOther result) where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(byte))
        {
            byte actualResult = checked((byte)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(char))
        {
            char actualResult = checked((char)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            decimal actualResult = checked((decimal)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(ushort))
        {
            ushort actualResult = checked((ushort)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(uint))
        {
            uint actualResult = checked((uint)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(ulong))
        {
            ulong actualResult = checked((ulong)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(UInt128))
        {
            UInt128 actualResult = checked((UInt128)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(nuint))
        {
            nuint actualResult = checked((nuint)(float)value);
            result = (TOther)(object)actualResult;
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertTo<TOther>(float value, [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        // In order to reduce overall code duplication and improve the inlinabilty of these
        // methods for the corelib types we have `ConvertFrom` handle the same sign and
        // `ConvertTo` handle the opposite sign. However, since there is an uneven split
        // between signed and unsigned types, the one that handles unsigned will also
        // handle `Decimal`.
        //
        // That is, `ConvertFrom` for `float` will handle the other signed types and
        // `ConvertTo` will handle the unsigned types.

        if (typeof(TOther) == typeof(byte))
        {
            var actualResult = (value >= byte.MaxValue) ? byte.MaxValue :
                               (value <= byte.MinValue) ? byte.MinValue : (byte)value;
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(char))
        {
            char actualResult = (value >= char.MaxValue) ? char.MaxValue :
                                (value <= char.MinValue) ? char.MinValue : (char)value;
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            decimal actualResult = (value >= +79228162514264337593543950336.0f) ? decimal.MaxValue :
                                   (value <= -79228162514264337593543950336.0f) ? decimal.MinValue :
                                   IsNaN(value) ? 0.0m : (decimal)value;
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(ushort))
        {
            ushort actualResult = (value >= ushort.MaxValue) ? ushort.MaxValue :
                                  (value <= ushort.MinValue) ? ushort.MinValue : (ushort)value;
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(uint))
        {
#if MONO
            uint actualResult = (value >= uint.MaxValue) ? uint.MaxValue :
                                (value <= uint.MinValue) ? uint.MinValue : (uint)value;
#else
            uint actualResult = (uint)value;
#endif
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(ulong))
        {
#if MONO
            ulong actualResult = (value >= ulong.MaxValue) ? ulong.MaxValue :
                                 (value <= ulong.MinValue) ? ulong.MinValue :
                                 IsNaN(value) ? 0 : (ulong)value;
#else
            ulong actualResult = (ulong)value;
#endif
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(UInt128))
        {
            UInt128 actualResult = (UInt128)value;
            result = (TOther)(object)actualResult;
            return true;
        }
        else if (typeof(TOther) == typeof(nuint))
        {
#if MONO
#if TARGET_64BIT
            nuint actualResult = (value >= ulong.MaxValue) ? unchecked((nuint)ulong.MaxValue) :
                                 (value <= ulong.MinValue) ? unchecked((nuint)ulong.MinValue) : (nuint)value;
#else
            nuint actualResult = (value >= uint.MaxValue) ? uint.MaxValue :
                                 (value <= uint.MinValue) ? uint.MinValue : (nuint)value;
#endif
#else
            nuint actualResult = (nuint)value;
#endif
            result = (TOther)(object)actualResult;
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }

    public static bool TryConvertToSaturating<TOther>(SinCosFloat value, [MaybeNullWhen(false)] out TOther result) where TOther : INumberBase<TOther>
    {
        return TryConvertTo(value.Value, out result);
    }

    public static bool TryConvertToTruncating<TOther>(SinCosFloat value, [MaybeNullWhen(false)] out TOther result) where TOther : INumberBase<TOther>
    {
        return TryConvertTo(value.Value, out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out SinCosFloat result)
    {
        if (float.TryParse(s, style, provider, out float value))
        {
            result = value;
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out SinCosFloat result)
    {
        if (float.TryParse(s, style, provider, out float value))
        {
            result = value;
            return true;
        }
        result = default;
        return false;
    }

    public static SinCosFloat One => 1f;
    public static int Radix => 2;
    public static SinCosFloat Zero => 0f;
    public static SinCosFloat E => MathF.E;
    public static SinCosFloat Pi => MathF.PI;
    public static SinCosFloat Tau => MathF.Tau;
    
    public static SinCosFloat Exp(SinCosFloat x)
    {
        return MathF.Exp(x.Value);
    }

    public static SinCosFloat Exp10(SinCosFloat x)
    {
        return float.Exp10(x.Value);
    }

    public static SinCosFloat Exp2(SinCosFloat x)
    {
        return float.Exp2(x.Value);
    }

    public static SinCosFloat operator %(SinCosFloat left, SinCosFloat right)
    {
        return left.Value % right.Value;
    }

    public static SinCosFloat NegativeOne => -1f;
    
    public int GetExponentByteCount()
    {
        return sizeof(sbyte);
    }

    public int GetExponentShortestBitLength()
    {
        sbyte exponent = Exponent;

        if (exponent >= 0)
        {
            return (sizeof(sbyte) * 8) - sbyte.LeadingZeroCount(exponent);
        }
        else
        {
            return (sizeof(sbyte) * 8) + 1 - sbyte.LeadingZeroCount((sbyte)(~exponent));
        }
    }

    public int GetSignificandBitLength()
    {
        return 24;
    }

    public int GetSignificandByteCount()
    {
        return sizeof(uint);
    }

    public static SinCosFloat Round(SinCosFloat x, int digits, MidpointRounding mode)
    {
        return MathF.Round(x.Value, digits, mode);
    }

    internal byte BiasedExponent
    {
        get
        {
            uint bits = BitConverter.SingleToUInt32Bits(Value);
            return ExtractBiasedExponentFromBits(bits);
        }
    }

    internal const byte ExponentBias = 127;

    internal sbyte Exponent
    {
        get
        {
            return (sbyte)(BiasedExponent - ExponentBias);
        }
    }

    internal uint Significand
    {
        get
        {
            return TrailingSignificand | ((BiasedExponent != 0) ? (1U << BiasedExponentShift) : 0U);
        }
    }

    internal uint TrailingSignificand
    {
        get
        {
            uint bits = BitConverter.SingleToUInt32Bits(Value);
            return ExtractTrailingSignificandFromBits(bits);
        }
    }

    internal const uint BiasedExponentMask = 0x7F80_0000;
    internal const int BiasedExponentShift = 23;
    internal const int BiasedExponentLength = 8;
    internal const byte ShiftedBiasedExponentMask = (byte)(BiasedExponentMask >> BiasedExponentShift);

    internal const uint TrailingSignificandMask = 0x007F_FFFF;

    internal static byte ExtractBiasedExponentFromBits(uint bits)
    {
        return (byte)((bits >> BiasedExponentShift) & ShiftedBiasedExponentMask);
    }

    internal static uint ExtractTrailingSignificandFromBits(uint bits)
    {
        return bits & TrailingSignificandMask;
    }
    
    public bool TryWriteExponentBigEndian(Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length >= sizeof(sbyte))
        {
            destination[0] = (byte)Exponent;
            bytesWritten = sizeof(sbyte);
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    public bool TryWriteExponentLittleEndian(Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length >= sizeof(sbyte))
        {
            destination[0] = (byte)Exponent;
            bytesWritten = sizeof(sbyte);
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    public bool TryWriteSignificandBigEndian(Span<byte> destination, out int bytesWritten)
    {
        if (BinaryPrimitives.TryWriteUInt32BigEndian(destination, Significand))
        {
            bytesWritten = sizeof(uint);
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    public bool TryWriteSignificandLittleEndian(Span<byte> destination, out int bytesWritten)
    {
        if (BinaryPrimitives.TryWriteUInt32LittleEndian(destination, Significand))
        {
            bytesWritten = sizeof(uint);
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    public static SinCosFloat Acosh(SinCosFloat x)
    {
        return MathF.Acosh(x.Value);
    }

    public static SinCosFloat Asinh(SinCosFloat x)
    {
        return MathF.Asinh(x.Value);
    }

    public static SinCosFloat Atanh(SinCosFloat x)
    {
        return MathF.Atanh(x.Value);
    }

    public static SinCosFloat Cosh(SinCosFloat x)
    {
        return MathF.Cosh(x.Value);
    }

    public static SinCosFloat Sinh(SinCosFloat x)
    {
        return MathF.Sinh(x.Value);
    }

    public static SinCosFloat Tanh(SinCosFloat x)
    {
        return MathF.Tanh(x.Value);
    }

    public static SinCosFloat Log(SinCosFloat x)
    {
        return MathF.Log(x.Value);
    }

    public static SinCosFloat Log(SinCosFloat x, SinCosFloat newBase)
    {
        return MathF.Log(x.Value, newBase.Value);
    }

    public static SinCosFloat Log10(SinCosFloat x)
    {
        return MathF.Log10(x.Value);
    }

    public static SinCosFloat Log2(SinCosFloat x)
    {
        return MathF.Log2(x.Value);
    }

    public static SinCosFloat Cbrt(SinCosFloat x)
    {
        return MathF.Cbrt(x.Value);
    }

    public static SinCosFloat Hypot(SinCosFloat x, SinCosFloat y)
    {
        return float.Hypot(x.Value, y.Value);
    }

    public static SinCosFloat RootN(SinCosFloat x, int n)
    {
        return float.RootN(x.Value, n);
    }

    public static SinCosFloat Sqrt(SinCosFloat x)
    {
        return MathF.Sqrt(x.Value);
    }

    public static SinCosFloat Acos(SinCosFloat x)
    {
        return MathF.Acos(x.Value);
    }

    public static SinCosFloat AcosPi(SinCosFloat x)
    {
        return float.AcosPi(x.Value);
    }

    public static SinCosFloat Asin(SinCosFloat x)
    {
        return MathF.Asin(x.Value);
    }

    public static SinCosFloat AsinPi(SinCosFloat x)
    {
        return float.AsinPi(x.Value);
    }

    public static SinCosFloat Atan(SinCosFloat x)
    {
        return MathF.Atan(x.Value);
    }

    public static SinCosFloat AtanPi(SinCosFloat x)
    {
        return float.AtanPi(x.Value);
    }

    static SinCosFloat ITrigonometricFunctions<SinCosFloat>.Cos(SinCosFloat x)
    {
        return MathF.Cos(x.Value);
    }

    public static SinCosFloat CosPi(SinCosFloat x)
    {
        return float.CosPi(x.Value);
    }

    static SinCosFloat ITrigonometricFunctions<SinCosFloat>.Sin(SinCosFloat x)
    {
        return MathF.Sin(x.Value);
    }

    public static (SinCosFloat Sin, SinCosFloat Cos) SinCos(SinCosFloat x)
    {
        var (sin, cos) = MathF.SinCos(x.Value);
        return (sin, cos);
    }

    public static (SinCosFloat SinPi, SinCosFloat CosPi) SinCosPi(SinCosFloat x)
    {
        var (sinPi, cosPi) = float.SinCosPi(x.Value);
        return (sinPi, cosPi);
    }

    public static SinCosFloat SinPi(SinCosFloat x)
    {
        return float.SinPi(x.Value);
    }

    public static SinCosFloat Tan(SinCosFloat x)
    {
        return MathF.Tan(x.Value);
    }

    public static SinCosFloat TanPi(SinCosFloat x)
    {
        return float.TanPi(x.Value);
    }

    public static SinCosFloat Atan2(SinCosFloat y, SinCosFloat x)
    {
        return MathF.Atan2(y.Value, x.Value);
    }

    public static SinCosFloat Atan2Pi(SinCosFloat y, SinCosFloat x)
    {
        return float.Atan2Pi(y.Value, x.Value);
    }

    public static SinCosFloat BitDecrement(SinCosFloat x)
    {
        return float.BitDecrement(x.Value);
    }

    public static SinCosFloat BitIncrement(SinCosFloat x)
    {
        return float.BitIncrement(x.Value);
    }

    public static SinCosFloat FusedMultiplyAdd(SinCosFloat left, SinCosFloat right, SinCosFloat addend)
    {
        return float.FusedMultiplyAdd(left.Value, right.Value, addend.Value);
    }

    public static SinCosFloat Ieee754Remainder(SinCosFloat left, SinCosFloat right)
    {
        return float.Ieee754Remainder(left.Value, right.Value);
    }

    public static int ILogB(SinCosFloat x)
    {
        return float.ILogB(x.Value);
    }

    public static SinCosFloat ScaleB(SinCosFloat x, int n)
    {
        return float.ScaleB(x.Value, n);
    }

    public static SinCosFloat Epsilon => float.Epsilon;
    public static SinCosFloat NaN => float.NaN;
    public static SinCosFloat NegativeInfinity => float.NegativeInfinity;
    public static SinCosFloat NegativeZero => float.NegativeZero;
    public static SinCosFloat PositiveInfinity => float.PositiveInfinity;

    public void Deconstruct(out float Value)
    {
        Value = this.Value;
    }

    public static bool operator ==(SinCosFloat left, float right)
    {
        return left.Value == right;
    }

    public static bool operator !=(SinCosFloat left, float right)
    {
        return left.Value != right;
    }

    public static bool operator >(SinCosFloat left, float right)
    {
        return left.Value > right;
    }

    public static bool operator >=(SinCosFloat left, float right)
    {
        return left.Value >= right;
    }

    public static bool operator <(SinCosFloat left, float right)
    {
        return left.Value < right;
    }

    public static bool operator <=(SinCosFloat left, float right)
    {
        return left.Value <= right;
    }

    public static bool operator ==(SinCosFloat left, int right)
    {
        return left.Value == right;
    }

    public static bool operator !=(SinCosFloat left, int right)
    {
        return left.Value != right;
    }

    public static bool operator >(SinCosFloat left, int right)
    {
        return left.Value > right;
    }

    public static bool operator >=(SinCosFloat left, int right)
    {
        return left.Value >= right;
    }

    public static bool operator <(SinCosFloat left, int right)
    {
        return left.Value < right;
    }

    public static bool operator <=(SinCosFloat left, int right)
    {
        return left.Value <= right;
    }
}