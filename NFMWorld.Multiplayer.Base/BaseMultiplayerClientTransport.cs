using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

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
        if (memory.IsEmpty)
        {
            Logging.Warning("Server sent an empty packet");
            return;
        }

        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        IPacketServerToClient? packet;
        try
        {
            packet = MultiplayerUtils.TryDeserializeS2CPacket(opcode, message);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Server sent an unreadable packet (opcode {opcode})", exception: ex);
            return;
        }

        if (packet is { } p)
        {
            _receivedPacketQueue.Enqueue(p);
        }
        else
        {
            Logging.Info($"Client received a message with unknown opcode {opcode}");
        }
    }

    public void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>
    {
        Logging.Debug($"Sending packet to server: {packet.GetType().Name}");
        
        using var arr = new ArrayPoolBufferWriter<byte>();
        arr.Write(packet.Opcode);
        packet.Write(arr);

        SendRawPacketToServer(arr.WrittenSpan, reliable);
    }

    public abstract void Stop();
}