using TightPack;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Game Master → Lobby: race completion report sent via HTTP POST /race-ended.
/// </summary>
[TightPackable]
public partial struct RaceServer2Lobby_RaceResults
{
    /// <summary>The MatchKey identifying this race.</summary>
    [TightPackOrder(0)]
    public required string MatchKey { get; set; }

    /// <summary>Player finish results, keyed by player index.</summary>
    [TightPackOrder(1)]
    public required RaceResults Results { get; set; }
}
