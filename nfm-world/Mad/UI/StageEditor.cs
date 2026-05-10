using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Gameplay;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using Environment = NFMWorld.Environment;

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
    public enum PieceTypeEnum { Set, Chk, Fix, Wall }
    
    public string Name { get; set; } = "";
    public StageObject? Obj { get; set; }
    public f64Vector3 Position { get; set; } = f64Vector3.Zero;
    public f64Vector3 Rotation { get; set; } = f64Vector3.Zero;
    public int Id { get; set; }
    public PieceTypeEnum PieceType { get; set; } = PieceTypeEnum.Set;
    public string Tags { get; set; } = ""; // AI waypoint tags like p, pr, pt, ph, etc.
    
    public StagePieceInstance(string name, StageObject? obj, int id)
    {
        Name = name;
        Obj = obj;
        Id = id;
    }
}

// Class to represent stage wall borders
public class StageWall
{
    public enum WallDirection { Right, Left, Top, Bottom }
    
    public WallDirection Direction { get; set; }
    public int Count { get; set; } // n parameter
    public int Position { get; set; } // o parameter
    public int Offset { get; set; } // p parameter
    public int Id { get; set; }
    
    public StageWall(WallDirection direction, int count, int position, int offset, int id)
    {
        Direction = direction;
        Count = count;
        Position = position;
        Offset = offset;
        Id = id;
    }
    
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
    public List<StagePieceInstance> ScenePieces { get; set; } = new();
    public List<StageWall> StageWalls { get; set; } = new();
    public List<MeshedGameObject> WallMeshes { get; set; } = new(); // Visual representation of walls
    public List<string> UnknownParameters { get; set; } = new(); // Unknown/unhandled stage parameters to preserve
    // Editor-only groups for hierarchy organisation (saved as metadata comments in stage file)
    public List<HierarchyGroup> HierarchyGroups { get; set; } = new();
    private int _nextGroupId = 0;
    public int GetNextGroupId() => _nextGroupId++;
    // Where the "Ungrouped" section appears relative to the group list (-1 = after all groups)
    public int UngroupedOrderIndex { get; set; } = -1;
    // Temporary storage during LoadStage for raw group key strings before piece resolution
    public List<List<string>> _pendingGroupKeys = new();
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
    public int SelectedPieceId { get; set; } = -1;
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
    public System.Numerics.Vector3 SkyColor { get; set; } = new(135, 206, 235);
    public System.Numerics.Vector3 FogColor { get; set; } = new(135, 206, 235);
    public System.Numerics.Vector3 GroundColor { get; set; } = new(100, 200, 100);
    public System.Numerics.Vector3 PolysColor { get; set; } = new(215, 210, 210);
    public bool PolysEnabled { get; set; } = false;
    public bool CloudsEnabled { get; set; } = false;
    public System.Numerics.Vector3 CloudsColor { get; set; } = new(210, 210, 210);
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

public class StageEditorPhase : BasePhase
{
    private readonly GraphicsDevice _graphicsDevice;
    private bool _isOpen = false;
    
    // Tab management
    private List<StageEditorTab> _tabs = new();
    private int _activeTabIndex = -1;
    
    // Available stage parts
    private List<(string Name, Rad3d? Rad)> _availableParts = new();
    
    // Active tab property
    private StageEditorTab? ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;
    
    // Viewport bounds for scissor testing
    private System.Numerics.Vector2 _viewportMin;
    private System.Numerics.Vector2 _viewportMax;
    
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
    public static PerspectiveCamera camera = new();
    
    // Drag and drop state
    private int _draggedPartIndex = -1;
    private bool _isDraggingFromLibrary = false;
    
    // Placement mode: user selects a part from the library then clicks in the viewport to place it
    private int _pendingPlacementPartIndex = -1; // index into _availableParts; -1 = not in placement mode
    private f64Vector3 _pendingPlacementPos = f64Vector3.Zero;
    private bool _hasValidPlacementPos = false;
    private float _pendingPlacementYaw = 0f;  // degrees, modified by Q/E while in placement mode
    
    // Snapping
    private bool _snapEnabled = false;
    private float _snapSize = 100f; // world units; standard road spacing is 5600
    // Preset snap sizes (shown as labels in the UI)
    private static readonly float[] SnapPresets = { 50f, 100f, 200f, 400f, 560f, 1000f, 2800f, 5600f };
    private int _snapPresetIndex = 0;
    
    // New stage dialog state
    private bool _showNewStageDialog = false;
    private string _newStageName = "";
    private string _stageFileName = "";
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
    private System.Numerics.Vector3 _editSkyColor = new(135, 206, 235);
    private System.Numerics.Vector3 _editFogColor = new(135, 206, 235);
    private System.Numerics.Vector3 _editGroundColor = new(100, 200, 100);
    private System.Numerics.Vector3 _editPolysColor = new(215, 210, 210);
    private bool _editPolysEnabled = false;
    private bool _editCloudsEnabled = false;
    private System.Numerics.Vector3 _editCloudsColor = new(210, 210, 210);
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
    private enum GizmoAxis { None, X, Y, Z, RotY }
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
    private const float GIZMO_ARROW_LENGTH = 600f;
    private const float GIZMO_ARROW_THICKNESS = 12f;
    private const float GIZMO_ROT_RADIUS = 400f;
    
    // Undo / Redo
    private record PieceSnapshot(string Name, f64Vector3 Position, f64Vector3 Rotation, int Id,
        StagePieceInstance.PieceTypeEnum PieceType, string Tags, StageObject? Obj);
    private readonly Stack<List<PieceSnapshot>> _undoStack = new();
    private readonly Stack<List<PieceSnapshot>> _redoStack = new();
    private bool _isCtrlPressed = false;
    
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
    
    // Copy/paste clipboard: stores (name, relativePos, rotation, type, tags, rad)
    private record ClipboardPiece(string Name, f64Vector3 RelPos, f64Vector3 Rotation,
        StagePieceInstance.PieceTypeEnum PieceType, string Tags, Rad3d? Rad);
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
            _availableParts.Add((part.FileName, part));
        }
        
        foreach (var part in BackendGameSparker.vendor_stage_parts)
        {
            _availableParts.Add((part.FileName, part));
        }
        
        foreach (var part in BackendGameSparker.user_stage_parts)
        {
            _availableParts.Add((part.FileName, part));
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
        _isOpen = true;

        // Clear stale shadow maps left over from any previous gameplay session.
        // Scene.RenderInternal always passes Program.shadowRenderTargets to the shader,
        // so old shadow data would bleed into the editor if not wiped here.
        foreach (var rt in WorldGame.shadowRenderTargets)
        {
            _graphicsDevice.SetRenderTarget(rt);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.White, 1.0f, 0);
        }
        _graphicsDevice.SetRenderTarget(null);
        
        // Initialize camera
        camera.Fov = 60f;
        camera.Width = GameSparker._game.GraphicsDevice.Viewport.Width;
        camera.Height = GameSparker._game.GraphicsDevice.Viewport.Height;
        camera.Near = 1f;
        camera.Far = 100000f;
        
        UpdateCameraPosition();
        
        Logging.Debug("Stage Editor opened");
    }
    
    public override void Exit()
    {
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
                                case StageWall.WallDirection.Right:
                                    position = new f64Vector3(o, World.Ground, q * 4800 + p);
                                    rotation = f64Euler.Identity;
                                    break;
                                case StageWall.WallDirection.Left:
                                    position = new f64Vector3(o, World.Ground, q * 4800 + p);
                                    rotation = new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                                    break;
                                case StageWall.WallDirection.Top:
                                    position = new f64Vector3(q * 4800 + p, World.Ground, o);
                                    rotation = new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                                    break;
                                case StageWall.WallDirection.Bottom:
                                    position = new f64Vector3(q * 4800 + p, World.Ground, o);
                                    rotation = new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                                    break;
                                default:
                                    position = f64Vector3.Zero;
                                    rotation = f64Euler.Identity;
                                    break;
                            }
                            
                            tab.Stage.pieces.Add(new StageObject(wallPart.Rad, position, rotation));
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
    
    private void CreateEmptyStage(string stageName, string? startPartName = null)
    {
        // Create a new tab with empty stage
        var tab = new StageEditorTab();
        tab.TabName = stageName;
        tab.StageFileName = ConvertStageNameToFilename(stageName);
        tab.Stage = new EditorStage();
        tab.StageRenderer = new ClientStageRenderer(_graphicsDevice, tab.Stage);
        
        // Set default values for properties in the tab
        tab.SkyColor = new System.Numerics.Vector3(135, 206, 235);
        tab.FogColor = new System.Numerics.Vector3(135, 206, 235);
        tab.GroundColor = new System.Numerics.Vector3(100, 200, 100);
        tab.PolysColor = new System.Numerics.Vector3(90, 190, 90);
        tab.PolysEnabled = false;
        tab.CloudsEnabled = false;
        tab.CloudsColor = new System.Numerics.Vector3(210, 210, 210);
        tab.CloudsParam4 = 1;
        tab.CloudsHeight = -1000;
        tab.CloudCoverage = 1.0f;
        tab.MountainsEnabled = false;
        tab.MountainsSeed = 0;
        tab.SnapA = 0;
        tab.SnapB = 0;
        tab.SnapC = 0;
        tab.FadeFrom = 10000;
        
        // Also update World for immediate effect
        World.Sky = new Color3((short)tab.SkyColor.X, (short)tab.SkyColor.Y, (short)tab.SkyColor.Z);
        World.Fog = new Color3((short)tab.FogColor.X, (short)tab.FogColor.Y, (short)tab.FogColor.Z);
        World.GroundColor = new Color3((short)tab.GroundColor.X, (short)tab.GroundColor.Y, (short)tab.GroundColor.Z);
        World.FadeFrom = tab.FadeFrom;
        World.HasPolys = false;
        World.DrawPolys = false;
        World.HasClouds = false;
        World.DrawClouds = false;
        World.DrawMountains = false;
        World.Snap = new Color3(0, 0, 0);
        
        _tabs.Add(tab);
        _activeTabIndex = _tabs.Count - 1;
        
        // Place start piece at origin if specified
        if (!string.IsNullOrEmpty(startPartName))
        {
            var partData = BackendGameSparker.GetStagePart(startPartName);
            if (partData.Rad != null)
            {
                var startPos = new f64Vector3((fix64)0, (fix64)250, (fix64)0);
                var startMesh = new StageObject(partData.Rad, startPos, f64Euler.Identity);
                int partId = tab.GetNextPieceId();
                var instance = new StagePieceInstance(startPartName, startMesh, partId);
                instance.Position = startPos;
                instance.Rotation = new f64Vector3((fix64)0, (fix64)0, (fix64)0);
                tab.ScenePieces.Add(instance);
                tab.Stage.pieces.Add(startMesh);
                Logging.Info($"Placed start piece '{startPartName}' at origin.");
            }
            else
            {
                Logging.Warning($"Start piece '{startPartName}' could not be loaded (GetStagePart returned null Rad).");
            }
        }
        
        RebuildClientRenderer();
        SaveStage(); // Automatically save the new stage
        
        Logging.Info($"Created new stage: {stageName} (filename: {tab.StageFileName})");
    }
    
    private string ConvertStageNameToFilename(string stageName)
    {
        // Convert to lowercase and replace spaces with underscores
        return stageName.ToLower().Replace(' ', '_');
    }
    
    private void SaveStage()
    {
        if (ActiveTab == null || ActiveTab.Stage == null || string.IsNullOrWhiteSpace(ActiveTab.StageFileName))
        {
            Logging.Info("Cannot save: no stage loaded");
            return;
        }
        
        // Ensure the user stages directory exists
        var userStagesDir = "data/stages/user";
        Directory.CreateDirectory(userStagesDir);
        
        var filePath = $"{userStagesDir}/{ActiveTab.StageFileName}.txt";
        
        try
        {
            using var writer = new StreamWriter(filePath);
            
            // Write stage parameters from active tab's stored values
            writer.WriteLine($"name({ActiveTab.TabName})");
            writer.WriteLine($"sky({(int)ActiveTab.SkyColor.X},{(int)ActiveTab.SkyColor.Y},{(int)ActiveTab.SkyColor.Z})");
            writer.WriteLine($"fog({(int)ActiveTab.FogColor.X},{(int)ActiveTab.FogColor.Y},{(int)ActiveTab.FogColor.Z})");
            writer.WriteLine($"ground({(int)ActiveTab.GroundColor.X},{(int)ActiveTab.GroundColor.Y},{(int)ActiveTab.GroundColor.Z})");
            
            // Write polys parameter
            if (ActiveTab.PolysEnabled)
            {
                writer.WriteLine($"polys({(int)ActiveTab.PolysColor.X},{(int)ActiveTab.PolysColor.Y},{(int)ActiveTab.PolysColor.Z})");
            }
            else
            {
                writer.WriteLine("polys(false)");
            }
            
            // Write snap parameter
            writer.WriteLine($"snap({ActiveTab.SnapA},{ActiveTab.SnapB},{ActiveTab.SnapC})");
            
            // Write clouds parameter
            if (ActiveTab.CloudsEnabled)
            {
                writer.WriteLine($"clouds({(int)ActiveTab.CloudsColor.X},{(int)ActiveTab.CloudsColor.Y},{(int)ActiveTab.CloudsColor.Z},{ActiveTab.CloudsParam4},{ActiveTab.CloudsHeight})");
                writer.WriteLine($"cloudcoverage({ActiveTab.CloudCoverage})");
            }
            else
            {
                writer.WriteLine("clouds(false)");
            }
            
            // Write mountains parameter
            if (ActiveTab.MountainsEnabled)
            {
                writer.WriteLine($"mountains({ActiveTab.MountainsSeed})");
            }
            else
            {
                writer.WriteLine("mountains(false)");
            }
            
            writer.WriteLine($"fadefrom({ActiveTab.FadeFrom})");
            
            // Write unknown parameters
            foreach (var param in ActiveTab.UnknownParameters)
            {
                writer.WriteLine(param);
            }
            
            writer.WriteLine();
            
            // ── Build piece lists ──────────────────────────────────────────────────
            var allNonWall = ActiveTab.ScenePieces
                .Where(p => !p.Name.Contains("thewall") && p.PieceType != StagePieceInstance.PieceTypeEnum.Wall)
                .ToList();
            var groupedIds = new HashSet<int>(ActiveTab.HierarchyGroups.SelectMany(g => g.PieceIds));
            var ungroupedPieces = allNonWall.Where(p => !groupedIds.Contains(p.Id)).ToList();
            int ungroupedSlotSave = ActiveTab.UngroupedOrderIndex >= 0
                ? Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count)
                : ActiveTab.HierarchyGroups.Count; // default: after all groups
            
            // Local helper: serialise one piece as set/chk/fix
            void WritePiece(StagePieceInstance piece)
            {
                if (piece.Obj == null) return;
                var pos = piece.Position;
                var rot = piece.Rotation;
                string pieceId;
                int numericId = -1;
                if (piece.Name.StartsWith("nfmm/"))
                {
                    var baseName = piece.Name.Substring(5);
                    var idx = Array.IndexOf(BackendGameSparker.StageRads, baseName);
                    if (idx >= 0) { numericId = idx + 10; pieceId = numericId.ToString(); }
                    else pieceId = piece.Name;
                }
                else { pieceId = piece.Name; }
                int yCoord = (int)pos.Y;
                int rotX   = (int)rot.Y;
                if (piece.PieceType == StagePieceInstance.PieceTypeEnum.Fix)
                {
                    writer.WriteLine($"fix({pieceId},{(int)pos.X},{(int)pos.Z},{yCoord},{rotX})");
                }
                else if (piece.PieceType == StagePieceInstance.PieceTypeEnum.Chk)
                {
                    bool isAir = numericId == 64 || piece.Name.Contains("nfmm/aircheckpoint");
                    if (yCoord == 250) writer.WriteLine($"chk({pieceId},{(int)pos.X},{(int)pos.Z},{rotX})");
                    else { int fileY = isAir ? yCoord : 250 - yCoord; writer.WriteLine($"chk({pieceId},{(int)pos.X},{(int)pos.Z},{rotX},{fileY})"); }
                }
                else
                {
                    if (yCoord == 250) writer.WriteLine($"set({pieceId},{(int)pos.X},{(int)pos.Z},{rotX})");
                    else writer.WriteLine($"set({pieceId},{(int)pos.X},{(int)pos.Z},{rotX},{250 - yCoord})");
                }
            }
            
            // ── Write pieces in visual order ────────────────────────────────────────
            // New format: each group is preceded by  #editor_group(Name)
            // and its pieces appear immediately below.  Ungrouped pieces appear at
            // ungroupedSlotSave (0 = before all groups, groups.Count = after all groups).
            for (int slot = 0; slot <= ActiveTab.HierarchyGroups.Count; slot++)
            {
                if (slot == ungroupedSlotSave && ungroupedPieces.Count > 0)
                {
                    foreach (var piece in ungroupedPieces)
                        WritePiece(piece);
                    writer.WriteLine();
                }
                if (slot < ActiveTab.HierarchyGroups.Count)
                {
                    var group = ActiveTab.HierarchyGroups[slot];
                    var groupPieces = group.PieceIds
                        .Select(id => allNonWall.Find(p => p.Id == id))
                        .Where(p => p != null).ToList()!;
                    writer.WriteLine($"#editor_group({group.Name})");
                    foreach (var piece in groupPieces)
                        WritePiece(piece);
                    writer.WriteLine();
                }
            }
            
            // ── Stage walls ─────────────────────────────────────────────────────────
            if (ActiveTab.StageWalls.Count > 0)
            {
                foreach (var wall in ActiveTab.StageWalls)
                {
                    string command = wall.Direction switch
                    {
                        StageWall.WallDirection.Right  => "maxr",
                        StageWall.WallDirection.Left   => "maxl",
                        StageWall.WallDirection.Top    => "maxt",
                        StageWall.WallDirection.Bottom => "maxb",
                        _                              => "maxr"
                    };
                    writer.WriteLine($"{command}({wall.Count},{wall.Position},{wall.Offset})");
                }
                writer.WriteLine();
            }
            
            // ── Editor metadata (non-default ungrouped order) ────────────────────────
            if (ActiveTab.UngroupedOrderIndex >= 0)
                writer.WriteLine($"#editor_ungrouped_order({ActiveTab.UngroupedOrderIndex})");
            
            Logging.Info($"Stage saved to: {filePath}");
            ActiveTab.HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error saving stage: {ex.Message}");
        }
    }
    
    private void RefreshAvailableStages()
    {
        _availableStages.Clear();
        
        var userStagesDir = "data/stages/user";
        if (!Directory.Exists(userStagesDir))
        {
            return;
        }
        
        var files = Directory.GetFiles(userStagesDir, "*.txt");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            _availableStages.Add(fileName);
        }
        
        _availableStages.Sort();
    }
    
    private void LoadStage(string stageFileName)
    {
        // Check if this stage is already open in a tab
        foreach (var tab in _tabs)
        {
            if (tab.StageFileName == stageFileName)
            {
                // Stage already open, switch to that tab
                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i] == tab)
                    {
                        _activeTabIndex = i;
                        UpdateCameraPosition();
                        break;
                    }
                }
                Logging.Info($"Stage '{stageFileName}' is already open, switched to that tab.");
                return;
            }
        }
        
        try
        {
            // Create a new tab
            var tab = new StageEditorTab();
            tab.StageFileName = stageFileName;
            
            // Load the stage using the Stage class (it expects filename without extension)
            tab.Stage = new BackendStage($"user/{stageFileName}");
            tab.TabName = tab.Stage.Name;
            
            // Remove all wall pieces from the stage BEFORE creating the ClientStageRenderer
            // so it never includes them as children
            int removedCount = 0;
            for (int i = tab.Stage.pieces.Count - 1; i >= 0; i--)
            {
                var piece = tab.Stage.pieces[i];
                if (piece is StageObject collisionObject && (collisionObject.FileName == "thewall" || collisionObject.FileName.Contains("wall")))
                {
                    tab.Stage.pieces.RemoveAt(i);
                    removedCount++;
                }
            }
            Logging.Info($"Removed {removedCount} wall pieces from stage");
            
            tab.StageRenderer = new ClientStageRenderer(_graphicsDevice, tab.Stage);
            
            // First pass: detect wall groups and piece tags by reading the original file
            var stageFilePath = $"data/stages/user/{stageFileName}.txt";
            var pieceTags = new Dictionary<string, string>(); // key: "set_x_z" or "chk_x_z" or "fix_x_z", value: tags
            
            int currentGroupForPieces = -1; // index into HierarchyGroups; -1 = ungrouped
            int newFmtPieceParseIdx = 0;    // increments for every set/chk/fix line
            var newFmtPieceGroup = new Dictionary<int, int>(); // pieceIdx → groupIdx (new format only)
            
            if (System.IO.File.Exists(stageFilePath))
            {
                var wallId = 0;
                foreach (var line in System.IO.File.ReadAllLines(stageFilePath))
                {
                    var trimmed = line.Trim();
                    
                    // Capture wall definitions
                    if (trimmed.StartsWith("maxr("))
                    {
                        var n = int.Parse(trimmed.Substring(5).Split(',')[0]);
                        var o = int.Parse(trimmed.Split(',')[1]);
                        var p = int.Parse(trimmed.Split(',')[2].TrimEnd(')'));
                        tab.StageWalls.Add(new StageWall(StageWall.WallDirection.Right, n, o, p, wallId++));
                    }
                    else if (trimmed.StartsWith("maxl("))
                    {
                        var n = int.Parse(trimmed.Substring(5).Split(',')[0]);
                        var o = int.Parse(trimmed.Split(',')[1]);
                        var p = int.Parse(trimmed.Split(',')[2].TrimEnd(')'));
                        tab.StageWalls.Add(new StageWall(StageWall.WallDirection.Left, n, o, p, wallId++));
                    }
                    else if (trimmed.StartsWith("maxt("))
                    {
                        var n = int.Parse(trimmed.Substring(5).Split(',')[0]);
                        var o = int.Parse(trimmed.Split(',')[1]);
                        var p = int.Parse(trimmed.Split(',')[2].TrimEnd(')'));
                        tab.StageWalls.Add(new StageWall(StageWall.WallDirection.Top, n, o, p, wallId++));
                    }
                    else if (trimmed.StartsWith("maxb("))
                    {
                        var n = int.Parse(trimmed.Substring(5).Split(',')[0]);
                        var o = int.Parse(trimmed.Split(',')[1]);
                        var p = int.Parse(trimmed.Split(',')[2].TrimEnd(')'));
                        tab.StageWalls.Add(new StageWall(StageWall.WallDirection.Bottom, n, o, p, wallId++));
                    }
                    // Capture piece tags AND track new-format group membership
                    else if (trimmed.StartsWith("set(") || trimmed.StartsWith("chk(") || trimmed.StartsWith("fix("))
                    {
                        // New-format group tracking: record which group this file-order piece index belongs to
                        if (currentGroupForPieces >= 0)
                            newFmtPieceGroup[newFmtPieceParseIdx] = currentGroupForPieces;
                        newFmtPieceParseIdx++;
                        
                        // Tags (extra characters after the closing parenthesis)
                        var parenIndex = trimmed.IndexOf(')');
                        if (parenIndex != -1 && parenIndex < trimmed.Length - 1)
                        {
                            var tags = trimmed.Substring(parenIndex + 1);
                            if (!string.IsNullOrEmpty(tags))
                            {
                                var type   = trimmed.Substring(0, 3);
                                var coords = trimmed.Substring(4, parenIndex - 4).Split(',');
                                if (coords.Length >= 3)
                                    pieceTags[$"{type}_{coords[1]}_{coords[2]}"] = tags;
                            }
                        }
                    }
                    // Capture editor group metadata
                    else if (trimmed.StartsWith("#editor_group(") && trimmed.EndsWith(")"))
                    {
                        var inner = trimmed.Substring(14, trimmed.Length - 15);
                        if (!inner.Contains(','))
                        {
                            // ── New format: #editor_group(Name) — no X:Z keys ──
                            // Pieces that follow (until next #editor_group) belong to this group.
                            tab.HierarchyGroups.Add(new HierarchyGroup
                            {
                                Id   = tab.GetNextGroupId(),
                                Name = inner
                            });
                            currentGroupForPieces = tab.HierarchyGroups.Count - 1;
                        }
                        else
                        {
                            // ── Old format: #editor_group(Name,x:z,...) — coordinate keys ──
                            var gparts = inner.Split(',');
                            if (gparts.Length >= 1)
                            {
                                var keys = gparts.Skip(1).Select(s => s.Trim()).ToList();
                                tab.HierarchyGroups.Add(new HierarchyGroup
                                {
                                    Id      = tab.GetNextGroupId(),
                                    Name    = gparts[0],
                                    PieceIds = new List<int>()
                                });
                                tab._pendingGroupKeys.Add(keys);
                            }
                        }
                    }
                    else if (trimmed.StartsWith("#editor_ungrouped_order(") && trimmed.EndsWith(")"))
                    {
                        var inner = trimmed.Substring(24, trimmed.Length - 25);
                        if (int.TryParse(inner.Trim(), out int uoi))
                            tab.UngroupedOrderIndex = uoi;
                    }
                    // Capture unknown parameters
                    else if (!string.IsNullOrWhiteSpace(trimmed) &&
                             !trimmed.StartsWith("name(") &&
                             !trimmed.StartsWith("sky(") &&
                             !trimmed.StartsWith("fog(") &&
                             !trimmed.StartsWith("ground(") &&
                             !trimmed.StartsWith("polys(") &&
                             !trimmed.StartsWith("snap(") &&
                             !trimmed.StartsWith("clouds(") &&
                             !trimmed.StartsWith("cloudcoverage(") &&
                             !trimmed.StartsWith("mountains(") &&
                             !trimmed.StartsWith("fadefrom(") &&
                             !trimmed.StartsWith("#editor_group(") &&
                             !trimmed.StartsWith("#editor_ungrouped_order("))
                    {
                        tab.UnknownParameters.Add(trimmed);
                    }
                }
            }
            
            // Populate editor pieces from loaded stage
            foreach (var piece in tab.Stage.pieces)
            {
                if (piece is not StageObject collisionObject)
                    continue;
                
                // Skip thewall pieces - they're handled as StageWalls
                if (collisionObject.FileName == "thewall" || collisionObject.FileName.Contains("thewall"))
                    continue;
                    
                // Construct the full piece name with folder prefix
                string pieceName = collisionObject.FileName;
                if (!pieceName.Contains("/"))
                {
                    // If no folder prefix, it's from nfmm
                    pieceName = "nfmm/" + pieceName;
                }
                
                var instance = new StagePieceInstance(
                    pieceName,
                    collisionObject,
                    tab.GetNextPieceId()
                );
                
                // Detect piece type from mesh type first (needed for Y coordinate fix)
                if (piece is IAiNode { Kind: AiNodeKind.CheckPoint })
                {
                    instance.PieceType = StagePieceInstance.PieceTypeEnum.Chk;
                }
                else if (piece is IAiNode { Kind: AiNodeKind.FixHoop })
                {
                    instance.PieceType = StagePieceInstance.PieceTypeEnum.Fix;
                }
                else
                {
                    instance.PieceType = StagePieceInstance.PieceTypeEnum.Set;
                }
                
                // For aircheckpoints the StageLoader uses ymult=1 (chkheight = fileY * 1 + 0), so the
                // loaded Position.Y is already the correct world Y — no adjustment needed.
                instance.Position = new f64Vector3(
                    piece.Position.X,
                    piece.Position.Y,
                    piece.Position.Z
                );
                
                var euler = piece.Rotation;
                instance.Rotation = new f64Vector3(
                    euler.Pitch.Degrees,
                    euler.Yaw.Degrees,
                    euler.Roll.Degrees
                );
                
                // Look up and assign tags for this piece
                var typePrefix = instance.PieceType == StagePieceInstance.PieceTypeEnum.Chk ? "chk" :
                                instance.PieceType == StagePieceInstance.PieceTypeEnum.Fix ? "fix" : "set";
                var key = $"{typePrefix}_{(int)piece.Position.X}_{(int)piece.Position.Z}";
                if (pieceTags.TryGetValue(key, out var tags))
                {
                    instance.Tags = tags;
                }
                
                tab.ScenePieces.Add(instance);
            }
            
            // Remove any thewall pieces that might have slipped through
            tab.ScenePieces.RemoveAll(p => p.Name.Contains("thewall"));
            
            // ── Resolve group membership ────────────────────────────────────────────
            if (newFmtPieceGroup.Count > 0 || (tab.HierarchyGroups.Count > 0 && tab._pendingGroupKeys.Count == 0))
            {
                // New format: assign by file-order index
                int pidx = 0;
                foreach (var instance in tab.ScenePieces)
                {
                    if (instance.PieceType != StagePieceInstance.PieceTypeEnum.Wall)
                    {
                        if (newFmtPieceGroup.TryGetValue(pidx, out int grpIdx) &&
                            grpIdx >= 0 && grpIdx < tab.HierarchyGroups.Count)
                            tab.HierarchyGroups[grpIdx].PieceIds.Add(instance.Id);
                        pidx++;
                    }
                }
            }
            else
            {
                // Old format: resolve X:Z coordinate keys
                for (int gi = 0; gi < tab.HierarchyGroups.Count; gi++)
                {
                    var group = tab.HierarchyGroups[gi];
                    var keys  = gi < tab._pendingGroupKeys.Count ? tab._pendingGroupKeys[gi] : new List<string>();
                    var resolved = new List<int>();
                    foreach (var key in keys)
                    {
                        if (key.Contains(':'))
                        {
                            var kparts = key.Split(':');
                            if (kparts.Length == 2 &&
                                int.TryParse(kparts[0], out int kx) &&
                                int.TryParse(kparts[1], out int kz))
                            {
                                var match = tab.ScenePieces.Find(p =>
                                    Math.Abs((int)p.Position.X - kx) <= 1 &&
                                    Math.Abs((int)p.Position.Z - kz) <= 1);
                                if (match != null && !resolved.Contains(match.Id))
                                    resolved.Add(match.Id);
                            }
                        }
                        else if (int.TryParse(key, out int legacyIdx) && legacyIdx >= 0 && legacyIdx < tab.ScenePieces.Count)
                        {
                            var legacyPiece = tab.ScenePieces[legacyIdx];
                            if (!resolved.Contains(legacyPiece.Id))
                                resolved.Add(legacyPiece.Id);
                        }
                    }
                    group.PieceIds = resolved;
                }
            }
            tab._pendingGroupKeys.Clear();
            
            // Store properties in the tab from World (set by Stage constructor)
            tab.TabName = tab.Stage.Name;
            tab.SkyColor = new System.Numerics.Vector3(World.Sky.R, World.Sky.G, World.Sky.B);
            tab.FogColor = new System.Numerics.Vector3(World.Fog.R, World.Fog.G, World.Fog.B);
            tab.GroundColor = new System.Numerics.Vector3(World.GroundColor.R, World.GroundColor.G, World.GroundColor.B);
            tab.PolysEnabled = World.HasPolys;
            if (World.HasPolys)
            {
                tab.PolysColor = new System.Numerics.Vector3(World.GroundPolysColor.R, World.GroundPolysColor.G, World.GroundPolysColor.B);
            }
            else
            {
                // Auto-calculate from ground color (reduce by 10 points)
                tab.PolysColor = new System.Numerics.Vector3(
                    Math.Max(0, World.GroundColor.R - 10),
                    Math.Max(0, World.GroundColor.G - 10),
                    Math.Max(0, World.GroundColor.B - 10)
                );
            }
            tab.CloudsEnabled = World.HasClouds;
            if (World.HasClouds)
            {
                tab.CloudsColor = new System.Numerics.Vector3(World.Clouds[0], World.Clouds[1], World.Clouds[2]);
                tab.CloudsParam4 = World.Clouds[3];
                tab.CloudsHeight = World.Clouds[4];
                tab.CloudCoverage = World.CloudCoverage;
            }
            tab.MountainsEnabled = World.DrawMountains;
            tab.MountainsSeed = World.MountainSeed;
            tab.SnapA = World.Snap.R;
            tab.SnapB = World.Snap.G;
            tab.SnapC = World.Snap.B;
            tab.FadeFrom = World.FadeFrom;
            
            // Add tab and activate it
            _tabs.Add(tab);
            _activeTabIndex = _tabs.Count - 1;
            
            // Recreate scene at the very end to ensure it has the final clean pieces array
            RecreateScene();
            
            // Rebuild walls AFTER recreating scene
            RebuildAllWalls();
            
            Logging.Info($"Loaded stage: {tab.Stage.Name}");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error loading stage: {ex.Message}");
        }
    }
    
    private void RecreateScene()
    {
        if (ActiveTab?.Stage == null || ActiveTab?.StageRenderer == null) return;
        
        // Create scene with the stage renderer and all current wall meshes
        var sceneObjects = new List<GameObject> { ActiveTab.StageRenderer };
        sceneObjects.AddRange(ActiveTab.WallMeshes);
        ActiveTab.Scene = new Scene(
            _graphicsDevice,
            sceneObjects,
            camera,
            [] // No shadow cameras for now
        );
    }
    
    /// <summary>
    /// Rebuilds the ClientStageRenderer from scratch using the current Stage.pieces,
    /// then re-applies the tab's World settings and recreates the Scene + walls.
    /// Call this whenever pieces are added or removed.
    /// </summary>
    private void RebuildClientRenderer()
    {
        if (ActiveTab?.Stage == null) return;
        
        ActiveTab.StageRenderer = new ClientStageRenderer(_graphicsDevice, ActiveTab.Stage);
        
        // ClientStageRenderer.ctor calls World.ResetValues(), re-apply our tab settings
        ApplyTabWorldValuesToWorld();
        RecreateEnvironment();
        RecreateScene();
        RebuildAllWalls();
    }
    
    private void ApplyTabWorldValuesToWorld()
    {
        if (ActiveTab == null) return;
        World.Sky = new Color3((short)ActiveTab.SkyColor.X, (short)ActiveTab.SkyColor.Y, (short)ActiveTab.SkyColor.Z);
        World.Fog = new Color3((short)ActiveTab.FogColor.X, (short)ActiveTab.FogColor.Y, (short)ActiveTab.FogColor.Z);
        World.GroundColor = new Color3((short)ActiveTab.GroundColor.X, (short)ActiveTab.GroundColor.Y, (short)ActiveTab.GroundColor.Z);
        World.FadeFrom = ActiveTab.FadeFrom;
        World.HasPolys = ActiveTab.PolysEnabled;
        World.DrawPolys = ActiveTab.PolysEnabled;
        if (ActiveTab.PolysEnabled)
            World.GroundPolysColor = new Color3((short)ActiveTab.PolysColor.X, (short)ActiveTab.PolysColor.Y, (short)ActiveTab.PolysColor.Z);
        World.HasClouds = ActiveTab.CloudsEnabled;
        World.DrawClouds = ActiveTab.CloudsEnabled;
        if (ActiveTab.CloudsEnabled)
        {
            World.Clouds = [(int)ActiveTab.CloudsColor.X, (int)ActiveTab.CloudsColor.Y, (int)ActiveTab.CloudsColor.Z, ActiveTab.CloudsParam4, ActiveTab.CloudsHeight];
            World.CloudCoverage = ActiveTab.CloudCoverage;
        }
        World.DrawMountains = ActiveTab.MountainsEnabled;
        if (ActiveTab.MountainsEnabled)
            World.MountainSeed = ActiveTab.MountainsSeed;
        World.Snap = new Color3((short)ActiveTab.SnapA, (short)ActiveTab.SnapB, (short)ActiveTab.SnapC);
    }
    
    private void RecreateEnvironment()
    {
        if (ActiveTab?.StageRenderer == null) return;
        ActiveTab.StageRenderer.sky = new Sky(_graphicsDevice);
        ActiveTab.StageRenderer.ground = new Ground(_graphicsDevice);
        if (ActiveTab.PolysEnabled && ActiveTab.Stage != null)
            ActiveTab.StageRenderer.polys = Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count, _graphicsDevice);
        else
            ActiveTab.StageRenderer.polys = null;
        if (ActiveTab.CloudsEnabled)
            ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
        else
            ActiveTab.StageRenderer.clouds = null;
        if (ActiveTab.MountainsEnabled)
            ActiveTab.StageRenderer.mountains = Environment.MakeMountains(-10000, 10000, -10000, 10000, _graphicsDevice);
        else
            ActiveTab.StageRenderer.mountains = null;
    }
    
    private void UpdateCameraPosition()
    {
        if (ActiveTab == null) return;
        
        if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene)
        {
            // First-person flying camera
            float yaw = ActiveTab.CameraYaw * (float)Math.PI / 180f;
            float pitch = ActiveTab.CameraPitch * (float)Math.PI / 180f;
            
            // Calculate look direction
            var lookDirection = new Vector3(
                (float)(Math.Cos(pitch) * Math.Sin(yaw)),
                (float)Math.Sin(pitch),
                (float)(Math.Cos(pitch) * Math.Cos(yaw))
            );
            
            camera.Position = ActiveTab.CameraPosition;
            camera.LookAt = ActiveTab.CameraPosition + lookDirection;
            camera.Up = -Vector3.UnitY;
            camera.IsOrthographic = false;
        }
        else
        {
            // Top down view - look from above at pan position (negative Y is up in this coordinate system)
            camera.Position = new Vector3(ActiveTab.TopDownPanPosition.X, -ActiveTab.TopDownHeight, ActiveTab.TopDownPanPosition.Z);
            camera.LookAt = new Vector3(ActiveTab.TopDownPanPosition.X, 0, ActiveTab.TopDownPanPosition.Z);
            camera.Up = Vector3.UnitZ;
            camera.IsOrthographic = ActiveTab.TopDownOrtho;
            if (ActiveTab.TopDownOrtho)
            {
                // Match the visible world area that perspective would show at this height.
                // half_height_world = TopDownHeight * tan(Fov/2)
                float halfH = ActiveTab.TopDownHeight * MathF.Tan(camera.Fov * MathF.PI / 180f * 0.5f);
                camera.OrthoScale = (camera.Height > 0) ? (2f * halfH / camera.Height) : 1f;
            }
        }
    }
    
    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        if (imguiWantsKeyboard) return;
        if (!_isOpen) return;
        
        // In placement mode: Q/E rotate the pending piece by 45°; Q is also the camera-down key so
        // we handle rotation first and skip the camera binding.
        if (_pendingPlacementPartIndex >= 0)
        {
            if (key == Keys.E)
            {
                _pendingPlacementYaw = (_pendingPlacementYaw + 45f) % 360f;
                return;
            }
            if (key == Keys.Q)
            {
                _pendingPlacementYaw = ((_pendingPlacementYaw - 45f) % 360f + 360f) % 360f;
                return;
            }
            if (key == Keys.R)
            {
                // Reset rotation
                _pendingPlacementYaw = 0f;
                return;
            }
        }
        
        // Camera movement
        switch (key)
        {
            case Keys.W:
                _moveForward = true;
                break;
            case Keys.S:
                _moveBackward = true;
                break;
            case Keys.A:
                _moveLeft = true;
                break;
            case Keys.D:
                _moveRight = true;
                break;
            case Keys.Space:
                _moveUp = true;
                break;
            case Keys.Q:
                _moveDown = true;
                break;
            case Keys.LShiftKey:
            case Keys.RShiftKey:
                _isShiftPressed = true;
                break;
            case Keys.LControlKey:
            case Keys.RControlKey:
                _isCtrlPressed = true;
                break;
        }
        
        // Handle keyboard shortcuts here
        if (key == Keys.Delete)
        {
            // Build the set of IDs to delete (multi-selection or single)
            var idsToDelete = ActiveTab.SelectedPieceIds.Count > 0
                ? new List<int>(ActiveTab.SelectedPieceIds)
                : ActiveTab.SelectedPieceId >= 0 ? new List<int> { ActiveTab.SelectedPieceId } : new List<int>();
            
            if (idsToDelete.Count > 0)
            {
                PushUndoSnapshot();
                foreach (var deleteId in idsToDelete)
                {
                    var piece = ActiveTab.ScenePieces.Find(p => p.Id == deleteId);
                    if (piece == null) continue;
                    if (ActiveTab.Stage != null)
                        for (int i = 0; i < ActiveTab.Stage.pieces.Count; i++)
                            if (ActiveTab.Stage.pieces[i] == piece.Obj) { ActiveTab.Stage.pieces.RemoveAt(i); break; }
                    ActiveTab.ScenePieces.Remove(piece);
                    foreach (var grp in ActiveTab.HierarchyGroups) grp.PieceIds.Remove(piece.Id);
                }
                ActiveTab.SelectedPieceIds.Clear();
                ActiveTab.SelectedPieceId = -1;
                ActiveTab.HasUnsavedChanges = true;
                RebuildClientRenderer();
            }
        }
        
        if (key == Keys.S && _isCtrlPressed)
        {
            if (ActiveTab?.Stage != null) SaveStage();
        }
        
        if (key == Keys.C && _isCtrlPressed && ActiveTab != null)
        {
            // Copy all selected pieces (or primary if no multi-selection)
            var ids = ActiveTab.SelectedPieceIds.Count > 0
                ? ActiveTab.SelectedPieceIds
                : (ActiveTab.SelectedPieceId >= 0 ? new HashSet<int> { ActiveTab.SelectedPieceId } : new HashSet<int>());
            if (ids.Count > 0)
            {
                var pieces = ActiveTab.ScenePieces.Where(p => ids.Contains(p.Id)).ToList();
                // Compute centroid so paste is relative
                var centroid = new f64Vector3(
                    (fix64)(pieces.Average(p => (double)p.Position.X)),
                    (fix64)(pieces.Average(p => (double)p.Position.Y)),
                    (fix64)(pieces.Average(p => (double)p.Position.Z)));
                _clipboard = pieces.Select(p => new ClipboardPiece(
                    p.Name,
                    new f64Vector3(p.Position.X - centroid.X, p.Position.Y - centroid.Y, p.Position.Z - centroid.Z),
                    p.Rotation, p.PieceType, p.Tags,
                    _availableParts.FirstOrDefault(a => a.Name == p.Name).Rad)).ToList();
            }
        }
        
        if (key == Keys.V && _isCtrlPressed && ActiveTab?.Stage != null && _clipboard.Count > 0)
        {
            PushUndoSnapshot();
            ActiveTab.SelectedPieceIds.Clear();
            // Determine paste offset — use snap size when enabled, otherwise a small fixed nudge
            float pasteOffsetXZ = _snapEnabled && _snapSize > 0f ? _snapSize : 200f;
            var primaryPiece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
            f64Vector3 pasteOrigin;
            if (primaryPiece != null)
            {
                float ox = (float)primaryPiece.Position.X + pasteOffsetXZ;
                float oz = (float)primaryPiece.Position.Z + pasteOffsetXZ;
                if (_snapEnabled && _snapSize > 0f)
                {
                    ox = MathF.Round(ox / _snapSize) * _snapSize;
                    oz = MathF.Round(oz / _snapSize) * _snapSize;
                }
                pasteOrigin = new f64Vector3((fix64)ox, primaryPiece.Position.Y, (fix64)oz);
            }
            else
            {
                pasteOrigin = f64Vector3.Zero;
            }
            int lastId = -1;
            foreach (var clip in _clipboard)
            {
                float wx = (float)pasteOrigin.X + (float)clip.RelPos.X;
                float wy = (float)pasteOrigin.Y + (float)clip.RelPos.Y;
                float wz = (float)pasteOrigin.Z + (float)clip.RelPos.Z;
                if (_snapEnabled && _snapSize > 0f)
                {
                    wx = MathF.Round(wx / _snapSize) * _snapSize;
                    wz = MathF.Round(wz / _snapSize) * _snapSize;
                }
                var worldPos = new f64Vector3((fix64)wx, (fix64)wy, (fix64)wz);
                StageObject? mesh = null;
                if (clip.Rad != null)
                {
                    var euler = new f64Euler(
                        f64AngleSingle.FromDegrees(clip.Rotation.Y),
                        f64AngleSingle.ZeroAngle,
                        f64AngleSingle.ZeroAngle);
                    mesh = new StageObject(clip.Rad, worldPos, euler);
                    ActiveTab.Stage.pieces[ActiveTab.Stage.stagePartCount] = mesh;
                }
                var instance = new StagePieceInstance(clip.Name, mesh, ActiveTab.GetNextPieceId())
                {
                    Position = worldPos,
                    Rotation = clip.Rotation,
                    PieceType = clip.PieceType,
                    Tags = clip.Tags
                };
                ActiveTab.ScenePieces.Add(instance);
                ActiveTab.SelectedPieceIds.Add(instance.Id);
                lastId = instance.Id;
            }
            if (lastId >= 0) ActiveTab.SelectedPieceId = lastId;
            ActiveTab.HasUnsavedChanges = true;
            RebuildClientRenderer();
        }
        
        if (key == Keys.Z && _isCtrlPressed)
        {
            PerformUndo();
        }
        
        if ((key == Keys.Y && _isCtrlPressed) || (key == Keys.Z && _isCtrlPressed && _isShiftPressed))
        {
            PerformRedo();
        }
        
        if (key == Keys.Escape)
        {
            // Cancel placement mode, swap mode, and rect selection
            _pendingPlacementPartIndex = -1;
            _hasValidPlacementPos = false;
            _isSwapMode = false;
            _isRectSelecting = false;
        }
    }
    
    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        if (imguiWantsKeyboard) return;
        if (!_isOpen) return;
        
        // Camera movement
        switch (key)
        {
            case Keys.W:
                _moveForward = false;
                break;
            case Keys.S:
                _moveBackward = false;
                break;
            case Keys.A:
                _moveLeft = false;
                break;
            case Keys.D:
                _moveRight = false;
                break;
            case Keys.Space:
                _moveUp = false;
                break;
            case Keys.Q:
                _moveDown = false;
                break;
            case Keys.LShiftKey:
            case Keys.RShiftKey:
                _isShiftPressed = false;
                break;
            case Keys.LControlKey:
            case Keys.RControlKey:
                _isCtrlPressed = false;
                break;
        }
    }
    
    private bool IsMouseInViewport(int x, int y)
    {
        return x >= _viewportMin.X && x <= _viewportMax.X &&
               y >= _viewportMin.Y && y <= _viewportMax.Y;
    }
    
    private void RebuildAllWalls()
    {
        if (ActiveTab?.Stage == null) return;
        
        // Get the wall mesh from GameSparker
        var wallPart = BackendGameSparker.GetStagePart("nfmm/thewall");
        if (wallPart.Rad == null)
        {
            SentrySdk.CaptureMessage("Wall mesh not found!");
            Logging.Error("Wall mesh not found!");
            return;
        }
        
        // Clear the wall meshes list
        ActiveTab.WallMeshes.Clear();
        
        Logging.Info($"Rebuilding walls: {ActiveTab.StageWalls.Count} wall groups");
        
        // Generate wall meshes based on StageWalls definitions
        foreach (var wall in ActiveTab.StageWalls)
        {
            var n = wall.Count;
            var o = wall.Position;
            var p = wall.Offset;
            
            Logging.Debug($"Creating wall: {wall.Direction}, count={n}, pos={o}, offset={p}");
            
            for (int q = 0; q < n; q++)
            {
                f64Vector3 position;
                f64Euler rotation;
                
                switch (wall.Direction)
                {
                    case StageWall.WallDirection.Right:
                        position = new f64Vector3(o, World.Ground, q * 4800 + p);
                        rotation = f64Euler.Identity;
                        break;
                    case StageWall.WallDirection.Left:
                        position = new f64Vector3(o, World.Ground, q * 4800 + p);
                        rotation = new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                        break;
                    case StageWall.WallDirection.Top:
                        position = new f64Vector3(q * 4800 + p, World.Ground, o);
                        rotation = new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                        break;
                    case StageWall.WallDirection.Bottom:
                        position = new f64Vector3(q * 4800 + p, World.Ground, o);
                        rotation = new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                        break;
                    default:
                        position = f64Vector3.Zero;
                        rotation = f64Euler.Identity;
                        break;
                }
                
                ActiveTab.WallMeshes.Add(new MeshedGameObject(new Mesh(_graphicsDevice, wallPart.Rad), position, rotation));
            }
        }
        
        Logging.Debug($"Total wall meshes created: {ActiveTab.WallMeshes.Count}");
        
        // Rebuild scene so new wall meshes are included in instanced rendering
        RecreateScene();
    }
    
    private void RenderSelectionHighlight(StagePieceInstance piece)
    {
        if (piece.Obj == null) return;
        
        var mesh = piece.Obj;
        
        // Save old depth state and disable depth testing so highlight renders on top
        var oldDepthStencilState = _graphicsDevice.DepthStencilState;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        
        // Draw wireframe box using BasicEffect
        var effect = new BasicEffect(_graphicsDevice);
        effect.View = camera.ViewMatrix;
        effect.Projection = camera.ProjectionMatrix;
        effect.VertexColorEnabled = true;
        
        var color = new Microsoft.Xna.Framework.Color(1.0f, 1.0f, 0.0f, 1.0f); // Yellow
        
        // Get rotation from piece only - match game engine's rotation order
        // Negate yaw to match game engine's coordinate system
        var yaw = -piece.Rotation.Y * (fix64)Math.PI / 180;
        var pitch = piece.Rotation.X * (fix64)Math.PI / 180;
        var roll = piece.Rotation.Z * (fix64)Math.PI / 180;
        
        var rotationMatrix = 
            Matrix.CreateRotationY((float)yaw) *
            Matrix.CreateRotationX((float)pitch) *
            Matrix.CreateRotationZ((float)roll);
        
        var totalPosition = new f64Vector3(
            piece.Position.X,
            piece.Position.Y,
            piece.Position.Z
        );
        
        // Collect all polygon edges for wireframe rendering
        var edgeVertices = new List<VertexPositionColor>();
        
        foreach (var poly in mesh.Rad.Polys)
        {
            if (poly.Points.Length < 2) continue;
            
            // Transform all points
            var transformedPoints = new Vector3[poly.Points.Length];
            for (int i = 0; i < poly.Points.Length; i++)
            {
                var localVert = new Vector3(
                    poly.Points[i].X,
                    poly.Points[i].Y,
                    poly.Points[i].Z
                );
                transformedPoints[i] = Vector3.Transform(localVert, rotationMatrix) + (Vector3)totalPosition;
            }
            
            // Add edges
            for (int i = 0; i < poly.Points.Length; i++)
            {
                var nextIdx = (i + 1) % poly.Points.Length;
                edgeVertices.Add(new(transformedPoints[i], color));
                edgeVertices.Add(new(transformedPoints[nextIdx], color));
            }
        }
        
        // Draw all edges
        if (edgeVertices.Count > 0)
        {
            // Draw the lines multiple times with slight offsets to make them thicker
            var offsets = new[] 
            { 
                new Vector3(0, 0, 0),
                new Vector3(0.5f, 0, 0),
                new Vector3(-0.5f, 0, 0),
                new Vector3(0, 0.5f, 0),
                new Vector3(0, -0.5f, 0)
            };
            
            foreach (var offset in offsets)
            {
                var offsetVertices = edgeVertices.Select(v => 
                    new VertexPositionColor(
                        v.Position + offset, 
                        v.Color
                    )
                ).ToArray();
                
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.LineList,
                        offsetVertices,
                        0,
                        offsetVertices.Length / 2
                    );
                }
            }
        }
        
        // Restore depth state
        _graphicsDevice.DepthStencilState = oldDepthStencilState;
    }
    
    /// <summary>
    /// Renders a translucent ghost preview of the pending placement part at _pendingPlacementPos.
    /// Shows semi-transparent filled polygons plus a bright wireframe outline.
    /// </summary>
    private void RenderPlacementPreview()
    {
        if (_pendingPlacementPartIndex < 0 || _pendingPlacementPartIndex >= _availableParts.Count) return;
        var part = _availableParts[_pendingPlacementPartIndex];
        if (part.Rad == null) return;
        
        var pos = new Vector3((float)_pendingPlacementPos.X, (float)_pendingPlacementPos.Y, (float)_pendingPlacementPos.Z);
        float yawRad = -_pendingPlacementYaw * (float)Math.PI / 180f; // negate to match engine convention
        var rotMatrix = Matrix.CreateRotationY(yawRad);
        
        var oldDepth = _graphicsDevice.DepthStencilState;
        var oldBlend = _graphicsDevice.BlendState;
        var oldRasterizer = _graphicsDevice.RasterizerState;
        
        var effect = new BasicEffect(_graphicsDevice);
        effect.View = camera.ViewMatrix;
        effect.Projection = camera.ProjectionMatrix;
        effect.VertexColorEnabled = true;
        
        // Semi-transparent fill (both faces so it looks solid from any angle)
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };
        
        var fillColor = new Microsoft.Xna.Framework.Color(0.3f, 0.8f, 1.0f, 0.35f);
        var fillVerts = new List<VertexPositionColor>();
        
        foreach (var poly in part.Rad.Polys)
        {
            if (poly.Points.Length < 3) continue;
            for (int i = 1; i < poly.Points.Length - 1; i++)
            {
                fillVerts.Add(new(Vector3.Transform(new Vector3(poly.Points[0].X, poly.Points[0].Y, poly.Points[0].Z), rotMatrix) + pos, fillColor));
                fillVerts.Add(new(Vector3.Transform(new Vector3(poly.Points[i].X, poly.Points[i].Y, poly.Points[i].Z), rotMatrix) + pos, fillColor));
                fillVerts.Add(new(Vector3.Transform(new Vector3(poly.Points[i + 1].X, poly.Points[i + 1].Y, poly.Points[i + 1].Z), rotMatrix) + pos, fillColor));
            }
        }
        
        if (fillVerts.Count > 0)
        {
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, fillVerts.ToArray(), 0, fillVerts.Count / 3);
            }
        }
        
        // Bright wireframe on top (depth-ignore so it's always visible)
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.BlendState = BlendState.Opaque;
        var wireColor = new Microsoft.Xna.Framework.Color(0.1f, 0.9f, 1.0f, 1.0f);
        var wireVerts = new List<VertexPositionColor>();
        
        foreach (var poly in part.Rad.Polys)
        {
            if (poly.Points.Length < 2) continue;
            for (int i = 0; i < poly.Points.Length; i++)
            {
                int next = (i + 1) % poly.Points.Length;
                wireVerts.Add(new(Vector3.Transform(new Vector3(poly.Points[i].X, poly.Points[i].Y, poly.Points[i].Z), rotMatrix) + pos, wireColor));
                wireVerts.Add(new(Vector3.Transform(new Vector3(poly.Points[next].X, poly.Points[next].Y, poly.Points[next].Z), rotMatrix) + pos, wireColor));
            }
        }
        
        if (wireVerts.Count > 0)
        {
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, wireVerts.ToArray(), 0, wireVerts.Count / 2);
            }
        }
        
        _graphicsDevice.DepthStencilState = oldDepth;
        _graphicsDevice.BlendState = oldBlend;
        _graphicsDevice.RasterizerState = oldRasterizer;
    }
    
    // Project a world-space point to screen coordinates (returns false if behind camera)
    private bool WorldToScreen(Vector3 worldPos, out System.Numerics.Vector2 screenPos)
    {
        var viewport = _graphicsDevice.Viewport;
        var clip = Microsoft.Xna.Framework.Vector4.Transform(new Vector4(worldPos, 1f),
            camera.ViewMatrix * camera.ProjectionMatrix);
        screenPos = default;
        if (clip.W <= 0f) return false;
        var ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W); // XNA Vector3
        screenPos = new System.Numerics.Vector2(
            (ndc.X + 1f) * 0.5f * viewport.Width,
            (1f - ndc.Y) * 0.5f * viewport.Height);
        return true;
    }
    
    private Vector3 ComputeSelectionCentroid()
    {
        if (ActiveTab == null) return Vector3.Zero;
        var ids = ActiveTab.SelectedPieceIds.Count > 0
            ? ActiveTab.SelectedPieceIds
            : ActiveTab.SelectedPieceId >= 0 ? (IEnumerable<int>)new[] { ActiveTab.SelectedPieceId } : Array.Empty<int>();
        var pieces = ActiveTab.ScenePieces.Where(p => ids.Contains(p.Id)).ToList();
        if (pieces.Count == 0) return Vector3.Zero;
        return new Vector3(
            (float)pieces.Average(p => (double)p.Position.X),
            (float)pieces.Average(p => (double)p.Position.Y),
            (float)pieces.Average(p => (double)p.Position.Z));
    }

    private void RenderGizmo(Vector3 gizmoPos)
    {
        var piecePos = gizmoPos;
        var xEnd = piecePos + new Vector3(GIZMO_ARROW_LENGTH, 0, 0);
        // Y arrow points up in world space (negative Y in FNA because Y is flipped)
        var yEnd = piecePos + new Vector3(0, -GIZMO_ARROW_LENGTH, 0);
        var zEnd = piecePos + new Vector3(0, 0, GIZMO_ARROW_LENGTH);
        
        var oldDepth = _graphicsDevice.DepthStencilState;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        
        var effect = new BasicEffect(_graphicsDevice);
        effect.View = camera.ViewMatrix;
        effect.Projection = camera.ProjectionMatrix;
        effect.VertexColorEnabled = true;
        
        // Colors: red=X, yellow=Y(up), blue=Z, green=RotY ring
        var colX = _gizmoHovered == GizmoAxis.X || _gizmoDragging == GizmoAxis.X
            ? new Microsoft.Xna.Framework.Color(1f, 0.6f, 0.6f, 1f)
            : new Microsoft.Xna.Framework.Color(1f, 0.1f, 0.1f, 1f);
        var colY = _gizmoHovered == GizmoAxis.Y || _gizmoDragging == GizmoAxis.Y
            ? new Microsoft.Xna.Framework.Color(1f, 1f, 0.6f, 1f)
            : new Microsoft.Xna.Framework.Color(1f, 0.9f, 0.1f, 1f);
        var colZ = _gizmoHovered == GizmoAxis.Z || _gizmoDragging == GizmoAxis.Z
            ? new Microsoft.Xna.Framework.Color(0.6f, 0.6f, 1f, 1f)
            : new Microsoft.Xna.Framework.Color(0.1f, 0.1f, 1f, 1f);
        var colRot = _gizmoHovered == GizmoAxis.RotY || _gizmoDragging == GizmoAxis.RotY
            ? new Microsoft.Xna.Framework.Color(0.6f, 1f, 0.6f, 1f)
            : new Microsoft.Xna.Framework.Color(0.1f, 0.9f, 0.1f, 1f);
        
        // Arrowhead side fins and tip offsets for each axis
        var xSide     = new Vector3(0, GIZMO_ARROW_THICKNESS * 2, 0);
        var ySide     = new Vector3(GIZMO_ARROW_THICKNESS * 2, 0, 0);
        var zSide     = new Vector3(GIZMO_ARROW_THICKNESS * 2, 0, 0);
        var xTipOffset = new Vector3(GIZMO_ARROW_LENGTH * 0.15f, 0, 0);
        var yTipOffset = new Vector3(0, -GIZMO_ARROW_LENGTH * 0.15f, 0); // negative = upward
        var zTipOffset = new Vector3(0, 0, GIZMO_ARROW_LENGTH * 0.15f);
        
        var verts = new List<VertexPositionColor>();
        
        // X arrow shaft
        verts.Add(new(piecePos, colX)); verts.Add(new(xEnd, colX));
        // X arrowhead
        verts.Add(new(xEnd - xTipOffset + xSide, colX)); verts.Add(new(xEnd, colX));
        verts.Add(new(xEnd - xTipOffset - xSide, colX)); verts.Add(new(xEnd, colX));
        
        // Y arrow shaft (points up)
        verts.Add(new(piecePos, colY)); verts.Add(new(yEnd, colY));
        // Y arrowhead
        verts.Add(new(yEnd - yTipOffset + ySide, colY)); verts.Add(new(yEnd, colY));
        verts.Add(new(yEnd - yTipOffset - ySide, colY)); verts.Add(new(yEnd, colY));
        
        // Z arrow shaft
        verts.Add(new(piecePos, colZ)); verts.Add(new(zEnd, colZ));
        // Z arrowhead
        verts.Add(new(zEnd - zTipOffset + zSide, colZ)); verts.Add(new(zEnd, colZ));
        verts.Add(new(zEnd - zTipOffset - zSide, colZ)); verts.Add(new(zEnd, colZ));
        
        // Rotation ring (circle of line segments at piece Y level)
        const int ringSegs = 32;
        for (int i = 0; i < ringSegs; i++)
        {
            float a0 = i / (float)ringSegs * (2f * MathF.PI);
            float a1 = (i + 1) / (float)ringSegs * (2f * MathF.PI);
            verts.Add(new(piecePos + new Vector3(MathF.Cos(a0) * GIZMO_ROT_RADIUS, 0, MathF.Sin(a0) * GIZMO_ROT_RADIUS), colRot));
            verts.Add(new(piecePos + new Vector3(MathF.Cos(a1) * GIZMO_ROT_RADIUS, 0, MathF.Sin(a1) * GIZMO_ROT_RADIUS), colRot));
        }
        
        var arr = verts.ToArray();
        
        // Compute camera-relative perpendicular offsets so lines appear ~5px wide at any distance.
        // Camera right/up come from the columns of the view matrix (orthonormal rotation part).
        float dist = Vector3.Distance(camera.Position, piecePos);
        float halfFovRad = camera.Fov * MathF.PI / 180f * 0.5f;
        // World units that map to 1 screen pixel at this distance
        float pixelSize = dist * MathF.Tan(halfFovRad) * 2f / _graphicsDevice.Viewport.Height;
        float s = pixelSize * 2f; // 2 px each side = ~5px total visual width
        var camRight = new Vector3(camera.ViewMatrix.M11, camera.ViewMatrix.M21, camera.ViewMatrix.M31);
        var camUp    = new Vector3(camera.ViewMatrix.M12, camera.ViewMatrix.M22, camera.ViewMatrix.M32);
        var thickOffsets = new[]
        {
            Vector3.Zero,
            camRight *  s, camRight * -s,
            camUp    *  s, camUp    * -s,
        };
        
        foreach (var offset in thickOffsets)
        {
            var offsetArr = offset == Vector3.Zero
                ? arr
                : arr.Select(v => new VertexPositionColor(v.Position + offset, v.Color)).ToArray();
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, offsetArr, 0, offsetArr.Length / 2);
            }
        }
        
        _graphicsDevice.DepthStencilState = oldDepth;
        
        // Update hover state based on screen-space distances
        UpdateGizmoHover(piecePos);
    }
    
    private void UpdateGizmoHover(Vector3 piecePos)
    {
        if (_gizmoDragging != GizmoAxis.None) return;
        
        float mx = _mouseX, my = _mouseY;
        float closestDist = 20f; // hover threshold in pixels
        _gizmoHovered = GizmoAxis.None;
        
        // Check X arrow
        if (WorldToScreen(piecePos, out var ss0) &&
            WorldToScreen(piecePos + new Vector3(GIZMO_ARROW_LENGTH, 0, 0), out var ss1))
        {
            float d = DistanceToSegment(new System.Numerics.Vector2(mx, my), ss0, ss1);
            if (d < closestDist) { closestDist = d; _gizmoHovered = GizmoAxis.X; }
        }
        // Check Y arrow (up)
        if (WorldToScreen(piecePos, out ss0) &&
            WorldToScreen(piecePos + new Vector3(0, -GIZMO_ARROW_LENGTH, 0), out ss1))
        {
            float d = DistanceToSegment(new System.Numerics.Vector2(mx, my), ss0, ss1);
            if (d < closestDist) { closestDist = d; _gizmoHovered = GizmoAxis.Y; }
        }
        // Check Z arrow
        if (WorldToScreen(piecePos, out ss0) &&
            WorldToScreen(piecePos + new Vector3(0, 0, GIZMO_ARROW_LENGTH), out ss1))
        {
            float d = DistanceToSegment(new System.Numerics.Vector2(mx, my), ss0, ss1);
            if (d < closestDist) { closestDist = d; _gizmoHovered = GizmoAxis.Z; }
        }
        // Check rotation ring (test each segment)
        const int ringSegs = 32;
        for (int i = 0; i < ringSegs; i++)
        {
            float a0 = i / (float)ringSegs * (2f * MathF.PI);
            float a1 = (i + 1) / (float)ringSegs * (2f * MathF.PI);
            var p0 = piecePos + new Vector3(MathF.Cos(a0) * GIZMO_ROT_RADIUS, 0, MathF.Sin(a0) * GIZMO_ROT_RADIUS);
            var p1 = piecePos + new Vector3(MathF.Cos(a1) * GIZMO_ROT_RADIUS, 0, MathF.Sin(a1) * GIZMO_ROT_RADIUS);
            if (WorldToScreen(p0, out ss0) && WorldToScreen(p1, out ss1))
            {
                float d = DistanceToSegment(new System.Numerics.Vector2(mx, my), ss0, ss1);
                if (d < closestDist) { closestDist = d; _gizmoHovered = GizmoAxis.RotY; }
            }
        }
    }
    
    private static float DistanceToSegment(System.Numerics.Vector2 p, System.Numerics.Vector2 a, System.Numerics.Vector2 b)
    {
        var ab = b - a;
        var ap = p - a;
        float t = System.Numerics.Vector2.Dot(ap, ab) / System.Numerics.Vector2.Dot(ab, ab);
        t = Math.Clamp(t, 0f, 1f);
        return System.Numerics.Vector2.Distance(p, a + t * ab);
    }
    
    private void ProcessOnePreviewThumbnail()
    {
        if (_previewQueue.Count == 0) return;
        var (name, rad) = _previewQueue.Dequeue();
        if (_partPreviews.ContainsKey(name)) return;
        
        // Find bounding sphere to set up camera
        float maxR = rad.MaxRadius > 0 ? rad.MaxRadius : 300;
        
        var rt = new RenderTarget2D(_graphicsDevice, PreviewSize, PreviewSize,
            false, SurfaceFormat.Color, DepthFormat.Depth24);
        
        var prevRTs = _graphicsDevice.GetRenderTargets();
        _graphicsDevice.SetRenderTarget(rt);
        _graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(45, 45, 48));
        
        // Set up a simple isometric-ish view camera for the preview
        float camDist = maxR * 3f;
        var eye = new Vector3(camDist * 0.7f, camDist * 0.6f, camDist * 0.7f);
        var target = Vector3.Zero;
        var view = Matrix.CreateLookAt(eye, target, Vector3.Up);
        var proj = Matrix.CreatePerspectiveFieldOfView(40f * (MathF.PI / 180f), 1f, 1f, camDist * 5f);
        
        var effect = new BasicEffect(_graphicsDevice)
        {
            View = view,
            Projection = proj,
            VertexColorEnabled = true
        };
        
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;
        
        // Build vertex buffer from Rad3d polygons
        var verts = new List<VertexPositionColor>();
        foreach (var poly in rad.Polys)
        {
            if (poly.Points.Length < 3) continue;
            // Fan triangulation
            for (int i = 2; i < poly.Points.Length; i++)
            {
                var p0 = new Vector3(poly.Points[0].X, poly.Points[0].Y, poly.Points[0].Z);
                var p1 = new Vector3(poly.Points[i - 1].X, poly.Points[i - 1].Y, poly.Points[i - 1].Z);
                var p2 = new Vector3(poly.Points[i].X, poly.Points[i].Y, poly.Points[i].Z);
                var xnaColor = new Microsoft.Xna.Framework.Color((int)poly.Color.R, (int)poly.Color.G, (int)poly.Color.B, 255);
                verts.Add(new(p0, xnaColor));
                verts.Add(new(p1, xnaColor));
                verts.Add(new(p2, xnaColor));
            }
        }
        
        if (verts.Count > 0)
        {
            var arr = verts.ToArray();
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, arr.Length / 3);
            }
        }
        
        _graphicsDevice.SetRenderTargets(prevRTs);
        
        var texRef = WorldGame.ImguiRenderer.BindTexture(rt);
        _partPreviews[name] = (rt, texRef);
    }
    
    private void QueuePartPreview(string name, Rad3d rad)
    {
        if (!_partPreviews.ContainsKey(name))
            _previewQueue.Enqueue((name, rad));
    }
    
    /// <summary>
    /// Computes the intersection of the mouse ray with the horizontal ground plane (Y = 250)
    /// in world space. Returns false if the ray doesn't hit the plane (parallel or behind camera).
    /// </summary>
    private bool TryGetGroundPositionAtMouse(int screenX, int screenY, out Vector3 result)
    {
        result = default;
        var viewport = _graphicsDevice.Viewport;
        float ndcX = (2.0f * screenX) / viewport.Width - 1.0f;
        float ndcY = 1.0f - (2.0f * screenY) / viewport.Height;
        
        var rayClip = new Vector4(ndcX, ndcY, -1.0f, 1.0f);
        var projMatrix = camera.ProjectionMatrix;
        Matrix.Invert(ref projMatrix, out var invProj);
        var rayEye = Microsoft.Xna.Framework.Vector4.Transform(rayClip, invProj);
        rayEye.Z = -1.0f;
        rayEye.W = 0.0f;
        
        var viewMatrix = camera.ViewMatrix;
        Matrix.Invert(ref viewMatrix, out var invView);
        var rayWorld4 = Microsoft.Xna.Framework.Vector4.Transform(rayEye, invView);
        var rayDirection = new Vector3(rayWorld4.X, rayWorld4.Y, rayWorld4.Z);
        rayDirection.Normalize();
        var rayOrigin = camera.Position;
        
        const float groundY = 250f;
        if (Math.Abs(rayDirection.Y) < 0.0001f) return false;
        float t = (groundY - rayOrigin.Y) / rayDirection.Y;
        if (t <= 0) return false;
        
        result = rayOrigin + rayDirection * t;
        return true;
    }
    
    private int PerformRayPicking(int screenX, int screenY)
    {
        if (ActiveTab.ScenePieces.Count == 0) return -1;
        
        var viewport = _graphicsDevice.Viewport;
        
        // Convert screen coords to normalized device coordinates
        float ndcX = (2.0f * screenX) / viewport.Width - 1.0f;
        float ndcY = 1.0f - (2.0f * screenY) / viewport.Height;
        
        // Create ray in clip space
        var rayClip = new Vector4(ndcX, ndcY, -1.0f, 1.0f);
        
        // Transform to view space
        var projMatrix = camera.ProjectionMatrix;
        Matrix.Invert(ref projMatrix, out var invProj);
        var rayEye = Microsoft.Xna.Framework.Vector4.Transform(rayClip, invProj);
        rayEye.Z = -1.0f;
        rayEye.W = 0.0f;
        
        // Transform to world space
        var viewMatrix = camera.ViewMatrix;
        Matrix.Invert(ref viewMatrix, out var invView);
        var rayWorld4 = Microsoft.Xna.Framework.Vector4.Transform(rayEye, invView);
        var rayDirection = new Vector3(rayWorld4.X, rayWorld4.Y, rayWorld4.Z);
        rayDirection.Normalize();
        
        var rayOrigin = camera.Position;
        
        // Find closest intersected piece using proper ray-triangle intersection
        float closestDistance = float.MaxValue;
        int closestPieceId = -1;
        
        foreach (var piece in ActiveTab.ScenePieces)
        {
            if (piece.Obj == null) continue;
            
            var mesh = piece.Obj;
            
            // Create rotation matrix matching the game engine's order
            // Rotation is stored as (Pitch, Yaw, Roll) in degrees
            // Negate yaw to match game engine's coordinate system  
            var yaw = -piece.Rotation.Y * (fix64)Math.PI / 180;
            var pitch = piece.Rotation.X * (fix64)Math.PI / 180;
            var roll = piece.Rotation.Z * (fix64)Math.PI / 180;
            
            // Create individual rotation matrices and combine them
            var rotationMatrix = 
                Matrix.CreateRotationY((float)yaw) *
                Matrix.CreateRotationX((float)pitch) *
                Matrix.CreateRotationZ((float)roll);
            
            // Test each polygon
            foreach (var poly in mesh.Rad.Polys)
            {
                if (poly.Points.Length < 3) continue;
                
                // Transform all vertices to world space
                var worldVerts = new Vector3[poly.Points.Length];
                for (int i = 0; i < poly.Points.Length; i++)
                {
                    var localVert = new Vector3(
                        poly.Points[i].X,
                        poly.Points[i].Y,
                        poly.Points[i].Z
                    );
                    
                    // Apply rotation then translation
                    var rotated = Vector3.Transform(localVert, rotationMatrix);
                    worldVerts[i] = new Vector3(
                        rotated.X + (float)piece.Position.X,
                        rotated.Y + (float)piece.Position.Y,
                        rotated.Z + (float)piece.Position.Z
                    );
                }
                
                // Test all triangles in the polygon (fan triangulation)
                for (int i = 2; i < poly.Points.Length; i++)
                {
                    var v0 = worldVerts[0];
                    var v1 = worldVerts[i - 1];
                    var v2 = worldVerts[i];
                    
                    // Try both winding orders
                    if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v1, v2, out float dist))
                    {
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestPieceId = piece.Id;
                        }
                    }
                    else if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v2, v1, out dist))
                    {
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestPieceId = piece.Id;
                        }
                    }
                }
            }
        }
        
        return closestPieceId;
    }
    
    private bool RayIntersectsTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        out float distance)
    {
        const float EPSILON = 0.001f; // Increased for better tolerance
        distance = 0;
        
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        
        // Check for degenerate triangle
        var edgeLen1 = edge1.Length();
        var edgeLen2 = edge2.Length();
        if (edgeLen1 < EPSILON || edgeLen2 < EPSILON)
            return false;
        
        var h = Vector3.Cross(rayDirection, edge2);
        var a = Vector3.Dot(edge1, h);
        
        // More lenient parallel check
        if (Math.Abs(a) < EPSILON)
            return false;
        
        var f = 1.0f / a;
        var s = rayOrigin - v0;
        var u = f * Vector3.Dot(s, h);
        
        // More lenient bounds checking
        if (u < -EPSILON || u > 1.0f + EPSILON)
            return false;
        
        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(rayDirection, q);
        
        // More lenient bounds checking
        if (v < -EPSILON || u + v > 1.0f + EPSILON)
            return false;
        
        var t = f * Vector3.Dot(edge2, q);
        
        // Accept slightly negative distances for near-miss cases
        if (t > -EPSILON)
        {
            distance = Math.Max(t, 0); // Clamp to non-negative
            return true;
        }
        
        return false;
    }
    
    private bool RayIntersectsBox(Vector3 rayOrigin, Vector3 rayDirection, Vector3 boxMin, Vector3 boxMax, out float distance)
    {
        // Slab method for ray-AABB intersection
        distance = 0;
        float tmin = float.MinValue;
        float tmax = float.MaxValue;
        
        // Check X slab
        if (Math.Abs(rayDirection.X) > 0.0001f)
        {
            float tx1 = (boxMin.X - rayOrigin.X) / rayDirection.X;
            float tx2 = (boxMax.X - rayOrigin.X) / rayDirection.X;
            tmin = Math.Max(tmin, Math.Min(tx1, tx2));
            tmax = Math.Min(tmax, Math.Max(tx1, tx2));
        }
        else if (rayOrigin.X < boxMin.X || rayOrigin.X > boxMax.X)
        {
            return false;
        }
        
        // Check Y slab
        if (Math.Abs(rayDirection.Y) > 0.0001f)
        {
            float ty1 = (boxMin.Y - rayOrigin.Y) / rayDirection.Y;
            float ty2 = (boxMax.Y - rayOrigin.Y) / rayDirection.Y;
            tmin = Math.Max(tmin, Math.Min(ty1, ty2));
            tmax = Math.Min(tmax, Math.Max(ty1, ty2));
        }
        else if (rayOrigin.Y < boxMin.Y || rayOrigin.Y > boxMax.Y)
        {
            return false;
        }
        
        // Check Z slab
        if (Math.Abs(rayDirection.Z) > 0.0001f)
        {
            float tz1 = (boxMin.Z - rayOrigin.Z) / rayDirection.Z;
            float tz2 = (boxMax.Z - rayOrigin.Z) / rayDirection.Z;
            tmin = Math.Max(tmin, Math.Min(tz1, tz2));
            tmax = Math.Min(tmax, Math.Max(tz1, tz2));
        }
        else if (rayOrigin.Z < boxMin.Z || rayOrigin.Z > boxMax.Z)
        {
            return false;
        }
        
        if (tmax >= tmin && tmax >= 0)
        {
            distance = Math.Max(tmin, 0); // Return entry point distance
            return true;
        }
        
        return false;
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse)
    {
        if (!GameSparker._game.IsActive) return;
        if (!_isOpen) return;
        
        // Handle gizmo dragging before anything else
        if (_gizmoDragging != GizmoAxis.None && ActiveTab != null)
        {
            var selectedPiece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
            if (selectedPiece != null)
            {
                int dx = x - _gizmoDragStartX;
                int dy = y - _gizmoDragStartY;
                
                if (_gizmoDragging == GizmoAxis.X)
                {
                    // Project the gizmo arrow from the centroid to get pixels-per-world-unit ratio
                    var piecePos = new Vector3(_gizmoCentroidX, _gizmoCentroidY, _gizmoCentroidZ);
                    if (WorldToScreen(piecePos, out var ss0) && WorldToScreen(piecePos + new Vector3(GIZMO_ARROW_LENGTH, 0, 0), out var ss1))
                    {
                        var screenArrow = ss1 - ss0;
                        float screenLen = screenArrow.Length();
                        if (screenLen > 1f)
                        {
                            var axisDir = screenArrow / screenLen;
                            float pixelDelta = System.Numerics.Vector2.Dot(new System.Numerics.Vector2(dx, dy), axisDir);
                            float worldDelta = pixelDelta * (GIZMO_ARROW_LENGTH / screenLen);
                            // Apply delta to every selected piece using its own start position
                            foreach (var (sid, spos) in _gizmoDragStartPositions)
                            {
                                var sp = ActiveTab.ScenePieces.Find(p => p.Id == sid);
                                if (sp == null) continue;
                                float newX = (float)spos.X + worldDelta;
                                if (_snapEnabled && _snapSize > 0f)
                                    newX = MathF.Round(newX / _snapSize) * _snapSize;
                                sp.Position = new f64Vector3((fix64)newX, spos.Y, spos.Z);
                            }
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                }
                else if (_gizmoDragging == GizmoAxis.Y)
                {
                    // Y axis: project the upward arrow (world -Y direction) to screen
                    var piecePos = new Vector3(_gizmoCentroidX, _gizmoCentroidY, _gizmoCentroidZ);
                    if (WorldToScreen(piecePos, out var ss0) && WorldToScreen(piecePos + new Vector3(0, -GIZMO_ARROW_LENGTH, 0), out var ss1))
                    {
                        var screenArrow = ss1 - ss0;
                        float screenLen = screenArrow.Length();
                        if (screenLen > 1f)
                        {
                            var axisDir = screenArrow / screenLen;
                            float pixelDelta = System.Numerics.Vector2.Dot(new System.Numerics.Vector2(dx, dy), axisDir);
                            // Moving up on screen decreases world Y (camera is flipped), so negate
                            float worldDelta = -pixelDelta * (GIZMO_ARROW_LENGTH / screenLen);
                            foreach (var (sid, spos) in _gizmoDragStartPositions)
                            {
                                var sp = ActiveTab.ScenePieces.Find(p => p.Id == sid);
                                if (sp == null) continue;
                                float newY = (float)spos.Y + worldDelta;
                                if (_snapEnabled && _snapSize > 0f)
                                    newY = MathF.Round(newY / _snapSize) * _snapSize;
                                sp.Position = new f64Vector3(spos.X, (fix64)newY, spos.Z);
                            }
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                }
                else if (_gizmoDragging == GizmoAxis.Z)
                {
                    var piecePos = new Vector3(_gizmoCentroidX, _gizmoCentroidY, _gizmoCentroidZ);
                    if (WorldToScreen(piecePos, out var ss0) && WorldToScreen(piecePos + new Vector3(0, 0, GIZMO_ARROW_LENGTH), out var ss1))
                    {
                        var screenArrow = ss1 - ss0;
                        float screenLen = screenArrow.Length();
                        if (screenLen > 1f)
                        {
                            var axisDir = screenArrow / screenLen;
                            float pixelDelta = System.Numerics.Vector2.Dot(new System.Numerics.Vector2(dx, dy), axisDir);
                            float worldDelta = pixelDelta * (GIZMO_ARROW_LENGTH / screenLen);
                            foreach (var (sid, spos) in _gizmoDragStartPositions)
                            {
                                var sp = ActiveTab.ScenePieces.Find(p => p.Id == sid);
                                if (sp == null) continue;
                                float newZ = (float)spos.Z + worldDelta;
                                if (_snapEnabled && _snapSize > 0f)
                                    newZ = MathF.Round(newZ / _snapSize) * _snapSize;
                                sp.Position = new f64Vector3(spos.X, spos.Y, (fix64)newZ);
                            }
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                }
                else if (_gizmoDragging == GizmoAxis.RotY)
                {
                    // Angle delta based on horizontal drag
                    float angleDelta = dx * 0.5f; // degrees per pixel
                    float radians = angleDelta * MathF.PI / 180f;
                    float cosA = MathF.Cos(radians);
                    float sinA = MathF.Sin(radians);
                    // Rotate every selected piece's position around the centroid and its own yaw
                    foreach (var (sid, startPos) in _gizmoDragStartPositions)
                    {
                        var sp = ActiveTab.ScenePieces.Find(p => p.Id == sid);
                        if (sp == null) continue;
                        float relX = (float)startPos.X - _gizmoCentroidX;
                        float relZ = (float)startPos.Z - _gizmoCentroidZ;
                        float newRelX = relX * cosA - relZ * sinA;
                        float newRelZ = relX * sinA + relZ * cosA;
                        sp.Position = new f64Vector3(
                            (fix64)(_gizmoCentroidX + newRelX),
                            startPos.Y,
                            (fix64)(_gizmoCentroidZ + newRelZ));
                        float startRot = _gizmoDragStartRotations.TryGetValue(sid, out var r) ? r : 0f;
                        sp.Rotation = new f64Vector3(sp.Rotation.X, (fix64)((startRot + angleDelta) % 360f), sp.Rotation.Z);
                    }
                    ActiveTab.HasUnsavedChanges = true;
                }
            }
            _mouseX = x; _mouseY = y;
            return;
        }
        
        _mouseX = x;
        _mouseY = y;
        
        // Update rect selection end while LMB is held
        if (_isRectSelecting)
        {
            _rectSelectEndX = x;
            _rectSelectEndY = y;
        }
        
        // Update placement preview position while hovering over the viewport
        if (_pendingPlacementPartIndex >= 0)
        {
            if (IsMouseInViewport(x, y))
            {
                _hasValidPlacementPos = TryGetGroundPositionAtMouse(x, y, out var groundPos);
                if (_hasValidPlacementPos)
                {
                    float gx = groundPos.X;
                    float gz = groundPos.Z;
                    if (_snapEnabled && _snapSize > 0f)
                    {
                        gx = MathF.Round(gx / _snapSize) * _snapSize;
                        gz = MathF.Round(gz / _snapSize) * _snapSize;
                    }
                    _pendingPlacementPos = new f64Vector3((fix64)gx, (fix64)groundPos.Y, (fix64)gz);
                }
            }
            else
            {
                _hasValidPlacementPos = false;
            }
        }
        
        // Check if right mouse button is currently held down
        var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        bool isRightButtonHeld = mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        
        // Start dragging if right button is held, we're in viewport, in Scene view, and not already dragging
        if (isRightButtonHeld && IsMouseInViewport(x, y) && ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene && !_isRightDragging)
        {
            _isRightDragging = true;
            _rightDragStartX = x;
            _rightDragStartY = y;
            _rightDragStartYaw = ActiveTab.CameraYaw;
            _rightDragStartPitch = ActiveTab.CameraPitch;
        }
        
        // Handle right-click drag for camera rotation (only in Scene view)
        if (_isRightDragging && isRightButtonHeld && !imguiWantsMouse && ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene)
        {
            int deltaX = x - _rightDragStartX;
            int deltaY = y - _rightDragStartY;
            
            ActiveTab.CameraYaw = _rightDragStartYaw + deltaX * 0.5f;
            ActiveTab.CameraPitch = Math.Clamp(_rightDragStartPitch + deltaY * 0.5f, -89f, 89f); // Inverted pitch
            
            UpdateCameraPosition();
        }
        
        // Stop dragging if right button is released
        if (!isRightButtonHeld && _isRightDragging)
        {
            _isRightDragging = false;
        }
    }
    
    public override void MousePressed(int x, int y, bool imguiWantsMouse)
    {
        if (imguiWantsMouse) return;
        if (!GameSparker._game.IsActive) return;
        if (!_isOpen) return;
        
        _mouseX = x;
        _mouseY = y;
        
        // Check if it's right mouse button via Microsoft.Xna.Framework.Input.Mouse
        var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        
        if (mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
        {
            // Right-click for camera rotation (only in Scene view)
            _isRightButtonDown = true;
            
            if (IsMouseInViewport(x, y) && !_isRightDragging && ActiveTab?.ViewMode == StageEditorTab.ViewModeEnum.Scene)
            {
                _isRightDragging = true;
                _rightDragStartX = x;
                _rightDragStartY = y;
                _rightDragStartYaw = ActiveTab.CameraYaw;
                _rightDragStartPitch = ActiveTab.CameraPitch;
            }
        }
        else
        {
            // Left-click
            _isLeftButtonDown = true;
            
            // Check if clicking a gizmo handle first
            if (_gizmoHovered != GizmoAxis.None && ActiveTab != null && ActiveTab.SelectedPieceId >= 0)
            {
                var piece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
                if (piece != null)
                {
                    PushUndoSnapshot();
                    _gizmoDragging = _gizmoHovered;
                    _gizmoDragStartX = x;
                    _gizmoDragStartY = y;
                    _gizmoDragStartRotY = (float)piece.Rotation.Y;
                    // Compute centroid of the whole selection as pivot
                    var centroid = ComputeSelectionCentroid();
                    _gizmoCentroidX = centroid.X;
                    _gizmoCentroidY = centroid.Y;
                    _gizmoCentroidZ = centroid.Z;
                    _gizmoDragStartPosX = centroid.X;
                    _gizmoDragStartPosY = centroid.Y;
                    _gizmoDragStartPosZ = centroid.Z;
                    // Capture start positions of all selected pieces for group drag
                    _gizmoDragStartPositions.Clear();
                    _gizmoDragStartRotations.Clear();
                    foreach (var selId in ActiveTab.SelectedPieceIds)
                    {
                        var selP = ActiveTab.ScenePieces.Find(p => p.Id == selId);
                        if (selP != null)
                        {
                            _gizmoDragStartPositions[selId] = selP.Position;
                            _gizmoDragStartRotations[selId] = (float)selP.Rotation.Y;
                        }
                    }
                }
            }
            else if (IsMouseInViewport(x, y) && _pendingPlacementPartIndex < 0 && !_isSwapMode)
            {
                // Begin potential rect selection
                _isRectSelecting = true;
                _rectSelectStartX = x;
                _rectSelectStartY = y;
                _rectSelectEndX = x;
                _rectSelectEndY = y;
            }
        }
    }
    
    public override void MouseScrolled(int delta, bool imguiWantsMouse)
    {
        if (imguiWantsMouse) return;
        if (!GameSparker._game.IsActive) return;
        if (!_isOpen) return;
        
        // Only act if mouse is in viewport
        if (IsMouseInViewport(_mouseX, _mouseY))
        {
            if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
            {
                // Adjust top-down height
                ActiveTab.TopDownHeight = Math.Clamp(ActiveTab.TopDownHeight - delta * 15f, 500f, 50000f);
                UpdateCameraPosition();
            }
            else
            {
                // Keep old distance tracking for compatibility
                ActiveTab.CameraDistance = Math.Clamp(ActiveTab.CameraDistance - delta * 50f, 100f, 10000f);
            }
        }
    }
    
    public override void MouseReleased(int x, int y, bool imguiWantsMouse)
    {
        // Check if it's right mouse button
        var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        
        if (mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _isRightButtonDown)
        {
            _isRightButtonDown = false;
            
            if (_isRightDragging)
            {
                _isRightDragging = false;
            }
            else if (_pendingPlacementPartIndex >= 0)
            {
                // Right-click cancels placement mode
                _pendingPlacementPartIndex = -1;
                _hasValidPlacementPos = false;
            }
        }
        else if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _isLeftButtonDown)
        {
            _isLeftButtonDown = false;
            
            // If we were dragging gizmo, stop and skip ray picking
            if (_gizmoDragging != GizmoAxis.None)
            {
                _gizmoDragging = GizmoAxis.None;
                return;
            }
        
        // Finalise rect selection if active
        if (_isRectSelecting)
        {
            _isRectSelecting = false;
            int rw = Math.Abs(_rectSelectEndX - _rectSelectStartX);
            int rh = Math.Abs(_rectSelectEndY - _rectSelectStartY);
            bool isRectDrag = rw > 5 || rh > 5;
            
            if (isRectDrag && ActiveTab != null && !imguiWantsMouse)
            {
                int minX = Math.Min(_rectSelectStartX, _rectSelectEndX);
                int maxX = Math.Max(_rectSelectStartX, _rectSelectEndX);
                int minY = Math.Min(_rectSelectStartY, _rectSelectEndY);
                int maxY = Math.Max(_rectSelectStartY, _rectSelectEndY);
                
                if (!_isShiftPressed) ActiveTab.SelectedPieceIds.Clear();
                int lastId = -1;
                foreach (var piece in ActiveTab.ScenePieces)
                {
                    if (piece.Obj == null) continue;
                    var wp = new Vector3((float)piece.Position.X, (float)piece.Position.Y, (float)piece.Position.Z);
                    if (WorldToScreen(wp, out var sp))
                    {
                        if (sp.X >= minX && sp.X <= maxX && sp.Y >= minY && sp.Y <= maxY)
                        {
                            ActiveTab.SelectedPieceIds.Add(piece.Id);
                            lastId = piece.Id;
                        }
                    }
                }
                if (lastId >= 0)
                {
                    ActiveTab.SelectedPieceId = lastId;
                    ActiveTab.SelectedWallId = -1;
                }
                return;
            }
            // else fall through to normal single click logic
        }
                // Handle piece selection on left click
                if (!imguiWantsMouse && IsMouseInViewport(x, y))
                {
                    // Placement mode: spawn the part at the hovered ground position
                    if (_pendingPlacementPartIndex >= 0)
                    {
                        if (_hasValidPlacementPos && ActiveTab?.Stage != null)
                        {
                            var pendingPart = _availableParts[_pendingPlacementPartIndex];
                            if (pendingPart.Rad != null)
                            {
                                var placementRot = new f64Euler(
                                    f64AngleSingle.FromDegrees((fix64)_pendingPlacementYaw),
                                    f64AngleSingle.ZeroAngle,
                                    f64AngleSingle.ZeroAngle);
                                var newMesh = new StageObject(pendingPart.Rad, _pendingPlacementPos, placementRot);
                                var instance = new StagePieceInstance(pendingPart.Name, newMesh, ActiveTab.GetNextPieceId());
                                instance.Position = _pendingPlacementPos;
                                instance.Rotation = new f64Vector3((fix64)0, (fix64)_pendingPlacementYaw, (fix64)0);
                                PushUndoSnapshot();
                                ActiveTab.ScenePieces.Add(instance);
                                ActiveTab.Stage.pieces[ActiveTab.Stage.stagePartCount] = newMesh;
                                ActiveTab.SelectedPieceId = instance.Id;
                                ActiveTab.HasUnsavedChanges = true;
                                RebuildClientRenderer();
                                // Stay in placement mode so the user can keep placing the same part
                            }
                        }
                        return; // Don't do ray picking while in placement mode
                    }
                    
                    var pickedPieceId = PerformRayPicking(x, y);
                    if (pickedPieceId >= 0)
                    {
                        if (_isCtrlPressed)
                        {
                            // Toggle in multi-selection
                            if (!ActiveTab.SelectedPieceIds.Remove(pickedPieceId))
                                ActiveTab.SelectedPieceIds.Add(pickedPieceId);
                            ActiveTab.SelectedPieceId = pickedPieceId;
                        }
                        else
                        {
                            ActiveTab.SelectedPieceIds.Clear();
                            ActiveTab.SelectedPieceIds.Add(pickedPieceId);
                            ActiveTab.SelectedPieceId = pickedPieceId;
                        }
                        ActiveTab.SelectedWallId = -1;
                    }
                    else
                    {
                        if (!_isCtrlPressed)
                        {
                            ActiveTab.SelectedPieceIds.Clear();
                            ActiveTab.SelectedPieceId = -1;
                        }
                    }
                }
        }
    }
    
    public override void GameTick()
    {
        if (!_isOpen) return;
        if (ActiveTab == null) return;
        
        // Handle camera movement with WASD in first-person flying mode
        if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene)
        {
            float yaw = ActiveTab.CameraYaw * (float)Math.PI / 180f;
            float pitch = ActiveTab.CameraPitch * (float)Math.PI / 180f;
            
            // Calculate forward vector based on camera orientation
            var forward = new Vector3(
                (float)(Math.Cos(pitch) * Math.Sin(yaw)),
                (float)Math.Sin(pitch),
                (float)(Math.Cos(pitch) * Math.Cos(yaw))
            );
            forward.Normalize();
            
            // Calculate right vector (perpendicular to forward on XZ plane)
            var right = new Vector3(
                (float)Math.Cos(yaw),
                0,
                -(float)Math.Sin(yaw)
            );
            right.Normalize();
            
            var up = Vector3.UnitY;
            
            // Move camera position directly (first-person flying); hold Shift to sprint at 3× speed
            float camSpeed = CAMERA_MOVE_SPEED * (_isShiftPressed ? 3f : 1f);
            if (_moveForward)
                ActiveTab.CameraPosition += forward * camSpeed;
            if (_moveBackward)
                ActiveTab.CameraPosition -= forward * camSpeed;
            if (_moveLeft)
                ActiveTab.CameraPosition -= right * camSpeed;
            if (_moveRight)
                ActiveTab.CameraPosition += right * camSpeed;
            if (_moveUp)
                ActiveTab.CameraPosition += up * camSpeed;
            if (_moveDown)
                ActiveTab.CameraPosition -= up * camSpeed;
            
            UpdateCameraPosition();
        }
        else if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
        {
            // Pan controls for top-down view (move the look-at point on XZ plane)
            var panSpeed = CAMERA_MOVE_SPEED * (_isShiftPressed ? 3f : 1f);
            
            if (_moveForward)
                ActiveTab.TopDownPanPosition += new Vector3(0, 0, panSpeed);
            if (_moveBackward)
                ActiveTab.TopDownPanPosition -= new Vector3(0, 0, panSpeed);
            if (_moveLeft)
                ActiveTab.TopDownPanPosition -= new Vector3(panSpeed, 0, 0);
            if (_moveRight)
                ActiveTab.TopDownPanPosition += new Vector3(panSpeed, 0, 0);
            
            UpdateCameraPosition();
        }
        
        // Update piece transforms in the stage
        if (ActiveTab?.Stage != null)
        {
            foreach (var piece in ActiveTab.ScenePieces)
            {
                if (piece.Obj != null)
                {
                    piece.Obj.Position = new f64Vector3(
                        piece.Position.X,
                        piece.Position.Y,
                        piece.Position.Z
                    );
                    
                    // Euler constructor is (Yaw, Pitch, Roll)
                    // piece.Rotation is stored as (Pitch, Yaw, Roll) for display consistency
                    piece.Obj.Rotation = new f64Euler(
                        f64AngleSingle.FromRadians(piece.Rotation.Y * (fix64)Math.PI / 180), // Yaw
                        f64AngleSingle.FromRadians(piece.Rotation.X * (fix64)Math.PI / 180), // Pitch
                        f64AngleSingle.FromRadians(piece.Rotation.Z * (fix64)Math.PI / 180)  // Roll
                    );
                }
            }
            
            // GameTick the renderer so StageObjectGameObjects sync position from piece.Obj
            ActiveTab.StageRenderer?.GameTick();
        }
    }
    
    public override void Render()
    {
        if (!_isOpen) return;
        if (ActiveTab == null) return;
        
        // Clear with appropriate background color based on view mode
        if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
        {
            // Gray background for top-down view
            _graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(128, 128, 128));
        }
        else
        {
            // Sky blue background for 3D scene view
            _graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(135, 206, 235));
        }
        
        // Set up scissor rectangle to only render within the viewport area
        var oldScissorRect = _graphicsDevice.ScissorRectangle;
        var oldRasterizerState = _graphicsDevice.RasterizerState;
        
        // Only set scissor if we have valid viewport bounds
        if (_viewportMax.X > _viewportMin.X && _viewportMax.Y > _viewportMin.Y)
        {
            var scissorRect = new Microsoft.Xna.Framework.Rectangle(
                (int)_viewportMin.X,
                (int)_viewportMin.Y,
                (int)(_viewportMax.X - _viewportMin.X),
                (int)(_viewportMax.Y - _viewportMin.Y)
            );
            
            var rasterizerState = new RasterizerState
            {
                CullMode = CullMode.CullCounterClockwiseFace,
                ScissorTestEnable = true
            };
            
            _graphicsDevice.ScissorRectangle = scissorRect;
            _graphicsDevice.RasterizerState = rasterizerState;
        }
        
        // Render the 3D scene
        if (ActiveTab?.Scene != null && ActiveTab?.Stage != null && ActiveTab?.StageRenderer != null)
        {
            if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
            {
                // Top-down view: with lighting, no sky/ground/polys/clouds/mountains
                var oldGround = ActiveTab?.StageRenderer.ground;
                var oldSky = ActiveTab?.StageRenderer.sky;
                var oldPolys = ActiveTab?.StageRenderer.polys;
                var oldClouds = ActiveTab?.StageRenderer.clouds;
                var oldMountains = ActiveTab?.StageRenderer.mountains;
                var oldFadeFrom = World.FadeFrom;
                
                // Temporarily remove environment elements and suppress fog
                ActiveTab?.StageRenderer.ground = null!;
                ActiveTab?.StageRenderer.sky = null!;
                ActiveTab?.StageRenderer.polys = null;
                ActiveTab?.StageRenderer.clouds = null;
                ActiveTab?.StageRenderer.mountains = null;
                World.FadeFrom = 9999999;
                
                // Render with lighting preserved
                ActiveTab?.Scene.Render(false);
                
                // Restore environment elements
                ActiveTab?.StageRenderer.ground = oldGround;
                ActiveTab?.StageRenderer.sky = oldSky;
                ActiveTab?.StageRenderer.polys = oldPolys;
                ActiveTab?.StageRenderer.clouds = oldClouds;
                ActiveTab?.StageRenderer.mountains = oldMountains;
                World.FadeFrom = oldFadeFrom;
            }
            else
            {
                // Normal 3D view with lighting and ground
                ActiveTab?.Scene.Render(false);
            }
        }
        
        // Render wall meshes separately (editor-only visualization) - BEFORE restoring scissor state
        if (ActiveTab != null)
        // Wall meshes are now part of the Scene (added in RecreateScene), no separate render needed
        
        // Restore old state
        _graphicsDevice.ScissorRectangle = oldScissorRect;
        _graphicsDevice.RasterizerState = oldRasterizerState;
        
        // Render selection highlight for all selected pieces, gizmo on primary
        var highlightIds = ActiveTab.SelectedPieceIds.Count > 0
            ? ActiveTab.SelectedPieceIds
            : ActiveTab.SelectedPieceId >= 0 ? (IEnumerable<int>)new[] { ActiveTab.SelectedPieceId } : null;
        if (highlightIds != null)
        {
            foreach (var hid in highlightIds)
            {
                var hp = ActiveTab.ScenePieces.Find(p => p.Id == hid);
                if (hp?.Obj != null)
                    RenderSelectionHighlight(hp);
            }
            if (ActiveTab.SelectedPieceId >= 0)
            {
                var selectedPiece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
                if (selectedPiece?.Obj != null)
                    RenderGizmo(ComputeSelectionCentroid());
            }
        }
        
        // Process one pending preview thumbnail per frame
        ProcessOnePreviewThumbnail();
        
        // Render placement ghost if in placement mode and mouse is over viewport
        if (_pendingPlacementPartIndex >= 0 && _hasValidPlacementPos)
            RenderPlacementPreview();
        
        // Clear the depth buffer so ImGui always renders on top of the 3D scene.
        // Without this, geometry close to the camera writes near-zero depth values and
        // ImGui pixels (rendered later with DepthRead) fail the depth test at those positions.
        _graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1.0f, 0);
    }
    
    public override void RenderImgui()
    {
        if (!_isOpen) return;
        
        RenderImGuiUI();
    }
    
    private void RenderImGuiUI()
    {
        var screenWidth = GameSparker._game.GraphicsDevice.Viewport.Width;
        var screenHeight = GameSparker._game.GraphicsDevice.Viewport.Height;
        
        // Menu bar at the top
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Stage"))
                {
                    _showNewStageDialog = true;
                    _newStageName = "";
                }
                
                if (ImGui.MenuItem("Load Stage"))
                {
                    _showLoadStageDialog = true;
                    RefreshAvailableStages();
                    _selectedStageIndex = -1;
                }
                
                if (ImGui.MenuItem("Save Stage", "", false, ActiveTab?.Stage != null))
                {
                    SaveStage();
                }
                
                ImGui.Separator();
                
                if (ImGui.MenuItem("Exit to Main Menu"))
                {
                    // Check all tabs for unsaved changes
                    bool hasAnyUnsavedChanges = _tabs.Any(t => t.HasUnsavedChanges);
                    if (hasAnyUnsavedChanges)
                    {
                        _showExitWarningDialog = true;
                    }
                    else
                    {
                        GameSparker.ReturnToMainMenu();
                    }
                }
                
                ImGui.EndMenu();
            }
            
            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Properties", "", false, ActiveTab?.Stage != null))
                {
                    // Initialize dialog values from active tab's stored values
                    _editStageName = ActiveTab?.TabName ?? "";
                    _editSkyColor = new System.Numerics.Vector3(ActiveTab.SkyColor.X / 255f, ActiveTab.SkyColor.Y / 255f, ActiveTab.SkyColor.Z / 255f);
                    _editFogColor = new System.Numerics.Vector3(ActiveTab.FogColor.X / 255f, ActiveTab.FogColor.Y / 255f, ActiveTab.FogColor.Z / 255f);
                    _editGroundColor = new System.Numerics.Vector3(ActiveTab.GroundColor.X / 255f, ActiveTab.GroundColor.Y / 255f, ActiveTab.GroundColor.Z / 255f);
                    _editPolysEnabled = ActiveTab.PolysEnabled;
                    if (_editPolysEnabled)
                    {
                        _editPolysColor = new System.Numerics.Vector3(ActiveTab.PolysColor.X / 255f, ActiveTab.PolysColor.Y / 255f, ActiveTab.PolysColor.Z / 255f);
                    }
                    else
                    {
                        // Auto-calculate from ground color (reduce by 10 points)
                        _editPolysColor = new System.Numerics.Vector3(
                            Math.Max(0, ActiveTab.GroundColor.X - 10) / 255f,
                            Math.Max(0, ActiveTab.GroundColor.Y - 10) / 255f,
                            Math.Max(0, ActiveTab.GroundColor.Z - 10) / 255f
                        );
                    }
                    _editCloudsEnabled = ActiveTab.CloudsEnabled;
                    _editCloudsColor = new System.Numerics.Vector3(ActiveTab.CloudsColor.X / 255f, ActiveTab.CloudsColor.Y / 255f, ActiveTab.CloudsColor.Z / 255f);
                    _editCloudsParam4 = ActiveTab.CloudsParam4;
                    _editCloudsHeight = ActiveTab.CloudsHeight;
                    _editCloudCoverage = ActiveTab.CloudCoverage;
                    _editMountainsEnabled = ActiveTab.MountainsEnabled;
                    _editMountainsSeed = ActiveTab.MountainsSeed;
                    _editSnapA = ActiveTab.SnapA;
                    _editSnapB = ActiveTab.SnapB;
                    _editSnapC = ActiveTab.SnapC;
                    _editFadeFrom = ActiveTab.FadeFrom;
                    
                    _showPropertiesDialog = true;
                }
                
                ImGui.EndMenu();
            }
            
            if (ImGui.BeginMenu("View"))
            {
                if (ActiveTab == null)
                {
                    ImGui.TextDisabled("No stage loaded");
                }
                else if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
                {
                    if (ImGui.MenuItem("Orthographic", "", ActiveTab.TopDownOrtho))
                    {
                        ActiveTab.TopDownOrtho = !ActiveTab.TopDownOrtho;
                        UpdateCameraPosition();
                    }
                }
                else
                {
                    ImGui.TextDisabled("Switch to Top Down View for options");
                }
                
                ImGui.EndMenu();
            }
            
            // Display camera info and stage name
            if (ActiveTab?.Stage != null)
            {
                ImGui.SetNextItemWidth(200);
                ImGui.Text($"  |  Stage: {ActiveTab.TabName}");
                if (ActiveTab.HasUnsavedChanges)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.6f, 0.1f, 1.0f), "(unsaved)");
                }
                ImGui.SameLine();
                ImGui.TextDisabled($"  Yaw={ActiveTab.CameraYaw:F1}°  Pitch={ActiveTab.CameraPitch:F1}°  |  {ActiveTab.ScenePieces.Count} pieces");
            }
            else
            {
                ImGui.TextDisabled("  |  No stage loaded — File > New Stage or Load Stage");
            }
            
            ImGui.EndMainMenuBar();
        }
        
        // Draw tabs below menu bar (full width for stage file tabs)
        float menuBarHeight = ImGui.GetFrameHeight();
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, menuBarHeight));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(screenWidth, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(4, 4));
        ImGui.Begin("StageTabsWindow", 
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav);
        
        if (ImGui.BeginTabBar("StageTabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs))
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                bool open = true;
                string tabLabel = tab.TabName + (tab.HasUnsavedChanges ? "*" : "");
                
                if (ImGui.BeginTabItem(tabLabel, ref open))
                {
                    if (_activeTabIndex != i)
                    {
                        // Switch to new tab: tab stored values are authoritative — never read World back into them.
                        // Just restore this tab's World properties and rebuild environment.
                        _activeTabIndex = i;
                        UpdateCameraPosition();
                        ApplyTabWorldValuesToWorld();
                        RecreateEnvironment();
                        RebuildAllWalls();
                        RecreateScene();
                    }
                    ImGui.EndTabItem();
                }
                
                if (!open)
                {
                    CloseTab(i);
                    break; // Exit loop after closing a tab to avoid index issues
                }
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        ImGui.PopStyleVar();
        
        float tabBarHeight = ImGui.GetFrameHeight();
        float totalHeaderHeight = menuBarHeight + tabBarHeight + 12; // Add 4px spacing
        
        // New Stage Dialog
        if (_showNewStageDialog)
        {
            ImGui.OpenPopup("New Stage");
        }
        
        if (ImGui.BeginPopupModal("New Stage", ref _showNewStageDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Enter stage name:");
            ImGui.Separator();
            
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("##stagename", ref _newStageName, 100);
            
            if (!string.IsNullOrWhiteSpace(_newStageName))
            {
                var filename = ConvertStageNameToFilename(_newStageName);
                ImGui.Text($"Filename: {filename}.txt");
            }
            
            ImGui.Spacing();
            ImGui.Text("Start piece (placed at 0, 0, 0):");
            ImGui.SetNextItemWidth(300);
            if (ImGui.BeginCombo("##starpiece", _newStageStartPartOptions[_newStageStartPartIndex]))
            {
                for (int si = 0; si < _newStageStartPartOptions.Length; si++)
                {
                    bool sel = si == _newStageStartPartIndex;
                    if (ImGui.Selectable(_newStageStartPartOptions[si], sel))
                        _newStageStartPartIndex = si;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This part will be automatically placed at the origin (0, 0, 0)\nwhen the stage is created.");
            
            ImGui.Separator();
            
            if (ImGui.Button("Create", new System.Numerics.Vector2(120, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_newStageName))
                {
                    string? startPart = _newStageStartPartIndex > 0 ? _newStageStartPartOptions[_newStageStartPartIndex] : null;
                    CreateEmptyStage(_newStageName, startPart);
                    _showNewStageDialog = false;
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
            {
                _showNewStageDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Load Stage Dialog
        if (_showLoadStageDialog)
        {
            ImGui.OpenPopup("Load Stage");
        }
        
        if (ImGui.BeginPopupModal("Load Stage", ref _showLoadStageDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Select a stage to load:");
            ImGui.Separator();
            
            ImGui.BeginChild("StageList", new System.Numerics.Vector2(300, 200), (ImGuiChildFlags)1);
            
            for (int i = 0; i < _availableStages.Count; i++)
            {
                if (ImGui.Selectable(_availableStages[i], _selectedStageIndex == i))
                {
                    _selectedStageIndex = i;
                }
            }
            
            ImGui.EndChild();
            
            if (_availableStages.Count == 0)
            {
                ImGui.TextDisabled("No user stages found in data/stages/user/");
            }
            
            ImGui.Separator();
            
            if (ImGui.Button("Load", new System.Numerics.Vector2(120, 0)))
            {
                if (_selectedStageIndex >= 0 && _selectedStageIndex < _availableStages.Count)
                {
                    LoadStage(_availableStages[_selectedStageIndex]);
                    _showLoadStageDialog = false;
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
            {
                _showLoadStageDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Properties Dialog
        if (_showPropertiesDialog)
        {
            ImGui.OpenPopup("Stage Properties");
        }
        
        if (ImGui.BeginPopupModal("Stage Properties", ref _showPropertiesDialog, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav))
        {
            ImGui.Text("Configure stage properties (changes preview live):");
            ImGui.Separator();
            
            ImGui.Text("Stage Name:");
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("##stagename_edit", ref _editStageName, 100);
            
            ImGui.Separator();
            
            ImGui.Text("Sky Color:");
            if (ImGui.ColorEdit3("##skycolor", ref _editSkyColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
            {
                // Live preview
                World.Sky = new Color3((short)(_editSkyColor.X * 255), (short)(_editSkyColor.Y * 255), (short)(_editSkyColor.Z * 255));
                if (ActiveTab?.StageRenderer != null) ActiveTab.StageRenderer.sky = new Sky(_graphicsDevice);
            }
            
            ImGui.Text("Fog Color:");
            if (ImGui.ColorEdit3("##fogcolor", ref _editFogColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
            {
                // Live preview
                World.Fog = new Color3((short)(_editFogColor.X * 255), (short)(_editFogColor.Y * 255), (short)(_editFogColor.Z * 255));
            }
            
            ImGui.Text("Ground Color:");
            if (ImGui.ColorEdit3("##groundcolor", ref _editGroundColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
            {
                // Live preview
                World.GroundColor = new Color3((short)(_editGroundColor.X * 255), (short)(_editGroundColor.Y * 255), (short)(_editGroundColor.Z * 255));
                if (ActiveTab?.StageRenderer != null) ActiveTab.StageRenderer.ground = new Ground(_graphicsDevice);
            }
            
            ImGui.Separator();
            
            if (ImGui.Checkbox("Enable Ground Polys", ref _editPolysEnabled))
            {
                // Live preview
                World.HasPolys = _editPolysEnabled;
                World.DrawPolys = _editPolysEnabled;
                if (_editPolysEnabled && ActiveTab?.StageRenderer != null && ActiveTab?.Stage != null)
                {
                    World.GroundPolysColor = new Color3(
                        (short)(_editPolysColor.X * 255),
                        (short)(_editPolysColor.Y * 255),
                        (short)(_editPolysColor.Z * 255)
                    );
                    ActiveTab.StageRenderer.polys = Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count, _graphicsDevice);
                }
                else if (!_editPolysEnabled && ActiveTab?.StageRenderer != null)
                {
                    ActiveTab.StageRenderer.polys = null;
                }
            }
            if (_editPolysEnabled)
            {
                ImGui.Text("Polys Color:");
                if (ImGui.ColorEdit3("##polyscolor", ref _editPolysColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
                {
                    // Live preview
                    World.GroundPolysColor = new Color3(
                        (short)(_editPolysColor.X * 255),
                        (short)(_editPolysColor.Y * 255),
                        (short)(_editPolysColor.Z * 255)
                    );
                    if (ActiveTab?.StageRenderer != null)
                    {
                        ActiveTab.StageRenderer.polys = Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count, _graphicsDevice);
                    }
                }
            }
            
            ImGui.Separator();
            
            if (ImGui.Checkbox("Enable Clouds", ref _editCloudsEnabled))
            {
                // Live preview
                World.HasClouds = _editCloudsEnabled;
                World.DrawClouds = _editCloudsEnabled;
                if (_editCloudsEnabled && ActiveTab?.StageRenderer != null)
                {
                    World.Clouds = new int[] 
                    { 
                        (int)(_editCloudsColor.X * 255), 
                        (int)(_editCloudsColor.Y * 255), 
                        (int)(_editCloudsColor.Z * 255), 
                        _editCloudsParam4, 
                        _editCloudsHeight 
                    };
                    World.CloudCoverage = _editCloudCoverage;
                    ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                }
                else if (!_editCloudsEnabled && ActiveTab?.StageRenderer != null)
                {
                    ActiveTab.StageRenderer.clouds = null;
                }
            }
            if (_editCloudsEnabled)
            {
                ImGui.Text("Clouds Color:");
                if (ImGui.ColorEdit3("##cloudscolor", ref _editCloudsColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
                {
                    // Live preview
                    World.Clouds[0] = (int)(_editCloudsColor.X * 255);
                    World.Clouds[1] = (int)(_editCloudsColor.Y * 255);
                    World.Clouds[2] = (int)(_editCloudsColor.Z * 255);
                    if (ActiveTab?.StageRenderer != null)
                    {
                        ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
                
                ImGui.Text("Clouds Height:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.DragInt("##cloudsheight", ref _editCloudsHeight, 10f, -10000, 10000))
                {
                    // Live preview
                    World.Clouds[4] = _editCloudsHeight;
                    if (ActiveTab?.StageRenderer != null)
                    {
                        ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
                
                ImGui.Text("Clouds Parameter 4:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("##cloudsparam4", ref _editCloudsParam4))
                {
                    // Live preview
                    World.Clouds[3] = _editCloudsParam4;
                    if (ActiveTab?.StageRenderer != null)
                    {
                        ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
                
                ImGui.Text("Cloud Coverage:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("##cloudcoverage", ref _editCloudCoverage, 0.0f, 10.0f))
                {
                    // Live preview
                    World.CloudCoverage = _editCloudCoverage;
                    if (ActiveTab?.StageRenderer != null)
                    {
                        ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
            }
            
            if (ImGui.Checkbox("Enable Mountains", ref _editMountainsEnabled))
            {
                // Live preview
                World.DrawMountains = _editMountainsEnabled;
                if (_editMountainsEnabled && ActiveTab?.StageRenderer != null)
                {
                    World.MountainSeed = _editMountainsSeed;
                    ActiveTab.StageRenderer.mountains = Environment.MakeMountains(-10000, 10000, -10000, 10000, _graphicsDevice);
                }
                else if (!_editMountainsEnabled && ActiveTab?.StageRenderer != null)
                {
                    ActiveTab.StageRenderer.mountains = null;
                }
            }
            if (_editMountainsEnabled)
            {
                ImGui.Text("Mountains Seed:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("##mountainsseed", ref _editMountainsSeed))
                {
                    // Live preview
                    World.MountainSeed = _editMountainsSeed;
                    if (ActiveTab?.StageRenderer != null)
                    {
                        ActiveTab.StageRenderer.mountains = Environment.MakeMountains(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
            }
            
            ImGui.Separator();
            
            ImGui.Text("Environment Lighting (Snap):");
            ImGui.Text("Brightness adjustment for each RGB channel (-100 to 100):");
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderInt("A (Red)", ref _editSnapA, -100, 100))
            {
                // Live preview
                World.Snap = new Color3((short)_editSnapA, (short)_editSnapB, (short)_editSnapC);
            }
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderInt("B (Green)", ref _editSnapB, -100, 100))
            {
                // Live preview
                World.Snap = new Color3((short)_editSnapA, (short)_editSnapB, (short)_editSnapC);
            }
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderInt("C (Blue)", ref _editSnapC, -100, 100))
            {
                // Live preview
                World.Snap = new Color3((short)_editSnapA, (short)_editSnapB, (short)_editSnapC);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Brightness values that affect environment lighting.\nHigher values = brighter environment.");
            }
            
            ImGui.Separator();
            
            ImGui.Text("Fade From Distance:");
            ImGui.SetNextItemWidth(200);
            if (ImGui.DragInt("##fadefrom", ref _editFadeFrom, 100f, 1000, 50000))
            {
                // Live preview
                World.FadeFrom = _editFadeFrom;
            }
            
            ImGui.Separator();
            
            if (ImGui.Button("Apply", new System.Numerics.Vector2(120, 0)))
            {
                if (ActiveTab != null)
                {
                    // Update the tab name
                    ActiveTab.TabName = _editStageName;
                    
                    // Store all properties in the active tab
                    ActiveTab.SkyColor = new System.Numerics.Vector3(_editSkyColor.X * 255, _editSkyColor.Y * 255, _editSkyColor.Z * 255);
                    ActiveTab.FogColor = new System.Numerics.Vector3(_editFogColor.X * 255, _editFogColor.Y * 255, _editFogColor.Z * 255);
                    ActiveTab.GroundColor = new System.Numerics.Vector3(_editGroundColor.X * 255, _editGroundColor.Y * 255, _editGroundColor.Z * 255);
                    ActiveTab.PolysColor = new System.Numerics.Vector3(_editPolysColor.X * 255, _editPolysColor.Y * 255, _editPolysColor.Z * 255);
                    ActiveTab.PolysEnabled = _editPolysEnabled;
                    ActiveTab.CloudsEnabled = _editCloudsEnabled;
                    ActiveTab.CloudsColor = new System.Numerics.Vector3(_editCloudsColor.X * 255, _editCloudsColor.Y * 255, _editCloudsColor.Z * 255);
                    ActiveTab.CloudsParam4 = _editCloudsParam4;
                    ActiveTab.CloudsHeight = _editCloudsHeight;
                    ActiveTab.CloudCoverage = _editCloudCoverage;
                    ActiveTab.MountainsEnabled = _editMountainsEnabled;
                    ActiveTab.MountainsSeed = _editMountainsSeed;
                    ActiveTab.SnapA = _editSnapA;
                    ActiveTab.SnapB = _editSnapB;
                    ActiveTab.SnapC = _editSnapC;
                    ActiveTab.FadeFrom = _editFadeFrom;
                    
                    ApplyTabWorldValuesToWorld();
                    RecreateEnvironment();
                    RecreateScene();
                    ActiveTab.HasUnsavedChanges = true;
                }
                
                _showPropertiesDialog = false;
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
            {
                // Undo any live-preview changes to World by restoring from the tab's stored values
                ApplyTabWorldValuesToWorld();
                RecreateEnvironment();
                _showPropertiesDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Exit Warning Dialog
        if (_showExitWarningDialog)
        {
            ImGui.OpenPopup("Unsaved Changes");
        }
        
        if (ImGui.BeginPopupModal("Unsaved Changes", ref _showExitWarningDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("You have unsaved changes in one or more stages.");
            ImGui.Text("Are you sure you want to exit without saving?");
            ImGui.Separator();
            
            if (ImGui.Button("Exit Without Saving", new System.Numerics.Vector2(150, 0)))
            {
                _showExitWarningDialog = false;
                GameSparker.ReturnToMainMenu();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
            {
                _showExitWarningDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Close Tab Warning Dialog
        if (_showCloseTabWarningDialog)
        {
            ImGui.OpenPopup("Close Tab?");
        }
        
        if (ImGui.BeginPopupModal("Close Tab?", ref _showCloseTabWarningDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_tabToClose >= 0 && _tabToClose < _tabs.Count)
            {
                var tab = _tabs[_tabToClose];
                ImGui.Text($"Stage '{tab.TabName}' has unsaved changes.");
                ImGui.Text("Are you sure you want to close it without saving?");
                ImGui.Separator();
                
                if (ImGui.Button("Close Without Saving", new System.Numerics.Vector2(170, 0)))
                {
                    PerformCloseTab(_tabToClose);
                    _showCloseTabWarningDialog = false;
                    _tabToClose = -1;
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
                {
                    _showCloseTabWarningDialog = false;
                    _tabToClose = -1;
                }
            }
            
            ImGui.EndPopup();
        }
        
        // If no stage is loaded, show a message in the center
        if (ActiveTab?.Stage == null)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(screenWidth / 2 - 200, screenHeight / 2 - 50));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 100));
            ImGui.Begin("No Stage Loaded", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
            ImGui.Text("No stage is currently loaded.");
            ImGui.Text("Use File > New Stage to create a new stage,");
            ImGui.Text("or File > Load Stage to load an existing one.");
            ImGui.End();
            return;
        }
        
        // LEFT PANEL - Hierarchy
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, totalHeaderHeight));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(_hierarchyWidth, screenHeight - totalHeaderHeight - _partsLibraryHeight));
        
        ImGui.Begin("Hierarchy", 
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoResize | 
            ImGuiWindowFlags.NoCollapse);
        
        RenderHierarchy();
        ImGui.End();
        
        // RIGHT PANEL - Inspector
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(screenWidth - _inspectorWidth, totalHeaderHeight));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(_inspectorWidth, screenHeight - totalHeaderHeight - _partsLibraryHeight));
        
        ImGui.Begin("Inspector", 
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoResize | 
            ImGuiWindowFlags.NoCollapse);
        
        RenderInspector();
        ImGui.End();
        
        // BOTTOM PANEL - Parts Library
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, screenHeight - _partsLibraryHeight));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(screenWidth, _partsLibraryHeight));
        
        ImGui.Begin("Stage Parts Library", 
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoResize | 
            ImGuiWindowFlags.NoCollapse);
        
        RenderPartsLibrary();
        ImGui.End();
        
        // Draw viewport tabs overlay (spans full width of viewport at the top)
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(_hierarchyWidth, totalHeaderHeight));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(screenWidth - _hierarchyWidth - _inspectorWidth, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(4, 4));
        ImGui.Begin("ViewportTabs", 
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav);
        
        if (ImGui.BeginTabBar("ViewModeTabs", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("3D Scene View"))
            {
                if (ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.Scene)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.Scene;
                    UpdateCameraPosition();
                }
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Top Down View"))
            {
                if (ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.TopDown)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.TopDown;
                    UpdateCameraPosition();
                }
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        ImGui.PopStyleVar();
        
        float viewportTabsHeight = ImGui.GetFrameHeight();
        
        // Calculate viewport bounds (center area minus the UI panels, accounting for all header bars)
        _viewportMin = new System.Numerics.Vector2(_hierarchyWidth, totalHeaderHeight + viewportTabsHeight);
        _viewportMax = new System.Numerics.Vector2(screenWidth - _inspectorWidth, screenHeight - _partsLibraryHeight);
        if (IsMouseInViewport(_mouseX, _mouseY))
        {
            // Calculate 3D world position at ground level (Y = -250)
            var viewport = _graphicsDevice.Viewport;
            float ndcX = (2.0f * _mouseX) / viewport.Width - 1.0f;
            float ndcY = 1.0f - (2.0f * _mouseY) / viewport.Height;
            
            var rayClip = new Vector4(ndcX, ndcY, -1.0f, 1.0f);
            var projMatrix = camera.ProjectionMatrix;
            Matrix.Invert(ref projMatrix, out var invProj);
            var rayEye = Microsoft.Xna.Framework.Vector4.Transform(rayClip, invProj);
            rayEye.Z = -1.0f;
            rayEye.W = 0.0f;
            
            var viewMatrix = camera.ViewMatrix;
            Matrix.Invert(ref viewMatrix, out var invView);
            var rayWorld4 = Microsoft.Xna.Framework.Vector4.Transform(rayEye, invView);
            var rayDirection = new Vector3(rayWorld4.X, rayWorld4.Y, rayWorld4.Z);
            rayDirection.Normalize();
            var rayOrigin = camera.Position;
            
            // Intersect with ground plane (Y = 250)
            float groundY = 250f;
            float t = (groundY - rayOrigin.Y) / rayDirection.Y;
            
            if (t > 0)
            {
                var groundPos = rayOrigin + rayDirection * t;
                
                // Show tooltip at bottom center of viewport
                var tooltipPos = new System.Numerics.Vector2(
                    _viewportMin.X + (_viewportMax.X - _viewportMin.X) / 2 - 150,
                    _viewportMax.Y - 30
                );
                
                ImGui.SetNextWindowPos(tooltipPos);
                ImGui.SetNextWindowBgAlpha(0.8f);
                ImGui.Begin("CursorPos",
                    ImGuiWindowFlags.NoTitleBar |
                    ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoFocusOnAppearing |
                    ImGuiWindowFlags.NoNav);
                
                if (_pendingPlacementPartIndex >= 0)
                {
                    var placingPart = _availableParts[_pendingPlacementPartIndex];
                    string placingName = placingPart.Name.Contains('/') ? placingPart.Name[(placingPart.Name.LastIndexOf('/') + 1)..] : placingPart.Name;
                    ImGui.TextColored(new System.Numerics.Vector4(0.1f, 0.9f, 1.0f, 1.0f), $"Placing: {placingName}");
                    ImGui.SameLine();
                    string snapInfo = _snapEnabled ? $"Snap:{_snapSize:F0}" : "Snap:OFF";
                    ImGui.TextDisabled($"  X:{groundPos.X:F0}  Z:{groundPos.Z:F0}  Yaw:{_pendingPlacementYaw:F0}°  [{snapInfo}]   [Q/E] Rotate  [LMB] Place  [Esc] Cancel");
                }
                else
                {
                    ImGui.Text($"X: {groundPos.X:F0}    Y: 0 ({groundY:F0})    Z: {groundPos.Z:F0}");
                }
                
                ImGui.End();
            }
        }
        
        // Draw selection rectangle overlay on viewport
        if (_isRectSelecting)
        {
            var dl = ImGui.GetForegroundDrawList();
            var ra = new System.Numerics.Vector2(_rectSelectStartX, _rectSelectStartY);
            var rb = new System.Numerics.Vector2(_rectSelectEndX, _rectSelectEndY);
            dl.AddRectFilled(ra, rb, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 0.15f)));
            dl.AddRect(ra, rb, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 0.9f)), 0f, ImDrawFlags.None, 1.5f);
        }
        
        if (!_isOpen)
        {
            GameSparker.ReturnToMainMenu();
        }
    }
    
    // ── Undo / Redo ──────────────────────────────────────────────────────────

    private void PushUndoSnapshot()
    {
        if (ActiveTab == null) return;
        var snapshot = ActiveTab.ScenePieces
            .Select(p => new PieceSnapshot(p.Name, p.Position, p.Rotation, p.Id, p.PieceType, p.Tags, p.Obj))
            .ToList();
        _undoStack.Push(snapshot);
        _redoStack.Clear();
    }

    private void ApplySnapshot(List<PieceSnapshot> snapshot)
    {
        if (ActiveTab?.Stage == null) return;

        var currentObjs  = new HashSet<StageObject?>(ActiveTab.ScenePieces.Select(p => p.Obj));
        var snapshotObjs = new HashSet<StageObject?>(snapshot.Select(p => p.Obj));
        bool needsRebuild = !currentObjs.SetEquals(snapshotObjs);

        // Rebuild Stage.pieces to match snapshot order (for Save correctness)
        if (needsRebuild)
        {
            ActiveTab.Stage.pieces.Clear();
            foreach (var s in snapshot)
                if (s.Obj != null) ActiveTab.Stage.pieces.Add(s.Obj);
        }

        // Rebuild ScenePieces list
        var newPieces = snapshot.Select(s =>
        {
            var existing = ActiveTab.ScenePieces.FirstOrDefault(p => p.Obj == s.Obj);
            if (existing != null)
            {
                existing.Position = s.Position;
                existing.Rotation = s.Rotation;
                return existing;
            }
            // Piece was deleted — resurrect it
            var inst = new StagePieceInstance(s.Name, s.Obj, s.Id)
            {
                Position = s.Position,
                Rotation = s.Rotation,
                PieceType = s.PieceType,
                Tags = s.Tags
            };
            return inst;
        }).ToList();

        ActiveTab.ScenePieces = newPieces;
        ActiveTab.SelectedPieceId = -1;
        ActiveTab.SelectedPieceIds.Clear();
        ActiveTab.HasUnsavedChanges = true;

        if (needsRebuild)
            RebuildClientRenderer();
    }

    private void PerformUndo()
    {
        if (_undoStack.Count == 0 || ActiveTab == null) return;
        var currentSnapshot = ActiveTab.ScenePieces
            .Select(p => new PieceSnapshot(p.Name, p.Position, p.Rotation, p.Id, p.PieceType, p.Tags, p.Obj))
            .ToList();
        _redoStack.Push(currentSnapshot);
        ApplySnapshot(_undoStack.Pop());
    }

    private void PerformRedo()
    {
        if (_redoStack.Count == 0 || ActiveTab == null) return;
        var currentSnapshot = ActiveTab.ScenePieces
            .Select(p => new PieceSnapshot(p.Name, p.Position, p.Rotation, p.Id, p.PieceType, p.Tags, p.Obj))
            .ToList();
        _undoStack.Push(currentSnapshot);
        ApplySnapshot(_redoStack.Pop());
    }

    // ── Hierarchy reordering ─────────────────────────────────────────────────

    private void ReorderPiece(int draggedId, int targetId)
    {
        if (ActiveTab == null || draggedId == targetId) return;
        int draggedIndex = ActiveTab.ScenePieces.FindIndex(p => p.Id == draggedId);
        int targetIndex  = ActiveTab.ScenePieces.FindIndex(p => p.Id == targetId);
        if (draggedIndex < 0 || targetIndex < 0) return;

        var dragged = ActiveTab.ScenePieces[draggedIndex];
        ActiveTab.ScenePieces.RemoveAt(draggedIndex);
        // Adjust for the removal shifting indices
        int insertAt = draggedIndex < targetIndex ? targetIndex : targetIndex;
        ActiveTab.ScenePieces.Insert(insertAt, dragged);

        // ── Sync group membership when dragging across groups ──────────────────
        HierarchyGroup? srcGroup = ActiveTab.HierarchyGroups.Find(g => g.PieceIds.Contains(draggedId));
        HierarchyGroup? dstGroup = ActiveTab.HierarchyGroups.Find(g => g.PieceIds.Contains(targetId));
        if (srcGroup != dstGroup)
        {
            // Move piece from source group to destination group (or ungrouped if dstGroup == null)
            srcGroup?.PieceIds.Remove(draggedId);
            if (dstGroup != null)
            {
                int tpos = dstGroup.PieceIds.IndexOf(targetId);
                if (tpos < 0) dstGroup.PieceIds.Add(draggedId);
                else dstGroup.PieceIds.Insert(tpos + 1, draggedId);
            }
        }
        else if (srcGroup != null)
        {
            // Reorder within the same group
            srcGroup.PieceIds.Remove(draggedId);
            int tpos = srcGroup.PieceIds.IndexOf(targetId);
            if (tpos < 0) srcGroup.PieceIds.Add(draggedId);
            else srcGroup.PieceIds.Insert(tpos + 1, draggedId);
        }

        // Sync Stage.pieces order (only non-wall pieces end up there)
        if (ActiveTab.Stage != null)
        {
            ActiveTab.Stage.pieces.Clear();
            foreach (var p in ActiveTab.ScenePieces)
                if (p.Obj != null && p.PieceType != StagePieceInstance.PieceTypeEnum.Wall)
                    ActiveTab.Stage.pieces.Add(p.Obj);
        }

        ActiveTab.HasUnsavedChanges = true;
    }

    private void RenderHierarchy()
    {
        if (ActiveTab == null)
        {
            ImGui.Text("No stage loaded");
            return;
        }
        
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f));
        ImGui.Text($"Hierarchy — {ActiveTab.TabName}");
        ImGui.PopStyleColor();
        ImGui.Separator();
        
        // Hierarchy search filter
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##hiersearch", ref _hierarchySearch, 128);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by name");
        ImGui.Spacing();
        
        bool hasFilter = !string.IsNullOrWhiteSpace(_hierarchySearch);
        
        // Stage Borders section
        if (ActiveTab.StageWalls.Count > 0)
        {
            bool bordersOpen = ImGui.TreeNodeEx("Stage Borders", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
            if (bordersOpen)
            {
                foreach (var wall in ActiveTab.StageWalls)
                {
                    string label = $"{wall.GetDisplayName()} ({wall.Count} walls)";
                    if (hasFilter && !label.Contains(_hierarchySearch, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    bool isSelected = wall.Id == ActiveTab.SelectedWallId;
                    if (isSelected)
                        ImGui.PushStyleColor(ImGuiCol.Header, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 0.45f));
                    
                    if (ImGui.Selectable($"  {label}##wall{wall.Id}", isSelected, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        ActiveTab.SelectedWallId = wall.Id;
                        ActiveTab.SelectedPieceId = -1;
                        ActiveTab.SelectedPieceIds.Clear();
                    }
                    
                    if (isSelected)
                        ImGui.PopStyleColor();
                }
                ImGui.TreePop();
            }
            ImGui.Spacing();
        }
        
        // All non-wall pieces
        var allPieces = ActiveTab.ScenePieces.Where(p => p.PieceType != StagePieceInstance.PieceTypeEnum.Wall).ToList();
        var groupedIds = new HashSet<int>(ActiveTab.HierarchyGroups.SelectMany(g => g.PieceIds));
        var ungrouped = allPieces.Where(p => !groupedIds.Contains(p.Id)).ToList();
        string ungroupedLabel = ActiveTab.HierarchyGroups.Count > 0 ? "Ungrouped" : "Pieces";
        
        int ungroupedSlot = ActiveTab.UngroupedOrderIndex >= 0
            ? Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count)
            : ActiveTab.HierarchyGroups.Count;
        
        // Deferred mutations — applied AFTER the render loop to avoid mid-loop ImGui stack corruption
        int pendingDeleteGrpId = -1;
        int pendingReorderFrom = -1;
        int pendingReorderInsertBefore = -1; // insert dragged group BEFORE this slot index
        
        // Render slots 0..Count inclusive; each slot has a drop-zone then optional content
        for (int ri = 0; ri <= ActiveTab.HierarchyGroups.Count; ri++)
        {
            // ── Drop zone before slot ri (thin invisible target for drag-reorder) ──
            ImGui.PushID($"dz_{ri}");
            ImGui.Dummy(new System.Numerics.Vector2(-1f, 4f));
            if (ImGui.BeginDragDropTarget())
            {
                unsafe
                {
                    var pl = ImGui.AcceptDragDropPayload("HIER_GROUP");
                    if (pl.Handle != null && pl.DataSize == sizeof(int))
                    {
                        int src = *(int*)pl.Data;
                        pendingReorderFrom = src;
                        pendingReorderInsertBefore = ri;
                    }
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.PopID();
            
            // ── Ungrouped block at this slot ──
            if (ri == ungroupedSlot)
                RenderHierarchyGroupFlat(ungroupedLabel, ungrouped, hasFilter);
            
            // ── Named group at slot ri ──
            if (ri < ActiveTab.HierarchyGroups.Count)
            {
                var group = ActiveTab.HierarchyGroups[ri];
                var gpPieces = group.PieceIds
                    .Select(id => allPieces.Find(p => p.Id == id))
                    .Where(p => p != null).ToList()!;
                
                ImGui.PushID($"grp{group.Id}");
                
                // ≡ drag handle — drag source for group reordering
                ImGui.SmallButton("=");
                if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                {
                    unsafe { int giv = ri; ImGui.SetDragDropPayload("HIER_GROUP", &giv, (nuint)sizeof(int)); }
                    ImGui.Text($"Moving: {group.Name}");
                    ImGui.EndDragDropSource();
                }
                ImGui.SameLine();
                
                bool open = ImGui.TreeNodeEx($"[G] {group.Name} ({gpPieces.Count})##grp{group.Id}",
                    ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
                
                // Accept piece drops onto this group header (right after TreeNodeEx so the target is on that item)
                if (ImGui.BeginDragDropTarget())
                {
                    unsafe
                    {
                        var pl = ImGui.AcceptDragDropPayload("HIER_PIECE");
                        if (pl.Handle != null && pl.DataSize == sizeof(int))
                        {
                            int srcId = *(int*)pl.Data;
                            foreach (var g2 in ActiveTab.HierarchyGroups) g2.PieceIds.Remove(srcId);
                            if (!group.PieceIds.Contains(srcId)) group.PieceIds.Add(srcId);
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                
                // Right-click context menu — after drag target so it doesn't steal the item
                if (ImGui.BeginPopupContextItem($"##grpctx{group.Id}"))
                {
                    ImGui.TextDisabled(group.Name);
                    ImGui.Separator();
                    if (ImGui.MenuItem("Rename..."))
                    {
                        _groupContextMenuGroupId = group.Id;
                        _renameGroupBuffer = group.Name;
                        _showRenameGroupDialog = true;
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("Select All in Group"))
                    {
                        ActiveTab.SelectedPieceIds.Clear();
                        foreach (var gp in gpPieces) ActiveTab.SelectedPieceIds.Add(gp.Id);
                        if (gpPieces.Count > 0) ActiveTab.SelectedPieceId = gpPieces[^1].Id;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Delete Group (keep pieces)"))
                    {
                        pendingDeleteGrpId = group.Id; // deferred — not safe to remove here
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                
                // Render children — ALWAYS call TreePop when open==true
                if (open)
                {
                    RenderHierarchyGroup(gpPieces, hasFilter, group);
                    ImGui.TreePop();
                }
                
                ImGui.PopID();
            }
        }
        
        // ── Apply deferred mutations (safe: loop is finished, no ImGui stack mid-state) ──
        if (pendingDeleteGrpId >= 0)
        {
            ActiveTab.HierarchyGroups.RemoveAll(g => g.Id == pendingDeleteGrpId);
            if (ActiveTab.UngroupedOrderIndex >= 0)
                ActiveTab.UngroupedOrderIndex = Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count);
            ActiveTab.HasUnsavedChanges = true;
        }
        if (pendingReorderFrom >= 0 && pendingReorderInsertBefore >= 0 &&
            pendingReorderFrom != pendingReorderInsertBefore &&
            pendingReorderFrom != pendingReorderInsertBefore - 1) // not a no-op (insert right after itself)
        {
            var grp = ActiveTab.HierarchyGroups[pendingReorderFrom];
            ActiveTab.HierarchyGroups.RemoveAt(pendingReorderFrom);
            // After removal, insertion index shifts when src was before the target
            int insertIdx = pendingReorderInsertBefore > pendingReorderFrom
                ? pendingReorderInsertBefore - 1
                : pendingReorderInsertBefore;
            insertIdx = Math.Clamp(insertIdx, 0, ActiveTab.HierarchyGroups.Count);
            ActiveTab.HierarchyGroups.Insert(insertIdx, grp);
            if (ActiveTab.UngroupedOrderIndex >= 0)
                ActiveTab.UngroupedOrderIndex = Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count);
            // Also reorder ScenePieces so save order matches visual order
            ReorderScenePiecesForGroupOrder();
            ActiveTab.HasUnsavedChanges = true;
        }
        
        // Rename group dialog (modal)
        if (_showRenameGroupDialog)
        {
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 90), ImGuiCond.Always);
            ImGui.OpenPopup("##renamegroup");
        }
        if (ImGui.BeginPopupModal("##renamegroup", ref _showRenameGroupDialog, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize))
        {
            ImGui.Text("Rename Group:");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
            ImGui.InputText("##renbuf", ref _renameGroupBuffer, 64);
            if (ImGui.Button("OK", new System.Numerics.Vector2(130, 0)))
            {
                var g = ActiveTab.HierarchyGroups.Find(gr => gr.Id == _groupContextMenuGroupId);
                if (g != null) { g.Name = _renameGroupBuffer; ActiveTab.HasUnsavedChanges = true; }
                _showRenameGroupDialog = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(-1, 0))) { _showRenameGroupDialog = false; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
        
        // "+ New Group" button
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Button("+ New Group from Selection", new System.Numerics.Vector2(-1, 0)))
        {
            var newGroup = new HierarchyGroup { Id = ActiveTab.GetNextGroupId(), Name = "Group" };
            var selIds = ActiveTab.SelectedPieceIds.Count > 0
                ? new List<int>(ActiveTab.SelectedPieceIds)
                : ActiveTab.SelectedPieceId >= 0 ? new List<int> { ActiveTab.SelectedPieceId } : new List<int>();
            foreach (var sid in selIds)
            {
                foreach (var g in ActiveTab.HierarchyGroups) g.PieceIds.Remove(sid);
                newGroup.PieceIds.Add(sid);
            }
            ActiveTab.HierarchyGroups.Add(newGroup);
            ActiveTab.HasUnsavedChanges = true;
            // Open rename immediately
            _groupContextMenuGroupId = newGroup.Id;
            _renameGroupBuffer = newGroup.Name;
            _showRenameGroupDialog = true;
        }
    }
    
    // Reorders ScenePieces so pieces appear in group order (grouped first, then ungrouped).
    // Call after any group reorder so the save file reflects the visual hierarchy.
    private void ReorderScenePiecesForGroupOrder()
    {
        if (ActiveTab == null) return;
        var walls = ActiveTab.ScenePieces
            .Where(p => p.PieceType == StagePieceInstance.PieceTypeEnum.Wall).ToList();
        var nonWalls = ActiveTab.ScenePieces
            .Where(p => p.PieceType != StagePieceInstance.PieceTypeEnum.Wall).ToList();
        var allGroupedIds = new HashSet<int>(ActiveTab.HierarchyGroups.SelectMany(g => g.PieceIds));
        var grouped = new List<StagePieceInstance>();
        foreach (var group in ActiveTab.HierarchyGroups)
            foreach (var id in group.PieceIds)
            {
                var piece = nonWalls.Find(p => p.Id == id);
                if (piece != null) grouped.Add(piece);
            }
        var ungroupedPieces = nonWalls.Where(p => !allGroupedIds.Contains(p.Id)).ToList();
        ActiveTab.ScenePieces.Clear();
        ActiveTab.ScenePieces.AddRange(walls);
        ActiveTab.ScenePieces.AddRange(grouped);
        ActiveTab.ScenePieces.AddRange(ungroupedPieces);
    }
    
    // Renders a list of pieces with Ctrl+click multi-select, drag-drop reorder, and right-click group context menu
    private void RenderHierarchyGroup(List<StagePieceInstance> pieces, bool hasFilter, HierarchyGroup? owningGroup = null)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            string typeTag = piece.PieceType switch
            {
                StagePieceInstance.PieceTypeEnum.Chk => " [Chk]",
                StagePieceInstance.PieceTypeEnum.Fix => " [Fix]",
                _ => " [Set]"
            };
            string displayName = $"{piece.Name}{typeTag} (ID: {piece.Id})";
            if (hasFilter && !displayName.Contains(_hierarchySearch, StringComparison.OrdinalIgnoreCase))
                continue;
            
            bool isSelected = ActiveTab!.SelectedPieceIds.Contains(piece.Id) || piece.Id == ActiveTab.SelectedPieceId;
            if (isSelected)
                ImGui.PushStyleColor(ImGuiCol.Header, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 0.45f));
            
            string rowLabel = _hierDragSourceId == piece.Id
                ? $"\u2195 {displayName}##piece{piece.Id}"
                : $"  {displayName}##piece{piece.Id}";
            
            if (ImGui.Selectable(rowLabel, isSelected, ImGuiSelectableFlags.SpanAllColumns))
            {
                if (_isCtrlPressed)
                {
                    if (!ActiveTab.SelectedPieceIds.Remove(piece.Id))
                        ActiveTab.SelectedPieceIds.Add(piece.Id);
                    ActiveTab.SelectedPieceId = piece.Id;
                }
                else
                {
                    ActiveTab.SelectedPieceIds.Clear();
                    ActiveTab.SelectedPieceIds.Add(piece.Id);
                    ActiveTab.SelectedPieceId = piece.Id;
                }
                ActiveTab.SelectedWallId = -1;
            }
            
            if (isSelected) ImGui.PopStyleColor();
            
            // Right-click: group management
            if (ImGui.BeginPopupContextItem($"##piecectx{piece.Id}"))
            {
                ImGui.TextDisabled(piece.Name);
                ImGui.Separator();
                if (owningGroup != null && ImGui.MenuItem("Remove from Group"))
                {
                    owningGroup.PieceIds.Remove(piece.Id);
                    ActiveTab!.HasUnsavedChanges = true;
                    ImGui.EndPopup();
                    continue;
                }
                if (ActiveTab!.HierarchyGroups.Count > 0 && ImGui.BeginMenu("Move to Group"))
                {
                    foreach (var g in ActiveTab.HierarchyGroups)
                    {
                        if (g == owningGroup) continue;
                        if (ImGui.MenuItem(g.Name))
                        {
                            if (owningGroup != null) owningGroup.PieceIds.Remove(piece.Id);
                            if (!g.PieceIds.Contains(piece.Id)) g.PieceIds.Add(piece.Id);
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndPopup();
            }
            
            // Drag source
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                _hierDragSourceId = piece.Id;
                unsafe
                {
                    int dragId = piece.Id;
                    ImGui.SetDragDropPayload("HIER_PIECE", &dragId, (nuint)sizeof(int));
                }
                ImGui.TextUnformatted($"\u2195 Move: {displayName}");
                ImGui.EndDragDropSource();
            }
            
            // Drop target: reorder
            if (ImGui.BeginDragDropTarget())
            {
                unsafe
                {
                    var payload = ImGui.AcceptDragDropPayload("HIER_PIECE");
                    if (payload.Handle != null && payload.DataSize == sizeof(int))
                    {
                        int sourceId = *(int*)payload.Data;
                        if (sourceId != piece.Id)
                        {
                            PushUndoSnapshot();
                            ReorderPiece(sourceId, piece.Id);
                        }
                    }
                }
                _hierDragSourceId = -1;
                ImGui.EndDragDropTarget();
            }
        }
    }
    
    // Wrapper: renders under a collapsible TreeNode header (for "Pieces" / "Ungrouped")
    private void RenderHierarchyGroupFlat(string label, List<StagePieceInstance> pieces, bool hasFilter)
    {
        if (pieces.Count == 0 && !hasFilter) return;
        bool open = ImGui.TreeNodeEx($"{label} ({pieces.Count})", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
        // The tree-node item itself is a drop target: dropping a piece here removes it from any group
        if (ImGui.BeginDragDropTarget())
        {
            unsafe
            {
                var pl = ImGui.AcceptDragDropPayload("HIER_PIECE");
                if (pl.Handle != null && pl.DataSize == sizeof(int))
                {
                    int srcId = *(int*)pl.Data;
                    foreach (var g in ActiveTab!.HierarchyGroups) g.PieceIds.Remove(srcId);
                    ActiveTab!.HasUnsavedChanges = true;
                }
            }
            ImGui.EndDragDropTarget();
        }
        if (open)
        {
            RenderHierarchyGroup(pieces, hasFilter, null);
            ImGui.TreePop();
        }
    }
    private void RenderViewport()
    {
        // Viewport tabs
        if (ImGui.BeginTabBar("ViewportTabs"))
        {
            if (ImGui.BeginTabItem("Scene"))
            {
                if (ActiveTab != null && ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.Scene)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.Scene;
                    UpdateCameraPosition();
                }
                
                ImGui.Text("3D Scene View");
                if (ActiveTab != null)
                {
                    ImGui.Text($"Camera: Yaw={ActiveTab.CameraYaw:F1}° Pitch={ActiveTab.CameraPitch:F1}° Dist={ActiveTab.CameraDistance:F0}");
                }
                ImGui.Text($"Pieces in scene: {ActiveTab.ScenePieces.Count}");
                
                // The actual 3D rendering happens in Render3D()
                
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Top Down"))
            {
                if (ActiveTab != null && ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.TopDown)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.TopDown;
                    UpdateCameraPosition();
                }
                
                ImGui.Text("Top Down View");
                ImGui.Text($"Pieces in scene: {ActiveTab.ScenePieces.Count}");
                
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
    
    private void RenderInspector()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f));
        ImGui.Text("Inspector");
        ImGui.PopStyleColor();
        ImGui.Separator();
        
        if (ActiveTab == null) return;
        
        // Wall selected
        if (ActiveTab.SelectedWallId >= 0)
        {
            var wall = ActiveTab.StageWalls.Find(w => w.Id == ActiveTab.SelectedWallId);
            if (wall != null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 1f, 1f), wall.GetDisplayName());
                ImGui.Spacing();
                
                if (ImGui.CollapsingHeader("Border Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Spacing();
                    var count = wall.Count;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.DragInt("Wall Count##wc", ref count, 1f, 1, 100))
                    {
                        if (wall.Count != count) { wall.Count = count; ActiveTab.HasUnsavedChanges = true; RebuildAllWalls(); }
                    }
                    var pos = wall.Position;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.DragInt("Position##wp", ref pos, 10f))
                    {
                        if (wall.Position != pos) { wall.Position = pos; ActiveTab.HasUnsavedChanges = true; RebuildAllWalls(); }
                    }
                    var offset = wall.Offset;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.DragInt("Offset##wo", ref offset, 10f))
                    {
                        if (wall.Offset != offset) { wall.Offset = offset; ActiveTab.HasUnsavedChanges = true; RebuildAllWalls(); }
                    }
                    ImGui.Spacing();
                }
                
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete Border", new System.Numerics.Vector2(-1, 0)))
                {
                    ActiveTab.StageWalls.Remove(wall);
                    ActiveTab.SelectedWallId = -1;
                    ActiveTab.HasUnsavedChanges = true;
                    RebuildAllWalls();
                }
                ImGui.PopStyleColor();
            }
            return;
        }
        
        // Piece selected
        if (ActiveTab.SelectedPieceId >= 0)
        {
            // Multi-selection banner
            int selCount = ActiveTab.SelectedPieceIds.Count;
            if (selCount > 1)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.26f, 0.8f, 0.98f, 1f), $"{selCount} pieces selected");
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete All Selected", new System.Numerics.Vector2(-1, 0)))
                {
                    PushUndoSnapshot();
                    var toDelete = new List<int>(ActiveTab.SelectedPieceIds);
                    foreach (var did in toDelete)
                    {
                        var dp = ActiveTab.ScenePieces.Find(p => p.Id == did);
                        if (dp == null) continue;
                        if (ActiveTab.Stage != null)
                            for (int si = 0; si < ActiveTab.Stage.pieces.Count; si++)
                                if (ActiveTab.Stage.pieces[si] == dp.Obj) { ActiveTab.Stage.pieces.RemoveAt(si); break; }
                        ActiveTab.ScenePieces.Remove(dp);
                        foreach (var grp in ActiveTab.HierarchyGroups) grp.PieceIds.Remove(dp.Id);
                    }
                    ActiveTab.SelectedPieceIds.Clear();
                    ActiveTab.SelectedPieceId = -1;
                    ActiveTab.HasUnsavedChanges = true;
                    RebuildClientRenderer();
                }
                ImGui.PopStyleColor();
                ImGui.Spacing();
                ImGui.TextDisabled("(showing primary piece below)");
                ImGui.Separator();
                ImGui.Spacing();
            }
            
            var piece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
            if (piece != null)
            {
                string shortName = piece.Name.Contains('/') ? piece.Name.Substring(piece.Name.LastIndexOf('/') + 1) : piece.Name;
                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 1f, 0.7f, 1f), shortName);
                ImGui.TextDisabled(piece.Name);
                ImGui.Spacing();
                
                // Transform section
                if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Spacing();
                    // Position: display Y offset by 250 so ground=0 for the user
                    var displayPos = new System.Numerics.Vector3((float)piece.Position.X, (float)piece.Position.Y - 250, (float)piece.Position.Z);
                    ImGui.Text("Position");
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.IsItemActivated()) PushUndoSnapshot();
                    if (ImGui.DragFloat3("##pos", ref displayPos, 10f))
                    {
                        var newPos = new f64Vector3((fix64)displayPos.X, (fix64)(displayPos.Y + 250), (fix64)displayPos.Z);
                        var deltaX = newPos.X - piece.Position.X;
                        var deltaY = newPos.Y - piece.Position.Y;
                        var deltaZ = newPos.Z - piece.Position.Z;
                        piece.Position = newPos;
                        // Apply same delta to all other selected pieces
                        foreach (var selId in ActiveTab.SelectedPieceIds)
                        {
                            if (selId == piece.Id) continue;
                            var sp = ActiveTab.ScenePieces.Find(p => p.Id == selId);
                            if (sp != null) sp.Position = new f64Vector3(sp.Position.X + deltaX, sp.Position.Y + deltaY, sp.Position.Z + deltaZ);
                        }
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Y=0 is ground level (internal Y=250).");
                    
                    ImGui.Text("Rotation (Yaw)");
                    float rotY = (float)piece.Rotation.Y;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.IsItemActivated()) PushUndoSnapshot();
                    if (ImGui.DragFloat("##roty", ref rotY, 1f, -180f, 180f))
                    {
                        float rotDelta = rotY - (float)piece.Rotation.Y;
                        piece.Rotation = new f64Vector3(piece.Rotation.X, (fix64)rotY, piece.Rotation.Z);
                        // Apply same rotation delta to all other selected pieces
                        foreach (var selId in ActiveTab.SelectedPieceIds)
                        {
                            if (selId == piece.Id) continue;
                            var sp = ActiveTab.ScenePieces.Find(p => p.Id == selId);
                            if (sp != null) sp.Rotation = new f64Vector3(sp.Rotation.X, (fix64)(((float)sp.Rotation.Y + rotDelta) % 360f), sp.Rotation.Z);
                        }
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    ImGui.Spacing();
                }
                
                // Piece settings section
                if (ImGui.CollapsingHeader("Piece Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Spacing();
                    ImGui.Text("Type");
                    int pieceType = (int)piece.PieceType;
                    string[] typeNames = { "Set", "Checkpoint", "Fix Hoop" };
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo("##type", ref pieceType, typeNames, typeNames.Length))
                    {
                        piece.PieceType = (StagePieceInstance.PieceTypeEnum)pieceType;
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    
                    ImGui.Text("AI Tags");
                    ImGui.SetNextItemWidth(-1);
                    string tags = piece.Tags;
                    if (ImGui.InputText("##tags", ref tags, 64))
                    {
                        piece.Tags = tags;
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("AI waypoint tags: p=road, pr=ramp, pt=turn, ph=halfpipe, po=fixroad start");
                    ImGui.Spacing();
                    
                    if (!_isSwapMode)
                    {
                        // Only allow swap when all selected pieces share the same model
                        bool swapAllowed = true;
                        if (ActiveTab.SelectedPieceIds.Count > 1)
                        {
                            var allSelected = ActiveTab.ScenePieces
                                .Where(p => ActiveTab.SelectedPieceIds.Contains(p.Id)).ToList();
                            var firstName = allSelected.Count > 0 ? allSelected[0].Name : piece.Name;
                            swapAllowed = allSelected.All(p => p.Name == firstName);
                        }
                        if (!swapAllowed) ImGui.BeginDisabled();
                        if (ImGui.Button("Swap Piece...", new System.Numerics.Vector2(-1, 0)))
                        {
                            _isSwapMode = true;
                            _pendingPlacementPartIndex = -1;
                        }
                        if (!swapAllowed)
                        {
                            ImGui.EndDisabled();
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                                ImGui.SetTooltip("All selected pieces must use the same model to swap");
                        }
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.4f, 0.05f, 1f));
                        if (ImGui.Button("Cancel Swap", new System.Numerics.Vector2(-1, 0)))
                            _isSwapMode = false;
                        ImGui.PopStyleColor();
                    }
                    ImGui.Spacing();
                }
                
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete Piece", new System.Numerics.Vector2(-1, 0)))
                {
                    if (ActiveTab.Stage != null)
                    {
                        for (int i = 0; i < ActiveTab.Stage.pieces.Count; i++)
                        {
                            if (ActiveTab.Stage.pieces[i] == piece.Obj)
                            {
                                ActiveTab.Stage.pieces.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    ActiveTab.ScenePieces.Remove(piece);
                    ActiveTab.SelectedPieceId = -1;
                    ActiveTab.HasUnsavedChanges = true;
                    RebuildClientRenderer();
                }
                ImGui.PopStyleColor();
                return;
            }
        }
        
        // Nothing selected
        ImGui.Spacing();
        ImGui.TextDisabled("No piece selected.");
        ImGui.TextDisabled("Click a piece in the viewport");
        ImGui.TextDisabled("or select from the Hierarchy.");
    }
    
    private void RenderPartsLibrary()
    {
        // Header row: title + counts
        if (_isSwapMode)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.75f, 0.1f, 1f));
            ImGui.Text("Stage Parts Library");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.75f, 0.1f, 1f), "— Click a part to swap. [Esc] to cancel.");
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f));
            ImGui.Text("Stage Parts Library");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextDisabled($"({_availableParts.Count} parts)");
        }
        ImGui.Separator();
        
        // Search bar
        float searchWidth = ImGui.GetContentRegionAvail().X - 4;
        ImGui.SetNextItemWidth(searchWidth);
        ImGui.InputText("##partssearch", ref _partsSearch, 128);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Search parts by name");
        ImGui.Spacing();
        
        // Category filter tabs
        string[] catLabels = { "All", "nfmm/", "Vendor", "User" };
        for (int c = 0; c < catLabels.Length; c++)
        {
            if (c > 0) ImGui.SameLine();
            bool active = _partsCategory == c;
            if (active)
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 0.7f));
            if (ImGui.SmallButton(catLabels[c]))
                _partsCategory = c;
            if (active)
                ImGui.PopStyleColor();
        }
        
        // Snap + rotation controls (right-aligned in same row)
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        
        // Snap toggle
        bool snapOn = _snapEnabled;
        if (snapOn) ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.65f, 0.2f, 0.8f));
        if (ImGui.SmallButton(_snapEnabled ? $"Snap ON: {_snapSize:F0}" : "Snap OFF"))
            _snapEnabled = !_snapEnabled;
        if (snapOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle grid snapping (S).\nScroll wheel cycles snap size when in placement mode.");
        
        if (_snapEnabled)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f);
            if (ImGui.BeginCombo("##snapsize", $"{_snapSize:F0}"))
            {
                for (int si = 0; si < SnapPresets.Length; si++)
                {
                    bool sel = si == _snapPresetIndex;
                    if (ImGui.Selectable($"{SnapPresets[si]:F0}", sel))
                    {
                        _snapPresetIndex = si;
                        _snapSize = SnapPresets[si];
                    }
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Grid snap size in world units.\nRoad spacing = 5600.");
        }
        
        if (_pendingPlacementPartIndex >= 0)
        {
            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(0.1f, 0.9f, 1.0f, 1.0f), $"Yaw: {_pendingPlacementYaw:F0}°");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("[Q] -45°   [E] +45°   [R] Reset");
            ImGui.SameLine();
            if (ImGui.SmallButton("-45")) _pendingPlacementYaw = ((_pendingPlacementYaw - 45f) % 360f + 360f) % 360f;
            ImGui.SameLine();
            if (ImGui.SmallButton("+45")) _pendingPlacementYaw = (_pendingPlacementYaw + 45f) % 360f;
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset##rot")) _pendingPlacementYaw = 0f;
        }
        
        ImGui.Separator();
        
        // Build filtered list
        bool hasSearchFilter = !string.IsNullOrWhiteSpace(_partsSearch);
        
        ImGui.BeginChild("##partsgrid", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.None);
        
        const float tileImgSize = 56f;  // image / button area height
        const float tileLabelH  = 16f;  // label height below image
        const float tileW       = 64f;  // total tile width
        const float tileH       = tileImgSize + tileLabelH + 4f;
        const float tilePad     = 6f;
        
        float regionW = ImGui.GetContentRegionAvail().X;
        int   cols    = Math.Max(1, (int)(regionW / (tileW + tilePad)));
        int   col     = 0;
        
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(tilePad, tilePad));
        
        for (int i = 0; i < _availableParts.Count; i++)
        {
            var part = _availableParts[i];
            
            // Category filter
            bool inCategory = _partsCategory switch
            {
                1 => part.Name.StartsWith("nfmm/"),
                2 => part.Name.StartsWith("nfmv/") || part.Name.StartsWith("vendor/"),
                3 => part.Name.StartsWith("user/"),
                _ => true
            };
            if (!inCategory) continue;
            
            // Name search
            if (hasSearchFilter && !part.Name.Contains(_partsSearch, StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Queue preview generation if not yet done
            if (part.Rad != null)
                QueuePartPreview(part.Name, part.Rad);
            
            if (col > 0)
                ImGui.SameLine(0, tilePad);
            
            var groupTopLeft = ImGui.GetCursorScreenPos();
            bool isPendingPlacement = i == _pendingPlacementPartIndex;
            
            // In swap mode, determine if this tile is the current piece
            bool isCurrentSwapPiece = false;
            if (_isSwapMode && ActiveTab != null && ActiveTab.SelectedPieceId >= 0)
            {
                var sp = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
                isCurrentSwapPiece = sp != null && sp.Name == part.Name;
            }
            ImGui.PushID(i);
            ImGui.BeginGroup();
            
            bool clicked;
            if (_partPreviews.TryGetValue(part.Name, out var preview))
            {
                // Show 3D thumbnail
                // Flip UVs vertically — FNA (OpenGL) render targets are stored bottom-up
                ImGui.Image(preview.Ref, new System.Numerics.Vector2(tileW, tileImgSize),
                    new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
                clicked = ImGui.IsItemClicked();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(part.Name);
            }
            else
            {
                // Fallback: colored button while preview is loading
                var tileColor = GetPartTileColor(part.Name);
                ImGui.PushStyleColor(ImGuiCol.Button, tileColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(
                    Math.Min(tileColor.X + 0.15f, 1f),
                    Math.Min(tileColor.Y + 0.15f, 1f),
                    Math.Min(tileColor.Z + 0.15f, 1f),
                    tileColor.W));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 0.9f));
                clicked = ImGui.Button("##tile", new System.Numerics.Vector2(tileW, tileImgSize));
                ImGui.PopStyleColor(3);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(part.Name);
            }
            
            // Label centered under thumbnail
            string shortName = part.Name.Contains('/') ? part.Name[(part.Name.LastIndexOf('/') + 1)..] : part.Name;
            var textSize = ImGui.CalcTextSize(shortName, false, tileW);
            float textOffX = Math.Max(0, (tileW - textSize.X) * 0.5f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + textOffX);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textSize.X);
            ImGui.TextUnformatted(shortName);
            ImGui.PopTextWrapPos();
            
            ImGui.EndGroup();
            
            // Highlight border when this part is selected for placement
            if (isPendingPlacement)
            {
                var drawList = ImGui.GetWindowDrawList();
                uint borderColor = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.1f, 0.9f, 1.0f, 1.0f));
                drawList.AddRect(
                    groupTopLeft,
                    new System.Numerics.Vector2(groupTopLeft.X + tileW, groupTopLeft.Y + tileH),
                    borderColor, 2f, ImDrawFlags.None, 2.5f
                );
            }
            
            // Highlight the current piece when in swap mode
            if (isCurrentSwapPiece)
            {
                var drawList = ImGui.GetWindowDrawList();
                uint borderColor = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 0.75f, 0.1f, 1.0f));
                drawList.AddRect(
                    groupTopLeft,
                    new System.Numerics.Vector2(groupTopLeft.X + tileW, groupTopLeft.Y + tileH),
                    borderColor, 2f, ImDrawFlags.None, 2.5f
                );
            }
            
            ImGui.PopID();
            
            if (clicked && part.Rad != null)
            {
                if (_isSwapMode && ActiveTab != null && ActiveTab.SelectedPieceId >= 0)
                {
                    // Swap the selected piece to this part
                    var swapPiece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
                    if (swapPiece != null && part.Name != swapPiece.Name)
                    {
                        PushUndoSnapshot();
                        var newRot = new f64Euler(
                            f64AngleSingle.FromDegrees((fix64)(float)swapPiece.Rotation.Y),
                            f64AngleSingle.ZeroAngle,
                            f64AngleSingle.ZeroAngle);
                        var newMesh = new StageObject(part.Rad, swapPiece.Position, newRot);
                        if (ActiveTab.Stage != null)
                        {
                            for (int si = 0; si < ActiveTab.Stage.pieces.Count; si++)
                            {
                                if (ActiveTab.Stage.pieces[si] == swapPiece.Obj)
                                {
                                    ActiveTab.Stage.pieces[si] = newMesh;
                                    break;
                                }
                            }
                        }
                        swapPiece.Name = part.Name;
                        swapPiece.Obj = newMesh;
                        ActiveTab.HasUnsavedChanges = true;
                        RebuildClientRenderer();
                    }
                    _isSwapMode = false;
                }
                else
                {
                    // Enter placement mode: the user will click in the viewport to place the part
                    _pendingPlacementPartIndex = i;
                    _hasValidPlacementPos = false;
                }
            }
            
            col++;
            if (col >= cols) col = 0;
        }
        
        ImGui.PopStyleVar(); // ItemSpacing
        ImGui.EndChild();
    }
    
    private static System.Numerics.Vector4 GetPartTileColor(string name)
    {
        if (name.Contains("checkpoint") || name.Contains("chk"))
            return new System.Numerics.Vector4(0.18f, 0.55f, 0.18f, 0.85f); // green
        if (name.Contains("fix") || name.Contains("hoop"))
            return new System.Numerics.Vector4(0.65f, 0.20f, 0.20f, 0.85f); // red
        if (name.Contains("road") || name.Contains("ramp") || name.Contains("roll"))
            return new System.Numerics.Vector4(0.38f, 0.30f, 0.20f, 0.85f); // brown
        if (name.Contains("wall") || name.Contains("border"))
            return new System.Numerics.Vector4(0.25f, 0.25f, 0.45f, 0.85f); // blue-grey
        if (name.Contains("turn") || name.Contains("twist") || name.Contains("bend"))
            return new System.Numerics.Vector4(0.45f, 0.30f, 0.10f, 0.85f); // orange-brown
        return new System.Numerics.Vector4(0.25f, 0.30f, 0.38f, 0.85f); // default slate
    }
}
