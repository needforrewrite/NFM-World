using NFMWorldLibrary.Files;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class TimeTrialSimulationGamemode(BaseGamemodeParameters gamemodeParameters, IRaceValues raceValues, SavedTimeTrial timeTrial)
    : TimeTrialGamemode(gamemodeParameters, raceValues)
{
    private int _tick = 0;
    public override void Reset()
    {
        base.Reset();
        _tick = 0;
    }

    protected override BackendCar LoadPlayerCar(int x, int z)
    {
        return new BackendCar(timeTrial.CarData ?? BackendGameSparker.GetCar(player.CarName).Rad!, 0, x, z, true);
    }

    protected override void TimeTrialInRace()
    {
        carsInRace[playerCarIndex].Control.Decode(timeTrial.GetTick(_tick) ?? (false, false, false, false, false));
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