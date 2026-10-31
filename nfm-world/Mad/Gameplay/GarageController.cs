using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

internal sealed class GarageController
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
    private readonly ObservableUnlimitedArray<BackendCar> _sceneCars;
    public BackendCar? CurrentCar => _backendCar;


    public GarageController(ObservableUnlimitedArray<BackendCar> sceneCars, BackendCar? currentCar = null)
    {
        _sceneCars = sceneCars;
        _backendCar = currentCar;
        if (currentCar == null) return;
        foreach (var (collection, cars) in BackendGameSparker.cars)
        {
            var index = cars.FindIndex(c => c.FileName == currentCar.Rad.FileName);
            if (index < 0) continue;
            _currentCollection = collection;
            _cars = cars;
            _selectedCarIdx = index;
            break;
        }
    }

    public void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        // Handled here rather than in the view: a key handler on the tree root only runs
        // while the root has focus, and clicking the search box (or any focusable child)
        // takes it away - the garage then stops responding. The phase sees every key.
        if (imguiWantsKeyboard)
            return;

        switch (key)
        {
            case Key.Left:
                CycleCarLeft();
                break;
            case Key.Right:
                CycleCarRight();
                break;
            case Key.Enter:
                SelectedCar();
                break;
            case Key.Escape:
                if (!GarageView.ClearSearch())
                    SelectionCancelled();
                break;
        }
    }

    /// <summary>The garage view, wired straight to this phase.</summary>
    public Node BuildUi() => GarageView.Build(new GarageView.Actions(
        CarSelected: SelectCar,
        CollectionSelected: SelectCollection,
        Back: SelectionCancelled));

    /// <summary>
    /// Browses to a car without committing it: the panel and the 3D preview show it, and
    /// Enter or the Back button commits. Matches the Luau garage, where clicking a card
    /// only changed what you were looking at.
    /// </summary>
    private void SelectCar(string collection, string fileName)
    {
        // Search ALL collections, switching if the car lives in another one.
        if (Enum.TryParse<Collection>(collection, out var col)
            && BackendGameSparker.cars.TryGetValue(col, out var cars))
        {
            var idx = cars.ToList().FindIndex(c => c.FileName == fileName);
            if (idx >= 0)
            {
                _currentCollection = col;
                _cars = cars;
                _selectedCarIdx = idx;
                SetupCurrentCar();
            }
        }
    }

    private void SelectCollection(string collectionName)
    {
        if (Enum.TryParse<Collection>(collectionName, out var col)
            && BackendGameSparker.cars.TryGetValue(col, out var cars))
        {
            _cars = cars;
            _selectedCarIdx = 0;
            _currentCollection = col;
            SetupCurrentCar();
        }
    }

    private void GarageDataRequested()
    {
        var collections = BackendGameSparker.cars
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => new CarCollectionViewModel(
                kv.Key.ToString(),
                kv.Value
                    .Select(c => new CarStatsViewModel(
                        c.FileName,
                        c.Stats.Name,
                        kv.Key.ToString(),
                        0, 0, 0, 0, 0, 0, 0, 0, 0))
                    .ToArray()))
            .ToArray();

        UiState.Garage.Collections.Value = collections;

        PushCurrentCar();
    }

    private void SetupCurrentCar()
    {
        if (_cars.Count == 0) return;
        var rad = _cars[_selectedCarIdx];
        if (_backendCar?.Rad != rad)
        {
            var previous = _backendCar;
            _backendCar = new BackendCar(rad, 0, previous?.Position.X ?? fix64.Zero,
                previous?.Position.Z ?? fix64.Zero, true);
            if (previous != null)
                _backendCar.Rotation = previous.Rotation;
            _sceneCars[0] = _backendCar;
        }

        PushCurrentCar();
    }

    private void PushCurrentCar()
    {
        // create and position stat bars
        if (_backendCar == null) return;
        float switsLevel = (_backendCar.Stats.Swits[2] - 220) / 90f;
        switsLevel = Math.Max(0.05f, switsLevel);

        float accel = (float)(_backendCar.Stats.Acelf.X * _backendCar.Stats.Acelf.Y * _backendCar.Stats.Acelf.Z * _backendCar.Stats.Grip / 7700);

        float powerloss = _backendCar.Stats.Powerloss / 5500000f;

        float strength = ((float)_backendCar.Stats.Moment + 0.5f) / 2.6f;

        float health = (float)_backendCar.Stats.Outdam / 1.05f + _backendCar.Stats.Maxmag / 100000f;

        float airs = (_backendCar.Stats.Airc * 2 * ((float)_backendCar.Stats.Airs * 0.5f) * (float)_backendCar.Stats.Bounce + 28f) / 100f;

        float hglide = ((Math.Abs(_backendCar.Stats.Flipy) + Math.Abs(_backendCar.GroundAt)) / 2f / 70f) + (float)_backendCar.Stats.Airs / 230f;

        float ab = _backendCar.Stats.Airc / 75f;

        UiState.Garage.CurrentCar.Value = new CarStatsViewModel(
            _backendCar.Rad.FileName,
            _backendCar.Stats.Name,
            _currentCollection.ToString(),
            switsLevel,
            accel,
            (float)_backendCar.Stats.Dishandle,
            powerloss,
            strength,
            health,
            airs,
            hglide,
            ab);

        // Highlights the active collection in the sidebar.
        UiState.Garage.CurrentCollection.Value = _currentCollection.ToString();
    }

    public void Enter()
    {
        GarageView.ClearSearch();
        if (_backendCar == null)
            SetupCurrentCar();

        // The view reads these signals directly; in Luau it asked for them on mount.
        GarageDataRequested();
    }

    private void SelectedCar()
    {
        if (CarSelected == null) throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelected, but it was null.");
        MenuHistory.Remember(car: _backendCar!.Rad.FileName);
        CarSelected.Invoke(this, _backendCar!.Rad);
    }

    private void SelectionCancelled()
    {
        if (CarSelectionCancelled == null) throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelectionCancelled, but it was null.");
        CarSelectionCancelled.Invoke(this, EventArgs.Empty);
    }

    private void CycleCarRight()
    {
        if (_cars.Count == 0) return;
        _selectedCarIdx += 1;
        if (_selectedCarIdx >= _cars.Count) _selectedCarIdx -= _cars.Count;
        SetupCurrentCar();
    }

    private void CycleCarLeft()
    {
        if (_cars.Count == 0) return;
        _selectedCarIdx -= 1;
        if (_selectedCarIdx < 0) _selectedCarIdx = _cars.Count - 1;
        SetupCurrentCar();
    }

}
