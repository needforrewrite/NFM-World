using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Files;

namespace NFMWorld.Gameplay;

/// <summary>
/// Plays a saved time trial back from its recorded frames.
/// </summary>
/// <remarks>
/// Frames are playback snapshots, not checkpoints physics can resume from, so nothing here runs
/// the gamemode, drives physics, saves a record or submits anything. The stage and car come from
/// the recording when it carries them, so a changed install cannot silently substitute another.
/// </remarks>
public sealed class ReplayViewerPhase : BaseStageRenderingPhase
{
    /// <summary>The client tick rate the frames were recorded at.</summary>
    private const double TicksPerSecond = 63.0;

    private readonly SavedTimeTrial _replay;
    private readonly ReplayViewState _state = new();
    private readonly RaceCameraDirector _cameraDirector = new();
    private readonly int _totalFrames;
    private readonly BackendCar? _car;

    private int _frame;
    private bool _playing;
    private double _speed = 1;
    private double _accumulator;

    public ReplayViewerPhase(SavedTimeTrial replay) : base()
    {
        _replay = replay;
        _totalFrames = replay.DemoData.Ticks.Count;
        _state.TotalFrames.Value = _totalFrames;
        _state.Speed.Value = _speed;

        StageName = replay.StageName;

        var rad = replay.CarData ?? BackendGameSparker.GetCar(replay.CarName).Rad;
        if (rad is null)
        {
            SetMissing("This recording needs a car that is not installed.");
            return;
        }

        _car = new BackendCar(rad, 0, 0, 0, true);
        CarsInRace.Add(_car);

        try
        {
            if (replay.StageData is { } stageData)
            {
                CurrentStage = new ClientStage(replay.StageName, CarsInRace, Camera, LightCameras, stageData);
                LoadStageMusic();
            }
            else if (VFS.FileExists($"data/stages/{replay.StageName}.txt")
                     || VFS.FileExists($"data/stages/{replay.StageName}.radpack"))
            {
                LoadStage(replay.StageName);
            }
            else
            {
                SetMissing($"This recording needs the stage '{replay.StageName}', which is not installed.");
                return;
            }
        }
        catch (Exception ex)
        {
            CurrentStage?.Dispose();
            CurrentStage = null!;
            SetMissing($"This recording's stage could not be loaded: {ex.Message}");
            return;
        }

        if (_totalFrames == 0)
        {
            SetMissing("This recording has no frames.");
            return;
        }

        // The car was added before the stage existed, so ask the stage to build its visual now.
        CurrentStage.SetCars(CarsInRace);
        ApplyFrame(0);
        _playing = true;
        _state.Playing.Value = true;
    }

    private void SetMissing(string message)
    {
        _state.MissingContent.Value = true;
        _state.Info.Value = message;
    }

    public override Node? BuildUi() => ReplayView.Build(
        _state,
        new ReplayView.Actions(
            TogglePlay: TogglePlay,
            Restart: Restart,
            Step: Step,
            Seek: Seek,
            SetSpeed: SetSpeed,
            CycleCamera: () => _cameraDirector.CycleViewMode(),
            Back: GameSparker.PopPhase
        )
    );

    private void TogglePlay()
    {
        if (_state.MissingContent.Value)
            return;

        if (!_playing && _frame >= _totalFrames - 1)
            _frame = 0;

        _playing = !_playing;
        _accumulator = 0;
        _state.Playing.Value = _playing;
        if (_playing)
            ApplyFrame(_frame);
    }

    private void Restart()
    {
        if (_state.MissingContent.Value)
            return;

        _frame = 0;
        _accumulator = 0;
        _playing = true;
        _state.Playing.Value = true;
        ApplyFrame(0);
    }

    private void Step(int delta)
    {
        if (_state.MissingContent.Value)
            return;

        _playing = false;
        _state.Playing.Value = false;
        Seek(_frame + delta);
    }

    private void Seek(double frame)
    {
        if (_state.MissingContent.Value)
            return;

        _frame = (int)Math.Clamp(frame, 0, _totalFrames - 1);
        _accumulator = 0;
        ApplyFrame(_frame);

        if (_frame >= _totalFrames - 1)
        {
            _playing = false;
            _state.Playing.Value = false;
        }

        // A seek jumps the camera; swallowing the smoothing from the old frame keeps it from
        // dragging the view across the course for the next second.
        CurrentStage?.OnBeforeGameTick();
    }

    private void SetSpeed(double speed)
    {
        _speed = speed;
        _state.Speed.Value = speed;
    }

    private void ApplyFrame(int index)
    {
        if (_car is null || index < 0 || index >= _totalFrames)
            return;

        _replay.DemoData.GetEntry(index).ApplyToCar(_car);
        _frame = index;
        _state.Frame.Value = index;
        _state.TimeText.Value = FormatTime(index / TicksPerSecond);
    }

    private static string FormatTime(double seconds)
    {
        var minutes = (int)(seconds / 60);
        var rest = seconds - minutes * 60;
        return $"{minutes}:{rest:00.000}";
    }

    public override void GameTick()
    {
        if (_state.MissingContent.Value)
            return;

        if (_playing && _totalFrames > 0)
        {
            _accumulator += _speed;
            while (_accumulator >= 1)
            {
                _accumulator -= 1;
                if (_frame >= _totalFrames - 1)
                {
                    _playing = false;
                    _accumulator = 0;
                    _state.Playing.Value = false;
                    break;
                }

                ApplyFrame(_frame + 1);
            }
        }

        if (_car is not null)
            _cameraDirector.Update(Camera, _car);

        base.GameTick();
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);
        if (imguiWantsKeyboard || _state.MissingContent.Value)
            return;

        switch (key)
        {
            case Key.Space:
                TogglePlay();
                break;
            case Key.Home:
                Seek(0);
                break;
            case Key.End:
                Seek(_totalFrames - 1);
                break;
            case Key.Left:
                Seek(_frame - 10);
                break;
            case Key.Right:
                Seek(_frame + 10);
                break;
            case Key.Oemcomma:
                Step(-1);
                break;
            case Key.OemPeriod:
                Step(1);
                break;
            case Key.C:
                _cameraDirector.CycleViewMode();
                break;
            case Key.D1:
                SetSpeed(1);
                break;
            case Key.D2:
                SetSpeed(2);
                break;
            case Key.D3:
                SetSpeed(4);
                break;
            case Key.D4:
                SetSpeed(0.5);
                break;
            case Key.Escape:
                GameSparker.PopPhase();
                break;
        }
    }
}
