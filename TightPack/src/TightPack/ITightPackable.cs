using System.Buffers;

namespace TightPack;

/// <summary>
/// Implemented by generated <c>[TightPackable]</c> types. The two members are static and generic
/// so a call site is a direct, devirtualised call rather than a formatter lookup.
/// </summary>
public interface ITightPackable<TSelf>
{
    /// <summary>Writes <paramref name="value"/>. <paramref name="value"/> is never modified.</summary>
    static abstract void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in TSelf value)
        where TBufferWriter : IBufferWriter<byte>;

    /// <summary>Replaces <paramref name="value"/> with the payload read from <paramref name="reader"/>.</summary>
    static abstract void Deserialize(ref TightPackReader reader, ref TSelf value);
}
