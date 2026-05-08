using Maxine.Extensions;
using Steamworks;
using Steamworks.Data;

namespace NFMWorldLibrary.Mad.Multiplayer;

public class SteamMultiplayerClientTransport : BaseMultiplayerClientTransport, IConnectionManager
{
    private readonly ConnectionManager _client;
    public ClientState State { get; private set; } = ClientState.Connecting;
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
        Logging.Info("Connected to server");
        State = ClientState.Connected;
    }

    public void OnConnecting(ConnectionInfo info)
    {
        Logging.Info("Connecting to server");
        State = ClientState.Connecting;
    }

    public void OnDisconnected(ConnectionInfo info)
    {
        Logging.Info("Disconnected from server");
        State = ClientState.Disconnected;
    }

    public unsafe void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        using var messageData = new UnmanagedMemoryManager<byte>((byte*)data, size);

        var memory = messageData.Memory;
        ReceivePacket(memory);
    }

    protected override void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable)
    {
        _client.Connection.SendMessage(span, reliable ? SendType.Reliable : SendType.Unreliable);
    }

    public override void Stop()
    {
        _isRunning = false;
        _client.Close();
    }
}