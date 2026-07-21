using System.Net.WebSockets;

namespace NFMWorldLibrary.Multiplayer;

public class WebSocketMultiplayerClientTransport : BaseMultiplayerClientTransport
{
    private readonly ClientWebSocket _client;
    private readonly CancellationTokenSource _cts = new();

    public WebSocketMultiplayerClientTransport(string hostName, ushort port = 7000)
    {
        _client = new ClientWebSocket();
        _ = ConnectAsync(hostName, port);
    }

    private async Task ConnectAsync(string host, ushort port)
    {
        try
        {
            await _client.ConnectAsync(new Uri($"ws://{host}:{port}/game"), _cts.Token);
            State = ClientState.Connected;
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Logging.Error($"WebSocket connect failed: {ex.Message}");
            State = ClientState.Disconnected;
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        try
        {
            while (_client.State == WebSocketState.Open)
            {
                var result = await _client.ReceiveAsync(buffer, _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;

                ms.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    ReceivePacket(ms.ToArray());
                    ms.SetLength(0);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }

        State = ClientState.Disconnected;
    }

    protected override void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable)
    {
        if (_client.State == WebSocketState.Open)
            _ = _client.SendAsync(span.ToArray(), WebSocketMessageType.Binary, true, _cts.Token);
    }

    public override void Stop()
    {
        _cts.Cancel();
        _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }
}