using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary;

namespace NFMWorld.Gameplay;

public class GaragePhase : BaseStageRenderingPhase
{
    private readonly GarageController _garage;
    private readonly GarageCameraControls _garageCamera = new();
    private readonly MenuCameraDirector _cameraDirector = new();
    private bool _cameraInitialized;

    public event EventHandler<Rad3d>? CarSelected;
    public event EventHandler? CarSelectionCancelled;

    public GaragePhase(string? stageName = null) : this(null, stageName)
    {
    }

    public GaragePhase(Rad3d? currentCar, string? stageName = null) : base(stageName ?? MenuHistory.Stage)
    {
        if (currentCar != null)
            CarsInRace.Add(new BackendCar(currentCar, 0, 0, 0, true));
        _garage = new GarageController(CarsInRace, CarsInRace.FirstOrDefault());
        _garage.CarSelected += (_, car) => CarSelected?.Invoke(this, car);
        _garage.CarSelectionCancelled += (_, args) => CarSelectionCancelled?.Invoke(this, args);
    }

    public override Node BuildUi() => _garage.BuildUi();

    public override void Enter()
    {
        base.Enter();
        _garage.Enter();
        if (_garage.CurrentCar is { } car && !_cameraInitialized)
        {
            _cameraDirector.ShowCar(Camera, car, 0, true, 1);
            _cameraInitialized = true;
        }
        FovOverride = 53;
    }

    public override void GameTick()
    {
        _garageCamera.Tick(1f / Physics.TargetTps);
        if (_garage.CurrentCar is { } car)
            _cameraDirector.ShowCar(Camera, car, 0, garageBlend: 1,
                garageAngle: _garageCamera.Angle, garageHeight: _garageCamera.Height);
        base.GameTick();
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button,
        MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Press(x, y, imguiWantsMouse, button);
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseMoved(x, y, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Move(x, y, imguiWantsMouse, buttons);
    }

    public override void MouseReleased(int x, int y, bool imguiWantsMouse, MouseButton button,
        MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Release(button);
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
        => _garage.KeyPressed(key, imguiWantsKeyboard, keys);
}
