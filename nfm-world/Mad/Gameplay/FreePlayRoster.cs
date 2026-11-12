using NFMWorldLibrary;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorld.Gameplay;

/// <summary>
/// The free-play roster: the player's car, optionally plus up to six AI cars drawn from the
/// installed NFMM collection. Shared by the main menu's free play and the stage and model
/// editors' test drives.
/// </summary>
public static class FreePlayRoster
{
    public const int MaxOpponents = 6;

    public static ClientSidePlayerInfo[] Create(string playerCar, bool withOpponents = true)
    {
        var opponentCars = withOpponents
            ? BackendGameSparker.cars[Collection.NFMM]
                .Select(car => car.FileName)
                .Where(name => !string.Equals(name, playerCar, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        System.Random.Shared.Shuffle(opponentCars);

        var players = new ClientSidePlayerInfo[1 + Math.Min(MaxOpponents, opponentCars.Length)];
        players[0] = new ClientSidePlayerInfo
        {
            CarName = playerCar,
            IsClientPlayer = true,
            PlayerName = "MadPlayer",
            Color = default,
            IsBot = false
        };

        for (var i = 1; i < players.Length; i++)
        {
            players[i] = new ClientSidePlayerInfo
            {
                CarName = opponentCars[i - 1],
                IsClientPlayer = false,
                PlayerName = $"ElStupido{i}",
                Color = default,
                IsBot = true
            };
        }

        return players;
    }
}
