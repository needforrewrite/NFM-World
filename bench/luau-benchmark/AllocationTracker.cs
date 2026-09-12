using System.Diagnostics.Tracing;

namespace LuauBenchmark;

/// <summary>
/// Aggregates CLR GCAllocationTick events (fired roughly every ~100KB allocated
/// per thread) by type name, giving a statistically-sampled breakdown of "what's
/// actually allocating" without needing an external dotnet-trace/PerfView pass.
/// </summary>
sealed class AllocationTracker : EventListener
{
    readonly Dictionary<string, (long count, long bytes)> stats = new();
    readonly object gate = new();

    public void Reset()
    {
        lock (gate)
        {
            stats.Clear();
        }
    }

    public void PrintReport(int top = 25)
    {
        lock (gate)
        {
            Console.WriteLine($"  Allocation samples by type (top {top}, GCAllocationTick-sampled):");
            var totalBytes = stats.Values.Sum(v => v.bytes);
            foreach (var (name, (count, bytes)) in stats.OrderByDescending(kv => kv.Value.bytes).Take(top))
            {
                var pct = totalBytes > 0 ? bytes * 100.0 / totalBytes : 0;
                Console.WriteLine($"    {bytes,12:N0} bytes ({pct,5:F1}%)  x{count,6:N0} samples  {name}");
            }

            Console.WriteLine($"    ({stats.Count} distinct types sampled, {totalBytes:N0} bytes total across samples)");
        }
    }

    public bool Debug;
    int debugCount;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        Console.WriteLine($"    [tracker] saw EventSource: {eventSource.Name}");
        if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
        {
            // AllocationSampled doesn't fire under keyword 0x1 (GC) alone in this
            // runtime version -- enabling everything is fine for a one-off diagnostic run.
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (Debug && debugCount < 5)
        {
            debugCount++;
            Console.WriteLine($"    [tracker] event: {e.EventName} payloadNames=[{string.Join(",", e.PayloadNames ?? [])}]");
        }

        if (e.EventName != "AllocationSampled")
        {
            return;
        }

        var payloadNames = e.PayloadNames;
        if (payloadNames is null)
        {
            return;
        }

        string? typeName = null;
        long amount = 0;
        for (var i = 0; i < payloadNames.Count; i++)
        {
            switch (payloadNames[i])
            {
                case "TypeName":
                    typeName = e.Payload?[i] as string;
                    break;
                case "ObjectSize":
                    amount = Convert.ToInt64(e.Payload?[i] ?? 0L);
                    break;
            }
        }

        typeName ??= "<unknown>";

        lock (gate)
        {
            stats.TryGetValue(typeName, out var existing);
            stats[typeName] = (existing.count + 1, existing.bytes + amount);
        }
    }
}
