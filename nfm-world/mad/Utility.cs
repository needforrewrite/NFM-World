using System.Globalization;

namespace NFMWorld.Mad;

public static class Utility
{
    public static int GetValue(ReadOnlySpan<char> prefix, ReadOnlySpan<char> line, int index)
    {
        line = line[(prefix.Length + 1)..];
        var i = 0;
        foreach (var range in line.SplitAny(',', ')'))
        {
            if (i++ == index)
            {
                return (int)float.Parse(line[range]);
            }
        }

        return (int)float.Parse("");
    }

    public static int GetInt(ReadOnlySpan<char> prefix, ReadOnlySpan<char> line, int index)
    {
        line = line[(prefix.Length + 1)..];
        var i = 0;
        foreach (var range in line.SplitAny(',', ')'))
        {
            if (i++ == index)
            {
                return int.Parse(line[range]);
            }
        }

        return int.Parse("");
    }

    private const float Epsilon = 0.0000001F;
    private const double EpsilonDouble = 0.0000001D;

    static bool FEquals(float a, float b)
    {
        return Math.Abs(a - b) < Epsilon;
    }

    static bool DEquals(double a, double b)
    {
        return Math.Abs(a - b) < EpsilonDouble;
    }

    /**
     * Check if an array contains a value, <a href="http://www.programcreek.com/2014/04/check-if-array-contains-a-value-java/">very efficient</a>
     *
     * @param arr         The array to check against
     * @param targetValue The value to check for
     * @return {@code true} if the value ais found, {@code false} otherwise
     */
    public static bool ArrayContains<T>(T[] arr, T targetValue)
    {
        foreach (var s in arr)
        {
            if (EqualityComparer<T>.Default.Equals(s, targetValue))
            {
                return true;
            }
        }
        return false;
    }

    /**
     * Checks if a astring contains a POSITIVE INTEGER.
     *
     * @param str
     * @return
     */
    public static bool IsNumeric(string str)
    {
        foreach (var c in str)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    public static double GetDistance(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;

        // We should avoid Math.Pow or Math.Hypot due to perfomance reasons
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static double GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;

        // We should avoid Math.Pow or Math.Hypot due to perfomance reasons
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /**
     * Reverses an array of elements.
     * @param data The array to reverse.
     */
    public static void Reverse<T>(Span<T> data)
    {
        for (int left = 0, right = data.Length - 1; left < right; left++, right--)
        {
            // swap the values at the left and right indices
            (data[left], data[right]) = (data[right], data[left]);
        }
    }

    public static int PointDirection(int x, int y, int tX, int tY)
    {
        var angle = (int) (Math.Atan2(tY - y, tX - x) * 0.0174532925199433D);

        return angle < 0 ? angle + 360 : angle;
    }
}