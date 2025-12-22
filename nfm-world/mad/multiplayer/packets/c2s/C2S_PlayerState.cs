using System.Buffers;
using CommunityToolkit.HighPerformance;
using MessagePack;
using NFMWorld.Util;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct C2S_PlayerState : IPacketClientToServer<C2S_PlayerState>
{
    [Key(0)] public required PlayerState State;

    public C2S_PlayerState()
    {
    }
}