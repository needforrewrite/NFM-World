using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Gameplay;

public class GaragePhase : BaseStageRenderingPhase
{
    private readonly GarageController _garage;

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
        Camera.LookAt = new Vector3(0, 250, 400);
        Camera.Position = new Vector3(-750, 50, 750);
        FovOverride = 53;
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
        => _garage.KeyPressed(key, imguiWantsKeyboard, keys);
}
