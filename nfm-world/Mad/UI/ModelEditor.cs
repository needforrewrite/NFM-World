using System.Globalization;
using System.Text;
using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Platform.SDL3;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using NFMWorld.Sentry;
using NFMWorld.UI.Routes;

namespace NFMWorld.UI;

// Class to hold the state for a single model editor tab
public class ModelEditorTab
{
    public string? ModelPath { get; set; }
    public EditorObject? Object { get; set; }

    // Text editor state
    public string TextContent { get; set; } = "";
    public bool TextEditorDirty { get; set; } = false;
    public bool TextEditorExpanded { get; set; } = false;

    // Camera/view controls
    public f64Vector3 ModelRotation { get; set; } = new f64Vector3(0, 0, 0);
    public f64Vector3 ModelPosition { get; set; } = new f64Vector3(0, 0, 0);
    public float CameraDistance { get; set; } = 800f;
    public Vector3 CameraPosition { get; set; } = new Vector3(0, 0, -800);
    public float CameraYaw { get; set; } = 0f;
    public float CameraPitch { get; set; } = -10f;

    // Selection state
    public int SelectedPolygonIndex { get; set; } = -1;

    // Wheel polygon selection
    public int SelectedWheelIndex { get; set; } = -1;
    public int SelectedWheelPolygonIndex { get; set; } = -1;

    // Mouse drag state for camera control
    public bool IsDragging { get; set; } = false;
    public int DragStartX { get; set; } = 0;
    public int DragStartY { get; set; } = 0;
    public float DragStartCameraYaw { get; set; } = 0f;
    public float DragStartCameraPitch { get; set; } = 0f;
    public f64Vector3 DragStartModelPosition { get; set; } = f64Vector3.Zero;

    // Text editor highlighting
    public int TextEditorSelectionStart { get; set; } = -1;
    public int TextEditorSelectionEnd { get; set; } = -1;

    // Polygon editor window
    public bool ShowPolygonEditor { get; set; } = false;
    public string PolygonEditorContent { get; set; } = "";
    public bool PolygonEditorDirty { get; set; } = false;
    public int PolygonEditorLastSelectedIndex { get; set; } = -1;
    public bool PolygonEditorIsWheel { get; set; } = false;
    public bool PolygonEditorIsCollision { get; set; } = false;
    public int PolygonEditorWheelIndex { get; set; } = -1;

    /// <summary>Source text before the current align edits, for the Align Reset button.</summary>
    public string? AlignBaseline { get; set; }

    /// <summary>Text as last loaded/saved, so undo can clear the dirty marker again.</summary>
    public string? SavedText { get; set; }

    // Reference car overlay for scaling
    public bool ShowReferenceOverlay { get; set; } = false;
    public int ReferenceCarIndex { get; set; } = 0;
    public float ReferenceOpacity { get; set; } = 0.5f;

    // Model type (car or stage scenery)
    public enum ModelTypeEnum
    {
        Car,
        Stage
    }

    public ModelTypeEnum ModelType { get; set; } = ModelTypeEnum.Car;

    // Collision editing (for stage scenery)
    public enum EditModeEnum
    {
        Polygon,
        Collision
    }

    public EditModeEnum EditMode { get; set; } = EditModeEnum.Polygon;
    public int SelectedCollisionIndex { get; set; } = -1;

    public string GetDisplayName()
    {
        if (ModelPath == null) return "Untitled";
        var name = Path.GetFileName(ModelPath);
        return TextEditorDirty ? name + "*" : name;
    }
}

public class ModelEditorPhase : BasePhase
{
    private readonly NFMWorld.Graphics.IGraphicsDevice _graphicsDevice;
    private bool _isOpen = false;
    private bool _preserveTabsOnExit = false;
    private string[] _userModelNames = [];

    // Tab management
    private List<ModelEditorTab> _tabs = new();
    private int _activeTabIndex = -1;

    // UI tree (the Sx view), created on first BuildUi
    private ModelEditorView? _view;

    // Control states (shared across all tabs)
    private bool _zoomIn = false;
    private bool _zoomOut = false;
    private bool _orbitLeft = false;
    private bool _orbitRight = false;
    private bool _orbitUp = false;
    private bool _orbitDown = false;
    private bool _panLeft = false;
    private bool _panRight = false;
    private bool _panUp = false;
    private bool _panDown = false;
    private bool _zoomInAlt = false;
    private bool _zoomOutAlt = false;

    // Camera control speeds
    private const float ORBIT_SPEED = 2.0f; // degrees per frame
    private static fix64 PAN_SPEED { get; } = 3; // units per frame
    private const float ZOOM_SPEED = 10.0f; // units per frame

    // Mouse state (shared)
    private int _mouseX;
    private int _mouseY;
    private bool _isLeftButtonDown = false;
    private bool _isRightButtonDown = false;
    private bool _isShiftPressed = false;

    // 3D
    public PerspectiveCamera camera = new();
    private Scene scene;
    private Scene overlayScene;

    private MeshedGameObject? _wheelHighlight;
    private EditorObject? _wheelHighlightOwner;
    private int _wheelHighlightIndex = -1;

    private MeshedGameObject? _selectionOverlay;
    private EditorObject? _selectionOverlayOwner;
    private int _selectionOverlayPolygon = -1;
    private int _selectionOverlayGeometryHash = -1;

    private MeshedGameObject? _wheelSelectionOverlay;
    private MeshedGameObject? _wheelSelectionOverlayParent;
    private int _wheelSelectionOverlayPolygon = -1;
    private int _wheelSelectionOverlayGeometryHash = -1;

    public ModelEditorPhase(NFMWorld.Graphics.IGraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        scene = new Scene(graphicsDevice, [], camera, []);
        overlayScene = new Scene(graphicsDevice, [], camera, []);
        RefreshUserModels();
    }

    internal void RefreshUserModels()
    {
        var modelList = new List<string>();

        // Scan user car models
        var userCarsPath = "./data/models/user/cars";
        if (Directory.Exists(userCarsPath))
        {
            foreach (var file in Directory.GetFiles(userCarsPath, "*.rad"))
            {
                modelList.Add(Path.GetRelativePath("./data/models/user", file).Replace('\\', '/'));
            }
        }

        // Scan user stage models
        var userStagePath = "./data/models/user/stage";
        if (Directory.Exists(userStagePath))
        {
            foreach (var file in Directory.GetFiles(userStagePath, "*.rad"))
            {
                modelList.Add(Path.GetRelativePath("./data/models/user", file).Replace('\\', '/'));
            }
        }

        _userModelNames = modelList.ToArray();
    }

    public bool IsOpen => _isOpen;

    internal IReadOnlyList<ModelEditorTab> Tabs => _tabs;
    internal string[] UserModelNames => _userModelNames;
    internal int ActiveTabIndex => _activeTabIndex;

    internal ModelEditorTab? ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;

    internal void SetActiveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _activeTabIndex = index;
        _view?.OnActiveTabChanged();
    }

    internal void NotifyUiChanged() => _view?.Refresh();

    private const string TestDriveCarName = "__model_test_drive__";

    /// <summary>
    /// Validates the edited car, registers its in-memory model for the race loader,
    /// then starts stage select. Returns an error instead of driving when the car
    /// cannot race.
    /// </summary>
    internal string? StartTestDrive(bool withOtherCars)
    {
        if (ActiveTab is not { ModelType: ModelEditorTab.ModelTypeEnum.Car } tab)
            return "Test drive is only available for car models.";
        if (tab.Object is null)
            return "The model must parse before it can be test driven.";

        var rad = tab.Object.Rad;
        if (rad.Wheels.Length == 0)
            return "This car has no wheels. Add wheels in the Wheels tab before test driving.";

        var invalidStat = rad.Stats.Validate(rad.FileName);
        if (invalidStat is not null)
            return $"This car's {invalidStat} stat is missing or invalid. Fix it in Stats & Physics before test driving.";

        // Race car lookup goes through BackendGameSparker. Keep both the internal
        // test-drive key and the editor's FileName ('editing') pointing at this Rad,
        // so MenuHistory and later lookups agree with each other.
        var index = BackendGameSparker.dynamic_models.Count;
        BackendGameSparker.dynamic_models[TestDriveCarName] = (index, rad);
        BackendGameSparker.dynamic_models[rad.FileName] = (index, rad);

        TestDriveStageSelectPhase stageSelect;
        try
        {
            stageSelect = new TestDriveStageSelectPhase();
        }
        catch (Exception ex)
        {
            Logging.Error($"Unable to start test drive stage select: {ex.Message}");
            return ex.Message;
        }

        stageSelect.StageSelected += (_, stageName) => StartTestDriveRace(stageName, withOtherCars);
        stageSelect.Cancelled += (_, _) => GameSparker.PopGroup(PhaseManager.Groups.Event);

        _preserveTabsOnExit = true;
        GameSparker.PushPhase(stageSelect, PhaseManager.Groups.Event);
        return null;
    }

    private void StartTestDriveRace(string stageName, bool withOtherCars)
    {
        var tab = ActiveTab;
        if (tab?.Object is null)
            return;

        var opponents = BackendGameSparker.cars[Collection.NFMM]
            .Select(car => car.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        System.Random.Shared.Shuffle(opponents);

        var botCount = withOtherCars ? Math.Min(6, opponents.Length) : 0;
        var players = new ClientSidePlayerInfo[botCount + 1];
        players[0] = new ClientSidePlayerInfo
        {
            PlayerName = "MadPlayer",
            CarName = TestDriveCarName,
            Color = default,
            IsBot = false,
            IsClientPlayer = true
        };

        for (var i = 0; i < botCount; i++)
        {
            players[i + 1] = new ClientSidePlayerInfo
            {
                PlayerName = $"ElStupido{i}",
                CarName = opponents[i],
                Color = default,
                IsBot = true,
                IsClientPlayer = false
            };
        }

        BaseGamemodeFactory factory;
        try
        {
            factory = GamemodeFactory.Create(
                "nfmm/pvp",
                new Dictionary<string, object>
                {
                    ["constraint"] = "both"
                }
            );
        }
        catch (Exception ex)
        {
            Logging.Error($"Unable to start test drive gamemode: {ex.Message}");
            GameSparker.MessageWindow.ShowMessage("Test drive unavailable", ex.Message);
            return;
        }

        LocalRaceHost host;
        try
        {
            host = LocalRaceHost.Create(stageName, factory, new ClientGamemodeParameters { Players = players });
        }
        catch (Exception ex)
        {
            factory.End();
            Logging.Error($"Unable to start test drive: {ex.Message}");
            GameSparker.MessageWindow.ShowMessage("Test drive unavailable", ex.Message);
            return;
        }

        RacePhase race;
        try
        {
            race = new RacePhase(stageName, factory, players, host);
        }
        catch (Exception ex)
        {
            Logging.Error($"Unable to initialize test drive: {ex.Message}");
            GameSparker.MessageWindow.ShowMessage("Test drive unavailable", ex.Message);
            return;
        }

        race.Exited += (_, _) => GameSparker.PopGroup(PhaseManager.Groups.Event);
        GameSparker.Phases.ReplaceGroup(race, PhaseManager.Groups.Event);
    }

    /// <summary>Stage select that can be backed out of to the model editor.</summary>
    private sealed class TestDriveStageSelectPhase : StageSelectPhase
    {
        public event EventHandler? Cancelled;

        public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
        {
            if (!imguiWantsKeyboard && key == Key.Escape)
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
                return;
            }

            base.KeyPressed(key, imguiWantsKeyboard, keys);
        }
    }

    public override NFMWorld.Reactor.Node BuildUi()
    {
        _view ??= new ModelEditorView(this);
        return _view.Build();
    }

    public override void Enter()
    {
        base.Enter();

        _isOpen = true;
        _preserveTabsOnExit = false;

        // WindowSizeChanged only arrives on an actual resize; a phase opened at the
        // current size would otherwise keep the 1280x720 default projection aspect.
        var viewport = G.Viewport;
        camera.Width = viewport.X > 1f ? (int)viewport.X : GameSparker.Game.Window.Width;
        camera.Height = viewport.Y > 1f ? (int)viewport.Y : GameSparker.Game.Window.Height;

        World.ResetValues();

        camera.Position = new Vector3(0, -800, -800);
        camera.LookAt = Vector3.Zero;

        RefreshUserModels();
    }

    public override void Exit()
    {
        base.Exit();

        _isOpen = false;
        if (!_preserveTabsOnExit)
        {
            _tabs.Clear();
            _activeTabIndex = -1;
        }

        DisposeWheelHighlight();
        DisposeSelectionOverlay();
        DisposeWheelSelectionOverlay();
    }

    private ModelEditorTab CreateNewTab()
    {
        var tab = new ModelEditorTab();
        ResetTabView(tab);
        return tab;
    }

    internal bool TabHasUnsavedChanges(int index)
        => index >= 0 && index < _tabs.Count && _tabs[index].TextEditorDirty && _tabs[index].ModelPath != null;

    internal void PerformCloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        _tabs.RemoveAt(index);

        // Adjust active tab index
        if (_tabs.Count == 0)
        {
            _activeTabIndex = -1;
        }
        else if (_activeTabIndex >= index)
        {
            _activeTabIndex = Math.Max(0, _activeTabIndex - 1);
        }

        if (_activeTabIndex >= _tabs.Count)
            _activeTabIndex = _tabs.Count - 1;

        _view?.OnActiveTabChanged();
        _view?.Refresh();
    }

    /// <summary>Reparses the active tab after its text was replaced by undo/redo.</summary>
    internal string? ReloadActiveTabFromText()
    {
        if (ActiveTab is not { } tab)
            return "No model open.";

        try
        {
            tab.Object = new EditorObject(RadParser.ParseRad(tab.TextContent, "editing"));
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error parsing model after history step: {ex.Message}");
            return ex.Message;
        }

        RefreshPolygonEditorAfterTextChange(tab);
        _view?.OnContentChanged();
        _view?.Refresh();
        return null;
    }

    /// <summary>Writes a tab's text back to disk and reparses it. Returns an error message, if any.</summary>
    internal string? SaveTab(ModelEditorTab tab, bool collapseEditor = false)
    {
        if (tab.ModelPath is null)
            return "This model has no file path.";

        try
        {
            File.WriteAllText(tab.ModelPath, tab.TextContent);
            tab.SavedText = tab.TextContent;
            tab.TextEditorDirty = false;
            tab.AlignBaseline = null;
            tab.Object = new EditorObject(RadParser.ParseRad(tab.TextContent, "editing"));
            if (collapseEditor)
                tab.TextEditorExpanded = false;
            _view?.Refresh();
            return null;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error saving/reloading model: {ex.Message}");
            return ex.Message;
        }
    }

    internal string? OpenInExternalEditor(ModelEditorTab tab)
    {
        if (tab.ModelPath is null)
            return "This model has no file path.";

        try
        {
            // ShellExecute does not resolve a relative path against the game's working
            // directory, so hand it the absolute file path.
            var fullPath = Path.GetFullPath(tab.ModelPath);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fullPath,
                    WorkingDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty,
                    UseShellExecute = true
                }
            );
            return null;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error opening external editor: {ex.Message}");
            return ex.Message;
        }
    }

    internal string? CreateNewModel(
        string name,
        bool isCar,
        bool importReference,
        int selectedReferenceModel,
        bool openInNewTab
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Enter a model name.";

        var category = isCar ? "cars" : "stage";
        var userPath = $"./data/models/user/{category}";
        Directory.CreateDirectory(userPath);

        var filePath = Path.Combine(userPath, name + ".rad");

        if (File.Exists(filePath))
            return $"A model named '{Path.GetFileName(filePath)}' already exists.";

        string radContent;

        if (importReference && selectedReferenceModel >= 0)
        {
            // The reference lists are the NFMM collection, not a top-level cars/stage folder.
            var builtInPath = $"./data/models/nfmm/{category}";
            var refModels = isCar ? BackendGameSparker.CarRads : BackendGameSparker.StageRads;

            if (selectedReferenceModel >= refModels.Length)
                return "Select a reference model.";

            var refFile = Path.Combine(builtInPath, refModels[selectedReferenceModel] + ".rad");
            if (!File.Exists(refFile))
                return $"Reference model not found: {refFile}";

            radContent = File.ReadAllText(refFile);
        }
        else
        {
            radContent = CreateEmptyRadFile();
        }

        File.WriteAllText(filePath, radContent);

        // Load the new model
        LoadModel(filePath, openInNewTab);

        // Refresh list
        RefreshUserModels();
        return null;
    }

    private string CreateEmptyRadFile()
    {
        // Create a minimal valid .rad file
        return @"// Created with NFM World Model Editor

";
    }

    internal string? LoadModel(string filePath, bool openInNewTab = false)
    {
        // Check if already open
        var existingTabIndex = _tabs.FindIndex(t => t.ModelPath == filePath);
        if (existingTabIndex >= 0 && !openInNewTab)
        {
            // Switch to existing tab
            _activeTabIndex = existingTabIndex;
            _view?.OnActiveTabChanged();
            _view?.Refresh();
            return null;
        }

        try
        {
            var radContent = File.ReadAllText(filePath);

            ModelEditorTab tab;
            if (openInNewTab || _activeTabIndex < 0 || _tabs.Count == 0)
            {
                // Create new tab
                tab = CreateNewTab();
                _tabs.Add(tab);
                _activeTabIndex = _tabs.Count - 1;
            }
            else
            {
                // Use current tab; unsaved changes were handled by the view before this call
                tab = ActiveTab!;
            }

            var parseError = LoadModelIntoTab(tab, filePath, radContent);
            _view?.OnActiveTabChanged();
            _view?.Refresh();
            return parseError;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            var err = $"Error loading file: {ex.Message}";
            Logging.Error(err);
            return err;
        }
    }

    private string? LoadModelIntoTab(ModelEditorTab tab, string filePath, string radContent)
    {
        tab.ModelPath = filePath;
        tab.TextContent = radContent;
        tab.SavedText = radContent;
        tab.TextEditorDirty = false;

        // Determine model type from file path - normalize path separators for comparison
        var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
        if (normalizedPath.Contains("/cars/"))
        {
            tab.ModelType = ModelEditorTab.ModelTypeEnum.Car;
        }
        else if (normalizedPath.Contains("/stage/"))
        {
            tab.ModelType = ModelEditorTab.ModelTypeEnum.Stage;
        }
        else
        {
            // Default to car if we can't determine from path
            tab.ModelType = ModelEditorTab.ModelTypeEnum.Car;
        }

        // Try to parse the model, but keep the file loaded even if it fails
        try
        {
            tab.Object = new EditorObject(RadParser.ParseRad(radContent, "editing"));
            ResetTabView(tab);
            return null;
        }
        catch (Exception parseEx)
        {
            SentrySdk.CaptureException(parseEx);
            var err = $"Error parsing model: {parseEx.Message}\n\nFile loaded in text editor for correction.";
            Logging.Error($"Parse error in {Path.GetFileName(filePath)}: {parseEx.Message}");
            tab.Object = null;
            return err;
        }
    }

    private void ResetTabView(ModelEditorTab tab)
    {
        tab.ModelRotation = new f64Vector3(0, 0, 0);
        tab.ModelPosition = new f64Vector3(0, 0, 0);
        tab.CameraDistance = 800f;
        tab.CameraYaw = 0f;
        tab.CameraPitch = 10f;
        UpdateTabCameraPosition(tab);
    }

    /// <summary>
    /// Shifts the rendered view right when the left editor dock is open so the car stays
    /// centred in the free part of the viewport instead of behind the panel.
    /// </summary>
    private Vector3 GetViewportShift(ModelEditorTab tab)
    {
        if (_view?.LeftPanelOpen != true)
            return Vector3.Zero;

        var forward = (Vector3)tab.ModelPosition - tab.CameraPosition;
        var distance = forward.Length();
        if (distance < 0.001f)
            return Vector3.Zero;

        forward /= distance;
        var right = Vector3.Normalize(Vector3.Cross(forward, -Vector3.UnitY));
        var fov = MathUtil.DegreesToRadians(camera.Fov);
        var worldPerPixel = 2f * distance * MathF.Tan(fov * 0.5f) / Math.Max(1, camera.Height);
        var scale = G.Scale;
        var cover = Math.Clamp(camera.Width * 0.4f, 340f * scale, 600f * scale) + 10f * scale;

        return -right * (worldPerPixel * cover * 0.5f);
    }

    private void UpdateTabCameraPosition(ModelEditorTab tab)
    {
        // Position camera in orbit around the model position
        // Camera orbits at the specified distance
        var yawRad = MathUtil.DegreesToRadians(tab.CameraYaw);
        var pitchRad = MathUtil.DegreesToRadians(tab.CameraPitch);

        var x = tab.CameraDistance * (float)(Math.Cos(pitchRad) * Math.Sin(yawRad));
        var y = -tab.CameraDistance * (float)Math.Sin(pitchRad);
        var z = tab.CameraDistance * (float)(Math.Cos(pitchRad) * Math.Cos(yawRad));

        // Camera position is relative to the model position
        tab.CameraPosition = (Vector3)tab.ModelPosition + new Vector3(x, y, -z);
    }

    // ── View commands (2D panel) ───────────────────────────────────────

    internal void SetModelRotation(int axis, double degrees)
    {
        if (ActiveTab is not { } tab) return;

        var rotation = tab.ModelRotation;
        switch (axis)
        {
            case 0: rotation.X = (fix64)degrees; break;
            case 1: rotation.Y = (fix64)degrees; break;
            default: rotation.Z = (fix64)degrees; break;
        }

        tab.ModelRotation = rotation;
        _view?.Refresh();
    }

    internal void SetModelHeight(double height)
    {
        if (ActiveTab is not { } tab) return;

        var position = tab.ModelPosition;
        position.Y = (fix64)height;
        tab.ModelPosition = position;
        UpdateTabCameraPosition(tab);
        _view?.Refresh();
    }

    internal void SetCameraDistance(double distance)
    {
        if (ActiveTab is not { } tab) return;

        tab.CameraDistance = Math.Clamp((float)distance, 100f, 10000f);
        UpdateTabCameraPosition(tab);
        _view?.Refresh();
    }

    internal void ResetActiveView()
    {
        if (ActiveTab is not { } tab) return;

        ResetTabView(tab);
        _view?.Refresh();
    }

    internal void SetEditMode(ModelEditorTab.EditModeEnum mode)
    {
        if (ActiveTab is not { } tab) return;

        tab.EditMode = mode;
        if (mode == ModelEditorTab.EditModeEnum.Polygon)
        {
            tab.SelectedCollisionIndex = -1;
        }
        else
        {
            tab.SelectedPolygonIndex = -1;
            tab.SelectedWheelIndex = -1;
            tab.SelectedWheelPolygonIndex = -1;
        }

        _view?.Refresh();
    }

    internal void ClearSelection()
    {
        if (ActiveTab is not { } tab) return;

        tab.SelectedPolygonIndex = -1;
        tab.SelectedWheelIndex = -1;
        tab.SelectedWheelPolygonIndex = -1;
        tab.SelectedCollisionIndex = -1;
        _view?.Refresh();
    }

    internal void SetReferenceOverlay(bool shown)
    {
        if (ActiveTab is not { } tab) return;

        tab.ShowReferenceOverlay = shown;
        _view?.Refresh();
    }

    internal void SetReferenceCar(int index)
    {
        if (ActiveTab is not { } tab) return;

        tab.ReferenceCarIndex = index;
        _view?.Refresh();
    }

    internal void SetReferenceOpacity(double opacity)
    {
        if (ActiveTab is not { } tab) return;

        tab.ReferenceOpacity = Math.Clamp((float)opacity, 0.1f, 1.0f);
        _view?.Refresh();
    }

    // ── Polygon colours (1stColor .. 4thColor) ─────────────────────────

    internal readonly record struct ColorDefinition(int Slot, Color3 Color);

    private static readonly string[] ColorPrefixes = ["1stColor(", "2ndColor(", "3rdColor(", "4thColor("];

    internal static string ColorSlotName(int slot) => slot switch
    {
        0 => "First Color",
        1 => "Second Color",
        2 => "Third Color",
        _ => "Fourth Color"
    };

    /// <summary>The colours the model declares, in slot order. Third and fourth are opt-in in the RAD.</summary>
    internal List<ColorDefinition> GetColorDefinitions(ModelEditorTab tab)
    {
        var colors = new Color3?[ColorPrefixes.Length];
        foreach (var raw in tab.TextContent.Split('\n'))
        {
            var line = raw.Trim();
            for (var slot = 0; slot < ColorPrefixes.Length; slot++)
            {
                if (TryParseColorCall(line, ColorPrefixes[slot], out var color))
                    colors[slot] = color;
            }
        }

        var result = new List<ColorDefinition>(ColorPrefixes.Length);
        for (var slot = 0; slot < colors.Length; slot++)
        {
            if (colors[slot] is { } color)
                result.Add(new ColorDefinition(slot, color));
        }

        return result;
    }

    internal Color3? GetSlotColor(ModelEditorTab tab, int slot)
    {
        if (slot < 0 || slot >= ColorPrefixes.Length)
            return null;

        foreach (var raw in tab.TextContent.Split('\n'))
        {
            var line = raw.Trim();
            if (TryParseColorCall(line, ColorPrefixes[slot], out var color))
                return color;
        }

        return null;
    }

    /// <summary>
    /// Adds whichever of 1st/2ndColor is missing. Matches the original Car Maker: the
    /// first two distinct <c>c(...)</c> colours in source order, inserting the
    /// definitions just before the first polygon block.
    /// </summary>
    internal string? DefineDefaultColors()
    {
        if (ActiveTab is not { } tab)
            return "No model open.";
        if (tab.ModelType != ModelEditorTab.ModelTypeEnum.Car)
            return "Colour definitions are only available for cars.";
        if (tab.Object is null)
            return "The model must parse before colours can be defined.";

        var first = GetSlotColor(tab, 0);
        var second = GetSlotColor(tab, 1);
        if (first is not null && second is not null)
            return null;

        var detected = FirstDistinctPolyColors(tab.TextContent);
        var used = new HashSet<Color3>();
        if (first is { } firstColor) used.Add(firstColor);
        if (second is { } secondColor) used.Add(secondColor);

        Color3 NextColor(Color3 fallback)
        {
            foreach (var color in detected)
                if (!used.Contains(color))
                    return color;

            return used.Contains(fallback) ? new Color3(255, 0, 0) : fallback;
        }

        var newline = tab.TextContent.Contains("\r\n") ? "\r\n" : "\n";
        var lines = new List<string>(2);

        if (first is null)
        {
            var color = NextColor(new Color3(255, 0, 0));
            used.Add(color);
            lines.Add(FormatColorDefinition(0, color));
            TagColorSlot(tab.Object, 0, color);
        }

        if (second is null)
        {
            var color = NextColor(new Color3(0, 0, 255));
            used.Add(color);
            lines.Add(FormatColorDefinition(1, color));
            TagColorSlot(tab.Object, 1, color);
        }

        if (lines.Count == 0)
            return null;

        var insertion = tab.TextContent.IndexOf("<p>", StringComparison.Ordinal);
        if (insertion < 0)
            insertion = tab.TextContent.IndexOf("[p]", StringComparison.Ordinal);
        if (insertion < 0)
            insertion = 0;

        tab.TextContent = tab.TextContent[..insertion]
                          + string.Join(newline, lines)
                          + newline
                          + newline
                          + tab.TextContent[insertion..];
        tab.TextEditorDirty = true;
        _view?.OnContentChanged();
        _view?.Refresh();
        return null;
    }

    /// <summary>The distinct body-polygon colours in file order, as the original Car Maker found them.</summary>
    private static List<Color3> FirstDistinctPolyColors(string text)
    {
        var lines = text.Split('\n');
        var result = new List<Color3>(4);
        var inPoly = false;
        var skipPoly = false;
        var inWheelBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("<wheel", StringComparison.Ordinal))
                inWheelBlock = true;
            else if (line.StartsWith("</wheel>", StringComparison.Ordinal))
                inWheelBlock = false;

            if (line.StartsWith("<p>", StringComparison.Ordinal) || line.StartsWith("[p]", StringComparison.Ordinal))
            {
                inPoly = true;
                skipPoly = inWheelBlock || BlockHasWheelMarker(lines, i);
            }
            else if (line.StartsWith("</p>", StringComparison.Ordinal) || line.StartsWith("[/p]", StringComparison.Ordinal))
            {
                inPoly = false;
                skipPoly = false;
            }

            if (!inPoly || skipPoly || !TryParseColorCall(line, "c(", out var color) || result.Contains(color))
                continue;

            result.Add(color);
            if (result.Count == 2)
                break;
        }

        return result;
    }

    /// <summary>Sets one channel of a colour slot, updating both the source text and the live polys.</summary>
    internal string? SetSlotChannel(int slot, int channel, short value)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";
        if (GetSlotColor(tab, slot) is not { } old)
            return $"{ColorSlotName(slot)} is not defined.";

        var clamped = (short)Math.Clamp(value, (short)0, (short)255);
        var color = channel switch
        {
            0 => old with { R = clamped },
            1 => old with { G = clamped },
            _ => old with { B = clamped }
        };

        return SetSlotColor(tab, slot, old, color);
    }

    private string? SetSlotColor(ModelEditorTab tab, int slot, Color3 oldColor, Color3 color)
    {
        if (oldColor == color)
            return null;

        // The text definition may have been edited by hand since the last parse; the
        // polygons still carry the parsed colour, which is what the c(...) lines hold.
        var polyColor = FindLiveSlotColor(tab.Object, slot) ?? oldColor;

        tab.TextContent = RewriteSlotColor(tab.TextContent, slot, polyColor, color);
        tab.TextEditorDirty = true;
        ApplyColorToObject(tab.Object, slot, polyColor, color);
        RefreshPolygonEditorAfterTextChange(tab);
        _view?.OnContentChanged();
        _view?.Refresh();
        return null;
    }

    private static Color3? FindLiveSlotColor(EditorObject? obj, int slot)
    {
        if (obj is null)
            return null;

        foreach (var poly in obj.Mesh.Polys)
            if (poly.ColNum == slot)
                return poly.Color;

        return null;
    }

    private static string FormatColorDefinition(int slot, Color3 color)
        => $"{ColorPrefixes[slot]}{color.R},{color.G},{color.B})";

    private static bool TryParseColorCall(string trimmedLine, string prefix, out Color3 color)
    {
        color = default;
        if (!trimmedLine.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var close = trimmedLine.IndexOf(')', prefix.Length);
        if (close < 0)
            return false;

        var parts = trimmedLine[prefix.Length..close].Split(',');
        if (parts.Length != 3
            || !short.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
            || !short.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)
            || !short.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            return false;

        color = new Color3(r, g, b);
        return true;
    }

    private static string ReplaceColorCall(string line, string prefix, Color3 color)
    {
        var trimmed = line.TrimStart();
        var indent = line[..^trimmed.Length];
        var close = trimmed.IndexOf(')', prefix.Length);
        var rest = close >= 0 ? trimmed[(close + 1)..] : "";
        return indent + prefix + $"{color.R},{color.G},{color.B})" + rest;
    }

    /// <summary>
    /// Rewrites the slot's definition and every polygon <c>c(...)</c> line that held the old
    /// value. Polygons are matched by the colour the definition named - that is what the
    /// parser uses to derive <see cref="Rad3dPoly.ColNum"/>.
    /// </summary>
    private static string RewriteSlotColor(string text, int slot, Color3 oldColor, Color3 newColor)
    {
        var lines = text.Split('\n');
        var prefix = ColorPrefixes[slot];
        var inPoly = false;
        var skipPoly = false;
        var inWheelBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var carriageReturn = raw.EndsWith('\r');
            var line = carriageReturn ? raw[..^1] : raw;
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("<wheel", StringComparison.Ordinal))
                inWheelBlock = true;
            else if (trimmed.StartsWith("</wheel>", StringComparison.Ordinal))
                inWheelBlock = false;

            if (trimmed.StartsWith("<p>", StringComparison.Ordinal) || trimmed.StartsWith("[p]", StringComparison.Ordinal))
            {
                inPoly = true;
                skipPoly = inWheelBlock || BlockHasWheelMarker(lines, i);
            }
            else if (trimmed.StartsWith("</p>", StringComparison.Ordinal) || trimmed.StartsWith("[/p]", StringComparison.Ordinal))
            {
                inPoly = false;
                skipPoly = false;
            }

            if (TryParseColorCall(trimmed, prefix, out _))
            {
                lines[i] = ReplaceColorCall(line, prefix, newColor) + (carriageReturn ? "\r" : "");
            }
            else if (inPoly && !skipPoly && TryParseColorCall(trimmed, "c(", out var polyColor) && polyColor == oldColor)
            {
                lines[i] = ReplaceColorCall(line, "c(", newColor) + (carriageReturn ? "\r" : "");
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>Whether a polygon block is a wheel mesh (phy-addons marks it with `wheel`).</summary>
    private static bool BlockHasWheelMarker(string[] lines, int start)
    {
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("</p>", StringComparison.Ordinal) || line.StartsWith("[/p]", StringComparison.Ordinal))
                return false;
            if (line.Equals("wheel", StringComparison.Ordinal) || line.StartsWith("wheel(", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Tags the body polys whose colour is <paramref name="color"/> with a slot number.</summary>
    private static void TagColorSlot(EditorObject obj, int slot, Color3 color)
    {
        var polys = obj.Mesh.Polys;
        for (var i = 0; i < polys.Length; i++)
        {
            var poly = polys[i];
            if (poly.Color == color && poly.ColNum is null)
                polys[i] = poly with { ColNum = slot };
        }
    }

    private static void ApplyColorToObject(EditorObject? obj, int slot, Color3 oldColor, Color3 newColor)
    {
        if (obj is null)
            return;

        // Wheels/rims are not player-editable slots at the moment.
        ApplyColorToMesh(obj.Mesh, slot, oldColor, newColor);
    }

    private static void ApplyColorToMesh(Mesh mesh, int slot, Color3 oldColor, Color3 newColor)
    {
        var changed = false;
        var polys = mesh.Polys;

        for (var i = 0; i < polys.Length; i++)
        {
            var poly = polys[i];
            if (poly.ColNum == slot || (poly.ColNum is null && poly.Color == oldColor))
            {
                polys[i] = poly with { Color = newColor, ColNum = slot };
                changed = true;
            }
        }

        if (changed)
            mesh.RebuildMesh();
    }

    // ── Scale & align ──────────────────────────────────────────────────

    internal enum ScaleMode
    {
        None,
        Percent,
        Div,
        Mixed
    }

    internal readonly record struct ScaleSettings(int X, int Y, int Z, bool IsIdiv, int DivValue, ScaleMode Mode);

    internal ScaleSettings GetScaleSettings(ModelEditorTab tab)
    {
        int? x = null, y = null, z = null;
        var hasDiv = false;
        var isIdiv = false;
        var divValue = 10;

        foreach (var raw in tab.TextContent.Split('\n'))
        {
            var line = raw.Trim();
            if (TryParseSingleInt(line, "ScaleX(", out var value)) x = value;
            else if (TryParseSingleInt(line, "ScaleY(", out value)) y = value;
            else if (TryParseSingleInt(line, "ScaleZ(", out value)) z = value;
            else if (TryParseSingleInt(line, "div(", out value))
            {
                hasDiv = true;
                isIdiv = false;
                divValue = value;
            }
            else if (TryParseSingleInt(line, "idiv(", out value))
            {
                hasDiv = true;
                isIdiv = true;
                divValue = value;
            }
        }

        var hasScale = x is not null || y is not null || z is not null;
        var mode = hasScale && hasDiv ? ScaleMode.Mixed
            : hasScale ? ScaleMode.Percent
            : hasDiv ? ScaleMode.Div
            : ScaleMode.None;

        return new ScaleSettings(x ?? 100, y ?? 100, z ?? 100, isIdiv, divValue, mode);
    }

    /// <summary>Switches between ScaleX/Y/Z and div/idiv, keeping the effective scale.</summary>
    internal string? UseScaleMode(bool percent)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";

        var settings = GetScaleSettings(tab);

        if (percent)
        {
            var x = settings.Mode == ScaleMode.Div
                ? (settings.IsIdiv ? settings.DivValue : settings.DivValue * 10)
                : settings.X;
            var y = settings.Mode == ScaleMode.Div ? x : settings.Y;
            var z = settings.Mode == ScaleMode.Div ? x : settings.Z;
            return SetScalePercent(x, y, z);
        }

        // idiv(n) is exactly ScaleX(n)%, so a percent mode converts without rounding.
        var value = settings.Mode is ScaleMode.Percent or ScaleMode.None
            ? settings.X
            : settings.DivValue;
        return SetScaleDiv(idiv: true, value);
    }

    internal string? SetScalePercent(int x, int y, int z)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";

        x = Math.Clamp(x, 1, 1000);
        y = Math.Clamp(y, 1, 1000);
        z = Math.Clamp(z, 1, 1000);

        var text = InsertAtStart(
            RemoveScaleDefinitionLines(tab.TextContent),
            [$"ScaleX({x})", $"ScaleY({y})", $"ScaleZ({z})"]
        );
        return ApplyEditedText(tab, text, clearAlignBaseline: true);
    }

    internal string? SetScaleDiv(bool idiv, int value)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";

        value = Math.Clamp(value, 1, 1000);
        var text = InsertAtStart(
            RemoveScaleDefinitionLines(tab.TextContent),
            [$"{(idiv ? "idiv" : "div")}({value})"]
        );
        return ApplyEditedText(tab, text, clearAlignBaseline: true);
    }

    internal string? RotateModel(int axis)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";

        tab.AlignBaseline ??= tab.TextContent;
        return ApplyEditedText(
            tab,
            TransformPoints(tab.TextContent, axis, 0, rotate: true),
            clearAlignBaseline: false
        );
    }

    internal string? OffsetModel(int axis, int delta)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";

        tab.AlignBaseline ??= tab.TextContent;
        return ApplyEditedText(
            tab,
            TransformPoints(tab.TextContent, axis, delta, rotate: false),
            clearAlignBaseline: false
        );
    }

    internal string? ResetAlignment()
    {
        if (ActiveTab is not { } tab || tab.AlignBaseline is not { } baseline)
            return null;

        tab.AlignBaseline = null;
        return ApplyEditedText(tab, baseline, clearAlignBaseline: false);
    }

    private string? ApplyEditedText(ModelEditorTab tab, string text, bool clearAlignBaseline)
    {
        var previous = tab.TextContent;
        try
        {
            tab.Object = new EditorObject(RadParser.ParseRad(text, "editing"));
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error parsing edited model: {ex.Message}");
            return ex.Message;
        }

        tab.TextContent = text;
        if (clearAlignBaseline)
            tab.AlignBaseline = null;
        tab.TextEditorDirty = true;
        RefreshPolygonEditorAfterTextChange(tab);
        _view?.OnContentChanged();
        _view?.Refresh();
        return null;
    }

    /// <summary>Re-derives the docked polygon editor's source range after the text shifted.</summary>
    private void RefreshPolygonEditorAfterTextChange(ModelEditorTab tab)
    {
        if (!tab.ShowPolygonEditor)
            return;

        if (tab.PolygonEditorIsCollision && tab.SelectedCollisionIndex >= 0)
            JumpToSelectedCollisionInText();
        else if (tab.PolygonEditorIsWheel
                 && tab.SelectedWheelIndex >= 0
                 && tab.SelectedWheelPolygonIndex >= 0)
            JumpToSelectedWheelPolygonInText();
        else if (!tab.PolygonEditorIsWheel && !tab.PolygonEditorIsCollision && tab.SelectedPolygonIndex >= 0)
            JumpToSelectedPolygonInText();
        else
            tab.ShowPolygonEditor = false;
    }

    private static bool TryParseSingleInt(string trimmedLine, string prefix, out int value)
    {
        value = 0;
        if (!trimmedLine.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var close = trimmedLine.IndexOf(')', prefix.Length);
        if (close < 0)
            return false;

        return int.TryParse(
            trimmedLine[prefix.Length..close].Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value
        );
    }

    private static string RemoveScaleDefinitionLines(string text)
    {
        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);

        foreach (var raw in lines)
        {
            var line = raw.TrimStart();
            if (line.StartsWith("ScaleX(", StringComparison.Ordinal)
                || line.StartsWith("ScaleY(", StringComparison.Ordinal)
                || line.StartsWith("ScaleZ(", StringComparison.Ordinal)
                || line.StartsWith("div(", StringComparison.Ordinal)
                || line.StartsWith("idiv(", StringComparison.Ordinal))
                continue;

            kept.Add(raw);
        }

        return string.Join("\n", kept);
    }

    private static string InsertAtStart(string text, IReadOnlyList<string> definitions)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var body = text.TrimStart('\r', '\n');
        return string.Join(newline, definitions) + newline + newline + body;
    }

    private static string InsertBeforeFirstPoly(string text, IReadOnlyList<string> definitions)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var insertion = text.IndexOf("<p>", StringComparison.Ordinal);
        if (insertion < 0)
            insertion = text.IndexOf("[p]", StringComparison.Ordinal);
        if (insertion < 0)
            insertion = 0;

        return text[..insertion]
               + string.Join(newline, definitions)
               + newline
               + newline
               + text[insertion..];
    }

    private static string TransformPoints(string text, int axis, int delta, bool rotate)
    {
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var carriageReturn = raw.EndsWith('\r');
            var line = carriageReturn ? raw[..^1] : raw;
            var trimmed = line.TrimStart();

            if (!TryParsePointCall(trimmed, out var x, out var y, out var z))
                continue;

            if (rotate)
            {
                (x, y, z) = axis switch
                {
                    0 => (x, z, -y),
                    1 => (-z, y, x),
                    _ => (y, -x, z)
                };
            }
            else
            {
                switch (axis)
                {
                    case 0: x += delta; break;
                    case 1: y += delta; break;
                    default: z += delta; break;
                }
            }

            var indent = line[..^trimmed.Length];
            var close = trimmed.IndexOf(')');
            var rest = close >= 0 ? trimmed[(close + 1)..] : "";
            lines[i] = $"{indent}p({x},{y},{z}){rest}{(carriageReturn ? "\r" : "")}";
        }

        return string.Join("\n", lines);
    }

    private static bool TryParsePointCall(string trimmedLine, out int x, out int y, out int z)
    {
        x = y = z = 0;
        if (!trimmedLine.StartsWith("p(", StringComparison.Ordinal))
            return false;

        var close = trimmedLine.IndexOf(')', 2);
        if (close < 0)
            return false;

        var parts = trimmedLine[2..close].Split(',');
        return parts.Length == 3
               && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
               && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
               && int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out z);
    }

    // ── Wheels ─────────────────────────────────────────────────────────

    internal readonly record struct WheelSpec(
        int X,
        int Y,
        int Z,
        int Rotates,
        int Width,
        int Height,
        string Suffix,
        bool Custom
    );

    internal readonly record struct RimsSpec(int R, int G, int B, int Size, int Depth);

    internal List<WheelSpec> GetWheelSpecs(ModelEditorTab tab)
    {
        var specs = new List<WheelSpec>();
        var lines = tab.TextContent.Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!TryParseIntCall(line, "w(", 6, out var values))
                continue;

            var custom = tab.Object is { } obj
                         && specs.Count < obj.Wheels.Count
                         && obj.Wheels[specs.Count].Polys is not null;

            var close = line.IndexOf(')');
            var suffix = close >= 0 ? line[(close + 1)..] : "";
            specs.Add(
                new WheelSpec(
                    values[0],
                    values[1],
                    values[2],
                    values[3],
                    values[4],
                    values[5],
                    suffix,
                    custom
                )
            );
        }

        return specs;
    }

    internal RimsSpec GetRimsSpec(ModelEditorTab tab)
    {
        var result = new RimsSpec(140, 140, 140, 20, 20);
        foreach (var raw in tab.TextContent.Split('\n'))
        {
            var line = raw.Trim();
            if (!TryParseIntCall(line, "rims(", 5, out var values))
                continue;

            result = new RimsSpec(values[0], values[1], values[2], values[3], values[4]);
        }

        return result;
    }

    /// <summary>Adds a default front/rear pair of wheels for a model that defines none.</summary>
    internal string? CreateBasicWheels()
    {
        if (ActiveTab is not { } tab)
            return "No model open.";
        if (tab.ModelType != ModelEditorTab.ModelTypeEnum.Car)
            return "Wheels are only available for cars.";
        if (GetWheelSpecs(tab).Count > 0)
            return null;

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        var minZ = float.MaxValue;
        var maxZ = float.MinValue;

        if (tab.Object is { } obj)
        {
            foreach (var poly in obj.Mesh.Polys)
            foreach (var point in poly.Points)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
                minZ = Math.Min(minZ, point.Z);
                maxZ = Math.Max(maxZ, point.Z);
            }
        }

        var hasBounds = minX <= maxX;
        var length = hasBounds ? maxZ - minZ : 0f;
        var height = hasBounds ? maxY - minY : 0f;

        var x = hasBounds ? (int)MathF.Round((maxX - minX) * 0.45f) : 32;
        x = Math.Max(10, x);
        var y = hasBounds ? (int)MathF.Round(maxY - 2f) : 0;
        var front = hasBounds ? (int)MathF.Round(maxZ - length * 0.28f) : 27;
        var rear = hasBounds ? (int)MathF.Round(minZ + length * 0.28f) : -72;
        var wheelHeight = hasBounds ? Math.Clamp((int)MathF.Round(height * 0.4f), 6, 30) : 10;
        var wheelWidth = Math.Clamp((int)MathF.Round(wheelHeight * 1.3f), 8, 24);

        var lines = new List<string>(5);
        if (!HasRimsDefinition(tab))
            lines.Add("rims(140,140,140,20,20)");

        lines.Add($"w({-x},{y},{front},11,{wheelWidth},{wheelHeight})");
        lines.Add($"w({x},{y},{front},11,{-wheelWidth},{wheelHeight})");
        lines.Add($"w({-x},{y},{rear},0,{wheelWidth},{wheelHeight})");
        lines.Add($"w({x},{y},{rear},0,{-wheelWidth},{wheelHeight})");

        var text = InsertBeforeFirstPoly(tab.TextContent, lines);
        return ApplyEditedText(tab, text, clearAlignBaseline: true);
    }

    private static bool HasRimsDefinition(ModelEditorTab tab)
    {
        foreach (var raw in tab.TextContent.Split('\n'))
            if (raw.TrimStart().StartsWith("rims(", StringComparison.Ordinal))
                return true;

        return false;
    }

    internal readonly record struct WheelEdit(int Index, int X, int Y, int Z, int Rotates, int Width, int Height);

    /// <summary>Nearest wheel on the opposite side, used for mirrored movement.</summary>
    internal int FindMirrorPartner(ModelEditorTab tab, int index)
    {
        var specs = GetWheelSpecs(tab);
        if (index < 0 || index >= specs.Count)
            return -1;

        var self = specs[index];
        var best = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < specs.Count; i++)
        {
            if (i == index)
                continue;

            var other = specs[i];
            if (self.X != 0 && Math.Sign(other.X) == Math.Sign(self.X))
                continue;

            var dx = -self.X - other.X;
            var dy = self.Y - other.Y;
            var dz = self.Z - other.Z;
            var distance = dx * dx + dy * dy + dz * dz;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = i;
        }

        return best;
    }

    internal string? SetWheels(IReadOnlyList<WheelEdit> edits)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";
        if (edits.Count == 0)
            return null;

        var byIndex = new Dictionary<int, WheelEdit>(edits.Count);
        foreach (var edit in edits)
            byIndex[edit.Index] = edit;

        var lines = tab.TextContent.Split('\n');
        var ordinal = 0;
        var replaced = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var carriageReturn = raw.EndsWith('\r');
            var line = carriageReturn ? raw[..^1] : raw;
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("w(", StringComparison.Ordinal))
                continue;

            if (byIndex.TryGetValue(ordinal, out var edit))
            {
                var close = trimmed.IndexOf(')');
                var suffix = close >= 0 ? trimmed[(close + 1)..] : "";
                var indent = line[..^trimmed.Length];
                lines[i] = $"{indent}w({edit.X},{edit.Y},{edit.Z},{edit.Rotates},{edit.Width},{edit.Height}){suffix}"
                           + (carriageReturn ? "\r" : "");
                replaced++;
            }

            ordinal++;
        }

        if (replaced == 0)
            return "Wheel not found.";

        return ApplyEditedText(tab, string.Join("\n", lines), clearAlignBaseline: true);
    }

    private static (float X, float Y, float Z) GetWheelRawFactors(ModelEditorTab tab)
    {
        var idiv = 1f;
        var iwid = 1f;
        var scaleX = 1f;
        var scaleY = 1f;
        var scaleZ = 1f;

        foreach (var raw in tab.TextContent.Split('\n'))
        {
            var line = raw.Trim();
            if (TryParseSingleInt(line, "div(", out var value))
                idiv = value / 10f;
            else if (TryParseSingleInt(line, "idiv(", out value))
                idiv = value / 100f;
            else if (TryParseSingleInt(line, "iwid(", out value))
                iwid = value / 100f;
            else if (TryParseSingleInt(line, "ScaleX(", out value))
                scaleX = value / 100f;
            else if (TryParseSingleInt(line, "ScaleY(", out value))
                scaleY = value / 100f;
            else if (TryParseSingleInt(line, "ScaleZ(", out value))
                scaleZ = value / 100f;
        }

        return (idiv * iwid * scaleX, idiv * scaleY, idiv * scaleZ);
    }

    private void UpdateWheelGizmo(ModelEditorTab tab)
    {
        if (_view is not { WheelsTabActive: true } || tab.Object is null)
        {
            _view?.HideWheelGizmo();
            return;
        }

        var index = _view.HighlightedWheelIndex;
        if (index < 0 || index >= tab.Object.Wheels.Count || index >= tab.Object.WheelObjects.Count)
        {
            _view.HideWheelGizmo();
            return;
        }

        // The raw wheel data is in model space; the rendered wheel's world matrix
        // already carries the model transform, so project from that instead.
        var wheelObject = tab.Object.WheelObjects[index];
        var world = wheelObject.MatrixWorld.Translation;
        var modelMatrix = tab.Object.MatrixWorld;
        var axisX = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, modelMatrix));
        var axisY = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, modelMatrix));
        var axisZ = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, modelMatrix));

        var factors = GetWheelRawFactors(tab);
        var mainWheel = index < 4 && tab.Object.Wheels.Count >= 4;
        var xScale = factors.X * (mainWheel ? 0.75f : 1f);
        var zScale = factors.Z * (mainWheel ? 0.75f : 1f);
        var viewProjection = camera.ViewMatrix * camera.ProjectionMatrix;

        // Projection gives NDC; it must be mapped through the drawable viewport, not
        // camera.Width/Height (which are the projection aspect inputs and can be stale).
        var viewport = G.Viewport;
        var width = viewport.X > 1f ? (int)viewport.X : camera.Width;
        var height = viewport.Y > 1f ? (int)viewport.Y : camera.Height;

        var origin = ProjectToScreen(world, viewProjection, width, height);
        var xEnd = ProjectToScreen(world + axisX * (xScale * 10f), viewProjection, width, height);
        var yEnd = ProjectToScreen(world + axisY * (factors.Y * 10f), viewProjection, width, height);
        var zEnd = ProjectToScreen(world + axisZ * (zScale * 10f), viewProjection, width, height);

        if (origin is not { } o || xEnd is not { } ex || yEnd is not { } ey || zEnd is not { } ez)
        {
            _view.HideWheelGizmo();
            return;
        }

        _view.UpdateWheelGizmo(true, o, (ex - o) / 10f, (ey - o) / 10f, (ez - o) / 10f);
    }

    private static Vector2? ProjectToScreen(Vector3 world, Matrix viewProjection, int width, int height)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);
        if (clip.W <= 0.0001f)
            return null;

        var inverse = 1f / clip.W;
        return new Vector2(
            (clip.X * inverse * 0.5f + 0.5f) * width,
            (0.5f - clip.Y * inverse * 0.5f) * height
        );
    }

    internal string? SetRims(int r, int g, int b, int size, int depth)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";

        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        var replacement = $"rims({r},{g},{b},{size},{depth})";
        var lines = tab.TextContent.Split('\n');
        var replaced = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var carriageReturn = raw.EndsWith('\r');
            var line = carriageReturn ? raw[..^1] : raw;
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("rims(", StringComparison.Ordinal))
                continue;

            var close = trimmed.IndexOf(')');
            var suffix = close >= 0 ? trimmed[(close + 1)..] : "";
            var indent = line[..^trimmed.Length];
            lines[i] = $"{indent}{replacement}{suffix}" + (carriageReturn ? "\r" : "");
            replaced = true;
        }

        var text = string.Join("\n", lines);
        if (!replaced)
            text = InsertBeforeFirstPoly(text, [replacement]);

        return ApplyEditedText(tab, text, clearAlignBaseline: false);
    }

    private static readonly HashSet<string> ManagedStatDirectives = new(StringComparer.Ordinal)
    {
        "class", "name", "enginsignature", "swits", "acelf", "handb", "airs", "airc",
        "turn", "grip", "bounce", "simag", "moment", "comprad", "push", "revpush",
        "lift", "revlift", "powerloss", "flipy", "msquash", "clrad", "dammult",
        "maxmag", "dishandle", "handling", "outdam", "turnradius", "roadgrip",
        "offroadgrip", "offtrackgrip"
    };

    /// <summary>
    /// Replaces the base stats with one contiguous block so repeated edits do not
    /// scatter directives across the file. Recharged stats after a second
    /// <c>stats()</c> marker are left alone.
    /// </summary>
    internal string? SetStats(IReadOnlyList<string> definitions)
    {
        if (ActiveTab is not { } tab)
            return "No model open.";
        if (tab.Object is null)
            return "The model must parse before its stats can be edited.";

        var text = NormalizeStatBlock(tab.TextContent, definitions);
        if (text == tab.TextContent)
            return null;

        return ApplyEditedText(tab, text, clearAlignBaseline: false);
    }

    private static string NormalizeStatBlock(string text, IReadOnlyList<string> definitions)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var carriageReturn = newline == "\r\n" ? "\r" : "";
        var lines = text.Split('\n');

        var firstStats = -1;
        var secondStats = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (!HasDirective(lines[i], "stats"))
                continue;

            if (firstStats < 0)
                firstStats = i;
            else
            {
                secondStats = i;
                break;
            }
        }

        var baseEnd = secondStats >= 0 ? secondStats : lines.Length;
        var kept = new List<string>(lines.Length);
        for (var i = 0; i < baseEnd; i++)
        {
            if (IsManagedStatLine(lines[i]))
                continue;
            kept.Add(lines[i]);
        }

        CollapseBlankLines(kept);

        int insertion;
        if (firstStats >= 0)
        {
            var statsLine = kept.FindIndex(line => HasDirective(line, "stats"));
            insertion = statsLine >= 0 ? statsLine + 1 : 0;
        }
        else
        {
            insertion = 0;
            while (insertion < kept.Count
                   && (IsScaleDefinitionLine(kept[insertion]) || IsBlankLine(kept[insertion])))
                insertion++;
        }

        for (var i = 0; i < definitions.Count; i++)
            kept.Insert(insertion + i, definitions[i] + carriageReturn);

        var after = insertion + definitions.Count;
        if (definitions.Count > 0 && after < kept.Count && !IsBlankLine(kept[after]))
            kept.Insert(after, carriageReturn);

        CollapseBlankLines(kept);

        var tail = lines.Skip(baseEnd);
        return string.Join("\n", kept.Concat(tail));
    }

    private static void CollapseBlankLines(List<string> lines)
    {
        for (var i = lines.Count - 1; i > 0; i--)
        {
            if (IsBlankLine(lines[i]) && IsBlankLine(lines[i - 1]))
                lines.RemoveAt(i);
        }
    }

    private static bool IsManagedStatLine(string raw)
        => DirectiveKey(raw) is { } key && ManagedStatDirectives.Contains(key);

    private static bool IsScaleDefinitionLine(string raw)
        => DirectiveKey(raw) is "ScaleX" or "ScaleY" or "ScaleZ" or "div" or "idiv";

    private static bool HasDirective(string raw, string directive)
        => string.Equals(DirectiveKey(raw), directive, StringComparison.Ordinal);

    private static string? DirectiveKey(string raw)
    {
        var trimmed = raw.Trim();
        var open = trimmed.IndexOf('(');
        return open > 0 ? trimmed[..open] : null;
    }

    private static bool IsBlankLine(string raw) => string.IsNullOrWhiteSpace(raw);

    private static bool TryParseIntCall(string trimmedLine, string prefix, int count, out int[] values)
    {
        values = [];
        if (!trimmedLine.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var close = trimmedLine.IndexOf(')', prefix.Length);
        if (close < 0)
            return false;

        var parts = trimmedLine[prefix.Length..close].Split(',');
        if (parts.Length < count)
            return false;

        values = new int[count];
        for (var i = 0; i < count; i++)
        {
            if (!int.TryParse(
                    parts[i].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out values[i]
                ))
                return false;
        }

        return true;
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);
        if (!_isOpen) return;
        if (imguiWantsKeyboard) return;

        if (key == Key.Escape)
        {
            if (_view?.HandleEscape() == true) return;
            if (GameSparker.UiRenderer?.WantsKeyboard == true)
            {
                NFMWorld.Reactor.FocusManager.ClearFocus();
                return;
            }

            if (_view is { } view)
                view.RequestExit();
            else
                ExitModelViewer();
            return;
        }

        // Save works even while the code editor or a stat field has focus.
        if (keys.ControlKey && key == Key.S && _view is { ModalOpen: false } saveView)
        {
            saveView.SaveActiveDocument();
            return;
        }

        // Document undo/redo works from any focus while the editor is open. The code
        // editor's local history is disabled so both code and structural edits share
        // one stack; the docked polygon editor keeps its own local history.
        if (keys.ControlKey
            && _view is { } historyView
            && !historyView.ModalOpen
            && ActiveTab is { ShowPolygonEditor: false }
            && key is Key.Z or Key.Y)
        {
            if (key == Key.Y || (key == Key.Z && keys.ShiftKey))
                historyView.RedoDocument();
            else
                historyView.UndoDocument();
            return;
        }

        // A focused UI control or an open dialog owns the keyboard.
        if (GameSparker.UiRenderer?.WantsKeyboard == true || _view?.ModalOpen == true) return;

        var keyboardKeys = SdlWindow.GetKeyboardState();
        bool isShiftPressed = keyboardKeys[Key.LShiftKey] || keyboardKeys[Key.RShiftKey];

        switch (key)
        {
            case Key.W:
                if (isShiftPressed)
                    _panUp = true;
                else
                    _zoomIn = true; // Zoom in like scroll up
                break;
            case Key.S:
                if (isShiftPressed)
                    _panDown = true;
                else
                    _zoomOut = true; // Zoom out like scroll down
                break;
            case Key.A:
                if (isShiftPressed)
                    _panLeft = true;
                else
                    _orbitLeft = true;
                break;
            case Key.D:
                if (isShiftPressed)
                    _panRight = true;
                else
                    _orbitRight = true;
                break;
            case Key.Up:
                _orbitUp = true;
                break;
            case Key.Down:
                _orbitDown = true;
                break;
            case Key.Left:
                _orbitLeft = true;
                break;
            case Key.Right:
                _orbitRight = true;
                break;
            case Key.Oemplus:
            case Key.Add:
                _zoomInAlt = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                _zoomOutAlt = true;
                break;
        }
    }

    public override void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyReleased(key, imguiWantsKeyboard, keys);

        if (imguiWantsKeyboard) return;

        if (!_isOpen) return;

        switch (key)
        {
            case Key.W:
                _zoomIn = false;
                _panUp = false;
                break;
            case Key.S:
                _zoomOut = false;
                _panDown = false;
                break;
            case Key.A:
                _orbitLeft = false;
                _panLeft = false;
                break;
            case Key.D:
                _orbitRight = false;
                _panRight = false;
                break;
            case Key.Up:
                _orbitUp = false;
                break;
            case Key.Down:
                _orbitDown = false;
                break;
            case Key.Left:
                _orbitLeft = false;
                break;
            case Key.Right:
                _orbitRight = false;
                break;
            case Key.Oemplus:
            case Key.Add:
                _zoomInAlt = false;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                _zoomOutAlt = false;
                break;
        }
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

        if (imguiWantsMouse) return;
        if (!GameSparker.Game.IsActive) return;

        if (!_isOpen) return;

        _view?.WheelGizmoMouseMoved(x, y);

        var tab = ActiveTab;
        if (tab != null && tab.IsDragging)
        {
            // Calculate mouse delta
            int deltaX = x - tab.DragStartX;
            int deltaY = y - tab.DragStartY;

            if (_isShiftPressed)
            {
                // Pan mode: move model position
                // Calculate pan based on camera orientation
                var sensitivity = 1.5f;
                var yawRad = MathUtil.DegreesToRadians(tab.CameraYaw);
                var pitchRad = MathUtil.DegreesToRadians(tab.CameraPitch);

                // Camera right vector (for horizontal panning)
                var rightX = (float)Math.Cos(yawRad);
                var rightZ = (float)Math.Sin(yawRad);

                // Camera up vector (for vertical panning)
                var upX = -(float)(Math.Sin(pitchRad) * Math.Sin(yawRad));
                var upY = (float)Math.Cos(pitchRad);
                var upZ = -(float)(Math.Sin(pitchRad) * Math.Cos(yawRad));

                // Update model position based on pan (inverted for intuitive control)
                var panX = (fix64)((-rightX * deltaX + upX * deltaY) * sensitivity);
                var panY = (fix64)(upY * deltaY * sensitivity);
                var panZ = (fix64)((-rightZ * deltaX + upZ * deltaY) * sensitivity);

                tab.ModelPosition = tab.DragStartModelPosition + new f64Vector3(panX, panY, panZ);
            }
            else
            {
                // Orbit mode: rotate camera around target
                var sensitivity = 0.3f;
                tab.CameraYaw = tab.DragStartCameraYaw + deltaX * sensitivity;
                tab.CameraPitch = tab.DragStartCameraPitch - deltaY * sensitivity;

                // Clamp pitch to avoid gimbal lock
                tab.CameraPitch = Math.Clamp(tab.CameraPitch, -89f, 89f);

                UpdateTabCameraPosition(tab);
            }
        }

        _mouseX = x;
        _mouseY = y;

        if (tab is { IsDragging: true })
            _view?.Refresh();
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

        if (imguiWantsMouse) return;
        if (!GameSparker.Game.IsActive) return;
        if (!_isOpen) return;
        if (GameSparker.UiRenderer?.WantsMouse(x, y) == true) return;

        var tab = ActiveTab;
        if (tab != null)
        {
            // Check which button was pressed
            var (mouseButtons, _, _) = SdlWindow.GetMouseState();
            _isLeftButtonDown = mouseButtons.HasFlag(MouseButtons.Primary);
            _isRightButtonDown = mouseButtons.HasFlag(MouseButtons.Secondary);

            // Check shift state
            var keyboardKeys = SdlWindow.GetKeyboardState();
            _isShiftPressed = keyboardKeys[Key.LShiftKey] || keyboardKeys[Key.RShiftKey];

            // Start dragging for camera control with left or right mouse button
            if (_isLeftButtonDown || _isRightButtonDown)
            {
                tab.IsDragging = true;
                tab.DragStartX = x;
                tab.DragStartY = y;
                tab.DragStartCameraYaw = tab.CameraYaw;
                tab.DragStartCameraPitch = tab.CameraPitch;
                tab.DragStartModelPosition = tab.ModelPosition;
            }
        }
    }

    public override void MouseScrolled(
        int x,
        int y,
        int delta,
        bool imguiWantsMouse,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseScrolled(x, y, delta, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);

        if (imguiWantsMouse) return;
        if (!GameSparker.Game.IsActive) return;
        if (!_isOpen) return;
        if (GameSparker.UiRenderer?.WantsMouse(x, y) == true) return;

        var tab = ActiveTab;
        if (tab != null)
        {
            // Zoom in/out by adjusting camera distance
            var zoomSpeed = 0.25f;
            tab.CameraDistance -= delta * zoomSpeed;

            // Clamp to reasonable values
            tab.CameraDistance = Math.Clamp(tab.CameraDistance, 100f, 10000f);

            UpdateTabCameraPosition(tab);
        }
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

        _view?.EndWheelGizmoDrag();

        var tab = ActiveTab;
        if (tab is { IsDragging: false } && GameSparker.UiRenderer?.WantsMouse(x, y) == true)
            return;

        if (tab != null)
        {
            // Check if this was a click (not a drag)
            bool wasClick = tab.IsDragging
                            && GameSparker.Game.IsActive
                            && Math.Abs(x - tab.DragStartX) < 5
                            && Math.Abs(y - tab.DragStartY) < 5;

            // Stop dragging
            tab.IsDragging = false;

            // Update button states
            var (mouseButtons, _, _) = SdlWindow.GetMouseState();
            _isLeftButtonDown = mouseButtons.HasFlag(MouseButtons.Primary);
            _isRightButtonDown = mouseButtons.HasFlag(MouseButtons.Secondary);

            // Process click for polygon/collision selection only if it was a simple click, not a drag
            if (wasClick && !imguiWantsMouse && tab.Object != null)
            {
                if (tab.EditMode == ModelEditorTab.EditModeEnum.Polygon)
                {
                    var result = PerformRayPicking(x, y, tab);

                    if (result.WheelIndex >= 0)
                    {
                        // Wheel polygon was picked
                        tab.SelectedPolygonIndex = -1;
                        tab.SelectedWheelIndex = result.WheelIndex;
                        tab.SelectedWheelPolygonIndex = result.PolygonIndex;
                    }
                    else if (result.PolygonIndex >= 0)
                    {
                        // Body polygon was picked
                        tab.SelectedPolygonIndex = result.PolygonIndex;
                        tab.SelectedWheelIndex = -1;
                        tab.SelectedWheelPolygonIndex = -1;
                    }
                    else
                    {
                        // Clicked on background, deselect
                        tab.SelectedPolygonIndex = -1;
                        tab.SelectedWheelIndex = -1;
                        tab.SelectedWheelPolygonIndex = -1;
                    }
                }
                else if (tab.EditMode == ModelEditorTab.EditModeEnum.Collision)
                {
                    var pickedIndex = PerformCollisionPicking(x, y, tab);

                    if (pickedIndex >= 0)
                    {
                        tab.SelectedCollisionIndex = pickedIndex;
                    }
                    else
                    {
                        // Clicked on background, deselect
                        tab.SelectedCollisionIndex = -1;
                    }
                }

                SyncPolygonEditorToSelection(tab);
                _view?.Refresh();
            }
        }
    }

    /// <summary>
    /// While the docked polygon editor is open, picking another piece reloads its source,
    /// as the floating editor used to. Same-piece picks keep unsaved edits.
    /// </summary>
    private void SyncPolygonEditorToSelection(ModelEditorTab tab)
    {
        if (!tab.ShowPolygonEditor || tab.Object is null)
            return;

        if (tab.EditMode == ModelEditorTab.EditModeEnum.Collision)
        {
            if (tab.SelectedCollisionIndex >= 0
                && (!tab.PolygonEditorIsCollision
                    || tab.PolygonEditorLastSelectedIndex != tab.SelectedCollisionIndex))
                JumpToSelectedCollisionInText();
            return;
        }

        if (tab.SelectedWheelIndex >= 0 && tab.SelectedWheelPolygonIndex >= 0)
        {
            if (!tab.PolygonEditorIsWheel
                || tab.PolygonEditorWheelIndex != tab.SelectedWheelIndex
                || tab.PolygonEditorLastSelectedIndex != tab.SelectedWheelPolygonIndex)
                JumpToSelectedWheelPolygonInText();
            return;
        }

        if (tab.SelectedPolygonIndex >= 0
            && (tab.PolygonEditorIsWheel
                || tab.PolygonEditorIsCollision
                || tab.PolygonEditorLastSelectedIndex != tab.SelectedPolygonIndex))
            JumpToSelectedPolygonInText();
    }

    private readonly record struct RayPickResult(int PolygonIndex, int WheelIndex);

    /// <summary>
    /// Screen-space to world-space unprojection for editor picking: the maths of XNA's
    /// <c>Viewport.Unproject</c> (FNA/src/Graphics/Viewport.cs), inlined because the graphics
    /// abstraction has no Viewport type carrying that helper. The viewport is always the whole
    /// window (origin 0,0, depth range 0..1), matching what the old
    /// <c>GraphicsDevice.Viewport</c> was used as here.
    /// </summary>
    private static Vector3 UnprojectScreenPoint(Vector3 source, Matrix projection, Matrix view, float width, float height)
    {
        var inverse = Matrix.Invert(Matrix.Identity * view * projection);

        source.X = ((source.X / width) * 2f) - 1f;
        source.Y = -(((source.Y / height) * 2f) - 1f);
        // source.Z is already the 0..1 depth the old viewport used (MinDepth 0, MaxDepth 1).

        var result = Vector3.Transform(source, inverse);

        var w = (source.X * inverse.M14) + (source.Y * inverse.M24) + (source.Z * inverse.M34) + inverse.M44;
        if (!(MathF.Abs(w - 1.0f) <= 1.192092896e-07f))
        {
            result.X /= w;
            result.Y /= w;
            result.Z /= w;
        }

        return result;
    }

    private RayPickResult PerformRayPicking(int screenX, int screenY, ModelEditorTab tab)
    {
        if (tab.Object == null) return new RayPickResult(-1, -1);

        var viewportWidth = GameSparker.Game.Window.Width;
        var viewportHeight = GameSparker.Game.Window.Height;

        // Set up the model's transform exactly as RenderModel does
        var originalPosition = tab.Object.Position;
        var originalRotation = tab.Object.Rotation;

        // Keep model at origin - ModelPosition is only for camera orbit center
        tab.Object.Position = f64Vector3.Zero;
        tab.Object.Rotation = new f64Euler(
            f64AngleSingle.FromDegrees(tab.ModelRotation.Y), // Yaw
            f64AngleSingle.FromDegrees(-tab.ModelRotation.X), // Pitch (negated)
            f64AngleSingle.FromDegrees(tab.ModelRotation.Z) // Roll
        );

        // Get the ACTUAL MatrixWorld that will be used for rendering
        var modelWorld = tab.Object.MatrixWorld;

        // Restore transform
        tab.Object.Position = originalPosition;
        tab.Object.Rotation = originalRotation;

        // Set up camera exactly as RenderModel does
        // Use GameSparker.camera's actual Width/Height (not viewport, which might differ)
        var viewShift = GetViewportShift(tab);
        var tempCamera = new PerspectiveCamera
        {
            Position = tab.CameraPosition + viewShift,
            LookAt = (Vector3)tab.ModelPosition + viewShift, // Look at the orbit center, not origin
            Up = -Vector3.UnitY,
            Width = camera.Width,
            Height = camera.Height,
            Fov = camera.Fov, // Use actual FOV from settings
            Near = camera.Near,
            Far = camera.Far
        };

        // Call OnBeforeRender to compute the View and Projection matrices
        tempCamera.OnBeforeRender(1);

        var view = tempCamera.ViewMatrix;
        var projection = tempCamera.ProjectionMatrix;

        // Unproject screen coordinates to world space ray
        var nearPoint = UnprojectScreenPoint(new Vector3(screenX, screenY, 0f), projection, view, viewportWidth, viewportHeight);
        var farPoint = UnprojectScreenPoint(new Vector3(screenX, screenY, 1f), projection, view, viewportWidth, viewportHeight);

        var rayOrigin = nearPoint;
        var rayDirection = Vector3.Normalize(farPoint - nearPoint);

        // Test against all body polygons
        float closestBodyDistance = float.MaxValue;
        int closestBodyPolyIndex = -1;

        for (int i = 0; i < tab.Object.Mesh.Polys.Length; i++)
        {
            var poly = tab.Object.Mesh.Polys[i];

            // Test each triangle in this polygon
            for (int t = 0; t < poly.Triangles.Length; t += 3)
            {
                var i0 = poly.Triangles[t];
                var i1 = poly.Triangles[t + 1];
                var i2 = poly.Triangles[t + 2];

                // Transform vertices by the model's world matrix
                var v0 = Vector3.Transform(poly.Points[i0], modelWorld);
                var v1 = Vector3.Transform(poly.Points[i1], modelWorld);
                var v2 = Vector3.Transform(poly.Points[i2], modelWorld);

                if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v1, v2, out float distance))
                {
                    if (distance < closestBodyDistance)
                    {
                        closestBodyDistance = distance;
                        closestBodyPolyIndex = i;
                    }
                }
            }
        }

        // Test against all wheel polygons
        float closestWheelDistance = float.MaxValue;
        int closestWheelIndex = -1;
        int closestWheelPolyIndex = -1;

        var wheelObjects = tab.Object.WheelObjects;
        for (int wi = 0; wi < wheelObjects.Count; wi++)
        {
            var wheelObj = wheelObjects[wi];
            var wheelWorld = wheelObj.MatrixWorld;

            for (int pi = 0; pi < wheelObj.Mesh.Polys.Length; pi++)
            {
                var poly = wheelObj.Mesh.Polys[pi];

                for (int t = 0; t < poly.Triangles.Length; t += 3)
                {
                    var i0 = poly.Triangles[t];
                    var i1 = poly.Triangles[t + 1];
                    var i2 = poly.Triangles[t + 2];

                    var v0 = Vector3.Transform(poly.Points[i0], wheelWorld);
                    var v1 = Vector3.Transform(poly.Points[i1], wheelWorld);
                    var v2 = Vector3.Transform(poly.Points[i2], wheelWorld);

                    if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v1, v2, out float distance))
                    {
                        if (distance < closestWheelDistance)
                        {
                            closestWheelDistance = distance;
                            closestWheelIndex = wi;
                            closestWheelPolyIndex = pi;
                        }
                    }
                }
            }
        }

        // Return the closest hit: prefer wheel if closer, otherwise body
        if (closestWheelIndex >= 0 && (closestBodyPolyIndex < 0 || closestWheelDistance < closestBodyDistance))
        {
            return new RayPickResult(closestWheelPolyIndex, closestWheelIndex);
        }

        return new RayPickResult(closestBodyPolyIndex, -1);
    }

    // Möller–Trumbore ray-triangle intersection algorithm
    private bool RayIntersectsTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        out float distance
    )
    {
        distance = 0;
        const float EPSILON = 0.000001f; // Smaller epsilon for better accuracy

        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var h = Vector3.Cross(rayDirection, edge2);
        var a = Vector3.Dot(edge1, h);

        // Check if ray is parallel to triangle (with smaller tolerance)
        if (a > -EPSILON && a < EPSILON)
            return false;

        var f = 1.0f / a;
        var s = rayOrigin - v0;
        var u = f * Vector3.Dot(s, h);

        // Allow slightly outside bounds for edge cases
        if (u < -EPSILON || u > 1.0f + EPSILON)
            return false;

        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(rayDirection, q);

        // Allow slightly outside bounds for edge cases
        if (v < -EPSILON || u + v > 1.0f + EPSILON)
            return false;

        distance = f * Vector3.Dot(edge2, q);

        // Only accept intersections in front of the ray
        return distance > EPSILON;
    }

    private int PerformCollisionPicking(int screenX, int screenY, ModelEditorTab tab)
    {
        if (tab.Object == null || tab.Object.Boxes.Count == 0) return -1;

        var viewportWidth = GameSparker.Game.Window.Width;
        var viewportHeight = GameSparker.Game.Window.Height;

        // Set up camera exactly as RenderModel does
        var viewShift = GetViewportShift(tab);
        var tempCamera = new PerspectiveCamera
        {
            Position = tab.CameraPosition + viewShift,
            LookAt = (Vector3)tab.ModelPosition + viewShift, // Look at the orbit center, not origin
            Up = -Vector3.UnitY,
            Width = camera.Width,
            Height = camera.Height,
            Fov = camera.Fov,
            Near = camera.Near,
            Far = camera.Far
        };

        tempCamera.OnBeforeRender(1);

        var view = tempCamera.ViewMatrix;
        var projection = tempCamera.ProjectionMatrix;

        // Unproject screen coordinates to world space ray
        var nearPoint = UnprojectScreenPoint(new Vector3(screenX, screenY, 0f), projection, view, viewportWidth, viewportHeight);
        var farPoint = UnprojectScreenPoint(new Vector3(screenX, screenY, 1f), projection, view, viewportWidth, viewportHeight);

        var rayOrigin = nearPoint;
        var rayDirection = Vector3.Normalize(farPoint - nearPoint);

        // Test against all collision boxes
        float closestDistance = float.MaxValue;
        int closestBoxIndex = -1;

        for (int i = 0; i < tab.Object.Boxes.Count; i++)
        {
            var box = tab.Object.Boxes[i];

            // Check if ray intersects this box
            if (RayIntersectsBox(rayOrigin, rayDirection, box, out float distance))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBoxIndex = i;
                }
            }
        }

        return closestBoxIndex;
    }

    private bool RayIntersectsBox(Vector3 rayOrigin, Vector3 rayDirection, Rad3dBoxDef box, out float distance)
    {
        distance = float.MaxValue;

        // Convert box to axis-aligned bounding box in world space
        var center = (Vector3)box.Translation;
        var radius = (Vector3)box.Radius;

        // Simple AABB ray intersection (ignoring rotation for now - can be enhanced later)
        var min = center - radius;
        var max = center + radius;

        float tmin = (min.X - rayOrigin.X) / rayDirection.X;
        float tmax = (max.X - rayOrigin.X) / rayDirection.X;

        if (tmin > tmax)
        {
            (tmin, tmax) = (tmax, tmin);
        }

        float tymin = (min.Y - rayOrigin.Y) / rayDirection.Y;
        float tymax = (max.Y - rayOrigin.Y) / rayDirection.Y;

        if (tymin > tymax)
        {
            var temp = tymin;
            tymin = tymax;
            tymax = temp;
        }

        if ((tmin > tymax) || (tymin > tmax))
            return false;

        if (tymin > tmin)
            tmin = tymin;

        if (tymax < tmax)
            tmax = tymax;

        float tzmin = (min.Z - rayOrigin.Z) / rayDirection.Z;
        float tzmax = (max.Z - rayOrigin.Z) / rayDirection.Z;

        if (tzmin > tzmax)
        {
            var temp = tzmin;
            tzmin = tzmax;
            tzmax = temp;
        }

        if ((tmin > tzmax) || (tzmin > tmax))
            return false;

        if (tzmin > tmin)
            tmin = tzmin;

        if (tzmax < tmax)
            tmax = tzmax;

        // Check if intersection is in front of ray
        if (tmax < 0)
            return false;

        distance = tmin > 0 ? tmin : tmax;
        return true;
    }

    private void FlipSelectedPolygonVertexOrder()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedPolygonIndex < 0 || tab.Object == null) return;

        var poly = tab.Object.Mesh.Polys[tab.SelectedPolygonIndex];
        Array.Reverse(poly.Points);

        // Rebuild the mesh with the flipped polygon
        tab.Object.Mesh.RebuildMesh();

        // Update the text content to reflect the change
        UpdateTextContentFromModel(tab);
    }

    private void RemoveSelectedPolygon()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedPolygonIndex < 0 || tab.Object == null) return;

        var polyList = tab.Object.Mesh.Polys.ToList();
        polyList.RemoveAt(tab.SelectedPolygonIndex);
        tab.Object.Mesh.Polys = polyList.ToArray();

        // Rebuild the mesh
        tab.Object.Mesh.RebuildMesh();

        // Update the text content to reflect the change
        UpdateTextContentFromModel(tab);

        // Clear selection
        tab.SelectedPolygonIndex = -1;
    }

    internal void JumpToSelectedPolygonInText()
    {
        var tab = ActiveTab;
        if (tab == null || tab.Object == null) return;

        // Handle wheel polygon selection
        if (tab.SelectedWheelIndex >= 0 && tab.SelectedWheelPolygonIndex >= 0)
        {
            JumpToSelectedWheelPolygonInText();
            return;
        }

        if (tab.SelectedPolygonIndex < 0) return;

        // Body polys can be written as <p>…</p> or [p]…[/p], often mixed in one
        // file, and wheel blocks are not part of the body index. Count only the
        // body blocks, in source order.
        var (selectionStart, selectionEnd) = FindBodyPolygonBlock(tab.TextContent, tab.SelectedPolygonIndex);

        // Store selection range for future use (currently just for debugging)
        tab.TextEditorSelectionStart = selectionStart;
        tab.TextEditorSelectionEnd = selectionEnd;

        // Extract the polygon code and open the editor window
        if (selectionStart >= 0 && selectionEnd >= 0 && selectionEnd <= tab.TextContent.Length)
        {
            tab.PolygonEditorContent = tab.TextContent.Substring(selectionStart, selectionEnd - selectionStart);
            tab.ShowPolygonEditor = true;
            tab.PolygonEditorDirty = false;
            tab.PolygonEditorLastSelectedIndex = tab.SelectedPolygonIndex; // Remember which polygon we're editing
            tab.PolygonEditorIsWheel = false;
            tab.PolygonEditorIsCollision = false;
            tab.PolygonEditorWheelIndex = -1;
        }

        // Debug output
        if (selectionStart >= 0)
        {
            Logging.Debug($"Polygon {tab.SelectedPolygonIndex + 1}: selection range {selectionStart}-{selectionEnd}");
        }

        _view?.OnPolygonContentChanged();
        _view?.Refresh();
    }

    /// <summary>Finds the nth body polygon block, supporting &lt;p&gt; and [p] and skipping wheels.</summary>
    private static (int Start, int End) FindBodyPolygonBlock(string text, int index)
    {
        if (index < 0)
            return (-1, -1);

        var count = 0;
        var i = 0;

        while (i + 3 <= text.Length)
        {
            if (i + 6 <= text.Length && text.AsSpan(i).StartsWith("<wheel", StringComparison.Ordinal))
            {
                var wheelEnd = text.IndexOf("</wheel>", i, StringComparison.Ordinal);
                i = wheelEnd < 0 ? text.Length : wheelEnd + 8;
                continue;
            }

            var isAngle = text.AsSpan(i).StartsWith("<p>", StringComparison.Ordinal);
            var isSquare = !isAngle && text.AsSpan(i).StartsWith("[p]", StringComparison.Ordinal);
            if (!isAngle && !isSquare)
            {
                i++;
                continue;
            }

            var openLength = 3;
            var closeTag = isAngle ? "</p>" : "[/p]";
            var close = text.IndexOf(closeTag, i + openLength, StringComparison.Ordinal);
            var end = close < 0 ? -1 : close + closeTag.Length;
            if (end < 0)
            {
                var nextAngle = text.IndexOf("<p>", i + openLength, StringComparison.Ordinal);
                var nextSquare = text.IndexOf("[p]", i + openLength, StringComparison.Ordinal);
                var next = nextAngle < 0 ? nextSquare
                    : nextSquare < 0 ? nextAngle
                    : Math.Min(nextAngle, nextSquare);
                end = next < 0 ? text.Length : next;
            }

            if (!BlockLooksLikeWheel(text, i, end))
            {
                if (count == index)
                    return (i, end);
                count++;
            }

            i = end;
        }

        return (-1, -1);
    }

    private static bool BlockLooksLikeWheel(string text, int start, int end)
    {
        foreach (var raw in text[start..Math.Min(end, text.Length)].Split('\n'))
        {
            var line = raw.Trim();
            if (line.Equals("wheel", StringComparison.Ordinal) || line.StartsWith("wheel(", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    internal void JumpToSelectedWheelPolygonInText()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedWheelIndex < 0 || tab.SelectedWheelPolygonIndex < 0 || tab.Object == null) return;

        int selectionStart = -1;
        int selectionEnd = -1;
        var wheelIndex = tab.SelectedWheelIndex;
        var polyIndex = tab.SelectedWheelPolygonIndex;

        var text = tab.TextContent;

        // First, check if this wheel uses SRC/PhyShot format (<wheel> blocks before w(...) lines)
        // SRC/PhyShot wheel meshes are declared before the wheel and correspond 1:1 by index
        int srcWheelBlockCount = 0;
        int srcWheelBlockStart = -1;
        int srcWheelBlockEnd = -1;

        for (int i = 0; i < text.Length; i++)
        {
            if (i + 7 <= text.Length && text.Substring(i, 7) == "<wheel>")
            {
                if (srcWheelBlockCount == wheelIndex)
                {
                    srcWheelBlockStart = i;
                    // Find closing </wheel>
                    int searchPos = i + 7;
                    while (searchPos + 8 <= text.Length)
                    {
                        if (text.Substring(searchPos, 8) == "</wheel>")
                        {
                            srcWheelBlockEnd = searchPos + 8;
                            break;
                        }

                        searchPos++;
                    }

                    break;
                }

                srcWheelBlockCount++;
            }
            else if (i + 7 <= text.Length
                     && text.Substring(i, 7) == "<wheel "
                     && text.IndexOf("radius=", i, Math.Min(30, text.Length - i)) > i)
            {
                // PhyShot format: <wheel radius="..." depth="...">
                if (srcWheelBlockCount == wheelIndex)
                {
                    srcWheelBlockStart = i;
                    int searchPos = i + 7;
                    while (searchPos + 8 <= text.Length)
                    {
                        if (text.Substring(searchPos, 8) == "</wheel>")
                        {
                            srcWheelBlockEnd = searchPos + 8;
                            break;
                        }

                        searchPos++;
                    }

                    break;
                }

                srcWheelBlockCount++;
            }
        }

        if (srcWheelBlockStart >= 0 && srcWheelBlockEnd >= 0)
        {
            // SRC/PhyShot format: find the Nth polygon block inside this <wheel> block.
            // Supports both <p>/</p> and [p]/[/p] formats.
            int polyCount = 0;
            for (int i = srcWheelBlockStart; i < srcWheelBlockEnd && i < text.Length; i++)
            {
                // Check for open tag: <p> or [p]
                if (i + 3 <= text.Length)
                {
                    string openTag = text.Substring(i, 3);
                    bool isOpenTag = openTag == "<p>" || openTag == "[p]";
                    if (isOpenTag)
                    {
                        if (polyCount == polyIndex)
                        {
                            selectionStart = i;
                            string closeTag = openTag == "<p>" ? "</p>" : "[/p]";
                            int searchPos = i + 3;
                            while (searchPos + closeTag.Length <= text.Length && searchPos < srcWheelBlockEnd)
                            {
                                if (text.Substring(searchPos, closeTag.Length) == closeTag)
                                {
                                    selectionEnd = searchPos + closeTag.Length;
                                    break;
                                }

                                searchPos++;
                            }

                            if (selectionEnd == -1 || selectionEnd > srcWheelBlockEnd)
                                selectionEnd = srcWheelBlockEnd;
                            break;
                        }

                        polyCount++;
                    }
                }
            }
        }
        else
        {
            // phy-addons format: polygons with wheel/wheel(N) markers after w(...)c lines
            // Find the Nth w(...)c line (phy-addons wheel declaration)
            int phyWheelCount = 0;
            int phyWheelLineEnd = -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == 'w' && i + 2 < text.Length && text[i + 1] == '(')
                {
                    // Find the closing ) of w(...)
                    int closeParen = text.IndexOf(')', i + 2);
                    if (closeParen > 0 && closeParen + 1 < text.Length && text[closeParen + 1] == 'c')
                    {
                        // This is a phy-addons wheel: w(...)c
                        if (phyWheelCount == wheelIndex)
                        {
                            phyWheelLineEnd = text.IndexOf('\n', closeParen + 1);
                            if (phyWheelLineEnd < 0) phyWheelLineEnd = text.Length;
                            break;
                        }

                        phyWheelCount++;
                    }
                }
            }

            if (phyWheelLineEnd >= 0)
            {
                // After the w(...)c line, find polygon blocks that contain wheel or wheel(N) markers.
                // Supports both <p>/</p> and [p]/[/p] formats.
                int wheelPolyCount = 0;

                for (int i = phyWheelLineEnd; i < text.Length; i++)
                {
                    // Check for open tag: <p> or [p]
                    int openLen = 0;
                    if (i + 3 <= text.Length && text.Substring(i, 3) == "<p>")
                        openLen = 3;
                    else if (i + 3 <= text.Length && text.Substring(i, 3) == "[p]")
                        openLen = 3;

                    if (openLen > 0)
                    {
                        int polyStart = i;

                        // Find matching close tag: </p> or [/p]
                        int polyEnd = -1;
                        string closeTag = openLen == 3 && text[i] == '<' ? "</p>" : "[/p]";
                        int searchPos = i + openLen;
                        while (searchPos + closeTag.Length <= text.Length)
                        {
                            if (text.Substring(searchPos, closeTag.Length) == closeTag)
                            {
                                polyEnd = searchPos + closeTag.Length;
                                break;
                            }

                            searchPos++;
                        }

                        if (polyEnd < 0)
                        {
                            // No closing tag found; skip this block
                            i = searchPos;
                            continue;
                        }

                        // Check if this block contains wheel/wheel(N) marker
                        bool isWheelPoly = false;
                        bool matchesWheel = false;

                        // Check for wheel(N) — specific wheel assignment
                        int wheelMarkerPos = text.IndexOf("wheel(", polyStart, polyEnd - polyStart);
                        if (wheelMarkerPos > polyStart && wheelMarkerPos < polyEnd)
                        {
                            isWheelPoly = true;
                            int numStart = wheelMarkerPos + 6;
                            int numEnd = text.IndexOf(')', numStart);
                            if (numEnd > numStart && numEnd < polyEnd)
                            {
                                if (int.TryParse(text.Substring(numStart, numEnd - numStart), out int parsedWheelId))
                                {
                                    matchesWheel = (parsedWheelId == wheelIndex);
                                }
                            }
                        }
                        else
                        {
                            // Check for bare "wheel" (shared mesh)
                            int bareWheelPos = text.IndexOf("wheel", polyStart, polyEnd - polyStart);
                            if (bareWheelPos > polyStart && bareWheelPos < polyEnd)
                            {
                                // Make sure it's not "wheel("
                                if (bareWheelPos + 5 >= polyEnd || text[bareWheelPos + 5] != '(')
                                {
                                    isWheelPoly = true;
                                    // Shared mesh — belongs to all wheels without specific meshes
                                    // Check if THIS wheel uses the shared mesh (no specific polys)
                                    bool wheelHasSpecificPolys = false;
                                    for (int checkI = phyWheelLineEnd; checkI < polyStart; checkI++)
                                    {
                                        if (text.IndexOf($"wheel({wheelIndex})", checkI, polyStart - checkI) > checkI)
                                        {
                                            wheelHasSpecificPolys = true;
                                            break;
                                        }
                                    }

                                    matchesWheel = !wheelHasSpecificPolys;
                                }
                            }
                        }

                        if (isWheelPoly && matchesWheel)
                        {
                            if (wheelPolyCount == polyIndex)
                            {
                                selectionStart = polyStart;
                                selectionEnd = polyEnd;
                                break;
                            }

                            wheelPolyCount++;
                        }

                        i = polyEnd - 1; // Continue after this block

                        // Stop when we hit the next w(...) line (start of next wheel pair)
                        int nextCheck = polyEnd;
                        while (nextCheck < text.Length
                               && (text[nextCheck] == '\n' || text[nextCheck] == '\r' || text[nextCheck] == '-'))
                            nextCheck++;
                        if (nextCheck < text.Length
                            && text[nextCheck] == 'w'
                            && nextCheck + 1 < text.Length
                            && text[nextCheck + 1] == '(')
                        {
                            break;
                        }
                    }
                }
            }
        }

        // Store selection and open editor
        tab.TextEditorSelectionStart = selectionStart;
        tab.TextEditorSelectionEnd = selectionEnd;

        if (selectionStart >= 0 && selectionEnd >= 0 && selectionEnd <= text.Length)
        {
            tab.PolygonEditorContent = text.Substring(selectionStart, selectionEnd - selectionStart);
            tab.ShowPolygonEditor = true;
            tab.PolygonEditorDirty = false;
            tab.PolygonEditorLastSelectedIndex = polyIndex;
            tab.PolygonEditorIsWheel = true;
            tab.PolygonEditorIsCollision = false;
            tab.PolygonEditorWheelIndex = wheelIndex;
        }

        if (selectionStart >= 0)
        {
            Logging.Debug($"Wheel {wheelIndex + 1} Polygon {polyIndex + 1}: selection range {selectionStart}-{selectionEnd}");
        }

        _view?.OnPolygonContentChanged();
        _view?.Refresh();
    }

    internal void JumpToSelectedCollisionInText()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedCollisionIndex < 0 || tab.Object == null) return;

        // Find the collision box in the text by searching for <track> tags and counting
        int collisionCount = 0;
        int selectionStart = -1;
        int selectionEnd = -1;

        for (int i = 0; i < tab.TextContent.Length; i++)
        {
            // Check if we're at the start of a <track> tag
            if (i + 7 <= tab.TextContent.Length && tab.TextContent.Substring(i, 7) == "<track>")
            {
                if (collisionCount == tab.SelectedCollisionIndex)
                {
                    // Found the start of our target collision
                    selectionStart = i;

                    // Now find the closing </track> tag
                    int searchPos = i + 7;
                    while (searchPos + 8 <= tab.TextContent.Length)
                    {
                        if (tab.TextContent.Substring(searchPos, 8) == "</track>")
                        {
                            selectionEnd = searchPos + 8; // Include the closing tag
                            break;
                        }

                        searchPos++;
                    }

                    // If we didn't find a closing tag, select until the next <track> or end of file
                    if (selectionEnd == -1)
                    {
                        searchPos = i + 7;
                        while (searchPos + 7 <= tab.TextContent.Length)
                        {
                            if (tab.TextContent.Substring(searchPos, 7) == "<track>")
                            {
                                selectionEnd = searchPos;
                                break;
                            }

                            searchPos++;
                        }

                        if (selectionEnd == -1)
                        {
                            selectionEnd = tab.TextContent.Length;
                        }
                    }

                    break;
                }

                collisionCount++;
            }
        }

        // Store selection range for future use
        tab.TextEditorSelectionStart = selectionStart;
        tab.TextEditorSelectionEnd = selectionEnd;

        // Extract the collision code and open the editor window
        if (selectionStart >= 0 && selectionEnd >= 0 && selectionEnd <= tab.TextContent.Length)
        {
            tab.PolygonEditorContent = tab.TextContent.Substring(selectionStart, selectionEnd - selectionStart);
            tab.ShowPolygonEditor = true;
            tab.PolygonEditorDirty = false;
            tab.PolygonEditorLastSelectedIndex = tab.SelectedCollisionIndex; // Remember which collision we're editing
            tab.PolygonEditorIsWheel = false;
            tab.PolygonEditorIsCollision = true;
            tab.PolygonEditorWheelIndex = -1;
        }

        // Debug output
        if (selectionStart >= 0)
        {
            Logging.Debug($"Collision {tab.SelectedCollisionIndex + 1}: selection range {selectionStart}-{selectionEnd}");
        }

        _view?.OnPolygonContentChanged();
        _view?.Refresh();
    }

    private void UpdateTextContentFromModel(ModelEditorTab tab)
    {
        if (tab.Object == null) return;

        // Don't regenerate from scratch - this would lose comments and formatting
        // Instead, this method should only be called when we've done structural changes
        // like removing/flipping polygons, and we accept losing the original formatting

        var sb = new StringBuilder();
        sb.AppendLine("// Modified in Model Editor");
        sb.AppendLine();

        foreach (var poly in tab.Object.Mesh.Polys)
        {
            sb.AppendLine("<p>");
            sb.AppendLine($"c({poly.Color.R},{poly.Color.G},{poly.Color.B})");
            sb.AppendLine();

            foreach (var point in poly.Points)
            {
                sb.AppendLine($"p({point.X:F0},{point.Y:F0},{point.Z:F0})");
            }

            sb.AppendLine("</p>");
            sb.AppendLine();
        }

        tab.TextContent = sb.ToString();
        tab.TextEditorDirty = true;
    }

    public override void GameTick()
    {
        base.GameTick();

        if (!_isOpen) return;

        var tab = ActiveTab;
        if (tab == null) return;

        // Apply camera orbit (A/D and Arrow keys)
        if (_orbitLeft)
            tab.CameraYaw -= ORBIT_SPEED;
        if (_orbitRight)
            tab.CameraYaw += ORBIT_SPEED;
        if (_orbitUp)
        {
            tab.CameraPitch += ORBIT_SPEED;
            tab.CameraPitch = Math.Clamp(tab.CameraPitch, -89f, 89f);
        }

        if (_orbitDown)
        {
            tab.CameraPitch -= ORBIT_SPEED;
            tab.CameraPitch = Math.Clamp(tab.CameraPitch, -89f, 89f);
        }

        // Apply panning (Shift+WASD for all directions)
        if (_panLeft || _panRight || _panUp || _panDown)
        {
            var yawRad = MathUtil.DegreesToRadians(tab.CameraYaw);
            var pitchRad = MathUtil.DegreesToRadians(tab.CameraPitch);

            // Camera right vector (for horizontal panning)
            var rightX = (fix64)Math.Cos(yawRad);
            var rightZ = (fix64)Math.Sin(yawRad);

            // Camera up vector (for vertical panning)
            var upX = -(fix64)(Math.Sin(pitchRad) * Math.Sin(yawRad));
            var upY = (fix64)Math.Cos(pitchRad);
            var upZ = -(fix64)(Math.Sin(pitchRad) * Math.Cos(yawRad));

            var pos = tab.ModelPosition;

            // Sideways panning (Shift+A/D)
            if (_panLeft)
            {
                pos.X += rightX * PAN_SPEED;
                pos.Z += rightZ * PAN_SPEED;
            }

            if (_panRight)
            {
                pos.X -= rightX * PAN_SPEED;
                pos.Z -= rightZ * PAN_SPEED;
            }

            // Height panning (Shift+W/S)
            if (_panUp)
            {
                pos.X -= upX * PAN_SPEED;
                pos.Y -= upY * PAN_SPEED;
                pos.Z -= upZ * PAN_SPEED;
            }

            if (_panDown)
            {
                pos.X += upX * PAN_SPEED;
                pos.Y += upY * PAN_SPEED;
                pos.Z += upZ * PAN_SPEED;
            }

            tab.ModelPosition = pos;
        }

        // Apply camera zoom (W/S and +/- keys)
        if (_zoomIn || _zoomInAlt)
        {
            tab.CameraDistance -= ZOOM_SPEED;
            tab.CameraDistance = Math.Clamp(tab.CameraDistance, 100f, 10000f);
        }

        if (_zoomOut || _zoomOutAlt)
        {
            tab.CameraDistance += ZOOM_SPEED;
            tab.CameraDistance = Math.Clamp(tab.CameraDistance, 100f, 10000f);
        }

        UpdateTabCameraPosition(tab);

        if (_orbitLeft
            || _orbitRight
            || _orbitUp
            || _orbitDown
            || _panLeft
            || _panRight
            || _panUp
            || _panDown
            || _zoomIn
            || _zoomOut
            || _zoomInAlt
            || _zoomOutAlt)
            _view?.Refresh();
    }

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);

        // Sky-blue editor background. Colour only, matching the old XNA Clear(Color) call - the
        // model itself is drawn later by Render3DOverlays, which clears to its own colour, so this
        // is the fill behind the UI panels.
        cb.Clear(NFMWorld.Graphics.ClearOptions.Color, new NFMWorld.Graphics.ColorRgba(135 / 255f, 206 / 255f, 235 / 255f));
    }

    internal void InsertPolygonFlag(string command)
    {
        var tab = ActiveTab;
        if (tab == null) return;

        // Find the c(...) line in the polygon editor content
        var lines = tab.PolygonEditorContent.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("c(") && trimmed.EndsWith(")"))
            {
                // Insert the command on the next line
                lines[i] = lines[i] + "\n" + command;
                tab.PolygonEditorContent = string.Join("\n", lines);
                tab.PolygonEditorDirty = true;
                break;
            }
        }

        _view?.OnPolygonContentChanged();
        _view?.Refresh();
    }

    /// <summary>Applies the polygon editor's code to the model. Returns an error message, if any.</summary>
    internal string? ApplyPolygonEditorChanges(bool removeElement)
    {
        var tab = ActiveTab;
        if (tab == null || tab.TextEditorSelectionStart < 0 || tab.TextEditorSelectionEnd < 0)
            return "Invalid selection range. Please select a polygon again.";

        // Validate selection indices are within bounds
        if (tab.TextEditorSelectionStart >= tab.TextContent.Length
            || tab.TextEditorSelectionEnd > tab.TextContent.Length
            || tab.TextEditorSelectionStart >= tab.TextEditorSelectionEnd)
        {
            var err =
                $"Selection range is out of sync with text content. Start: {tab.TextEditorSelectionStart}, End: {tab.TextEditorSelectionEnd}, Length: {tab.TextContent.Length}";
            SentrySdk.CaptureMessage(err, SentryLevel.Error);
            Logging.Error(err);

            // Invalidate selection so user is forced to reselect
            tab.TextEditorSelectionStart = -1;
            tab.TextEditorSelectionEnd = -1;
            tab.ShowPolygonEditor = false;
            _view?.OnPolygonContentChanged();
            _view?.Refresh();
            return err;
        }

        bool editingCollision = tab.PolygonEditorIsCollision;
        bool editingWheel = !editingCollision && tab.PolygonEditorIsWheel;
        int editedIndex = editingCollision ? tab.SelectedCollisionIndex
            : editingWheel ? tab.SelectedWheelPolygonIndex
            : tab.SelectedPolygonIndex;

        return PerformApplyChanges(tab, editingCollision, editedIndex, removeElement);
    }

    private string? PerformApplyChanges(ModelEditorTab tab, bool editingCollision, int editedIndex, bool removeElement)
    {
        // Replace the polygon/collision code in the main text content
        var before = tab.TextContent.Substring(0, tab.TextEditorSelectionStart);
        var after = tab.TextContent.Substring(tab.TextEditorSelectionEnd);

        tab.TextContent = before + tab.PolygonEditorContent + after;
        tab.TextEditorDirty = true;

        // Try to reload the model with the new code
        try
        {
            tab.Object = new EditorObject(RadParser.ParseRad(tab.TextContent, "editing"));
            tab.PolygonEditorDirty = false;

            if (removeElement)
            {
                // Element was removed, close editor and deselect
                tab.ShowPolygonEditor = false;
                if (editingCollision)
                {
                    tab.SelectedCollisionIndex = -1;
                }
                else if (tab.PolygonEditorIsWheel)
                {
                    tab.SelectedWheelIndex = -1;
                    tab.SelectedWheelPolygonIndex = -1;
                }
                else
                {
                    tab.SelectedPolygonIndex = -1;
                }

                var itemType = editingCollision ? "Collision" : "Polygon";
                Logging.Debug($"{itemType} {editedIndex + 1} removed successfully");
            }
            else
            {
                // Re-select the same polygon/collision and update selection range
                // This allows the user to continue editing without having to reselect
                if (editingCollision)
                {
                    tab.SelectedCollisionIndex = editedIndex;
                    // Find the updated collision in the text
                    JumpToSelectedCollisionInText();
                }
                else if (tab.PolygonEditorIsWheel)
                {
                    tab.SelectedPolygonIndex = -1;
                    tab.SelectedWheelIndex = tab.PolygonEditorWheelIndex;
                    tab.SelectedWheelPolygonIndex = editedIndex;
                    // Find the updated wheel polygon in the text
                    JumpToSelectedWheelPolygonInText();
                }
                else
                {
                    tab.SelectedPolygonIndex = editedIndex;
                    tab.SelectedWheelIndex = -1;
                    tab.SelectedWheelPolygonIndex = -1;
                    // Find the updated polygon in the text
                    JumpToSelectedPolygonInText();
                }

                // Keep the editor window open with updated content
                tab.ShowPolygonEditor = true;

                var itemType = editingCollision ? "Collision" : "Polygon";
                Logging.Debug($"{itemType} {editedIndex + 1} updated successfully");
            }

            _view?.OnContentChanged();
            _view?.OnPolygonContentChanged();
            _view?.Refresh();
            return null;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            var err = $"Error parsing updated code:\n{ex.Message}";
            Logging.Error(err);
            return err;
        }
    }

    public void ExitModelViewer()
    {
        GameSparker.ExitEditor();
    }

    public override void Render3DOverlays(NFMWorld.Graphics.ICommandBuffer cb)
    {
        base.Render3DOverlays(cb);

        var tab = ActiveTab;
        if (!_isOpen || tab == null || tab.Object == null)
        {
            _view?.HideWheelGizmo();
            return;
        }

        // (The old scene-wide BlendState.Opaque/DepthStencilState.Default pair is gone: the new
        // model bakes blend/depth state into each pipeline, and PolyPipeline already is the
        // opaque/default pair for exactly this geometry.)

        // Set up camera to orbit around and look at the model position
        var viewShift = GetViewportShift(tab);
        camera.Position = tab.CameraPosition + viewShift;
        camera.LookAt = (Vector3)tab.ModelPosition + viewShift; // Look at the orbit center (which can be panned)

        // Store original transform
        var originalPosition = tab.Object.Position;
        var originalRotation = tab.Object.Rotation;

        // Keep the model at the origin - don't apply ModelPosition
        // ModelPosition is used only for camera orbit center, not mesh position
        tab.Object.Position = f64Vector3.Zero;
        // Euler constructor is (yaw, pitch, roll)
        tab.Object.Rotation = new f64Euler(
            f64AngleSingle.FromDegrees(tab.ModelRotation.Y), // Yaw (Y-axis rotation)
            f64AngleSingle.FromDegrees(-tab.ModelRotation.X), // Pitch (X-axis rotation, negated for correct direction)
            f64AngleSingle.FromDegrees(tab.ModelRotation.Z) // Roll (Z-axis rotation)
        );

        scene.Objects.Clear();
        scene.Objects.Add(tab.Object);

        // Render main model first
        scene.Render(cb, 1, false);
        UpdateWheelGizmo(tab);

        // Render reference car overlay with transparency (rendered separately after main model)
        if (tab.ShowReferenceOverlay
            && tab.ReferenceCarIndex >= 0
            && tab.ReferenceCarIndex < BackendGameSparker.cars[Collection.NFMM].Count)
        {
            // TODO optimize by caching reference car object instead of recreating each frame
            var referenceCar = new StaticMeshObject(BackendGameSparker.cars[Collection.NFMM][tab.ReferenceCarIndex]);

            // Store original state
            var originalRefPosition = referenceCar.Position;
            var originalRefRotation = referenceCar.Rotation;

            // Position reference car at same location as main model
            referenceCar.Position = tab.ModelPosition;
            referenceCar.Rotation = new f64Euler(
                f64AngleSingle.FromDegrees(tab.ModelRotation.Y),
                f64AngleSingle.FromDegrees(-tab.ModelRotation.X),
                f64AngleSingle.FromDegrees(tab.ModelRotation.Z)
            );

            // Alpha blending + no depth test/write, so the reference car always renders in front:
            // the pipeline carries that state (see Effects.PolyNoDepthPipeline), and the depth
            // buffer is cleared first so nothing else occludes it either.
            referenceCar.Mesh.OverlayMode = PolyOverlayMode.NoDepth;
            cb.Clear(NFMWorld.Graphics.ClearOptions.Depth, default, 1f, 0);

            referenceCar.AlphaOverride = tab.ReferenceOpacity;

            overlayScene.Objects.Clear();
            overlayScene.Objects.Add(referenceCar);
            overlayScene.Render(cb, 1, false, clearRenderBuffer: false);

            referenceCar.Mesh.OverlayMode = PolyOverlayMode.None;
            referenceCar.Position = originalRefPosition;
            referenceCar.Rotation = originalRefRotation;
        }

        // Render selected polygon overlay with transparency
        if (tab.EditMode == ModelEditorTab.EditModeEnum.Polygon)
        {
            if (tab.SelectedWheelIndex >= 0
                && tab.SelectedWheelPolygonIndex >= 0
                && tab.SelectedWheelIndex < tab.Object.WheelObjects.Count)
            {
                RenderWheelSelectionOverlay(cb, tab);
            }
            else if (tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex < tab.Object.Mesh.Polys.Length)
            {
                RenderSelectionOverlay(cb, tab);
            }
        }

        // Render selected collision box overlay
        if (tab.EditMode == ModelEditorTab.EditModeEnum.Collision
            && tab.SelectedCollisionIndex >= 0
            && tab.SelectedCollisionIndex < tab.Object.Boxes.Count)
        {
            RenderCollisionSelectionOverlay(cb, camera, tab);
        }

        // Highlight the wheel being edited in the Wheels tab.
        if (_view is { WheelsTabActive: true }
            && _view.HighlightedWheelIndex is var wheelIndex
            && wheelIndex >= 0
            && wheelIndex < tab.Object.WheelObjects.Count)
        {
            RenderWheelHighlight(cb, tab, wheelIndex);
        }
        else
        {
            DisposeWheelHighlight();
        }

        // Restore original transform
        tab.Object.Position = originalPosition;
        tab.Object.Rotation = originalRotation;
    }

    private void RenderWheelHighlight(NFMWorld.Graphics.ICommandBuffer cb, ModelEditorTab tab, int index)
    {
        var wheel = tab.Object!.WheelObjects[index];
        if (_wheelHighlight is null || _wheelHighlightOwner != tab.Object || _wheelHighlightIndex != index)
        {
            DisposeWheelHighlight();

            var polys = wheel.Mesh.Polys
                .Select(poly => poly with { Color = new Color3(255, 40, 220), PolyType = PolyType.Flat })
                .ToArray();

            _wheelHighlight = new MeshedGameObject(new Mesh(new Rad3d(polys, false, "wheelHighlight")))
            {
                Parent = wheel,
                Position = f64Vector3.Zero,
                AlphaOverride = 0.45f
            };
            _wheelHighlight.Mesh.OverlayMode = PolyOverlayMode.NoDepth;
            _wheelHighlightOwner = tab.Object;
            _wheelHighlightIndex = index;
        }

        overlayScene.Objects.Clear();
        overlayScene.Objects.Add(_wheelHighlight);
        overlayScene.Render(cb, 1, false, clearRenderBuffer: false);
        overlayScene.Objects.Clear();
    }

    private void DisposeWheelHighlight()
    {
        overlayScene.Objects.Remove(_wheelHighlight);
        _wheelHighlight?.Mesh.Dispose();
        _wheelHighlight = null;
        _wheelHighlightOwner = null;
        _wheelHighlightIndex = -1;
    }

    private void RenderSelectionOverlay(NFMWorld.Graphics.ICommandBuffer cb, ModelEditorTab tab)
    {
        if (tab.Object == null
            || tab.SelectedPolygonIndex < 0
            || tab.SelectedPolygonIndex >= tab.Object.Mesh.Polys.Length)
            return;

        var obj = tab.Object;
        var index = tab.SelectedPolygonIndex;
        var selectedPoly = obj.Mesh.Polys[index];
        var geometryHash = SelectionGeometryHash(selectedPoly);

        if (_selectionOverlay is null
            || _selectionOverlayOwner != obj
            || _selectionOverlayPolygon != index
            || _selectionOverlayGeometryHash != geometryHash)
        {
            DisposeSelectionOverlay();

            _selectionOverlay = new MeshedGameObject(CreateSelectionMesh(selectedPoly))
            {
                Glow = true
            };
            _selectionOverlay.Mesh.OverlayMode = PolyOverlayMode.DepthRead;
            _selectionOverlayOwner = obj;
            _selectionOverlayPolygon = index;
            _selectionOverlayGeometryHash = geometryHash;
        }

        _selectionOverlay.Position = obj.Position;
        _selectionOverlay.Rotation = obj.Rotation;

        overlayScene.Objects.Clear();
        overlayScene.Objects.Add(_selectionOverlay);
        overlayScene.Render(cb, 1, false, clearRenderBuffer: false);
        overlayScene.Objects.Clear();
    }

    private void DisposeSelectionOverlay()
    {
        if (_selectionOverlay is { } overlay)
        {
            overlayScene.Objects.Remove(overlay);
            overlay.Mesh.Dispose();
        }

        _selectionOverlay = null;
        _selectionOverlayOwner = null;
        _selectionOverlayPolygon = -1;
        _selectionOverlayGeometryHash = -1;
    }

    private static int SelectionGeometryHash(Rad3dPoly poly)
    {
        var hash = new HashCode();
        hash.Add(poly.Color);
        hash.Add(poly.DecalOffset);
        foreach (var point in poly.Points)
            hash.Add(point);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Unlit dark-red fill plus a line-only full-red perimeter. The overlay object is
    /// marked Glow, which makes both the polygon and line shaders fullbright and
    /// brightens the line.
    /// </summary>
    private static Mesh CreateSelectionMesh(Rad3dPoly selectedPoly)
    {
        // One pair offset each way along the normal. A single offset is in front only
        // on the side its normal points at; from the back the original surface hides it.
        Rad3dPoly Fill(float decalOffset) => selectedPoly with
        {
            Color = new Color3(110, 0, 0),
            PolyType = PolyType.Flat,
            LineType = null,
            DecalOffset = decalOffset
        };

        Rad3dPoly Outline(float decalOffset) => selectedPoly with
        {
            Color = new Color3(255, 0, 0),
            PolyType = PolyType.Flat,
            LineType = LineType.BrightColored,
            Triangles = [],
            DecalOffset = decalOffset
        };

        return new Mesh(
            new Rad3d(
                [
                    Fill(selectedPoly.DecalOffset - 0.1f),
                    Fill(selectedPoly.DecalOffset + 0.1f),
                    Outline(selectedPoly.DecalOffset - 0.1f),
                    Outline(selectedPoly.DecalOffset + 0.1f)
                ],
                false,
                "selectionOverlay"
            )
        );
    }

    private void RenderWheelSelectionOverlay(NFMWorld.Graphics.ICommandBuffer cb, ModelEditorTab tab)
    {
        if (tab.Object == null || tab.SelectedWheelIndex < 0 || tab.SelectedWheelPolygonIndex < 0) return;
        if (tab.SelectedWheelIndex >= tab.Object.WheelObjects.Count) return;

        var wheelObj = tab.Object.WheelObjects[tab.SelectedWheelIndex];
        if (tab.SelectedWheelPolygonIndex >= wheelObj.Mesh.Polys.Length) return;

        var index = tab.SelectedWheelPolygonIndex;
        var selectedPoly = wheelObj.Mesh.Polys[index];
        var geometryHash = SelectionGeometryHash(selectedPoly);

        if (_wheelSelectionOverlay is null
            || _wheelSelectionOverlayParent != wheelObj
            || _wheelSelectionOverlayPolygon != index
            || _wheelSelectionOverlayGeometryHash != geometryHash)
        {
            DisposeWheelSelectionOverlay();

            _wheelSelectionOverlay = new MeshedGameObject(CreateSelectionMesh(selectedPoly))
            {
                Position = f64Vector3.Zero,
                Parent = wheelObj,
                Glow = true
            };
            _wheelSelectionOverlay.Mesh.OverlayMode = PolyOverlayMode.DepthRead;
            _wheelSelectionOverlayParent = wheelObj;
            _wheelSelectionOverlayPolygon = index;
            _wheelSelectionOverlayGeometryHash = geometryHash;
        }

        overlayScene.Objects.Clear();
        overlayScene.Objects.Add(_wheelSelectionOverlay);
        overlayScene.Render(cb, 1, false, clearRenderBuffer: false);
        overlayScene.Objects.Clear();
    }

    private void DisposeWheelSelectionOverlay()
    {
        if (_wheelSelectionOverlay is { } overlay)
        {
            overlayScene.Objects.Remove(overlay);
            overlay.Mesh.Dispose();
        }

        _wheelSelectionOverlay = null;
        _wheelSelectionOverlayParent = null;
        _wheelSelectionOverlayPolygon = -1;
        _wheelSelectionOverlayGeometryHash = -1;
    }

    private void RenderCollisionSelectionOverlay(
        NFMWorld.Graphics.ICommandBuffer cb,
        PerspectiveCamera camera,
        ModelEditorTab tab
    )
    {
        if (tab.Object == null || tab.SelectedCollisionIndex < 0) return;

        // Create a highlighted collision box mesh for the selected collision
        var selectedBox = tab.Object.Boxes[tab.SelectedCollisionIndex];
        var highlightedBox = selectedBox with
        {
            Color = new Color3(255, 255, 0) // Yellow highlight
        };

        var highlightBoxes = new[] { highlightedBox };
        using var highlightMesh = new CollisionDebugMesh(highlightBoxes);

        // Match the main model's transform. Unlike the polygon/wheel overlays - which render through
        // overlayScene and therefore have Scene.Render call OnBeforeRender for them - this one is
        // drawn directly, so it has to be driven by hand: MatrixWorld is only computed in
        // OnBeforeRender (see Transform.MatrixWorld), and without that it stays the default all-zero
        // matrix and the box collapses to a point. The *WithoutInterpolation setters set the previous
        // state too, so OnBeforeRender(alpha) lands on exactly this transform whatever it blends.
        highlightMesh.PositionWithoutInterpolation = tab.Object.Position;
        highlightMesh.RotationWithoutInterpolation = tab.Object.Rotation;
        highlightMesh.OnBeforeRender(1f);

        // Render with highlighting
        var oldDevRenderTrackers = GameSparker.devRenderTrackers;
        GameSparker.devRenderTrackers = true;
        highlightMesh.Render(cb, camera, null);
        GameSparker.devRenderTrackers = oldDevRenderTrackers;
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);

        camera.Width = width;
        camera.Height = height;
    }
}
