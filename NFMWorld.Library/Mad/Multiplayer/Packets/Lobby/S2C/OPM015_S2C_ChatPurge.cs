using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Tells every client to drop a sender's chat lines (a kick purges the target's recent chat).
/// </summary>
[TightPackable]
[PacketServerToClient(-18)]
public partial struct S2C_ChatPurge : IPacketServerToClient<S2C_ChatPurge>
{
    [TightPackOrder(0)] public required Guid SenderId { get; set; }
}
