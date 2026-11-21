using TightPack;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Game Master → Lobby: one in-game chat line. The lobby relays it to the members of the
/// session that match belongs to, so players waiting in the match lobby see the race talk.
/// </summary>
[TightPackable]
public partial struct RaceServer2Lobby_Chat
{
    [TightPackOrder(0)] public required string MatchKey { get; set; }
    [TightPackOrder(1)] public Guid SenderId { get; set; }
    [TightPackOrder(2)] public required string Sender { get; set; }
    [TightPackOrder(3)] public required string Message { get; set; }
    [TightPackOrder(4)] public Color3 Color { get; set; }
}
