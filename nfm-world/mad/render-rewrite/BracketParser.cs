using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;

namespace NFMWorld.Mad;

public static class BracketParser
{
    private static ReadOnlySpan<char> GetLineSlice(ReadOnlySpan<char> line)
    {
        var closingParen = line.IndexOf(')');
        return line[(line.IndexOf('(') + 1)..(closingParen != -1 ? closingParen : line.Length)];
    }

    public static T GetNumber<T>(ReadOnlySpan<char> line)
        where T : INumberBase<T>
    {
        return T.Parse(GetLineSlice(line), NumberStyles.Number, CultureInfo.InvariantCulture);
    }
    
    public static Span<T> GetNumbers<T>(ReadOnlySpan<char> line, Span<T> n)
        where T : INumberBase<T>
    {
        // Console.WriteLine(line);
        var lineSlice = GetLineSlice(line);

        var i = 0;
        foreach (var range in lineSlice.Split(','))
        {
            if (i >= n.Length)
                break;
            // Console.WriteLine($"{i},{lineSlice[range]}");
            n[i] = T.Parse(lineSlice[range], NumberStyles.Number, CultureInfo.InvariantCulture);
            i++;
        }

        return n[..i];
    }

    public static string GetString(ReadOnlySpan<char> line)
    {
        return new string(GetLineSlice(line));
    }
    
    public static Span<string> GetStrings(ReadOnlySpan<char> line, int? n = null)
    {
        var lineSlice = GetLineSlice(line);

        var strings = new List<string>(n ?? 3);
        var i = 0;
        foreach (var range in lineSlice.Split(','))
        {
            if (i >= n)
                break;
            strings.Add(new string(lineSlice[range]));
            i++;
        }

        if (strings.Capacity == strings.Count)
        {
            return CollectionsMarshal.AsSpan(strings);
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