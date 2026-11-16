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

        var previousCts = _receiveCts;
        var previousClient = _client;
        _client = client;
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        previousCts?.Cancel();
        previousCts?.Dispose();

        // The superseded socket has to go, not just its receive loop: the server only drops the
        // connection - and the account seat it holds - when the socket closes, so a reconnected
        // client would otherwise be refused as a duplicate of its own previous connection.
        previousClient?.Dispose();

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
        var evicted = false;

        try
        {
            while (client.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // A policy violation is the server refusing this connection for good - an
                    // account that signed in elsewhere. Reconnecting would take the seat back
                    // and evict the connection that holds it now, so the two would ping-pong.
                    evicted = result.CloseStatus == WebSocketCloseStatus.PolicyViolation;
                    break;
                }

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
        catch (ObjectDisposedException)
        {
            // ConnectAsync disposes the superseded socket; that ends its loop.
        }

        // Only the live socket may change the state: an older loop finishing after a
        // reconnect must not report the new connection as dropped.
        if (!ReferenceEquals(client, _client))
            return;

        client.Dispose();
        State = ClientState.Disconnected;

        if (evicted)
        {
            // The reason is in the refusal packet the server sent ahead of the close, so the
            // lobby can still say why; only the retry is pointless.
            Logging.Info("The lobby closed this connection; not reconnecting.");
            StopReconnecting();
        }

        // Reconnect unless we were told to stop (a deliberate leave).
        if (!IsStopping && !evicted)
            BeginReconnect();
    }

    private bool IsStopping { get; set; }

    // One send at a time: a second SendAsync while one is outstanding throws, and the packet is
    // dropped. The handshake packets go out back to back, so this is a real path.
    private readonly Lock _sendLock = new();
    private Task _pendingSend = Task.CompletedTask;
    private int _queuedSends;

    /// <summary>Backlog cap, so a stalled connection cannot grow this queue without bound.</summary>
    private const int MaxQueuedSends = 256;

    protected override void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable)
    {
        var client = _client;
        if (client is not { State: WebSocketState.Open })
        {
            Logging.Warning("Dropped a packet for the lobby: the connection is not open");
            return;
        }

        if (Volatile.Read(ref _queuedSends) >= MaxQueuedSends)
        {
            Logging.Warning("Dropped a packet for the lobby: the send queue is backed up");
            return;
        }

        var data = span.ToArray();
        Interlocked.Increment(ref _queuedSends);
        lock (_sendLock)
        {
            _pendingSend = _pendingSend
                .ContinueWith(
                    _ =>
                    {
                        Interlocked.Decrement(ref _queuedSends);
                        return SendCoreAsync(client, data);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default
                )
                .Unwrap();
        }
    }

    private async Task SendCoreAsync(ClientWebSocket client, byte[] data)
    {
        try
        {
            if (client.State == WebSocketState.Open)
                await client.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or OperationCanceledException)
        {
            Logging.Warning($"Failed to send a packet to the lobby: {ex.Message}");
        }
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
