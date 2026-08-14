using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

public class BackendGamemodeData : IGamemodeData
{
    public required UnlimitedArray<IInGameCar> CarsInRace { get; init; }
    public required BackendStage CurrentStage { get; init; }
    public required RaceState RaceState { get; init; }
    public IClientCallbacks ClientCallbacks => ClientServer.AccidentallyCalledClientMethodOnServer<IClientCallbacks>();

    public static BackendGamemodeData Create(string stage)
    {
        var backendStage = new BackendStage(stage);
        var carsInRace = new ObservableUnlimitedArray<IInGameCar>();

        return new BackendGamemodeData
        {
            CurrentStage = backendStage,
            CarsInRace = carsInRace,
            RaceState = RaceState.InProgress
        };
    }

    public static IGamemodeData Create(string stage, StageLoader stageData)
    {
        var backendStage = new BackendStage(stage, stageData);
        var carsInRace = new ObservableUnlimitedArray<IInGameCar>();

        return new BackendGamemodeData
        {
            CurrentStage = backendStage,
            CarsInRace = carsInRace,
            RaceState = RaceState.InProgress
        };
    }
}
