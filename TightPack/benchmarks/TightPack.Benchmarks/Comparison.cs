using System.Buffers;
using System.Diagnostics;
using MemoryPack;
using TightPack;

namespace TightPack.Benchmarks;

public static class Comparison
{
    private static BenchBufferWriter Writer = new();

    public static void Run()
    {
        ReportSizes();

        var rows = new List<Row>();

        Add(
            rows,
            "blittable struct",
            4_000_000,
            () => TightPackSerializer.Serialize(in S.TpBlittable).Length,
            () => MemoryPackSerializer.Serialize(S.MpBlittable).Length
        );
        Add(
            rows,
            "blittable deser",
            4_000_000,
            () => TightPackSerializer.Deserialize<TpBlittable>(S.TpBlittableBytes).X,
            () => MemoryPackSerializer.Deserialize<MpBlittable>(S.MpBlittableBytes).X
        );

        Add(
            rows,
            "padded struct",
            4_000_000,
            () => TightPackSerializer.Serialize(in S.TpPadded).Length,
            () => MemoryPackSerializer.Serialize(S.MpPadded).Length
        );
        Add(
            rows,
            "padded deser",
            2_000_000,
            () => TightPackSerializer.Deserialize<TpPadded>(S.TpPaddedBytes).A,
            () => MemoryPackSerializer.Deserialize<MpPadded>(S.MpPaddedBytes).A
        );

        Add(
            rows,
            "CarFrame-like struct",
            2_000_000,
            () => TightPackSerializer.Serialize(in S.TpFrame).Length,
            () => MemoryPackSerializer.Serialize(S.MpFrame).Length
        );
        Add(
            rows,
            "CarFrame-like deser",
            2_000_000,
            () => TightPackSerializer.Deserialize<TpFrame>(S.TpFrameBytes).Damage,
            () => MemoryPackSerializer.Deserialize<MpFrame>(S.MpFrameBytes).Damage
        );

        Add(
            rows,
            "object + string",
            1_000_000,
            () => TightPackSerializer.Serialize(in S.TpObject).Length,
            () => MemoryPackSerializer.Serialize(S.MpObject).Length
        );
        Add(
            rows,
            "object + string deser",
            1_000_000,
            () => TightPackSerializer.Deserialize<TpObject>(S.TpObjectBytes).Id,
            () => MemoryPackSerializer.Deserialize<MpObject>(S.MpObjectBytes).Id
        );

        Add(
            rows,
            "packet of 63 frames",
            60_000,
            () => TightPackSerializer.Serialize(in S.TpPacket).Length,
            () => MemoryPackSerializer.Serialize(S.MpPacket).Length
        );
        Add(
            rows,
            "packet of 63 frames deser",
            60_000,
            () => TightPackSerializer.Deserialize<TpPacket>(S.TpPacketBytes).Ticks,
            () => MemoryPackSerializer.Deserialize<MpPacket>(S.MpPacketBytes).Ticks
        );

        Add(
            rows,
            "CarFrame into buffer",
            2_000_000,
            () => { Writer.Reset(); TightPackSerializer.Serialize(ref Writer, in S.TpFrame); return Writer.Length; },
            () => { Writer.Reset(); MemoryPackSerializer.Serialize(ref Writer, in S.MpFrame); return Writer.Length; }
        );

        Add(
            rows,
            "packet into buffer",
            60_000,
            () => { Writer.Reset(); TightPackSerializer.Serialize(ref Writer, in S.TpPacket); return Writer.Length; },
            () => { Writer.Reset(); MemoryPackSerializer.Serialize(ref Writer, in S.MpPacket); return Writer.Length; }
        );

        Print(rows);
    }

    private static void ReportSizes()
    {
        Console.WriteLine("wire sizes (bytes)");
        SizeRow(
            "blittable struct",
            TightPackSerializer.Serialize(in S.TpBlittable).Length,
            MemoryPackSerializer.Serialize(S.MpBlittable).Length
        );
        SizeRow(
            "padded struct",
            TightPackSerializer.Serialize(in S.TpPadded).Length,
            MemoryPackSerializer.Serialize(S.MpPadded).Length
        );
        SizeRow(
            "CarFrame-like struct",
            TightPackSerializer.Serialize(in S.TpFrame).Length,
            MemoryPackSerializer.Serialize(S.MpFrame).Length
        );
        SizeRow(
            "object + string",
            TightPackSerializer.Serialize(in S.TpObject).Length,
            MemoryPackSerializer.Serialize(S.MpObject).Length
        );
        SizeRow(
            "packet of 63 frames",
            TightPackSerializer.Serialize(in S.TpPacket).Length,
            MemoryPackSerializer.Serialize(S.MpPacket).Length
        );
        Console.WriteLine();
    }

    private static void SizeRow(string label, int tightPack, int memoryPack) =>
        Console.WriteLine($"  {label,-24} tightpack={tightPack,6}  memorypack={memoryPack,6}");

    private static void Add<T>(
        List<Row> rows,
        string name,
        int iterations,
        Func<T> tightPack,
        Func<T> memoryPack
    )
    {
        double mpNs = Measure(memoryPack, iterations, out long mpAlloc);
        double tpNs = Measure(tightPack, iterations, out long tpAlloc);

        rows.Add(new Row
        {
            Name = name,
            MemoryPackNs = mpNs,
            TightPackNs = tpNs,
            MemoryPackAlloc = mpAlloc,
            TightPackAlloc = tpAlloc,
        });
    }

    private static double Measure<T>(Func<T> action, int iterations, out long bytesPerOp)
    {
        var sink = new object?[1];
        for (int i = 0; i < Math.Max(2000, iterations / 10); i++)
        {
            sink[0] = action();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var samples = new double[5];
        bytesPerOp = 0;
        for (int s = 0; s < samples.Length; s++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++)
            {
                sink[0] = action();
            }

            long end = Stopwatch.GetTimestamp();
            if (s == samples.Length - 1)
            {
                bytesPerOp = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
            }

            samples[s] = (end - start) * (1_000_000_000.0 / Stopwatch.Frequency) / iterations;
        }

        GC.KeepAlive(sink);
        Array.Sort(samples);
        return samples[samples.Length / 2];
    }

    private static void Print(List<Row> rows)
    {
        Console.WriteLine($"{"benchmark",-26} {"memorypack",10} {"tightpack",10} {"tp/mp",6} {"mp alloc",9} {"tp alloc",9}");
        Console.WriteLine(new string('-', 78));
        foreach (Row row in rows)
        {
            double ratio = row.TightPackNs / row.MemoryPackNs;
            Console.WriteLine(
                $"{row.Name,-26} {row.MemoryPackNs,8:F1}ns {row.TightPackNs,8:F1}ns {ratio,5:F2}x {row.MemoryPackAlloc,7}B {row.TightPackAlloc,7}B"
            );
        }
    }
}
