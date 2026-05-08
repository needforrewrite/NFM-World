using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer.packets.c2s;
using NFMWorldLibrary.Multiplayer.packets.s2c;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseMultiplayerClientTransport : IMultiplayerClientTransport
{
    private ConcurrentQueue<IPacketServerToClient> _receivedPacketQueue = new();
    public ClientState State { get; protected set; } = ClientState.Connecting;
    
    protected abstract void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable);

    public IPacketServerToClient[] GetNewPackets()
    {
        var packets = new List<IPacketServerToClient>();
        while (_receivedPacketQueue.TryDequeue(out var packet))
        {
            packets.Add(packet);
        }
        return packets.Count > 0 ? packets.ToArray() : [];
    }

    protected void ReceivePacket(Memory<byte> memory)
    {
        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        if (MultiplayerUtils.TryDeserializeS2CPacket(opcode, message) is {} packet)
        {
            _receivedPacketQueue.Enqueue(packet);
        }
        else
        {
            Logging.Info($"Client received a message with unknown opcode {opcode}");
        }
    }

    public void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>
    {
        using var arr = new ArrayPoolBufferWriter<byte>();
        arr.Write(MultiplayerUtils.OpcodesC2SReverse[typeof(T)]);
        packet.Write(arr);

        SendRawPacketToServer(arr.WrittenSpan, reliable);
    }

    public abstract void Stop();
}