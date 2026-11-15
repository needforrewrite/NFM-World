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

    /// <summary>
    /// The shared public-content client. Downloading a race's assets before it starts uses the
    /// same connection pool as browsing, so a race does not open a second one.
    /// </summary>
    public static ArchiveContentClient ContentClient => Content.Value;

    private int _selectedCarIdx;
    private string? _selectedCarIdentity;
    private string? _selectedCarAuthor;
    private bool _selectionReady = true;
    private CancellationTokenSource? _onlineCancellation;
    private int _onlineGeneration;

    private Collection _currentCollection = Collection.NFMM;
    private UnlimitedArray<Rad3d> _cars = BackendGameSparker.cars[Collection.NFMM];
    private BackendCar? _backendCar;
    private Rad3d? _previewSource;
    private readonly ObservableUnlimitedArray<BackendCar> _sceneCars;
    private readonly Func<BackendCar, CarVisual?>? _visualOf;
    private readonly bool _quickSelect;
    private bool _painting;
    private long _lastPaintChange;
    private readonly Dictionary<string, Rad3d> _changedCarThumbnails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Id, bool Original), Rad3d> _changedWheelThumbnails = new();
    private readonly KeyboardNavigation _quickCategoryNavigation = new();

    public BackendCar? CurrentCar => _backendCar;

    public GarageController(
        ObservableUnlimitedArray<BackendCar> sceneCars,
        BackendCar? currentCar = null,
        Func<BackendCar, CarVisual?>? visualOf = null,
        bool quickSelect = false
    )
    {
        _sceneCars = sceneCars;
        _backendCar = currentCar;
        _visualOf = visualOf;
        _quickSelect = quickSelect;
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

    // ── Input ─────────────────────────────────────────────────────────

    public void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        // Handled here rather than in the view: a key handler on the tree root only runs
        // while the root has focus, and clicking a field (or any focusable child) takes it
        // away. A text field really does own the keys; anything else does not, or clicking
        // a slider would kill the garage's navigation.
        if (imguiWantsKeyboard && FocusManager.FocusedNode is TextInput or TextArea)
            return;

        switch (UiState.Garage.Page.Value)
        {
            case GaragePage.Home:
                HomeKey(key);
                break;
            case GaragePage.Cars:
                if (_quickSelect) QuickSelectKey(key);
                else CarsKey(key);
                break;
            case GaragePage.Colors:
                ColorsKey(key, keys);
                break;
        }
    }

    /// <summary>
    /// A held arrow arrives as a repeat, not as repeated presses. The garage walks its
    /// lists on the same keys it does on a press.
    /// </summary>
    public void KeyRepeated(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        if (key is not (Key.Up or Key.Down or Key.Left or Key.Right))
            return;

        KeyPressed(key, imguiWantsKeyboard, keys);
    }

    private void HomeKey(Key key)
    {
        switch (key)
        {
            case Key.Up:
                MoveHome(-1);
                break;
            case Key.Down:
                MoveHome(1);
                break;
            case Key.Enter:
                ActivateHome(UiState.Garage.HomeCursor.Value);
                break;
            case Key.Escape:
                SelectionCancelled();
                break;
        }
    }

    private void CarsKey(Key key)
    {
        if (GarageView.NavigatingCategories.Value)
        {
            switch (key)
            {
                case Key.Left:
                    MoveCategory(-1);
                    break;
                case Key.Right:
                    MoveCategory(1);
                    break;
                case Key.Down:
                case Key.Enter:
                    GarageView.NavigatingCategories.Value = false;
                    if (!VisibleCars().Any(car => car.FileName == CurrentIdentity()))
                        MoveSelection(0);
                    break;
                case Key.Up:
                    GarageView.NavigatingCategories.Value = false;
                    var last = VisibleCars().LastOrDefault();
                    if (last is not null)
                        SelectCar(last.Collection, last.FileName);
                    break;
                case Key.Escape:
                    GarageView.NavigatingCategories.Value = false;
                    break;
            }

            return;
        }

        switch (key)
        {
            case Key.Left:
                MoveSelection(-1);
                break;
            case Key.Right:
                MoveSelection(1);
                break;
            case Key.Up:
                MoveGridRow(-1);
                break;
            case Key.Down:
                MoveGridRow(1);
                break;
            case Key.Enter:
                SelectedCar();
                break;
            case Key.Escape:
                if (!GarageView.ClearSearch())
                    SetPage(GaragePage.Home);
                break;
        }
    }

    private void MoveCategory(int delta)
    {
        var categories = UiState.Garage.Categories.Value;
        if (categories.Count == 0) return;
        var index = categories.ToList().IndexOf(UiState.Garage.Category.Value);
        SetCategory(categories[(index + delta + categories.Count) % categories.Count]);
    }

    private void QuickSelectKey(Key key)
    {
        if (FocusManager.FocusedNode is Component { Name: "Quick car category" } category)
        {
            if (key == Key.Escape) FocusManager.ClearFocus();
            else _quickCategoryNavigation.HandleKey(category, key);
            return;
        }

        if (FocusManager.FocusedNode is TextInput or TextArea)
        {
            if (key == Key.Escape)
            {
                FocusManager.ClearFocus();
                GarageView.ClearSearch();
            }

            return;
        }

        switch (key)
        {
            case Key.Left: MoveSelection(-1); break;
            case Key.Right: MoveSelection(1); break;
            case Key.Up: MoveCategory(-1); break;
            case Key.Down: MoveCategory(1); break;
            case Key.Enter: SelectedCar(); break;
            case Key.C: SetPage(GaragePage.Colors); break;
            case Key.Escape:
                if (!GarageView.ClearSearch()) SelectionCancelled();
                break;
        }
    }

    private void ColorsKey(Key key, in Keys keys)
    {
        var state = UiState.Garage;
        if (key == Key.Tab)
        {
            SetCustomizeTab(!state.WheelsTab.Value);
            state.CustomizeTabsFocused.Value = true;
            return;
        }

        if (state.CustomizeTabsFocused.Value && key is (Key.Left or Key.Right))
        {
            SetCustomizeTab(key == Key.Right);
            state.CustomizeTabsFocused.Value = true;
            return;
        }

        if (state.CustomizeTabsFocused.Value && key is (Key.Down or Key.Up or Key.Enter))
        {
            state.CustomizeTabsFocused.Value = false;
            state.ActiveColorSlot.Value = key == Key.Up ? state.EditingSlots.Count
                : state.WheelsTab.Value && state.HasCustomWheels.Value ? -1 : 0;
            return;
        }

        if (state.WheelsTab.Value && state.ActiveColorSlot.Value == -1
            && key is (Key.Left or Key.Right or Key.Enter))
        {
            var original = !state.HasCustomWheels.Value || key == Key.Right
                           || key == Key.Enter && state.OriginalWheels.Value;
            SetWheelStyle(original);
            state.ActiveColorSlot.Value = -1;
            return;
        }

        var shift = keys.ShiftKey;
        switch (key)
        {
            case Key.Enter:
                if (state.EditingSlots.Count > 0
                    && state.ActiveColorSlot.Value == state.EditingSlots.Count)
                    ResetColors();
                break;
            case Key.Up:
                MoveColorSlot(-1);
                break;
            case Key.Down:
                MoveColorSlot(1);
                break;
            case Key.Left:
                if (shift) AdjustColor(-16);
                else MoveColorChannel(-1);
                break;
            case Key.Right:
                if (shift) AdjustColor(16);
                else MoveColorChannel(1);
                break;
            case Key.Escape:
                SetPage(_quickSelect ? GaragePage.Cars : GaragePage.Home);
                break;
        }
    }

    /// <summary>The garage view, wired straight to this controller.</summary>
    public Node BuildUi()
    {
        var actions = new GarageView.Actions(
            CarSelected: SelectCar,
            Confirm: SelectedCar,
            Back: SelectionCancelled,
            CategorySelected: SetCategory,
            OnlineSearch: SearchOnline,
            Navigate: SetPage,
            ActivateHome: ActivateHome,
            SetColorChannel: SetColorChannel,
            ResetColors: ResetColors,
            CustomizeTab: SetCustomizeTab,
            WheelStyle: SetWheelStyle,
            PaintInteraction: active =>
            {
                _painting = active;
                if (!active) FlushThumbnailChanges();
            }
        );
        return _quickSelect ? GarageView.BuildQuickSelect(actions) : GarageView.Build(actions);
    }

    public void Enter()
    {
        CarThumbnails.BeginUse(this);
        GarageView.NavigatingCategories.Value = false;
        GarageView.ClearSearch();
        _selectionReady = true;
        UiState.Garage.Page.Value = GaragePage.Home;
        UiState.Garage.HomeCursor.Value = 0;
        UiState.Garage.WheelsTab.Value = false;
        UiState.Garage.CustomizeTabsFocused.Value = false;
        UiState.Garage.OnlineStatus.Value = "";
        if (_backendCar == null)
        {
            SetupCurrentCar();
        }
        else
        {
            // The committed car can be an archive model the collection arrays do not hold,
            // so its identity has to come from the car itself.
            _selectedCarIdentity ??= _backendCar.Rad.FileName;
            CarPaint.Apply(_backendCar.Rad, _selectedCarIdentity);
            PushCurrentCar();
            PushColorSlots();
        }

        // The view reads these signals directly; in Luau it asked for them on mount.
        // Reopening starts on the tab the committed car lives in.
        UiState.Garage.Category.Value = _currentCollection.ToString();
        GarageDataRequested();
        if (_quickSelect)
        {
            SetPage(GaragePage.Cars);
            SetCategory(GarageView.AllCategory);
        }
    }

    /// <summary>Persists whatever paint is still pending. The host calls this when it closes the garage.</summary>
    public void End()
    {
        CarThumbnails.EndUse(this);
        _onlineGeneration++;
        _painting = false;
        FlushThumbnailChanges();
        CarPaint.Flush();
        _onlineCancellation?.Cancel();
        _onlineCancellation?.Dispose();
        _onlineCancellation = null;
        UiState.Garage.Page.Value = GaragePage.Home;
    }

    // ── Pages ─────────────────────────────────────────────────────────

    private void SetPage(GaragePage page)
    {
        _painting = false;
        FlushThumbnailChanges();
        if (_quickSelect && page == GaragePage.Home) page = GaragePage.Cars;
        if (UiState.Garage.Page.Value == page)
            return;

        CarPaint.Flush();
        GarageView.NavigatingCategories.Value = false;
        UiState.Garage.Page.Value = page;

        if (page == GaragePage.Cars && UiState.Garage.Collections.Value.Count == 0)
            GarageDataRequested();

        if (page == GaragePage.Colors)
            PushColorSlots();
    }

    private void MoveHome(int delta)
    {
        var entries = GarageView.HomeEntries;
        var count = entries.Count;
        if (count == 0) return;

        var index = Math.Clamp(UiState.Garage.HomeCursor.Value, 0, count - 1);
        for (var step = 0; step < count; step++)
        {
            index = (index + delta + count) % count;
            if (entries[index].Enabled)
                break;
        }

        UiState.Garage.HomeCursor.Value = index;
    }

    private void ActivateHome(int index)
    {
        var entries = GarageView.HomeEntries;
        if (index < 0 || index >= entries.Count)
            return;

        UiState.Garage.HomeCursor.Value = index;
        var entry = entries[index];
        if (!entry.Enabled)
            return;

        switch (entry.Action)
        {
            case GarageHomeAction.ChangeCar:
                SetPage(GaragePage.Cars);
                break;
            case GarageHomeAction.CustomizeCar:
                if (_backendCar == null)
                    SetupCurrentCar();
                SetPage(GaragePage.Colors);
                break;
            case GarageHomeAction.Back:
                SelectionCancelled();
                break;
        }
    }

    // ── Browse ────────────────────────────────────────────────────────

    /// <summary>
    /// Browses to a car without committing it: the panel and the 3D preview show it, and
    /// Enter or SELECT CAR commits; Back cancels.
    /// </summary>
    private void SelectCar(string collection, string fileName)
    {
        if (collection == GarageView.OnlineCategory)
        {
            _ = PrepareOnlineCarAsync(fileName);
            return;
        }

        // Search ALL collections, switching if the car lives in another one.
        if (Enum.TryParse<Collection>(collection, out var col)
            && BackendGameSparker.cars.TryGetValue(col, out var cars))
        {
            var idx = cars.ToList().FindIndex(c => c.FileName == fileName);
            if (idx >= 0 && BackendCar.IsDrivable(cars[idx]))
            {
                _currentCollection = col;
                _cars = cars;
                _selectedCarIdx = idx;
                _selectedCarIdentity = fileName;
                _selectedCarAuthor = null;
                _selectionReady = true;
                SetupCurrentCar();
            }
        }
    }

    private void SetCategory(string category)
    {
        CarPaint.Flush();
        GarageView.ClearSearch();
        _onlineCancellation?.Cancel();
        _onlineGeneration++;
        UiState.Garage.Category.Value = category;

        if (category == GarageView.OnlineCategory)
        {
            UiState.Garage.OnlineSource.Value = true;
            UiState.Garage.Collections.Value = [];
            UiState.Garage.OnlineStatus.Value = "Loading public cars...";
            _selectionReady = false;
            // List recent public cars straight away; searching narrows it.
            SearchOnline("");
            return;
        }

        _selectionReady = true;
        FillCategory(category);

        // The grid changed under the selection, so keep it only if the car is in this tab.
        var identity = CurrentIdentity();
        var shown = UiState.Garage.Collections.Value.SelectMany(collection => collection.Cars).ToArray();
        if (shown.Length > 0
            && !shown.Any(car => string.Equals(car.FileName, identity, StringComparison.OrdinalIgnoreCase)))
        {
            SelectCar(shown[0].Collection, shown[0].FileName);
        }
    }

    /// <summary>Publishes one local category's drivable cars and queues their card images.</summary>
    private void FillCategory(string category)
    {
        if (category == GarageView.AllCategory)
        {
            UiState.Garage.OnlineSource.Value = false;
            UiState.Garage.OnlineStatus.Value = "";
            UiState.Garage.Collections.Value = BackendGameSparker.cars
                .Select(pair => new CarCollectionViewModel(
                    pair.Key.ToString(),
                    DrivableViews(pair.Value, pair.Key.ToString())
                )
                )
                .Where(collection => collection.Cars.Count > 0)
                .ToArray();
            if (_quickSelect && UiState.Garage.CurrentCar.Value is { } selected
                && !UiState.Garage.Collections.Value.SelectMany(collection => collection.Cars)
                .Any(car => car.FileName == selected.FileName))
                UiState.Garage.Collections.Value = UiState.Garage.Collections.Value
                    .Append(new CarCollectionViewModel(selected.Collection, [selected])).ToArray();
            foreach (var car in BackendGameSparker.cars.Values.SelectMany(cars => cars).Where(BackendCar.IsDrivable))
                CarThumbnails.Request(car.FileName, car);
            return;
        }

        if (!Enum.TryParse<Collection>(category, out var collection)
            || !BackendGameSparker.cars.TryGetValue(collection, out var cars))
            return;

        _currentCollection = collection;
        _cars = cars;
        UiState.Garage.OnlineSource.Value = false;
        UiState.Garage.OnlineStatus.Value = "";
        UiState.Garage.Collections.Value =
        [
            new CarCollectionViewModel(category, DrivableViews(cars, category))
        ];

        foreach (var car in cars.Where(BackendCar.IsDrivable))
            CarThumbnails.Request(car.FileName, car);
    }

    /// <summary>
    /// Every installed collection with at least one car the physics can build, then the
    /// archive. An unfinished editor model has no wheels, so it can never be a car here.
    /// </summary>
    private static IReadOnlyList<string> CategoryIds()
    {
        var ids = BackendGameSparker.cars
            .Where(kv => kv.Value.Any(BackendCar.IsDrivable))
            .Select(kv => kv.Key.ToString())
            .ToList();
        ids.Insert(0, GarageView.AllCategory);
        ids.Add(GarageView.OnlineCategory);
        return ids;
    }

    private static CarStatsViewModel[] DrivableViews(IEnumerable<Rad3d> cars, string collection)
        => cars
            .Where(BackendCar.IsDrivable)
            .Select(car => new CarStatsViewModel(
                car.FileName,
                car.Stats.Name,
                collection,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0
            ))
            .ToArray();

    /// <summary>Moves the highlighted car through the list the view is showing.</summary>
    private void MoveSelection(int delta)
    {
        var cars = VisibleCars();
        if (cars.Count == 0)
            return;

        var identity = CurrentIdentity();
        var index = -1;
        for (var i = 0; i < cars.Count; i++)
        {
            if (string.Equals(cars[i].FileName, identity, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            SelectCar(cars[0].Collection, cars[0].FileName);
            return;
        }

        var next = (index + delta + cars.Count) % cars.Count;
        SelectCar(cars[next].Collection, cars[next].FileName);
    }

    private IReadOnlyList<CarStatsViewModel> VisibleCars()
        => GarageView
            .Filter(UiState.Garage.Collections.Value, UiState.Garage.SearchQuery.Value)
            .SelectMany(collection => collection.Cars)
            .ToArray();

    private void MoveGridRow(int delta)
    {
        var rows = GarageView.Filter(UiState.Garage.Collections.Value, UiState.Garage.SearchQuery.Value)
            .SelectMany(collection => collection.Cars.Chunk(GarageView.GridColumns))
            .ToArray();
        var row = Array.FindIndex(rows, cars => cars.Any(car => car.FileName == CurrentIdentity()));
        if (delta < 0 && row <= 0)
        {
            GarageView.NavigatingCategories.Value = true;
            return;
        }

        if (rows.Length == 0) return;
        var column = row < 0 ? 0 : Array.FindIndex(rows[row], car => car.FileName == CurrentIdentity());
        var next = rows[(row + delta + rows.Length) % rows.Length];
        var selected = next[Math.Min(column, next.Length - 1)];
        SelectCar(selected.Collection, selected.FileName);
    }

    private void GarageDataRequested()
    {
        var categories = CategoryIds();
        UiState.Garage.Categories.Value = categories;

        var category = UiState.Garage.Category.Value;
        if (!categories.Contains(category))
            category = categories.Count > 0 ? categories[0] : "";

        if (category.Length == 0)
        {
            UiState.Garage.Collections.Value = [];
            PushCurrentCar();
            return;
        }

        UiState.Garage.Category.Value = category;
        if (category == GarageView.OnlineCategory)
        {
            UiState.Garage.OnlineSource.Value = true;
            _selectionReady = false;
            SearchOnline("");
            return;
        }

        FillCategory(category);
        PushCurrentCar();
    }

    private void SetupCurrentCar(bool force = false)
    {
        if (_cars.Count == 0) return;
        var rad = _cars[_selectedCarIdx];
        if (!BackendCar.IsDrivable(rad))
        {
            UiState.Garage.OnlineStatus.Value = "That car is incomplete and cannot be previewed.";
            return;
        }

        var identity = _selectedCarIdentity ?? rad.FileName;

        if (force || _previewSource != rad || _backendCar is null)
        {
            var instance = CarPaint.CreateInstance(rad);
            CarPaint.Apply(instance, identity);
            var previous = _backendCar;
            try
            {
                _backendCar = new BackendCar(
                    instance,
                    0,
                    previous?.Position.X ?? fix64.Zero,
                    previous?.Position.Z ?? fix64.Zero,
                    true
                );
            }
            catch (Exception ex)
            {
                // A model the physics cannot build must not take the garage down with it.
                Logging.Error($"Could not build the preview car '{rad.FileName}': {ex.Message}");
                UiState.Garage.OnlineStatus.Value = "That car is incomplete and cannot be previewed.";
                return;
            }

            if (previous != null)
                _backendCar.Rotation = previous.Rotation;
            _previewSource = rad;
            _sceneCars[0] = _backendCar;
        }

        PushCurrentCar();
        PushColorSlots();
        CarThumbnails.Request(identity, rad);
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
            RadpackReader.IsArchiveId(_selectedCarIdentity ?? _backendCar.Rad.FileName)
                ? GarageView.OnlineCategory
                : _currentCollection.ToString(),
            switsLevel,
            accel,
            (float)_backendCar.Stats.Dishandle,
            powerloss,
            strength,
            health,
            airs,
            hglide,
            ab
        )
        {
            Author = _selectedCarAuthor,
            Class = _backendCar.Rad.Stats.Class
        };
    }

    private string? CurrentIdentity()
        => _selectedCarIdentity ?? _backendCar?.Rad.FileName;

    // ── Paint ─────────────────────────────────────────────────────────

    private void SetCustomizeTab(bool wheels)
    {
        _painting = false;
        FlushThumbnailChanges();
        CarPaint.Flush();
        UiState.Garage.WheelsTab.Value = wheels;
        UiState.Garage.CustomizeTabsFocused.Value = false;
        UiState.Garage.ActiveColorSlot.Value = wheels && UiState.Garage.HasCustomWheels.Value ? -1 : 0;
        UiState.Garage.ActiveColorChannel.Value = 0;
        PushWheelSlots();
    }

    private void PushWheelSlots()
    {
        if (_backendCar is not { } car || CurrentIdentity() is not { } id) return;
        var style = CarWheels.Get(id);
        UiState.Garage.HasCustomWheels.Value = CarWheels.HasCustomMeshes(car.Rad);
        UiState.Garage.OriginalWheels.Value = style.OriginalWheels || !CarWheels.HasCustomMeshes(car.Rad);
        var slots = CarWheels.Slots(car.Rad).Select(slot => new UiState.GarageColorSlot(slot.Slot, slot.Color)).ToList();
        if (car.Rad.Wheels.Any(wheel => wheel.Polys is null))
            slots.Add(new UiState.GarageColorSlot(
                CarPaint.SlotCount,
                car.Rad.Rims?.Color ?? new Color3(120, 120, 120),
                "Rim Color"
            )
            );
        UiState.Garage.WheelColorSlots.Value = slots;
        if (UiState.Garage.WheelsTab.Value && UiState.Garage.HasCustomWheels.Value)
        {
            CarThumbnails.RequestWheel(id, car.Rad, true);
            CarThumbnails.RequestWheel(id, car.Rad, false);
        }
    }

    private void SetWheelStyle(bool original)
    {
        if (_backendCar is not { } car || CurrentIdentity() is not { } id) return;
        var style = CarWheels.Get(id);
        style.OriginalWheels = original && CarWheels.HasCustomMeshes(car.Rad);
        CarWheels.Set(id, style);
        RefreshWheelPaint(car, id, paintChanged: false);
        PushWheelSlots();
        UiState.Garage.CustomizeTabsFocused.Value = false;
        UiState.Garage.ActiveColorSlot.Value = -1;
    }

    private void RefreshWheelPaint(BackendCar car, string id, bool paintChanged = true)
    {
        CarWheels.Apply(car.Rad, CarWheels.Get(id));
        _visualOf?.Invoke(car)?.RebuildWheels();
        MarkCarThumbnailChanged(car, id);
        if (paintChanged && CarWheels.HasCustomMeshes(car.Rad))
            _changedWheelThumbnails[(id, UiState.Garage.OriginalWheels.Value)] = car.Rad;
    }

    private void PushColorSlots()
    {
        var rad = _backendCar?.Rad;
        UiState.Garage.ColorSlots.Value = rad is null
            ? []
            : CarPaint
                .Slots(rad)
                .Select(slot => new UiState.GarageColorSlot(slot.Slot, slot.Color))
                .ToArray();

        PushWheelSlots();

        UiState.Garage.ActiveColorSlot.Value = 0;
        UiState.Garage.ActiveColorChannel.Value = 0;
    }

    private void SetColorChannel(int slot, int channel, double value)
    {
        if (_backendCar is not { } car || CurrentIdentity() is not { Length: > 0 } identity)
            return;

        var entry = UiState.Garage.EditingSlots.FirstOrDefault(candidate => candidate.Slot == slot);
        if (entry is null)
            return;

        entry.SetChannel(channel, value);
        var color = entry.Color;
        if (UiState.Garage.WheelsTab.Value)
        {
            var style = CarWheels.Get(identity);
            if (slot == CarPaint.SlotCount) style.RimColor = color;
            else
            {
                var colors = new Color3?[CarPaint.SlotCount];
                if (style.Colors is { } saved) Array.Copy(saved, colors, Math.Min(saved.Length, colors.Length));
                colors[slot] = color;
                style.Colors = colors;
            }

            CarWheels.Set(identity, style);
            RefreshWheelPaint(car, identity);
            return;
        }

        CarPaint.Set(identity, slot, color);
        CarPaint.Apply(car.Rad, identity);
        RefreshPreviewPaint(car);
        MarkCarThumbnailChanged(car, identity);
    }

    private void ResetColors()
    {
        if (_backendCar is not { } car || CurrentIdentity() is not { Length: > 0 } identity)
            return;

        if (UiState.Garage.WheelsTab.Value)
        {
            var style = CarWheels.Get(identity);
            if (UiState.Garage.OriginalWheels.Value) style.RimColor = null;
            else style.Colors = [];
            CarWheels.Set(identity, style);
            RefreshWheelPaint(car, identity);
            PushWheelSlots();
            UiState.Garage.ActiveColorSlot.Value = UiState.Garage.EditingSlots.Count;
            return;
        }

        CarPaint.Reset(identity);
        CarPaint.Apply(car.Rad, identity);
        var resetSelected = UiState.Garage.ActiveColorSlot.Value == UiState.Garage.ColorSlots.Value.Count;
        PushColorSlots();
        if (resetSelected)
            UiState.Garage.ActiveColorSlot.Value = UiState.Garage.ColorSlots.Value.Count;
        RefreshPreviewPaint(car);
        MarkCarThumbnailChanged(car, identity);
    }

    private void MarkCarThumbnailChanged(BackendCar car, string id)
    {
        _changedCarThumbnails[id] = car.Rad;
        _lastPaintChange = System.Environment.TickCount64;
    }

    private void FlushThumbnailChanges()
    {
        foreach (var (id, rad) in _changedCarThumbnails)
        {
            CarThumbnails.Invalidate(id);
            if (UiState.Garage.Page.Value == GaragePage.Cars) CarThumbnails.Request(id, rad);
        }

        _changedCarThumbnails.Clear();
        foreach (var (key, rad) in _changedWheelThumbnails)
        {
            CarThumbnails.Invalidate(CarThumbnails.WheelKey(key.Id, key.Original));
            if (UiState.Garage.Page.Value == GaragePage.Colors && UiState.Garage.WheelsTab.Value
                && CurrentIdentity() == key.Id)
                CarThumbnails.RequestWheel(key.Id, rad, key.Original);
        }

        _changedWheelThumbnails.Clear();
    }

    public void FinishPaintInteraction()
    {
        if (!_painting) return;
        _painting = false;
        FlushThumbnailChanges();
    }

    public void RenderThumbnails(NFMWorld.Graphics.ICommandBuffer cb)
    {
        if (_painting) return;
        if (System.Environment.TickCount64 - _lastPaintChange >= 150) FlushThumbnailChanges();
        if (_changedCarThumbnails.Count > 0 || _changedWheelThumbnails.Count > 0) return;
        if (UiState.Garage.Page.Value == GaragePage.Cars)
        {
            var shown = GarageView.Filter(UiState.Garage.Collections.Value, UiState.Garage.SearchQuery.Value)
                .SelectMany(collection => collection.Cars)
                .Select(car => car.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (_backendCar is { } car && CurrentIdentity() is { } identity && shown.Contains(identity))
                CarThumbnails.Request(identity, car.Rad);
            CarThumbnails.RenderPending(cb, shown.Contains);
        }
        else if (UiState.Garage.Page.Value == GaragePage.Colors && UiState.Garage.WheelsTab.Value
                 && UiState.Garage.HasCustomWheels.Value && CurrentIdentity() is { } id)
        {
            if (_backendCar is { } wheelCar)
            {
                CarThumbnails.RequestWheel(id, wheelCar.Rad, true);
                CarThumbnails.RequestWheel(id, wheelCar.Rad, false);
            }

            CarThumbnails.RenderPending(cb, key => key == CarThumbnails.WheelKey(id, true)
                                                   || key == CarThumbnails.WheelKey(id, false)
            );
        }
        else CarThumbnails.RenderPending(cb, _ => false);
    }

    private void MoveColorSlot(int delta)
    {
        var state = UiState.Garage;
        var count = state.EditingSlots.Count;
        var next = state.ActiveColorSlot.Value + delta;
        var minimum = state.WheelsTab.Value && state.HasCustomWheels.Value ? -1 : 0;
        if (next < minimum || next > count)
        {
            state.CustomizeTabsFocused.Value = true;
            return;
        }

        state.ActiveColorSlot.Value = next;
    }

    private void MoveColorChannel(int delta)
    {
        if (UiState.Garage.EditingSlots.Count == 0
            || UiState.Garage.ActiveColorSlot.Value >= UiState.Garage.EditingSlots.Count) return;
        var index = (UiState.Garage.ActiveColorChannel.Value + delta) % 3;
        if (index < 0) index += 3;
        UiState.Garage.ActiveColorChannel.Value = index;
    }

    private void AdjustColor(int delta)
    {
        var slots = UiState.Garage.EditingSlots;
        if (slots.Count == 0 || UiState.Garage.ActiveColorSlot.Value < 0 || UiState.Garage.ActiveColorSlot.Value >= slots
            .Count) return;

        var slot = slots[Math.Clamp(UiState.Garage.ActiveColorSlot.Value, 0, slots.Count - 1)];
        var channel = Math.Clamp(UiState.Garage.ActiveColorChannel.Value, 0, 2);
        SetColorChannel(slot.Slot, channel, slot.Channel(channel) + delta / 255.0);
    }

    /// <summary>
    /// The visible car already copied the rad's polys when its visual was built, so a
    /// repaint has to be pushed into that mesh. Without a visible car the rad change alone
    /// is enough: the next visual is built from the painted polys.
    /// </summary>
    private void RefreshPreviewPaint(BackendCar car)
    {
        if (_visualOf?.Invoke(car) is not { } visual)
            return;

        var polys = visual.Mesh.Polys;
        var source = car.Rad.Polys;
        var count = Math.Min(polys.Length, source.Length);
        for (var i = 0; i < count; i++)
        {
            if (polys[i].Color.Equals(source[i].Color))
                continue;

            polys[i] = polys[i] with { Color = source[i].Color };
        }

        visual.Mesh.RebuildMesh();
    }

    // ── Online ────────────────────────────────────────────────────────

    private async void SearchOnline(string query)
    {
        using var loading = LoadingWork.Begin("Searching archive...");
        _onlineCancellation?.Cancel();
        _onlineCancellation = new CancellationTokenSource();
        var cancellation = _onlineCancellation;
        var generation = ++_onlineGeneration;
        UiState.Garage.OnlineStatus.Value = "Searching...";

        try
        {
            var page = await Content.Value.SearchAsync(query, "Car", 30, null, cancellation.Token);
            await LoadingWork.NextFrame;
            if (generation != _onlineGeneration)
                return;

            var cars = page.Items
                .Where(item => item.Eligible)
                .Select(item => new CarStatsViewModel(
                        RadpackReader.ArchivePrefix + item.ContentHash.ToLowerInvariant(),
                        item.Name,
                        GarageView.OnlineCategory,
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
                    {
                        Author = string.IsNullOrWhiteSpace(item.Author) ? null : item.Author
                    }
                )
                .ToArray();

            UiState.Garage.Collections.Value = [new CarCollectionViewModel(GarageView.OnlineCategory, cars)];
            UiState.Garage.OnlineStatus.Value = cars.Length == 0
                ? "No public cars matched."
                : $"{cars.Length} public car(s). Select one to preview it.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await LoadingWork.NextFrame;
            if (generation == _onlineGeneration)
                UiState.Garage.OnlineStatus.Value = $"Could not search the archive: {ex.Message}";
        }
    }

    /// <summary>
    /// Downloads and verifies a selected public car, then previews it. The previously committed
    /// car stays untouched until SELECT CAR, so a failed download changes nothing.
    /// </summary>
    private async Task PrepareOnlineCarAsync(string hashOrIdentity)
    {
        using var loading = LoadingWork.Begin("Loading car...");
        var generation = ++_onlineGeneration;
        UiState.Garage.OnlineStatus.Value = "Preparing car...";
        _selectionReady = false;

        // Keep the published author on the preview: the committed car has no other memory of it.
        _selectedCarAuthor = UiState.Garage.Collections.Value
            .SelectMany(collection => collection.Cars)
            .FirstOrDefault(car => car.FileName.Equals(hashOrIdentity, StringComparison.OrdinalIgnoreCase))
            ?.Author;

        var result = await Task.Run(
            () => ContentCache.PrepareAsync(Content.Value, hashOrIdentity, AssetKind.Car, CancellationToken.None)
        );
        await LoadingWork.NextFrame;
        if (generation != _onlineGeneration)
            return;

        if (!result.Ok)
        {
            UiState.Garage.OnlineStatus.Value = result.Error ?? "That car could not be prepared.";
            return;
        }

        // A bare hash reaches here from a typed id; both forms resolve to the same identity.
        var identity = RadpackReader.Identity(
            RadpackReader.IsArchiveId(hashOrIdentity) ? hashOrIdentity : RadpackReader.ArchivePrefix + hashOrIdentity
        );
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

    private void SelectedCar()
    {
        if (!_selectionReady || _backendCar is null)
        {
            UiState.Garage.OnlineStatus.Value = "That car is not ready yet.";
            return;
        }

        if (CarSelected == null)
            throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelected, but it was null.");
        CarPaint.Flush();
        MenuHistory.Remember(car: _selectedCarIdentity ?? _backendCar!.Rad.FileName);
        SetPage(GaragePage.Home);
        CarSelected.Invoke(this, _backendCar!.Rad);
    }

    private void SelectionCancelled()
    {
        if (CarSelectionCancelled == null)
            throw new ArgumentNullException(nameof(CarSelected), "Attempted to invoke CarSelectionCancelled, but it was null.");
        CarPaint.Flush();
        CarSelectionCancelled.Invoke(this, EventArgs.Empty);
    }
}
