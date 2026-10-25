using System.Diagnostics;
using System.Threading;

namespace NFMWorldLibrary.Util;

/// <summary>
/// Cheap counters around <see cref="GameThreadContext.ExecutePendingTasks"/> -- the "GTC"
/// flush every deferred UI/signal update goes through once per frame. Reuses the method's
/// existing Stopwatch rather than adding a second one; read the fields directly (e.g.
/// snapshot before/after a window of interest) rather than treating this as a
/// general-purpose profiler.
/// </summary>
public static class GameThreadContextDiagnostics
{
    static long flushCount;
    static long elapsedTicks;

    public static long FlushCount => flushCount;
    public static double ElapsedMilliseconds => elapsedTicks * 1000.0 / Stopwatch.Frequency;

    public static void Reset()
    {
        Interlocked.Exchange(ref flushCount, 0);
        Interlocked.Exchange(ref elapsedTicks, 0);
    }

    [Conditional("LUA_VM_DIAGNOSTICS")]
    internal static void Record(long ticks)
    {
        Interlocked.Increment(ref flushCount);
        Interlocked.Add(ref elapsedTicks, ticks);
    }
}
