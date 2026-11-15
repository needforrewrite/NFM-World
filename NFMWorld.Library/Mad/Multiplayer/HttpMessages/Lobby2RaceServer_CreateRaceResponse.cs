using TightPack;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

/// <summary>
/// Response from Game Master to Lobby after creating a race.
/// The Lobby already knows the GM's game address from SRV resolution;
/// only the per-player join tokens need to be returned.
/// </summary>
[TightPackable]
public partial struct Lobby2RaceServer_CreateRaceResponse
{
    /// <summary>
    /// Key: player car index as in <see cref="MatchGameplayInfo"/>
    /// Value: Secret GUID that said player can use to authenticate with the race server.
    /// </summary>
    [TightPackOrder(0)]
    public required IDictionary<byte, Guid> PlayerSecretIds { get; set; }

    /// <summary>
    /// Used by the lobby to register named spectators without taking a car slot.
    /// </summary>
    [TightPackOrder(1)]
    public Guid SpectatorToken { get; set; }
}
