using NFMWorld.DriverInterface;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

/// <summary>
/// Data for the gamemode.
/// </summary>
public interface IGamemodeData
{
    ObservableUnlimitedArray<IInGameCar> CarsInRace { get; }
    BackendStage CurrentStage { get; }
    RaceState RaceState { get; }

    [ClientOnly]
    IClientCallbacks ClientCallbacks { get; }
}