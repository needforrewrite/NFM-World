using System.Collections.Concurrent;
using ENet;

namespace NFMWorldLibrary.Mad.Multiplayer;

public class ENetMultiplayerClientTransport : BaseMultiplayerClientTransport
{
    private readonly Host _client;
    private Peer _peer;
    private readonly Thread _receiveThread;
    private bool _isRunning = true;
    private ConcurrentQueue<Packet> _sendPacketQueue = new();

    public ENetMultiplayerClientTransport(string hostName, ushort port = 7000)
    {
        _client = new Host();
        
        var address = new Address();
        address.SetHost(hostName);
        address.Port = port;
        
        _client.Create();
        _peer = _client.Connect(address);
        
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
    }

    private void ReceiveLoop()
    {
        while (_isRunning)
        {
            bool polled = false;

            while (!polled)
            {
                if (_client.CheckEvents(out var netEvent) <= 0) {
                    if (_client.Service(15, out netEvent) <= 0)
                        break;

                    polled = true;
                }

                switch (netEvent.Type) {
                    case EventType.None:
                        break;

                    case EventType.Connect:
                        Logging.Info("Client connected to server");
                        State = ClientState.Connected;
                        break;

                    case EventType.Disconnect:
                        Logging.Info("Client disconnected from server");
                        State = ClientState.Disconnected;
                        break;

                    case EventType.Timeout:
                        Logging.Info("Client connection timeout");
                        State = ClientState.Disconnected;
                        break;

                    case EventType.Receive:
                        Logging.Info(
                            $"Packet received from server - Channel ID: {netEvent.ChannelID}, Data length: {netEvent.Packet.Length}");
                        try
                        {
                            using var messageData = netEvent.Packet.AsMemory();
                            ReceivePacket(messageData.Memory);
                        }
                        finally
                        {
                            netEvent.Packet.Dispose();
                        }

                        break;
                }
            }
            
            // Send queued packets
            while (_sendPacketQueue.TryDequeue(out var packet))
            {
                _peer.Send(0, ref packet);
            }
        }
        
        while (_sendPacketQueue.TryDequeue(out var packet))
        {
            packet.Dispose();
        }
        
        _client.Flush();
        _client.Dispose();
    }

    protected override void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable)
    {
        if (_isRunning)
        {
            var packet = new Packet();
            packet.Create(span, reliable ? PacketFlags.Reliable : (PacketFlags.Instant | PacketFlags.Unsequenced));
            _sendPacketQueue.Enqueue(packet);
        }
    }

    public override void Stop()
    {
        _isRunning = false;
    }
}