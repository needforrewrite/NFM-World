using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-3)]
public partial struct S2C_PlayerState : IPacketServerToClient<S2C_PlayerState>
{
    [TightPackOrder(0)] public required Guid PlayerId { get; set; }
    [TightPackOrder(1)] public required PlayerState State;

    [TightPackOrder(2)] private ulong _currentTimeInMs;

    [TightPackIgnore]
    public DateTimeOffset CurrentServerTime
    {
        readonly get => DateTimeOffset.FromUnixTimeMilliseconds((long)_currentTimeInMs);
        set => _currentTimeInMs = (ulong)value.ToUnixTimeMilliseconds();
    }
}
