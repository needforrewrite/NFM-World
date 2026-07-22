using System.Collections.ObjectModel;
using Hexa.NET.ImGui;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorld.UI;

// Custom Stage class for the editor that doesn't require loading from file
public class EditorStage : BackendStage
{
    public EditorStage()
    {
        // Initialize with default settings for an empty stage
        World.ResetValues();
    }
}

// Class to represent a stage piece instance in the scene
public class StagePieceInstance
{
    public PiecePlacement PiecePlacement
    {
        get;
        set
        {
            field = value;
            Obj.Position = value.Position;
            Obj.Rotation = value.Rotation;
        }
    }

    public string Name => PiecePlacement.Object.FileName;
    public StageObject Obj { get; }

    public Rad3d Rad => PiecePlacement.Object;

    public f64Vector3 Position
    {
        get => PiecePlacement.Position;
        set
        {
            PiecePlacement = PiecePlacement with { Position = value };
            Obj.Position = PiecePlacement.Position;
        }
    }

    public f64Euler Rotation
    {
        get => PiecePlacement.Rotation;
        set
        {
            PiecePlacement = PiecePlacement with { Rotation = value };
            Obj.Rotation = PiecePlacement.Rotation;
        }
    }

    public int Id { get; set; }

    public StagePieceInstance(StageObject obj, int id)
    {
        Obj = obj;
        PiecePlacement = obj.OriginalPlacement;
        Id = id;
    }
}

// Class to represent stage wall borders
public class EditorStageWall(WallDirection direction, int count, int position, int offset, int id)
{
    private StageWall wallDef = new(direction, count, position, offset);

    public WallDirection Direction
    {
        get => wallDef.Direction;
        set => wallDef = wallDef with { Direction = value };
    }

    public int Count
    {
        get => wallDef.Count;
        set => wallDef = wallDef with { Count = value };
    }
    public int Position
    {
        get => wallDef.Position;
        set => wallDef = wallDef with { Position = value };
    }
    public int Offset
    {
        get => wallDef.Offset;
        set => wallDef = wallDef with { Offset = value };
    }

    public int Id { get; set; } = id;

    public string GetDisplayName()
    {
        return Direction switch
        {
            WallDirection.Right => "Border Right",
            WallDirection.Left => "Border Left",
            WallDirection.Top => "Border Top",
            WallDirection.Bottom => "Border Bottom",
            _ => "Border"
        };
    }
}

// Editor-only group for organizing pieces in the hierarchy — no gameplay effect
public class HierarchyGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "Group";
    public List<int> PieceIds { get; set; } = new(); // editor-assigned piece IDs
    public bool IsExpanded { get; set; } = true;
}

// Main viewport tab for the stage editor
public class StageEditorTab
{
    public string TabName { get; set; } = "Stage";
    public KeyedCollection<int, StagePieceInstance> ScenePieces { get; set; } = KeyedCollection.From<int, StagePieceInstance>(p => p.Id);
    public KeyedCollection<int, EditorStageWall> StageWalls { get; set; } = KeyedCollection.From<int, EditorStageWall>(p => p.Id);
    public List<MeshedGameObject> WallMeshes { get; set; } = []; // Visual representation of walls
    public List<string> UnknownParameters { get; set; } = []; // Unknown/unhandled stage parameters to preserve
    // Known directives the editor does not currently expose; keep raw lines for round-trip safety.
    public List<string> PreservedDirectives { get; set; } = [];
    // Editor-only groups for hierarchy organisation (saved as metadata comments in stage file)
    public KeyedCollection<int,HierarchyGroup> HierarchyGroups { get; set; } = KeyedCollection.From<int, HierarchyGroup>(g => g.Id);
    private int _nextGroupId = 0;
    public int GetNextGroupId() => _nextGroupId++;
    // Where the "Ungrouped" section appears relative to the group list (-1 = after all groups)
    public int UngroupedOrderIndex { get; set; } = -1;
    // Camera/view controls
    public Vector3 CameraPosition { get; set; } = new Vector3(0, -300, -1500);
    public float CameraYaw { get; set; } = 0f;
    public float CameraPitch { get; set; } = -10f;
    public float CameraDistance { get; set; } = 1000f;
    public float TopDownHeight { get; set; } = 2000f;
    public Vector3 TopDownPanPosition { get; set; } = Vector3.Zero;
    public bool TopDownOrtho { get; set; } = false;

    // Mouse drag state for camera control
    public bool IsDragging { get; set; } = false;
    public int DragStartX { get; set; } = 0;
    public int DragStartY { get; set; } = 0;
    public float DragStartCameraYaw { get; set; } = 0f;
    public float DragStartCameraPitch { get; set; } = 0f;

    // Selection state
    public int ActivePieceId { get; set; } = -1;
    public int SelectedWallId { get; set; } = -1;
    public HashSet<int> SelectedPieceIds { get; set; } = new(); // multi-selection set

    // View mode
    public enum ViewModeEnum { Scene, TopDown }
    public ViewModeEnum ViewMode { get; set; } = ViewModeEnum.Scene;

    // Associated stage and scene
    public BackendStage? Stage { get; set; }
    public ClientStageRenderer? StageRenderer { get; set; }
    public Scene? Scene { get; set; }
    public string? StageFileName { get; set; }
    public bool HasUnsavedChanges { get; set; } = false;

    // Stage properties (stored per tab)
    public Color3 SkyColor { get; set; } = new(135, 206, 235);
    public Color3 FogColor { get; set; } = new(135, 206, 235);
    public Color3 GroundColor { get; set; } = new(100, 200, 100);
    public Color3 PolysColor { get; set; } = new(215, 210, 210);
    public bool PolysEnabled { get; set; } = false;
    public bool CloudsEnabled { get; set; } = false;
    public Color3 CloudsColor { get; set; } = new(210, 210, 210);
    public int CloudsParam4 { get; set; } = 1;
    public int CloudsHeight { get; set; } = -1000;
    public float CloudCoverage { get; set; } = 1.0f;
    public bool MountainsEnabled { get; set; } = false;
    public int MountainsSeed { get; set; } = 0;
    public int SnapA { get; set; } = 0;
    public int SnapB { get; set; } = 0;
    public int SnapC { get; set; } = 0;
    public int FadeFrom { get; set; } = 10000;

    public int GetNextPieceId()
    {
        int maxId = -1;
        foreach (var piece in ScenePieces)
        {
            if (piece.Id > maxId)
                maxId = piece.Id;
        }
        foreach (var wall in StageWalls)
        {
            if (wall.Id > maxId)
                maxId = wall.Id;
        }
        return maxId + 1;
    }
}

public partial class StageEditorPhase : BasePhase
{
    private readonly GraphicsDevice _graphicsDevice;
    private bool _isOpen = false;

    // Tab management
    private List<StageEditorTab> _tabs = new();
    private int _activeTabIndex = -1;

    // Available stage parts
    private KeyedCollection<string, Rad3d> _availableParts = KeyedCollection.From<string, Rad3d>(p => p.FileName);

    // Active tab property
    private StageEditorTab? ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;

    // Viewport bounds for scissor testing
    private Vector2 _viewportMin;
    private Vector2 _viewportMax;

    // UI state
    private float _hierarchyWidth = 250f;
    private float _inspectorWidth = 300f;
    private float _partsLibraryHeight = 280f;

    // Mouse state
    private int _mouseX;
    private int _mouseY;
    private bool _isLeftButtonDown = false;
    private bool _isRightButtonDown = false;
    private bool _isShiftPressed = false;
    private bool _isRightDragging = false;
    private int _rightDragStartX = 0;
    private int _rightDragStartY = 0;
    private float _rightDragStartYaw = 0f;
    private float _rightDragStartPitch = 0f;

    // Camera movement state
    private bool _moveForward = false;
    private bool _moveBackward = false;
    private bool _moveLeft = false;
    private bool _moveRight = false;
    private bool _moveUp = false;
    private bool _moveDown = false;
    private const float CAMERA_MOVE_SPEED = 50f;

    // 3D Camera
    public static OrthoCamera orthoCamera = new();
    public static PerspectiveCamera perspectiveCamera = new();
    public static Camera activeCamera = perspectiveCamera;

    // Placement mode: user selects a part from the library then clicks in the viewport to place it
    private int _pendingPlacementPartIndex = -1; // index into _availableParts; -1 = not in placement mode
    private f64Vector3 _pendingPlacementPos = f64Vector3.Zero;
    private bool _hasValidPlacementPos = false;
    private float _pendingPlacementYaw = 0f;  // degrees, modified by Q/E while in placement mode
    private int _pendingPlacementYOff = 0;

    // Auto-update stage walls when pieces are placed/moved
    private bool _autoUpdateWalls = false;

    // Auto-generate ground polys mesh (disable for performance with large stages)
    private bool _autoGeneratePolys = true;

    // Snapping
    private bool _snapEnabled = false;

    // Grid Snapping
    private bool _gridSnapEnabled = false;
    private float _gridSnapSize = 100f; // world units; standard road spacing is 5600
    // Preset snap sizes (shown as labels in the UI)
    private static readonly float[] SnapPresets = [50f, 100f, 200f, 400f, 560f, 1000f, 2800f, 5600f];
    private int _snapPresetIndex = 0;

    // New stage dialog state
    private bool _showNewStageDialog = false;
    private string _newStageName = "";
    private int _newStageStartPartIndex = 0; // index into _newStageStartPartOptions
    private static readonly string[] _newStageStartPartOptions =
    {
        "(none)",
        "nfmm/road",
        "nfmm/offroad",
    };

    // Load stage dialog state
    private bool _showLoadStageDialog = false;
    private List<string> _availableStages = new();
    private int _selectedStageIndex = -1;

    // Properties dialog state
    private bool _showPropertiesDialog = false;
    private string _editStageName = "";
    private Color3 _editSkyColor = new(135, 206, 235);
    private Color3 _editFogColor = new(135, 206, 235);
    private Color3 _editGroundColor = new(100, 200, 100);
    private Color3 _editPolysColor = new(215, 210, 210);
    private bool _editPolysEnabled = false;
    private bool _editCloudsEnabled = false;
    private Color3 _editCloudsColor = new(210, 210, 210);
    private int _editCloudsParam4 = 1;
    private int _editCloudsHeight = -1000;
    private float _editCloudCoverage = 1.0f;
    private bool _editMountainsEnabled = false;
    private int _editMountainsSeed = 0;
    private int _editSnapA = 0;
    private int _editSnapB = 0;
    private int _editSnapC = 0;
    private int _editFadeFrom = 10000;

    // Unsaved changes warning dialogs
    private bool _showExitWarningDialog = false;
    private bool _showCloseTabWarningDialog = false;
    private int _tabToClose = -1;

    // Export top-down image
    private bool _showExportDialog = false;
    private int _exportWidth = 1024;
    private int _exportHeight = 1024;
    private int _exportPadding = 500;
    private string _exportResultMessage = "";

    // Hierarchy panel search
    private string _hierarchySearch = "";

    // Parts library state
    private string _partsSearch = "";
    private int _partsCategory = 0; // 0=All, 1=nfmm, 2=vendor, 3=user

    // Swap piece mode
    private bool _isSwapMode = false;

    // Part preview thumbnails: FileName -> (RenderTarget, ImGui texture ref)
    private readonly Dictionary<string, (RenderTarget2D RT, ImTextureRef Ref)> _partPreviews = new();
    private readonly Queue<(string Name, Rad3d Rad)> _previewQueue = new();
    private const int PreviewSize = 64;

    // Gizmo state
    private GizmoAxis _gizmoHovered = GizmoAxis.None;
    private GizmoAxis _gizmoDragging = GizmoAxis.None;
    private int _gizmoDragStartX;
    private int _gizmoDragStartY;
    private float _gizmoDragStartPosX;
    private float _gizmoDragStartPosY;
    private float _gizmoDragStartPosZ;
    private float _gizmoDragStartRotY;
    // Centroid of the selection at drag start (used for rotation pivot and axis projection)
    private float _gizmoCentroidX, _gizmoCentroidY, _gizmoCentroidZ;
    // Start positions of ALL selected pieces at gizmo drag start (id -> position/rotY)
    private Dictionary<int, f64Vector3> _gizmoDragStartPositions = new();
    private Dictionary<int, float> _gizmoDragStartRotations = new();
    private const float WALL_SEGMENT_SPACING = 4800f;
    private const float WALL_SEGMENT_HALF_LENGTH = WALL_SEGMENT_SPACING * 0.5f;
    private const float WALL_SEGMENT_HALF_WIDTH = 450f;
    private const float WALL_SEGMENT_HALF_HEIGHT = 700f;

    // Undo / Redo
    private readonly record struct PieceSnapshot(PiecePlacement Piece, StageObject Obj, int Id);
    private readonly record struct WallSnapshot(int Id, WallDirection Direction, int Count, int Position, int Offset);
    private readonly record struct EditorSnapshot(List<PieceSnapshot> Pieces, List<WallSnapshot> Walls);
    private readonly Stack<EditorSnapshot> _undoStack = new();
    private readonly Stack<EditorSnapshot> _redoStack = new();
    private bool _isCtrlPressed = false;

    // Inspector drag state tracking for undo/redo
    private bool _inspectorPosDragging = false;
    private bool _inspectorRotDragging = false;
    private bool _inspectorWallDragging = false;

    // Hierarchy drag-reorder state
    private int _hierDragSourceId = -1;

    // Rectangle selection state (viewport LMB drag)
    private bool _isRectSelecting = false;
    private int _rectSelectStartX, _rectSelectStartY;
    private int _rectSelectEndX, _rectSelectEndY;

    // Hierarchy group context menu state
    private int _groupContextMenuGroupId = -1;
    private string _renameGroupBuffer = "";
    private bool _showRenameGroupDialog = false;

    private static readonly string[] _preservedDirectivePrefixes =
    [
        "soundtrack(",
        "soundtrackremaster(",
        "soundtrackfreqmul(",
        "soundtracktempomul(",
        "density(",
        "distfog(",
        "lightson(",
        "mountaincoverage(",
        "lightdir(",
        "modeloffset(",
        "swapRotY(",
        "reverseChkY(",
        "nlaps(",
        "stagemaker(",
        "publish("
    ];

    // Copy/paste clipboard: stores (name, relativePos, rotation, type, tags, rad)
    private readonly record struct ClipboardPiece(
        Rad3d Rad,
        f64Vector3 RelativePosition,
        f64Euler Rotation,
        PiecePlacementType PlacementType,
        AiNodeKind? AiNodeKind,
        bool IsSpecial);

    private List<ClipboardPiece> _clipboard = new();

    public StageEditorPhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        RefreshAvailableParts();
    }

    private void RefreshAvailableParts()
    {
        _availableParts.Clear();

        // Add all stage parts from the loaded collections
        foreach (var part in BackendGameSparker.stage_parts)
        {
            _availableParts.TryAdd(part);
        }

        foreach (var part in BackendGameSparker.vendor_stage_parts)
        {
            _availableParts.TryAdd(part);
        }

        foreach (var part in BackendGameSparker.user_stage_parts)
        {
            _availableParts.TryAdd(part);
        }
    }

    public bool IsOpen => _isOpen;

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        var tab = _tabs[index];

        if (tab.HasUnsavedChanges)
        {
            _tabToClose = index;
            _showCloseTabWarningDialog = true;
        }
        else
        {
            PerformCloseTab(index);
        }
    }

    private void PerformCloseTab(int index)
    {
        _tabs.RemoveAt(index);

        if (_tabs.Count == 0)
        {
            _activeTabIndex = -1;
        }
        else if (_activeTabIndex >= _tabs.Count)
        {
            _activeTabIndex = _tabs.Count - 1;
        }
    }

    public override void Enter()
    {
        base.Enter();

        _isOpen = true;

        // Clear stale shadow maps left over from any previous gameplay session.
        // Scene.RenderInternal always passes Program.shadowRenderTargets to the shader,
        // so old shadow data would bleed into the editor if not wiped here.
        foreach (var rt in WorldGame.ShadowRenderTargets)
        {
            _graphicsDevice.SetRenderTarget(rt);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1.0f, 0);
        }
        _graphicsDevice.SetRenderTarget(null);

        // Initialize camera
        perspectiveCamera.Fov = 60f;
        perspectiveCamera.Width = GameSparker.Game.GraphicsDevice.Viewport.Width;
        perspectiveCamera.Height = GameSparker.Game.GraphicsDevice.Viewport.Height;

        orthoCamera.Width = GameSparker.Game.GraphicsDevice.Viewport.Width;
        orthoCamera.Height = GameSparker.Game.GraphicsDevice.Viewport.Height;

        UpdateCameraPosition();

        Logging.Debug("Stage Editor opened");
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);

        perspectiveCamera.Width = width;
        perspectiveCamera.Height = height;

        orthoCamera.Width = width;
        orthoCamera.Height = height;

        UpdateCameraPosition();
    }

    public override void Exit()
    {
        base.Exit();
        _isOpen = false;

        // Restore walls to all stages before exiting so they appear in gameplay
        foreach (var tab in _tabs)
        {
            if (tab.Stage != null && tab.StageWalls.Count > 0)
            {
                var wallPart = BackendGameSparker.GetStagePart("nfmm/thewall");
                if (wallPart.Rad != null)
                {
                    foreach (var wall in tab.StageWalls)
                    {
                        var n = wall.Count;
                        var o = wall.Position;
                        var p = wall.Offset;

                        for (int q = 0; q < n; q++)
                        {
                            f64Vector3 position;
                            f64Euler rotation;

                            switch (wall.Direction)
                            {
                                case WallDirection.Right:
                                    position = new f64Vector3(o, World.Ground, q * 4800 + p);
                                    rotation = f64Euler.Identity;
                                    break;
                                case WallDirection.Left:
                                    position = new f64Vector3(o, World.Ground, q * 4800 + p);
                                    rotation = new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                                    break;
                                case WallDirection.Top:
                                    position = new f64Vector3(q * 4800 + p, World.Ground, o);
                                    rotation = new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                                    break;
                                case WallDirection.Bottom:
                                    position = new f64Vector3(q * 4800 + p, World.Ground, o);
                                    rotation = new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                                    break;
                                default:
                                    position = f64Vector3.Zero;
                                    rotation = f64Euler.Identity;
                                    break;
                            }

                            tab.Stage.pieces.Add(StageObject.CreateDefaultObject(wallPart.Rad, position, rotation, isWall: true));
                        }
                    }
                }
            }

            // Clear wall meshes to prevent them from appearing when re-entering the editor
            tab.WallMeshes.Clear();
        }

        _tabs.Clear();
        _activeTabIndex = -1;
        Logging.Debug("Stage Editor closed");
    }

}
