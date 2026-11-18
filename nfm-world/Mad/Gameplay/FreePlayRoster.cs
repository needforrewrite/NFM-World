using NFMWorldLibrary;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

/// <summary>
/// The free-play roster: the player's car, optionally plus up to six AI cars drawn from the
/// installed NFMM collection. Shared by the main menu's free play and the stage and model
/// editors' test drives.
/// </summary>
public static class FreePlayRoster
{
    public const int MaxOpponents = 6;

    public static ClientSidePlayerInfo[] Create(string playerCar, bool withOpponents = true, bool rocket = false)
    {
        var opponentCars = withOpponents && !rocket
            ? BackendGameSparker.cars[Collection.NFMM]
                .Where(car => !string.Equals(car.FileName, playerCar, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(car => car.FileName, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        System.Random.Shared.Shuffle(opponentCars);

        var players = rocket
            ? new ClientSidePlayerInfo[1]
            : new ClientSidePlayerInfo[1 + Math.Min(MaxOpponents, opponentCars.Length)];
        players[0] = new ClientSidePlayerInfo
        {
            CarName = playerCar,
            CarColors = CarPaint.SavedColors(playerCar),
            Wheels = CarWheels.Get(playerCar),
            IsClientPlayer = true,
            PlayerName = "MadPlayer",
            Color = default,
            IsBot = false
        };

        for (var i = 1; i < players.Length; i++)
        {
            var car = opponentCars[i - 1];
            players[i] = new ClientSidePlayerInfo
            {
                CarName = car.FileName,
                IsClientPlayer = false,
                PlayerName = string.IsNullOrWhiteSpace(car.Stats.Name) ? car.FileName : car.Stats.Name,
                Color = default,
                IsBot = true
            };
        }

        return players;
    }
}
