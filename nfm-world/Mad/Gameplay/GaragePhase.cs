using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary;
using NFMWorld.UI.ViewModels;

namespace NFMWorld.Gameplay;

public class GaragePhase : BaseStageRenderingPhase
{
    private readonly GarageController _garage;
    private readonly GarageCameraControls _garageCamera = new();
    private readonly MenuCameraDirector _cameraDirector = new();
    private bool _cameraInitialized;
    private readonly bool _quickSelect;

    public event EventHandler<Rad3d>? CarSelected;
    public event EventHandler? CarSelectionCancelled;

    public GaragePhase(string? stageName = null) : this(null, stageName)
    {
    }

    internal GaragePhase(StageSelectPhase source, bool quickSelect = false)
        : this(quickSelect ? MenuHistory.Car : null, source.StageName, source, quickSelect)
    {
        try
        {
            LoadStageMusic();
        }
        catch
        {
            source.TakeStageFrom(this);
            source.CurrentStage!.FadeFromOverride = 9999999;
            throw;
        }
    }

    public GaragePhase(
        Rad3d? currentCar,
        string? stageName = null,
        BaseStageRenderingPhase? stageSource = null,
        bool quickSelect = false
    ) : base(stageName ?? MenuHistory.Stage, stageSource)
    {
        _quickSelect = quickSelect;
        if (currentCar != null)
            CarsInRace.Add(new BackendCar(currentCar, 0, 0, 0, true));
        _garage = new GarageController(
            CarsInRace,
            CarsInRace.FirstOrDefault(),
            car => CurrentStage?.GetCarVisual(car),
            quickSelect
        );
        _garage.CarSelected += (_, car) => CarSelected?.Invoke(this, car);
        _garage.CarSelectionCancelled += (_, args) => CarSelectionCancelled?.Invoke(this, args);
    }

    public override Node BuildUi() => _garage.BuildUi();

    protected override void OnStageLoaded()
    {
        PositionPreviewCar();
        CurrentStage?.SetCars(CarsInRace);
    }

    public override void Enter()
    {
        base.Enter();
        _garage.Enter();
        PositionPreviewCar();
        if (_garage.CurrentCar is { } car && !_cameraInitialized)
        {
            _cameraDirector.ShowCar(
                Camera,
                car,
                0,
                true,
                1,
                garageFraming: PreviewFraming,
                garageVerticalFraming: PreviewVerticalFraming
            );
            _cameraInitialized = true;
        }

        FovOverride = 53;
    }

    public override void GameTick()
    {
        _garageCamera.Tick(1f / Physics.TargetTps);
        PositionPreviewCar();
        if (_garage.CurrentCar is { } car)
            _cameraDirector.ShowCar(
                Camera,
                car,
                0,
                garageBlend: 1,
                garageAngle: _garageCamera.Angle,
                garageHeight: _garageCamera.Height,
                garageFraming: PreviewFraming,
                garageVerticalFraming: PreviewVerticalFraming
            );
        base.GameTick();
    }

    private bool CompactPreview => _quickSelect && UiState.Garage.Page.Value != GaragePage.Colors;
    private float PreviewFraming => CompactPreview ? 0.2f : MenuCameraDirector.GarageFraming;
    private float PreviewVerticalFraming => CompactPreview ? 0.25f : 0;

    private void PositionPreviewCar()
    {
        if (!_quickSelect || _garage.CurrentCar is not { } car || CurrentStage is null) return;
        var start = CurrentStage.Backend.GetStartTransform(0, 1);
        car.Position = new(start.Position.X, car.Position.Y, start.Position.Z);
        car.Rotation = start.Rotation;
    }

    public override void MousePressed(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Press(x, y, imguiWantsMouse, button);
    }

    public override void MouseMoved(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseMoved(x, y, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Move(x, y, imguiWantsMouse, buttons);
    }

    public override void MouseReleased(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Release(button);
        if (button == MouseButton.Primary) _garage.FinishPaintInteraction();
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
        => _garage.KeyPressed(key, imguiWantsKeyboard, keys);

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);

        // One card image per frame, into an offscreen target; the backbuffer is restored.
        _garage.RenderThumbnails(cb);
    }

    public override void KeyRepeated(Key key, bool imguiWantsKeyboard, in Keys keys)
        => _garage.KeyRepeated(key, imguiWantsKeyboard, keys);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _garage.End();

        base.Dispose(disposing);
    }
}
