using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace SoftFloat;

public partial struct fix64 : ISpanParsable<fix64>
{
    public static fix64 Parse(string s, IFormatProvider? provider)
    {
        return (fix64)float.Parse(s, provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out fix64 result)
    {
        var success = float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out var temp);
        result = (fix64)temp;
        return success;
    }

    public static fix64 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return (fix64)float.Parse(s, provider);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out fix64 result)
    {
        var success = float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out var temp);
        result = (fix64)temp;
        return success;
    }
}