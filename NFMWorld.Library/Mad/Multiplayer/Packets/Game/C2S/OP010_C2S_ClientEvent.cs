using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NFMWorldLibrary.Serialization;
using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(10)]
public partial struct C2S_ClientEvent : IPacketClientToServer<C2S_ClientEvent>, IDisposable
{
    [TightPackOrder(0)]
    [TightPackCustomFormatter(typeof(PooledPayloadFormatter))] public required ReadOnlyMemory<byte> Payload;

    private bool _usePool;

    [TightPackOnDeserialized]
    private void OnDeserialized()
    {
        _usePool = true;
    }

    public void Dispose()
    {
        if (!_usePool) return;

        Return(Payload);
        Payload = default;
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
