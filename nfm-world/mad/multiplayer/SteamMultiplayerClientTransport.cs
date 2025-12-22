using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorld.Util;
using Steamworks;
using Steamworks.Data;

namespace NFMWorld.Mad;

public class SteamMultiplayerClientTransport : IMultiplayerClientTransport, IConnectionManager
{
    private readonly ConnectionManager _client;
    public ClientState State { get; private set; } = ClientState.Connecting;
    private ConcurrentQueue<IPacketServerToClient> _receivedPacketQueue = new();
    private bool _isRunning = true;
    private readonly Thread _receiveThread;

    public SteamMultiplayerClientTransport(SteamId serverId, int virtualport = 1)
    {
        _client = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(serverId, virtualport);
        _client.Interface = this;
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
    }

    private void ReceiveLoop()
    {
        while (_isRunning)
        {
            _client.Receive();
        }
    }

    public void OnConnected(ConnectionInfo info)
    {
        Console.WriteLine("Connected to server");
        State = ClientState.Connected;
    }

    public void OnConnecting(ConnectionInfo info)
    {
        Console.WriteLine("Connecting to server");
        State = ClientState.Connecting;
    }

    public void OnDisconnected(ConnectionInfo info)
    {
        Console.WriteLine("Disconnected from server");
        State = ClientState.Disconnected;
    }

    public unsafe void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        using var messageData = new PointerMemoryManager<byte>((void*)data, size);

        var memory = messageData.Memory;
        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        if (MultiplayerUtils.TryDeserializeS2CPacket(opcode, message) is {} packet)
        {
            _receivedPacketQueue.Enqueue(packet);
        }
        else
        {
            Console.WriteLine($"Client received a message with unknown opcode {opcode}");
        }
    }
    
    public IPacketServerToClient[] GetNewPackets()
    {
        var packets = new List<IPacketServerToClient>();
        while (_receivedPacketQueue.TryDequeue(out var packet))
        {
            packets.Add(packet);
        }
        return packets.Count > 0 ? packets.ToArray() : [];
    }

    public void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>
    {
        using var arr = new ArrayPoolBufferWriter<byte>();
        arr.Write(MultiplayerUtils.OpcodesC2SReverse[typeof(T)]);
        packet.Write(arr);
        _client.Connection.SendMessage(arr.WrittenSpan, reliable ? SendType.Reliable : SendType.Unreliable);
    }

    public void Stop()
    {
        _isRunning = false;
        _client.Close();
    }
}