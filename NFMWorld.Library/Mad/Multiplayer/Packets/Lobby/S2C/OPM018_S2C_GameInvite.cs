using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-21)]
public partial struct S2C_GameInvite : IPacketServerToClient<S2C_GameInvite>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
    [TightPackOrder(1)] public required Guid InviteId { get; set; }
    [TightPackOrder(2)] public required string CreatorName { get; set; }
    [TightPackOrder(3)] public required long ExpiresAtUnixMs { get; set; }
}
