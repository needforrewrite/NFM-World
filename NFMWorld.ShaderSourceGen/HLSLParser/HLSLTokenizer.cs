// Ported from HLSLTokenizer.h / HLSLTokenizer.cpp (M4 / Unknown Worlds Entertainment)

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace NFMWorld.ShaderSourceGen;

public enum HLSLToken
{
    // Built-in types (start at 256 to leave room for single-char ASCII tokens).
    Float = 256,
    Float2,
    Float3,
    Float4,
    Float3x3,
    Float4x4,
    Half,
    Half2,
    Half3,
    Half4,
    Half3x3,
    Half4x4,
    Bool,
    Int,
    Int2,
    Int3,
    Int4,
    Uint,
    Uint2,
    Uint3,
    Uint4,
    Texture,
    Sampler,
    Sampler2D,
    SamplerCube,

    // Reserved words.
    If,
    Else,
    For,
    While,
    Break,
    True,
    False,
    Void,
    Struct,
    CBuffer,
    TBuffer,
    Register,
    Return,
    Continue,
    Discard,
    Const,
    Static,
    Inline,

    // Input modifiers.
    Uniform,
    In,
    Out,
    InOut,

    // Effect keywords.
    SamplerState,
    Technique,
    Pass,

    // Multi-character symbols.
    LessEqual,
    GreaterEqual,
    EqualEqual,
    NotEqual,
    PlusPlus,
    MinusMinus,
    PlusEqual,
    MinusEqual,
    TimesEqual,
    DivideEqual,
    AndAnd,
    BarBar,

    // Literals / identifier.
    FloatLiteral,
    HalfLiteral,
    IntLiteral,
    Identifier,

    EndOfStream,
}

public ref struct HLSLTokenizer
{
    public const int MaxIdentifier = 256;

    private static readonly string[] ReservedWords =
    [
        "float",
        "float2",
        "float3",
        "float4",
        "float3x3",
        "float4x4",
        "half",
        "half2",
        "half3",
        "half4",
        "half3x3",
        "half4x4",
        "bool",
        "int", "int2", "int3", "int4",
        "uint", "uint2", "uint3", "uint4",
        "texture",
        "sampler", "sampler2D", "samplerCUBE",
        "if", "else", "for", "while", "break",
        "true", "false",
        "void", "struct",
        "cbuffer", "tbuffer",
        "register", "return", "continue", "discard",
        "const", "static", "inline",
        "uniform", "in", "out", "inout",
        "sampler_state", "technique", "pass"
    ];

    private ReadOnlySpan<char> _fileName;
    private readonly ReadOnlySpan<char> _buffer;
    private int _pos;
    private readonly int _length;
    private int _lineNumber;

    private ushort _token;
    private float _fValue;
    private int _iValue;
    private ReadOnlySpan<char> _identifier = ReadOnlySpan<char>.Empty;
    private ReadOnlySpan<char> _lineDirectiveFileName = ReadOnlySpan<char>.Empty;
    private int _tokenLineNumber;

    public HLSLTokenizer(ReadOnlySpan<char> fileName, ReadOnlySpan<char> buffer, int length)
    {
        _fileName = fileName;
        _buffer = buffer;
        _length = length;
        _pos = 0;
        _lineNumber = 1;
        _tokenLineNumber = 1;
        Next();
    }

    public HLSLTokenizer(ReadOnlySpan<char> fileName, ReadOnlySpan<char> buffer)
        : this(fileName, buffer, buffer.Length)
    {
    }

    public void Next()
    {
        while (SkipWhitespace() || SkipComment() || ScanLineDirective() || SkipPragmaDirective())
        {
        }

        _tokenLineNumber = _lineNumber;

        if (_pos >= _length)
        {
            _token = (ushort)HLSLToken.EndOfStream;
            return;
        }

        var c0 = _buffer[_pos];
        var c1 = _pos + 1 < _length ? _buffer[_pos + 1] : '\0';

        // Two-character operators
        if (c0 == '+' && c1 == '=')
        {
            _token = (ushort)HLSLToken.PlusEqual;
            _pos += 2;
            return;
        }

        if (c0 == '-' && c1 == '=')
        {
            _token = (ushort)HLSLToken.MinusEqual;
            _pos += 2;
            return;
        }

        if (c0 == '*' && c1 == '=')
        {
            _token = (ushort)HLSLToken.TimesEqual;
            _pos += 2;
            return;
        }

        if (c0 == '/' && c1 == '=')
        {
            _token = (ushort)HLSLToken.DivideEqual;
            _pos += 2;
            return;
        }

        if (c0 == '=' && c1 == '=')
        {
            _token = (ushort)HLSLToken.EqualEqual;
            _pos += 2;
            return;
        }

        if (c0 == '!' && c1 == '=')
        {
            _token = (ushort)HLSLToken.NotEqual;
            _pos += 2;
            return;
        }

        if (c0 == '<' && c1 == '=')
        {
            _token = (ushort)HLSLToken.LessEqual;
            _pos += 2;
            return;
        }

        if (c0 == '>' && c1 == '=')
        {
            _token = (ushort)HLSLToken.GreaterEqual;
            _pos += 2;
            return;
        }

        if (c0 == '&' && c1 == '&')
        {
            _token = (ushort)HLSLToken.AndAnd;
            _pos += 2;
            return;
        }

        if (c0 == '|' && c1 == '|')
        {
            _token = (ushort)HLSLToken.BarBar;
            _pos += 2;
            return;
        }

        // ++, --
        if (c0 is '+' or '-' && c1 == c0)
        {
            _token = c0 == '+' ? (ushort)HLSLToken.PlusPlus : (ushort)HLSLToken.MinusMinus;
            _pos += 2;
            return;
        }

        // Number
        if (ScanNumber()) return;

        // Single-char symbol
        if (IsSymbol(c0))
        {
            _token = c0;
            _pos++;
            return;
        }

        // Identifier or reserved word
        var start = _pos;
        while (_pos < _length && !IsSymbol(_buffer[_pos]) && !char.IsWhiteSpace(_buffer[_pos]))
            _pos++;

        _identifier = _buffer[start.._pos];

        for (var i = 0; i < ReservedWords.Length; i++)
        {
            if (_identifier.SequenceEqual(ReservedWords[i]))
            {
                _token = (ushort)(256 + i);
                return;
            }
        }

        _token = (int)HLSLToken.Identifier;
    }

    public int Token => _token;

    public float Float => _fValue;

    public int Int => _iValue;

    public ReadOnlySpan<char> Identifier => _identifier;

    public int LineNumber => _tokenLineNumber;

    public ReadOnlySpan<char> FileName => _fileName;

    public ReadOnlySpan<char> TokenName
    {
        get
        {
            if (_token is (int)HLSLToken.FloatLiteral or (int)HLSLToken.HalfLiteral)
                return _fValue.ToString(CultureInfo.InvariantCulture);
            if (_token == (int)HLSLToken.IntLiteral)
                return _iValue.ToString(CultureInfo.InvariantCulture);
            if (_token == (int)HLSLToken.Identifier)
                return _identifier;
            return GetTokenName(_token);
        }
    }

    public static string GetTokenName(int token)
    {
        if (token < 256) return ((char)token).ToString();
        if (token < (int)HLSLToken.LessEqual) return ReservedWords[token - 256];

        return (HLSLToken)token switch
        {
            HLSLToken.PlusPlus => "++",
            HLSLToken.MinusMinus => "--",
            HLSLToken.PlusEqual => "+=",
            HLSLToken.MinusEqual => "-=",
            HLSLToken.TimesEqual => "*=",
            HLSLToken.DivideEqual => "/=",
            HLSLToken.LessEqual => "<=",
            HLSLToken.GreaterEqual => ">=",
            HLSLToken.EqualEqual => "==",
            HLSLToken.NotEqual => "!=",
            HLSLToken.AndAnd => "&&",
            HLSLToken.BarBar => "||",
            HLSLToken.HalfLiteral => "half",
            HLSLToken.FloatLiteral => "float",
            HLSLToken.IntLiteral => "int",
            HLSLToken.Identifier => "identifier",
            HLSLToken.EndOfStream => "<eof>",
            _ => "unknown",
        };
    }

    [DoesNotReturn]
    internal bool Error(ReadOnlySpan<char> error)
    {
        throw new InvalidOperationException($"{_fileName}({_lineNumber}) : {error}");
    }

    [DoesNotReturn]
    internal bool Error(DefaultInterpolatedStringHandler handler)
    {
        throw new InvalidOperationException($"{_fileName}({_lineNumber}) : {handler.ToStringAndClear()}");
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static bool IsSymbol(char c) => c is ';' or ':' or '(' or ')' or '[' or ']' or '{' or '}'
        or '-' or '+' or '*' or '/' or '?' or '!' or ',' or '=' or '.' or '<' or '>'
        or '|' or '&' or '^' or '~' or '@';

    private static bool IsNumberSeparator(char c) => c == '\0' || char.IsWhiteSpace(c) || IsSymbol(c);

    private char CharAt(int idx) => idx < _length ? _buffer[idx] : '\0';

    private bool SkipWhitespace()
    {
        var result = false;
        while (_pos < _length && char.IsWhiteSpace(_buffer[_pos]))
        {
            result = true;
            if (_buffer[_pos] == '\n') _lineNumber++;
            _pos++;
        }

        return result;
    }

    private bool SkipComment()
    {
        if (_pos >= _length || _buffer[_pos] != '/') return false;
        var next = CharAt(_pos + 1);
        if (next == '/')
        {
            _pos += 2;
            while (_pos < _length)
            {
                if (_buffer[_pos++] == '\n')
                {
                    _lineNumber++;
                    break;
                }
            }

            return true;
        }

        if (next == '*')
        {
            _pos += 2;
            while (_pos < _length)
            {
                if (_buffer[_pos] == '\n') _lineNumber++;
                if (_buffer[_pos] == '*' && CharAt(_pos + 1) == '/')
                {
                    _pos += 2;
                    break;
                }

                _pos++;
            }

            return true;
        }

        return false;
    }

    private bool SkipPragmaDirective()
    {
        if (_pos >= _length || _buffer[_pos] != '#') return false;
        var ptr = _pos + 1;
        while (ptr < _length && char.IsWhiteSpace(_buffer[ptr]) && _buffer[ptr] != '\n') ptr++;
        if (ptr + 6 <= _length
            && _buffer.Slice(ptr, 6) is "pragma"
            && (ptr + 6 >= _length || char.IsWhiteSpace(_buffer[ptr + 6])))
        {
            _pos = ptr + 6;
            while (_pos < _length)
            {
                if (_buffer[_pos++] == '\n')
                {
                    _lineNumber++;
                    break;
                }
            }

            return true;
        }

        return false;
    }

    private bool ScanNumber()
    {
        if (_pos >= _length) return false;
        var c0 = _buffer[_pos];
        if (c0 == '+' || c0 == '-') return false;

        // Hex literal
        if (_length - _pos > 2 && c0 == '0' && _buffer[_pos + 1] == 'x')
        {
            var end = _pos + 2;
            while (end < _length && IsHexDigit(_buffer[end])) end++;
            if (end > _pos + 2 && IsNumberSeparator(CharAt(end)))
            {
                _iValue = Convert.ToInt32(string.FromSpan(_buffer.Slice(_pos + 2, end - _pos - 2)), 16);
                _pos = end;
                _token = (int)HLSLToken.IntLiteral;
                return true;
            }
        }

        // Try floating point
        var fEnd = _pos;
        var hasDot = false;
        var hasDigit = false;
        while (fEnd < _length && char.IsDigit(_buffer[fEnd]))
        {
            fEnd++;
            hasDigit = true;
        }

        if (fEnd < _length && _buffer[fEnd] == '.')
        {
            hasDot = true;
            fEnd++;
        }

        while (fEnd < _length && char.IsDigit(_buffer[fEnd]))
        {
            fEnd++;
            hasDigit = true;
        }

        // Exponent
        if (fEnd < _length && (_buffer[fEnd] == 'e' || _buffer[fEnd] == 'E'))
        {
            fEnd++;
            if (fEnd < _length && (_buffer[fEnd] == '+' || _buffer[fEnd] == '-')) fEnd++;
            while (fEnd < _length && char.IsDigit(_buffer[fEnd])) fEnd++;
        }

        if (!hasDigit) return false;

        // Check for f/h suffix
        var hasSuffix = false;
        var suffix = '\0';
        if (fEnd < _length && (_buffer[fEnd] == 'f' || _buffer[fEnd] == 'h'))
        {
            suffix = _buffer[fEnd];
            hasSuffix = true;
            fEnd++;
        }

        // Try integer
        var iEnd = _pos;
        while (iEnd < _length && char.IsDigit(_buffer[iEnd])) iEnd++;

        if ((hasDot || hasSuffix) && hasDigit && fEnd > iEnd && IsNumberSeparator(CharAt(fEnd)))
        {
            var numStr = _buffer.Slice(_pos, (hasSuffix ? fEnd - 1 : fEnd) - _pos);
            if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
            {
                _fValue = (float)dv;
                _pos = fEnd;
                _token = suffix == 'h' ? (ushort)HLSLToken.HalfLiteral : (ushort)HLSLToken.FloatLiteral;
                return true;
            }
        }

        if (iEnd > _pos && IsNumberSeparator(CharAt(iEnd)))
        {
            _iValue = int.Parse(_buffer.Slice(_pos, iEnd - _pos), CultureInfo.InvariantCulture);
            _pos = iEnd;
            _token = (int)HLSLToken.IntLiteral;
            return true;
        }

        return false;
    }

    private bool ScanLineDirective()
    {
        if (_length - _pos <= 5 || _buffer.Slice(_pos, 5) is not "#line" || !char.IsWhiteSpace(_buffer[_pos + 5]))
            return false;

        _pos += 5;
        while (_pos < _length && char.IsWhiteSpace(_buffer[_pos]))
        {
            if (_buffer[_pos] == '\n')
            {
                Error("Syntax error: expected line number after #line");
                return false;
            }

            _pos++;
        }

        var numStart = _pos;
        while (_pos < _length && char.IsDigit(_buffer[_pos])) _pos++;
        if (_pos == numStart || !char.IsWhiteSpace(CharAt(_pos)))
        {
            Error("Syntax error: expected line number after #line");
            return false;
        }

        var lineNumber = int.Parse(_buffer.Slice(numStart, _pos - numStart), CultureInfo.InvariantCulture);

        while (_pos < _length && char.IsWhiteSpace(_buffer[_pos]))
        {
            var ch = _buffer[_pos];
            _pos++;
            if (ch == '\n')
            {
                _lineNumber = lineNumber;
                return true;
            }
        }

        if (_pos >= _length)
        {
            _lineNumber = lineNumber;
            return true;
        }

        if (_buffer[_pos] != '"')
        {
            Error("Syntax error: expected '\"' after line number near #line");
            return false;
        }

        _pos++;

        var fnStart = _pos;
        while (_pos < _length && _buffer[_pos] != '"')
        {
            if (_buffer[_pos] == '\n')
            {
                Error("Syntax error: expected '\"' before end of line near #line");
                return false;
            }

            _pos++;
        }

        if (_pos >= _length)
        {
            Error("Syntax error: expected '\"' before end of file near #line");
            return false;
        }

        _lineDirectiveFileName = _buffer.Slice(fnStart, _pos - fnStart);
        _pos++; // skip closing quote

        while (_pos < _length && _buffer[_pos] != '\n')
        {
            if (!char.IsWhiteSpace(_buffer[_pos]))
            {
                Error("Syntax error: unexpected input after file name near #line");
                return false;
            }

            _pos++;
        }

        _pos++; // skip newline

        _lineNumber = lineNumber;
        _fileName = _lineDirectiveFileName;
        return true;
    }

    private static bool IsHexDigit(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
