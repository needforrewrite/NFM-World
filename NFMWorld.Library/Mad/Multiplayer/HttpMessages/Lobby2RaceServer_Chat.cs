using TightPack;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Lobby → Game Master: one match-lobby chat line. Racers get it in the in-game chat, and the
/// game master does not report it back to the lobby (the lobby already has it).
/// </summary>
[TightPackable]
public partial struct Lobby2RaceServer_Chat
{
    [TightPackOrder(0)] public required Guid MatchKey { get; set; }
    [TightPackOrder(1)] public Guid SenderId { get; set; }
    [TightPackOrder(2)] public required string Sender { get; set; }
    [TightPackOrder(3)] public required string Message { get; set; }
    [TightPackOrder(4)] public Color3 Color { get; set; }
}
