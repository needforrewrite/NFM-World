using System.Runtime.CompilerServices;

namespace NFMWorldLibrary.Util;

public static class SafeMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Abs(int value) => value >= 0 ? value : (value == int.MinValue ? int.MaxValue : -value);
    public static float Abs(float value) => value >= 0 ? value : (value == float.MinValue ? float.MaxValue : -value);
}