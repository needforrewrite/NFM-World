using System.Collections.Concurrent;
using ENet;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// The race connection to a Game Master. Reconnects on its own when the peer drops, so a
/// game-master restart or a lost packet path does not leave the race frozen for good.
/// </summary>
public class ENetMultiplayerClientTransport : BaseMultiplayerClientTransport
{
    private readonly Host _client;
    private readonly Address _address;
    private readonly Thread _receiveThread;
    private Peer _peer;
    private volatile bool _isRunning = true;
    // Set when the next Connect is the result of a reconnect rather than the initial dial.
    private volatile bool _reconnectAttempted;
    private readonly ConcurrentQueue<Packet> _sendPacketQueue = new();

    public ENetMultiplayerClientTransport(string hostName, ushort port = 7000)
    {
        _client = new Host();
        _client.Create();

        _address = new Address();
        _address.SetHost(hostName);
        _address.Port = port;

        _peer = _client.Connect(_address);

        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
    }

    /// <summary>
    /// Reconnects the peer. ENet keeps the host and its event queue across a reconnect, so
    /// this only replaces the peer rather than rebuilding the host.
    /// </summary>
    protected override Task<bool> ConnectAsync(CancellationToken ct)
    {
        _peer = _client.Connect(_address);
        // ENet reports the real outcome as a Connect/Disconnect event on the receive loop,
        // which is also what sets State; until then this stays Connecting.
        State = ClientState.Connecting;
        return Task.FromResult(true);
    }

    private void ReceiveLoop()
    {
        while (_isRunning)
        {
            bool polled = false;

            while (!polled)
            {
                if (_client.CheckEvents(out var netEvent) <= 0)
                {
                    if (_client.Service(15, out netEvent) <= 0)
                        break;

                    polled = true;
                }

                switch (netEvent.Type)
                {
                    case EventType.None:
                        break;

                    case EventType.Connect:
                        Logging.Info("Client connected to server");
                        State = ClientState.Connected;
                        // Only a genuine reconnect: the first Connect also lands here, and
                        // raising it then would make the lobby re-handshake for nothing.
                        if (_reconnectAttempted)
                        {
                            _reconnectAttempted = false;
                            // The caller re-sends its handshake (join token, loaded state) here.
                            RaiseReconnected();
                        }
                        break;

                    case EventType.Disconnect:
                    case EventType.Timeout:
                        Logging.Info("Client disconnected from server");
                        State = ClientState.Disconnected;
                        // Reconnect unless the transport is being stopped; a deliberate
                        // leave must not bring the peer back.
                        if (_isRunning && !IsStopping)
                        {
                            _reconnectAttempted = true;
                            BeginReconnect();
                        }
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

    private bool IsStopping { get; set; }

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
        IsStopping = true;
        base.Stop();

        // The receive thread is what sends queued packets, so one written a moment before Stop
        // (a race forfeit) was dropped. Let it drain before the peer goes.
        SpinWait.SpinUntil(() => _sendPacketQueue.IsEmpty, TimeSpan.FromMilliseconds(250));

        _isRunning = false;
        _peer.DisconnectNow(0);
    }
}
