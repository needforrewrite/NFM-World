using TightPack;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Game Master → Lobby: the current order of a running race. The lobby relays it to the members
/// waiting in that session's match lobby; people already in the race are watching the real thing.
/// </summary>
[TightPackable]
public partial struct RaceServer2Lobby_LiveState
{
    [TightPackOrder(0)] public required string MatchKey { get; set; }
    [TightPackOrder(1)] public required RaceLiveState State { get; set; }
}
