using MemoryPack;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

// Authorization: Bearer <SecretKey>
[MemoryPackable]
public partial struct Lobby2RaceServer_CreateRace
{
    /// <summary>
    /// Secret string unquely identifying the match. When the race is over, the game server sends an HTTP request to
    /// the lobby server to notify it that the match is over. The lobby server uses this key to identify which match is
    /// over.
    /// </summary>
    [MemoryPackOrder(0)] public required Guid MatchKey { get; set; }

    /// <summary>
    /// Information used to construct the race gameplay.
    /// </summary>
    [MemoryPackOrder(1)] public required MatchGameplayInfo MatchGameplayInfo { get; set; }
}