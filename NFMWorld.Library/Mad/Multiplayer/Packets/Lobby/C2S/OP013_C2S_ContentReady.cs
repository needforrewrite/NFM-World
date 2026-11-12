using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

/// <summary>
/// Says whether this client now holds every asset from an <see cref="S2C.S2C_PrepareContent"/>.
/// <see cref="Ready"/> false carries the reason it could not, so the lobby can fail the start
/// instead of waiting for a race that can never load.
/// </summary>
[TightPackable]
[PacketClientToServer(13)]
public partial struct C2S_ContentReady : IPacketClientToServer<C2S_ContentReady>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
    [TightPackOrder(1)] public required Guid PreparationId { get; set; }
    [TightPackOrder(2)] public required bool Ready { get; set; }
    [TightPackOrder(3)] public string? Reason { get; set; }
}
