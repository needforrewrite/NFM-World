using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using TightPack;

namespace NFMWorldLibrary.Util;

public interface IReadableWritable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] out TSelf>
    where TSelf : ITightPackable<TSelf>
{
    void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        TightPackSerializer.Serialize<TSelf, T>(ref writer, (TSelf)this);
    }

    public static virtual TSelf Read(ReadOnlyMemory<byte> data)
    {
        return TightPackSerializer.Deserialize<TSelf>(data.Span)!;
    }
}
