using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(3)]
public partial struct C2S_LeaveSession : IPacketClientToServer<C2S_LeaveSession>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
}
