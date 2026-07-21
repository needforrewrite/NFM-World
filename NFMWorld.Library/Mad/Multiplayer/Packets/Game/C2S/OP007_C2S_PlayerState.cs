using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MemoryPackable]
[PacketClientToServer(7)]
public partial struct C2S_PlayerState : IPacketClientToServer<C2S_PlayerState>
{
    [MemoryPackOrder(0)] public required PlayerState State;
}