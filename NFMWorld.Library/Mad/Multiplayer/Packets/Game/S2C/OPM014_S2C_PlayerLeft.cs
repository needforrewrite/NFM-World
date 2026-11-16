using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-14)]
public partial struct S2C_PlayerLeft : IPacketServerToClient<S2C_PlayerLeft>
{
    [TightPackOrder(0)] public required Guid PlayerId { get; set; }
    [TightPackOrder(1)] public required PlayerDepartureReason Reason { get; set; }
    [TightPackOrder(2)] public bool Announce { get; set; }
}
