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
        /// <summary>Behind the car, aimed at whatever the guidance arrow points to.</summary>
        CarCam,
        /// <summary>Stationary trackside camera that turns to watch the car pass.</summary>
        Watch,
        /// <summary>Free-flying camera, steered with the driving keys.</summary>
        Freelook
    }

    private readonly FollowCamera _playerFollowCamera = new();
    private readonly AroundCamera _playerAroundCamera = new();
    private readonly AroundStageCamera _stageAroundCamera = new();
    private readonly WatchCamera _watchCamera = new();
    private readonly FreelookCamera _freelookCamera = new();

    public ViewMode CurrentViewMode { get; private set; } = ViewMode.Follow;

    /// <summary>Navigation keys held, for <see cref="ViewMode.Freelook"/>.</summary>
    public FreelookInput Navigation { get; set; }

    /// <summary>Where the guidance arrow points, for <see cref="ViewMode.CarCam"/>.</summary>
    public Vector3? ArrowTarget { get; set; }

    public void CycleViewMode()
    {
        CurrentViewMode = (ViewMode)(((int)CurrentViewMode + 1) % Enum.GetValues<ViewMode>().Length);
        // Entering the car cam should start already facing the arrow, not sweep round to it.
        _playerFollowCamera.ResetAim();
        _watchCamera.Reset();
        _freelookCamera.Reset();
    }

    public void Update(PerspectiveCamera camera, BackendCar car)
    {
        switch (CurrentViewMode)
        {
            // No arrow target (stage finished, nothing to aim at): the plain follow view is the
            // same seat, just looking down the car instead of at the objective.
            case ViewMode.CarCam when ArrowTarget is { } target:
                _playerFollowCamera.FollowTarget(camera, car, (float)car.CarPhysics.Cxz,
                    (float)car.CarPhysics.Speed, car.Stats.Swits[2], target, interpolate: true);
                break;
            case ViewMode.CarCam:
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
            case ViewMode.Watch:
                _watchCamera.Watch(camera, car, (float)car.CarPhysics.Mxz);
                break;
            case ViewMode.Freelook:
                _freelookCamera.Fly(camera, Navigation);
                break;
        }
    }
}
