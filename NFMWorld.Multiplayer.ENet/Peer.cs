using System.Net;
using static ENet.PeerConstants;
using static ENet.ProtocolConstants;

namespace ENet;

internal static class PeerConstants
{
    public const uint DefaultRoundTripTime = 1;
    public const uint DefaultPacketThrottle = 32;
    public const uint PacketThrottleThreshold = 40;
    public const uint PacketThrottleScale = 32;
    public const uint PacketThrottleCounter = 7;
    public const uint PacketThrottleAcceleration = 2;
    public const uint PacketThrottleDeceleration = 2;
    public const uint PacketThrottleInterval = 5000;
    public const uint WindowSizeScale = 64 * 1024;
    public const uint TimeoutLimit = 32;
    public const uint TimeoutMinimum = 5000;
    public const uint TimeoutMaximum = 30000;
    public const uint PingInterval = 250;
    public const int UnsequencedWindowSize = 1024;
    public const int FreeUnsequencedWindows = 32;
    public const int ReliableWindows = 16;
    public const int ReliableWindowSize = 0x1000;
    public const int FreeReliableWindows = 8;
}

internal sealed class Channel
{
    public ushort OutgoingReliableSequenceNumber;
    public ushort OutgoingUnreliableSequenceNumber;
    public ushort UsedReliableWindows;
    public readonly ushort[] ReliableWindows = new ushort[PeerConstants.ReliableWindows];
    public ushort IncomingReliableSequenceNumber;
    public ushort IncomingUnreliableSequenceNumber;
    public readonly EnetList<IncomingCommand> IncomingReliableCommands = new();
    public readonly EnetList<IncomingCommand> IncomingUnreliableCommands = new();
}

internal sealed class Acknowledgement : IEnetListLink<Acknowledgement>
{
    public uint SentTime;
    public ProtocolCommand Command;
    public Acknowledgement? Next { get; set; }
    public Acknowledgement? Previous { get; set; }
}

internal sealed class OutgoingCommand : IEnetListLink<OutgoingCommand>
{
    public ushort ReliableSequenceNumber;
    public ushort UnreliableSequenceNumber;
    public uint SentTime;
    public uint RoundTripTimeout;
    public uint QueueTime;
    public uint FragmentOffset;
    public ushort FragmentLength;
    public ushort SendAttempts;
    public ProtocolCommand Command;
    public Packet.PacketData? Packet;
    public OutgoingCommand? Next { get; set; }
    public OutgoingCommand? Previous { get; set; }
}

internal sealed class IncomingCommand : IEnetListLink<IncomingCommand>
{
    public ushort ReliableSequenceNumber;
    public ushort UnreliableSequenceNumber;
    public ProtocolCommand Command;
    public uint FragmentCount;
    public uint FragmentsRemaining;
    public uint[]? Fragments;
    public Packet.PacketData? Packet;
    public IncomingCommand? Next { get; set; }
    public IncomingCommand? Previous { get; set; }
}

/// <summary>Managed <c>ENetPeer</c>. One instance per slot in the host's peer array.</summary>
internal sealed class PeerImpl : IEnetListLink<PeerImpl>
{
    private static readonly IncomingCommand DummyCommand = new();

    public Host Host = null!;
    public int IncomingPeerId;
    public ushort OutgoingPeerId = (ushort)MaximumPeerId;
    public uint ConnectId;
    public byte OutgoingSessionId = 0xFF;
    public byte IncomingSessionId = 0xFF;
    public Address Address;
    public object? Data;
    public PeerState State;

    public Channel[]? Channels;
    public int ChannelCount;

    public uint IncomingBandwidth;
    public uint OutgoingBandwidth;
    public uint IncomingBandwidthThrottleEpoch;
    public uint OutgoingBandwidthThrottleEpoch;
    public uint IncomingDataTotal;
    public uint OutgoingDataTotal;
    public ulong TotalDataReceived;
    public ulong TotalDataSent;
    public uint LastSendTime;
    public uint LastReceiveTime;
    public uint NextTimeout;
    public uint EarliestTimeout;
    public ulong TotalPacketsSent;
    public ulong TotalPacketsLost;

    public uint PacketThrottle = PeerConstants.DefaultPacketThrottle;
    public uint PacketThrottleThreshold = PeerConstants.PacketThrottleThreshold;
    public uint PacketThrottleLimit = PeerConstants.PacketThrottleScale;
    public uint PacketThrottleCounter;
    public uint PacketThrottleEpoch;
    public uint PacketThrottleAcceleration = PeerConstants.PacketThrottleAcceleration;
    public uint PacketThrottleDeceleration = PeerConstants.PacketThrottleDeceleration;
    public uint PacketThrottleInterval = PeerConstants.PacketThrottleInterval;

    public uint PingIntervalValue = PeerConstants.PingInterval;
    public uint TimeoutLimitValue = PeerConstants.TimeoutLimit;
    public uint TimeoutMinimumValue = PeerConstants.TimeoutMinimum;
    public uint TimeoutMaximumValue = PeerConstants.TimeoutMaximum;

    public uint LastRoundTripTime = PeerConstants.DefaultRoundTripTime;
    public uint LowestRoundTripTime = PeerConstants.DefaultRoundTripTime;
    public uint LastRoundTripTimeVariance;
    public uint HighestRoundTripTimeVariance;
    public uint RoundTripTime = PeerConstants.DefaultRoundTripTime;
    public uint RoundTripTimeVariance;

    public uint Mtu = 1280;
    public uint WindowSize = MaximumWindowSize;
    public uint ReliableDataInTransit;
    public ushort OutgoingReliableSequenceNumber;

    public readonly EnetList<Acknowledgement> Acknowledgements = new();
    public readonly EnetList<OutgoingCommand> SentReliableCommands = new();
    public readonly EnetList<OutgoingCommand> OutgoingSendReliableCommands = new();
    public readonly EnetList<OutgoingCommand> OutgoingCommands = new();
    public readonly EnetList<IncomingCommand> DispatchedCommands = new();

    public bool NeedsDispatch;
    public bool ContinueSending;

    public ushort IncomingUnsequencedGroup;
    public ushort OutgoingUnsequencedGroup;
    public readonly uint[] UnsequencedWindow = new uint[UnsequencedWindowSize / 32];

    public uint EventData;
    public long TotalWaitingData;

    public PeerImpl? Next { get; set; }
    public PeerImpl? Previous { get; set; }

    public IPEndPoint EndPoint => Address.ToEndPoint();

    public void ThrottleConfigure(uint interval, uint acceleration, uint deceleration, uint threshold)
    {
        PacketThrottleThreshold = threshold;
        PacketThrottleInterval = interval;
        PacketThrottleAcceleration = acceleration;
        PacketThrottleDeceleration = deceleration;

        var command = new ProtocolCommand
        {
            Type = CommandType.ThrottleConfigure,
            Command = (byte)CommandType.ThrottleConfigure | CommandFlagAcknowledge,
            ChannelId = 0xFF,
            PacketThrottleInterval = interval,
            PacketThrottleAcceleration = acceleration,
            PacketThrottleDeceleration = deceleration
        };

        QueueOutgoingCommand(command, null, 0, 0);
    }

    public int Throttle(uint rtt)
    {
        if (LastRoundTripTime <= LastRoundTripTimeVariance)
        {
            PacketThrottle = PacketThrottleLimit;
        }
        else if (rtt < LastRoundTripTime + (LastRoundTripTimeVariance + 1) / 2)
        {
            PacketThrottle += PacketThrottleAcceleration;

            if (PacketThrottle > PacketThrottleLimit)
                PacketThrottle = PacketThrottleLimit;

            return 1;
        }
        else if (rtt > LastRoundTripTime + PacketThrottleThreshold + 2 * LastRoundTripTimeVariance)
        {
            if (PacketThrottle > PacketThrottleDeceleration)
                PacketThrottle -= PacketThrottleDeceleration;
            else
                PacketThrottle = 0;

            return -1;
        }

        return 0;
    }

    public int Send(byte channelId, ref Packet packet)
    {
        if (State != PeerState.Connected || channelId >= ChannelCount || packet.Length > Host.MaximumPacketSize)
            return -1;

        var channel = Channels![channelId];
        var fragmentLength = (int)Mtu
                             - HeaderSize
                             - Protocol.CommandSizes[(int)CommandType.SendFragment]
                             - Protocol.CommandSizes[(int)CommandType.Acknowledge];

        if (Host.HasChecksum)
            fragmentLength -= ChecksumSize;

        if (packet.Length > fragmentLength)
        {
            var packetLength = (uint)packet.Length;
            var fragmentCount = (packetLength + (uint)fragmentLength - 1) / (uint)fragmentLength;
            byte commandNumber;
            ushort startSequenceNumber;

            if (fragmentCount > MaximumFragmentCount)
                return -1;

            if ((packet.Flags & (PacketFlags.Reliable | PacketFlags.UnreliableFragmented)) == PacketFlags.UnreliableFragmented
                && channel.OutgoingUnreliableSequenceNumber < 0xFFFF)
            {
                commandNumber = (byte)CommandType.SendUnreliableFragment;
                startSequenceNumber = unchecked((ushort)(channel.OutgoingUnreliableSequenceNumber + 1));
            }
            else
            {
                commandNumber = (byte)((byte)CommandType.SendFragment | CommandFlagAcknowledge);
                startSequenceNumber = unchecked((ushort)(channel.OutgoingReliableSequenceNumber + 1));
            }

            var fragments = new List<OutgoingCommand>();

            for (uint fragmentNumber = 0, fragmentOffset = 0;
                 fragmentOffset < packetLength;
                 ++fragmentNumber, fragmentOffset += (uint)fragmentLength)
            {
                if (packetLength - fragmentOffset < (uint)fragmentLength)
                    fragmentLength = (int)(packetLength - fragmentOffset);

                fragments.Add(
                    new OutgoingCommand
                    {
                        FragmentOffset = fragmentOffset,
                        FragmentLength = (ushort)fragmentLength,
                        Packet = packet.Content,
                        Command = new ProtocolCommand
                        {
                            Type = (CommandType)(commandNumber & CommandMask),
                            Command = commandNumber,
                            ChannelId = channelId,
                            StartSequenceNumber = startSequenceNumber,
                            DataLength = (ushort)fragmentLength,
                            FragmentCount = fragmentCount,
                            FragmentNumber = fragmentNumber,
                            TotalLength = packetLength,
                            FragmentOffset = fragmentOffset
                        }
                    }
                );
            }

            if (packet.Content is { } content)
                content.ReferenceCount += fragments.Count;

            foreach (var fragment in fragments)
                SetupOutgoingCommand(fragment);

            return 0;
        }

        var command = default(ProtocolCommand);
        command.ChannelId = channelId;

        if ((packet.Flags & (PacketFlags.Reliable | PacketFlags.Unsequenced)) == PacketFlags.Unsequenced)
        {
            command.Type = CommandType.SendUnsequenced;
            command.Command = (byte)CommandType.SendUnsequenced | CommandFlagUnsequenced;
            command.DataLength = (ushort)packet.Length;
        }
        else if ((packet.Flags & PacketFlags.Reliable) != 0 || channel.OutgoingUnreliableSequenceNumber >= 0xFFFF)
        {
            command.Type = CommandType.SendReliable;
            command.Command = (byte)CommandType.SendReliable | CommandFlagAcknowledge;
            command.DataLength = (ushort)packet.Length;
        }
        else
        {
            command.Type = CommandType.SendUnreliable;
            command.Command = (byte)CommandType.SendUnreliable;
            command.DataLength = (ushort)packet.Length;
        }

        if (QueueOutgoingCommand(command, packet.Content, 0, (ushort)packet.Length) is null)
            return -1;

        if ((packet.Flags & PacketFlags.Instant) != 0)
            Host.Flush();

        return 0;
    }

    public Packet Receive(out byte channelId)
    {
        channelId = 0;

        if (DispatchedCommands.IsEmpty)
            return default;

        var incomingCommand = DispatchedCommands.RemoveFirst()!;
        channelId = incomingCommand.Command.ChannelId;

        var packet = incomingCommand.Packet!;
        packet.ReleaseReference();

        TotalWaitingData -= packet.Length;

        return new Packet(packet);
    }

    public void ResetQueues()
    {
        if (NeedsDispatch)
        {
            Host.DispatchQueue.Remove(this);
            NeedsDispatch = false;
        }

        Acknowledgements.Clear();
        ResetOutgoingCommands(SentReliableCommands);
        ResetOutgoingCommands(OutgoingCommands);
        ResetOutgoingCommands(OutgoingSendReliableCommands);
        ResetIncomingCommands(DispatchedCommands);

        if (Channels is not null && ChannelCount > 0)
        {
            for (var i = 0; i < ChannelCount; ++i)
            {
                ResetIncomingCommands(Channels[i].IncomingReliableCommands);
                ResetIncomingCommands(Channels[i].IncomingUnreliableCommands);
            }
        }

        Channels = null;
        ChannelCount = 0;
    }

    public bool HasOutgoingCommands =>
        !OutgoingCommands.IsEmpty || !OutgoingSendReliableCommands.IsEmpty || !SentReliableCommands.IsEmpty;

    public void OnConnect()
    {
        if (State != PeerState.Connected && State != PeerState.DisconnectLater)
        {
            if (IncomingBandwidth != 0)
                ++Host.BandwidthLimitedPeers;

            ++Host.ConnectedPeers;
        }
    }

    public void OnDisconnect()
    {
        if (State == PeerState.Connected || State == PeerState.DisconnectLater)
        {
            if (IncomingBandwidth != 0)
                --Host.BandwidthLimitedPeers;

            --Host.ConnectedPeers;
        }
    }

    public void Reset()
    {
        OnDisconnect();

        OutgoingPeerId = MaximumPeerId;
        State = PeerState.Disconnected;

        IncomingBandwidth = 0;
        OutgoingBandwidth = 0;
        IncomingBandwidthThrottleEpoch = 0;
        OutgoingBandwidthThrottleEpoch = 0;
        IncomingDataTotal = 0;
        TotalDataReceived = 0;
        OutgoingDataTotal = 0;
        TotalDataSent = 0;
        LastSendTime = 0;
        LastReceiveTime = 0;
        NextTimeout = 0;
        EarliestTimeout = 0;
        TotalPacketsSent = 0;
        TotalPacketsLost = 0;

        PacketThrottle = PeerConstants.DefaultPacketThrottle;
        PacketThrottleThreshold = PeerConstants.PacketThrottleThreshold;
        PacketThrottleLimit = PeerConstants.PacketThrottleScale;
        PacketThrottleCounter = 0;
        PacketThrottleEpoch = 0;
        PacketThrottleAcceleration = PeerConstants.PacketThrottleAcceleration;
        PacketThrottleDeceleration = PeerConstants.PacketThrottleDeceleration;
        PacketThrottleInterval = PeerConstants.PacketThrottleInterval;

        PingIntervalValue = PeerConstants.PingInterval;
        TimeoutLimitValue = PeerConstants.TimeoutLimit;
        TimeoutMinimumValue = PeerConstants.TimeoutMinimum;
        TimeoutMaximumValue = PeerConstants.TimeoutMaximum;

        LastRoundTripTime = PeerConstants.DefaultRoundTripTime;
        LowestRoundTripTime = PeerConstants.DefaultRoundTripTime;
        LastRoundTripTimeVariance = 0;
        HighestRoundTripTimeVariance = 0;
        RoundTripTime = 1;
        RoundTripTimeVariance = 0;
        Mtu = Host.Mtu;
        ReliableDataInTransit = 0;
        OutgoingReliableSequenceNumber = 0;
        WindowSize = MaximumWindowSize;
        IncomingUnsequencedGroup = 0;
        OutgoingUnsequencedGroup = 0;
        EventData = 0;
        TotalWaitingData = 0;

        Array.Clear(UnsequencedWindow);

        ResetQueues();
    }

    public void Ping()
    {
        if (State != PeerState.Connected)
            return;

        var command = new ProtocolCommand
        {
            Type = CommandType.Ping,
            Command = (byte)CommandType.Ping | CommandFlagAcknowledge,
            ChannelId = 0xFF
        };

        QueueOutgoingCommand(command, null, 0, 0);
    }

    public void PingInterval(uint interval) =>
        PingIntervalValue = interval != 0 ? interval : PeerConstants.PingInterval;

    public void Timeout(uint limit, uint minimum, uint maximum)
    {
        TimeoutLimitValue = limit != 0 ? limit : PeerConstants.TimeoutLimit;
        TimeoutMinimumValue = minimum != 0 ? minimum : PeerConstants.TimeoutMinimum;
        TimeoutMaximumValue = maximum != 0 ? maximum : PeerConstants.TimeoutMaximum;
    }

    public void DisconnectNow(uint data)
    {
        if (State == PeerState.Disconnected)
            return;

        if (State != PeerState.Zombie && State != PeerState.Disconnecting)
        {
            ResetQueues();

            var command = new ProtocolCommand
            {
                Type = CommandType.Disconnect,
                Command = (byte)CommandType.Disconnect | CommandFlagUnsequenced,
                ChannelId = 0xFF,
                Data = data
            };

            QueueOutgoingCommand(command, null, 0, 0);
            Host.Flush();
        }

        Reset();
    }

    public void Disconnect(uint data)
    {
        if (State is PeerState.Disconnecting or PeerState.Disconnected or PeerState.AcknowledgingDisconnect or PeerState.Zombie)
            return;

        ResetQueues();

        var command = new ProtocolCommand
        {
            Type = CommandType.Disconnect,
            ChannelId = 0xFF,
            Data = data
        };

        command.Command = State is PeerState.Connected or PeerState.DisconnectLater
            ? (byte)((byte)CommandType.Disconnect | CommandFlagAcknowledge)
            : (byte)((byte)CommandType.Disconnect | CommandFlagUnsequenced);

        QueueOutgoingCommand(command, null, 0, 0);

        if (State is PeerState.Connected or PeerState.DisconnectLater)
        {
            OnDisconnect();
            State = PeerState.Disconnecting;
        }
        else
        {
            Host.Flush();
            Reset();
        }
    }

    public void DisconnectLater(uint data)
    {
        if (State is PeerState.Connected or PeerState.DisconnectLater && HasOutgoingCommands)
        {
            State = PeerState.DisconnectLater;
            EventData = data;
        }
        else
        {
            Disconnect(data);
        }
    }

    public Acknowledgement? QueueAcknowledgement(in ProtocolCommand command, ushort sentTime)
    {
        if (command.ChannelId < ChannelCount)
        {
            var channel = Channels![command.ChannelId];
            int reliableWindow = command.ReliableSequenceNumber / PeerConstants.ReliableWindowSize;
            int currentWindow = channel.IncomingReliableSequenceNumber / PeerConstants.ReliableWindowSize;

            if (command.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                reliableWindow += PeerConstants.ReliableWindows;

            if (reliableWindow >= currentWindow + PeerConstants.FreeReliableWindows - 1
                && reliableWindow <= currentWindow + PeerConstants.FreeReliableWindows)
                return null;
        }

        OutgoingDataTotal += (uint)Protocol.CommandSizes[(int)CommandType.Acknowledge];

        var acknowledgement = new Acknowledgement
        {
            SentTime = sentTime,
            Command = command
        };

        Acknowledgements.InsertEnd(acknowledgement);

        return acknowledgement;
    }

    public void SetupOutgoingCommand(OutgoingCommand outgoingCommand)
    {
        Channel? channel = null;

        if (outgoingCommand.Command.ChannelId < ChannelCount)
            channel = Channels![outgoingCommand.Command.ChannelId];

        OutgoingDataTotal += (uint)Protocol.CommandSize(outgoingCommand.Command.Command) + outgoingCommand.FragmentLength;

        if (outgoingCommand.Command.ChannelId == 0xFF)
        {
            ++OutgoingReliableSequenceNumber;
            outgoingCommand.ReliableSequenceNumber = OutgoingReliableSequenceNumber;
            outgoingCommand.UnreliableSequenceNumber = 0;
        }
        else if ((outgoingCommand.Command.Command & CommandFlagAcknowledge) != 0)
        {
            channel!.OutgoingReliableSequenceNumber = unchecked((ushort)(channel.OutgoingReliableSequenceNumber + 1));
            channel.OutgoingUnreliableSequenceNumber = 0;
            outgoingCommand.ReliableSequenceNumber = channel.OutgoingReliableSequenceNumber;
            outgoingCommand.UnreliableSequenceNumber = 0;
        }
        else if ((outgoingCommand.Command.Command & CommandFlagUnsequenced) != 0)
        {
            ++OutgoingUnsequencedGroup;
            outgoingCommand.ReliableSequenceNumber = 0;
            outgoingCommand.UnreliableSequenceNumber = 0;
        }
        else
        {
            if (outgoingCommand.FragmentOffset == 0)
                channel!.OutgoingUnreliableSequenceNumber =
                    unchecked((ushort)(channel.OutgoingUnreliableSequenceNumber + 1));

            outgoingCommand.ReliableSequenceNumber = channel!.OutgoingReliableSequenceNumber;
            outgoingCommand.UnreliableSequenceNumber = channel.OutgoingUnreliableSequenceNumber;
        }

        outgoingCommand.SendAttempts = 0;
        outgoingCommand.SentTime = 0;
        outgoingCommand.RoundTripTimeout = 0;

        var command = outgoingCommand.Command;
        command.ReliableSequenceNumber = outgoingCommand.ReliableSequenceNumber;

        switch (command.Type)
        {
            case CommandType.SendUnreliable:
                command.UnreliableSequenceNumber = outgoingCommand.UnreliableSequenceNumber;
                break;

            case CommandType.SendUnsequenced:
                command.UnsequencedGroup = OutgoingUnsequencedGroup;
                break;
        }

        outgoingCommand.Command = command;
        outgoingCommand.QueueTime = ++Host.TotalQueued;

        if ((command.Command & CommandFlagAcknowledge) != 0 && outgoingCommand.Packet is not null)
            OutgoingSendReliableCommands.InsertEnd(outgoingCommand);
        else
            OutgoingCommands.InsertEnd(outgoingCommand);
    }

    public OutgoingCommand? QueueOutgoingCommand(
        in ProtocolCommand command,
        Packet.PacketData? packet,
        uint offset,
        ushort length
    )
    {
        var outgoingCommand = new OutgoingCommand
        {
            Command = command,
            FragmentOffset = offset,
            FragmentLength = length,
            Packet = packet
        };

        packet?.AddReference();
        SetupOutgoingCommand(outgoingCommand);

        return outgoingCommand;
    }

    public void RemoveIncomingCommands(
        EnetList<IncomingCommand> queue,
        IncomingCommand? start,
        IncomingCommand? end,
        IncomingCommand? exclude
    )
    {
        var current = start;

        while (current is not null && !ReferenceEquals(current, end))
        {
            var incomingCommand = current;
            current = current.Next;

            if (ReferenceEquals(incomingCommand, exclude))
                continue;

            queue.Remove(incomingCommand);

            if (incomingCommand.Packet is { } packet)
                packet.ReleaseAndDestroyIfUnreferenced();
        }
    }

    public void ResetIncomingCommands(EnetList<IncomingCommand> queue)
    {
        RemoveIncomingCommands(queue, queue.Begin, null, null);
    }

    public void ResetOutgoingCommands(EnetList<OutgoingCommand> queue)
    {
        while (queue.RemoveFirst() is { } outgoingCommand)
            outgoingCommand.Packet?.ReleaseAndDestroyIfUnreferenced();
    }

    public void DispatchIncomingUnreliableCommands(Channel channel, IncomingCommand? queuedCommand)
    {
        var queue = channel.IncomingUnreliableCommands;
        var current = queue.Begin;
        var start = current;
        var dropped = current;

        while (current is not null)
        {
            var incomingCommand = current;
            var advanceStart = true;

            if ((incomingCommand.Command.Command & CommandMask) == (byte)CommandType.SendUnsequenced)
            {
                // Unsequenced commands are always delivered in arrival order.
                advanceStart = false;
            }
            else if (incomingCommand.ReliableSequenceNumber == channel.IncomingReliableSequenceNumber)
            {
                if (incomingCommand.FragmentsRemaining <= 0)
                {
                    channel.IncomingUnreliableSequenceNumber = incomingCommand.UnreliableSequenceNumber;
                    advanceStart = false;
                }
                else if (!ReferenceEquals(start, current))
                {
                    queue.MoveToEnd(DispatchedCommands, start!, current.Previous!);
                    MarkNeedsDispatch();
                    dropped = current;
                }
                else if (!ReferenceEquals(dropped, current))
                {
                    dropped = current.Previous;
                }
            }
            else
            {
                int reliableWindow = incomingCommand.ReliableSequenceNumber / PeerConstants.ReliableWindowSize;
                int currentWindow = channel.IncomingReliableSequenceNumber / PeerConstants.ReliableWindowSize;

                if (incomingCommand.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                    reliableWindow += PeerConstants.ReliableWindows;

                if (reliableWindow >= currentWindow && reliableWindow < currentWindow + PeerConstants.FreeReliableWindows - 1)
                    break;

                dropped = current.Next;

                if (!ReferenceEquals(start, current))
                {
                    queue.MoveToEnd(DispatchedCommands, start!, current.Previous!);
                    MarkNeedsDispatch();
                }
            }

            if (advanceStart)
                start = current.Next;

            current = current.Next;
        }

        if (!ReferenceEquals(start, current))
        {
            queue.MoveToEnd(DispatchedCommands, start!, current?.Previous ?? queue.Tail!);
            MarkNeedsDispatch();
            dropped = current;
        }

        RemoveIncomingCommands(queue, queue.Begin, dropped, queuedCommand);
    }

    public void DispatchIncomingReliableCommands(Channel channel, IncomingCommand? queuedCommand)
    {
        var queue = channel.IncomingReliableCommands;
        var current = queue.Begin;

        while (current is not null)
        {
            var incomingCommand = current;

            if (incomingCommand.FragmentsRemaining > 0
                || incomingCommand.ReliableSequenceNumber != unchecked((ushort)(channel.IncomingReliableSequenceNumber + 1)))
                break;

            channel.IncomingReliableSequenceNumber = incomingCommand.ReliableSequenceNumber;

            if (incomingCommand.FragmentCount > 0)
                channel.IncomingReliableSequenceNumber =
                    unchecked((ushort)(channel.IncomingReliableSequenceNumber + incomingCommand.FragmentCount - 1));

            current = current.Next;
        }

        if (current == queue.Begin)
            return;

        channel.IncomingUnreliableSequenceNumber = 0;

        queue.MoveToEnd(DispatchedCommands, queue.Begin!, current?.Previous ?? queue.Tail!);
        MarkNeedsDispatch();

        if (!channel.IncomingUnreliableCommands.IsEmpty)
            DispatchIncomingUnreliableCommands(channel, queuedCommand);
    }

    private void MarkNeedsDispatch()
    {
        if (NeedsDispatch)
            return;

        Host.DispatchQueue.InsertEnd(this);
        NeedsDispatch = true;
    }

    public IncomingCommand? QueueIncomingCommand(
        in ProtocolCommand command,
        ReadOnlySpan<byte> payload,
        int dataLength,
        PacketFlags flags,
        uint fragmentCount
    )
    {
        var channel = Channels![command.ChannelId];
        uint unreliableSequenceNumber = 0;
        uint reliableSequenceNumber = 0;

        if (State == PeerState.DisconnectLater)
            return Discard(fragmentCount);

        if (command.Type != CommandType.SendUnsequenced)
        {
            reliableSequenceNumber = command.ReliableSequenceNumber;
            int reliableWindow = (int)(reliableSequenceNumber / PeerConstants.ReliableWindowSize);
            int currentWindow = channel.IncomingReliableSequenceNumber / PeerConstants.ReliableWindowSize;

            if (reliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                reliableWindow += PeerConstants.ReliableWindows;

            if (reliableWindow < currentWindow || reliableWindow >= currentWindow + PeerConstants.FreeReliableWindows - 1)
                return Discard(fragmentCount);
        }

        IncomingCommand? currentCommand;

        switch (command.Type)
        {
            case CommandType.SendFragment:
            case CommandType.SendReliable:
            {
                if (reliableSequenceNumber == channel.IncomingReliableSequenceNumber)
                    return Discard(fragmentCount);

                var node = channel.IncomingReliableCommands.Tail;

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

                    if (incomingCommand.ReliableSequenceNumber <= reliableSequenceNumber)
                    {
                        if (incomingCommand.ReliableSequenceNumber < reliableSequenceNumber)
                            break;

                        return Discard(fragmentCount);
                    }

                    node = incomingCommand.Previous;
                }

                currentCommand = node;
                break;
            }

            case CommandType.SendUnreliable:
            case CommandType.SendUnreliableFragment:
            {
                unreliableSequenceNumber = command.Type == CommandType.SendUnreliable
                    ? command.UnreliableSequenceNumber
                    : command.StartSequenceNumber;

                if (reliableSequenceNumber == channel.IncomingReliableSequenceNumber
                    && unreliableSequenceNumber <= channel.IncomingUnreliableSequenceNumber)
                    return Discard(fragmentCount);

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

                    if (incomingCommand.UnreliableSequenceNumber <= unreliableSequenceNumber)
                    {
                        if (incomingCommand.UnreliableSequenceNumber < unreliableSequenceNumber)
                            break;

                        return Discard(fragmentCount);
                    }

                    node = incomingCommand.Previous;
                }

                currentCommand = node;
                break;
            }

            case CommandType.SendUnsequenced:
                currentCommand = null;
                break;

            default:
                return Discard(fragmentCount);
        }

        if (TotalWaitingData >= Host.MaximumWaitingData)
            return null;
        var packet = new Packet.PacketData
        {
            Buffer = new byte[dataLength],
            Length = dataLength,
            Flags = flags
        };

        payload.CopyTo(packet.Buffer);

        var incoming = new IncomingCommand
        {
            ReliableSequenceNumber = command.ReliableSequenceNumber,
            UnreliableSequenceNumber = unchecked((ushort)(unreliableSequenceNumber & 0xFFFF)),
            Command = command,
            FragmentCount = fragmentCount,
            FragmentsRemaining = fragmentCount,
            Packet = packet
        };

        if (fragmentCount > 0)
        {
            if (fragmentCount <= MaximumFragmentCount)
                incoming.Fragments = new uint[(fragmentCount + 31) / 32];

            if (incoming.Fragments is null)
                return null;
        }

        packet.AddReference();
        TotalWaitingData += packet.Length;

        var queue = command.Type is CommandType.SendFragment or CommandType.SendReliable
            ? channel.IncomingReliableCommands
            : channel.IncomingUnreliableCommands;

        // A null anchor is the list end, whose "next" is the head; the tail's next is the end.
        var insertBefore = currentCommand is null ? queue.Begin : currentCommand.Next;
        queue.InsertBefore(insertBefore, incoming);

        if (command.Type is CommandType.SendFragment or CommandType.SendReliable)
            DispatchIncomingReliableCommands(channel, incoming);
        else
            DispatchIncomingUnreliableCommands(channel, incoming);

        return incoming;
    }

    private static IncomingCommand? Discard(uint fragmentCount) =>
        fragmentCount > 0 ? null : DummyCommand;
}

/// <summary>Managed <c>ENetPeer*</c> handle. The C API returns a pointer; this wraps the slot object.</summary>
public struct Peer
{
    internal PeerImpl? Impl;

    internal Peer(PeerImpl? impl)
    {
        Impl = impl;
    }

    public readonly bool IsSet => Impl is not null;

    public readonly uint ID => Impl is null ? 0 : (uint)Impl.IncomingPeerId;

    public readonly string IP => Impl?.Address.GetIP() ?? string.Empty;

    public readonly ushort Port => Impl?.Address.Port ?? 0;

    public readonly uint MTU => Impl?.Mtu ?? 0;

    public readonly PeerState State => Impl is null ? PeerState.Uninitialized : Impl.State;

    public readonly uint RoundTripTime => Impl?.RoundTripTime ?? 0;

    public readonly uint LastRoundTripTime => Impl?.LastRoundTripTime ?? 0;

    public readonly uint LastSendTime => Impl?.LastSendTime ?? 0;

    public readonly uint LastReceiveTime => Impl?.LastReceiveTime ?? 0;

    public readonly ulong PacketsSent => Impl?.TotalPacketsSent ?? 0;

    public readonly ulong PacketsLost => Impl?.TotalPacketsLost ?? 0;

    public readonly float PacketsThrottle =>
        Impl is null ? 0 : Impl.PacketThrottle / (float)PeerConstants.PacketThrottleScale * 100.0f;

    public readonly ulong BytesSent => Impl?.TotalDataSent ?? 0;

    public readonly ulong BytesReceived => Impl?.TotalDataReceived ?? 0;

    public object? Data
    {
        readonly get => Impl?.Data;
        set
        {
            if (Impl is not null)
                Impl.Data = value;
        }
    }

    public readonly void ConfigureThrottle(uint interval, uint acceleration, uint deceleration, uint threshold) =>
        Impl?.ThrottleConfigure(interval, acceleration, deceleration, threshold);

    public bool Send(byte channelId, ref Packet packet)
    {
        if (Impl is null)
            return false;

        return Impl.Send(channelId, ref packet) == 0;
    }

    public bool Receive(out byte channelId, out Packet packet)
    {
        if (Impl is null)
        {
            channelId = 0;
            packet = default;
            return false;
        }

        packet = Impl.Receive(out channelId);
        return packet.IsSet;
    }

    public readonly void Ping() => Impl?.Ping();

    public readonly void PingInterval(uint interval) => Impl?.PingInterval(interval);

    public readonly void Timeout(uint timeoutLimit, uint timeoutMinimum, uint timeoutMaximum) =>
        Impl?.Timeout(timeoutLimit, timeoutMinimum, timeoutMaximum);

    public readonly void Disconnect(uint data) => Impl?.Disconnect(data);

    public readonly void DisconnectNow(uint data) => Impl?.DisconnectNow(data);

    public readonly void DisconnectLater(uint data) => Impl?.DisconnectLater(data);

    public readonly void Reset() => Impl?.Reset();
}
