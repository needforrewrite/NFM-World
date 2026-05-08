using MessagePack;

namespace NFMWorldLibrary.Multiplayer.packets.s2c;

[MessagePackObject(AllowPrivate = true)]
public partial struct S2C_PlayerState : IPacketServerToClient<S2C_PlayerState>
{
    [Key(0)] public required uint PlayerClientId { get; set; } = 0;
    [Key(1)] public required PlayerState State;

    [Key(2)] private ulong _currentTimeInMs;

    [IgnoreMember]
    public DateTimeOffset CurrentServerTime
    {
        readonly get => DateTimeOffset.FromUnixTimeMilliseconds((long)_currentTimeInMs);
        set => _currentTimeInMs = (ulong)value.ToUnixTimeMilliseconds();
    }
    
    public S2C_PlayerState()
    {
    }
}