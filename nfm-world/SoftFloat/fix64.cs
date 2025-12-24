
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

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using FixedMathSharp;

namespace SoftFloat;

// Internal representation is identical to IEEE binary32 floating point numbers
[DebuggerDisplay("{ToString()}")]
public readonly partial struct fix64(Fixed64 value) : IEquatable<fix64>, IComparable<fix64>, IComparable, IFormattable
{
    public const int FRACTION_BITS = FixedMath.SHIFT_AMOUNT_I;

    public readonly Fixed64 Value = value;
    public static fix64 MinusOne => new(-Fixed64.One);
    public static fix64 Zero => new(Fixed64.Zero);
    public static fix64 One => new(Fixed64.One);
    public static fix64 Two => new(Fixed64.Two);
    public static fix64 Half => new(Fixed64.Half);
    public static fix64 MinValue => new(Fixed64.MIN_VALUE);
    public static fix64 MaxValue => new(Fixed64.MAX_VALUE);
    public long Raw => Value.m_rawValue;

    public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format!);
    public bool Equals(fix64 other) => Value.Equals(other.Value);
    public int CompareTo(fix64 other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public int CompareTo(object? obj) => obj is fix64 other ? Value.CompareTo(other.Value) : 0;
        
    public static explicit operator fix64(float value) => new(new Fixed64(value));
    public static explicit operator float(fix64 value) => value.Value.ToPreciseFloat();
    public static implicit operator fix64(int value) => new(new Fixed64(value));

    public static explicit operator int(fix64 value)
    {
        // truncation toward zero. regularly casting fixed64 to int doesn't do that
        if (value.Value > Fixed64.Zero)
            return value.Value.FloorToInt();
        else
            return value.Value.CeilToInt();
    }

    public static fix64 operator +(fix64 a, fix64 b) => new(a.Value + b.Value);
    public static fix64 operator -(fix64 a, fix64 b) => new(a.Value - b.Value);
    public static fix64 operator *(fix64 a, fix64 b) => new(a.Value * b.Value);
    public static fix64 operator /(fix64 a, fix64 b) => new(a.Value / b.Value);
    public static fix64 operator %(fix64 a, fix64 b) => new(a.Value % b.Value);
    public static fix64 operator -(fix64 a) => new(-a.Value);
    public static bool operator ==(fix64 a, fix64 b) => a.Value == b.Value;
    public static bool operator !=(fix64 a, fix64 b) => a.Value != b.Value;
    public static bool operator <(fix64 a, fix64 b) => a.Value < b.Value;
    public static bool operator <=(fix64 a, fix64 b) => a.Value <= b.Value;
    public static bool operator >(fix64 a, fix64 b) => a.Value > b.Value;
    public static bool operator >=(fix64 a, fix64 b) => a.Value >= b.Value;

    public static fix64 Abs(fix64 a) => new(a.Value.Abs());
    public int Sign() => Value.Sign();
    public static fix64 Min(fix64 a, fix64 b) => a < b ? a : b;
    public static fix64 Max(fix64 a, fix64 b) => a > b ? a : b;

    public static fix64 FromRaw(long raw) => new(new(raw));
}