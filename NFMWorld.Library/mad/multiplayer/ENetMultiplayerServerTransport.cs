using System.Collections;
using System.Collections.Concurrent;
using ENet;

namespace NFMWorldLibrary.Mad.Multiplayer;

public class ENetMultiplayerServerTransport : BaseMultiplayerServerTransport
{
    private bool _isRunning = true;
    private Thread _receiveThread;
    private readonly ConcurrentDictionary<uint, Peer> _connectedClients = [];
    private readonly Host _server;
    private ConcurrentQueue<(uint Peer, Packet Packet)> _sendPacketQueue = new();

    public override IReadOnlyCollection<uint> Connections { get; }
    
    public override event EventHandler<uint>? ClientConnecting;
    public override event EventHandler<uint>? ClientConnected;
    public override event EventHandler<uint>? ClientDisconnected;
    
    private class ConnectionsList(ENetMultiplayerServerTransport parent) : IReadOnlyCollection<uint>
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
    
    public ENetMultiplayerServerTransport(ushort port = 7000)
    {
        Connections = new ConnectionsList(this);
        
        _server = new Host();
        
        var address = new Address();
        address.Port = port;
        
        _server.Create(address, 255);
    }

    private void ReceiveLoop()
    {
        while (_isRunning)
        {
            bool polled = false;

            while (!polled) {
                if (_server.CheckEvents(out var netEvent) <= 0) {
                    if (_server.Service(15, out netEvent) <= 0)
                        break;

                    polled = true;
                }

                switch (netEvent.Type) {
                    case EventType.None:
                        break;

                    case EventType.Connect:
                        Logging.Info("Client connected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                        ClientConnecting?.Invoke(this, netEvent.Peer.ID);
                        ClientConnected?.Invoke(this, netEvent.Peer.ID);
                        _connectedClients.TryAdd(netEvent.Peer.ID, netEvent.Peer);
                        break;

                    case EventType.Disconnect:
                        Logging.Info("Client disconnected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                        ClientDisconnected?.Invoke(this, netEvent.Peer.ID);
                        _connectedClients.TryRemove(netEvent.Peer.ID, out _);
                        break;

                    case EventType.Timeout:
                        Logging.Info("Client timeout - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                        ClientDisconnected?.Invoke(this, netEvent.Peer.ID);
                        _connectedClients.TryRemove(netEvent.Peer.ID, out _);
                        break;

                    case EventType.Receive:
                        Logging.Info("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP + ", Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);
                        try
                        {
                            using var messageData = netEvent.Packet.AsMemory();
                            ReceivePacket(netEvent.Peer.ID, messageData.Memory);
                        }
                        finally
                        {
                            netEvent.Packet.Dispose();
                        }

                        break;
                }
            }
            
            while (_sendPacketQueue.TryDequeue(out var sendPacket))
            {
                if (_connectedClients.TryGetValue(sendPacket.Peer, out var peer))
                {
                    peer.Send(0, ref sendPacket.Packet);
                }
            }
        }
        
        while (_sendPacketQueue.TryDequeue(out var packet))
        {
            packet.Packet.Dispose();
        }

        _server.Flush();
        _server.Dispose();
    }

    public override void SendRawPacketToClients(ReadOnlySpan<uint> clientIndices, ReadOnlySpan<byte> span, bool reliable)
    {
        foreach (var clientIndex in clientIndices)
        {
            var packet = new Packet();
            packet.Create(span.ToArray(), reliable ? PacketFlags.Reliable : (PacketFlags.Instant | PacketFlags.Unsequenced));
            _sendPacketQueue.Enqueue((clientIndex, packet));
        }
    }

    public override void Stop()
    {
        _isRunning = false;
    }

    public override void Start()
    {
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
    }
}