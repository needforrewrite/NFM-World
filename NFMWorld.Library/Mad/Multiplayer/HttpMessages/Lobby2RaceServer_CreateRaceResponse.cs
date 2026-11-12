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
    /// A token that attaches a connection to the race without taking a car slot. It is counted
    /// in neither the roster nor the race start, so anyone may use it to watch.
    /// </summary>
    [TightPackOrder(1)]
    public Guid SpectatorToken { get; set; }
}
