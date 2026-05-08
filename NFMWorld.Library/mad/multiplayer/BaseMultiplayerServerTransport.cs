using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer.packets.c2s;
using NFMWorldLibrary.Multiplayer.packets.s2c;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseMultiplayerServerTransport : IMultiplayerServerTransport
{
    public abstract IReadOnlyCollection<uint> Connections { get; }
    
    public event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
    public abstract event EventHandler<uint>? ClientConnecting;
    public abstract event EventHandler<uint>? ClientConnected;
    public abstract event EventHandler<uint>? ClientDisconnected;
    
    public abstract void SendRawPacketToClients(ReadOnlySpan<uint> clientIndices, ReadOnlySpan<byte> span, bool reliable);

    public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(MultiplayerUtils.OpcodesS2CReverse[typeof(T)]);
        packet.Write(arrayWriter);
        SendRawPacketToClients([clientIndex], arrayWriter.WrittenSpan, reliable);
    }

    public void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(MultiplayerUtils.OpcodesS2CReverse[typeof(T)]);
        packet.Write(arrayWriter);
        SendRawPacketToClients(clientIndices, arrayWriter.WrittenSpan, reliable);
    }

    public void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(MultiplayerUtils.OpcodesS2CReverse[typeof(T)]);
        packet.Write(arrayWriter);
        SendRawPacketToClients(Connections.ToArray(), arrayWriter.WrittenSpan, reliable);
    }

    protected void ReceivePacket(uint clientIndex, Memory<byte> memory)
    {
        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        if (MultiplayerUtils.TryDeserializeC2SPacket(opcode, message) is { } packet)
        {
            PacketReceived?.Invoke(this, (clientIndex, packet));
        }
        else
        {
            Logging.Info($"Client {clientIndex} has received a message with unknown opcode {opcode}");
        }
    }

    public abstract void Stop();
    public abstract void Start();
}