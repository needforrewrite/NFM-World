namespace ENet;

[Flags]
public enum PacketFlags
{
    None = 0,
    Reliable = 1 << 0,
    Unsequenced = 1 << 1,
    NoAllocate = 1 << 2,
    UnreliableFragmented = 1 << 3,
    Instant = 1 << 4,
    Unthrottled = 1 << 5,
    Sent = 1 << 8
}

public enum EventType
{
    None = 0,
    Connect = 1,
    Disconnect = 2,
    Receive = 3,
    Timeout = 4
}

public enum PeerState
{
    Uninitialized = -1,
    Disconnected = 0,
    Connecting = 1,
    AcknowledgingConnect = 2,
    ConnectionPending = 3,
    ConnectionSucceeded = 4,
    Connected = 5,
    DisconnectLater = 6,
    Disconnecting = 7,
    AcknowledgingDisconnect = 8,
    Zombie = 9
}
