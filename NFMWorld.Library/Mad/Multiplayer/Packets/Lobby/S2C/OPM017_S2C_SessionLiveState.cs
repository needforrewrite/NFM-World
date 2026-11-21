using TightPack;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Live order for one session, delivered only to its lobby members who are not in the race.
/// </summary>
[TightPackable]
[PacketServerToClient(-20)]
public partial struct S2C_SessionLiveState : IPacketServerToClient<S2C_SessionLiveState>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
    [TightPackOrder(1)] public required IList<RaceLiveStanding> Standings { get; set; }
}
