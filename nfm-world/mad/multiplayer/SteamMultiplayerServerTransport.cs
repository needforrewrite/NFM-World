using System.Collections;
using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorld.Util;
using Steamworks;
using Steamworks.Data;

namespace NFMWorld.Mad;

public class SteamMultiplayerServerTransport : IMultiplayerServerTransport, ISocketManager
{
    private readonly SocketManager _server;
    private bool _isRunning = true;
    private readonly Thread _receiveThread;

    public IReadOnlyCollection<uint> Connections { get; }

    private class ConnectionsList(SteamMultiplayerServerTransport parent) : IReadOnlyCollection<uint>
    {
        public IEnumerator<uint> GetEnumerator()
        {
            foreach (var client in parent.ConnectedClients)
            {
                yield return client.Key;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => parent.ConnectedClients.Count;
    }

    public event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
    public event EventHandler<uint>? ClientConnecting;
    public event EventHandler<uint>? ClientConnected;
    public event EventHandler<uint>? ClientDisconnected;

    public SteamMultiplayerServerTransport(int virtualport = 0)
    {
        Connections = new ConnectionsList(this);
        _server = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(virtualport);
        _server.Interface = this;
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
        Console.WriteLine($"SteamID: {SteamClient.SteamId}");
    }

    private void ReceiveLoop()
    {
        while (_isRunning)
        {
            _server.Receive();
        }
    }

    public ConcurrentDictionary<uint, Connection> ConnectedClients { get; } = [];
    
    public void OnConnecting(Connection connection, ConnectionInfo info)
    {
        connection.Accept();
        ClientConnecting?.Invoke(this, connection.Id);
        Console.WriteLine($"{info.Identity} is connecting");
    }

    public void OnConnected(Connection connection, ConnectionInfo info)
    {
        Console.WriteLine($"{info.Identity} has joined the game");
        ConnectedClients.TryAdd(connection, connection);
    }

    public void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        ConnectedClients.TryRemove(connection, out _);
        Console.WriteLine($"{info.Identity} has left the game");
    }

    public unsafe void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        using var messageData = new PointerMemoryManager<byte>((void*)data, size);

        var memory = messageData.Memory;
        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        if (MultiplayerUtils.TryDeserializeC2SPacket(opcode, message) is { } packet)
        {
            PacketReceived?.Invoke(this, (connection.Id, packet));
        }
        else
        {
            Console.WriteLine($"{identity} has received a message with unknown opcode {opcode}");
        }
    }

    public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        if (ConnectedClients.TryGetValue(clientIndex, out var connection))
        {
            using var arrayWriter = new ArrayPoolBufferWriter<byte>();
            arrayWriter.Write(MultiplayerUtils.OpcodesS2CReverse[typeof(T)]);
            packet.Write(arrayWriter);
            connection.SendMessage(arrayWriter.WrittenSpan, reliable ? SendType.Reliable : SendType.Unreliable);
        }
    }
    public void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(MultiplayerUtils.OpcodesS2CReverse[typeof(T)]);
        packet.Write(arrayWriter);
        var messageSpan = arrayWriter.WrittenSpan;
        
        foreach (var clientIndex in clientIndices)
        {
            if (ConnectedClients.TryGetValue(clientIndex, out var connection))
            {
                connection.SendMessage(messageSpan, reliable ? SendType.Reliable : SendType.Unreliable);
            }
        }
    }

    public void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>
    {
        using var arrayWriter = new ArrayPoolBufferWriter<byte>();
        arrayWriter.Write(MultiplayerUtils.OpcodesS2CReverse[typeof(T)]);
        packet.Write(arrayWriter);
        var messageSpan = arrayWriter.WrittenSpan;

        foreach (var (id, connection) in ConnectedClients)
        {
            connection.SendMessage(messageSpan, reliable ? SendType.Reliable : SendType.Unreliable);
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _server.Close();
    }
}