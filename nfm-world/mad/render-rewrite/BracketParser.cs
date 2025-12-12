using System.Globalization;
using System.Numerics;

namespace NFMWorld.Mad;

public static class BracketParser
{
    public static Span<T> GetNumbers<T>(ReadOnlySpan<char> line, Span<T> n)
        where T : INumberBase<T>
    {
        // Console.WriteLine(line);
        var closingParen = line.IndexOf(')');
        var lineSlice = line[(line.IndexOf('(') + 1)..(closingParen != -1 ? closingParen : line.Length)];

        var i = 0;
        foreach (var range in lineSlice.Split(','))
        {
            if (i >= n.Length)
                break;
            // Console.WriteLine($"{i},{lineSlice[range]}");
            n[i] = T.Parse(lineSlice[range], NumberStyles.Number, null);
            i++;
        }

        return n[..i];
    }
    
    public static string[] GetStrings(ReadOnlySpan<char> line, int? n = null)
    {
        var lineSlice = line[(line.IndexOf('(') + 1)..line.IndexOf(')')];

        var strings = new List<string>(n ?? 64);
        var i = 0;
        foreach (var range in lineSlice.Split(','))
        {
            if (i >= n)
                break;
            strings.Add(new string(lineSlice[range]));
            i++;
        }

        return strings.ToArray();
    }

    public static void Deconstruct<T>(this ReadOnlySpan<T> items, out T t0, out T t1, out T t2)
    {
        t0 = items[0];
        t1 = items[1];
        t2 = items[2];
    }

    public static void Deconstruct<T>(this ReadOnlySpan<T> items, out T t, out ReadOnlySpan<T> rest)
    {
        t = items[0];
        rest = items[1..];
    }
}