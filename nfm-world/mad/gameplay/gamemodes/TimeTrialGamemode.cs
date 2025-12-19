using System.Diagnostics;
using NFMWorld.Mad;
using NFMWorld.Util;
using Stride.Core.Mathematics;

public class TimeTrialGamemode : BaseGamemode
{
    private enum TimeTrialState
    {
        NotStarted,
        Countdown,
        InProgress,
        Finished
    }

    private int _countdownTime = 3;
    // Amount of ticks until we decrease countdown by 1
    private int _innerCountdownTicks = 0;
    private TimeTrialState _currentState = TimeTrialState.NotStarted;

    private int currentCheckpoint = 0;
    private int currentLap = 0;

    private Stopwatch raceTimer = new Stopwatch();

    public override void Enter()
    {

    }

    public override void Exit()
    {
        // Cleanup for Time Trial mode
    }

    public override void GameTick(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        switch (_currentState)
        {
            case TimeTrialState.NotStarted:
                _currentState = TimeTrialState.Countdown;
                _countdownTime = 4;
                _innerCountdownTicks = 0; // Tick down immediately
                currentCheckpoint = 0;
                currentLap = 1;
                raceTimer.Reset();
                carsInRace[0].ResetPosition();
                break;
            case TimeTrialState.Countdown:
                CountdownTick();
                break;
            case TimeTrialState.InProgress:
                TimeTrialInRace(carsInRace, currentStage);
                break;
            case TimeTrialState.Finished:
                TimeTrialFinished();
                break;
        }
    }

    private void TimeTrialInRace(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        carsInRace[0].Drive();

        G.SetColor(new NFMWorld.Util.Color(0, 0, 0));
        G.DrawString($"Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds / 10:D2}", 100, 200);
        G.DrawString($"Lap: {currentLap}/{currentStage.nlaps}", 100, 250);

        CheckPoint nextCheckpoint = currentStage.checkpoints[currentCheckpoint];
        Vector3 carPos = carsInRace[0].CarRef.Position;

        if (nextCheckpoint.CheckPointRot == CheckPoint.CheckPointRotation.None)
        {
            if (Math.Abs(carPos.Z - nextCheckpoint.Position.Z) <
                        60.0F + Math.Abs(carsInRace[0].Mad.Scz[0] + carsInRace[0].Mad.Scz[1] + carsInRace[0].Mad.Scz[2] + carsInRace[0].Mad.Scz[3]) / 4.0F &&
                        Math.Abs(carsInRace[0].CarRef.Position.X - nextCheckpoint.Position.X) < 700 &&
                        Math.Abs(carsInRace[0].CarRef.Position.Y - nextCheckpoint.Position.Y + 350) < 450)
            {
                currentCheckpoint++;
                if (currentCheckpoint >= currentStage.checkpoints.Count)
                {
                    currentCheckpoint = 0;
                    currentLap++;
                    // For Time Trial, we can consider finishing after one lap
                    _currentState = TimeTrialState.Finished;
                    raceTimer.Stop();
                }
            }
        } else // None
        {
            if (Math.Abs(carPos.X - nextCheckpoint.Position.X) <
                        60.0F + Math.Abs(carsInRace[0].Mad.Scx[0] + carsInRace[0].Mad.Scx[1] + carsInRace[0].Mad.Scx[2] + carsInRace[0].Mad.Scx[3]) / 4.0F &&
                        Math.Abs(carPos.Z - nextCheckpoint.Position.Z) < 700 &&
                        Math.Abs(carPos.Y - nextCheckpoint.Position.Y + 350) < 450)
            {
                currentCheckpoint++;
                if (currentCheckpoint >= currentStage.checkpoints.Count)
                {
                    currentCheckpoint = 0;
                    currentLap++;
                }
            }
        }

        if(currentLap > currentStage.nlaps)
        {
            _currentState = TimeTrialState.Finished;
            raceTimer.Stop();
        }
    }

    private void TimeTrialFinished()
    {
        G.SetColor(new NFMWorld.Util.Color(128, 255, 128));
        G.DrawString($"Finished! Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds / 10:D2}", 100, 200);
        G.DrawString($"Press R to restart", 100, 250);
    }

    private void CountdownTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            SfxLibrary.countdown[_countdownTime].Play();
            _innerCountdownTicks = (int)(20 * (1 / GameSparker.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
            {
                _currentState = TimeTrialState.InProgress;
                raceTimer.Start();
            }
        }

        G.SetColor(new NFMWorld.Util.Color(0, 0, 0));
        G.DrawString($"Starting in {_countdownTime}", 400, 300);
    }

    public override void KeyPressed(Keys key)
    {
        // Handle key presses specific to Time Trial mode
        if (key == Keys.R)
        {
            // Reset
            _currentState = TimeTrialState.NotStarted;
        }
    }

    public override void KeyReleased(Keys key)
    {
        // Handle key releases specific to Time Trial mode
    }

    public override void Render(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        // Time Trial specific rendering logic
    }
}