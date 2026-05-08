using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

public interface IRaceValues
{
    UnlimitedArray<IInGameCar> CarsInRace { get; }
    BackendStage CurrentStage { get; }
    RaceState raceState { get; }
}