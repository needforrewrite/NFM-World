using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

public class BackendRaceValues : IRaceValues
{
    public required UnlimitedArray<IInGameCar> CarsInRace { get; init; }
    public required BackendStage CurrentStage { get; init; }
    public required RaceState raceState { get; init; }
    
    public static BackendRaceValues Create(string stage)
    {
        var backendStage = new BackendStage(stage);
        var carsInRace = new UnlimitedArray<IInGameCar>();

        return new BackendRaceValues
        {
            CurrentStage = backendStage,
            CarsInRace = carsInRace,
            raceState = RaceState.InProgress
        };
    }

    public static IRaceValues Create(string stage, StageLoader stageData)
    {
        var backendStage = new BackendStage(stage, stageData);
        var carsInRace = new UnlimitedArray<IInGameCar>();

        return new BackendRaceValues
        {
            CurrentStage = backendStage,
            CarsInRace = carsInRace,
            raceState = RaceState.InProgress
        };
    }
}