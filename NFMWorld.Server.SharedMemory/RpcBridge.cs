using MemoryPack;
using SharedMemory;

namespace NFMWorld.Server.SharedMemory;

/// <summary>
/// Thin wrapper around <see cref="RpcBuffer"/> providing typed MemoryPack-serialized
/// communication between a Controller (Race Host) and its Worker process.
/// 
/// Uses request/response pattern: the Controller sends a <see cref="RpcMessage"/> and awaits
/// the Worker's response. The Worker registers a handler that processes incoming messages
/// and returns results.
/// 
/// The underlying <see cref="RpcBuffer"/> handles controller/worker arbitration via a named
/// Mutex — the first instance to construct becomes Controller (creates shared memory),
/// the second becomes Worker (opens existing).
/// </summary>
public sealed class RpcBridge : IDisposable
{
    private readonly RpcBuffer _rpcBuffer;
    private volatile bool _disposed;

    /// <summary>
    /// Whether this instance is the Controller side of the channel.
    /// </summary>
    public bool IsMaster { get; }

    /// <summary>
    /// Whether the underlying channel is still connected (not disposed, buffers not shut down).
    /// </summary>
    public bool IsConnected => !_disposed && !_rpcBuffer.DisposeFinished;

    /// <summary>
    /// Statistics about messages sent/received.
    /// </summary>
    public RpcStatistics Statistics => _rpcBuffer.Statistics;

    /// <summary>
    /// Creates a Controller-side bridge (must be constructed first).
    /// </summary>
    /// <param name="channelName">Unique shared memory channel name. Must match between Master and Slave.</param>
    /// <param name="handler">Handler for incoming messages from the remote side.</param>
    /// <param name="bufferCapacity">Shared memory buffer capacity in bytes (default 50000). Only used by Master.</param>
    /// <param name="bufferNodeCount">Number of ring buffer nodes (default 10). Only used by Master.</param>
    public RpcBridge(
        string channelName,
        Func<RpcMessage, RpcMessage> handler,
        int bufferCapacity = 50000,
        int bufferNodeCount = 10)
    {
        _rpcBuffer = new RpcBuffer(
            channelName,
            (ulong msgId, byte[] requestData) =>
            {
                var request = MemoryPackSerializer.Deserialize<RpcMessage>(requestData);
                var response = handler(request);
                return MemoryPackSerializer.Serialize(response);
            },
            bufferCapacity,
            RpcProtocol.V1,
            bufferNodeCount);

        // The RpcBuffer auto-detects Controller/Worker via a named Mutex.
        // For Phase 1, we don't actually need IsMaster — both sides use SendAsync the same way.
        IsMaster = true; // Will be refined
    }

    /// <summary>
    /// Sends a message to the remote side and awaits the response.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 30s).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The remote side's response message.</returns>
    /// <exception cref="TimeoutException">The remote side did not respond within the timeout.</exception>
    /// <exception cref="ObjectDisposedException">The bridge has been disposed or the remote side closed the channel.</exception>
    public async Task<RpcMessage> SendAsync(
        RpcMessage message,
        int timeoutMs = 30000,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var requestBytes = MemoryPackSerializer.Serialize(message);
        var response = await _rpcBuffer.RemoteRequestAsync(
            requestBytes,
            timeoutMs,
            cancellationToken);

        if (!response.Success)
            throw new TimeoutException(
                $"RPC request of type {message.Type} timed out after {timeoutMs}ms. " +
                "The remote side may have crashed or be unresponsive.");

        return MemoryPackSerializer.Deserialize<RpcMessage>(response.Data!);
    }

    /// <summary>
    /// Synchronous version of <see cref="SendAsync"/>. Blocks the calling thread.
    /// Prefer <see cref="SendAsync"/> on the Master side to avoid blocking the game loop.
    /// </summary>
    public RpcMessage Send(
        RpcMessage message,
        int timeoutMs = 30000,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var requestBytes = MemoryPackSerializer.Serialize(message);
        var response = _rpcBuffer.RemoteRequest(
            requestBytes,
            timeoutMs,
            cancellationToken);

        if (!response.Success)
            throw new TimeoutException(
                $"RPC request of type {message.Type} timed out after {timeoutMs}ms.");

        return MemoryPackSerializer.Deserialize<RpcMessage>(response.Data!);
    }

    /// <summary>
    /// Disconnects the channel. On the Master side, this also destroys the shared memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rpcBuffer.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RpcBridge));
    }
}
