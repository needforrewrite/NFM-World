using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseMultiplayerClientTransport : IMultiplayerClientTransport
{
    private ConcurrentQueue<IPacketServerToClient> _receivedPacketQueue = new();
    public ClientState State { get; protected set; } = ClientState.Connecting;

    private readonly CancellationTokenSource _reconnectCts = new();
    private Task? _reconnectLoop;

    /// <summary>How many times to retry a dropped connection before giving up.</summary>
    protected virtual int MaxReconnectAttempts => 10;

    /// <summary>Base delay between attempts, doubling each time.</summary>
    protected virtual TimeSpan ReconnectDelay => TimeSpan.FromSeconds(2);

    public bool IsReconnecting => _reconnectLoop is { IsCompleted: false };

    /// <summary>Set when a retry would only repeat the reason the server dropped us.</summary>
    protected bool ReconnectDisabled { get; private set; }

    public void StopReconnecting()
    {
        ReconnectDisabled = true;
        _reconnectCts.Cancel();
    }

    public bool ReconnectFailed { get; private set; }

    public event Action? Reconnected;

    /// <summary>Raises <see cref="Reconnected"/> for a subclass that detects the reconnect itself.</summary>
    protected void RaiseReconnected() => Reconnected?.Invoke();

    /// <summary>
    /// Opens the underlying connection. Returns false if the attempt failed. A transport that
    /// does not support reconnecting leaves this at the default, which never succeeds.
    /// </summary>
    protected virtual Task<bool> ConnectAsync(CancellationToken ct) => Task.FromResult(false);

    /// <summary>
    /// Called when a connection attempt failed with an exception.
    /// </summary>
    protected virtual void OnConnectException(Exception ex)
    {
        Logging.Error($"Connect failed: {ex.Message}");
    }

    /// <summary>
    /// Starts the retry loop after the connection dropped. A subclass whose connection
    /// cannot be re-established leaves this alone.
    /// </summary>
    protected void BeginReconnect()
    {
        if (ReconnectDisabled
            || _reconnectLoop is { IsCompleted: false }
            || _reconnectCts.IsCancellationRequested)
            return;

        ReconnectFailed = false;
        _reconnectLoop = Task.Run(ReconnectLoopAsync);
    }

    private async Task ReconnectLoopAsync()
    {
        var delay = ReconnectDelay;
        for (var attempt = 0; attempt < MaxReconnectAttempts; attempt++)
        {
            try
            {
                await Task.Delay(delay, _reconnectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (await TryConnectAsync())
            {
                RaiseReconnected();
                return;
            }

            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
        }

        ReconnectFailed = true;
        State = ClientState.Disconnected;
        Logging.Warning("Gave up reconnecting.");
    }

    /// <summary>
    /// One reconnect attempt. Swallows a failed connection so the loop can keep going.
    /// </summary>
    protected async Task<bool> TryConnectAsync()
    {
        try
        {
            if (await ConnectAsync(_reconnectCts.Token))
            {
                State = ClientState.Connected;
                return true;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            OnConnectException(ex);
        }

        State = ClientState.Disconnected;
        return false;
    }

    protected abstract void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable);

    public IPacketServerToClient[] GetNewPackets()
    {
        var packets = new List<IPacketServerToClient>();
        while (_receivedPacketQueue.TryDequeue(out var packet))
        {
            packets.Add(packet);
        }

        return packets.Count > 0 ? packets.ToArray() : [];
    }

    protected void ReceivePacket(ReadOnlyMemory<byte> memory)
    {
        if (memory.IsEmpty)
        {
            Logging.Warning("Server sent an empty packet");
            return;
        }

        var opcode = (sbyte)memory.Span[0];
        var message = memory[1..];

        IPacketServerToClient? packet;
        try
        {
            packet = MultiplayerUtils.TryDeserializeS2CPacket(opcode, message);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Server sent an unreadable packet (opcode {opcode})", exception: ex);
            return;
        }

        if (packet is { } p)
        {
            _receivedPacketQueue.Enqueue(p);
        }
        else
        {
            Logging.Info($"Client received a message with unknown opcode {opcode}");
        }
    }

    public void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>
    {
        Logging.Debug($"Sending packet to server: {packet.GetType().Name}");

        using var arr = new ArrayPoolBufferWriter<byte>();
        arr.Write(packet.Opcode);
        packet.Write(arr);

        SendRawPacketToServer(arr.WrittenSpan, reliable);
    }

    /// <summary>Stops the transport. Subclasses that own a connection must call this after closing it.</summary>
    public virtual void Stop()
    {
        // Not disposed: a pending reconnect may still be reading the token, and a slightly
        // longer-lived cancellation source is not worth the race.
        _reconnectCts.Cancel();
    }
}
