using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MemoryPackable]
[PacketClientToServer(3)]
public partial struct C2S_LeaveSession : IPacketClientToServer<C2S_LeaveSession>
{
    [MemoryPackOrder(0)] public required uint SessionId { get; set; }
}
