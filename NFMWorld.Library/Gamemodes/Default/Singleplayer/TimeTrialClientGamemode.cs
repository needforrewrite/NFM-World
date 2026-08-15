using System.Diagnostics;
using System.Globalization;
using Microsoft.Xna.Framework;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sfx;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Client-side time trial gamemode. Handles ghost replay, recording, HUD,
/// split tracking, and saving best times. Physics and state machine are in
/// the abstract base <see cref="TimeTrialGamemode"/>.
/// </summary>
public class TimeTrialClientGamemode1(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData)
    : TimeTrialGamemode(gamemodeParameters, gamemodeData)
{
    private Stopwatch _raceTimer = new();
    private bool _writtenData;

    // Ghost playback and recording
    private SavedTimeTrial? _bestTimeTrial;
    private int _tick;
    public static bool PlaybackOnReset = true;
    private SavedTimeTrial? _currentTimeTrial;
    private long _lastCheckpointSplitDiff;
    private long _lastLapSplitDiff;
    private long _lastLapTime;
    private ushort _lastCurrentCheckpoint;
    private byte _lastLap;

    // ── Hooks ──────────────────────────────────────────────────────

    protected override void OnResetComplete()
    {
        _raceTimer.Reset();
        _writtenData = false;
        _bestTimeTrial = null;
        _tick = 0;

        // Load ghost
        SavedTimeTrial? bestTimeDemo = SavedTimeTrial.Load(Players[PlayerCarIndex].Parameters.CarName, CurrentStage.Path);
        if (bestTimeDemo != null && PlaybackOnReset)
        {
            _bestTimeTrial = bestTimeDemo;
            var ghostCar = bestTimeDemo.CarData != null
                ? new BackendCar(bestTimeDemo.CarData, PlayerCarIndex, 0, 0, false)
                : new BackendCar(Players[PlayerCarIndex].Car!, PlayerCarIndex, false);
            ghostCar.CurrentLap = 0;

            var ghostPlayer = new ClientSidePlayer(Players[PlayerCarIndex].Parameters, GhostCarIndex, isFake: true)
            {
                Car = ghostCar
            };
            Players.Add(ghostPlayer);

            gamemodeData.ClientCallbacks.GetClientCarCallbacks(GhostCarIndex).AlphaOverride = 0.2f;
        }

        _currentTimeTrial = new SavedTimeTrial(Players[PlayerCarIndex].Parameters.CarName, CurrentStage.Path,
            CurrentStage.stageLoader, Players[PlayerCarIndex].Car!.Rad);

        gamemodeData.ClientCallbacks.ResetCheckpointGlow();
        SetTimeText();
        HudState = new HudStateData { Lap = 1, TotalLaps = CurrentStage.nlaps };
        IBackend.Backend.StopAllSounds();

        _lastLapSplitDiff = 0;
        _lastCheckpointSplitDiff = 0;
        _lastLapTime = 0;
    }

    protected override void OnGameTickComplete()
    {
        FrameTrace.AddMessage(
            $"contox: {Players[PlayerCarIndex].Car?.Position.X:0.00}, contoz: {Players[PlayerCarIndex].Car?.Position.Z:0.00}, contoy: {Players[PlayerCarIndex].Car?.Position.Y:0.00}");

        switch (_currentState)
        {
            case TimeTrialState.InProgress:
                HudState.CountdownTimer = 0;
                RenderInfo();
                break;
            case TimeTrialState.Countdown:
                RenderInfo();
                HudState.CountdownTimer = _countdownTime;
                break;
            case TimeTrialState.Finished:
                RenderInfo();
                RenderFinishedText();
                break;
        }
    }

    protected override void OnBeforePhysics()
    {
        SetTimeText();

        var playerCar = Players[PlayerCarIndex].Car!;
        base.UpdateHudAndSounds(playerCar);

        // Replay ghost
        if (_bestTimeTrial != null && Players[GhostCarIndex].Car is { } ghostCar)
        {
            ghostCar.Control.Decode(
                _bestTimeTrial.GetTick(_tick) ?? (false, false, false, false, false));
            ghostCar.Drive(gamemodeData.CurrentStage);
        }

        _currentTimeTrial?.RecordTick(playerCar);

        _lastCurrentCheckpoint = playerCar.CurrentCheckpoint;
        _lastLap = playerCar.CurrentLap;
    }

    protected override void OnAfterPhysics()
    {
        var car = Players[PlayerCarIndex].Car!;

        if (car.CurrentCheckpoint != _lastCurrentCheckpoint)
        {
            if (_bestTimeTrial != null && _currentTimeTrial is { Splits.SplitTimes.Count: > PlayerCarIndex })
                _lastCheckpointSplitDiff = _currentTimeTrial.GetSplitDiff(_bestTimeTrial,
                    _currentTimeTrial.Splits.SplitTimes.Count - 1);

            long currentLapSplitDiff = 0;
            if (_lastLap > 0 && _bestTimeTrial != null && _currentTimeTrial != null)
                currentLapSplitDiff = _currentTimeTrial.GetLapTime(CurrentStage.checkpoints.Count, _lastLap) -
                                      _bestTimeTrial.GetLapTime(CurrentStage.checkpoints.Count, _lastLap - 1);

            _currentTimeTrial?.RecordSplit(_raceTimer.ElapsedMilliseconds);

            if (_lastLap != car.CurrentLap)
            {
                _lastLapSplitDiff = currentLapSplitDiff;
                _lastLapTime = _currentTimeTrial?.GetLapTime(CurrentStage.checkpoints.Count, _lastLap) ?? 0;
            }
        }

        if (car.CurrentLap >= CurrentStage.nlaps)
            _raceTimer.Stop();

        _tick++;
    }

    protected override void OnFinishedComplete()
    {
        if (!_writtenData)
        {
            _writtenData = true;
            if (_bestTimeTrial == null || (_currentTimeTrial != null &&
                _currentTimeTrial.GetSplitDiff(_bestTimeTrial,
                    _currentTimeTrial.Splits.SplitTimes.Count - 1) < 0))
            {
                _currentTimeTrial?.Save();
            }
        }
    }

    protected override void OnCountdownTickComplete()
    {
        base.UpdateCountdown(_countdownTime);
        if (_countdownTime <= 0)
            _raceTimer.Start();
    }

    // ── Rendering ─────────────────────────────────────────────────

    public void SetTimeText()
    {
        HudState.LapTime = (int)_raceTimer.Elapsed.TotalMilliseconds;
    }

    private void RenderFinishedText()
    {
        string finalTime =
            $"{_raceTimer.Elapsed.Minutes:D2}:{_raceTimer.Elapsed.Seconds:D2}.{_raceTimer.Elapsed.Milliseconds:D3}";
        string centerText = $"Finished! Time: {finalTime}";

        bool newBest = _bestTimeTrial == null || (_bestTimeTrial != null && _currentTimeTrial != null &&
            _currentTimeTrial.GetSplitDiff(_bestTimeTrial,
                _currentTimeTrial.Splits.SplitTimes.Count - 1) < 0);

        if (newBest)
            centerText += "\nNew best time!";

        if (_bestTimeTrial != null || newBest)
        {
            long bestTimeMs = Math.Min(
                _currentTimeTrial != null ? _currentTimeTrial.Splits.SplitTimes[^1] : long.MaxValue,
                _bestTimeTrial != null ? _bestTimeTrial.Splits.SplitTimes[^1] : long.MaxValue);

            TimeSpan t = TimeSpan.FromMilliseconds(bestTimeMs);
            var time = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}",
                t.Minutes, t.Seconds, t.Milliseconds);
            centerText += $"\nBest time: {time}";
        }

        centerText += "\nPress R to restart";
        HudState.StateText = centerText;
        HudState.StateTextEndsAt = null;
    }

    private void RenderInfo()
    {
        var car = Players[PlayerCarIndex].Car;
        if (car is not null && (car.CurrentCheckpoint != 0 || car.CurrentLap != 0) &&
            _bestTimeTrial != null && _currentTimeTrial != null)
        {
            long diff = _currentTimeTrial.GetSplitDiff(_bestTimeTrial,
                _currentTimeTrial.Splits.SplitTimes.Count - 1);
            long lastSplitChange = diff - _lastCheckpointSplitDiff;

            HudState.ChkDiffMs = (int)diff;
            HudState.LastChkDiffMs = (int)lastSplitChange;
        }
        else
        {
            HudState.ChkDiffMs = 0;
            HudState.LastChkDiffMs = 0;
        }
    }

    // ── Input ─────────────────────────────────────────────────────

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);
        if (key == Key.R)
            Reset();
    }
}
