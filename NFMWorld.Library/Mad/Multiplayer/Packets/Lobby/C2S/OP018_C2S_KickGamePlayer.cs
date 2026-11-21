using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(18)]
public partial struct C2S_KickGamePlayer : IPacketClientToServer<C2S_KickGamePlayer>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
    [TightPackOrder(1)] public required Guid PlayerId { get; set; }
}
