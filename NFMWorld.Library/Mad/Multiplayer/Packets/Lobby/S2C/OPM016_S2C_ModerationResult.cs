using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// The actor's own outcome for a moderation command; the only client that receives it.
/// </summary>
[TightPackable]
[PacketServerToClient(-19)]
public partial struct S2C_ModerationResult : IPacketServerToClient<S2C_ModerationResult>
{
    [TightPackOrder(0)] public bool Ok { get; set; }
    [TightPackOrder(1)] public required string Message { get; set; }
}
