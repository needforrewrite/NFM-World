using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

if (args.Length < 1)
{
    Console.WriteLine("Usage: TraceAnalyzer <trace.nettrace> [topN]");
    return;
}

var tracePath = args[0];
var topN = args.Length > 1 ? int.Parse(args[1]) : 20;

var etlxPath = TraceLog.CreateFromEventPipeDataFile(tracePath);
using var traceLog = TraceLog.OpenOrConvert(etlxPath);

// Pass 1: TypeID -> TypeName, from the Type/BulkType events.
var typeNames = new Dictionary<ulong, string>();
foreach (var evt in traceLog.Events)
{
    if (evt is GCBulkTypeTraceData bulk)
    {
        for (var i = 0; i < bulk.Count; i++)
        {
            var v = bulk.Values(i);
            typeNames[v.TypeID] = v.TypeName;
        }
    }
}

Console.WriteLine($"(resolved {typeNames.Count:N0} type names)");

// Pass 2: aggregate sampled allocations by type and by (type, stack).
var byType = new Dictionary<string, (long count, long bytes)>();
var byStack = new Dictionary<string, (long count, long bytes)>();

foreach (var evt in traceLog.Events)
{
    if (evt is not GCSampledObjectAllocationTraceData alloc)
    {
        continue;
    }

    var typeName = typeNames.TryGetValue((ulong)alloc.TypeID, out var n) ? n : $"TypeID:0x{alloc.TypeID:X}";
    var bytes = alloc.TotalSizeForTypeSample;

    byType.TryGetValue(typeName, out var t);
    byType[typeName] = (t.count + alloc.ObjectCountForTypeSample, t.bytes + bytes);

    var stack = evt.CallStack();
    if (stack is null)
    {
        continue;
    }

    var frames = new List<string>();
    var cur = stack;
    var depth = 0;
    while (cur != null && depth < 14)
    {
        frames.Add(cur.CodeAddress?.FullMethodName ?? "?");
        cur = cur.Caller;
        depth++;
    }

    var key = typeName + "\n      " + string.Join("\n   <- ", frames);
    byStack.TryGetValue(key, out var s);
    byStack[key] = (s.count + alloc.ObjectCountForTypeSample, s.bytes + bytes);
}

var grandTotal = byType.Values.Sum(v => v.bytes);
Console.WriteLine($"=== Top {topN} types by sampled allocation bytes (total {grandTotal:N0}) ===");
foreach (var (name, (count, bytes)) in byType.OrderByDescending(kv => kv.Value.bytes).Take(topN))
{
    Console.WriteLine($"  {bytes,14:N0} bytes ({bytes * 100.0 / grandTotal,5:F1}%)  x{count,8:N0}  {name}");
}

Console.WriteLine();
Console.WriteLine($"=== Top {topN} allocation stacks by sampled bytes ===");
var rank = 0;
foreach (var (key, (count, bytes)) in byStack.OrderByDescending(kv => kv.Value.bytes).Take(topN))
{
    rank++;
    Console.WriteLine($"  #{rank}: {bytes,14:N0} bytes ({bytes * 100.0 / grandTotal,5:F1}%)  x{count,8:N0} objects");
    Console.WriteLine($"      {key}");
    Console.WriteLine();
}
