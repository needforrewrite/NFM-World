using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.UI.Cef;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public class GaragePhase : BaseStageRenderingPhase
{
    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Returns the car that was selected.
    /// </summary>
    public event EventHandler<Rad3d>? CarSelected;

    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Indicates no selection was made; retain existing car, if any.
    /// </summary>
    public event EventHandler? CarSelectionCancelled;

    private int _selectedCarIdx = 0;

    private Collection _currentCollection = Collection.NFMM;
    private UnlimitedArray<Rad3d> _cars = BackendGameSparker.cars[Collection.NFMM];
    private BackendCar? _backendCar;

    private readonly GarageBridge _bridge = new();

    private bool _pushedCollections = false;

    public GaragePhase(GraphicsDevice graphicsDevice, string? stageName = null) : base(graphicsDevice, stageName ?? GameSparker.GetAvailableStages().Shuffle().First())
    {
        InitBridge();
    }

    public GaragePhase(GraphicsDevice graphicsDevice, Rad3d currentCar, string? stageName = null) : this(graphicsDevice, stageName)
    {
        _selectedCarIdx = _cars.FindIndex(c =>
        {
            ArgumentNullException.ThrowIfNull(c);
            return c.FileName == currentCar.FileName;
        });

        if (_selectedCarIdx == -1) _selectedCarIdx = 0;

        InitBridge();
    }

    /// <summary>
    /// Auto-called from all constructors to wire the CEF bridge events.
    /// </summary>
    private void InitBridge()
    {
        CefBridge = _bridge;

        // Car selected from CEF — search ALL collections, switch if needed.
        _bridge.CarSelected += (collection, carName) =>
        {
            if (Enum.TryParse<Collection>(collection, out var col)
                && BackendGameSparker.cars.TryGetValue(col, out var cars))
            {
                var idx = cars.ToList().FindIndex(c => c.Stats.Name == carName);
                if (idx >= 0)
                {
                    _currentCollection = col;
                    _cars = cars;
                    _selectedCarIdx = idx;
                    SetupCurrentCar();
                    _bridge.PushCurrentCollection(_currentCollection);
                }
            }
        };

        // Collection switched from CEF.
        _bridge.CollectionSelected += collectionName =>
        {
            if (Enum.TryParse<Collection>(collectionName, out var col)
                && BackendGameSparker.cars.TryGetValue(col, out var cars))
            {
                _cars = cars;
                _selectedCarIdx = 0;
                _currentCollection = col;
                SetupCurrentCar();
                _bridge.PushCurrentCollection(_currentCollection);
            }
        };

        // Car cycling from CEF keyboard.
        _bridge.CycleCarRequested += direction =>
        {
            if (direction > 0)
                CycleCarRight();
            else
                CycleCarLeft();
        };

        // Confirm selection (Enter key in CEF).
        _bridge.ConfirmSelection += SelectedCar;

        // Cancel selection (Escape key in CEF).
        _bridge.CancelSelection += SelectionCancelled;

        _bridge.BackRequested += SelectionCancelled;
    }

    private void SetupCurrentCar()
    {
        _backendCar = new BackendCar(_cars[_selectedCarIdx], 0, 0, 0, true);
        CarsInRace[0] = _backendCar;

        Camera.LookAt = new Vector3(0, 250, 400);
        Camera.Position = new Vector3(-750, 50, 750);
        FovOverride = 53;

        // create and position stat bars
        float switsLevel = (_backendCar.Stats.Swits[2] - 220) / 90f;
        switsLevel = Math.Max(0.05f, switsLevel);

        float accel = (float)(_backendCar.Stats.Acelf.X * _backendCar.Stats.Acelf.Y * _backendCar.Stats.Acelf.Z * _backendCar.Stats.Grip / 7700);

        float powerloss = _backendCar.Stats.Powerloss / 5500000f;

        float strength = ((float)_backendCar.Stats.Moment + 0.5f) / 2.6f;

        float health = (float)_backendCar.Stats.Outdam / 1.05f + _backendCar.Stats.Maxmag / 100000f;

        float airs = (_backendCar.Stats.Airc * 2 * ((float)_backendCar.Stats.Airs * 0.5f) * (float)_backendCar.Stats.Bounce + 28f) / 100f;

        float hglide = ((Math.Abs(_backendCar.Stats.Flipy) + Math.Abs(_backendCar.GroundAt)) / 2f / 70f) + (float)_backendCar.Stats.Airs / 230f;

        float ab = _backendCar.Stats.Airc / 75f;

        // Push car stats to the CEF garage page
        _bridge.PushCurrentCar(new CarStatsData
        {
            Name = _cars[_selectedCarIdx].Stats.Name,
            Collection = _currentCollection,
            TopSpeed = switsLevel,
            Acceleration = accel,
            Handling = (float)_backendCar.Stats.Dishandle,
            PowerSave = powerloss,
            Strength = strength,
            MaxHealth = health,
            Stunting = airs,
            Hypergliding = hglide,
            Abing = ab,
        });

        // Push current collection so JS can highlight the active one.
        _bridge.PushCurrentCollection(_currentCollection);
    }

    /// <summary>
    /// Push all available car collections (lightweight — only Name/Collection populated)
    /// to the CEF garage page. Called once on Enter.
    /// </summary>
    private void PushAllCollections()
    {
        var data = BackendGameSparker.cars
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => new CarCollectionData
            {
                Id = kv.Key,
                Name = kv.Key.ToString(),
                Cars = kv.Value
                    .Select(c => new CarStatsData
                    {
                        Name = c.Stats.Name,
                        Collection = kv.Key,
                    })
                    .ToArray(),
            })
            .ToArray();

        _bridge.PushCollections(data);
        _pushedCollections = true;
    }

    public override void Enter()
    {
        base.Enter();
        SetupCurrentCar();

        // Push all car collections to CEF on first enter.
        if (!_pushedCollections)
        {
            PushAllCollections();
        }
    }

    private void SelectedCar()
    {
        if (CarSelected == null) throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelected, but it was null.");
        CarSelected.Invoke(this, _backendCar!.Rad);
    }

    private void SelectionCancelled()
    {
        if (CarSelectionCancelled == null) throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelectionCancelled, but it was null.");
        CarSelectionCancelled.Invoke(this, EventArgs.Empty);
    }

    private void CycleCarRight()
    {
        _selectedCarIdx += 1;
        if (_selectedCarIdx >= _cars.Count) _selectedCarIdx -= _cars.Count;
        SetupCurrentCar();
    }

    private void CycleCarLeft()
    {
        _selectedCarIdx -= 1;
        if (_selectedCarIdx < 0) _selectedCarIdx = _cars.Count - 1;
        SetupCurrentCar();
    }

    public override void WindowSizeChanged(int width, int height)
    {

    }
}
