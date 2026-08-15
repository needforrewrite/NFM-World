using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Helpers;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Abstract base for time trial gamemodes. Contains the shared state machine,
/// physics simulation, and checkpoint handling. No rendering, HUD, or recording.
/// </summary>
public abstract class TimeTrialGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData)
    : BaseClientGamemode(gamemodeParameters, gamemodeData)
{
    protected const int PlayerCarIndex = 0;
    protected const int GhostCarIndex = 1;

    protected enum TimeTrialState
    {
        NotStarted,
        Countdown,
        InProgress,
        Finished
    }

    protected int _countdownTime = 3;
    protected int _innerCountdownTicks = PlayerCarIndex;
    protected TimeTrialState _currentState = TimeTrialState.NotStarted;

    public override void Begin()
    {
        base.Begin();
        _currentState = TimeTrialState.NotStarted;
    }

    public override void Reset()
    {
        base.Reset();
        _countdownTime = 4;
        _innerCountdownTicks = 0;
        
        // Drop gamemode-created players (e.g., the ghost from a previous run).
        for (var i = Players.Count - 1; i >= 0; i--)
        {
            if (Players[i].IsFake)
                Players.RemoveAt(i);
        }

        var playerCar = LoadPlayerCar(0, 0);
        playerCar.CurrentCheckpoint = 0;
        playerCar.CurrentLap = 0;
        Players[PlayerCarIndex].Car = playerCar;

        _currentState = TimeTrialState.Countdown;

        OnResetComplete();
    }

    protected virtual BackendCar LoadPlayerCar(int x, int z)
    {
        return new BackendCar(Players[PlayerCarIndex].Parameters, PlayerCarIndex, x, z);
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

        OnGameTickComplete();
    }

    protected virtual void TimeTrialInRace()
    {
        OnBeforePhysics();

        var car = Players[PlayerCarIndex].Car!;
        car.Drive(CurrentStage);

        if (CurrentStage.checkpoints.Count == 0)
            return;

        FixHoopHelper.HandleFixHoops(CurrentStage, car);
        CheckPointHelper.HandleCheckPoint(CurrentStage, car);

        if (car.CurrentLap >= CurrentStage.nlaps)
            _currentState = TimeTrialState.Finished;

        OnAfterPhysics();
    }

    protected virtual void TimeTrialFinished()
    {
        var car = Players[PlayerCarIndex].Car!;
        car.CarPhysics.Halted = true;
        car.Drive(gamemodeData.CurrentStage);

        OnFinishedComplete();
    }

    protected virtual void CountdownTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            _innerCountdownTicks = (int)(10 * (1 / Physics.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
                _currentState = TimeTrialState.InProgress;
        }

        OnCountdownTickComplete();
    }

    // ── Virtual hooks ──

    protected virtual void OnResetComplete() { }
    protected virtual void OnGameTickComplete() { }
    protected virtual void OnBeforePhysics() { }
    protected virtual void OnAfterPhysics() { }
    protected virtual void OnFinishedComplete() { }
    protected virtual void OnCountdownTickComplete() { }
}
