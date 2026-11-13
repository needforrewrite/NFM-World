using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using TightPack;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseMultiplayerClientTransport : IMultiplayerClientTransport
{
    private ConcurrentQueue<IPacketServerToClient> _receivedPacketQueue = new();

    private ClientState _state = ClientState.Connecting;

    public ClientState State
    {
        get => _state;
        protected set
        {
            // The version goes first on every fresh connection. A peer that does not know it
            // would read every later packet with the wrong layout, so nothing else is sent
            // until it has been answered.
            var isNewConnection = value == ClientState.Connected && _state != ClientState.Connected;
            _state = value;

            if (isNewConnection)
                BeginHandshake();
        }
    }

    /// <summary>True once the server confirmed it reads this build's wire version.</summary>
    public bool HandshakeAccepted { get; private set; }

    /// <summary>
    /// Why the server refused this build, or null while it may still answer. Set on a refusal
    /// and after a handshake that was never answered.
    /// </summary>
    public string? HandshakeError { get; private set; }

    /// <summary>
    /// Declares the wire version again. The transport does this itself on connect; a caller
    /// repeats it only to give a server that never answered another chance.
    /// </summary>
    public void SendHandshake() => SendPacketToServer(new C2S_Handshake { Version = Protocol.Version });

    /// <summary>How long to wait for the version answer before declaring it again.</summary>
    protected virtual int HandshakeRetryDelayMs => 5000;

    /// <summary>
    /// How many declarations to send before giving up. A server from before this handshake exists
    /// ignores the opcode, so it never answers - silence is the only sign it is too old.
    /// </summary>
    protected virtual int MaxHandshakeAttempts => 3;

    /// <summary>Bumped on every connection, so a stale retry loop stops touching live state.</summary>
    private int _handshakeGeneration;

    private void BeginHandshake()
    {
        HandshakeAccepted = false;
        HandshakeError = null;

        var generation = Interlocked.Increment(ref _handshakeGeneration);
        SendHandshake();
        _ = RetryHandshakeAsync(generation);
    }

    private async Task RetryHandshakeAsync(int generation)
    {
        for (var attempt = 1; attempt < MaxHandshakeAttempts; attempt++)
        {
            await Task.Delay(HandshakeRetryDelayMs);

            if (generation != Volatile.Read(ref _handshakeGeneration) || HandshakeAccepted)
                return;

            if (State != ClientState.Connected)
                return;

            SendHandshake();
        }

        await Task.Delay(HandshakeRetryDelayMs);

        if (generation != Volatile.Read(ref _handshakeGeneration) || HandshakeAccepted)
            return;

        // Refuse to talk to a server whose layout cannot be confirmed. An older build ignores the
        // opcode instead of refusing it, so "never answered" is the only thing it can say.
        HandshakeError = "The server did not answer the game version handshake. It is probably running an older version.";
        Logging.Warning($"The server never acknowledged protocol {Protocol.Version}.");
        StopReconnecting();
        State = ClientState.Disconnected;
    }

    /// <summary>
    /// Reads the server's answer. Accepting an old refusal's error string is what lets the
    /// player see which side is out of date rather than "connection lost".
    /// </summary>
    private void HandleHandshake(ReadOnlyMemory<byte> message)
    {
        S2C_Handshake handshake;
        try
        {
            handshake = TightPackSerializer.Deserialize<S2C_Handshake>(message.Span);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Server sent an unreadable handshake ({message.Length} bytes)", exception: ex);
            return;
        }

        if (handshake.Accepted && handshake.Version == Protocol.Version)
        {
            HandshakeAccepted = true;
            HandshakeError = null;
            return;
        }

        HandshakeAccepted = false;
        HandshakeError = handshake.Error is { Length: > 0 } error
            ? error
            : $"This server runs game version {handshake.Version}; this build is version {Protocol.Version}.";
        Logging.Warning($"Server refused the connection: {HandshakeError}");

        // Reconnecting reaches the same server, which will refuse it again.
        StopReconnecting();
        State = ClientState.Disconnected;
    }

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

        // Answered here and never queued: it is the transport's own business, and a caller that
        // saw it would have to know to ignore it.
        if (opcode == Protocol.ServerHandshakeOpcode)
        {
            HandleHandshake(message);
            return;
        }

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
