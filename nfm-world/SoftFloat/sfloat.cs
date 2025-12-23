
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

namespace SoftFloat
{
    // Internal representation is identical to IEEE binary32 floating point numbers
    [DebuggerDisplay("{ToStringInv()}")]
    public readonly partial struct sfloat(Fixed64 value) : IEquatable<sfloat>, IComparable<sfloat>, IComparable, IFormattable
    {
        public readonly Fixed64 Value = value;
        public static sfloat MinusOne => new(-Fixed64.One);
        public static sfloat Zero => new(Fixed64.Zero);
        public static sfloat One => new(Fixed64.One);
        public static sfloat Two => new(Fixed64.Two);
        public static sfloat Half => new(Fixed64.Half);
        public static sfloat MinValue => new(Fixed64.MIN_VALUE);
        public static sfloat MaxValue => new(Fixed64.MAX_VALUE);

        public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format!);
        public bool Equals(sfloat other) => Value.Equals(other.Value);
        public int CompareTo(sfloat other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString();
        public int CompareTo(object? obj) => obj is sfloat other ? Value.CompareTo(other.Value) : 0;
        
        public static explicit operator sfloat(float value) => new(new Fixed64(value));
        public static explicit operator float(sfloat value) => value.Value.ToPreciseFloat();
        public static implicit operator sfloat(int value) => new(new Fixed64(value));
        public static explicit operator int(sfloat value) => (int)value.Value;
        
        public static sfloat operator +(sfloat a, sfloat b) => new(a.Value + b.Value);
        public static sfloat operator -(sfloat a, sfloat b) => new(a.Value - b.Value);
        public static sfloat operator *(sfloat a, sfloat b) => new(a.Value * b.Value);
        public static sfloat operator /(sfloat a, sfloat b) => new(a.Value / b.Value);
        public static sfloat operator %(sfloat a, sfloat b) => new(a.Value % b.Value);
        public static sfloat operator -(sfloat a) => new(-a.Value);
        public static bool operator ==(sfloat a, sfloat b) => a.Value == b.Value;
        public static bool operator !=(sfloat a, sfloat b) => a.Value != b.Value;
        public static bool operator <(sfloat a, sfloat b) => a.Value < b.Value;
        public static bool operator <=(sfloat a, sfloat b) => a.Value <= b.Value;
        public static bool operator >(sfloat a, sfloat b) => a.Value > b.Value;
        public static bool operator >=(sfloat a, sfloat b) => a.Value >= b.Value;

        public static sfloat Abs(sfloat a) => new(a.Value.Abs());
        public int Sign() => Value.Sign();
        public static sfloat Min(sfloat a, sfloat b) => a < b ? a : b;
        public static sfloat Max(sfloat a, sfloat b) => a > b ? a : b;
    }
}
