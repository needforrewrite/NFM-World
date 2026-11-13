using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using TightPack;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseMultiplayerServerTransport : IMultiplayerServerTransport
{
    /// <summary>
    /// Connections that have declared a matching wire version. Keyed by connection index, so a
    /// transport that reuses an index after a disconnect has to call <see cref="ForgetConnection"/>.
    /// </summary>
    private readonly ConcurrentDictionary<uint, byte> _handshaked = new();

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

    /// <summary>
    /// Drops the protocol state held for a connection index. Call it wherever the subclass
    /// raises <c>ClientDisconnected</c>; a transport that reuses indices (ENet does) would
    /// otherwise let the next connection inherit a handshake that never happened.
    /// </summary>
    protected void ForgetConnection(uint clientIndex) => _handshaked.TryRemove(clientIndex, out _);

    public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(packet.Opcode);
        packet.Write(arrayWriter);
        SendRawPacketToClients([clientIndex], arrayWriter.WrittenSpan, reliable);
    }

    public void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true)
        where T : IPacketServerToClient<T>
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

    protected void ReceivePacket(uint clientIndex, ReadOnlyMemory<byte> memory)
    {
        // An empty datagram has no opcode; remote input, so it must not throw here.
        if (memory.IsEmpty)
        {
            Logging.Warning($"Client {clientIndex} sent an empty packet");
            return;
        }

        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        // The version is settled before anything else is looked at, so no handler ever sees a
        // packet from a build whose layout it does not share.
        if (opcode == Protocol.ClientHandshakeOpcode)
        {
            HandleHandshake(clientIndex, message);
            return;
        }

        if (!_handshaked.ContainsKey(clientIndex))
        {
            Logging.Warning($"Client {clientIndex} sent opcode {opcode} before declaring its version");
            RefuseProtocol(
                clientIndex,
                "This connection did not declare its game version. Update the game and try again."
            );
            return;
        }

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

    private void HandleHandshake(uint clientIndex, ReadOnlyMemory<byte> message)
    {
        int version;
        try
        {
            version = TightPackSerializer.Deserialize<C2S_Handshake>(message.Span).Version;
        }
        catch (Exception ex)
        {
            Logging.Warning($"Client {clientIndex} sent an unreadable handshake", exception: ex);
            RefuseProtocol(clientIndex, "This connection sent an unreadable handshake.");
            return;
        }

        if (version != Protocol.Version)
        {
            Logging.Warning(
                $"Client {clientIndex} runs protocol {version}; this server runs {Protocol.Version}"
            );
            RefuseProtocol(
                clientIndex,
                $"This server runs game version {Protocol.Version} and your build is version {version}. "
                + "Update the game and try again."
            );
            return;
        }

        _handshaked[clientIndex] = 0;
        SendPacketToClient(
            clientIndex,
            new S2C_Handshake { Version = Protocol.Version, Accepted = true }
        );
    }

    /// <summary>
    /// Tells the peer which version to expect, then closes it. The answer goes out first so a
    /// client that reads it can name the mismatch instead of retrying forever.
    /// </summary>
    private void RefuseProtocol(uint clientIndex, string error)
    {
        _handshaked.TryRemove(clientIndex, out _);
        SendPacketToClient(
            clientIndex,
            new S2C_Handshake { Version = Protocol.Version, Accepted = false, Error = error }
        );
        DisconnectClient(clientIndex);
    }

    public abstract void Stop();
    public abstract void Start();
}
