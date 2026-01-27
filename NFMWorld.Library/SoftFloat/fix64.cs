
// mostly from https://github.com/CodesInChaos/SoftFloat

// Copyright (c) 2011 CodesInChaos
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies
// or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// The MIT License (MIT) - http://www.opensource.org/licenses/mit-license.php
// If you need a different license please contact me

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using FixedMathSharp;

namespace nfm_world_library.SoftFloat;

[DebuggerDisplay("{ToString()}")]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)] 
public readonly partial struct fix64(Fixed64 value) : IEquatable<fix64>, IComparable<fix64>, IComparable, IFormattable
{
    public const int FRACTION_BITS = FixedMath.SHIFT_AMOUNT_I;

    public readonly Fixed64 Value = value;
    public static fix64 MinusOne => new(-Fixed64.One);
    public static fix64 Zero => new(Fixed64.Zero);
    public static fix64 One => new(Fixed64.One);
    public static fix64 Two => new(Fixed64.Two);
    public static fix64 Half => new(Fixed64.Half);
    public static fix64 Quarter => new(Fixed64.Quarter);
    public static fix64 MinValue => new(Fixed64.MIN_VALUE);
    public static fix64 MaxValue => new(Fixed64.MAX_VALUE);
    public long Raw
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value.m_rawValue;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private fix64(long value) : this(Fixed64.FromRaw(value))
    {
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public string ToString(string? format, IFormatProvider? formatProvider) => ((double)Value).ToString(format, CultureInfo.InvariantCulture);;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool Equals(fix64 other) => Value.Equals(other.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public int CompareTo(fix64 other) => Value.CompareTo(other.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public override string ToString() => Value.ToString();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public int CompareTo(object? obj) => obj is fix64 other ? Value.CompareTo(other.Value) : 0;
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static explicit operator fix64(float value) => new(new Fixed64(value));
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static explicit operator float(fix64 value) => value.Value.ToPreciseFloat();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static implicit operator fix64(int value) => new(new Fixed64(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(fix64 value)
    {
        // truncation toward zero. regularly casting fixed64 to int doesn't do that
        if (value.Value > Fixed64.Zero)
            return value.Value.FloorToInt();
        else
            return value.Value.CeilToInt();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 operator +(fix64 a, fix64 b) => new(a.Value + b.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 operator -(fix64 a, fix64 b) => new(a.Value - b.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 operator *(fix64 a, fix64 b)
    {
        // Widen to 128 bits to prevent overflow during multiplication
        // 128-bit intrinsic is faster than hand rolled multiplication + shift
        var mul = ((Int128)a.Raw * b.Raw) >> FRACTION_BITS;
        
        if (mul < long.MinValue)
            mul = long.MinValue;
        else if (mul > long.MaxValue)
            mul = long.MaxValue;

        var result = new fix64((long)mul);
#if DEBUG
        var expectedResult = new fix64(a.Value * b.Value);
        Debug.Assert(Abs(result - expectedResult).Value.ToPreciseFloat() < 0.001f, $"fix64 multiplication mismatch: {a} * {b} = {result}, expected {expectedResult}");
#endif
        return result;
    }

    // Slightly faster division algorithm than the one in FixedMathSharp
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static fix64 operator /(fix64 a, fix64 b)
    {
        long xl = a.Raw;
        long yl = b.Raw;

        if (yl == 0)
        {
            ThrowDivideByZeroException(a);
            return default;
        }

        ulong remainder = (ulong)(xl < 0 ? -xl : xl);
        ulong divider = (ulong)(yl < 0 ? -yl : yl);
        ulong quotient = 0UL;
        int bitPos = FixedMath.SHIFT_AMOUNT_I + 1;

        // If the divider is divisible by 2^n, take advantage of it.
        while ((divider & 0xF) == 0 && bitPos >= 4)
        {
            divider >>= 4;
            bitPos -= 4;
        }

        while (remainder != 0 && bitPos >= 0)
        {
            int shift = BitOperations.LeadingZeroCount(remainder);
            if (shift > bitPos)
                shift = bitPos;

            remainder <<= shift;
            bitPos -= shift;

            ulong div = remainder / divider;
            remainder %= divider;
            quotient += div << bitPos;

            // Detect overflow
            if ((div & ~(0xFFFFFFFFFFFFFFFF >> bitPos)) != 0)
                return ((xl ^ yl) & FixedMath.MIN_VALUE_L) == 0 ? new fix64(long.MaxValue) : new fix64(long.MinValue);

            remainder <<= 1;
            --bitPos;
        }

        // Rounding logic: "Round half to even" or "Banker's rounding"
        if ((quotient & 0x1) != 0)
            quotient += 1;

        long result = (long)(quotient >> 1);
        if (((xl ^ yl) & FixedMath.MIN_VALUE_L) != 0)
            result = -result;

        return new fix64(result);

        [DoesNotReturn]
        static void ThrowDivideByZeroException(fix64 a)
        {
            throw new DivideByZeroException($"Attempted to divide {a} by zero.");
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 operator %(fix64 a, fix64 b) => new(a.Value % b.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 operator -(fix64 a) => new(-a.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator ==(fix64 a, fix64 b) => a.Raw == b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator !=(fix64 a, fix64 b) => a.Raw != b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator <(fix64 a, fix64 b) => a.Raw < b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator <=(fix64 a, fix64 b) => a.Raw <= b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator >(fix64 a, fix64 b) => a.Raw > b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator >=(fix64 a, fix64 b) => a.Raw >= b.Raw;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 Abs(fix64 a) => new(a.Value.Abs());
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public int Sign() => Value.Sign();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 Min(fix64 a, fix64 b) => a < b ? a : b;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 Max(fix64 a, fix64 b) => a > b ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static fix64 FromRaw(long raw) => new(Fixed64.FromRaw(raw));

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloorToInt(fix64 f64) => f64.Value.FloorToInt();

    public static fix64 IEEERemainder(fix64 dividend, fix64 divisor)
    {
        var quotient = (dividend / divisor);
        var roundedQuotient = new fix64(new Fixed64(quotient.Value.m_rawValue & ~((1L << FRACTION_BITS) - 1)));
        var remainder = dividend - (roundedQuotient * divisor);

        var absRemainder = Abs(remainder);
        var absDivisor = Abs(divisor);

        if (absRemainder * Two > absDivisor)
        {
            remainder -= divisor;
        }
        else if (absRemainder * Two == absDivisor)
        {
            // halfway case - make even
            if ((roundedQuotient.Value.m_rawValue & 1) != 0)
            {
                remainder -= divisor;
            }
        }

        return remainder;
    }

    public static fix64 Atan(fix64 value)
    {
        return new(FixedMath.Atan(value.Value));
    }
}