using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseMultiplayerServerTransport : IMultiplayerServerTransport
{
    public abstract IReadOnlyCollection<uint> Connections { get; }
    
    public event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
    public abstract event EventHandler<uint>? ClientConnecting;
    public abstract event EventHandler<uint>? ClientConnected;
    public abstract event EventHandler<uint>? ClientDisconnected;
    
    public abstract void SendRawPacketToClients(ReadOnlySpan<uint> clientIndices, ReadOnlySpan<byte> span, bool reliable);

    /// <summary>Closes one client's connection. Defaults to dropping it from the write path.</summary>
    public virtual void DisconnectClient(uint clientIndex)
    {
    }

    public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(packet.Opcode);
        packet.Write(arrayWriter);
        SendRawPacketToClients([clientIndex], arrayWriter.WrittenSpan, reliable);
    }

    public void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(packet.Opcode);
        packet.Write(arrayWriter);
        SendRawPacketToClients(clientIndices, arrayWriter.WrittenSpan, reliable);
    }

    public void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(packet.Opcode);
        packet.Write(arrayWriter);
        SendRawPacketToClients(Connections.ToArray(), arrayWriter.WrittenSpan, reliable);
    }

    protected void ReceivePacket(uint clientIndex, Memory<byte> memory)
    {
        // An empty datagram has no opcode; remote input, so it must not throw here.
        if (memory.IsEmpty)
        {
            Logging.Warning($"Client {clientIndex} sent an empty packet");
            return;
        }

        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        IPacketClientToServer? packet;
        try
        {
            packet = MultiplayerUtils.TryDeserializeC2SPacket(opcode, message);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Client {clientIndex} sent an unreadable packet (opcode {opcode})", exception: ex);
            return;
        }

        if (packet is { } p)
        {
            // A throwing handler must not take down the receive thread for every client.
            try
            {
                PacketReceived?.Invoke(this, (clientIndex, p));
            }
            catch (Exception ex)
            {
                Logging.Error($"Error handling packet from client {clientIndex}", exception: ex);
            }
        }
        else
        {
            Logging.Info($"Client {clientIndex} has received a message with unknown opcode {opcode}");
        }
    }

    public abstract void Stop();
    public abstract void Start();
}