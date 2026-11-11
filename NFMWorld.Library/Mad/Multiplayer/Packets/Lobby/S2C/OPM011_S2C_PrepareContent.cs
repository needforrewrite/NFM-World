using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Asks a lobby member to download the race's canonical content before the race is created.
/// The acknowledgement must quote <see cref="PreparationId"/>: a reply to a superseded request
/// (the selection changed, the session restarted) must not be accepted for the current one.
/// </summary>
[MemoryPackable]
[PacketServerToClient(-11)]
public partial struct S2C_PrepareContent : IPacketServerToClient<S2C_PrepareContent>
{
    [MemoryPackOrder(0)] public required uint SessionId { get; set; }
    [MemoryPackOrder(1)] public required Guid PreparationId { get; set; }
    [MemoryPackOrder(2)] public required PreparedContent[] Content { get; set; }
}
