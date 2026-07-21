using MemoryPack;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Game Master → Lobby: race completion report sent via HTTP POST /race-ended.
/// </summary>
[MemoryPackable]
public partial struct RaceServer2Lobby_RaceResults
{
    /// <summary>The MatchKey identifying this race.</summary>
    [MemoryPackOrder(0)]
    public required string MatchKey { get; set; }

    /// <summary>Player finish results, keyed by player index.</summary>
    [MemoryPackOrder(1)]
    public required RaceResults Results { get; set; }
}
