using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

/// <summary>
/// Asks the lobby for a way into a race that is already running, without taking a slot. The
/// lobby answers with an <see cref="S2C.S2C_RaceStarted"/> marked as a spectator join.
/// </summary>
[TightPackable]
[PacketClientToServer(12)]
public partial struct C2S_SpectateSession : IPacketClientToServer<C2S_SpectateSession>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
}
