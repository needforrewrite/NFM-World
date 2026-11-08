using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using MemoryPack;

namespace NFMWorldLibrary.Util;

public interface IReadableWritable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] out TSelf>
    where TSelf : IMemoryPackable<TSelf>
{
    void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        MemoryPackSerializer.Serialize<TSelf, T>(writer, (TSelf)this, MemoryPackHelpers.Options);
    }

    public static virtual TSelf Read(ReadOnlyMemory<byte> data)
    {
        return MemoryPackSerializer.Deserialize<TSelf>(data.Span, MemoryPackHelpers.Options)!;
    }
}
