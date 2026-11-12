using System.Buffers;
using System.Runtime.CompilerServices;

namespace TightPack;

/// <summary>
/// Helpers for the version-tolerant body, which needs a member's byte length before writing it.
/// Generated code rents one scratch buffer per object and reuses it for every member.
/// </summary>
public static class TightPackScratch
{
    public static TightPackBufferWriter Rent() => TightPackBufferWriter.Rent();

    public static void Return(TightPackBufferWriter scratch) => TightPackBufferWriter.Return(scratch);

    /// <summary>Writes the scratch payload with its varint length prefix and clears the scratch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Commit<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, TightPackBufferWriter scratch)
        where TBufferWriter : IBufferWriter<byte>
    {
        writer.WriteVarUInt32((uint)scratch.WrittenCount);
        writer.WriteBytes(scratch.WrittenSpan);
        scratch.Reset();
    }
}
