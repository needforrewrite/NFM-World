using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// The gamemode's full current state, sent once to a client that just attached and therefore
/// missed every event broadcast before it. Distinct from <see cref="S2C_ServerEvent"/> because
/// the client replaces its state with this rather than applying it as a delta, and because the
/// host has to send it before the first delta reaches that connection.
/// </summary>
[MemoryPackable]
[PacketServerToClient(-10)]
public partial struct S2C_ServerState : IPacketServerToClient<S2C_ServerState>, IDisposable
{
    [MemoryPackOrder(0)] [ReadOnlyMemoryPoolFormatterAttribute<byte>]
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
