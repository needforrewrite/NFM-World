using System.Buffers;
using System.Collections.Concurrent;
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
        ClientConnecting?.Invoke(this, id);
        ClientConnected?.Invoke(this, id);

        try
        {
            await ReceiveLoop(client, tokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logging.Error($"WS client {id} disconnected due to exception", exception: ex);
        }

        _clients.TryRemove(id, out _);
        Logging.Info($"WS client disconnected - ID: {id}");
        ClientDisconnected?.Invoke(this, id);
    }

    private async Task ReceiveLoop(WsClient client, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        using var ms = new MemoryStream();

        while (client.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await client.Socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) break;

            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                ReceivePacket(client.Id, ms.ToArray());
                ms.SetLength(0);
            }
        }
    }

    public override void SendRawPacketToClients(ReadOnlySpan<uint> clientIndices, ReadOnlySpan<byte> span, bool reliable)
    {
        var data = span.ToArray();
        foreach (var id in clientIndices)
        {
            if (_clients.TryGetValue(id, out var client))
                _ = client.SendAsync(data);
        }
    }

    private sealed class WsClient(uint id, WebSocket socket)
    {
        public readonly uint Id = id;
        public readonly WebSocket Socket = socket;

        public async Task SendAsync(byte[] data)
        {
            try
            {
                if (Socket.State == WebSocketState.Open)
                    await Socket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logging.Warning($"Failed to send data to WS client {Id}", exception: ex);
            }
        }
    }
}
