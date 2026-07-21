using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Response from Game Master to Lobby after creating a race.
/// The Lobby already knows the GM's game address from SRV resolution;
/// only the per-player join tokens need to be returned.
/// </summary>
[MemoryPackable]
public partial struct Lobby2RaceServer_CreateRaceResponse
{
    /// <summary>
    /// Key: player car index as in <see cref="MatchGameplayInfo"/>
    /// Value: Secret GUID that said player can use to authenticate with the race server.
    /// </summary>
    [MemoryPackOrder(0)] public required IDictionary<byte, Guid> PlayerSecretIds { get; set; }
}