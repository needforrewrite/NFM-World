using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using static ENet.ProtocolConstants;

namespace ENet;

public delegate ulong ChecksumCallback(ReadOnlySpan<byte> buffer);

public delegate int InterceptCallback(ref Event? @event, ref Address address, ReadOnlySpan<byte> receivedData);

/// <summary>Managed <c>ENetEvent</c>.</summary>
public struct Event
{
    private EventType _type;
    private Peer _peer;
    private byte _channelId;
    private uint _data;
    private Packet _packet;

    internal Event(EventType type, PeerImpl peer, uint data = 0, byte channelId = 0, Packet packet = default)
    {
        _type = type;
        _peer = new Peer(peer);
        _channelId = channelId;
        _data = data;
        _packet = packet;
    }

    public readonly EventType Type => _type;

    public readonly Peer Peer => _peer;

    public readonly byte ChannelID => _channelId;

    public readonly uint Data => _data;

    public readonly Packet Packet => _packet;
}

/// <summary>
/// Managed port of the ENet host (<c>host.c</c>, <c>protocol.c</c> and <c>peer.c</c> from
/// nxrighthere/ENet-CSharp's enet fork). Wire-compatible with the native library: same command
/// layout, big-endian fields, handshake, reliable/unreliable/fragment windows and timeout rules.
/// </summary>
public sealed class Host : IDisposable
{
    private PeerImpl[] _peers;
    private Socket? _socket;
    private bool _dualMode;
    private bool _created;

    private readonly byte[] _receiveBuffer = new byte[MaximumMtu];
    private readonly byte[] _sendBuffer = new byte[MaximumMtu + ChecksumSize];
    private readonly EnetList<OutgoingCommand> _sentUnreliableCommands = new();

    internal readonly EnetList<PeerImpl> DispatchQueue = new();

    internal uint ServiceTime;
    internal uint TotalQueued;
    internal uint TotalSentData;
    internal uint TotalSentPackets;
    internal uint TotalReceivedData;
    internal uint TotalReceivedPackets;

    internal int CommandCount;
    internal int BufferCount;
    internal int PacketSize;
    internal ushort HeaderFlags;

    internal Address ReceivedAddress;
    internal byte[] ReceivedData = [];
    internal int ReceivedDataLength;

    public Host()
    {
        _peers = [];
    }

    internal PeerImpl[] Peers => _peers;

    internal uint Mtu { get; private set; } = 1280;

    internal int MaximumPacketSize { get; private set; } = 32 * 1024 * 1024;

    internal long MaximumWaitingData { get; private set; } = 32 * 1024 * 1024;

    internal uint IncomingBandwidth;
    internal uint OutgoingBandwidth;
    internal uint BandwidthThrottleEpoch;
    internal uint RandomSeed;
    internal bool RecalculateBandwidthLimits;
    internal bool ConnectionsPrevented;
    internal uint ChannelLimit;
    internal int ConnectedPeers;
    internal int BandwidthLimitedPeers;
    internal int DuplicatePeers = MaximumPeerId;
    internal bool HasChecksum => Checksum is not null;
    internal ChecksumCallback? Checksum;
    internal InterceptCallback? Intercept;

    public bool IsSet => _created;

    public uint PeersCount => (uint)ConnectedPeers;

    public uint PacketsSent => TotalSentPackets;

    public uint PacketsReceived => TotalReceivedPackets;

    public uint BytesSent => TotalSentData;

    public uint BytesReceived => TotalReceivedData;

    private void ThrowIfNotCreated()
    {
        if (!_created)
            throw new InvalidOperationException("Host not created");
    }

    private static void ThrowIfChannelsExceeded(int channelLimit)
    {
        if (channelLimit < 0 || channelLimit > MaximumChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelLimit));
    }

    public void Create() => Create(null, 1, 0);

    public void Create(int bufferSize) => Create(null, 1, 0, 0, 0, bufferSize);

    public void Create(Address? address, int peerLimit) => Create(address, peerLimit, 0);

    public void Create(Address? address, int peerLimit, int channelLimit) =>
        Create(address, peerLimit, channelLimit, 0, 0, 0);

    public void Create(int peerLimit, int channelLimit) => Create(null, peerLimit, channelLimit, 0, 0, 0);

    public void Create(int peerLimit, int channelLimit, uint incomingBandwidth, uint outgoingBandwidth) =>
        Create(null, peerLimit, channelLimit, incomingBandwidth, outgoingBandwidth, 0);

    public void Create(
        Address? address,
        int peerLimit,
        int channelLimit,
        uint incomingBandwidth,
        uint outgoingBandwidth
    ) =>
        Create(address, peerLimit, channelLimit, incomingBandwidth, outgoingBandwidth, 0);

    public void Create(
        Address? address,
        int peerLimit,
        int channelLimit,
        uint incomingBandwidth,
        uint outgoingBandwidth,
        int bufferSize
    )
    {
        if (_created)
            throw new InvalidOperationException("Host already created");

        if (peerLimit < 0 || peerLimit > MaximumPeerId)
            throw new ArgumentOutOfRangeException(nameof(peerLimit));

        ThrowIfChannelsExceeded(channelLimit);

        if (channelLimit != 0 && (channelLimit < MinimumChannelCount || channelLimit > MaximumChannelCount))
            channelLimit = channelLimit < MinimumChannelCount ? MinimumChannelCount : MaximumChannelCount;

        CreateSocket(address, bufferSize);

        ChannelLimit = channelLimit == 0 || channelLimit > MaximumChannelCount
            ? MaximumChannelCount
            : (uint)channelLimit;

        RandomSeed = (uint)Environment.TickCount64;
        RandomSeed = (RandomSeed << 16) | (RandomSeed >> 16);
        IncomingBandwidth = incomingBandwidth;
        OutgoingBandwidth = outgoingBandwidth;
        BandwidthThrottleEpoch = 0;
        RecalculateBandwidthLimits = false;
        ConnectionsPrevented = false;
        Mtu = 1280;
        ConnectedPeers = 0;
        BandwidthLimitedPeers = 0;
        DuplicatePeers = MaximumPeerId;
        MaximumPacketSize = 32 * 1024 * 1024;
        MaximumWaitingData = 32 * 1024 * 1024;
        CommandCount = 0;
        BufferCount = 0;
        ReceivedAddress = default;
        ReceivedData = _receiveBuffer;
        ReceivedDataLength = 0;
        TotalSentData = 0;
        TotalSentPackets = 0;
        TotalReceivedData = 0;
        TotalReceivedPackets = 0;
        TotalQueued = 0;

        _peers = new PeerImpl[peerLimit];

        for (var i = 0; i < peerLimit; ++i)
        {
            var peer = new PeerImpl
            {
                Host = this,
                IncomingPeerId = i,
                OutgoingPeerId = MaximumPeerId,
                OutgoingSessionId = 0xFF,
                IncomingSessionId = 0xFF,
                Address = default,
                State = PeerState.Disconnected
            };

            peer.Reset();
            _peers[i] = peer;
        }

        _created = true;
    }

    private void CreateSocket(Address? address, int bufferSize)
    {
        var resolved = address?.ResolvedAddress;
        var useIpv6 = resolved is null || resolved.AddressFamily == AddressFamily.InterNetworkV6;

        try
        {
            if (useIpv6)
            {
                _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
                {
                    DualMode = true
                };

                _dualMode = true;
            }
            else
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _dualMode = false;
            }
        }
        catch (SocketException)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _dualMode = false;
        }

        _socket.Blocking = false;
        _socket.EnableBroadcast = true;

        bufferSize = Math.Clamp(bufferSize, 256 * 1024, 1024 * 1024);

        try
        {
            _socket.ReceiveBufferSize = bufferSize;
            _socket.SendBufferSize = bufferSize;
        }
        catch (SocketException)
        {
            // Buffer sizing is advisory.
        }

        if (address is { } bindAddress)
        {
            var bindIp = bindAddress.ResolvedAddress;

            if (bindIp is null)
                bindIp = _dualMode ? IPAddress.IPv6Any : IPAddress.Any;

            _socket.Bind(new IPEndPoint(bindIp, bindAddress.Port));
        }
        else
        {
            // ENet leaves client sockets unbound; .NET requires a local endpoint before
            // ReceiveFrom, so bind an ephemeral port up front (same wire behaviour).
            var any = _dualMode ? IPAddress.IPv6Any : IPAddress.Any;
            _socket.Bind(new IPEndPoint(any, 0));
        }
    }

    public void Dispose()
    {
        if (!_created && _socket is null)
            return;

        _socket?.Dispose();
        _socket = null;

        if (_created)
            foreach (var peer in _peers)
                peer.Reset();

        _created = false;
    }

    public void PreventConnections(bool state) => ConnectionsPrevented = state;

    public void SetChannelLimit(int channelLimit)
    {
        ThrowIfNotCreated();
        ThrowIfChannelsExceeded(channelLimit);

        ChannelLimit = channelLimit == 0 || channelLimit > MaximumChannelCount
            ? MaximumChannelCount
            : (uint)channelLimit;
    }

    public void SetBandwidthLimit(uint incomingBandwidth, uint outgoingBandwidth)
    {
        ThrowIfNotCreated();

        IncomingBandwidth = incomingBandwidth;
        OutgoingBandwidth = outgoingBandwidth;
        RecalculateBandwidthLimits = true;
    }

    public void SetMaxDuplicatePeers(ushort number)
    {
        ThrowIfNotCreated();

        DuplicatePeers = number < 1 ? 1 : Math.Min((int)number, MaximumPeerId);
    }

    public void SetChecksumCallback(ChecksumCallback? callback)
    {
        ThrowIfNotCreated();
        Checksum = callback;
    }

    public void SetInterceptCallback(InterceptCallback? callback)
    {
        ThrowIfNotCreated();
        Intercept = callback;
    }

    public Peer Connect(Address address) => Connect(address, 0, 0);

    public Peer Connect(Address address, int channelLimit) => Connect(address, channelLimit, 0);

    public Peer Connect(Address address, int channelLimit, uint data)
    {
        ThrowIfNotCreated();
        ThrowIfChannelsExceeded(channelLimit);

        return new Peer(HostConnect(address, channelLimit, data));
    }

    private PeerImpl? HostConnect(Address address, int channelCount, uint data)
    {
        EnsureSocketFor(address);

        if (channelCount < MinimumChannelCount)
            channelCount = MinimumChannelCount;
        else if (channelCount > MaximumChannelCount)
            channelCount = MaximumChannelCount;

        PeerImpl? peer = null;

        foreach (var candidate in _peers)
        {
            if (candidate.State == PeerState.Disconnected)
            {
                peer = candidate;
                break;
            }
        }

        if (peer is null)
            return null;

        peer.Channels = new Channel[channelCount];
        for (var i = 0; i < channelCount; ++i)
            peer.Channels[i] = new Channel();

        peer.ChannelCount = channelCount;
        peer.State = PeerState.Connecting;
        peer.Address = address;
        peer.ConnectId = ++RandomSeed;

        if (OutgoingBandwidth == 0)
            peer.WindowSize = MaximumWindowSize;
        else
            peer.WindowSize = (OutgoingBandwidth / PeerConstants.WindowSizeScale) * MinimumWindowSize;

        if (peer.WindowSize < MinimumWindowSize)
            peer.WindowSize = MinimumWindowSize;
        else if (peer.WindowSize > MaximumWindowSize)
            peer.WindowSize = MaximumWindowSize;

        var command = new ProtocolCommand
        {
            Type = CommandType.Connect,
            Command = (byte)CommandType.Connect | CommandFlagAcknowledge,
            ChannelId = 0xFF,
            OutgoingPeerId = (ushort)peer.IncomingPeerId,
            IncomingSessionId = peer.IncomingSessionId,
            OutgoingSessionId = peer.OutgoingSessionId,
            Mtu = peer.Mtu,
            WindowSize = peer.WindowSize,
            ChannelCount = (uint)channelCount,
            IncomingBandwidth = IncomingBandwidth,
            OutgoingBandwidth = OutgoingBandwidth,
            PacketThrottleInterval = peer.PacketThrottleInterval,
            PacketThrottleAcceleration = peer.PacketThrottleAcceleration,
            PacketThrottleDeceleration = peer.PacketThrottleDeceleration,
            ConnectId = peer.ConnectId,
            Data = data
        };

        peer.QueueOutgoingCommand(command, null, 0, 0);

        return peer;
    }

    private void EnsureSocketFor(Address address)
    {
        if (_socket is not null)
            return;

        CreateSocket(null, 0);

        var target = address.ResolvedAddress;

        if (target is null)
            return;

        var needsIpv6 = target.AddressFamily == AddressFamily.InterNetworkV6;

        if (!needsIpv6 && _dualMode)
            return;

        if (needsIpv6 && !_dualMode)
        {
            _socket!.Dispose();
            _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp) { DualMode = true };
            _socket!.Blocking = false;
            _socket.EnableBroadcast = true;
            _socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
            _dualMode = true;
        }
    }

    public void Broadcast(byte channelId, ref Packet packet)
    {
        ThrowIfNotCreated();
        packet.ThrowIfNotCreatedPublic();

        if ((packet.Flags & PacketFlags.Instant) != 0)
            packet.Content?.AddReference();

        foreach (var peer in _peers)
        {
            if (peer.State != PeerState.Connected)
                continue;

            peer.Send(channelId, ref packet);
        }

        FinishBroadcast(ref packet);
    }

    public void Broadcast(byte channelId, ref Packet packet, Peer excludedPeer)
    {
        ThrowIfNotCreated();
        packet.ThrowIfNotCreatedPublic();

        if ((packet.Flags & PacketFlags.Instant) != 0)
            packet.Content?.AddReference();

        foreach (var peer in _peers)
        {
            if (peer.State != PeerState.Connected || ReferenceEquals(peer, excludedPeer.Impl))
                continue;

            peer.Send(channelId, ref packet);
        }

        FinishBroadcast(ref packet);
    }

    public void Broadcast(byte channelId, ref Packet packet, Peer[] peers)
    {
        ArgumentNullException.ThrowIfNull(peers);
        ThrowIfNotCreated();

        if (peers.Length == 0)
        {
            packet.Dispose();
            throw new ArgumentOutOfRangeException(nameof(peers), "Peers array can't be empty");
        }

        packet.ThrowIfNotCreatedPublic();

        if ((packet.Flags & PacketFlags.Instant) != 0)
            packet.Content?.AddReference();

        foreach (var peer in peers)
        {
            if (peer.Impl is not { } impl || impl.State != PeerState.Connected)
                continue;

            var copy = packet;
            impl.Send(channelId, ref copy);
        }

        FinishBroadcast(ref packet);
    }

    private static void FinishBroadcast(ref Packet packet)
    {
        if ((packet.Flags & PacketFlags.Instant) != 0)
            packet.Content?.ReleaseReference();

        if (packet.Content is { ReferenceCount: 0 } content)
            content.Destroyed = true;

        packet = default;
    }

    public void Flush()
    {
        ThrowIfNotCreated();

        ServiceTime = Library.Time;
        Event? ev = null;
        SendOutgoingCommands(ref ev, false);
    }

    public int CheckEvents(out Event @event)
    {
        ThrowIfNotCreated();

        Event? ev = default(Event);
        var result = DispatchIncomingCommands(ref ev);

        @event = result > 0 && ev is { } value ? value : default;
        return result;
    }

    public int Service(int timeout, out Event @event)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        ThrowIfNotCreated();

        Event? ev = default(Event);

        switch (DispatchIncomingCommands(ref ev))
        {
            case 1:
                @event = ev!.Value;
                return 1;
            case -1:
                @event = default;
                return -1;
        }

        ServiceTime = Library.Time;
        var timeoutAt = unchecked((uint)timeout + ServiceTime);
        uint waitCondition;

        do
        {
            if (TimeDifference(ServiceTime, BandwidthThrottleEpoch) >= 1000)
                BandwidthThrottle();

            switch (SendOutgoingCommands(ref ev, true))
            {
                case 1:
                    @event = ev!.Value;
                    return 1;
                case -1:
                    @event = default;
                    return -1;
            }

            switch (ReceiveIncomingCommands(ref ev))
            {
                case 1:
                    @event = ev!.Value;
                    return 1;
                case -1:
                    @event = default;
                    return -1;
            }

            switch (SendOutgoingCommands(ref ev, true))
            {
                case 1:
                    @event = ev!.Value;
                    return 1;
                case -1:
                    @event = default;
                    return -1;
            }

            switch (DispatchIncomingCommands(ref ev))
            {
                case 1:
                    @event = ev!.Value;
                    return 1;
                case -1:
                    @event = default;
                    return -1;
            }

            if (TimeGreaterEqual(ServiceTime, timeoutAt))
            {
                @event = default;
                return 0;
            }

            do
            {
                ServiceTime = Library.Time;

                if (TimeGreaterEqual(ServiceTime, timeoutAt))
                {
                    @event = default;
                    return 0;
                }

                waitCondition = 2 | 4;

                if (SocketWait(ref waitCondition, TimeDifference(timeoutAt, ServiceTime)) != 0)
                {
                    @event = default;
                    return -1;
                }
            } while ((waitCondition & 4) != 0);

            ServiceTime = Library.Time;
        } while ((waitCondition & 2) != 0);

        @event = default;
        return 0;
    }

    private int SocketWait(ref uint condition, uint timeoutMs)
    {
        condition = 0;

        if (_socket is null)
        {
            Thread.Sleep((int)Math.Min(timeoutMs, 100));
            return 0;
        }

        var micros = (int)Math.Min((long)timeoutMs * 1000, int.MaxValue);

        try
        {
            if (_socket.Poll(micros, SelectMode.SelectRead))
                condition |= 2;
        }
        catch (SocketException)
        {
            condition |= 4;
        }
        catch (ObjectDisposedException)
        {
            return -1;
        }

        return 0;
    }

    private int SocketSend(PeerImpl peer, ReadOnlySpan<byte> data)
    {
        if (_socket is null)
            return -1;

        try
        {
            var endpoint = ToSocketEndPoint(peer.EndPoint);
            return _socket.SendTo(data, SocketFlags.None, endpoint);
        }
        catch (SocketException)
        {
            return -1;
        }
        catch (ObjectDisposedException)
        {
            return -1;
        }
    }

    private int SocketReceive(out Address address, Span<byte> data)
    {
        address = default;

        if (_socket is null)
            return -1;

        EndPoint remote = _dualMode
            ? new IPEndPoint(IPAddress.IPv6Any, 0)
            : new IPEndPoint(IPAddress.Any, 0);

        try
        {
            var received = _socket.ReceiveFrom(data, SocketFlags.None, ref remote);

            if (remote is IPEndPoint endPoint)
                address = new Address(Normalize(endPoint.Address), (ushort)endPoint.Port);

            return received;
        }
        catch (SocketException exception)
        {
            return exception.SocketErrorCode switch
            {
                SocketError.WouldBlock => 0,
                SocketError.Interrupted => -2,
                SocketError.ConnectionReset => -2,
                SocketError.MessageSize => 0,
                _ => -1
            };
        }
        catch (ObjectDisposedException)
        {
            return -1;
        }
    }

    private IPEndPoint ToSocketEndPoint(IPEndPoint endpoint)
    {
        if (_dualMode && endpoint.AddressFamily == AddressFamily.InterNetwork)
            return new IPEndPoint(endpoint.Address.MapToIPv6(), endpoint.Port);

        return endpoint;
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool SameAddress(in Address left, in Address right)
    {
        var leftIp = left.ResolvedAddress;
        var rightIp = right.ResolvedAddress;

        if (leftIp is null || rightIp is null)
            return leftIp is null && rightIp is null;

        return leftIp.Equals(rightIp) && left.Port == right.Port;
    }

    internal static uint TimeDifference(uint later, uint earlier) => unchecked(later - earlier);

    internal static bool TimeLess(uint left, uint right) => unchecked((int)(left - right)) < 0;

    internal static bool TimeGreaterEqual(uint left, uint right) => unchecked((int)(left - right)) >= 0;

    // ------------------------------------------------------------------
    // Protocol state
    // ------------------------------------------------------------------

    internal void ChangeState(PeerImpl peer, PeerState state)
    {
        if (state is PeerState.Connected or PeerState.DisconnectLater)
            peer.OnConnect();
        else
            peer.OnDisconnect();

        peer.State = state;
    }

    internal void DispatchState(PeerImpl peer, PeerState state)
    {
        ChangeState(peer, state);

        if (!peer.NeedsDispatch)
        {
            DispatchQueue.InsertEnd(peer);
            peer.NeedsDispatch = true;
        }
    }

    private int DispatchIncomingCommands(ref Event? @event)
    {
        while (!DispatchQueue.IsEmpty)
        {
            var peer = DispatchQueue.RemoveFirst()!;
            peer.NeedsDispatch = false;

            switch (peer.State)
            {
                case PeerState.ConnectionPending:
                case PeerState.ConnectionSucceeded:
                {
                    ChangeState(peer, PeerState.Connected);
                    @event = new Event(EventType.Connect, peer, peer.EventData);
                    return 1;
                }

                case PeerState.Zombie:
                {
                    RecalculateBandwidthLimits = true;
                    var data = peer.EventData;
                    var zombie = peer;
                    peer.Reset();
                    @event = new Event(EventType.Disconnect, zombie, data);
                    return 1;
                }

                case PeerState.Connected:
                {
                    if (peer.DispatchedCommands.IsEmpty)
                        continue;

                    var packet = peer.Receive(out var channelId);

                    if (!packet.IsSet)
                        continue;

                    @event = new Event(EventType.Receive, peer, channelId: channelId, packet: packet);

                    if (!peer.DispatchedCommands.IsEmpty)
                    {
                        peer.NeedsDispatch = true;
                        DispatchQueue.InsertEnd(peer);
                    }

                    return 1;
                }

                default:
                    break;
            }
        }

        return 0;
    }

    private void NotifyConnect(PeerImpl peer, ref Event? @event)
    {
        RecalculateBandwidthLimits = true;

        if (@event is not null)
        {
            ChangeState(peer, PeerState.Connected);

            peer.TotalDataSent = 0;
            peer.TotalDataReceived = 0;
            peer.TotalPacketsSent = 0;
            peer.TotalPacketsLost = 0;

            @event = new Event(EventType.Connect, peer, peer.EventData);
        }
        else
        {
            DispatchState(
                peer,
                peer.State == PeerState.Connecting ? PeerState.ConnectionSucceeded : PeerState.ConnectionPending
            );
        }
    }

    private void NotifyDisconnect(PeerImpl peer, ref Event? @event)
    {
        if (peer.State >= PeerState.ConnectionPending)
            RecalculateBandwidthLimits = true;

        if (peer.State != PeerState.Connecting && peer.State < PeerState.ConnectionSucceeded)
        {
            peer.Reset();
        }
        else if (@event is not null)
        {
            var disconnected = peer;
            peer.Reset();
            @event = new Event(EventType.Disconnect, disconnected);
        }
        else
        {
            peer.EventData = 0;
            DispatchState(peer, PeerState.Zombie);
        }
    }

    private void NotifyDisconnectTimeout(PeerImpl peer, ref Event? @event)
    {
        if (peer.State >= PeerState.ConnectionPending)
            RecalculateBandwidthLimits = true;

        if (peer.State != PeerState.Connecting && peer.State < PeerState.ConnectionSucceeded)
        {
            peer.Reset();
        }
        else if (@event is not null)
        {
            var disconnected = peer;
            peer.Reset();
            @event = new Event(EventType.Timeout, disconnected);
        }
        else
        {
            peer.EventData = 0;
            DispatchState(peer, PeerState.Zombie);
        }
    }

    private void RemoveSentUnreliableCommands(PeerImpl peer)
    {
        while (_sentUnreliableCommands.RemoveFirst() is { } outgoingCommand)
            outgoingCommand.Packet?.ReleaseAndDestroyIfUnreferenced();

        if (peer.State == PeerState.DisconnectLater && !peer.HasOutgoingCommands)
            peer.Disconnect(peer.EventData);
    }

    private static OutgoingCommand? FindSentReliableCommand(
        EnetList<OutgoingCommand> list,
        ushort reliableSequenceNumber,
        byte channelId
    )
    {
        var current = list.Begin;

        while (current is not null)
        {
            var outgoingCommand = current;

            if ((outgoingCommand.Command.Command & CommandFlagAcknowledge) != 0)
            {
                if (outgoingCommand.SendAttempts < 1)
                    break;

                if (outgoingCommand.ReliableSequenceNumber == reliableSequenceNumber
                    && outgoingCommand.Command.ChannelId == channelId)
                    return outgoingCommand;
            }

            current = current.Next;
        }

        return null;
    }

    private static CommandType RemoveSentReliableCommand(PeerImpl peer, ushort reliableSequenceNumber, byte channelId)
    {
        OutgoingCommand? outgoingCommand = null;
        EnetList<OutgoingCommand>? list = null;
        var wasSent = true;

        var current = peer.SentReliableCommands.Begin;

        while (current is not null)
        {
            var candidate = current;
            current = current.Next;

            if (candidate.ReliableSequenceNumber == reliableSequenceNumber
                && candidate.Command.ChannelId == channelId)
            {
                outgoingCommand = candidate;
                list = peer.SentReliableCommands;
                break;
            }
        }

        if (outgoingCommand is null)
        {
            outgoingCommand = FindSentReliableCommand(peer.OutgoingCommands, reliableSequenceNumber, channelId);
            list = peer.OutgoingCommands;

            if (outgoingCommand is null)
            {
                outgoingCommand = FindSentReliableCommand(
                    peer.OutgoingSendReliableCommands,
                    reliableSequenceNumber,
                    channelId
                );
                list = peer.OutgoingSendReliableCommands;
            }

            wasSent = false;
        }

        if (outgoingCommand is null)
            return CommandType.None;

        if (channelId < peer.ChannelCount)
        {
            var channel = peer.Channels![channelId];
            var reliableWindow = reliableSequenceNumber / PeerConstants.ReliableWindowSize;

            if (channel.ReliableWindows[reliableWindow] > 0)
            {
                --channel.ReliableWindows[reliableWindow];

                if (channel.ReliableWindows[reliableWindow] == 0)
                    channel.UsedReliableWindows &= unchecked((ushort)~(1u << reliableWindow));
            }
        }

        var commandNumber = (CommandType)(outgoingCommand.Command.Command & CommandMask);
        list!.Remove(outgoingCommand);

        if (outgoingCommand.Packet is { } packet)
        {
            if (wasSent)
                peer.ReliableDataInTransit -= outgoingCommand.FragmentLength;

            packet.ReleaseAndDestroyIfUnreferenced();
        }

        if (!peer.SentReliableCommands.IsEmpty)
        {
            var first = peer.SentReliableCommands.Begin!;
            peer.NextTimeout = first.SentTime + first.RoundTripTimeout;
        }

        return commandNumber;
    }

    private PeerImpl? HandleConnect(in ProtocolCommand command)
    {
        var channelCount = command.ChannelCount;

        if (channelCount < MinimumChannelCount || channelCount > MaximumChannelCount)
            return null;

        PeerImpl? peer = null;
        var duplicatePeers = 0;

        foreach (var currentPeer in _peers)
        {
            if (currentPeer.State == PeerState.Disconnected)
            {
                peer ??= currentPeer;
            }
            else if (currentPeer.State != PeerState.Connecting
                     && SameAddress(currentPeer.Address, ReceivedAddress))
            {
                if (currentPeer.Address.Port == ReceivedAddress.Port
                    && currentPeer.ConnectId == command.ConnectId)
                    return null;

                ++duplicatePeers;
            }
        }

        if (peer is null || duplicatePeers >= DuplicatePeers)
            return null;

        if (channelCount > ChannelLimit)
            channelCount = ChannelLimit;

        peer.Channels = new Channel[channelCount];
        for (var i = 0; i < channelCount; ++i)
            peer.Channels[i] = new Channel();

        peer.ChannelCount = (int)channelCount;
        peer.State = PeerState.AcknowledgingConnect;
        peer.ConnectId = command.ConnectId;
        peer.Address = ReceivedAddress;
        peer.OutgoingPeerId = command.OutgoingPeerId;
        peer.IncomingBandwidth = command.IncomingBandwidth;
        peer.OutgoingBandwidth = command.OutgoingBandwidth;
        peer.PacketThrottleInterval = command.PacketThrottleInterval;
        peer.PacketThrottleAcceleration = command.PacketThrottleAcceleration;
        peer.PacketThrottleDeceleration = command.PacketThrottleDeceleration;
        peer.EventData = command.Data;

        var incomingSessionId = command.IncomingSessionId == 0xFF ? peer.OutgoingSessionId : command.IncomingSessionId;
        incomingSessionId = (byte)((incomingSessionId + 1) & (HeaderSessionMask >> HeaderSessionShift));

        if (incomingSessionId == peer.OutgoingSessionId)
            incomingSessionId = (byte)((incomingSessionId + 1) & (HeaderSessionMask >> HeaderSessionShift));

        peer.OutgoingSessionId = incomingSessionId;

        var outgoingSessionId = command.OutgoingSessionId == 0xFF ? peer.IncomingSessionId : command.OutgoingSessionId;
        outgoingSessionId = (byte)((outgoingSessionId + 1) & (HeaderSessionMask >> HeaderSessionShift));

        if (outgoingSessionId == peer.IncomingSessionId)
            outgoingSessionId = (byte)((outgoingSessionId + 1) & (HeaderSessionMask >> HeaderSessionShift));

        peer.IncomingSessionId = outgoingSessionId;

        var mtu = command.Mtu;

        if (mtu < MinimumMtu)
            mtu = MinimumMtu;
        else if (mtu > MaximumMtu)
            mtu = MaximumMtu;

        if (mtu < peer.Mtu)
            peer.Mtu = mtu;

        if (OutgoingBandwidth == 0 && peer.IncomingBandwidth == 0)
            peer.WindowSize = MaximumWindowSize;
        else if (OutgoingBandwidth == 0 || peer.IncomingBandwidth == 0)
            peer.WindowSize = (Math.Max(OutgoingBandwidth, peer.IncomingBandwidth) / PeerConstants.WindowSizeScale)
                              * MinimumWindowSize;
        else
            peer.WindowSize = (Math.Min(OutgoingBandwidth, peer.IncomingBandwidth) / PeerConstants.WindowSizeScale)
                              * MinimumWindowSize;

        if (peer.WindowSize < MinimumWindowSize)
            peer.WindowSize = MinimumWindowSize;
        else if (peer.WindowSize > MaximumWindowSize)
            peer.WindowSize = MaximumWindowSize;

        uint windowSize;

        if (IncomingBandwidth == 0)
            windowSize = MaximumWindowSize;
        else
            windowSize = (IncomingBandwidth / PeerConstants.WindowSizeScale) * MinimumWindowSize;

        if (windowSize > command.WindowSize)
            windowSize = command.WindowSize;

        if (windowSize < MinimumWindowSize)
            windowSize = MinimumWindowSize;
        else if (windowSize > MaximumWindowSize)
            windowSize = MaximumWindowSize;

        var verifyCommand = new ProtocolCommand
        {
            Type = CommandType.VerifyConnect,
            Command = (byte)CommandType.VerifyConnect | CommandFlagAcknowledge,
            ChannelId = 0xFF,
            OutgoingPeerId = (ushort)peer.IncomingPeerId,
            IncomingSessionId = incomingSessionId,
            OutgoingSessionId = outgoingSessionId,
            Mtu = peer.Mtu,
            WindowSize = windowSize,
            ChannelCount = channelCount,
            IncomingBandwidth = IncomingBandwidth,
            OutgoingBandwidth = OutgoingBandwidth,
            PacketThrottleInterval = peer.PacketThrottleInterval,
            PacketThrottleAcceleration = peer.PacketThrottleAcceleration,
            PacketThrottleDeceleration = peer.PacketThrottleDeceleration,
            ConnectId = peer.ConnectId
        };

        peer.QueueOutgoingCommand(verifyCommand, null, 0, 0);

        return peer;
    }

    private static int HandleSendReliable(
        in ProtocolCommand command,
        PeerImpl peer,
        ReadOnlySpan<byte> data,
        ref int currentData
    )
    {
        if (command.ChannelId >= peer.ChannelCount
            || (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater))
            return -1;

        var dataLength = command.DataLength;
        currentData += dataLength;

        if (dataLength > peer.Host.MaximumPacketSize || currentData > data.Length)
            return -1;

        var payload = data.Slice(currentData - dataLength, dataLength);

        if (peer.QueueIncomingCommand(command, payload, dataLength, PacketFlags.Reliable, 0) is null)
            return -1;

        return 0;
    }

    private static int HandleSendUnsequenced(
        in ProtocolCommand command,
        PeerImpl peer,
        ReadOnlySpan<byte> data,
        ref int currentData
    )
    {
        if (command.ChannelId >= peer.ChannelCount
            || (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater))
            return -1;

        var dataLength = command.DataLength;
        currentData += dataLength;

        if (dataLength > peer.Host.MaximumPacketSize || currentData > data.Length)
            return -1;

        var unsequencedGroup = (uint)command.UnsequencedGroup;
        var index = unsequencedGroup % (uint)PeerConstants.UnsequencedWindowSize;

        if (unsequencedGroup < peer.IncomingUnsequencedGroup)
            unsequencedGroup += 0x10000;

        if (unsequencedGroup
            >= (uint)peer.IncomingUnsequencedGroup
            + (uint)(PeerConstants.FreeUnsequencedWindows * PeerConstants.UnsequencedWindowSize))
            return 0;

        unsequencedGroup &= 0xFFFF;

        if (unsequencedGroup - index != peer.IncomingUnsequencedGroup)
        {
            peer.IncomingUnsequencedGroup = unchecked((ushort)(unsequencedGroup - index));
            Array.Clear(peer.UnsequencedWindow);
        }
        else if ((peer.UnsequencedWindow[index / 32] & (1u << (int)(index % 32))) != 0)
        {
            return 0;
        }

        var payload = data.Slice(currentData - dataLength, dataLength);

        if (peer.QueueIncomingCommand(command, payload, dataLength, PacketFlags.Unsequenced, 0) is null)
            return -1;

        peer.UnsequencedWindow[index / 32] |= 1u << (int)(index % 32);

        return 0;
    }

    private static int HandleSendUnreliable(
        in ProtocolCommand command,
        PeerImpl peer,
        ReadOnlySpan<byte> data,
        ref int currentData
    )
    {
        if (command.ChannelId >= peer.ChannelCount
            || (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater))
            return -1;

        var dataLength = command.DataLength;
        currentData += dataLength;

        if (dataLength > peer.Host.MaximumPacketSize || currentData > data.Length)
            return -1;

        var payload = data.Slice(currentData - dataLength, dataLength);

        if (peer.QueueIncomingCommand(command, payload, dataLength, 0, 0) is null)
            return -1;

        return 0;
    }

    private static int HandleSendFragment(
        in ProtocolCommand command,
        PeerImpl peer,
        ReadOnlySpan<byte> data,
        ref int currentData
    )
    {
        if (command.ChannelId >= peer.ChannelCount
            || (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater))
            return -1;

        var fragmentLength = (uint)command.DataLength;
        currentData += (int)fragmentLength;

        if (fragmentLength == 0 || fragmentLength > peer.Host.MaximumPacketSize || currentData > data.Length)
            return -1;

        var channel = peer.Channels![command.ChannelId];
        var startSequenceNumber = command.StartSequenceNumber;
        int startWindow = startSequenceNumber / PeerConstants.ReliableWindowSize;
        int currentWindow = channel.IncomingReliableSequenceNumber / PeerConstants.ReliableWindowSize;

        if (startSequenceNumber < channel.IncomingReliableSequenceNumber)
            startWindow += PeerConstants.ReliableWindows;

        if (startWindow < currentWindow || startWindow >= currentWindow + PeerConstants.FreeReliableWindows - 1)
            return 0;

        var fragmentNumber = command.FragmentNumber;
        var fragmentCount = command.FragmentCount;
        var fragmentOffset = command.FragmentOffset;
        var totalLength = command.TotalLength;

        if (fragmentCount > MaximumFragmentCount
            || fragmentNumber >= fragmentCount
            || totalLength > (uint)peer.Host.MaximumPacketSize
            || totalLength < fragmentCount
            || fragmentOffset >= totalLength
            || fragmentLength > totalLength - fragmentOffset)
            return -1;

        IncomingCommand? startCommand = null;
        var node = channel.IncomingReliableCommands.Tail;

        while (node is not null)
        {
            var incomingCommand = node;

            if (startSequenceNumber >= channel.IncomingReliableSequenceNumber)
            {
                if (incomingCommand.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                {
                    node = incomingCommand.Previous;
                    continue;
                }
            }
            else if (incomingCommand.ReliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
            {
                break;
            }

            if (incomingCommand.ReliableSequenceNumber <= startSequenceNumber)
            {
                if (incomingCommand.ReliableSequenceNumber < startSequenceNumber)
                    break;

                if (incomingCommand.Command.Type != CommandType.SendFragment
                    || totalLength != (uint)incomingCommand.Packet!.Length
                    || fragmentCount != incomingCommand.FragmentCount)
                    return -1;

                startCommand = incomingCommand;
                break;
            }

            node = incomingCommand.Previous;
        }

        if (startCommand is null)
        {
            var hostCommand = command;
            hostCommand.ReliableSequenceNumber = startSequenceNumber;

            startCommand = peer.QueueIncomingCommand(
                hostCommand,
                default,
                (int)totalLength,
                PacketFlags.Reliable,
                fragmentCount
            );

            if (startCommand is null)
                return -1;
        }

        var fragments = startCommand.Fragments!;

        if ((fragments[fragmentNumber / 32] & (1u << (int)(fragmentNumber % 32))) == 0)
        {
            --startCommand.FragmentsRemaining;
            fragments[fragmentNumber / 32] |= 1u << (int)(fragmentNumber % 32);

            if (fragmentOffset + fragmentLength > (uint)startCommand.Packet!.Length)
                fragmentLength = (uint)(startCommand.Packet.Length - (int)fragmentOffset);

            data.Slice(currentData - (int)fragmentLength, (int)fragmentLength)
                .CopyTo(startCommand.Packet.Buffer.AsSpan((int)fragmentOffset));

            if (startCommand.FragmentsRemaining <= 0)
                peer.DispatchIncomingReliableCommands(channel, null);
        }

        return 0;
    }

    private static int HandleSendUnreliableFragment(
        in ProtocolCommand command,
        PeerImpl peer,
        ReadOnlySpan<byte> data,
        ref int currentData
    )
    {
        if (command.ChannelId >= peer.ChannelCount
            || (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater))
            return -1;

        var fragmentLength = (uint)command.DataLength;
        currentData += (int)fragmentLength;

        if (fragmentLength > peer.Host.MaximumPacketSize || currentData > data.Length)
            return -1;

        var channel = peer.Channels![command.ChannelId];
        var reliableSequenceNumber = (uint)command.ReliableSequenceNumber;
        var startSequenceNumber = command.StartSequenceNumber;

        int reliableWindow = (int)(reliableSequenceNumber / PeerConstants.ReliableWindowSize);
        int currentWindow = channel.IncomingReliableSequenceNumber / PeerConstants.ReliableWindowSize;

        if (reliableSequenceNumber < channel.IncomingReliableSequenceNumber)
            reliableWindow += PeerConstants.ReliableWindows;

        if (reliableWindow < currentWindow || reliableWindow >= currentWindow + PeerConstants.FreeReliableWindows - 1)
            return 0;

        if (reliableSequenceNumber == channel.IncomingReliableSequenceNumber
            && startSequenceNumber <= channel.IncomingUnreliableSequenceNumber)
            return 0;

        var fragmentNumber = command.FragmentNumber;
        var fragmentCount = command.FragmentCount;
        var fragmentOffset = command.FragmentOffset;
        var totalLength = command.TotalLength;

        if (fragmentCount > MaximumFragmentCount
            || fragmentNumber >= fragmentCount
            || totalLength > (uint)peer.Host.MaximumPacketSize
            || fragmentOffset >= totalLength
            || fragmentLength > totalLength - fragmentOffset)
            return -1;

        IncomingCommand? startCommand = null;
        var node = channel.IncomingUnreliableCommands.Tail;

        while (node is not null)
        {
            var incomingCommand = node;

            if (reliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
            {
                if (incomingCommand.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                {
                    node = incomingCommand.Previous;
                    continue;
                }
            }
            else if (incomingCommand.ReliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
            {
                break;
            }

            if (incomingCommand.ReliableSequenceNumber < reliableSequenceNumber)
                break;

            if (incomingCommand.ReliableSequenceNumber > reliableSequenceNumber)
            {
                node = incomingCommand.Previous;
                continue;
            }

            if (incomingCommand.UnreliableSequenceNumber <= startSequenceNumber)
            {
                if (incomingCommand.UnreliableSequenceNumber < startSequenceNumber)
                    break;

                if (incomingCommand.Command.Type != CommandType.SendUnreliableFragment
                    || totalLength != (uint)incomingCommand.Packet!.Length
                    || fragmentCount != incomingCommand.FragmentCount)
                    return -1;

                startCommand = incomingCommand;
                break;
            }

            node = incomingCommand.Previous;
        }

        if (startCommand is null)
        {
            startCommand = peer.QueueIncomingCommand(
                command,
                default,
                (int)totalLength,
                PacketFlags.UnreliableFragmented,
                fragmentCount
            );

            if (startCommand is null)
                return -1;
        }

        var fragments = startCommand.Fragments!;

        if ((fragments[fragmentNumber / 32] & (1u << (int)(fragmentNumber % 32))) == 0)
        {
            --startCommand.FragmentsRemaining;
            fragments[fragmentNumber / 32] |= 1u << (int)(fragmentNumber % 32);

            if (fragmentOffset + fragmentLength > (uint)startCommand.Packet!.Length)
                fragmentLength = (uint)(startCommand.Packet.Length - (int)fragmentOffset);

            data.Slice(currentData - (int)fragmentLength, (int)fragmentLength)
                .CopyTo(startCommand.Packet.Buffer.AsSpan((int)fragmentOffset));

            if (startCommand.FragmentsRemaining <= 0)
                peer.DispatchIncomingUnreliableCommands(channel, null);
        }

        return 0;
    }

    private static int HandlePing(PeerImpl peer)
    {
        if (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater)
            return -1;

        return 0;
    }

    private int HandleBandwidthLimit(in ProtocolCommand command, PeerImpl peer)
    {
        if (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater)
            return -1;

        if (peer.IncomingBandwidth != 0)
            --BandwidthLimitedPeers;

        peer.IncomingBandwidth = command.IncomingBandwidth;
        peer.OutgoingBandwidth = command.OutgoingBandwidth;

        if (peer.IncomingBandwidth != 0)
            ++BandwidthLimitedPeers;

        if (peer.IncomingBandwidth == 0 && OutgoingBandwidth == 0)
            peer.WindowSize = MaximumWindowSize;
        else if (peer.IncomingBandwidth == 0 || OutgoingBandwidth == 0)
            peer.WindowSize = (Math.Max(peer.IncomingBandwidth, OutgoingBandwidth) / PeerConstants.WindowSizeScale)
                              * MinimumWindowSize;
        else
            peer.WindowSize = (Math.Min(peer.IncomingBandwidth, OutgoingBandwidth) / PeerConstants.WindowSizeScale)
                              * MinimumWindowSize;

        if (peer.WindowSize < MinimumWindowSize)
            peer.WindowSize = MinimumWindowSize;
        else if (peer.WindowSize > MaximumWindowSize)
            peer.WindowSize = MaximumWindowSize;

        return 0;
    }

    private static int HandleThrottleConfigure(in ProtocolCommand command, PeerImpl peer)
    {
        if (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater)
            return -1;

        peer.PacketThrottleInterval = command.PacketThrottleInterval;
        peer.PacketThrottleAcceleration = command.PacketThrottleAcceleration;
        peer.PacketThrottleDeceleration = command.PacketThrottleDeceleration;

        return 0;
    }

    private int HandleDisconnect(in ProtocolCommand command, PeerImpl peer)
    {
        if (peer.State is PeerState.Disconnected or PeerState.Zombie or PeerState.AcknowledgingDisconnect)
            return 0;

        peer.ResetQueues();

        if (peer.State is PeerState.ConnectionSucceeded or PeerState.Disconnecting or PeerState.Connecting)
        {
            DispatchState(peer, PeerState.Zombie);
        }
        else if (peer.State != PeerState.Connected && peer.State != PeerState.DisconnectLater)
        {
            if (peer.State == PeerState.ConnectionPending)
                RecalculateBandwidthLimits = true;

            peer.Reset();
        }
        else if ((command.Command & CommandFlagAcknowledge) != 0)
        {
            ChangeState(peer, PeerState.AcknowledgingDisconnect);
        }
        else
        {
            DispatchState(peer, PeerState.Zombie);
        }

        if (peer.State != PeerState.Disconnected)
            peer.EventData = command.Data;

        return 0;
    }

    private int HandleAcknowledge(in ProtocolCommand command, ref Event? @event, PeerImpl peer)
    {
        if (peer.State is PeerState.Disconnected or PeerState.Zombie)
            return 0;

        var receivedSentTime = (uint)command.ReceivedSentTime;
        receivedSentTime |= ServiceTime & 0xFFFF0000;

        if ((receivedSentTime & 0x8000) > (ServiceTime & 0x8000))
            receivedSentTime = unchecked(receivedSentTime - 0x10000);

        if (TimeLess(ServiceTime, receivedSentTime))
            return 0;

        var roundTripTime = TimeDifference(ServiceTime, receivedSentTime);

        if (roundTripTime == 0)
            roundTripTime = 1;

        peer.Throttle(roundTripTime);

        if (peer.LastReceiveTime > 0)
        {
            if (roundTripTime >= peer.RoundTripTime)
            {
                var diff = roundTripTime - peer.RoundTripTime;
                peer.RoundTripTimeVariance -= peer.RoundTripTimeVariance / 4;
                peer.RoundTripTimeVariance += diff / 4;
                peer.RoundTripTime += diff / 8;
            }
            else
            {
                var diff = peer.RoundTripTime - roundTripTime;

                if (diff <= peer.RoundTripTimeVariance)
                {
                    peer.RoundTripTimeVariance -= peer.RoundTripTimeVariance / 4;
                    peer.RoundTripTimeVariance += diff / 4;
                }
                else
                {
                    peer.RoundTripTimeVariance -= peer.RoundTripTimeVariance / 32;
                    peer.RoundTripTimeVariance += diff / 32;
                }

                peer.RoundTripTime -= diff / 8;
            }
        }
        else
        {
            peer.RoundTripTime = roundTripTime;
            peer.RoundTripTimeVariance = roundTripTime / 2;
        }

        if (peer.RoundTripTime < peer.LowestRoundTripTime)
            peer.LowestRoundTripTime = peer.RoundTripTime;

        if (peer.RoundTripTimeVariance > peer.HighestRoundTripTimeVariance)
            peer.HighestRoundTripTimeVariance = peer.RoundTripTimeVariance;

        if (peer.PacketThrottleEpoch == 0
            || TimeDifference(ServiceTime, peer.PacketThrottleEpoch) >= peer.PacketThrottleInterval)
        {
            peer.LastRoundTripTime = peer.LowestRoundTripTime;
            peer.LastRoundTripTimeVariance = peer.HighestRoundTripTimeVariance;
            peer.LowestRoundTripTime = peer.RoundTripTime;
            peer.HighestRoundTripTimeVariance = peer.RoundTripTimeVariance;
            peer.PacketThrottleEpoch = ServiceTime;
        }

        peer.LastReceiveTime = Math.Max(ServiceTime, 1);
        peer.EarliestTimeout = 0;

        var receivedReliableSequenceNumber = command.ReceivedReliableSequenceNumber;

        var commandNumber = RemoveSentReliableCommand(peer, receivedReliableSequenceNumber, command.ChannelId);

        switch (peer.State)
        {
            case PeerState.AcknowledgingConnect:
                if (commandNumber != CommandType.VerifyConnect)
                    return -1;

                NotifyConnect(peer, ref @event);
                break;

            case PeerState.Disconnecting:
                if (commandNumber != CommandType.Disconnect)
                    return -1;

                NotifyDisconnect(peer, ref @event);
                break;

            case PeerState.DisconnectLater:
                if (!peer.HasOutgoingCommands)
                    peer.Disconnect(peer.EventData);
                break;
        }

        return 0;
    }

    private int HandleVerifyConnect(in ProtocolCommand command, ref Event? @event, PeerImpl peer)
    {
        if (peer.State != PeerState.Connecting)
            return 0;

        var channelCount = command.ChannelCount;

        if (channelCount < MinimumChannelCount
            || channelCount > MaximumChannelCount
            || command.PacketThrottleInterval != peer.PacketThrottleInterval
            || command.PacketThrottleAcceleration != peer.PacketThrottleAcceleration
            || command.PacketThrottleDeceleration != peer.PacketThrottleDeceleration
            || command.ConnectId != peer.ConnectId)
        {
            peer.EventData = 0;
            DispatchState(peer, PeerState.Zombie);
            return -1;
        }

        RemoveSentReliableCommand(peer, 1, 0xFF);

        if (channelCount < peer.ChannelCount)
            peer.ChannelCount = (int)channelCount;

        peer.OutgoingPeerId = command.OutgoingPeerId;
        peer.IncomingSessionId = command.IncomingSessionId;
        peer.OutgoingSessionId = command.OutgoingSessionId;

        var mtu = command.Mtu;

        if (mtu < MinimumMtu)
            mtu = MinimumMtu;
        else if (mtu > MaximumMtu)
            mtu = MaximumMtu;

        if (mtu < peer.Mtu)
            peer.Mtu = mtu;

        var windowSize = command.WindowSize;

        if (windowSize < MinimumWindowSize)
            windowSize = MinimumWindowSize;

        if (windowSize > MaximumWindowSize)
            windowSize = MaximumWindowSize;

        if (windowSize < peer.WindowSize)
            peer.WindowSize = windowSize;

        peer.IncomingBandwidth = command.IncomingBandwidth;
        peer.OutgoingBandwidth = command.OutgoingBandwidth;

        NotifyConnect(peer, ref @event);

        return 0;
    }

    private int HandleIncomingCommands(ref Event? @event)
    {
        var data = ReceivedData.AsSpan(0, ReceivedDataLength);

        if (data.Length < 2)
            return 0;

        var peerId = BinaryPrimitives.ReadUInt16BigEndian(data);
        var sessionId = (peerId & HeaderSessionMask) >> HeaderSessionShift;
        var flags = (ushort)(peerId & HeaderFlagMask);
        peerId &= unchecked((ushort)~(HeaderFlagMask | HeaderSessionMask));

        var headerSize = (flags & HeaderFlagSentTime) != 0 ? HeaderSize : 2;

        if (HasChecksum)
            headerSize += ChecksumSize;

        if (data.Length < headerSize)
            return 0;

        PeerImpl? peer;

        if (peerId == MaximumPeerId)
        {
            peer = null;
        }
        else if (peerId >= _peers.Length)
        {
            return 0;
        }
        else
        {
            peer = _peers[peerId];

            var addressMismatch = !SameAddress(ReceivedAddress, peer.Address);
            var broadcast = peer.Address.ResolvedAddress is { } peerIp && peerIp.Equals(IPAddress.Broadcast);

            if (peer.State is PeerState.Disconnected or PeerState.Zombie
                || (addressMismatch && !broadcast)
                || (peer.OutgoingPeerId < MaximumPeerId && sessionId != peer.IncomingSessionId))
                return 0;
        }

        if (HasChecksum && Checksum is { } checksumCallback)
        {
            var checksumOffset = headerSize - ChecksumSize;
            var desiredChecksum = BinaryPrimitives.ReadUInt64BigEndian(data[checksumOffset..]);
            var newChecksum = peer is not null ? peer.ConnectId : 0u;

            BinaryPrimitives.WriteUInt64BigEndian(ReceivedData.AsSpan(checksumOffset), newChecksum);

            if (checksumCallback(data) != desiredChecksum)
                return 0;
        }

        if (peer is not null)
        {
            peer.Address = ReceivedAddress;
            peer.IncomingDataTotal += (uint)data.Length;
            peer.TotalDataReceived += (uint)data.Length;
        }

        var currentData = headerSize;

        while (currentData < data.Length)
        {
            if (!Protocol.TryRead(data, currentData, out var command, out var commandSize))
                break;

            currentData += commandSize;

            if (peer is null && (command.Type != CommandType.Connect || currentData < data.Length))
                break;

            switch (command.Type)
            {
                case CommandType.Acknowledge:
                    if (peer is null || HandleAcknowledge(command, ref @event, peer) != 0)
                        goto commandError;
                    break;

                case CommandType.Connect:
                    if (peer is not null)
                        goto commandError;

                    if (!ConnectionsPrevented)
                    {
                        peer = HandleConnect(command);

                        if (peer is null)
                            goto commandError;
                    }

                    break;

                case CommandType.VerifyConnect:
                    if (peer is null || HandleVerifyConnect(command, ref @event, peer) != 0)
                        goto commandError;
                    break;

                case CommandType.Disconnect:
                    if (peer is null || HandleDisconnect(command, peer) != 0)
                        goto commandError;
                    break;

                case CommandType.Ping:
                    if (peer is null || HandlePing(peer) != 0)
                        goto commandError;
                    break;

                case CommandType.SendReliable:
                    if (peer is null || HandleSendReliable(command, peer, data, ref currentData) != 0)
                        goto commandError;
                    break;

                case CommandType.SendUnreliable:
                    if (peer is null || HandleSendUnreliable(command, peer, data, ref currentData) != 0)
                        goto commandError;
                    break;

                case CommandType.SendUnsequenced:
                    if (peer is null || HandleSendUnsequenced(command, peer, data, ref currentData) != 0)
                        goto commandError;
                    break;

                case CommandType.SendFragment:
                    if (peer is null || HandleSendFragment(command, peer, data, ref currentData) != 0)
                        goto commandError;
                    break;

                case CommandType.BandwidthLimit:
                    if (peer is null || HandleBandwidthLimit(command, peer) != 0)
                        goto commandError;
                    break;

                case CommandType.ThrottleConfigure:
                    if (peer is null || HandleThrottleConfigure(command, peer) != 0)
                        goto commandError;
                    break;

                case CommandType.SendUnreliableFragment:
                    if (peer is null || HandleSendUnreliableFragment(command, peer, data, ref currentData) != 0)
                        goto commandError;
                    break;

                default:
                    goto commandError;
            }

            if (peer is not null && (command.Command & CommandFlagAcknowledge) != 0)
            {
                if ((flags & HeaderFlagSentTime) == 0)
                    break;

                var sentTime = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);

                switch (peer.State)
                {
                    case PeerState.Disconnecting:
                    case PeerState.AcknowledgingConnect:
                    case PeerState.Disconnected:
                    case PeerState.Zombie:
                        break;

                    case PeerState.AcknowledgingDisconnect:
                        if (command.Type == CommandType.Disconnect)
                            peer.QueueAcknowledgement(command, sentTime);
                        break;

                    default:
                        peer.QueueAcknowledgement(command, sentTime);
                        break;
                }
            }
        }

        commandError:

        if (@event is { Type: not EventType.None })
            return 1;

        return 0;
    }

    private int ReceiveIncomingCommands(ref Event? @event)
    {
        for (var packets = 0; packets < 256; ++packets)
        {
            var receivedLength = SocketReceive(out var address, _receiveBuffer.AsSpan(0, (int)Mtu));

            if (receivedLength == -2)
                continue;

            if (receivedLength < 0)
                return -1;

            if (receivedLength == 0)
                return 0;

            ReceivedAddress = address;
            ReceivedData = _receiveBuffer;
            ReceivedDataLength = receivedLength;
            TotalReceivedData += (uint)receivedLength;
            ++TotalReceivedPackets;

            if (Intercept is { } intercept)
            {
                Event? intercepted = null;

                switch (intercept(ref intercepted, ref address, _receiveBuffer.AsSpan(0, receivedLength)))
                {
                    case 1:
                        if (intercepted is { Type: not EventType.None } interceptedEvent)
                        {
                            @event = interceptedEvent;
                            return 1;
                        }

                        continue;

                    case -1:
                        return -1;
                }
            }

            switch (HandleIncomingCommands(ref @event))
            {
                case 1:
                    return 1;

                case -1:
                    return -1;
            }
        }

        return 0;
    }

    private void SendAcknowledgements(PeerImpl peer)
    {
        var current = peer.Acknowledgements.Begin;

        while (current is not null)
        {
            if (CommandCount >= MaximumPacketCommands
                || BufferCount >= BufferMaximum
                || (long)peer.Mtu - PacketSize < Protocol.CommandSizes[(int)CommandType.Acknowledge])
            {
                peer.ContinueSending = true;
                break;
            }

            var acknowledgement = current;
            current = current.Next;

            var reliableSequenceNumber = acknowledgement.Command.ReliableSequenceNumber;

            var command = new ProtocolCommand
            {
                Type = CommandType.Acknowledge,
                Command = (byte)CommandType.Acknowledge,
                ChannelId = acknowledgement.Command.ChannelId,
                ReliableSequenceNumber = reliableSequenceNumber,
                ReceivedReliableSequenceNumber = reliableSequenceNumber,
                ReceivedSentTime = (ushort)acknowledgement.SentTime
            };

            AppendCommand(command);

            if ((acknowledgement.Command.Command & CommandMask) == (byte)CommandType.Disconnect)
                DispatchState(peer, PeerState.Zombie);

            peer.Acknowledgements.Remove(acknowledgement);
        }
    }

    private int CheckTimeouts(PeerImpl peer, ref Event? @event)
    {
        var insertPosition = peer.OutgoingCommands.Begin;
        var insertSendReliablePosition = peer.OutgoingSendReliableCommands.Begin;

        var current = peer.SentReliableCommands.Begin;

        while (current is not null)
        {
            var outgoingCommand = current;
            current = current.Next;

            if (TimeDifference(ServiceTime, outgoingCommand.SentTime) < outgoingCommand.RoundTripTimeout)
                continue;

            if (peer.EarliestTimeout == 0 || TimeLess(outgoingCommand.SentTime, peer.EarliestTimeout))
                peer.EarliestTimeout = outgoingCommand.SentTime;

            if (peer.EarliestTimeout != 0
                && (TimeDifference(ServiceTime, peer.EarliestTimeout) >= peer.TimeoutMaximumValue
                    || ((1u << (int)(unchecked((uint)(outgoingCommand.SendAttempts - 1)) % 32)) >= peer.TimeoutLimitValue
                        && TimeDifference(ServiceTime, peer.EarliestTimeout) >= peer.TimeoutMinimumValue)))
            {
                NotifyDisconnectTimeout(peer, ref @event);
                return 1;
            }

            ++peer.TotalPacketsLost;
            outgoingCommand.RoundTripTimeout = peer.RoundTripTime + 4 * peer.RoundTripTimeVariance;

            if (outgoingCommand.Packet is not null)
            {
                peer.ReliableDataInTransit -= outgoingCommand.FragmentLength;

                peer.SentReliableCommands.Remove(outgoingCommand);
                peer.OutgoingSendReliableCommands.InsertBefore(insertSendReliablePosition, outgoingCommand);
            }
            else
            {
                peer.SentReliableCommands.Remove(outgoingCommand);
                peer.OutgoingCommands.InsertBefore(insertPosition, outgoingCommand);
            }

            if (current == peer.SentReliableCommands.Begin && !peer.SentReliableCommands.IsEmpty)
            {
                var first = peer.SentReliableCommands.Begin!;
                peer.NextTimeout = first.SentTime + first.RoundTripTimeout;
            }
        }

        return 0;
    }

    private int CheckOutgoingCommands(PeerImpl peer)
    {
        OutgoingCommand? outgoingCommand = null;
        Channel? channel = null;
        ushort reliableWindow = 0;
        int commandSize;
        var windowWrap = false;
        var canPing = true;

        var currentCommand = peer.OutgoingCommands.Begin;
        var currentSendReliableCommand = peer.OutgoingSendReliableCommands.Begin;

        for (;;)
        {
            var useSendReliableCommand = false;

            if (currentCommand is not null)
            {
                outgoingCommand = currentCommand;

                if (currentSendReliableCommand is not null
                    && TimeLess(currentSendReliableCommand.QueueTime, outgoingCommand.QueueTime))
                    useSendReliableCommand = true;
                else
                    currentCommand = currentCommand.Next;
            }
            else if (currentSendReliableCommand is not null)
            {
                useSendReliableCommand = true;
            }
            else
            {
                break;
            }

            if (useSendReliableCommand)
            {
                outgoingCommand = currentSendReliableCommand!;
                currentSendReliableCommand = currentSendReliableCommand!.Next;
            }

            var acknowledged = (outgoingCommand!.Command.Command & CommandFlagAcknowledge) != 0;

            if (acknowledged)
            {
                channel = outgoingCommand.Command.ChannelId < peer.ChannelCount
                    ? peer.Channels![outgoingCommand.Command.ChannelId]
                    : null;

                reliableWindow = unchecked((ushort)(outgoingCommand.ReliableSequenceNumber / PeerConstants.ReliableWindowSize));

                if (channel is not null)
                {
                    if (windowWrap)
                    {
                        continue;
                    }

                    if (outgoingCommand.SendAttempts < 1
                        && outgoingCommand.ReliableSequenceNumber % PeerConstants.ReliableWindowSize == 0
                        && (channel.ReliableWindows[(reliableWindow + PeerConstants.ReliableWindows - 1)
                                                    % PeerConstants.ReliableWindows]
                            >= PeerConstants.ReliableWindowSize
                            || (channel.UsedReliableWindows
                                & ((((1u << (PeerConstants.FreeReliableWindows + 2)) - 1)
                                    << reliableWindow)
                                   | (((1u << (PeerConstants.FreeReliableWindows + 2)) - 1)
                                      >> (PeerConstants.ReliableWindows - reliableWindow))))
                            != 0))
                    {
                        windowWrap = true;
                        currentSendReliableCommand = null;
                        continue;
                    }
                }

                if (outgoingCommand.Packet is not null)
                {
                    var windowSize = peer.PacketThrottle * peer.WindowSize / PeerConstants.PacketThrottleScale;

                    if (peer.ReliableDataInTransit + outgoingCommand.FragmentLength
                        > Math.Max(windowSize, peer.Mtu))
                    {
                        currentSendReliableCommand = null;
                        continue;
                    }
                }

                canPing = false;
            }

            commandSize = Protocol.CommandSize(outgoingCommand.Command.Command);

            if (CommandCount >= MaximumPacketCommands
                || BufferCount + 1 >= BufferMaximum
                || (long)peer.Mtu - PacketSize < commandSize
                || (outgoingCommand.Packet is not null
                    && unchecked((ushort)((long)peer.Mtu - PacketSize))
                    < unchecked((ushort)(commandSize + outgoingCommand.FragmentLength))))
            {
                peer.ContinueSending = true;
                break;
            }

            var source = acknowledged && outgoingCommand.Packet is not null
                ? peer.OutgoingSendReliableCommands
                : peer.OutgoingCommands;

            if (acknowledged)
            {
                if (channel is not null && outgoingCommand.SendAttempts < 1)
                {
                    channel.UsedReliableWindows |= unchecked((ushort)(1u << reliableWindow));
                    ++channel.ReliableWindows[reliableWindow];
                }

                ++outgoingCommand.SendAttempts;

                if (outgoingCommand.RoundTripTimeout == 0)
                    outgoingCommand.RoundTripTimeout =
                        peer.RoundTripTime + 4 * peer.RoundTripTimeVariance;

                if (peer.SentReliableCommands.IsEmpty)
                    peer.NextTimeout = ServiceTime + outgoingCommand.RoundTripTimeout;

                source.Remove(outgoingCommand);
                peer.SentReliableCommands.InsertEnd(outgoingCommand);

                outgoingCommand.SentTime = ServiceTime;
                HeaderFlags |= HeaderFlagSentTime;
                peer.ReliableDataInTransit += outgoingCommand.FragmentLength;
            }
            else
            {
                if (outgoingCommand.Packet is not null
                    && outgoingCommand.FragmentOffset == 0
                    && (outgoingCommand.Packet.Flags & PacketFlags.Unthrottled) == 0)
                {
                    peer.PacketThrottleCounter += PeerConstants.PacketThrottleCounter;
                    peer.PacketThrottleCounter %= PeerConstants.PacketThrottleScale;

                    if (peer.PacketThrottleCounter > peer.PacketThrottle)
                    {
                        var reliableSequenceNumber = outgoingCommand.ReliableSequenceNumber;
                        var unreliableSequenceNumber = outgoingCommand.UnreliableSequenceNumber;

                        for (;;)
                        {
                            outgoingCommand.Packet?.ReleaseAndDestroyIfUnreferenced();

                            source.Remove(outgoingCommand);

                            if (currentCommand is null)
                                break;

                            outgoingCommand = currentCommand;

                            if (outgoingCommand.ReliableSequenceNumber != reliableSequenceNumber
                                || outgoingCommand.UnreliableSequenceNumber != unreliableSequenceNumber)
                                break;

                            currentCommand = currentCommand.Next;
                        }

                        continue;
                    }
                }

                source.Remove(outgoingCommand);

                if (outgoingCommand.Packet is not null)
                    _sentUnreliableCommands.InsertEnd(outgoingCommand);
            }

            AppendCommand(outgoingCommand.Command);

            if (outgoingCommand.Packet is { } outgoingPacket)
            {
                outgoingPacket.Buffer.AsSpan((int)outgoingCommand.FragmentOffset, outgoingCommand.FragmentLength)
                    .CopyTo(_sendBuffer.AsSpan(PacketSize));

                PacketSize += outgoingCommand.FragmentLength;
                ++BufferCount;
            }

            ++peer.TotalPacketsSent;
        }

        if (peer.State == PeerState.DisconnectLater
            && !peer.HasOutgoingCommands
            && _sentUnreliableCommands.IsEmpty)
            peer.Disconnect(peer.EventData);

        return canPing ? 1 : 0;
    }

    private void AppendCommand(in ProtocolCommand command)
    {
        PacketSize += Protocol.Write(_sendBuffer.AsSpan(PacketSize), command);
        ++CommandCount;
        ++BufferCount;
    }

    private int SendOutgoingCommands(ref Event? @event, bool checkForTimeouts)
    {
        _sentUnreliableCommands.Clear();

        var continueSending = 0;

        for (var sendPass = 0; sendPass <= continueSending; ++sendPass)
        {
            foreach (var peer in _peers)
            {
                if (peer is null)
                    continue;

                if (peer.State is PeerState.Disconnected or PeerState.Zombie
                    || (sendPass > 0 && !peer.ContinueSending))
                    continue;

                peer.ContinueSending = false;

                HeaderFlags = 0;
                CommandCount = 0;
                BufferCount = 1;
                PacketSize = HeaderSize;

                if (HasChecksum)
                    PacketSize += ChecksumSize;

                if (!peer.Acknowledgements.IsEmpty)
                    SendAcknowledgements(peer);

                if (checkForTimeouts
                    && !peer.SentReliableCommands.IsEmpty
                    && TimeGreaterEqual(ServiceTime, peer.NextTimeout)
                    && CheckTimeouts(peer, ref @event) == 1)
                {
                    if (@event is { Type: not EventType.None })
                        return 1;

                    goto nextPeer;
                }

                if (((peer.OutgoingCommands.IsEmpty && peer.OutgoingSendReliableCommands.IsEmpty)
                     || CheckOutgoingCommands(peer) != 0)
                    && peer.SentReliableCommands.IsEmpty
                    && TimeDifference(ServiceTime, peer.LastReceiveTime) >= peer.PingIntervalValue
                    && (long)peer.Mtu - PacketSize >= Protocol.CommandSizes[(int)CommandType.Ping])
                {
                    peer.Ping();
                    CheckOutgoingCommands(peer);
                }

                if (CommandCount == 0)
                    goto nextPeer;

                var hasSentTime = (HeaderFlags & HeaderFlagSentTime) != 0;

                if (hasSentTime)
                    BinaryPrimitives.WriteUInt16BigEndian(
                        _sendBuffer.AsSpan(2),
                        (ushort)(ServiceTime & 0xFFFF)
                    );

                var headerSize = HeaderSize;

                if (!hasSentTime)
                {
                    Buffer.BlockCopy(_sendBuffer, headerSize, _sendBuffer, headerSize - 2, PacketSize - headerSize);
                    PacketSize -= 2;
                    headerSize -= 2;
                }

                if (HasChecksum && Checksum is { } checksumCallback)
                {
                    BinaryPrimitives.WriteUInt64BigEndian(
                        _sendBuffer.AsSpan(headerSize),
                        peer.OutgoingPeerId < MaximumPeerId ? peer.ConnectId : 0
                    );

                    var checksum = checksumCallback(_sendBuffer.AsSpan(0, PacketSize));
                    BinaryPrimitives.WriteUInt64BigEndian(_sendBuffer.AsSpan(headerSize), checksum);
                }

                var header = (ushort)(peer.OutgoingPeerId | HeaderFlags);

                if (peer.OutgoingPeerId < MaximumPeerId)
                    header |= (ushort)(peer.OutgoingSessionId << HeaderSessionShift);

                BinaryPrimitives.WriteUInt16BigEndian(_sendBuffer, header);

                peer.LastSendTime = ServiceTime;

                var sentLength = SocketSend(peer, _sendBuffer.AsSpan(0, PacketSize));

                RemoveSentUnreliableCommands(peer);

                if (sentLength < 0)
                    return -1;

                TotalSentData += (uint)sentLength;
                peer.TotalDataSent += (uint)sentLength;
                ++TotalSentPackets;

                nextPeer:

                if (peer.ContinueSending)
                    continueSending = sendPass + 1;
            }
        }

        return 0;
    }

    private void BandwidthThrottle()
    {
        var timeCurrent = Library.Time;
        var elapsedTime = TimeDifference(timeCurrent, BandwidthThrottleEpoch);
        var peersRemaining = (uint)ConnectedPeers;
        var dataTotal = ~0u;
        var bandwidth = ~0u;
        uint throttle = 0;
        uint bandwidthLimit = 0;
        var needsAdjustment = BandwidthLimitedPeers > 0;

        if (elapsedTime < 1000)
            return;

        if (OutgoingBandwidth == 0 && IncomingBandwidth == 0)
            return;

        BandwidthThrottleEpoch = timeCurrent;

        if (peersRemaining == 0)
            return;

        if (OutgoingBandwidth != 0)
        {
            dataTotal = 0;
            bandwidth = OutgoingBandwidth * elapsedTime / 1000;

            foreach (var peer in _peers)
            {
                if (peer.State is not (PeerState.Connected or PeerState.DisconnectLater))
                    continue;

                dataTotal += peer.OutgoingDataTotal;
            }
        }

        while (peersRemaining > 0 && needsAdjustment)
        {
            needsAdjustment = false;

            throttle = dataTotal <= bandwidth
                ? PeerConstants.PacketThrottleScale
                : bandwidth * PeerConstants.PacketThrottleScale / dataTotal;

            foreach (var peer in _peers)
            {
                if (peer.State is not (PeerState.Connected or PeerState.DisconnectLater)
                    || peer.IncomingBandwidth == 0
                    || peer.OutgoingBandwidthThrottleEpoch == timeCurrent)
                    continue;

                var peerBandwidth = peer.IncomingBandwidth * elapsedTime / 1000;

                if (throttle * peer.OutgoingDataTotal / PeerConstants.PacketThrottleScale <= peerBandwidth)
                    continue;

                peer.PacketThrottleLimit = peerBandwidth * PeerConstants.PacketThrottleScale / peer.OutgoingDataTotal;

                if (peer.PacketThrottleLimit == 0)
                    peer.PacketThrottleLimit = 1;

                if (peer.PacketThrottle > peer.PacketThrottleLimit)
                    peer.PacketThrottle = peer.PacketThrottleLimit;

                peer.OutgoingBandwidthThrottleEpoch = timeCurrent;
                peer.IncomingDataTotal = 0;
                peer.OutgoingDataTotal = 0;
                needsAdjustment = true;
                --peersRemaining;
                bandwidth -= peerBandwidth;
                dataTotal -= peerBandwidth;
            }
        }

        if (peersRemaining > 0)
        {
            throttle = dataTotal <= bandwidth
                ? PeerConstants.PacketThrottleScale
                : bandwidth * PeerConstants.PacketThrottleScale / dataTotal;

            foreach (var peer in _peers)
            {
                if (peer.State is not (PeerState.Connected or PeerState.DisconnectLater)
                    || peer.OutgoingBandwidthThrottleEpoch == timeCurrent)
                    continue;

                peer.PacketThrottleLimit = throttle;

                if (peer.PacketThrottle > peer.PacketThrottleLimit)
                    peer.PacketThrottle = peer.PacketThrottleLimit;

                peer.IncomingDataTotal = 0;
                peer.OutgoingDataTotal = 0;
            }
        }

        if (!RecalculateBandwidthLimits)
            return;

        RecalculateBandwidthLimits = false;
        peersRemaining = (uint)ConnectedPeers;
        bandwidth = IncomingBandwidth;
        needsAdjustment = true;

        if (bandwidth == 0)
        {
            bandwidthLimit = 0;
        }
        else
        {
            while (peersRemaining > 0 && needsAdjustment)
            {
                needsAdjustment = false;
                bandwidthLimit = bandwidth / peersRemaining;

                foreach (var peer in _peers)
                {
                    if (peer.State is not (PeerState.Connected or PeerState.DisconnectLater)
                        || peer.IncomingBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    if (peer.OutgoingBandwidth > 0 && peer.OutgoingBandwidth >= bandwidthLimit)
                        continue;

                    peer.IncomingBandwidthThrottleEpoch = timeCurrent;
                    needsAdjustment = true;
                    --peersRemaining;
                    bandwidth -= peer.OutgoingBandwidth;
                }
            }
        }

        foreach (var peer in _peers)
        {
            if (peer.State is not (PeerState.Connected or PeerState.DisconnectLater))
                continue;

            var command = new ProtocolCommand
            {
                Type = CommandType.BandwidthLimit,
                Command = (byte)CommandType.BandwidthLimit | CommandFlagAcknowledge,
                ChannelId = 0xFF,
                OutgoingBandwidth = OutgoingBandwidth,
                IncomingBandwidth = peer.IncomingBandwidthThrottleEpoch == timeCurrent
                    ? peer.OutgoingBandwidth
                    : bandwidthLimit
            };

            peer.QueueOutgoingCommand(command, null, 0, 0);
        }
    }
}
