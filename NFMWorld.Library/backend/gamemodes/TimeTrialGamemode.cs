using NFMWorldLibrary.Helpers;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class TimeTrialGamemode(BaseGamemodeParameters gamemodeParameters, IRaceValues raceValues)
    : BaseGamemode(gamemodeParameters, raceValues)
{
    public override event EventHandler<byte[]>? RaceFinished;

    protected enum TimeTrialState
    {
        NotStarted,
        Countdown,
        InProgress,
        Finished
    }

    protected int _countdownTime = 3;
    // Amount of ticks until we decrease countdown by 1
    protected int _innerCountdownTicks = 0;
    protected TimeTrialState _currentState = TimeTrialState.NotStarted;

    public override void Enter()
    {
        base.Enter();
        
        _currentState = TimeTrialState.NotStarted;
    }

    public override void Reset()
    {
        base.Reset();
        _countdownTime = 4;
        _innerCountdownTicks = 0; // Tick down immediately to "three"
        
        carsInRace.Clear();
        carsInRace[playerCarIndex] = LoadPlayerCar(0, 0);
        carsInRace[playerCarIndex].currentCheckpoint = 0;
        carsInRace[playerCarIndex].currentLap = 0;

        _currentState = TimeTrialState.Countdown;

        carsInRace[playerCarIndex].currentLap = 0;
    }

    protected virtual BackendCar LoadPlayerCar(int x, int z)
    {
        return new BackendCar(BackendGameSparker.GetCar(player.CarName).Rad!, 0, x, z, true);
    }

    public override void GameTick()
    {
        base.GameTick();
        switch (_currentState)
        {
            case TimeTrialState.NotStarted:
                Reset();
                break;
            case TimeTrialState.Countdown:
                CountdownTick();
                break;
            case TimeTrialState.InProgress:
                TimeTrialInRace();
                break;
            case TimeTrialState.Finished:
                TimeTrialFinished();
                break;
        }
    }

    protected virtual void TimeTrialInRace()
    {
        carsInRace[playerCarIndex].Drive(currentStage);
        
        if (currentStage.checkpoints.Count == 0)
        {
            // lol
            return;
        }

        FixHoopHelper.HandleFixHoops(currentStage, carsInRace[playerCarIndex]);
        CheckPointHelper.HandleCheckPoint(currentStage, carsInRace[playerCarIndex]);

        if (carsInRace[playerCarIndex].currentLap >= currentStage.nlaps)
        {
            RaceFinished?.Invoke(this, []);
            _currentState = TimeTrialState.Finished;
        }
    }
    
    protected virtual void TimeTrialFinished()
    {
        carsInRace[playerCarIndex].Mad.Halted = true;
        carsInRace[playerCarIndex].Drive(raceValues.CurrentStage);;
    }

    protected virtual void CountdownTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            _innerCountdownTicks = (int)(10 * (1 / Physics.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
            {
                _currentState = TimeTrialState.InProgress;
            }
        }
    }
}