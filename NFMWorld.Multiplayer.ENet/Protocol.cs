using System.Buffers.Binary;

namespace ENet;

internal static class ProtocolConstants
{
    public const int MinimumMtu = 576;
    public const int MaximumMtu = 4096;
    public const int MaximumPacketCommands = 32;
    public const int MinimumWindowSize = 4096;
    public const int MaximumWindowSize = 65536;
    public const int MinimumChannelCount = 1;
    public const int MaximumChannelCount = 255;
    public const int MaximumPeerId = 0xFFF;
    public const int MaximumFragmentCount = 1024 * 1024;

    public const int HeaderSize = 4;
    public const int ChecksumSize = 8;
    public const int BufferMaximum = 1 + 2 * MaximumPacketCommands;

    // The fork dropped compression and moved SENT_TIME down to bit 14.
    public const ushort HeaderFlagSentTime = 1 << 14;
    public const ushort HeaderFlagMask = HeaderFlagSentTime;
    public const ushort HeaderSessionMask = 3 << 12;
    public const int HeaderSessionShift = 12;

    public const byte CommandFlagAcknowledge = 1 << 7;
    public const byte CommandFlagUnsequenced = 1 << 6;
    public const byte CommandMask = 0x0F;
}

internal enum CommandType : byte
{
    None = 0,
    Acknowledge = 1,
    Connect = 2,
    VerifyConnect = 3,
    Disconnect = 4,
    Ping = 5,
    SendReliable = 6,
    SendUnreliable = 7,
    SendFragment = 8,
    SendUnsequenced = 9,
    BandwidthLimit = 10,
    ThrottleConfigure = 11,
    SendUnreliableFragment = 12,
    Count = 13
}

/// <summary>Host-order representation of a packed <c>ENetProtocol</c> union.</summary>
internal struct ProtocolCommand
{
    public CommandType Type;
    public byte Command;
    public byte ChannelId;
    public ushort ReliableSequenceNumber;

    public ushort ReceivedReliableSequenceNumber;
    public ushort ReceivedSentTime;

    public ushort OutgoingPeerId;
    public byte IncomingSessionId;
    public byte OutgoingSessionId;
    public uint Mtu;
    public uint WindowSize;
    public uint ChannelCount;
    public uint IncomingBandwidth;
    public uint OutgoingBandwidth;
    public uint PacketThrottleInterval;
    public uint PacketThrottleAcceleration;
    public uint PacketThrottleDeceleration;
    public uint ConnectId;
    public uint Data;

    public ushort DataLength;
    public ushort UnreliableSequenceNumber;
    public ushort UnsequencedGroup;
    public ushort StartSequenceNumber;
    public uint FragmentCount;
    public uint FragmentNumber;
    public uint TotalLength;
    public uint FragmentOffset;

    public readonly bool Acknowledged => (Command & ProtocolConstants.CommandFlagAcknowledge) != 0;
    public readonly bool Unsequenced => (Command & ProtocolConstants.CommandFlagUnsequenced) != 0;
}

internal static class Protocol
{
    public static readonly int[] CommandSizes =
    [
        0, // none
        8, // acknowledge
        48, // connect
        44, // verify connect
        8, // disconnect
        4, // ping
        6, // send reliable
        8, // send unreliable
        24, // send fragment
        8, // send unsequenced
        12, // bandwidth limit
        16, // throttle configure
        24, // send unreliable fragment
    ];

    public static int CommandSize(byte command)
    {
        return CommandSizes[command & ProtocolConstants.CommandMask];
    }

    public static bool TryRead(ReadOnlySpan<byte> data, int offset, out ProtocolCommand command, out int commandSize)
    {
        command = default;
        commandSize = 0;

        if (data.Length - offset < 4)
            return false;

        var type = (CommandType)(data[offset] & ProtocolConstants.CommandMask);

        if (type >= CommandType.Count)
            return false;

        var size = CommandSizes[(int)type];

        if (size == 0 || data.Length - offset < size)
            return false;

        commandSize = size;
        command.Type = type;
        command.Command = data[offset];
        command.ChannelId = data[offset + 1];
        command.ReliableSequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 2)..]);

        switch (type)
        {
            case CommandType.Acknowledge:
                command.ReceivedReliableSequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 4)..]);
                command.ReceivedSentTime = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 6)..]);
                break;

            case CommandType.Connect:
                command.OutgoingPeerId = ReadU16(data, offset + 4);
                command.IncomingSessionId = data[offset + 6];
                command.OutgoingSessionId = data[offset + 7];
                command.Mtu = ReadU32(data, offset + 8);
                command.WindowSize = ReadU32(data, offset + 12);
                command.ChannelCount = ReadU32(data, offset + 16);
                command.IncomingBandwidth = ReadU32(data, offset + 20);
                command.OutgoingBandwidth = ReadU32(data, offset + 24);
                command.PacketThrottleInterval = ReadU32(data, offset + 28);
                command.PacketThrottleAcceleration = ReadU32(data, offset + 32);
                command.PacketThrottleDeceleration = ReadU32(data, offset + 36);
                command.ConnectId = ReadU32(data, offset + 40);
                command.Data = ReadU32(data, offset + 44);
                break;

            case CommandType.VerifyConnect:
                command.OutgoingPeerId = ReadU16(data, offset + 4);
                command.IncomingSessionId = data[offset + 6];
                command.OutgoingSessionId = data[offset + 7];
                command.Mtu = ReadU32(data, offset + 8);
                command.WindowSize = ReadU32(data, offset + 12);
                command.ChannelCount = ReadU32(data, offset + 16);
                command.IncomingBandwidth = ReadU32(data, offset + 20);
                command.OutgoingBandwidth = ReadU32(data, offset + 24);
                command.PacketThrottleInterval = ReadU32(data, offset + 28);
                command.PacketThrottleAcceleration = ReadU32(data, offset + 32);
                command.PacketThrottleDeceleration = ReadU32(data, offset + 36);
                command.ConnectId = ReadU32(data, offset + 40);
                break;

            case CommandType.Disconnect:
                command.Data = ReadU32(data, offset + 4);
                break;

            case CommandType.SendReliable:
                command.DataLength = ReadU16(data, offset + 4);
                break;

            case CommandType.SendUnreliable:
                command.UnreliableSequenceNumber = ReadU16(data, offset + 4);
                command.DataLength = ReadU16(data, offset + 6);
                break;

            case CommandType.SendUnsequenced:
                command.UnsequencedGroup = ReadU16(data, offset + 4);
                command.DataLength = ReadU16(data, offset + 6);
                break;

            case CommandType.SendFragment:
            case CommandType.SendUnreliableFragment:
                command.StartSequenceNumber = ReadU16(data, offset + 4);
                command.DataLength = ReadU16(data, offset + 6);
                command.FragmentCount = ReadU32(data, offset + 8);
                command.FragmentNumber = ReadU32(data, offset + 12);
                command.TotalLength = ReadU32(data, offset + 16);
                command.FragmentOffset = ReadU32(data, offset + 20);
                break;

            case CommandType.BandwidthLimit:
                command.IncomingBandwidth = ReadU32(data, offset + 4);
                command.OutgoingBandwidth = ReadU32(data, offset + 8);
                break;

            case CommandType.ThrottleConfigure:
                command.PacketThrottleInterval = ReadU32(data, offset + 4);
                command.PacketThrottleAcceleration = ReadU32(data, offset + 8);
                command.PacketThrottleDeceleration = ReadU32(data, offset + 12);
                break;
        }

        return true;
    }

    public static int Write(Span<byte> destination, in ProtocolCommand command)
    {
        var size = CommandSizes[(int)command.Type];

        destination[0] = command.Command;
        destination[1] = command.ChannelId;
        BinaryPrimitives.WriteUInt16BigEndian(destination[2..], command.ReliableSequenceNumber);

        switch (command.Type)
        {
            case CommandType.Acknowledge:
                WriteU16(destination, 4, command.ReceivedReliableSequenceNumber);
                WriteU16(destination, 6, command.ReceivedSentTime);
                break;

            case CommandType.Connect:
                WriteU16(destination, 4, command.OutgoingPeerId);
                destination[6] = command.IncomingSessionId;
                destination[7] = command.OutgoingSessionId;
                WriteU32(destination, 8, command.Mtu);
                WriteU32(destination, 12, command.WindowSize);
                WriteU32(destination, 16, command.ChannelCount);
                WriteU32(destination, 20, command.IncomingBandwidth);
                WriteU32(destination, 24, command.OutgoingBandwidth);
                WriteU32(destination, 28, command.PacketThrottleInterval);
                WriteU32(destination, 32, command.PacketThrottleAcceleration);
                WriteU32(destination, 36, command.PacketThrottleDeceleration);
                WriteU32(destination, 40, command.ConnectId);
                WriteU32(destination, 44, command.Data);
                break;

            case CommandType.VerifyConnect:
                WriteU16(destination, 4, command.OutgoingPeerId);
                destination[6] = command.IncomingSessionId;
                destination[7] = command.OutgoingSessionId;
                WriteU32(destination, 8, command.Mtu);
                WriteU32(destination, 12, command.WindowSize);
                WriteU32(destination, 16, command.ChannelCount);
                WriteU32(destination, 20, command.IncomingBandwidth);
                WriteU32(destination, 24, command.OutgoingBandwidth);
                WriteU32(destination, 28, command.PacketThrottleInterval);
                WriteU32(destination, 32, command.PacketThrottleAcceleration);
                WriteU32(destination, 36, command.PacketThrottleDeceleration);
                WriteU32(destination, 40, command.ConnectId);
                break;

            case CommandType.Disconnect:
                WriteU32(destination, 4, command.Data);
                break;

            case CommandType.SendReliable:
                WriteU16(destination, 4, command.DataLength);
                break;

            case CommandType.SendUnreliable:
                WriteU16(destination, 4, command.UnreliableSequenceNumber);
                WriteU16(destination, 6, command.DataLength);
                break;

            case CommandType.SendUnsequenced:
                WriteU16(destination, 4, command.UnsequencedGroup);
                WriteU16(destination, 6, command.DataLength);
                break;

            case CommandType.SendFragment:
            case CommandType.SendUnreliableFragment:
                WriteU16(destination, 4, command.StartSequenceNumber);
                WriteU16(destination, 6, command.DataLength);
                WriteU32(destination, 8, command.FragmentCount);
                WriteU32(destination, 12, command.FragmentNumber);
                WriteU32(destination, 16, command.TotalLength);
                WriteU32(destination, 20, command.FragmentOffset);
                break;

            case CommandType.BandwidthLimit:
                WriteU32(destination, 4, command.IncomingBandwidth);
                WriteU32(destination, 8, command.OutgoingBandwidth);
                break;

            case CommandType.ThrottleConfigure:
                WriteU32(destination, 4, command.PacketThrottleInterval);
                WriteU32(destination, 8, command.PacketThrottleAcceleration);
                WriteU32(destination, 12, command.PacketThrottleDeceleration);
                break;
        }

        return size;
    }

    private static ushort ReadU16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    private static uint ReadU32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);

    private static void WriteU16(Span<byte> data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(data[offset..], value);

    private static void WriteU32(Span<byte> data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(data[offset..], value);
}
