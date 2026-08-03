namespace NFMWorldLibrary.FixedMath;

using FixedMathSharp;

public static partial class FixedTrigLUT
{
    public static fix64 SinDeg(fix64 value)
    {
        // wrap to 0-360 degrees
        var degrees = value % 360;
        if (degrees < fix64.Zero)
            degrees += 360;
        // scale to 0-36000, interpolate with fractional part
        var scaled = degrees * 100;
        var index = (int)(scaled.rawValue >> FixedMath.SHIFT_AMOUNT_I); // integer part
        var frac = scaled - fix64.FromRaw((long)index << FixedMath.SHIFT_AMOUNT_I); // fractional part
        var nextIndex = index + 1;
        if (nextIndex >= 36000)
            nextIndex = 0;
        var sinA = fix64.FromRaw(SinTable[index]);
        var sinB = fix64.FromRaw(SinTable[nextIndex]);
        var delta = sinB - sinA;
        return sinA + (delta * frac);
    }
    
    public static fix64 CosDeg(fix64 value)
    {
        return SinDeg(value + 90);
    }
    
    public static void SinCosDeg(fix64 value, out fix64 sinValue, out fix64 cosValue)
    {
        // wrap to 0-360 degrees
        var degrees = value % 360;
        if (degrees < fix64.Zero)
            degrees += 360;
        // scale to 0-36000, interpolate with fractional part
        var scaled = degrees * 100;
        var index = (int)(scaled.rawValue >> FixedMath.SHIFT_AMOUNT_I); // integer part
        var frac = scaled - fix64.FromRaw((long)index << FixedMath.SHIFT_AMOUNT_I); // fractional part
        var nextIndex = index + 1;
        if (nextIndex >= 36000)
            nextIndex = 0;
        var sinA = fix64.FromRaw(SinTable[index]);
        var sinB = fix64.FromRaw(SinTable[nextIndex]);
        var deltaSin = sinB - sinA;
        sinValue = sinA + (deltaSin * frac);
        
        var cosIndex = index + 9000;
        if (cosIndex >= 36000)
            cosIndex -= 36000;
        var cosNextIndex = cosIndex + 1;
        if (cosNextIndex >= 36000)
            cosNextIndex = 0;
        var cosA = fix64.FromRaw(SinTable[cosIndex]);
        var cosB = fix64.FromRaw(SinTable[cosNextIndex]);
        var deltaCos = cosB - cosA;
        cosValue = cosA + (deltaCos * frac);
    }
}