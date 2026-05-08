using System.Collections;
using System.Collections.Concurrent;
using Maxine.Extensions;
using Steamworks;
using Steamworks.Data;

namespace NFMWorldLibrary.Multiplayer;

public class SteamMultiplayerServerTransport : BaseMultiplayerServerTransport, ISocketManager
{
    private readonly SocketManager _server;
    private bool _isRunning = true;
    private Thread _receiveThread;
    private readonly ConcurrentDictionary<uint, Connection> _connectedClients = [];

    public override IReadOnlyCollection<uint> Connections { get; }
    
    public override event EventHandler<uint>? ClientConnecting;
    public override event EventHandler<uint>? ClientConnected;
    public override event EventHandler<uint>? ClientDisconnected;

    private class ConnectionsList(SteamMultiplayerServerTransport parent) : IReadOnlyCollection<uint>
    {
        public IEnumerator<uint> GetEnumerator()
        {
            foreach (var client in parent._connectedClients)
            {
                yield return client.Key;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => parent._connectedClients.Count;
    }

    public SteamMultiplayerServerTransport(int virtualport = 0)
    {
        Connections = new ConnectionsList(this);
        _server = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(virtualport);
        _server.Interface = this;
        Logging.Info($"SteamID: {SteamClient.SteamId}");
    }

    private void ReceiveLoop()
    {
        while (_isRunning)
        {
            _server.Receive();
        }
    }

    public void OnConnecting(Connection connection, ConnectionInfo info)
    {
        connection.Accept();
        ClientConnecting?.Invoke(this, connection.Id);
        Logging.Info($"{info.Identity} is connecting");
    }

    public void OnConnected(Connection connection, ConnectionInfo info)
    {
        Logging.Info($"{info.Identity} has joined the game");
        _connectedClients.TryAdd(connection, connection);
    }

    public void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        _connectedClients.TryRemove(connection, out _);
        Logging.Info($"{info.Identity} has left the game");
    }
    
    public unsafe void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        using var messageData = new UnmanagedMemoryManager<byte>((byte*)data, size);
        ReceivePacket(connection.Id, messageData.Memory);
    }

    public override void SendRawPacketToClients(ReadOnlySpan<uint> clientIndices, ReadOnlySpan<byte> span, bool reliable)
    {
        foreach (var clientIndex in clientIndices)
        {
            if (_connectedClients.TryGetValue(clientIndex, out var connection))
            {
                connection.SendMessage(span, reliable ? SendType.Reliable : SendType.Unreliable);
            }
        }
    }

    public override void Stop()
    {
        _isRunning = false;
        _server.Close();
    }

    public override void Start()
    {
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
    }
}