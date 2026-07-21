using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[MemoryPackable]
[PacketServerToClient(-8)]
public partial struct S2C_ServerEvent : IPacketServerToClient<S2C_ServerEvent>, IDisposable
{
    [MemoryPackOrder(0)]
    [ReadOnlyMemoryPoolFormatterAttribute<byte>]
    public required ReadOnlyMemory<byte> Payload;

    private bool _usePool;

    [MemoryPackOnDeserialized]
    private void OnDeserialized()
    {
        _usePool = true;
    }

    public void Dispose()
    {
        if (!_usePool) return;

        Return(Payload); Payload = default;
    }

    private static void Return<T>(Memory<T> memory) => Return((ReadOnlyMemory<T>)memory);

    private static void Return<T>(ReadOnlyMemory<T> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out var segment) && segment.Array is { Length: > 0 })
        {
            ArrayPool<T>.Shared.Return(segment.Array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}
