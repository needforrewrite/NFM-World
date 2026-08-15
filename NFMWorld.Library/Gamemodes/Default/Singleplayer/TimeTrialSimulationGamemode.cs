using NFMWorldLibrary.Files;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class TimeTrialSimulationGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData, SavedTimeTrial timeTrial)
    : TimeTrialGamemode(gamemodeParameters, gamemodeData)
{
    private int _tick = 0;
    public override void Reset()
    {
        base.Reset();
        _tick = 0;
    }

    protected override BackendCar LoadPlayerCar(int x, int z)
    {
        return new BackendCar(timeTrial.CarData ?? BackendGameSparker.GetCar(Players[0].Parameters.CarName).Rad!, 0, x, z, true);
    }

    protected override void TimeTrialInRace()
    {
        Players[PlayerCarIndex].Car!.Control.Decode(timeTrial.GetTick(_tick) ?? (false, false, false, false, false));
        base.TimeTrialInRace();
        _tick++;
    }

    public int? SimulateToCompletion(int tickLimit = 100_000_000)
    {
        while (_currentState != TimeTrialState.Finished)
        {
            GameTick();
            if (_tick > tickLimit)
            {
                return null;
            }
        }

        return _tick;
    }
}