using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[MemoryPackable]
[PacketServerToClient(-3)]
public partial struct S2C_PlayerState : IPacketServerToClient<S2C_PlayerState>
{
    [MemoryPackOrder(0)] public required Guid PlayerId { get; set; }
    [MemoryPackOrder(1)] public required PlayerState State;

    [MemoryPackOrder(2)] private ulong _currentTimeInMs;

    [MemoryPackIgnore]
    public DateTimeOffset CurrentServerTime
    {
        readonly get => DateTimeOffset.FromUnixTimeMilliseconds((long)_currentTimeInMs);
        set => _currentTimeInMs = (ulong)value.ToUnixTimeMilliseconds();
    }
}