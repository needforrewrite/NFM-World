using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(19)]
public partial struct C2S_InviteGamePlayer : IPacketClientToServer<C2S_InviteGamePlayer>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
    [TightPackOrder(1)] public required Guid PlayerId { get; set; }
}
