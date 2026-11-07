using System.Net.WebSockets;

namespace NFMWorldLibrary.Multiplayer;

public class WebSocketMultiplayerClientTransport : BaseMultiplayerClientTransport
{
    private readonly ClientWebSocket _client;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Connects to <c>/game</c> on the lobby.
    /// </summary>
    /// <param name="insecure">
    /// Plain <c>ws://</c> - only for a lobby on this machine or a trusted network. The session
    /// token is presented on this connection, so a public lobby needs <c>wss://</c>.
    /// </param>
    public WebSocketMultiplayerClientTransport(string hostName, ushort port = 7000, bool insecure = true)
    {
        _client = new ClientWebSocket();
        _ = ConnectAsync(hostName, port, insecure);
    }

    private async Task ConnectAsync(string host, ushort port, bool insecure)
    {
        try
        {
            var scheme = insecure ? "ws" : "wss";
            await _client.ConnectAsync(new Uri($"{scheme}://{host}:{port}/game"), _cts.Token);
            State = ClientState.Connected;
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Logging.Error($"WebSocket connect failed: {ex.Message}");
            State = ClientState.Disconnected;
        }
    }

    /// <summary>Largest message the server may send, so a bad server cannot exhaust memory.</summary>
    private const int MaxMessageBytes = 4 * 1024 * 1024;

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
                if (ms.Length > MaxMessageBytes)
                {
                    Logging.Warning($"Server sent a message over {MaxMessageBytes} bytes; disconnecting");
                    break;
                }

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
        // Cancelling is enough to end the receive loop; the close is a courtesy the server
        // does not need, and an unawaited CloseAsync can fault with nothing to observe it.
        _cts.Cancel();
    }
}