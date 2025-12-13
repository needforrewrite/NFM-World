namespace NFMWorld.Util;

public class MicroStopwatch : System.Diagnostics.Stopwatch
{
    readonly double _microSecPerTick = 1000000D / Frequency;

    public MicroStopwatch()
    {
        if (!IsHighResolution)
        {
            throw new Exception("On this system the high-resolution performance counter is not available");
        }
    }

    public long ElapsedMicroseconds => (long)(ElapsedTicks * _microSecPerTick);
}