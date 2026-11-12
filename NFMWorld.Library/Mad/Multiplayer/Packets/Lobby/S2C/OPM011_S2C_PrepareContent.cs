using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Asks a lobby member to download the race's canonical content before the race is created.
/// The acknowledgement must quote <see cref="PreparationId"/>: a reply to a superseded request
/// (the selection changed, the session restarted) must not be accepted for the current one.
/// </summary>
[TightPackable]
[PacketServerToClient(-11)]
public partial struct S2C_PrepareContent : IPacketServerToClient<S2C_PrepareContent>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
    [TightPackOrder(1)] public required Guid PreparationId { get; set; }
    [TightPackOrder(2)] public required PreparedContent[] Content { get; set; }
}
