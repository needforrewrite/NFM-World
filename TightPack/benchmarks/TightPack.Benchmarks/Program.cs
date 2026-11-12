using System.Buffers;
using System.Diagnostics;
using MemoryPack;
using TightPack;

namespace TightPack.Benchmarks;

// Shapes are declared twice, once per library, because the attributes differ. Every pair is the
// same type, member for member.

[TightPackable]
public partial struct TpBlittable
{
    public int X;
    public float Y;
}

[MemoryPackable]
public partial struct MpBlittable
{
    public int X;
    public float Y;
}

/// <summary>Needs padding in memory. MemoryPack blits unmanaged structs as raw bytes, so this is
/// where its wire format and TightPack's deliberately diverge.</summary>
[TightPackable]
public partial struct TpPadded
{
    public byte A;
    public long B;
}

[MemoryPackable]
public partial struct MpPadded
{
    public byte A;
    public long B;
}

/// <summary>Shaped like <c>CarFrame</c>, the real per-tick payload.</summary>
[TightPackable]
public partial struct TpFrame
{
    public (long X, long Y, long Z) Position;
    public (long Xz, long Pxy, long Pzy) Rotation;
    public long Power;
    public int Damage;
    public (long U, long D, long L, long R) Angular;
    public (byte Lap, ushort Checkpoint) RacePosition;
    public long Powerup;
    public long Speed;
    public (long Mxz, long Txz) Readings;
    public uint Flags;
    public (long Cxz, long StaticXz) Camera;
}

[MemoryPackable]
public partial struct MpFrame
{
    public (long X, long Y, long Z) Position;
    public (long Xz, long Pxy, long Pzy) Rotation;
    public long Power;
    public int Damage;
    public (long U, long D, long L, long R) Angular;
    public (byte Lap, ushort Checkpoint) RacePosition;
    public long Powerup;
    public long Speed;
    public (long Mxz, long Txz) Readings;
    public uint Flags;
    public (long Cxz, long StaticXz) Camera;
}

[TightPackable]
public partial class TpObject
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public double Score { get; set; }
}

[MemoryPackable]
public partial class MpObject
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public double Score { get; set; }
}

[TightPackable]
public partial class TpPacket
{
    public uint Ticks { get; set; }
    public TpFrame[]? Frames { get; set; }
}

[MemoryPackable]
public partial class MpPacket
{
    public uint Ticks { get; set; }
    public MpFrame[]? Frames { get; set; }
}

public sealed class BenchBufferWriter : IBufferWriter<byte>
{
    public readonly byte[] Buffer = new byte[256 * 1024];
    public int Length;

    public void Reset() => Length = 0;

    public void Advance(int count) => Length += count;

    public Memory<byte> GetMemory(int sizeHint = 0) => Buffer.AsMemory(Length);

    public Span<byte> GetSpan(int sizeHint = 0) => Buffer.AsSpan(Length);
}

internal sealed class Row
{
    public required string Name { get; init; }
    public required double MemoryPackNs { get; init; }
    public required double TightPackNs { get; init; }
    public required long MemoryPackAlloc { get; init; }
    public required long TightPackAlloc { get; init; }
}

public static class Program
{
    public static void Main(string[] args)
    {
        Comparison.Run();
        Console.WriteLine();
        RealWorldShape.Run();
    }
}

internal static class S
{
    public static readonly TpBlittable TpBlittable = new() { X = 42, Y = 1.5f };
    public static readonly MpBlittable MpBlittable = new() { X = 42, Y = 1.5f };
    public static readonly TpPadded TpPadded = new() { A = 7, B = 1234567890123 };
    public static readonly MpPadded MpPadded = new() { A = 7, B = 1234567890123 };

    public static readonly TpFrame TpFrame = new()
    {
        Position = (1, 2, 3), Rotation = (4, 5, 6), Power = 7, Damage = 8,
        Angular = (9, 10, 11, 12), RacePosition = (1, 2), Powerup = 13, Speed = 14,
        Readings = (15, 16), Flags = 0xFFFF, Camera = (17, 18),
    };

    public static readonly MpFrame MpFrame = new()
    {
        Position = (1, 2, 3), Rotation = (4, 5, 6), Power = 7, Damage = 8,
        Angular = (9, 10, 11, 12), RacePosition = (1, 2), Powerup = 13, Speed = 14,
        Readings = (15, 16), Flags = 0xFFFF, Camera = (17, 18),
    };

    public static readonly TpObject TpObject = new() { Id = 9, Name = "nfm-world", Score = 3.14159 };
    public static readonly MpObject MpObject = new() { Id = 9, Name = "nfm-world", Score = 3.14159 };

    public static readonly TpPacket TpPacket = new() { Ticks = 12345, Frames = MakeTpFrames() };
    public static readonly MpPacket MpPacket = new() { Ticks = 12345, Frames = MakeMpFrames() };

    public static readonly byte[] TpBlittableBytes = TightPackSerializer.Serialize(in TpBlittable);
    public static readonly byte[] MpBlittableBytes = MemoryPackSerializer.Serialize(MpBlittable);
    public static readonly byte[] TpPaddedBytes = TightPackSerializer.Serialize(in TpPadded);
    public static readonly byte[] MpPaddedBytes = MemoryPackSerializer.Serialize(MpPadded);
    public static readonly byte[] TpFrameBytes = TightPackSerializer.Serialize(in TpFrame);
    public static readonly byte[] MpFrameBytes = MemoryPackSerializer.Serialize(MpFrame);
    public static readonly byte[] TpObjectBytes = TightPackSerializer.Serialize(in TpObject);
    public static readonly byte[] MpObjectBytes = MemoryPackSerializer.Serialize(MpObject);
    public static readonly byte[] TpPacketBytes = TightPackSerializer.Serialize(in TpPacket);
    public static readonly byte[] MpPacketBytes = MemoryPackSerializer.Serialize(MpPacket);

    private static TpFrame[] MakeTpFrames()
    {
        var frames = new TpFrame[63];
        Array.Fill(frames, TpFrame);
        return frames;
    }

    private static MpFrame[] MakeMpFrames()
    {
        var frames = new MpFrame[63];
        Array.Fill(frames, MpFrame);
        return frames;
    }
}
