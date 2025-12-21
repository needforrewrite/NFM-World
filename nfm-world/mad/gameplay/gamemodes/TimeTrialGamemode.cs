using System.Collections;
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

    private TimeTrialSplitsFile thisRunSplits = null!;
    private TimeTrialSplitsFile? bestTimeSplits = null;

    private int _countdownTime = 3;
    // Amount of ticks until we decrease countdown by 1
    private int _innerCountdownTicks = 0;
    private TimeTrialState _currentState = TimeTrialState.NotStarted;

    private int currentCheckpoint = 0;
    private int currentLap = 0;
    private bool writtenData;

    private Stopwatch raceTimer = new Stopwatch();

    // demo playback and recording
    private TimeTrialDemoFile? playbackInputs = null;
    private int tick = 0;
    public static bool PlaybackOnReset = true;
    private TimeTrialDemoFile recordedInputs = null!;

    public override void Enter(UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
    {
        Reset();

        InRacePhase.playerCarIndex = 0;
        carsInRace[0] = new InGameCar(0, GameSparker.GetCar(InRacePhase.playerCarName).Car, 0, 0, true);

        // ghost
        carsInRace[1] = new InGameCar(carsInRace[0], 0, false);
        carsInRace[1].Sfx.Mute = true;

        TimeTrialDemoFile bestTimeDemo = new TimeTrialDemoFile(InRacePhase.playerCarName, currentStage.Name);
        if (bestTimeDemo.Load())
        {
            playbackInputs = bestTimeDemo;
            carsInRace[1].CarRef.alphaOverride = 0.05f;
        }
        else
        {
            carsInRace.RemoveAt(1);
        }

        TimeTrialSplitsFile bestTimeSplits = new TimeTrialSplitsFile(InRacePhase.playerCarName, currentStage.Name);
        if (bestTimeSplits.Load())
        {
            this.bestTimeSplits = bestTimeSplits;
        } else
        {
            this.bestTimeSplits = null;
        }

        thisRunSplits = new TimeTrialSplitsFile(InRacePhase.playerCarName, currentStage.Name);
        recordedInputs = new TimeTrialDemoFile(InRacePhase.playerCarName, currentStage.Name);

        _currentState = TimeTrialState.Countdown;
    }

    public override void Exit(UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
    {
        // Cleanup for Time Trial mode
    }

    public override void Reset()
    {
        base.Reset();

        _currentState = TimeTrialState.NotStarted;
        _countdownTime = 4;
        _innerCountdownTicks = 0; // Tick down immediately
        currentCheckpoint = 0;
        currentLap = 1;
        raceTimer.Reset();
        writtenData = false;

        // demos
        playbackInputs = null;
        tick = 0;
    }

    public override void GameTick(UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
    {
        FrameTrace.AddMessage($"contox: {carsInRace[0].CarRef.Position.X:0.00}, contoz: {carsInRace[0].CarRef.Position.Z:0.00}, contoy: {carsInRace[0].CarRef.Position.Y:0.00}");
        switch (_currentState)
        {
            case TimeTrialState.NotStarted:
                Enter(carsInRace, currentStage, currentScene);
                break;
            case TimeTrialState.Countdown:
                CountdownTick();
                break;
            case TimeTrialState.InProgress:
                TimeTrialInRace(carsInRace, currentStage);
                break;
            case TimeTrialState.Finished:
                TimeTrialFinished(carsInRace, currentStage);
                break;
        }
    }

    private void TimeTrialInRace(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        if (playbackInputs != null)
        {
            carsInRace[1].Control.Decode(playbackInputs.GetTick(tick) ?? new BitArray([false, false, false, false, false]));

            carsInRace[1].Drive();
        }

        recordedInputs.Record(carsInRace[0].Control);
        carsInRace[0].Drive();

        if (currentStage.checkpoints.Count == 0)
        {
            // lol
            return;
        }

        CheckPoint nextCheckpoint = currentStage.checkpoints[currentCheckpoint];
        Vector3 carPos = carsInRace[0].CarRef.Position;

        if (nextCheckpoint.CheckPointRot == CheckPoint.CheckPointRotation.None)
        {
            if (Math.Abs(carPos.Z - nextCheckpoint.Position.Z) <
                        60.0F + Math.Abs(carsInRace[0].Mad.Scz[0] + carsInRace[0].Mad.Scz[1] + carsInRace[0].Mad.Scz[2] + carsInRace[0].Mad.Scz[3]) / 4.0F &&
                        Math.Abs(carsInRace[0].CarRef.Position.X - nextCheckpoint.Position.X) < 700 &&
                        Math.Abs(carsInRace[0].CarRef.Position.Y - nextCheckpoint.Position.Y + 350) < 450)
            {
                thisRunSplits.Record(raceTimer.ElapsedMilliseconds);
                currentCheckpoint++;
                SfxLibrary.checkpoint?.Play();
                if (currentCheckpoint >= currentStage.checkpoints.Count)
                {
                    currentCheckpoint = 0;
                    currentLap++;
                }
            }
        }
        else // None
        {
            if (Math.Abs(carPos.X - nextCheckpoint.Position.X) <
                        60.0F + Math.Abs(carsInRace[0].Mad.Scx[0] + carsInRace[0].Mad.Scx[1] + carsInRace[0].Mad.Scx[2] + carsInRace[0].Mad.Scx[3]) / 4.0F &&
                        Math.Abs(carPos.Z - nextCheckpoint.Position.Z) < 700 &&
                        Math.Abs(carPos.Y - nextCheckpoint.Position.Y + 350) < 450)
            {
                thisRunSplits.Record(raceTimer.ElapsedMilliseconds);
                SfxLibrary.checkpoint?.Play();
                currentCheckpoint++;
                if (currentCheckpoint >= currentStage.checkpoints.Count)
                {
                    currentCheckpoint = 0;
                    currentLap++;
                }
            }
        }

        if (currentCheckpoint == currentStage.checkpoints.Count - 1 && currentLap == currentStage.nlaps)
        {
            currentStage.checkpoints[^1].Finish = true;
        }
        else
        {
            currentStage.checkpoints[^1].Finish = false;
        }

        if (currentCheckpoint > 0)
        {
            currentStage.checkpoints[currentCheckpoint - 1].Glow = false;
        }
        else
        {
            currentStage.checkpoints[^1].Glow = false;
        }

        if (currentCheckpoint < currentStage.checkpoints.Count)
        {
            currentStage.checkpoints[currentCheckpoint].Glow = true;
        }

        if (currentLap > currentStage.nlaps)
        {
            _currentState = TimeTrialState.Finished;
            raceTimer.Stop();
        }

        tick++;
    }

    private void TimeTrialFinished(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        if (!writtenData)
        {
            writtenData = true;
            if (bestTimeSplits == null || (bestTimeSplits != null && thisRunSplits.GetDiff(bestTimeSplits, thisRunSplits.Splits.Count - 1) < 0))
            {
                thisRunSplits.Save();
                recordedInputs.Save();
            }
        }

        carsInRace[0].Mad.Halted = true;
        carsInRace[0].Drive();
    }

    private void CountdownTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            SfxLibrary.countdown[_countdownTime].Play();
            _innerCountdownTicks = (int)(10 * (1 / GameSparker.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
            {
                _currentState = TimeTrialState.InProgress;
                raceTimer.Start();
            }
        }
    }

    public override void KeyPressed(Keys key)
    {
        // Handle key presses specific to Time Trial mode
        if (key == Keys.R)
        {
            Reset();
        }
    }

    public override void KeyReleased(Keys key)
    {
        // Handle key releases specific to Time Trial mode
    }

    public override void Render(UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
    {
        if (_currentState == TimeTrialState.InProgress)
        {
            G.SetColor(new NFMWorld.Util.Color(0, 0, 0));
            G.DrawString($"Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds / 10:D2}", 100, 200);
            G.DrawString($"Lap: {currentLap}/{currentStage.nlaps}", 100, 250);

            if ((currentCheckpoint != 0 || currentLap != 1) && bestTimeSplits != null)
            {
                long diff = thisRunSplits.GetDiff(bestTimeSplits, thisRunSplits.Splits.Count - 1);
                if (diff > 0)
                {
                    G.SetColor(new NFMWorld.Util.Color(255, 128, 128));
                }
                else if (diff < 0)
                {
                    G.SetColor(new NFMWorld.Util.Color(128, 255, 128));
                }

                long diffSeconds = Math.Abs(diff / 1000);
                long diffMs = Math.Abs(diff % 1000);

                string fmt = $"{(diff > 0 ? "+" : "-")}{diffSeconds}s {diffMs}ms";

                G.DrawString("This Split: " + fmt, 100, 350);
            }
        }
        else if (_currentState == TimeTrialState.Countdown)
        {
            G.SetColor(new NFMWorld.Util.Color(0, 0, 0));
            G.DrawString($"Starting in {_countdownTime}", 400, 300);
        }
        else if (_currentState == TimeTrialState.Finished)
        {
            G.SetColor(new NFMWorld.Util.Color(128, 255, 128));
            G.DrawString($"Finished! Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds:D2}", 300, 200);

            bool newBest = bestTimeSplits == null || (bestTimeSplits != null && thisRunSplits.GetDiff(bestTimeSplits, thisRunSplits.Splits.Count - 1) < 0);

            if (bestTimeSplits != null || newBest)
            {
                long bestTimeMs = Math.Min(thisRunSplits.Splits[^1], bestTimeSplits != null ? bestTimeSplits.Splits[^1] : long.MaxValue);

                TimeSpan t = TimeSpan.FromMilliseconds(bestTimeMs);

                string time = string.Format("{0:D2}:{1:D2}:{2:D2}",
                            t.Minutes,
                            t.Seconds,
                            t.Milliseconds);

                G.DrawString("Best time: " + time, 300, 225);
            }
            G.DrawString($"Press R to restart", 300, 250);
        }
    }
}