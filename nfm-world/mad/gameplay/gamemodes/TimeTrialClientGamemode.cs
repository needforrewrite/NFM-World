using System.Diagnostics;
using NFMWorld.DriverInterface;
using NFMWorld.Sfx;
using NFMWorld.UI.Hud;
using NFMWorld.UI.Yoga;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;

namespace NFMWorld.Gameplay.Gamemodes;

public class TimeTrialClientGamemode(BaseGamemodeParameters gamemodeParameters, BaseRacePhase raceValues)
    : TimeTrialGamemode(gamemodeParameters, raceValues), IClientGamemode
{
    private Stopwatch _raceTimer = new Stopwatch();

    private bool _writtenData;
    
    // demo playback and recording
    private SavedTimeTrial? _bestTimeTrial = null;
    private int _tick = 0;
    public static bool PlaybackOnReset = true;
    private SavedTimeTrial currentTimeTrial = null!;
    private long _lastCheckpointSplitDiff = 0;
    private long _lastLapSplitDiff = 0;
    private long _lastLapTime = 0;

    private PowerDamageBars _pdBars = new PowerDamageBars();

    private TTLapTimerSplitsView _lapTimerSplits = new TTLapTimerSplitsView();

    private CentralTextView _centralTextNode = new CentralTextView();

    public void SetLapText(int currentLap)
    {
        _lapTimerSplits.LapText.Text = $"{currentLap + 1}/{currentStage.nlaps}";
    }

    public void SetTimeText()
    {
        _lapTimerSplits.TimeText.Text = $"{_raceTimer.Elapsed.Minutes:D2}:{_raceTimer.Elapsed.Seconds:D2}.{_raceTimer.Elapsed.Milliseconds:D3}";
    }

    public override void Enter()
    {
        base.Enter();

        Reset();
    }

    public override void Exit()
    {
        // Cleanup for Time Trial mode
        base.Exit();
    }

    public override void Reset()
    {
        base.Reset();

        _raceTimer.Reset();
        _writtenData = false;

        // ghosts
        _bestTimeTrial = null;
        _tick = 0;

        carsInRace[playerCarIndex].Mad.PowerUp += _pdBars.EventPowerUp;

        // ghost
        SavedTimeTrial? bestTimeDemo = SavedTimeTrial.Load(player.CarName, currentStage.Path);
        if (bestTimeDemo != null && PlaybackOnReset)
        {
            _bestTimeTrial = bestTimeDemo;
            carsInRace[playerCarIndex + 1] = bestTimeDemo.CarData != null
                ? new BackendCar(bestTimeDemo.CarData, 0, 0, 0, false)
                : new BackendCar(carsInRace[playerCarIndex], 0, false);
            raceValues.GetClientCar(playerCarIndex + 1)!.Sfx!.Mute = true;
            raceValues.GetClientCar(playerCarIndex + 1).AlphaOverride = 0.2f;
            carsInRace[playerCarIndex + 1].currentLap = 0;
        }

        currentTimeTrial = new SavedTimeTrial(player.CarName, currentStage.Path, currentStage.stageLoader, carsInRace[playerCarIndex].Rad);
        
        raceValues.clientStageRenderer.ResetCheckpointGlow();

        SetTimeText();

        _pdBars.Reset();
        IBackend.Backend.StopAllSounds();

        SetLapText(0);
        _lapTimerSplits.CheckpointSplitsText.Display = YgDisplay.None;

        _lastLapSplitDiff = 0;
        _lastCheckpointSplitDiff = 0;
        _lapTimerSplits.LapSplitsText.Display = YgDisplay.None;
        _lastLapTime = 0;
        _lapTimerSplits.LapTimeText.Text = "";
        _lapTimerSplits.LapTimeText.Display = YgDisplay.None;
    }

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {carsInRace[playerCarIndex].Position.X:0.00}, contoz: {carsInRace[playerCarIndex].Position.Z:0.00}, contoy: {carsInRace[playerCarIndex].Position.Y:0.00}");
        base.GameTick();
    }

    protected override void TimeTrialInRace()
    {
        SetLapText(carsInRace[playerCarIndex].currentLap);
        SetTimeText();

        _pdBars.SetDamageBarFill(carsInRace[playerCarIndex].Mad.Hitmag, carsInRace[0].Stats.Maxmag);
        _pdBars.UpdateDamageBarColor();
        _pdBars.SetPowerBarFill((float)carsInRace[playerCarIndex].Mad.Power);
        _pdBars.UpdatePowerBarColor();

        if (_bestTimeTrial != null)
        {
            carsInRace[playerCarIndex + 1].Control.Decode(_bestTimeTrial.GetTick(_tick) ?? (false, false, false, false, false));

            carsInRace[playerCarIndex + 1].Drive(raceValues.CurrentStage);
        }

        currentTimeTrial.RecordTick(carsInRace[playerCarIndex]);
        
        var lastCurrentCheckpoint = carsInRace[playerCarIndex].currentCheckpoint;
        var lastLap = carsInRace[playerCarIndex].currentLap;
        base.TimeTrialInRace();

        if (carsInRace[playerCarIndex].currentCheckpoint != lastCurrentCheckpoint)
        {
            if (_bestTimeTrial != null && currentTimeTrial.Splits.SplitTimes.Count > 0)
            {
                _lastCheckpointSplitDiff = currentTimeTrial.GetSplitDiff(_bestTimeTrial, currentTimeTrial.Splits.SplitTimes.Count - 1);
            }

            long currentLapSplitDiff = 0;
            if (lastLap > 0 && _bestTimeTrial != null)
            {
                currentLapSplitDiff = currentTimeTrial.GetLapTime(currentStage.checkpoints.Count, lastLap) - _bestTimeTrial.GetLapTime(currentStage.checkpoints.Count, lastLap - 1);
            }

            currentTimeTrial.RecordSplit(_raceTimer.ElapsedMilliseconds);

            if (lastLap != carsInRace[playerCarIndex].currentLap)
            {
                // lap changed
                _lastLapSplitDiff = currentLapSplitDiff;
                _lastLapTime = currentTimeTrial.GetLapTime(currentStage.checkpoints.Count, lastLap);
            }

            SfxLibrary.checkpoint?.Play();
        }

        raceValues.clientStageRenderer.UpdateCheckpointGlow(
            carsInRace[playerCarIndex].currentCheckpoint,
            carsInRace[playerCarIndex].currentCheckpoint == currentStage.checkpoints.Count - 1 && carsInRace[playerCarIndex].currentLap == currentStage.nlaps - 1
        );

        if (carsInRace[playerCarIndex].currentLap >= currentStage.nlaps)
        {
            _raceTimer.Stop();
        }

        _tick++;
    }

    protected override void TimeTrialFinished()
    {
        base.TimeTrialFinished();

        if (!_writtenData)
        {
            _writtenData = true;
            if (_bestTimeTrial == null || (currentTimeTrial != null && currentTimeTrial.GetSplitDiff(_bestTimeTrial, currentTimeTrial.Splits.SplitTimes.Count - 1) < 0))
            {
                currentTimeTrial.Save();
            }
        }
    }

    protected override void CountdownTick()
    {
        var innerCountdownTicks = _innerCountdownTicks;
        var countdownTime = _countdownTime;
        base.CountdownTick();
        
        if (innerCountdownTicks < _innerCountdownTicks) // innercountdownticks reset
        {
            SfxLibrary.countdown[_countdownTime].Play();
            if (_countdownTime <= 0)
            {
                _currentState = TimeTrialState.InProgress;
                _centralTextNode.CenterText.Display = YgDisplay.None;
                _raceTimer.Start();
            }
        }
    }

    public void KeyPressed(Keys key)
    {
        // Handle key presses specific to Time Trial mode
        if (key == Keys.R)
        {
            Reset();
        }
    }

    public void KeyReleased(Keys key)
    {
        // Handle key releases specific to Time Trial mode
    }

    private void RenderInfo()
    {
        if ((carsInRace[playerCarIndex].currentCheckpoint != 0 || carsInRace[playerCarIndex].currentLap != 0) && _bestTimeTrial != null)
        {
            _lapTimerSplits.CheckpointSplitsText.Display = YgDisplay.Flex;
            long diff = currentTimeTrial.GetSplitDiff(_bestTimeTrial, currentTimeTrial.Splits.SplitTimes.Count - 1);
            _lapTimerSplits.CheckpointSplitsText.Color = diff > 0 ? new Color(255, 128, 128) : new Color(128, 255, 128);

            long lastSplitChange = diff - _lastCheckpointSplitDiff;
            string lastSplitFmt = FormatTimeMs(lastSplitChange, true);

            string thisDiffFmt = FormatTimeMs(diff, true);
            _lapTimerSplits.CheckpointSplitsText.Text = $"CHK Diff: {thisDiffFmt} ({lastSplitFmt})";
        }
        else
        {
            _lapTimerSplits.CheckpointSplitsText.Display = YgDisplay.None;
        }

        if (carsInRace[playerCarIndex].currentLap > 0 && _bestTimeTrial != null)
        {
            _lapTimerSplits.LapSplitsText.Display = YgDisplay.Flex;
            long lapTime = currentTimeTrial.GetLapTime(currentStage.checkpoints.Count, carsInRace[playerCarIndex].currentLap - 1);
            long bestLapTime = _bestTimeTrial.GetLapTime(currentStage.checkpoints.Count, carsInRace[playerCarIndex].currentLap - 1);
            long lapDiff = lapTime - bestLapTime;
            _lapTimerSplits.LapSplitsText.Color = lapDiff > 0 ? new Color(255, 128, 128) : new Color(128, 255, 128);

            long lastSplitChange = lapDiff - _lastLapSplitDiff;
            string lastLapSplitFmt = FormatTimeMs(lastSplitChange, true);

            string lapDiffFmt = FormatTimeMs(lapDiff, true);

            _lapTimerSplits.LapSplitsText.Text = $"Lap Diff: {lapDiffFmt} ({lastLapSplitFmt})";
        }
        else
        {
            _lapTimerSplits.LapSplitsText.Display = YgDisplay.None;
        }

        if(_lastLapTime > 0)
        {
            _lapTimerSplits.LapTimeText.Display = YgDisplay.Flex;
            _lapTimerSplits.LapTimeText.Text = $"Lap Time: {FormatTimeMs(_lastLapTime, false)}";
        }
    }

    public void Render()
    {
        _pdBars.LayoutAndRender(G.Viewport);
        _lapTimerSplits.LayoutAndRender(G.Viewport);
        _centralTextNode.LayoutAndRender(G.Viewport);

        if (_currentState == TimeTrialState.InProgress)
        {
            RenderInfo();
        }
        else if (_currentState == TimeTrialState.Countdown)
        {
            RenderInfo();

            _centralTextNode.CenterText.Display = YgDisplay.Flex;
            _centralTextNode.CenterText.Font = new Font(FontFamily.Adventure, FontStyle.Bold, 24);
            _centralTextNode.CenterText.Color = new Color(255, 255, 255);
            _centralTextNode.CenterText.StrokeColor = new Color(0, 0, 0);
            _centralTextNode.CenterText.Text = $"Starting in {_countdownTime}";
        }
        else if (_currentState == TimeTrialState.Finished)
        {
            RenderInfo();

            string finalTime = $"{_raceTimer.Elapsed.Minutes:D2}:{_raceTimer.Elapsed.Seconds:D2}.{_raceTimer.Elapsed.Milliseconds:D3}";
            _centralTextNode.CenterText.Display = YgDisplay.Flex;
            _centralTextNode.CenterText.Color = new Color(128, 255, 128);
            _centralTextNode.CenterText.StrokeColor = new Color(0, 0, 0);
            _centralTextNode.CenterText.Font = new Font(FontFamily.DroidSans, FontStyle.Bold, 24);
            _centralTextNode.CenterText.Text = $"Finished! Time: {finalTime}";

            bool newBest = _bestTimeTrial == null || (_bestTimeTrial != null && currentTimeTrial.GetSplitDiff(_bestTimeTrial, currentTimeTrial.Splits.SplitTimes.Count - 1) < 0);

            if (newBest)
                _centralTextNode.CenterText.Text += "\nNew best time!";

            if (_bestTimeTrial != null || newBest)
            {
                long bestTimeMs = Math.Min(currentTimeTrial.Splits.SplitTimes[^1], _bestTimeTrial != null ? _bestTimeTrial.Splits.SplitTimes[^1] : long.MaxValue);

                TimeSpan t = TimeSpan.FromMilliseconds(bestTimeMs);

                string time = string.Format("{0:D2}:{1:D2}:{2:D2}",
                    t.Minutes,
                    t.Seconds,
                    t.Milliseconds);

                _centralTextNode.CenterText.Text += $"\nBest time: {time}";
            }

            _centralTextNode.CenterText.Text += "\nPress R to restart";
        }
    }

    private string FormatTimeMs(long time, bool plusMinus)
    {
        long timeMins = Math.Abs(time / (1000 * 60));
        string timeMinsFmt = $"{timeMins:D2}";
        long timeSecs = Math.Abs(time / 1000 % 60);
        long timeMs = Math.Abs(time % 1000);
        string fmt = $"{(plusMinus ? ((time > 0) ? "+" : "-") : "")}{(timeMins > 0 ? timeMinsFmt + ":" : "")}{timeSecs:D2}.{timeMs:D3}";
        return fmt;
    }
}