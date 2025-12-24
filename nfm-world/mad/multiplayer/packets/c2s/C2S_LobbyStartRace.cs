using System.Buffers;
using CommunityToolkit.HighPerformance;
using MessagePack;
using NFMWorld.Util;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct C2S_LobbyStartRace : IPacketClientToServer<C2S_LobbyStartRace>
{
    [Key(0)] public required uint SessionId { get; set; }
    
    public C2S_LobbyStartRace()
    {
    }
}