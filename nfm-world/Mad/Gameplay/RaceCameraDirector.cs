using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld.Gameplay;

/// <summary>
/// Owns the race cameras and the active view mode, following the client car
/// each tick. Extracted from the race phase to keep it free of camera plumbing.
/// </summary>
public sealed class RaceCameraDirector
{
    public enum ViewMode
    {
        Follow,
        FollowStatic,
        Around,
        Watch
    }

    private readonly FollowCamera _playerFollowCamera = new();
    private readonly AroundCamera _playerAroundCamera = new();
    private readonly AroundStageCamera _stageAroundCamera = new();

    public ViewMode CurrentViewMode { get; private set; } = ViewMode.Follow;

    public void CycleViewMode()
        => CurrentViewMode = (ViewMode)(((int)CurrentViewMode + 1) % Enum.GetValues<ViewMode>().Length);

    public void Update(PerspectiveCamera camera, BackendCar car)
    {
        switch (CurrentViewMode)
        {
            case ViewMode.Follow:
                _playerFollowCamera.Follow(
                    camera,
                    car,
                    (float)car.CarPhysics.Cxz,
                    car.Control.Lookback,
                    (float)car.CarPhysics.Speed,
                    car.Stats.Swits[2]);
                break;
            case ViewMode.FollowStatic:
                _playerFollowCamera.Follow(
                    camera,
                    car,
                    (float)car.CarPhysics.StaticCameraXz,
                    car.Control.Lookback,
                    (float)car.CarPhysics.Speed,
                    car.Stats.Swits[2]);
                break;
            case ViewMode.Around:
                _playerAroundCamera.Around(camera, car);
                break;
        }
    }
}
