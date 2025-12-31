using System.Collections;
using System.Diagnostics;
using System.Reflection.Metadata;
using Maxine.Extensions;
using nfm_world.mad.collision;
using NFMWorld.DriverInterface;
using NFMWorld.Mad;
using NFMWorld.Mad.gamemodes;
using NFMWorld.Mad.helpers;
using NFMWorld.Mad.UI.Elements;
using NFMWorld.Mad.UI.yoga;
using NFMWorld.Util;
using SoftFloat;
using Stride.Core.Mathematics;
using Color = NFMWorld.Util.Color;

public class TimeTrialGamemode(BaseGamemodeParameters gamemodeParameters, BaseRacePhase baseRacePhase)
    : BaseGamemode(gamemodeParameters, baseRacePhase)
{
    public override event EventHandler<byte[]>? RaceFinished;

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

    private bool writtenData;

    private Stopwatch raceTimer = new Stopwatch();

    // demo playback and recording
    private SavedTimeTrial? bestTimeTrial = null;
    private int tick = 0;
    public static bool PlaybackOnReset = true;
    private SavedTimeTrial currentTimeTrial = null!;

    private PowerDamageBars _pdBars = new PowerDamageBars();

    private static TextBlock _lapText = null!;

    private static TextBlock _timerText = null!;

    private static TextRun _splitsText = null!;

    private Node _lapTimerSplits = new Node()
    {
        Name = "LapTimerSplits",
        FlexDirection = Yoga.YGFlexDirection.YGFlexDirectionColumn,
        AlignItems = Yoga.YGAlign.YGAlignFlexStart,
        JustifyContent = Yoga.YGJustify.YGJustifyCenter,
        Gap = 10,
        Padding = 10,

        Children =
        {
            new Node()
            {
                Name = "LapDisplay",
                FlexDirection = Yoga.YGFlexDirection.YGFlexDirectionRow,
                Children =
                {
                    new TextRun()
                    {
                        Name = "LapIcon",
                        Font = new Font(FontFamily.Adventure, 1, 24),
                        Color = new Color(255, 255, 255),
                        StrokeColor = new Color(0, 0, 0),
                        Text = "Lap: ",
                        Flex = 1
                    },
                    new TextBlock()
                    {
                        Ref = textBlock => _lapText = textBlock,
                        StrokeColor = new Color(0, 0, 0),
                        Name = "LapText",
                        Color = new Color(255, 255, 255),
                        Font = new Font(FontFamily.DroidSans, 1, 24),
                        Flex = 1,
                    }
                }
            },
            new Node()
            {
                Name = "TimeDisplay",
                FlexDirection = Yoga.YGFlexDirection.YGFlexDirectionRow,
                Children =
                {
                    new TextRun()
                    {
                        Name = "TimeIcon",
                        Font = new Font(FontFamily.Adventure, 1, 24),
                        Color = new Color(255, 255, 255),
                        StrokeColor = new Color(0, 0, 0),
                        Text = "Time: ",
                        Flex = 1
                    },
                    new TextBlock()
                    {
                        Ref = textBlock => _timerText = textBlock,
                        StrokeColor = new Color(0, 0, 0),
                        Name = "TimeText",
                        Color = new Color(255, 255, 255),
                        Font = new Font(FontFamily.DroidSans, 1, 24),
                        Flex = 1,
                    }
                }
            },
            new Node()
            {
              Children =
                {
                    new TextRun()
                    {
                        Ref = textBlock => _splitsText = textBlock,
                        Name = "SplitsText",
                        StrokeColor = new Color(0, 0, 0),
                        Color = new Color(255, 255, 255),
                        Font = new Font(FontFamily.DroidSans, 1, 24),
                        Flex = 1,
                    }
                }
            }
        }
    };

    private static TextRun _centerText = null!;
    private Node _centralTextNode = new Node()
    {
        Name = "CentralText",
        AlignItems = Yoga.YGAlign.YGAlignCenter,
        FlexDirection = Yoga.YGFlexDirection.YGFlexDirectionColumn,

        Children =
        {
            new Node()
            {
                AlignItems = Yoga.YGAlign.YGAlignCenter,
                Flex = 1,
                Children = {
                    new TextRun()
                    {
                        Ref = textBlock => _centerText = textBlock,
                        Text = "",
                        Color = new Color(0, 0, 0, 0),
                        Font = new Font(FontFamily.Adventure, 1, 24),
                        Display = Yoga.YGDisplay.YGDisplayNone
                    },
                }
            },

            new Node()
            {
                Flex = 1
            }
        }
    };

    public void SetLapText(int currentLap)
    {
        _lapText.Text = $"{currentLap + 1}/{currentStage.nlaps}";
    }

    public void SetTimeText()
    {
        _timerText.Text = $"{raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds:D3}";
    }

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
        raceTimer.Reset();
        writtenData = false;

        // ghosts
        bestTimeTrial = null;
        tick = 0;

        carsInRace.Clear();
        carsInRace[playerCarIndex] = new InGameCar(0, GameSparker.GetCar(player.CarName).Car!, 0, 0, true);
        carsInRace[playerCarIndex].Mad.PowerUp += _pdBars.EventPowerUp;
        carsInRace[playerCarIndex].currentCheckpoint = 0;
        carsInRace[playerCarIndex].currentLap = 0;

        // ghost
        carsInRace[playerCarIndex + 1] = new InGameCar(carsInRace[playerCarIndex], 0, false);
        carsInRace[playerCarIndex + 1].Sfx.Mute = true;

        SavedTimeTrial? bestTimeDemo = SavedTimeTrial.Load(player.CarName, currentStage.Path);
        if (bestTimeDemo != null && PlaybackOnReset)
        {
            bestTimeTrial = bestTimeDemo;
            carsInRace[playerCarIndex + 1].AlphaOverride = 0.2f;
        }
        else
        {
            carsInRace.RemoveAt(1);
        }

        currentTimeTrial = new SavedTimeTrial(player.CarName, currentStage.Path);

        foreach (var cp in currentStage.checkpoints)
        {
            cp.Glow = false;
        }

        SetTimeText();

        _pdBars.Reset();
        IBackend.Backend.StopAllSounds();

        SetLapText(1);
        _splitsText.Display = Yoga.YGDisplay.YGDisplayNone;

        _currentState = TimeTrialState.Countdown;
    }

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {carsInRace[playerCarIndex].Position.X:0.00}, contoz: {carsInRace[playerCarIndex].Position.Z:0.00}, contoy: {carsInRace[playerCarIndex].Position.Y:0.00}");
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
        SetLapText(carsInRace[playerCarIndex].currentLap);
        SetTimeText();

        _pdBars.SetDamageBarFill(carsInRace[playerCarIndex].Mad.Hitmag, carsInRace[0].Stats.Maxmag);
        _pdBars.UpdateDamageBarColor();
        _pdBars.SetPowerBarFill((float)carsInRace[playerCarIndex].Mad.Power);
        _pdBars.UpdatePowerBarColor();

        if (bestTimeTrial != null)
        {
            carsInRace[playerCarIndex + 1].Control.Decode(bestTimeTrial.GetTick(tick) ?? (false, false, false, false, false));

            carsInRace[playerCarIndex + 1].Drive(baseRacePhase.CurrentStage);
        }

        currentTimeTrial.RecordTick(carsInRace[playerCarIndex]);
        carsInRace[playerCarIndex].Drive(currentStage);

        if (currentStage.checkpoints.Count == 0)
        {
            // lol
            return;
        }
        
        FixHoopHelper.HandleFixHoops(currentStage, carsInRace[playerCarIndex]);
        
        if (CheckPointHelper.HandleCheckPoint(currentStage, carsInRace[playerCarIndex]))
        {
            currentTimeTrial.RecordSplit(raceTimer.ElapsedMilliseconds);
            
            SfxLibrary.checkpoint?.Play();
        }

        if (carsInRace[playerCarIndex].currentCheckpoint == currentStage.checkpoints.Count - 1 && carsInRace[playerCarIndex].currentLap == currentStage.nlaps - 1)
        {
            currentStage.checkpoints[^1].Finish = true;
        }
        else
        {
            currentStage.checkpoints[^1].Finish = false;
        }

        if (carsInRace[playerCarIndex].currentCheckpoint > 0)
        {
            currentStage.checkpoints[carsInRace[playerCarIndex].currentCheckpoint - 1].Glow = false;
        }
        else
        {
            currentStage.checkpoints[^1].Glow = false;
        }

        if (carsInRace[playerCarIndex].currentCheckpoint < currentStage.checkpoints.Count)
        {
            currentStage.checkpoints[carsInRace[playerCarIndex].currentCheckpoint].Glow = true;
        }

        if (carsInRace[playerCarIndex].currentLap >= currentStage.nlaps)
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
            if (bestTimeTrial == null || (currentTimeTrial != null && currentTimeTrial.GetSplitDiff(bestTimeTrial, currentTimeTrial.Splits.SplitTimes.Count - 1) < 0))
            {
                currentTimeTrial.Save();
            }
        }

        carsInRace[playerCarIndex].Mad.Halted = true;
        carsInRace[playerCarIndex].Drive(baseRacePhase.CurrentStage);
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
                _centerText.Display = Yoga.YGDisplay.YGDisplayNone;
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
        _lapTimerSplits.LayoutAndRender(G.Viewport);
        _centralTextNode.LayoutAndRender(G.Viewport);

        if (_currentState == TimeTrialState.InProgress)
        {
            if ((carsInRace[playerCarIndex].currentCheckpoint != 0 || carsInRace[playerCarIndex].currentLap != 0) && bestTimeTrial != null)
            {
                _splitsText.Display = Yoga.YGDisplay.YGDisplayFlex;
                long diff = currentTimeTrial.GetSplitDiff(bestTimeTrial, currentTimeTrial.Splits.SplitTimes.Count - 1);
                if (diff > 0)
                {
                    _splitsText.Color = new Color(255, 128, 128);
                }
                else if (diff < 0)
                {
                    _splitsText.Color = new Color(128, 255, 128);
                }

                long diffSeconds = Math.Abs(diff / 1000);
                long diffMs = Math.Abs(diff % 1000);

                string fmt = $"{(diff > 0 ? "+" : "-")}{diffSeconds}s {diffMs}ms";

                _splitsText.Text = $"This Split: {fmt}";
            }
        }
        else if (_currentState == TimeTrialState.Countdown)
        {
            _centerText.Display = Yoga.YGDisplay.YGDisplayFlex;
            _centerText.Font = new Font(FontFamily.Adventure, 1, 24);
            _centerText.Color = new Color(255, 255, 255);
            _centerText.StrokeColor = new Color(0, 0, 0);
            _centerText.Text = $"Starting in {_countdownTime}";
        }
        else if (_currentState == TimeTrialState.Finished)
        {
            string finalTime = $"{raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds:D3}";
            _centerText.Display = Yoga.YGDisplay.YGDisplayFlex;
            _centerText.Color = new Color(128, 255, 128);
            _centerText.StrokeColor = new Color(0, 0, 0);
            _centerText.Font = new Font(FontFamily.DroidSans, 1, 24);
            _centerText.Text = $"Finished! Time: {finalTime}";

            bool newBest = bestTimeTrial == null || (bestTimeTrial != null && currentTimeTrial.GetSplitDiff(bestTimeTrial, currentTimeTrial.Splits.SplitTimes.Count - 1) < 0);

            if(newBest)
                _centerText.Text += "\nNew best time!";

            if (bestTimeTrial != null || newBest)
            {
                long bestTimeMs = Math.Min(currentTimeTrial.Splits.SplitTimes[^1], bestTimeTrial != null ? bestTimeTrial.Splits.SplitTimes[^1] : long.MaxValue);

                TimeSpan t = TimeSpan.FromMilliseconds(bestTimeMs);

                string time = string.Format("{0:D2}:{1:D2}:{2:D2}",
                            t.Minutes,
                            t.Seconds,
                            t.Milliseconds);

                _centerText.Text += $"\nBest time: {time}";
            }
            
            _centerText.Text += "\nPress R to restart";
        }
    }
}