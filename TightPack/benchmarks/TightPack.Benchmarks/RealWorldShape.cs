using System.Buffers;
using System.Diagnostics;
using MemoryPack;
using TightPack;

namespace TightPack.Benchmarks;

// Models the real CarFrame: tuples whose elements are a provider-resolved struct (fix64), not a
// scalar. Those cannot be inlined the way a tuple of longs can.

[TightPackable]
public partial struct TpFix
{
    public long Raw;
}

[TightPackable]
public partial struct TpRealFrame
{
    public (TpFix X, TpFix Y, TpFix Z) Position;
    public (TpFix Xz, TpFix Pxy, TpFix Pzy) Rotation;
    public (TpFix A, TpFix B, TpFix C, TpFix D) Wheels;
    public TpFix Power;
    public int Damage;
    public (TpFix U, TpFix D, TpFix L, TpFix R) Angular;
    public (byte Lap, ushort Checkpoint) RacePosition;
    public TpFix Powerup;
    public TpFix Speed;
    public (TpFix Mxz, TpFix Txz) Readings;
    public uint Flags;
    public (TpFix Cxz, TpFix StaticXz) Camera;
}

[MemoryPackable]
public partial struct MpFix
{
    public long Raw;
}

[MemoryPackable]
public partial struct MpRealFrame
{
    public (MpFix X, MpFix Y, MpFix Z) Position;
    public (MpFix Xz, MpFix Pxy, MpFix Pzy) Rotation;
    public (MpFix A, MpFix B, MpFix C, MpFix D) Wheels;
    public MpFix Power;
    public int Damage;
    public (MpFix U, MpFix D, MpFix L, MpFix R) Angular;
    public (byte Lap, ushort Checkpoint) RacePosition;
    public MpFix Powerup;
    public MpFix Speed;
    public (MpFix Mxz, MpFix Txz) Readings;
    public uint Flags;
    public (MpFix Cxz, MpFix StaticXz) Camera;
}

public static class RealWorldShape
{
    private static BenchBufferWriter Writer = new();

    private static readonly TpRealFrame Tp = new()
    {
        Position = (new TpFix { Raw = 1 }, new TpFix { Raw = 2 }, new TpFix { Raw = 3 }),
        Rotation = (new TpFix { Raw = 4 }, new TpFix { Raw = 5 }, new TpFix { Raw = 6 }),
        Wheels = (new TpFix { Raw = 7 }, new TpFix { Raw = 8 }, new TpFix { Raw = 9 }, new TpFix { Raw = 10 }),
        Power = new TpFix { Raw = 11 }, Damage = 12,
        Angular = (new TpFix { Raw = 13 }, new TpFix { Raw = 14 }, new TpFix { Raw = 15 }, new TpFix { Raw = 16 }),
        RacePosition = (1, 2), Powerup = new TpFix { Raw = 17 }, Speed = new TpFix { Raw = 18 },
        Readings = (new TpFix { Raw = 19 }, new TpFix { Raw = 20 }), Flags = 0xFFFF,
        Camera = (new TpFix { Raw = 21 }, new TpFix { Raw = 22 }),
    };

    private static readonly MpRealFrame Mp = new()
    {
        Position = (new MpFix { Raw = 1 }, new MpFix { Raw = 2 }, new MpFix { Raw = 3 }),
        Rotation = (new MpFix { Raw = 4 }, new MpFix { Raw = 5 }, new MpFix { Raw = 6 }),
        Wheels = (new MpFix { Raw = 7 }, new MpFix { Raw = 8 }, new MpFix { Raw = 9 }, new MpFix { Raw = 10 }),
        Power = new MpFix { Raw = 11 }, Damage = 12,
        Angular = (new MpFix { Raw = 13 }, new MpFix { Raw = 14 }, new MpFix { Raw = 15 }, new MpFix { Raw = 16 }),
        RacePosition = (1, 2), Powerup = new MpFix { Raw = 17 }, Speed = new MpFix { Raw = 18 },
        Readings = (new MpFix { Raw = 19 }, new MpFix { Raw = 20 }), Flags = 0xFFFF,
        Camera = (new MpFix { Raw = 21 }, new MpFix { Raw = 22 }),
    };

    public static void Run()
    {
        int tpLen = TightPackSerializer.Serialize(in Tp).Length;
        int mpLen = MemoryPackSerializer.Serialize(Mp).Length;
        Console.WriteLine($"CarFrame shape (tuples of a struct, not scalars): tightpack={tpLen} memorypack={mpLen}");
        Console.WriteLine();

        double mpNs = Measure(() => MemoryPackSerializer.Serialize(Mp).Length, 2_000_000);
        double tpNs = Measure(() => TightPackSerializer.Serialize(in Tp).Length, 2_000_000);
        Console.WriteLine($"  to byte[]            memorypack={mpNs,7:F1}ns  tightpack={tpNs,7:F1}ns  {tpNs / mpNs:F2}x");

        double mpBuf = Measure(
            () => { Writer.Reset(); MemoryPackSerializer.Serialize(ref Writer, in Mp); return Writer.Length; },
            2_000_000
        );
        double tpBuf = Measure(
            () => { Writer.Reset(); TightPackSerializer.Serialize(ref Writer, in Tp); return Writer.Length; },
            2_000_000
        );
        Console.WriteLine($"  into buffer          memorypack={mpBuf,7:F1}ns  tightpack={tpBuf,7:F1}ns  {tpBuf / mpBuf:F2}x");

        byte[] mpBytes = MemoryPackSerializer.Serialize(Mp);
        byte[] tpBytes = TightPackSerializer.Serialize(in Tp);
        double mpDe = Measure(() => MemoryPackSerializer.Deserialize<MpRealFrame>(mpBytes).Damage, 2_000_000);
        double tpDe = Measure(() => TightPackSerializer.Deserialize<TpRealFrame>(tpBytes).Damage, 2_000_000);
        Console.WriteLine($"  deserialize          memorypack={mpDe,7:F1}ns  tightpack={tpDe,7:F1}ns  {tpDe / mpDe:F2}x");
    }

    private static double Measure<T>(Func<T> action, int iterations)
    {
        var sink = new object?[1];
        for (int i = 0; i < Math.Max(2000, iterations / 10); i++)
        {
            sink[0] = action();
        }

        GC.Collect();
        var samples = new double[5];
        for (int s = 0; s < samples.Length; s++)
        {
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++)
            {
                sink[0] = action();
            }

            long end = Stopwatch.GetTimestamp();
            samples[s] = (end - start) * (1_000_000_000.0 / Stopwatch.Frequency) / iterations;
        }

        GC.KeepAlive(sink);
        Array.Sort(samples);
        return samples[samples.Length / 2];
    }
}
