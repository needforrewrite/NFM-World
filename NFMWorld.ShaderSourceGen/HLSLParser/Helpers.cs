using System;

namespace NFMWorld.ShaderSourceGen;

internal static class Helpers
{
    extension(string)
    {
        public static unsafe string FromSpan(ReadOnlySpan<char> span)
        {
            if (span.IsEmpty)
            {
                return string.Empty;
            }

            fixed (char* ptr = span)
            {
                return new string(ptr, 0, span.Length);
            }
        }
    }

    extension(int)
    {
        public static int Parse(ReadOnlySpan<char> s, IFormatProvider provider)
        {
            return int.Parse(string.FromSpan(s), provider);
        }
    }

    extension(ReadOnlySpan<char> span)
    {
        public int IndexOf(char value, int startIndex)
        {
            var idx = span[startIndex..].IndexOf(value);
            return idx == -1 ? -1 : idx + startIndex;
        }
    }
}
