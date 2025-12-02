namespace NFMWorld.Util;

public static class UMath
{
    public static int SafeAbs(int value)
    {
        return value == int.MinValue ? int.MaxValue : Math.Abs(value);
    }
}