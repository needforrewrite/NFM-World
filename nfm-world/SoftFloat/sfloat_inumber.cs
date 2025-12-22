using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace SoftFloat;

public partial struct sfloat : ISpanParsable<sfloat>
{
    public static sfloat Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out sfloat result)
    {
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static sfloat Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        var success = float_scan(s, out var result);
        if (!success)
        {
            throw new FormatException($"Input string '{s.ToString()}' was not in a correct format.");
        }
        if (!float.TryParse(s, CultureInfo.InvariantCulture, out var temp) || temp != (float)result)
        {
            Console.WriteLine($"Discrepancy parsing '{s.ToString()}': float={temp}, sfloat={result}");
        }
        return result;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out sfloat result)
    {
        var success = float_scan(s, out result);
        if (!float.TryParse(s, CultureInfo.InvariantCulture, out var temp) || temp != (float)result)
        {
            Console.WriteLine($"Discrepancy parsing '{s.ToString()}': float={temp}, sfloat={result}");
        }
        return success;
    }
    
    private static unsafe bool float_scan(ReadOnlySpan<char> span, out sfloat val)
    {
        if (!span.EndsWith('\0'))
        {
            Span<char> tmp = stackalloc char[span.Length + 1];
            span.CopyTo(tmp);
            tmp[span.Length] = '\0';
            fixed (char* p = tmp)
            {
                return float_scan(p, out val);
            }
        }

        fixed (char* p = span)
        {
            return float_scan(p, out val);
        }
    }

    // https://stackoverflow.com/a/1638340
    // made by bueddl & shawak
    private static unsafe bool float_scan(char* wcs, out sfloat val)
    {
        int hdr = 0;
        while (wcs[hdr] == ' ')
            hdr++;

        int cur = hdr;

        bool negative = false;
        bool has_sign = false;

        if (wcs[cur] == '+' || wcs[cur] == '-')
        {
            if (wcs[cur] == '-')
                negative = true;
            has_sign = true;
            cur++;
        }
        else
            has_sign = false;

        int quot_digs = 0;
        int frac_digs = 0;

        bool full = false;

        char period = '\0';
        int binexp = 0;
        int decexp = 0;
        uint value = 0;

        while (wcs[cur] >= '0' && wcs[cur] <= '9')
        {
            if (!full)
            {
                if (value >= 0x19999999 && wcs[cur] - '0' > 5 || value > 0x19999999)
                {
                    full = true;
                    decexp++;
                }
                else
                    value = value * 10 + wcs[cur] - '0';
            }
            else
                decexp++;

            quot_digs++;
            cur++;
        }

        if (wcs[cur] == '.' || wcs[cur] == ',')
        {
            period = wcs[cur];
            cur++;

            while (wcs[cur] >= '0' && wcs[cur] <= '9')
            {
                if (!full)
                {
                    if (value >= 0x19999999 && wcs[cur] - '0' > 5 || value > 0x19999999)
                        full = true;
                    else
                    {
                        decexp--;
                        value = value * 10 + wcs[cur] - '0';
                    }
                }

                frac_digs++;
                cur++;
            }
        }

        if (quot_digs == 0 && frac_digs == 0)
        {
            val = 0;
            return false;
        }

        char exp_char = '\0';

        int decexp2 = 0; // explicit exponent
        bool exp_negative = false;
        bool has_expsign = false;
        int exp_digs = 0;

        // even if value is 0, we still need to eat exponent chars
        if (wcs[cur] == 'e' || wcs[cur] == 'E')
        {
            exp_char = wcs[cur];
            cur++;

            if (wcs[cur] == '+' || wcs[cur] == '-')
            {
                has_expsign = true;
                if (wcs[cur] == '-')
                    exp_negative = true;
                cur++;
            }

            while (wcs[cur] >= '0' && wcs[cur] <= '9')
            {
                if (decexp2 >= 0x19999999)
                {
                    val = 0;
                    return false;
                }
                decexp2 = 10 * decexp2 + wcs[cur] - '0';
                exp_digs++;
                cur++;
            }

            if (exp_negative)
                decexp -= decexp2;
            else
                decexp += decexp2;
        }

        // end of wcs scan, cur contains value's tail

        if (value != 0)
        {
            while (value <= 0x19999999)
            {
                decexp--;
                value = value * 10;
            }

            if (decexp != 0)
            {
                // ensure 1bit space for mul by something lower than 2.0
                if ((value & 0x80000000) != 0)
                {
                    value >>= 1;
                    binexp++;
                }

                if (decexp > 308 || decexp < -307)
                {
                    val = 0;
                    return false;
                }

                // convert exp from 10 to 2 (using FPU)
                int E;
                sfloat v = libm.powf((sfloat)10.0f, decexp);
                sfloat m = frexp(v, out E);
                m = (sfloat)2.0f * m;
                E--;
                value = (uint)libm.floorf(value * m);

                binexp += E;
            }

            binexp += 23; // rebase exponent to 23bits of mantisa


            // so the value is: +/- VALUE * pow(2,BINEXP);
            // (normalize manthisa to 24bits, update exponent)
            while ((value & 0xFE000000) != 0)
            {
                value >>= 1;
                binexp++;
            }

            if ((value & 0x01000000) != 0)
            {
                if ((value & 1) != 0)
                    value++;
                value >>= 1;
                binexp++;
                if ((value & 0x01000000) != 0)
                {
                    value >>= 1;
                    binexp++;
                }
            }

            while ((value & 0x00800000) == 0)
            {
                value <<= 1;
                binexp--;
            }

            if (binexp < -127)
            {
                // underflow
                value = 0;
                binexp = -127;
            }
            else if (binexp > 128)
            {
                val = 0;
                return false;
            }

            //exclude "implicit 1"
            value &= 0x007FFFFF;

            // encode exponent
            uint exponent = (uint)((binexp + 127) << 23);
            value |= exponent;
        }

        // encode sign
        uint sign = (negative ? 1u : 0u) << 31;
        value |= sign;

        val = new sfloat(value);

        return true;
    }

    /*!
        https://github.com/MachineCognitis/C.math.NET

        MIT License

        Copyright (c) 2016 Robert Baron, Machine Cognitis

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
     */
    private static sfloat frexp(sfloat number, out int exponent)
    {
        uint bits = number.rawValue;
        int exp = number.RawExponent;
        exponent = 0;

        if (exp == 0xff || number == Zero)
            number += number;
        else
        {
            // Not zero and finite.
            exponent = exp - 126;
            if (exp == 0)
            {
                // Subnormal, scale number so that it is in [1, 2).
                number *= new sfloat(0x4c000000); // 2^25
                bits = number.rawValue;
                exp = number.RawExponent;
                exponent = exp - 126 - 25;
            }
            // Set exponent to -1 so that number is in [0.5, 1).
            const int FLT_MANT_MASK = 0x007fffff;
            const int FLT_SGN_MASK = -1 - 0x7fffffff;
            const int FLT_EXP_CLR_MASK = FLT_SGN_MASK | FLT_MANT_MASK;
            number = new sfloat((uint)(((int)bits & FLT_EXP_CLR_MASK) | 0x3f000000));
        }

        return number;
    }
}