using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MemoryPackable]
[PacketClientToServer(2)]
public partial struct C2S_JoinSession : IPacketClientToServer<C2S_JoinSession>
{
    [MemoryPackOrder(0)] public required uint SessionId { get; set; }
}
