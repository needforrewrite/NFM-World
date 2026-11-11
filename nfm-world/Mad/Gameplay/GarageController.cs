using System.Net.Http;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
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

    private static readonly Lazy<ArchiveContentClient> Content =
        new(() => new ArchiveContentClient(ArchiveEndpoint.Url));

    private static readonly Lazy<IReadOnlyList<StockPackage>> Stock =
        new(() => StockCatalog.Build(AppContext.BaseDirectory));

    private int _selectedCarIdx = 0;
    private string? _selectedCarIdentity;
    private bool _selectionReady = true;
    private CancellationTokenSource? _onlineCancellation;
    private int _onlineGeneration;

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
    public Node BuildUi() => GarageView.Build(
        new GarageView.Actions(
            CarSelected: SelectCar,
            CollectionSelected: SelectCollection,
            Confirm: SelectedCar,
            Back: SelectionCancelled,
            SourceSelected: SetOnlineSource,
            OnlineSearch: SearchOnline
        )
    );

    /// <summary>
    /// Browses to a car without committing it: the panel and the 3D preview show it, and
    /// Enter or SELECT CAR commits; Back cancels.
    /// </summary>
    private void SelectCar(string collection, string fileName)
    {
        if (collection == "Online")
        {
            _ = PrepareOnlineCarAsync(fileName);
            return;
        }

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
                _selectedCarIdentity = fileName;
                _selectionReady = true;
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
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0
                            )
                        )
                        .ToArray()
                )
            )
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
            _backendCar = new BackendCar(
                rad,
                0,
                previous?.Position.X ?? fix64.Zero,
                previous?.Position.Z ?? fix64.Zero,
                true
            );
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

        float accel = (float)(_backendCar.Stats.Acelf.X
                              * _backendCar.Stats.Acelf.Y
                              * _backendCar.Stats.Acelf.Z
                              * _backendCar.Stats.Grip
                              / 7700);

        float powerloss = _backendCar.Stats.Powerloss / 5500000f;

        float strength = ((float)_backendCar.Stats.Moment + 0.5f) / 2.6f;

        float health = (float)_backendCar.Stats.Outdam / 1.05f + _backendCar.Stats.Maxmag / 100000f;

        float airs = (_backendCar.Stats.Airc * 2 * ((float)_backendCar.Stats.Airs * 0.5f) * (float)_backendCar.Stats.Bounce + 28f)
                     / 100f;

        float hglide = ((Math.Abs(_backendCar.Stats.Flipy) + Math.Abs(_backendCar.GroundAt)) / 2f / 70f)
                       + (float)_backendCar.Stats.Airs / 230f;

        float ab = _backendCar.Stats.Airc / 75f;

        UiState.Garage.CurrentCar.Value = new CarStatsViewModel(
            _selectedCarIdentity ?? _backendCar.Rad.FileName,
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
            ab
        );

        // Highlights the active collection in the sidebar.
        UiState.Garage.CurrentCollection.Value = _currentCollection.ToString();
    }

    private void SetOnlineSource(bool online)
    {
        if (UiState.Garage.OnlineSource.Value == online)
            return;

        UiState.Garage.OnlineSource.Value = online;
        GarageView.ClearSearch();
        UiState.Garage.OnlineStatus.Value = online ? "Search the archive for public cars." : "";

        if (online)
        {
            _onlineCancellation?.Cancel();
            _onlineGeneration++;
            UiState.Garage.Collections.Value = [];
            _selectionReady = false;
            return;
        }

        GarageDataRequested();
    }

    private async void SearchOnline(string query)
    {
        _onlineCancellation?.Cancel();
        _onlineCancellation = new CancellationTokenSource();
        var cancellation = _onlineCancellation;
        var generation = ++_onlineGeneration;
        UiState.Garage.OnlineStatus.Value = "Searching...";

        try
        {
            var page = await Content.Value.SearchAsync(query, "Car", 30, null, cancellation.Token);
            if (generation != _onlineGeneration)
                return;

            var cars = page.Items
                .Where(item => item.Eligible)
                .Select(item => new CarStatsViewModel(
                        item.ContentHash,
                        item.Name,
                        "Online",
                        0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0
                    )
                )
                .ToArray();

            UiState.Garage.Collections.Value = [new CarCollectionViewModel("Online", cars)];
            UiState.Garage.OnlineStatus.Value = cars.Length == 0
                ? "No public cars matched."
                : $"{cars.Length} public car(s). Select one to preview it.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (generation == _onlineGeneration)
                UiState.Garage.OnlineStatus.Value = $"Could not search the archive: {ex.Message}";
        }
    }

    /// <summary>
    /// Downloads and verifies a selected public car, then previews it. The previously committed
    /// car stays untouched until SELECT CAR, so a failed download changes nothing.
    /// </summary>
    private async Task PrepareOnlineCarAsync(string hash)
    {
        var generation = ++_onlineGeneration;
        UiState.Garage.OnlineStatus.Value = "Preparing car...";
        _selectionReady = false;

        var result = await ContentCache.PrepareAsync(Content.Value, hash, AssetKind.Car, CancellationToken.None);
        if (generation != _onlineGeneration)
            return;

        if (!result.Ok)
        {
            UiState.Garage.OnlineStatus.Value = result.Error ?? "That car could not be prepared.";
            return;
        }

        var identity = RadpackReader.ArchivePrefix + hash.ToLowerInvariant();
        var (_, rad) = BackendGameSparker.GetCar(identity);
        if (rad is null)
        {
            UiState.Garage.OnlineStatus.Value = "The downloaded car could not be read.";
            return;
        }

        _cars = new UnlimitedArray<Rad3d>();
        _cars.Add(rad);
        _selectedCarIdx = 0;
        _currentCollection = Collection.User;
        _selectedCarIdentity = identity;
        _selectionReady = true;
        UiState.Garage.OnlineStatus.Value = "";
        SetupCurrentCar();
    }

    private void UpdateOnlineEligibility()
    {
        var identity = _selectedCarIdentity ?? _backendCar?.Rad.FileName;
        if (identity is null)
            return;

        if (RadpackReader.IsArchiveId(identity))
        {
            UiState.Garage.OnlineStatus.Value = "Public archive car.";
            return;
        }

        var stock = Stock.Value.Any(p => p.Kind == AssetKind.Car && p.Id == identity);
        UiState.Garage.OnlineStatus.Value = stock
            ? ""
            : "Local car — publish it to the archive before using it online.";
    }

    public void Enter()
    {
        GarageView.ClearSearch();
        _selectionReady = true;
        if (_backendCar == null)
            SetupCurrentCar();

        // The view reads these signals directly; in Luau it asked for them on mount.
        GarageDataRequested();
    }

    private void SelectedCar()
    {
        if (!_selectionReady || _backendCar is null)
        {
            UiState.Garage.OnlineStatus.Value = "That car is not ready yet.";
            return;
        }

        if (CarSelected == null)
            throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelected, but it was null.");
        MenuHistory.Remember(car: _selectedCarIdentity ?? _backendCar!.Rad.FileName);
        UpdateOnlineEligibility();
        CarSelected.Invoke(this, _backendCar!.Rad);
    }

    private void SelectionCancelled()
    {
        if (CarSelectionCancelled == null)
            throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelectionCancelled, but it was null.");
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
