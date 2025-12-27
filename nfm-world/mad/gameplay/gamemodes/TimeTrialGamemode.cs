using System.Collections;
using System.Diagnostics;
using System.Reflection.Metadata;
using Maxine.Extensions;
using NFMWorld.DriverInterface;
using NFMWorld.Mad;
using NFMWorld.Mad.gamemodes;
using NFMWorld.Mad.UI.Elements;
using NFMWorld.Mad.UI.yoga;
using NFMWorld.Util;
using SoftFloat;
using Stride.Core.Mathematics;
using Color = NFMWorld.Util.Color;

public class TimeTrialGamemode(BaseGamemodeParameters gamemodeParameters, BaseRacePhase baseRacePhase)
    : BaseGamemode(gamemodeParameters, baseRacePhase)
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
    private bool writtenData;

    private Stopwatch raceTimer = new Stopwatch();

    // demo playback and recording
    private SavedTimeTrial? bestTimeTrial = null;
    private int tick = 0;
    public static bool PlaybackOnReset = true;
    private SavedTimeTrial currentTimeTrial = null!;

    private PowerDamageBars _pdBars = new PowerDamageBars();

    public override void Enter()
    {
        _currentState = TimeTrialState.NotStarted;

        Reset();
    }

    public override void Exit()
    {
        // Cleanup for Time Trial mode
    }

    public override void Reset()
    {
        base.Reset();

        _countdownTime = 4;
        _innerCountdownTicks = 0; // Tick down immediately to "three"
        currentCheckpoint = 0;
        currentLap = 1;
        raceTimer.Reset();
        writtenData = false;

        playerCarIndex = 0;

        // ghosts
        bestTimeTrial = null;
        tick = 0;

        carsInRace[playerCarIndex] = new InGameCar(0, GameSparker.GetCar(playerCarName).Car!, 0, 0, true);
        carsInRace[playerCarIndex].Mad.PowerUp += _pdBars.EventPowerUp;

        // ghost
        carsInRace[playerCarIndex + 1] = new InGameCar(carsInRace[playerCarIndex], 0, false);
        carsInRace[playerCarIndex + 1].Sfx.Mute = true;

        SavedTimeTrial bestTimeDemo = new SavedTimeTrial(player.CarName, currentStage.Path);
        if (bestTimeDemo.Load())
        {
            bestTimeTrial = bestTimeDemo;
            carsInRace[playerCarIndex + 1].CarRef.AlphaOverride = 0.05f;
        }
        else
        {
            carsInRace.RemoveAt(1);
        }

        currentTimeTrial = new SavedTimeTrial(player.CarName, currentStage.Path);

        foreach (CheckPoint cp in currentStage.checkpoints)
        {
            cp.Glow = false;
        }

        _currentState = TimeTrialState.Countdown;
    }

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {carsInRace[playerCarIndex].CarRef.Position.X:0.00}, contoz: {carsInRace[playerCarIndex].CarRef.Position.Z:0.00}, contoy: {carsInRace[playerCarIndex].CarRef.Position.Y:0.00}");
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

    private void TimeTrialInRace()
    {
        _pdBars.SetDamageBarFill(carsInRace[playerCarIndex].Mad.Hitmag, carsInRace[0].Stats.Maxmag);
        _pdBars.UpdateDamageBarColor();
        _pdBars.SetPowerBarFill((float)carsInRace[playerCarIndex].Mad.Power);
        _pdBars.UpdatePowerBarColor();

        if (bestTimeTrial != null)
        {
            carsInRace[playerCarIndex + 1].Control.Decode(bestTimeTrial.GetTick(tick) ?? Nibble<byte>.AllZeros);

            carsInRace[playerCarIndex + 1].Drive();
        }

        currentTimeTrial.RecordTick(carsInRace[playerCarIndex].Control);
        carsInRace[playerCarIndex].Drive();

        if (currentStage.checkpoints.Count == 0)
        {
            // lol
            return;
        }

        CheckPoint nextCheckpoint = currentStage.checkpoints[currentCheckpoint];
        Vector3 carPos = carsInRace[playerCarIndex].CarRef.Position;

        if (nextCheckpoint.CheckPointRot == CheckPoint.CheckPointRotation.None)
        {
            if (fix64.Abs((fix64)carPos.Z - (fix64)nextCheckpoint.Position.Z) <
                (fix64)60.0F + fix64.Abs(carsInRace[playerCarIndex].Mad.Scz[0] + carsInRace[playerCarIndex].Mad.Scz[1] + carsInRace[playerCarIndex].Mad.Scz[2] + carsInRace[playerCarIndex].Mad.Scz[3]) / (fix64)4.0F &&
                fix64.Abs((fix64)carsInRace[playerCarIndex].CarRef.Position.X - (fix64)nextCheckpoint.Position.X) < 700 &&
                fix64.Abs((fix64)carsInRace[playerCarIndex].CarRef.Position.Y - (fix64)nextCheckpoint.Position.Y + 350) < 450)
            {
                currentTimeTrial.RecordSplit(raceTimer.ElapsedMilliseconds);
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
            if (fix64.Abs((fix64)carPos.X - (fix64)nextCheckpoint.Position.X) <
                (fix64)60.0F + fix64.Abs(carsInRace[playerCarIndex].Mad.Scx[0] + carsInRace[playerCarIndex].Mad.Scx[1] + carsInRace[playerCarIndex].Mad.Scx[2] + carsInRace[playerCarIndex].Mad.Scx[3]) / (fix64)4.0F &&
                fix64.Abs((fix64)carPos.Z - (fix64)nextCheckpoint.Position.Z) < 700 &&
                fix64.Abs((fix64)carPos.Y - (fix64)nextCheckpoint.Position.Y + 350) < 450)
            {
                currentTimeTrial.RecordSplit(raceTimer.ElapsedMilliseconds);
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

    private void TimeTrialFinished()
    {
        if (!writtenData)
        {
            writtenData = true;
            if (bestTimeTrial == null || (currentTimeTrial != null && currentTimeTrial.GetSplitDiff(bestTimeTrial, currentTimeTrial.Splits.Count - 1) < 0))
            {
                currentTimeTrial.Save();
            }
        }

        carsInRace[playerCarIndex].Mad.Halted = true;
        carsInRace[playerCarIndex].Drive();
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

    public override void Render()
    {
        _pdBars.Render();

        if (_currentState == TimeTrialState.InProgress)
        {
            G.SetColor(new Color(255, 255, 255));
            G.DrawString($"Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds / 10:D2}", 100, 200);
            G.DrawString($"Lap: {currentLap}/{currentStage.nlaps}", 100, 250);
            G.SetColor(new Color(0, 0, 0));
            G.DrawStringStroke($"Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds / 10:D2}", 100, 200);
            G.DrawStringStroke($"Lap: {currentLap}/{currentStage.nlaps}", 100, 250);

            if ((currentCheckpoint != 0 || currentLap != 1) && bestTimeTrial != null)
            {
                long diff = currentTimeTrial.GetSplitDiff(bestTimeTrial, currentTimeTrial.Splits.Count - 1);
                if (diff > 0)
                {
                    G.SetColor(new Color(255, 128, 128));
                }
                else if (diff < 0)
                {
                    G.SetColor(new Color(128, 255, 128));
                }

                long diffSeconds = Math.Abs(diff / 1000);
                long diffMs = Math.Abs(diff % 1000);

                string fmt = $"{(diff > 0 ? "+" : "-")}{diffSeconds}s {diffMs}ms";

                G.DrawString("This Split: " + fmt, 100, 350);
            }
        }
        else if (_currentState == TimeTrialState.Countdown)
        {
            G.SetColor(new Color(255, 255, 255));
            G.DrawString($"Starting in {_countdownTime}", 400, 300);
            G.SetColor(new Color(0, 0, 0));
            G.DrawStringStroke($"Starting in {_countdownTime}", 400, 300);
        }
        else if (_currentState == TimeTrialState.Finished)
        {
            G.SetColor(new Color(128, 255, 128));
            G.DrawString($"Finished! Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds:D2}", 300, 200);
            G.DrawString($"Press R to restart", 300, 250);
            G.SetColor(new Color(0, 0, 0));
            G.DrawStringStroke($"Finished! Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds:D2}", 300, 200);
            G.DrawStringStroke($"Press R to restart", 300, 250);

            bool newBest = bestTimeTrial == null || (bestTimeTrial != null && currentTimeTrial.GetSplitDiff(bestTimeTrial, currentTimeTrial.Splits.Count - 1) < 0);

            if (bestTimeTrial != null || newBest)
            {
                long bestTimeMs = Math.Min(currentTimeTrial.Splits[^1], bestTimeTrial != null ? bestTimeTrial.Splits[^1] : long.MaxValue);

                TimeSpan t = TimeSpan.FromMilliseconds(bestTimeMs);

                string time = string.Format("{0:D2}:{1:D2}:{2:D2}",
                            t.Minutes,
                            t.Seconds,
                            t.Milliseconds);

                G.SetColor(new Color(128, 255, 128));
                G.DrawString("Best time: " + time, 300, 225);
                G.SetColor(new Color(0, 0, 0));
                G.DrawStringStroke("Best time: " + time, 300, 225);
            }
        }
    }
}