using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// WebSocket server transport using System.Net.HttpListener + System.Net.WebSockets.
/// No external dependencies.
/// </summary>
public class WebSocketMultiplayerServerTransport : BaseMultiplayerServerTransport
{
    private readonly ConcurrentDictionary<uint, WsClient> _clients = new();
    private uint _nextId;
    private CancellationTokenSource? _cts;

    public override IReadOnlyCollection<uint> Connections => (IReadOnlyCollection<uint>)_clients.Keys;

    public override event EventHandler<uint>? ClientConnecting;
    public override event EventHandler<uint>? ClientConnected;
    public override event EventHandler<uint>? ClientDisconnected;

    public override void Start()
    {
        _cts = new CancellationTokenSource();
    }

    public override void Stop()
    {
        _cts?.Cancel();
    }

    public async Task AcceptWebSocketRequest(HttpListenerContext ctx, CancellationToken ct)
    {
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts?.Token ?? CancellationToken.None);

        HttpListenerWebSocketContext? wsCtx = null;
        try
        {
            wsCtx = await ctx.AcceptWebSocketAsync(null);
        }
        catch (Exception ex)
        {
            Logging.Error($"Failed to accept WebSocket connection from {ctx.Request.RemoteEndPoint}", exception: ex);
            return;
        }

        var id = Interlocked.Increment(ref _nextId);
        var client = new WsClient(id, wsCtx.WebSocket);
        _clients.TryAdd(id, client);

        Logging.Info($"WS client connected - ID: {id}, IP: {ctx.Request.RemoteEndPoint}");

        // The caller discards this task, so anything thrown here would vanish and leave the
        // socket open and never read: the client's handshake would simply time out.
        try
        {
            var started = Stopwatch.GetTimestamp();
            ClientConnecting?.Invoke(this, id);
            ClientConnected?.Invoke(this, id);
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (elapsed > SlowConnectHandlerThreshold)
                Logging.Warning($"WS client {id}: connect handlers took {elapsed.TotalMilliseconds:F0} ms");

            await ReceiveLoop(client, tokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logging.Error($"WS client {id} disconnected due to exception", exception: ex);
            client.Socket.Abort();
        }

        _clients.TryRemove(id, out _);
        ForgetConnection(id);
        Logging.Info($"WS client disconnected - ID: {id}");
        try
        {
            ClientDisconnected?.Invoke(this, id);
        }
        catch (Exception ex)
        {
            Logging.Error($"WS client {id}: disconnect handler threw", exception: ex);
        }
    }

    private static readonly TimeSpan SlowConnectHandlerThreshold = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Largest message a client may send. Packets are a few dozen bytes; this exists only
    /// so a client cannot grow the reassembly buffer without bound.
    /// </summary>
    private const int MaxMessageBytes = 64 * 1024;

    private async Task ReceiveLoop(WsClient client, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        using var ms = new MemoryStream();

        try
        {
            while (client.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await client.Socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;

                ms.Write(buffer, 0, result.Count);
                if (ms.Length > MaxMessageBytes)
                {
                    Logging.Warning($"WS client {client.Id} sent a message over {MaxMessageBytes} bytes; closing");
                    await client.Socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too big", ct);
                    break;
                }

                if (result.EndOfMessage)
                {
                    ReceivePacket(client.Id, ms.ToArray());
                    ms.SetLength(0);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override void DisconnectClient(uint clientIndex)
    {
        // Queued behind the packets already written to this client, so a refusal sent just before
        // the close is actually delivered.
        if (_clients.TryGetValue(clientIndex, out var client))
            _ = client.CloseAsync();
    }

    public override void SendRawPacketToClients(ReadOnlySpan<uint> clientIndices, ReadOnlySpan<byte> span, bool reliable)
    {
        var data = span.ToArray();
        foreach (var id in clientIndices)
        {
            if (_clients.TryGetValue(id, out var client))
                client.Send(data);
        }
    }

    private sealed class WsClient(uint id, WebSocket socket)
    {
        public readonly uint Id = id;
        public readonly WebSocket Socket = socket;

        // One send at a time: a second SendAsync while one is outstanding throws, and the packet
        // is lost. A close also has to wait for the packets before it, or a refusal written just
        // ahead of it never reaches the client.
        private readonly Lock _sendLock = new();
        private Task _pending = Task.CompletedTask;
        private int _queued;

        /// <summary>
        /// Backlog cap. A client that stops reading would otherwise grow this queue without
        /// bound, since nothing else throttles the lobby's writes.
        /// </summary>
        private const int MaxQueuedSends = 1024;

        public void Send(byte[] data)
        {
            if (Volatile.Read(ref _queued) >= MaxQueuedSends)
            {
                Logging.Warning($"WS client {Id} is not reading; dropping a packet");
                return;
            }

            Chain(SendCoreAsync, data);
        }

        public Task CloseAsync() => Chain(CloseCoreAsync, null);

        private Task Chain(Func<byte[]?, Task> operation, byte[]? data)
        {
            Interlocked.Increment(ref _queued);
            lock (_sendLock)
            {
                return _pending = _pending
                    .ContinueWith(
                        _ =>
                        {
                            Interlocked.Decrement(ref _queued);
                            return operation(data);
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default
                    )
                    .Unwrap();
            }
        }

        private async Task SendCoreAsync(byte[]? data)
        {
            try
            {
                if (data is not null && Socket.State == WebSocketState.Open)
                    await Socket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logging.Warning($"Failed to send data to WS client {Id}", exception: ex);
                // A send that failed means the peer is gone. Nothing else ends this connection's
                // receive loop - it blocks until the socket breaks - so without this the client
                // stays in the registry for ever, holding the account seat it authenticated with.
                Socket.Abort();
            }
        }

        /// <summary>
        /// How long a peer gets to answer the closing handshake. Without a bound, a peer that is
        /// gone without closing leaves this waiting for ever - and its receive loop, its entry in
        /// the registry and the account it authenticated with along with it.
        /// </summary>
        private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

        private async Task CloseCoreAsync(byte[]? _)
        {
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    using var timeout = new CancellationTokenSource(CloseTimeout);
                    await Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "refused", timeout.Token);
                }
            }
            catch (Exception ex)
            {
                Logging.Warning($"Failed to close WS client {Id}", exception: ex);
            }

            // A peer that never answers the close keeps this receive loop - and the registry
            // entry, and the account it authenticated with - alive indefinitely. Aborting is the
            // only way to end it from this side.
            if (Socket.State != WebSocketState.Closed)
                Socket.Abort();
        }
    }
}
