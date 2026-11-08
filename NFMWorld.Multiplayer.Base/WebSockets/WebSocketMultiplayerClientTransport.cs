using System.Net.WebSockets;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// The lobby connection. Reconnects on its own when the socket drops, so a lobby restart
/// or a momentary network loss does not strand the player on a dead screen.
/// </summary>
public class WebSocketMultiplayerClientTransport : BaseMultiplayerClientTransport
{
    private readonly string _host;
    private readonly ushort _port;
    private readonly bool _insecure;

    private ClientWebSocket? _client;
    private CancellationTokenSource? _receiveCts;

    /// <summary>
    /// Connects to <c>/game</c> on the lobby.
    /// </summary>
    /// <param name="insecure">
    /// Plain <c>ws://</c> - only for a lobby on this machine or a trusted network. The session
    /// token is presented on this connection, so a public lobby needs <c>wss://</c>.
    /// </param>
    public WebSocketMultiplayerClientTransport(string hostName, ushort port = 7000, bool insecure = true)
    {
        _host = hostName;
        _port = port;
        _insecure = insecure;
        _ = TryConnectAsync();
    }

    protected override async Task<bool> ConnectAsync(CancellationToken ct)
    {
        // A fresh socket per attempt: a ClientWebSocket cannot be reopened once aborted.
        var client = new ClientWebSocket();
        var scheme = _insecure ? "ws" : "wss";

        try
        {
            await client.ConnectAsync(new Uri($"{scheme}://{_host}:{_port}/game"), ct);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        var previous = _receiveCts;
        _client = client;
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        previous?.Cancel();
        previous?.Dispose();

        _ = ReceiveLoop(client, _receiveCts.Token);
        return true;
    }

    protected override void OnConnectException(Exception ex)
        => Logging.Error($"WebSocket connect failed: {ex.Message}");

    /// <summary>Largest message the server may send, so a bad server cannot exhaust memory.</summary>
    private const int MaxMessageBytes = 4 * 1024 * 1024;

    private async Task ReceiveLoop(ClientWebSocket client, CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        try
        {
            while (client.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(buffer, ct);
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
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }

        // Only the live socket may change the state: an older loop finishing after a
        // reconnect must not report the new connection as dropped.
        if (!ReferenceEquals(client, _client))
            return;

        client.Dispose();
        State = ClientState.Disconnected;

        // Reconnect unless we were told to stop (a deliberate leave).
        if (!IsStopping)
            BeginReconnect();
    }

    private bool IsStopping { get; set; }

    protected override void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable)
    {
        if (_client is { State: WebSocketState.Open } client)
            _ = client.SendAsync(span.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public override void Stop()
    {
        IsStopping = true;
        base.Stop();
        _receiveCts?.Cancel();
        _client?.Dispose();
        State = ClientState.Disconnected;
    }
}
