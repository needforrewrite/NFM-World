using System.Runtime.CompilerServices;

namespace NFMWorld.Util;

public static class SafeMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Abs(int value) => value >= 0 ? value : (value == int.MinValue ? int.MaxValue : -value);
}