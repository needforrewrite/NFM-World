using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-13)]
public partial struct S2C_RaceChat : IPacketServerToClient<S2C_RaceChat>
{
    [TightPackOrder(0)] public required Guid SenderId { get; set; }
    [TightPackOrder(1)] public required string Sender { get; set; }
    [TightPackOrder(2)] public required string Message { get; set; }
    [TightPackOrder(3)] public Color3 Color { get; set; }
}
