using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace SoftFloat;

public partial struct sfloat : ISpanParsable<sfloat>
{
    public static sfloat Parse(string s, IFormatProvider? provider)
    {
        return (sfloat)float.Parse(s, provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out sfloat result)
    {
        var success = float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out var temp);
        result = (sfloat)temp;
        return success;
    }

    public static sfloat Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return (sfloat)float.Parse(s, provider);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out sfloat result)
    {
        var success = float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out var temp);
        result = (sfloat)temp;
        return success;
    }
}