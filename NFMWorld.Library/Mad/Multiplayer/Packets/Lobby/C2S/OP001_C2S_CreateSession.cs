using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MemoryPackable]
[PacketClientToServer(1)]
public partial struct C2S_CreateSession : IPacketClientToServer<C2S_CreateSession>
{
    [MemoryPackOrder(0)] public required string StageName { get; set; }
    [MemoryPackOrder(1)] public required int MaxPlayers { get; set; }
    [MemoryPackOrder(2)] public required string GameMode { get; set; }
}
