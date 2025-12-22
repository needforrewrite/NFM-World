using System.Buffers;
using CommunityToolkit.HighPerformance;
using MessagePack;
using NFMWorld.Util;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct S2C_PlayerState : IPacketServerToClient<S2C_PlayerState>
{
    [Key(0)] public required uint PlayerClientId { get; set; } = 0;
    [Key(1)] public required PlayerState State;

    public S2C_PlayerState()
    {
    }
}