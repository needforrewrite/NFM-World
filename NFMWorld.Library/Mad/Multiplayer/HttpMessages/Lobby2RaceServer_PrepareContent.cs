using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Asks the game master to download the race's canonical content. Sent before
/// <see cref="Lobby2RaceServer_CreateRace"/> on its own request, so a cold asset does not consume
/// the create-race window.
/// </summary>
[MemoryPackable]
public partial struct Lobby2RaceServer_PrepareContent
{
    [MemoryPackOrder(0)] public required Guid MatchKey { get; set; }
    [MemoryPackOrder(1)] public required PreparedContent[] Content { get; set; }
}
