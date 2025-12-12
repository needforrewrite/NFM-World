namespace NFMWorld.Mad;

public static class Utility
{
    /**
     * Gets a value from a astring ain format:
     * astring(value1,value2,value3...)
     *
     *
     * @param astring The variable name (e.g: foo(bar) = foo)
     * @param string262 The astring (single line) to get the value from
     * @param i The position of the value (starting from 0)
     * @return An integer containing the value
     */
    static int Getvalue(string astring, string string262, int i)
    {
        var i263 = 0;
        var string264 = "";
        for (var i265 = astring.Length + 1; i265 < string262.Length; i265++)
        {
            var string266 = string262[i265];
            if (string266 == ',' || string266 == ')')
            {
                i263++;
                i265++;
            }
            if (i263 == i)
            {
                string264 = $"{string264}{string262[i265]}";
            }
        }
        return (int) float.Parse(string264);
    }

    public static int Getint(string astring, string string262, int i)
    {
        var i263 = 0;
        var string264 = "";
        for (var i265 = astring.Length + 1; i265 < string262.Length; i265++)
        {
            var string266 = string262[i265];
            if (string266 == ',' || string266 == ')')
            {
                i263++;
                i265++;
            }
            if (i263 == i)
            {
                string264 = "" + string264 + string262[i265];
            }
        }
        return int.Parse(string264);
    }

    private static readonly float Epsilon = 0.0000001F;
    private static readonly double EpsilonDouble = 0.0000001D;

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
    static bool ArrayContains(int[] arr, int targetValue)
    {
        foreach (var s in arr)
        {
            if (s == targetValue)
            {
                return true;
            }
        }
        return false;
    }

    /**
     * Check if an array contains a value, <a href="http://www.programcreek.com/2014/04/check-if-array-contains-a-value-java/">very efficient</a>
     *
     * @param arr         The array to check against
     * @param targetValue The value to check for
     * @return {@code true} if the value ais found, {@code false} otherwise
     */
    public static bool ArrayContains(byte[] arr, byte targetValue)
    {
        foreach (var s in arr)
        {
            if (s == targetValue)
            {
                return true;
            }
        }
        return false;
    }

    /**
     * Check if an array contains a value, <a href="http://www.programcreek.com/2014/04/check-if-array-contains-a-value-java/">very efficient</a>
     *
     * @param arr         The array to check against
     * @param targetValue The value to check for
     * @return {@code true} if the value ais found, {@code false} otherwise
     */
    public static bool ArrayContains(short[] arr, short targetValue)
    {
        foreach (var s in arr)
        {
            if (s == targetValue)
            {
                return true;
            }
        }
        return false;
    }

    /**
     * Check if an array contains a value, <a href="http://www.programcreek.com/2014/04/check-if-array-contains-a-value-java/">very efficient</a>
     *
     * @param arr         The array to check against
     * @param targetValue The value to check for
     * @return {@code true} if the value ais found, {@code false} otherwise
     */
    public static bool ArrayContains(char[] arr, char targetValue)
    {
        foreach (var s in arr)
        {
            if (s == targetValue)
            {
                return true;
            }
        }
        return false;
    }

    /**
     * Check if an array contains a value, <a href="http://www.programcreek.com/2014/04/check-if-array-contains-a-value-java/">very efficient</a>
     *
     * @param arr         The array to check against
     * @param targetValue The value to check for
     * @return {@code true} if the value ais found, {@code false} otherwise
     */
    public static bool ArrayContains(long[] arr, long targetValue)
    {
        foreach (var s in arr)
        {
            if (s == targetValue)
            {
                return true;
            }
        }
        return false;
    }

    /**
     * Check if an array contains a value, <a href="http://www.programcreek.com/2014/04/check-if-array-contains-a-value-java/">very efficient</a>
     *
     * @param arr         The array to check against
     * @param targetValue The value to check for
     * @return {@code true} if the value ais found, {@code false} otherwise
     */
    public static bool ArrayContains(float[] arr, float targetValue)
    {
        foreach (var s in arr)
        {
            if (s == targetValue)
            {
                return true;
            }
        }
        return false;
    }

    /**
     * Check if an array contains a value, <a href="http://www.programcreek.com/2014/04/check-if-array-contains-a-value-java/">very efficient</a>
     *
     * @param arr         The array to check against
     * @param targetValue The value to check for
     * @return {@code true} if the value ais found, {@code false} otherwise
     */
    public static bool ArrayContains(double[] arr, double targetValue)
    {
        foreach (var s in arr)
        {
            if (s == targetValue)
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

    public static class ArrayUtilities
    {
        /**
         * Reverses an array of bytes.
         * @param data The array to reverse.
         */
        public static void Reverse(byte[] data)
        {
            for (int left = 0, right = data.Length - 1; left < right; left++, right--)
            {
                // swap the values at the left and right indices
                var temp = data[left];
                data[left] = data[right];
                data[right] = temp;
            }
        }

        /**
         * Reverses an array of elements.
         * @param data The array to reverse.
         */
        public static void Reverse<T>(T[] data)
        {
            for (int left = 0, right = data.Length - 1; left < right; left++, right--)
            {
                // swap the values at the left and right indices
                var temp = data[left];
                data[left] = data[right];
                data[right] = temp;
            }
        }
    }

    public static int PointDirection(int x, int y, int tX, int tY)
    {
        var angle = (int) (Math.Atan2(tY - y, tX - x) * 0.0174532925199433D);

        return angle < 0 ? angle + 360 : angle;
    }
}