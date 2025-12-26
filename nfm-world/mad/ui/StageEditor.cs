using ImGuiNET;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad.UI;

// Custom Stage class for the editor that doesn't require loading from file
public class EditorStage : Stage
{
    public EditorStage(GraphicsDevice graphicsDevice) : base("", graphicsDevice)
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
    public Mesh? MeshRef { get; set; }
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero;
    public int Id { get; set; }
    public PieceTypeEnum PieceType { get; set; } = PieceTypeEnum.Set;
    public string Tags { get; set; } = ""; // AI waypoint tags like p, pr, pt, ph, etc.
    
    public StagePieceInstance(string name, Mesh? meshRef, int id)
    {
        Name = name;
        MeshRef = meshRef;
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

// Main viewport tab for the stage editor
public class StageEditorTab
{
    public string TabName { get; set; } = "Stage";
    public List<StagePieceInstance> ScenePieces { get; set; } = new();
    public List<StageWall> StageWalls { get; set; } = new();
    public List<Mesh> WallMeshes { get; set; } = new(); // Visual representation of walls
    public List<string> UnknownParameters { get; set; } = new(); // Unknown/unhandled stage parameters to preserve
    // Camera/view controls
    public Vector3 CameraPosition { get; set; } = new Vector3(0, -300, -1500);
    public float CameraYaw { get; set; } = 0f;
    public float CameraPitch { get; set; } = -10f;
    public float CameraDistance { get; set; } = 1000f;
    public float TopDownHeight { get; set; } = 2000f;
    public Vector3 TopDownPanPosition { get; set; } = Vector3.Zero;
    
    // Mouse drag state for camera control
    public bool IsDragging { get; set; } = false;
    public int DragStartX { get; set; } = 0;
    public int DragStartY { get; set; } = 0;
    public float DragStartCameraYaw { get; set; } = 0f;
    public float DragStartCameraPitch { get; set; } = 0f;
    
    // Selection state
    public int SelectedPieceId { get; set; } = -1;
    public int SelectedWallId { get; set; } = -1;
    
    // View mode
    public enum ViewModeEnum { Scene, TopDown }
    public ViewModeEnum ViewMode { get; set; } = ViewModeEnum.Scene;
    
    // Associated stage and scene
    public Stage? Stage { get; set; }
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
    private List<(string Name, Mesh? Mesh)> _availableParts = new();
    
    // Active tab property
    private StageEditorTab? ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;
    
    // Viewport bounds for scissor testing
    private System.Numerics.Vector2 _viewportMin;
    private System.Numerics.Vector2 _viewportMax;
    
    // UI state
    private float _hierarchyWidth = 250f;
    private float _inspectorWidth = 300f;
    private float _partsLibraryHeight = 200f;
    
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
    
    // New stage dialog state
    private bool _showNewStageDialog = false;
    private string _newStageName = "";
    private string _stageFileName = "";
    
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
    
    public StageEditorPhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        RefreshAvailableParts();
    }
    
    private void RefreshAvailableParts()
    {
        _availableParts.Clear();
        
        // Add all stage parts from the loaded collections
        foreach (var part in GameSparker.stage_parts)
        {
            _availableParts.Add((part.FileName, part));
        }
        
        foreach (var part in GameSparker.vendor_stage_parts)
        {
            _availableParts.Add((part.FileName, part));
        }
        
        foreach (var part in GameSparker.user_stage_parts)
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
        
        // Initialize camera
        camera.Fov = 60f;
        camera.Width = GameSparker._game.GraphicsDevice.Viewport.Width;
        camera.Height = GameSparker._game.GraphicsDevice.Viewport.Height;
        camera.Near = 1f;
        camera.Far = 100000f;
        
        UpdateCameraPosition();
        
        Console.WriteLine("Stage Editor opened");
    }
    
    public override void Exit()
    {
        _isOpen = false;
        
        // Restore walls to all stages before exiting so they appear in gameplay
        foreach (var tab in _tabs)
        {
            if (tab.Stage != null && tab.StageWalls.Count > 0)
            {
                var wallPart = GameSparker.GetStagePart("nfmm/thewall");
                if (wallPart.Mesh != null)
                {
                    foreach (var wall in tab.StageWalls)
                {
                    var n = wall.Count;
                    var o = wall.Position;
                    var p = wall.Offset;
                    
                    for (int q = 0; q < n; q++)
                    {
                        Vector3 position;
                        Euler rotation;
                        
                        switch (wall.Direction)
                        {
                            case StageWall.WallDirection.Right:
                                position = new Vector3(o, World.Ground, q * 4800 + p);
                                rotation = Euler.Identity;
                                break;
                            case StageWall.WallDirection.Left:
                                position = new Vector3(o, World.Ground, q * 4800 + p);
                                rotation = new Euler(AngleSingle.FromDegrees(180), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle);
                                break;
                            case StageWall.WallDirection.Top:
                                position = new Vector3(q * 4800 + p, World.Ground, o);
                                rotation = new Euler(AngleSingle.FromDegrees(90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle);
                                break;
                            case StageWall.WallDirection.Bottom:
                                position = new Vector3(q * 4800 + p, World.Ground, o);
                                rotation = new Euler(AngleSingle.FromDegrees(-90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle);
                                break;
                            default:
                                position = Vector3.Zero;
                                rotation = Euler.Identity;
                                break;
                        }
                        
                        tab.Stage.pieces.Add(new Mesh(wallPart.Mesh, position, rotation));
                    }
                }
            }
        }
        
        // Clear wall meshes to prevent them from appearing when re-entering the editor
        tab.WallMeshes.Clear();
        }
        
        _tabs.Clear();
        _activeTabIndex = -1;
        Console.WriteLine("Stage Editor closed");
    }
    
    private void CreateEmptyStage(string stageName)
    {
        // Create a new tab with empty stage
        var tab = new StageEditorTab();
        tab.TabName = stageName;
        tab.StageFileName = ConvertStageNameToFilename(stageName);
        tab.Stage = new EditorStage(_graphicsDevice);
        
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
        
        RecreateScene();
        SaveStage(); // Automatically save the new stage
        
        Console.WriteLine($"Created new stage: {stageName} (filename: {tab.StageFileName})");
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
            Console.WriteLine("Cannot save: no stage loaded");
            return;
        }
        
        // Ensure the user stages directory exists
        var userStagesDir = "data/stages/user";
        System.IO.Directory.CreateDirectory(userStagesDir);
        
        var filePath = $"{userStagesDir}/{ActiveTab.StageFileName}.txt";
        
        try
        {
            using var writer = new System.IO.StreamWriter(filePath);
            
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
            
            // Write all stage pieces
            foreach (var piece in ActiveTab.ScenePieces)
            {
                // Skip thewall pieces - they're handled by StageWalls
                if (piece.Name.Contains("thewall") || piece.PieceType == StagePieceInstance.PieceTypeEnum.Wall)
                    continue;
                    
                if (piece.MeshRef != null)
                {
                    var pos = piece.Position;
                    var rot = piece.Rotation;
                    
                    // Determine piece identifier (numeric ID or string name)
                    string pieceId;
                    int numericId = -1;
                    if (piece.Name.StartsWith("nfmm/"))
                    {
                        // For nfmm pieces, find the numeric index and add offset of 10
                        var baseName = piece.Name.Substring(5); // Remove "nfmm/" prefix
                        var index = Array.IndexOf(GameSparker.StageRads, baseName);
                        if (index >= 0)
                        {
                            numericId = index + 10;
                            pieceId = numericId.ToString();
                        }
                        else
                        {
                            pieceId = piece.Name; // Fallback to string name
                        }
                    }
                    else
                    {
                        // For other pieces, use string name
                        pieceId = piece.Name;
                    }
                    
                    int yCoord = (int)pos.Y;
                    int rotX = (int)rot.Y; // Yaw is the rotation around Y axis
                    
                    // Write based on piece type
                    if (piece.PieceType == StagePieceInstance.PieceTypeEnum.Fix)
                    {
                        // fix(id,x,z,-y,rot)
                        writer.WriteLine($"fix({pieceId},{(int)pos.X},{(int)pos.Z},{yCoord},{rotX})");
                    }
                    else if (piece.PieceType == StagePieceInstance.PieceTypeEnum.Chk)
                    {
                        // chk(id,x,z,rot) or chk(id,x,z,rot,y/-y)
                        // Stage.cs loads with ymult: -1 for regular checkpoints, +1 for aircheckpoint
                        // So we need to reverse: regular chk gets negated, aircheckpoint doesn't
                        bool isAirCheckpoint = numericId == 64 || piece.Name.Contains("nfmm/aircheckpoint");
                        
                        if (yCoord == 250) // Ground level, omit Y
                        {
                            writer.WriteLine($"chk({pieceId},{(int)pos.X},{(int)pos.Z},{rotX})");
                        }
                        else
                        {
                            // Regular checkpoint: negate Y (will be negated again on load)
                            // Aircheckpoint: don't negate Y (won't be negated on load)
                            int fileY = isAirCheckpoint ? yCoord : -yCoord;
                            writer.WriteLine($"chk({pieceId},{(int)pos.X},{(int)pos.Z},{rotX},{fileY})");
                        }
                    }
                    else // PieceTypeEnum.Set
                    {
                        // set(id,x,z,rot) or set(id,x,z,rot,-y)
                        if (yCoord == 250) // Ground level, omit Y
                        {
                            writer.WriteLine($"set({pieceId},{(int)pos.X},{(int)pos.Z},{rotX})");
                        }
                        else
                        {
                            writer.WriteLine($"set({pieceId},{(int)pos.X},{(int)pos.Z},{rotX},{-yCoord})");
                        }
                    }
                }
            }
            
            // Write stage walls at the end
            if (ActiveTab.StageWalls.Count > 0)
            {
                writer.WriteLine();
                foreach (var wall in ActiveTab.StageWalls)
                {
                    string command = wall.Direction switch
                    {
                        StageWall.WallDirection.Right => "maxr",
                        StageWall.WallDirection.Left => "maxl",
                        StageWall.WallDirection.Top => "maxt",
                        StageWall.WallDirection.Bottom => "maxb",
                        _ => "maxr"
                    };
                    writer.WriteLine($"{command}({wall.Count},{wall.Position},{wall.Offset})");
                }
            }
            
            Console.WriteLine($"Stage saved to: {filePath}");
            ActiveTab.HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving stage: {ex.Message}");
        }
    }
    
    private void RefreshAvailableStages()
    {
        _availableStages.Clear();
        
        var userStagesDir = "data/stages/user";
        if (!System.IO.Directory.Exists(userStagesDir))
        {
            return;
        }
        
        var files = System.IO.Directory.GetFiles(userStagesDir, "*.txt");
        foreach (var file in files)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
            _availableStages.Add(fileName);
        }
        
        _availableStages.Sort();
    }
    
    private void LoadStage(string stageFileName)
    {
        // Save current tab's World properties before loading new stage
        if (ActiveTab != null)
        {
            ActiveTab.SkyColor = new System.Numerics.Vector3(World.Sky.R, World.Sky.G, World.Sky.B);
            ActiveTab.FogColor = new System.Numerics.Vector3(World.Fog.R, World.Fog.G, World.Fog.B);
            ActiveTab.GroundColor = new System.Numerics.Vector3(World.GroundColor.R, World.GroundColor.G, World.GroundColor.B);
            ActiveTab.PolysEnabled = World.HasPolys;
            if (World.HasPolys)
            {
                ActiveTab.PolysColor = new System.Numerics.Vector3(World.GroundPolysColor.R, World.GroundPolysColor.G, World.GroundPolysColor.B);
            }
            ActiveTab.CloudsEnabled = World.HasClouds;
            if (World.HasClouds)
            {
                ActiveTab.CloudsColor = new System.Numerics.Vector3(World.Clouds[0], World.Clouds[1], World.Clouds[2]);
                ActiveTab.CloudsParam4 = World.Clouds[3];
                ActiveTab.CloudsHeight = World.Clouds[4];
                ActiveTab.CloudCoverage = World.CloudCoverage;
            }
            ActiveTab.MountainsEnabled = World.DrawMountains;
            ActiveTab.MountainsSeed = World.MountainSeed;
            ActiveTab.SnapA = World.Snap.R;
            ActiveTab.SnapB = World.Snap.G;
            ActiveTab.SnapC = World.Snap.B;
            ActiveTab.FadeFrom = World.FadeFrom;
        }
        
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
                Console.WriteLine($"Stage '{stageFileName}' is already open, switched to that tab.");
                return;
            }
        }
        
        try
        {
            // Create a new tab
            var tab = new StageEditorTab();
            tab.StageFileName = stageFileName;
            
            // Load the stage using the Stage class (it expects filename without extension)
            tab.Stage = new Stage($"user/{stageFileName}", _graphicsDevice);
            tab.TabName = tab.Stage.Name;
            
            // Remove all wall pieces from the stage immediately (we'll manage them separately)
            int removedCount = 0;
            for (int i = tab.Stage.pieces.Count - 1; i >= 0; i--)
            {
                var piece = tab.Stage.pieces[i];
                if (piece != null && (piece.FileName == "thewall" || piece.FileName.Contains("wall")))
                {
                    tab.Stage.pieces.RemoveAt(i);
                    removedCount++;
                }
            }
            Console.WriteLine($"Removed {removedCount} wall pieces from stage");
            
            // First pass: detect wall groups and piece tags by reading the original file
            var stageFilePath = $"data/stages/user/{stageFileName}.txt";
            var pieceTags = new Dictionary<string, string>(); // key: "set_x_z" or "chk_x_z" or "fix_x_z", value: tags
            
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
                    // Capture piece tags
                    else if (trimmed.StartsWith("set(") || trimmed.StartsWith("chk(") || trimmed.StartsWith("fix("))
                    {
                        var parenIndex = trimmed.IndexOf(')');
                        if (parenIndex != -1 && parenIndex < trimmed.Length - 1)
                        {
                            var tags = trimmed.Substring(parenIndex + 1);
                            if (!string.IsNullOrEmpty(tags))
                            {
                                // Extract coordinates to create unique key
                                var type = trimmed.Substring(0, 3);
                                var coords = trimmed.Substring(4, parenIndex - 4).Split(',');
                                if (coords.Length >= 3)
                                {
                                    var x = coords[1];
                                    var z = coords[2];
                                    var key = $"{type}_{x}_{z}";
                                    pieceTags[key] = tags;
                                }
                            }
                        }
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
                             !trimmed.StartsWith("mountains(") &&
                             !trimmed.StartsWith("fadefrom("))
                    {
                        tab.UnknownParameters.Add(trimmed);
                    }
                }
            }
            
            // Populate editor pieces from loaded stage
            foreach (var piece in tab.Stage.pieces)
            {
                // Skip thewall pieces - they're handled as StageWalls
                if (piece.FileName == "thewall" || piece.FileName.Contains("thewall"))
                    continue;
                    
                // Construct the full piece name with folder prefix
                string pieceName = piece.FileName;
                if (!pieceName.Contains("/"))
                {
                    // If no folder prefix, it's from nfmm
                    pieceName = "nfmm/" + pieceName;
                }
                
                var instance = new StagePieceInstance(
                    pieceName,
                    piece,
                    tab.GetNextPieceId()
                );
                
                // Detect piece type from mesh type first (needed for Y coordinate fix)
                if (piece is CheckPoint)
                {
                    instance.PieceType = StagePieceInstance.PieceTypeEnum.Chk;
                }
                else if (piece is FixHoop)
                {
                    instance.PieceType = StagePieceInstance.PieceTypeEnum.Fix;
                }
                else
                {
                    instance.PieceType = StagePieceInstance.PieceTypeEnum.Set;
                }
                
                // For aircheckpoints loaded from file, Stage.cs uses ymult=1 which doesn't negate the Y
                // But the file has negative Y values, so we need to negate them to get the correct position
                bool isAirCheckpoint = pieceName.Contains("nfmm/aircheckpoint");
                float loadedY = piece.Position.Y;
                if (isAirCheckpoint && instance.PieceType == StagePieceInstance.PieceTypeEnum.Chk)
                {
                    loadedY = -loadedY;
                }
                
                instance.Position = new Vector3(
                    piece.Position.X,
                    loadedY,
                    piece.Position.Z
                );
                
                var euler = piece.Rotation;
                instance.Rotation = new Vector3(
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
            
            Console.WriteLine($"Loaded stage: {tab.Stage.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stage: {ex.Message}");
        }
    }
    
    private void RecreateScene()
    {
        if (ActiveTab?.Stage == null) return;
        
        // Create scene with the stage
        ActiveTab?.Scene = new Scene(
            _graphicsDevice,
            [ActiveTab?.Stage],
            camera,
            [] // No shadow cameras for now
        );
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
        }
        else
        {
            // Top down view - look from above at pan position (negative Y is up in this coordinate system)
            camera.Position = new Vector3(ActiveTab.TopDownPanPosition.X, -ActiveTab.TopDownHeight, ActiveTab.TopDownPanPosition.Z);
            camera.LookAt = new Vector3(ActiveTab.TopDownPanPosition.X, 0, ActiveTab.TopDownPanPosition.Z);
            camera.Up = Vector3.UnitZ;
        }
    }
    
    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        if (imguiWantsKeyboard) return;
        if (!_isOpen) return;
        
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
        }
        
        // Handle keyboard shortcuts here
        if (key == Keys.Delete)
        {
            if (ActiveTab.SelectedPieceId >= 0)
            {
                var piece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
                if (piece != null)
                {
                    ActiveTab.ScenePieces.Remove(piece);
                    ActiveTab.SelectedPieceId = -1;
                    RecreateScene();
                }
            }
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
        var wallPart = GameSparker.GetStagePart("nfmm/thewall");
        if (wallPart.Mesh == null)
        {
            Console.WriteLine("Wall mesh not found!");
            return;
        }
        
        // Clear the wall meshes list
        ActiveTab.WallMeshes.Clear();
        
        Console.WriteLine($"Rebuilding walls: {ActiveTab.StageWalls.Count} wall groups");
        
        // Generate wall meshes based on StageWalls definitions
        foreach (var wall in ActiveTab.StageWalls)
        {
            var n = wall.Count;
            var o = wall.Position;
            var p = wall.Offset;
            
            Console.WriteLine($"Creating wall: {wall.Direction}, count={n}, pos={o}, offset={p}");
            
            for (int q = 0; q < n; q++)
            {
                Vector3 position;
                Euler rotation;
                
                switch (wall.Direction)
                {
                    case StageWall.WallDirection.Right:
                        position = new Vector3(o, World.Ground, q * 4800 + p);
                        rotation = Euler.Identity;
                        break;
                    case StageWall.WallDirection.Left:
                        position = new Vector3(o, World.Ground, q * 4800 + p);
                        rotation = new Euler(AngleSingle.FromDegrees(180), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle);
                        break;
                    case StageWall.WallDirection.Top:
                        position = new Vector3(q * 4800 + p, World.Ground, o);
                        rotation = new Euler(AngleSingle.FromDegrees(90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle);
                        break;
                    case StageWall.WallDirection.Bottom:
                        position = new Vector3(q * 4800 + p, World.Ground, o);
                        rotation = new Euler(AngleSingle.FromDegrees(-90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle);
                        break;
                    default:
                        position = Vector3.Zero;
                        rotation = Euler.Identity;
                        break;
                }
                
                ActiveTab.WallMeshes.Add(new Mesh(wallPart.Mesh, position, rotation));
            }
        }
        
        Console.WriteLine($"Total wall meshes created: {ActiveTab.WallMeshes.Count}");
    }
    
    private void RenderSelectionHighlight(StagePieceInstance piece)
    {
        if (piece.MeshRef == null) return;
        
        var mesh = piece.MeshRef;
        
        // Save old depth state and disable depth testing so highlight renders on top
        var oldDepthStencilState = _graphicsDevice.DepthStencilState;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        
        // Draw wireframe box using BasicEffect
        var effect = new Microsoft.Xna.Framework.Graphics.BasicEffect(_graphicsDevice);
        effect.View = camera.ViewMatrix;
        effect.Projection = camera.ProjectionMatrix;
        effect.VertexColorEnabled = true;
        
        var color = new Microsoft.Xna.Framework.Color(1.0f, 1.0f, 0.0f, 1.0f); // Yellow
        
        // Get rotation from piece only - match game engine's rotation order
        // Negate yaw to match game engine's coordinate system
        var yaw = -piece.Rotation.Y * (float)Math.PI / 180f;
        var pitch = piece.Rotation.X * (float)Math.PI / 180f;
        var roll = piece.Rotation.Z * (float)Math.PI / 180f;
        
        var rotationMatrix = 
            Microsoft.Xna.Framework.Matrix.CreateRotationY(yaw) *
            Microsoft.Xna.Framework.Matrix.CreateRotationX(pitch) *
            Microsoft.Xna.Framework.Matrix.CreateRotationZ(roll);
        
        var totalPosition = new Microsoft.Xna.Framework.Vector3(
            piece.Position.X,
            piece.Position.Y,
            piece.Position.Z
        );
        
        // Collect all polygon edges for wireframe rendering
        var edgeVertices = new List<Microsoft.Xna.Framework.Graphics.VertexPositionColor>();
        
        foreach (var poly in mesh.Polys)
        {
            if (poly.Points.Length < 2) continue;
            
            // Transform all points
            var transformedPoints = new Microsoft.Xna.Framework.Vector3[poly.Points.Length];
            for (int i = 0; i < poly.Points.Length; i++)
            {
                var localVert = new Microsoft.Xna.Framework.Vector3(
                    poly.Points[i].X,
                    poly.Points[i].Y,
                    poly.Points[i].Z
                );
                transformedPoints[i] = Microsoft.Xna.Framework.Vector3.Transform(localVert, rotationMatrix) + totalPosition;
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
                new Microsoft.Xna.Framework.Vector3(0, 0, 0),
                new Microsoft.Xna.Framework.Vector3(0.5f, 0, 0),
                new Microsoft.Xna.Framework.Vector3(-0.5f, 0, 0),
                new Microsoft.Xna.Framework.Vector3(0, 0.5f, 0),
                new Microsoft.Xna.Framework.Vector3(0, -0.5f, 0)
            };
            
            foreach (var offset in offsets)
            {
                var offsetVertices = edgeVertices.Select(v => 
                    new Microsoft.Xna.Framework.Graphics.VertexPositionColor(
                        v.Position + offset, 
                        v.Color
                    )
                ).ToArray();
                
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        Microsoft.Xna.Framework.Graphics.PrimitiveType.LineList,
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
    
    private int PerformRayPicking(int screenX, int screenY)
    {
        if (ActiveTab.ScenePieces.Count == 0) return -1;
        
        var viewport = _graphicsDevice.Viewport;
        
        // Convert screen coords to normalized device coordinates
        float ndcX = (2.0f * screenX) / viewport.Width - 1.0f;
        float ndcY = 1.0f - (2.0f * screenY) / viewport.Height;
        
        // Create ray in clip space
        var rayClip = new Microsoft.Xna.Framework.Vector4(ndcX, ndcY, -1.0f, 1.0f);
        
        // Transform to view space
        var projMatrix = camera.ProjectionMatrix;
        Microsoft.Xna.Framework.Matrix.Invert(ref projMatrix, out var invProj);
        var rayEye = Microsoft.Xna.Framework.Vector4.Transform(rayClip, invProj);
        rayEye.Z = -1.0f;
        rayEye.W = 0.0f;
        
        // Transform to world space
        var viewMatrix = camera.ViewMatrix;
        Microsoft.Xna.Framework.Matrix.Invert(ref viewMatrix, out var invView);
        var rayWorld4 = Microsoft.Xna.Framework.Vector4.Transform(rayEye, invView);
        var rayDirection = new Vector3(rayWorld4.X, rayWorld4.Y, rayWorld4.Z);
        rayDirection.Normalize();
        
        var rayOrigin = camera.Position;
        
        // Find closest intersected piece using proper ray-triangle intersection
        float closestDistance = float.MaxValue;
        int closestPieceId = -1;
        
        foreach (var piece in ActiveTab.ScenePieces)
        {
            if (piece.MeshRef == null) continue;
            
            var mesh = piece.MeshRef;
            
            // Create rotation matrix matching the game engine's order
            // Rotation is stored as (Pitch, Yaw, Roll) in degrees
            // Negate yaw to match game engine's coordinate system  
            var yaw = -piece.Rotation.Y * (float)Math.PI / 180f;
            var pitch = piece.Rotation.X * (float)Math.PI / 180f;
            var roll = piece.Rotation.Z * (float)Math.PI / 180f;
            
            // Create individual rotation matrices and combine them
            var rotationMatrix = 
                Microsoft.Xna.Framework.Matrix.CreateRotationY(yaw) *
                Microsoft.Xna.Framework.Matrix.CreateRotationX(pitch) *
                Microsoft.Xna.Framework.Matrix.CreateRotationZ(roll);
            
            // Test each polygon
            foreach (var poly in mesh.Polys)
            {
                if (poly.Points.Length < 3) continue;
                
                // Transform all vertices to world space
                var worldVerts = new Vector3[poly.Points.Length];
                for (int i = 0; i < poly.Points.Length; i++)
                {
                    var localVert = new Microsoft.Xna.Framework.Vector3(
                        poly.Points[i].X,
                        poly.Points[i].Y,
                        poly.Points[i].Z
                    );
                    
                    // Apply rotation then translation
                    var rotated = Microsoft.Xna.Framework.Vector3.Transform(localVert, rotationMatrix);
                    worldVerts[i] = new Vector3(
                        rotated.X + piece.Position.X,
                        rotated.Y + piece.Position.Y,
                        rotated.Z + piece.Position.Z
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
        
        _mouseX = x;
        _mouseY = y;
        
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
            
            if (IsMouseInViewport(x, y) && !_isRightDragging && ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene)
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
            // Left-click (for future selection)
            _isLeftButtonDown = true;
        }
    }
    
    public override void MouseScrolled(int delta, bool imguiWantsMouse)
    {
        if (imguiWantsMouse) return;
        if (!GameSparker._game.IsActive) return;
        if (!_isOpen) return;
        
        // Only zoom if mouse is in viewport
        if (IsMouseInViewport(_mouseX, _mouseY))
        {
            if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
            {
                // Adjust top-down height
                ActiveTab.TopDownHeight = Math.Clamp(ActiveTab.TopDownHeight - delta * 50f, 500f, 50000f);
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
        }
        else if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _isLeftButtonDown)
        {
            _isLeftButtonDown = false;
            
            if (ActiveTab != null)
            {
                if (ActiveTab.IsDragging)
                {
                    ActiveTab.IsDragging = false;
                }
                
                // Handle piece selection on left click
                if (!imguiWantsMouse && IsMouseInViewport(x, y))
                {
                    var pickedPieceId = PerformRayPicking(x, y);
                    if (pickedPieceId >= 0)
                    {
                        ActiveTab.SelectedPieceId = pickedPieceId;
                    }
                    else
                    {
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
            
            // Move camera position directly (first-person flying)
            if (_moveForward)
                ActiveTab.CameraPosition += forward * CAMERA_MOVE_SPEED;
            if (_moveBackward)
                ActiveTab.CameraPosition -= forward * CAMERA_MOVE_SPEED;
            if (_moveLeft)
                ActiveTab.CameraPosition -= right * CAMERA_MOVE_SPEED;
            if (_moveRight)
                ActiveTab.CameraPosition += right * CAMERA_MOVE_SPEED;
            if (_moveUp)
                ActiveTab.CameraPosition += up * CAMERA_MOVE_SPEED;
            if (_moveDown)
                ActiveTab.CameraPosition -= up * CAMERA_MOVE_SPEED;
            
            UpdateCameraPosition();
        }
        else if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
        {
            // Pan controls for top-down view (move the look-at point on XZ plane)
            var panSpeed = CAMERA_MOVE_SPEED;
            
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
                if (piece.MeshRef != null)
                {
                    piece.MeshRef.Position = new Microsoft.Xna.Framework.Vector3(
                        piece.Position.X,
                        piece.Position.Y,
                        piece.Position.Z
                    );
                    
                    // Euler constructor is (Yaw, Pitch, Roll)
                    // piece.Rotation is stored as (Pitch, Yaw, Roll) for display consistency
                    piece.MeshRef.Rotation = new Euler(
                        Stride.Core.Mathematics.AngleSingle.FromRadians(piece.Rotation.Y * (float)Math.PI / 180f), // Yaw
                        Stride.Core.Mathematics.AngleSingle.FromRadians(piece.Rotation.X * (float)Math.PI / 180f), // Pitch
                        Stride.Core.Mathematics.AngleSingle.FromRadians(piece.Rotation.Z * (float)Math.PI / 180f)  // Roll
                    );
                }
            }
            
            // GameTick the stage pieces
            foreach (var piece in ActiveTab.ScenePieces)
            {
                piece.MeshRef?.GameTick();
            }
            
            // GameTick the wall meshes
            foreach (var wallMesh in ActiveTab.WallMeshes)
            {
                wallMesh.GameTick();
            }
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
        if (ActiveTab?.Scene != null && ActiveTab?.Stage != null)
        {
            if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
            {
                // Top-down view: with lighting, no sky/ground/polys/clouds/mountains
                var oldGround = ActiveTab?.Stage.ground;
                var oldSky = ActiveTab?.Stage.sky;
                var oldPolys = ActiveTab?.Stage.polys;
                var oldClouds = ActiveTab?.Stage.clouds;
                var oldMountains = ActiveTab?.Stage.mountains;
                
                // Temporarily remove environment elements
                ActiveTab?.Stage.ground = null!;
                ActiveTab?.Stage.sky = null!;
                ActiveTab?.Stage.polys = null;
                ActiveTab?.Stage.clouds = null;
                ActiveTab?.Stage.mountains = null;
                
                // Render with lighting preserved
                ActiveTab?.Scene.Render(false);
                
                // Restore environment elements
                ActiveTab?.Stage.ground = oldGround;
                ActiveTab?.Stage.sky = oldSky;
                ActiveTab?.Stage.polys = oldPolys;
                ActiveTab?.Stage.clouds = oldClouds;
                ActiveTab?.Stage.mountains = oldMountains;
            }
            else
            {
                // Normal 3D view with lighting and ground
                ActiveTab?.Scene.Render(false);
            }
        }
        
        // Render wall meshes separately (editor-only visualization) - BEFORE restoring scissor state
        if (ActiveTab != null)
        {
            foreach (var wallMesh in ActiveTab.WallMeshes)
            {
                wallMesh.Render(camera, null);
            }
        }
        
        // Restore old state
        _graphicsDevice.ScissorRectangle = oldScissorRect;
        _graphicsDevice.RasterizerState = oldRasterizerState;
        
        // Render selection highlight for selected piece
        if (ActiveTab.SelectedPieceId >= 0)
        {
            var selectedPiece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
            if (selectedPiece?.MeshRef != null)
            {
                RenderSelectionHighlight(selectedPiece);
            }
        }
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
                if (ImGui.MenuItem("Scene View", "", ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene))
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.Scene;
                    UpdateCameraPosition();
                }
                
                if (ImGui.MenuItem("Top Down View", "", ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown))
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.TopDown;
                    UpdateCameraPosition();
                }
                
                ImGui.EndMenu();
            }
            
            // Display camera info and stage name
            if (ActiveTab?.Stage != null)
            {
                ImGui.Text($"    Stage: {ActiveTab.TabName}");
                if (ActiveTab.HasUnsavedChanges)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.5f, 0.0f, 1.0f), "*");
                }
                ImGui.Text($"    Camera: Yaw={ActiveTab.CameraYaw:F1}° Pitch={ActiveTab.CameraPitch:F1}° Dist={ActiveTab.CameraDistance:F0}");
                ImGui.Text($"    Pieces: {ActiveTab.ScenePieces.Count}");
            }
            else
            {
                ImGui.Text("    No stage loaded - use File > New Stage or Load Stage");
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
                        // Save current tab's World properties before switching
                        if (ActiveTab != null)
                        {
                            ActiveTab.SkyColor = new System.Numerics.Vector3(World.Sky.R, World.Sky.G, World.Sky.B);
                            ActiveTab.FogColor = new System.Numerics.Vector3(World.Fog.R, World.Fog.G, World.Fog.B);
                            ActiveTab.GroundColor = new System.Numerics.Vector3(World.GroundColor.R, World.GroundColor.G, World.GroundColor.B);
                            ActiveTab.PolysEnabled = World.HasPolys;
                            if (World.HasPolys)
                            {
                                ActiveTab.PolysColor = new System.Numerics.Vector3(World.GroundPolysColor.R, World.GroundPolysColor.G, World.GroundPolysColor.B);
                            }
                            ActiveTab.CloudsEnabled = World.HasClouds;
                            if (World.HasClouds)
                            {
                                ActiveTab.CloudsColor = new System.Numerics.Vector3(World.Clouds[0], World.Clouds[1], World.Clouds[2]);
                                ActiveTab.CloudsParam4 = World.Clouds[3];
                                ActiveTab.CloudsHeight = World.Clouds[4];
                                ActiveTab.CloudCoverage = World.CloudCoverage;
                            }
                            ActiveTab.MountainsEnabled = World.DrawMountains;
                            ActiveTab.MountainsSeed = World.MountainSeed;
                            ActiveTab.SnapA = World.Snap.R;
                            ActiveTab.SnapB = World.Snap.G;
                            ActiveTab.SnapC = World.Snap.B;
                            ActiveTab.FadeFrom = World.FadeFrom;
                        }
                        
                        _activeTabIndex = i;
                        UpdateCameraPosition();
                        
                        // Restore this tab's World properties
                        if (ActiveTab != null)
                        {
                            World.Sky = new Color3(
                                (short)(ActiveTab.SkyColor.X),
                                (short)(ActiveTab.SkyColor.Y),
                                (short)(ActiveTab.SkyColor.Z)
                            );
                            World.Fog = new Color3(
                                (short)(ActiveTab.FogColor.X),
                                (short)(ActiveTab.FogColor.Y),
                                (short)(ActiveTab.FogColor.Z)
                            );
                            World.GroundColor = new Color3(
                                (short)(ActiveTab.GroundColor.X),
                                (short)(ActiveTab.GroundColor.Y),
                                (short)(ActiveTab.GroundColor.Z)
                            );
                            World.HasPolys = ActiveTab.PolysEnabled;
                            World.DrawPolys = ActiveTab.PolysEnabled;
                            if (ActiveTab.PolysEnabled)
                            {
                                World.GroundPolysColor = new Color3(
                                    (short)(ActiveTab.PolysColor.X),
                                    (short)(ActiveTab.PolysColor.Y),
                                    (short)(ActiveTab.PolysColor.Z)
                                );
                            }
                            World.HasClouds = ActiveTab.CloudsEnabled;
                            World.DrawClouds = ActiveTab.CloudsEnabled;
                            if (ActiveTab.CloudsEnabled)
                            {
                                World.Clouds = new int[]
                                {
                                    (int)ActiveTab.CloudsColor.X,
                                    (int)ActiveTab.CloudsColor.Y,
                                    (int)ActiveTab.CloudsColor.Z,
                                    ActiveTab.CloudsParam4,
                                    ActiveTab.CloudsHeight
                                };
                                World.CloudCoverage = ActiveTab.CloudCoverage;
                            }
                            World.DrawMountains = ActiveTab.MountainsEnabled;
                            if (ActiveTab.MountainsEnabled)
                            {
                                World.MountainSeed = ActiveTab.MountainsSeed;
                            }
                            World.Snap = new Color3(
                                (short)ActiveTab.SnapA,
                                (short)ActiveTab.SnapB,
                                (short)ActiveTab.SnapC
                            );
                            World.FadeFrom = ActiveTab.FadeFrom;
                            
                            // Recreate environment elements with new colors
                            if (ActiveTab.Stage != null)
                            {
                                ActiveTab.Stage.sky = new Sky(_graphicsDevice);
                                ActiveTab.Stage.ground = new Ground(_graphicsDevice);
                                
                                // Recreate polys, clouds, and mountains based on tab settings
                                if (ActiveTab.PolysEnabled)
                                {
                                    ActiveTab.Stage.polys = NFMWorld.Mad.Environment.MakePolys(-10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count, _graphicsDevice);
                                }
                                else
                                {
                                    ActiveTab.Stage.polys = null;
                                }
                                
                                if (ActiveTab.CloudsEnabled)
                                {
                                    ActiveTab.Stage.clouds = NFMWorld.Mad.Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                                }
                                else
                                {
                                    ActiveTab.Stage.clouds = null;
                                }
                                
                                if (ActiveTab.MountainsEnabled)
                                {
                                    ActiveTab.Stage.mountains = NFMWorld.Mad.Environment.MakeMountains(-10000, 10000, -10000, 10000, _graphicsDevice);
                                }
                                else
                                {
                                    ActiveTab.Stage.mountains = null;
                                }
                            }
                            
                            // Rebuild walls for this tab
                            RebuildAllWalls();
                            
                            RecreateScene();
                        }
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
            
            ImGui.Separator();
            
            if (ImGui.Button("Create", new System.Numerics.Vector2(120, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_newStageName))
                {
                    CreateEmptyStage(_newStageName);
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
            if (ImGui.ColorEdit3("##skycolor", ref _editSkyColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRGB))
            {
                // Live preview
                World.Sky = new Color3((short)(_editSkyColor.X * 255), (short)(_editSkyColor.Y * 255), (short)(_editSkyColor.Z * 255));
                if (ActiveTab?.Stage != null) ActiveTab.Stage.sky = new Sky(_graphicsDevice);
            }
            
            ImGui.Text("Fog Color:");
            if (ImGui.ColorEdit3("##fogcolor", ref _editFogColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRGB))
            {
                // Live preview
                World.Fog = new Color3((short)(_editFogColor.X * 255), (short)(_editFogColor.Y * 255), (short)(_editFogColor.Z * 255));
            }
            
            ImGui.Text("Ground Color:");
            if (ImGui.ColorEdit3("##groundcolor", ref _editGroundColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRGB))
            {
                // Live preview
                World.GroundColor = new Color3((short)(_editGroundColor.X * 255), (short)(_editGroundColor.Y * 255), (short)(_editGroundColor.Z * 255));
                if (ActiveTab?.Stage != null) ActiveTab.Stage.ground = new Ground(_graphicsDevice);
            }
            
            ImGui.Separator();
            
            if (ImGui.Checkbox("Enable Ground Polys", ref _editPolysEnabled))
            {
                // Live preview
                World.HasPolys = _editPolysEnabled;
                World.DrawPolys = _editPolysEnabled;
                if (_editPolysEnabled && ActiveTab?.Stage != null)
                {
                    World.GroundPolysColor = new Color3(
                        (short)(_editPolysColor.X * 255),
                        (short)(_editPolysColor.Y * 255),
                        (short)(_editPolysColor.Z * 255)
                    );
                    ActiveTab.Stage.polys = NFMWorld.Mad.Environment.MakePolys(-10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count, _graphicsDevice);
                }
                else if (!_editPolysEnabled && ActiveTab?.Stage != null)
                {
                    ActiveTab.Stage.polys = null;
                }
            }
            if (_editPolysEnabled)
            {
                ImGui.Text("Polys Color:");
                if (ImGui.ColorEdit3("##polyscolor", ref _editPolysColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRGB))
                {
                    // Live preview
                    World.GroundPolysColor = new Color3(
                        (short)(_editPolysColor.X * 255),
                        (short)(_editPolysColor.Y * 255),
                        (short)(_editPolysColor.Z * 255)
                    );
                    if (ActiveTab?.Stage != null)
                    {
                        ActiveTab.Stage.polys = NFMWorld.Mad.Environment.MakePolys(-10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count, _graphicsDevice);
                    }
                }
            }
            
            ImGui.Separator();
            
            if (ImGui.Checkbox("Enable Clouds", ref _editCloudsEnabled))
            {
                // Live preview
                World.HasClouds = _editCloudsEnabled;
                World.DrawClouds = _editCloudsEnabled;
                if (_editCloudsEnabled && ActiveTab?.Stage != null)
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
                    ActiveTab.Stage.clouds = NFMWorld.Mad.Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                }
                else if (!_editCloudsEnabled && ActiveTab?.Stage != null)
                {
                    ActiveTab.Stage.clouds = null;
                }
            }
            if (_editCloudsEnabled)
            {
                ImGui.Text("Clouds Color:");
                if (ImGui.ColorEdit3("##cloudscolor", ref _editCloudsColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRGB))
                {
                    // Live preview
                    World.Clouds[0] = (int)(_editCloudsColor.X * 255);
                    World.Clouds[1] = (int)(_editCloudsColor.Y * 255);
                    World.Clouds[2] = (int)(_editCloudsColor.Z * 255);
                    if (ActiveTab?.Stage != null)
                    {
                        ActiveTab.Stage.clouds = NFMWorld.Mad.Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
                
                ImGui.Text("Clouds Height:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.DragInt("##cloudsheight", ref _editCloudsHeight, 10f, -10000, 10000))
                {
                    // Live preview
                    World.Clouds[4] = _editCloudsHeight;
                    if (ActiveTab?.Stage != null)
                    {
                        ActiveTab.Stage.clouds = NFMWorld.Mad.Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
                
                ImGui.Text("Clouds Parameter 4:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("##cloudsparam4", ref _editCloudsParam4))
                {
                    // Live preview
                    World.Clouds[3] = _editCloudsParam4;
                    if (ActiveTab?.Stage != null)
                    {
                        ActiveTab.Stage.clouds = NFMWorld.Mad.Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
                
                ImGui.Text("Cloud Coverage:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("##cloudcoverage", ref _editCloudCoverage, 0.0f, 10.0f))
                {
                    // Live preview
                    World.CloudCoverage = _editCloudCoverage;
                    if (ActiveTab?.Stage != null)
                    {
                        ActiveTab.Stage.clouds = NFMWorld.Mad.Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
                    }
                }
            }
            
            if (ImGui.Checkbox("Enable Mountains", ref _editMountainsEnabled))
            {
                // Live preview
                World.DrawMountains = _editMountainsEnabled;
                if (_editMountainsEnabled && ActiveTab?.Stage != null)
                {
                    World.MountainSeed = _editMountainsSeed;
                    ActiveTab.Stage.mountains = NFMWorld.Mad.Environment.MakeMountains(-10000, 10000, -10000, 10000, _graphicsDevice);
                }
                else if (!_editMountainsEnabled && ActiveTab?.Stage != null)
                {
                    ActiveTab.Stage.mountains = null;
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
                    if (ActiveTab?.Stage != null)
                    {
                        ActiveTab.Stage.mountains = NFMWorld.Mad.Environment.MakeMountains(-10000, 10000, -10000, 10000, _graphicsDevice);
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
                    ActiveTab.SkyColor = new System.Numerics.Vector3(
                        _editSkyColor.X * 255,
                        _editSkyColor.Y * 255,
                        _editSkyColor.Z * 255
                    );
                    ActiveTab.FogColor = new System.Numerics.Vector3(
                        _editFogColor.X * 255,
                        _editFogColor.Y * 255,
                        _editFogColor.Z * 255
                    );
                    ActiveTab.GroundColor = new System.Numerics.Vector3(
                        _editGroundColor.X * 255,
                        _editGroundColor.Y * 255,
                        _editGroundColor.Z * 255
                    );
                    ActiveTab.PolysColor = new System.Numerics.Vector3(
                        _editPolysColor.X * 255,
                        _editPolysColor.Y * 255,
                        _editPolysColor.Z * 255
                    );
                    ActiveTab.PolysEnabled = _editPolysEnabled;
                    ActiveTab.CloudsEnabled = _editCloudsEnabled;
                    ActiveTab.CloudsColor = new System.Numerics.Vector3(
                        _editCloudsColor.X * 255,
                        _editCloudsColor.Y * 255,
                        _editCloudsColor.Z * 255
                    );
                    ActiveTab.CloudsParam4 = _editCloudsParam4;
                    ActiveTab.CloudsHeight = _editCloudsHeight;
                    ActiveTab.CloudCoverage = _editCloudCoverage;
                    ActiveTab.MountainsEnabled = _editMountainsEnabled;
                    ActiveTab.MountainsSeed = _editMountainsSeed;
                    ActiveTab.SnapA = _editSnapA;
                    ActiveTab.SnapB = _editSnapB;
                    ActiveTab.SnapC = _editSnapC;
                    ActiveTab.FadeFrom = _editFadeFrom;
                    
                    // Update World values directly for immediate visual effect
                    World.Sky = new Color3(
                        (short)ActiveTab.SkyColor.X,
                        (short)ActiveTab.SkyColor.Y,
                        (short)ActiveTab.SkyColor.Z
                    );
                    World.Fog = new Color3(
                        (short)ActiveTab.FogColor.X,
                        (short)ActiveTab.FogColor.Y,
                        (short)ActiveTab.FogColor.Z
                    );
                    World.GroundColor = new Color3(
                        (short)ActiveTab.GroundColor.X,
                        (short)ActiveTab.GroundColor.Y,
                        (short)ActiveTab.GroundColor.Z
                    );
                    
                    // Update polys
                    World.HasPolys = ActiveTab.PolysEnabled;
                    World.DrawPolys = ActiveTab.PolysEnabled;
                    if (ActiveTab.PolysEnabled)
                    {
                        World.GroundPolysColor = new Color3(
                            (short)ActiveTab.PolysColor.X,
                            (short)ActiveTab.PolysColor.Y,
                            (short)ActiveTab.PolysColor.Z
                        );
                    }
                    
                    // Update clouds
                    World.HasClouds = ActiveTab.CloudsEnabled;
                    World.DrawClouds = ActiveTab.CloudsEnabled;
                    if (ActiveTab.CloudsEnabled)
                    {
                        World.Clouds = new int[]
                        {
                            (int)ActiveTab.CloudsColor.X,
                            (int)ActiveTab.CloudsColor.Y,
                            (int)ActiveTab.CloudsColor.Z,
                            ActiveTab.CloudsParam4,
                            ActiveTab.CloudsHeight
                        };
                        World.CloudCoverage = ActiveTab.CloudCoverage;
                    }
                    
                    // Update mountains
                    World.DrawMountains = ActiveTab.MountainsEnabled;
                    if (ActiveTab.MountainsEnabled)
                    {
                        World.MountainSeed = ActiveTab.MountainsSeed;
                    }
                    
                    World.Snap = new Color3(
                        (short)ActiveTab.SnapA,
                        (short)ActiveTab.SnapB,
                        (short)ActiveTab.SnapC
                    );
                    World.FadeFrom = ActiveTab.FadeFrom;
                    
                    // Recreate sky and ground objects to reflect color changes
                    if (ActiveTab.Stage != null)
                    {
                        ActiveTab.Stage.sky = new Sky(_graphicsDevice);
                        ActiveTab.Stage.ground = new Ground(_graphicsDevice);
                    }
                    
                    // Recreate scene to apply visual changes
                    RecreateScene();
                    
                    // Mark as dirty since properties changed
                    ActiveTab.HasUnsavedChanges = true;
                }
                
                _showPropertiesDialog = false;
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
            {
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
            
            var rayClip = new Microsoft.Xna.Framework.Vector4(ndcX, ndcY, -1.0f, 1.0f);
            var projMatrix = camera.ProjectionMatrix;
            Microsoft.Xna.Framework.Matrix.Invert(ref projMatrix, out var invProj);
            var rayEye = Microsoft.Xna.Framework.Vector4.Transform(rayClip, invProj);
            rayEye.Z = -1.0f;
            rayEye.W = 0.0f;
            
            var viewMatrix = camera.ViewMatrix;
            Microsoft.Xna.Framework.Matrix.Invert(ref viewMatrix, out var invView);
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
                
                ImGui.Text($"X: {groundPos.X:F0}    Y: 0 ({groundY:F0})    Z: {groundPos.Z:F0}");
                
                ImGui.End();
            }
        }
        
        if (!_isOpen)
        {
            GameSparker.ReturnToMainMenu();
        }
    }
    
    private void RenderHierarchy()
    {
        if (ActiveTab == null)
        {
            ImGui.Text("No stage loaded");
            return;
        }
        
        ImGui.Text($"Hierarchy - {ActiveTab.TabName}");
        ImGui.Separator();
        
        // List stage walls
        if (ActiveTab.StageWalls.Count > 0)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 1.0f, 1.0f), "Stage Borders:");
            foreach (var wall in ActiveTab.StageWalls)
            {
                bool isSelected = wall.Id == ActiveTab.SelectedWallId;
                
                if (ImGui.Selectable($"{wall.GetDisplayName()} ({wall.Count} walls)", isSelected))
                {
                    ActiveTab.SelectedWallId = wall.Id;
                    ActiveTab.SelectedPieceId = -1; // Deselect pieces
                }
            }
            ImGui.Separator();
        }
        
        // List all non-wall pieces in the scene
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 1.0f, 0.7f, 1.0f), "Stage Pieces:");
        for (int i = 0; i < ActiveTab.ScenePieces.Count; i++)
        {
            var piece = ActiveTab.ScenePieces[i];
            bool isSelected = piece.Id == ActiveTab.SelectedPieceId;
            
            if (ImGui.Selectable($"{piece.Name} (ID: {piece.Id})", isSelected))
            {
                ActiveTab.SelectedPieceId = piece.Id;
                ActiveTab.SelectedWallId = -1; // Deselect walls
            }
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
        ImGui.Text("Inspector");
        ImGui.Separator();
        
        if (ActiveTab == null) return;
        
        // Check if a wall is selected
        if (ActiveTab.SelectedWallId >= 0)
        {
            var wall = ActiveTab.StageWalls.Find(w => w.Id == ActiveTab.SelectedWallId);
            if (wall != null)
            {
                ImGui.Text($"Selected: {wall.GetDisplayName()}");
                ImGui.Separator();
                
                // Wall Count
                var count = wall.Count;
                if (ImGui.DragInt("Wall Count", ref count, 1f, 1, 100))
                {
                    if (wall.Count != count)
                    {
                        wall.Count = count;
                        ActiveTab!.HasUnsavedChanges = true;
                        RebuildAllWalls();
                    }
                }
                
                // Position
                var pos = wall.Position;
                if (ImGui.DragInt("Position", ref pos, 10f))
                {
                    if (wall.Position != pos)
                    {
                        wall.Position = pos;
                        ActiveTab!.HasUnsavedChanges = true;
                        RebuildAllWalls();
                    }
                }
                
                // Offset
                var offset = wall.Offset;
                if (ImGui.DragInt("Offset", ref offset, 10f))
                {
                    if (wall.Offset != offset)
                    {
                        wall.Offset = offset;
                        ActiveTab!.HasUnsavedChanges = true;
                        RebuildAllWalls();
                    }
                }
                
                ImGui.Separator();
                
                if (ImGui.Button("Delete Border"))
                {
                    ActiveTab.StageWalls.Remove(wall);
                    ActiveTab.SelectedWallId = -1;
                    ActiveTab!.HasUnsavedChanges = true;
                }
            }
        }
        else if (ActiveTab.SelectedPieceId >= 0)
        {
            var piece = ActiveTab.ScenePieces.Find(p => p.Id == ActiveTab.SelectedPieceId);
            if (piece != null)
            {
                ImGui.Text($"Selected: {piece.Name}");
                ImGui.Separator();
                
                // Position (Y is displayed with -250 offset, so 250 ground level shows as 0)
                var displayPos = new System.Numerics.Vector3(piece.Position.X, piece.Position.Y - 250, piece.Position.Z);
                if (ImGui.DragFloat3("Position", ref displayPos, 10f))
                {
                    piece.Position = new Vector3(displayPos.X, displayPos.Y + 250, displayPos.Z);
                    ActiveTab!.HasUnsavedChanges = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Y is offset by 250 for clarity.\nY=0 represents ground level (actual Y=250)");
                }
                
                // Rotation (Y only - Yaw)
                var rotY = piece.Rotation.Y;
                if (ImGui.DragFloat("Rotation (Yaw)", ref rotY, 1f, -180f, 180f))
                {
                    piece.Rotation = new Vector3(piece.Rotation.X, rotY, piece.Rotation.Z);
                    ActiveTab!.HasUnsavedChanges = true;
                }
                
                // Piece Type
                var pieceType = (int)piece.PieceType;
                string[] typeNames = { "Set", "Checkpoint", "Fix Hoop" };
                if (ImGui.Combo("Type", ref pieceType, typeNames, typeNames.Length))
                {
                    piece.PieceType = (StagePieceInstance.PieceTypeEnum)pieceType;
                    ActiveTab!.HasUnsavedChanges = true;
                }
                
                ImGui.Separator();
                
                if (ImGui.Button("Delete"))
                {
                    if (ActiveTab?.Stage != null)
                    {
                        // Find and remove from stage pieces array
                        for (int i = 0; i < ActiveTab?.Stage.pieces.Count; i++)
                        {
                            if (ActiveTab?.Stage.pieces[i] == piece.MeshRef)
                            {
                                ActiveTab?.Stage.pieces.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    
                    ActiveTab.ScenePieces.Remove(piece);
                    ActiveTab.SelectedPieceId = -1;
                    ActiveTab!.HasUnsavedChanges = true;
                }
            }
        }
        else
        {
            ImGui.TextDisabled("No piece selected");
        }
    }
    
    private void RenderPartsLibrary()
    {
        ImGui.Text("Stage Parts Library");
        ImGui.Separator();
        
        ImGui.Text($"Available parts: {_availableParts.Count}");
        
        // Simple list for now - we'll add thumbnails later
        ImGui.BeginChild("PartsScroll");
        
        for (int i = 0; i < _availableParts.Count; i++)
        {
            var part = _availableParts[i];
            
            if (ImGui.Selectable(part.Name))
            {
                // Create a new mesh instance and add it to the stage (stage is guaranteed to exist here since we return early if null)
                var newMesh = new Mesh(
                    part.Mesh!,
                    Vector3.Zero,
                    Euler.Identity
                );
                
                var instance = new StagePieceInstance(
                    part.Name,
                    newMesh,
                    ActiveTab.GetNextPieceId()
                );
                
                ActiveTab.ScenePieces.Add(instance);
                ActiveTab!.Stage!.pieces[ActiveTab!.Stage.stagePartCount] = newMesh;
                ActiveTab.SelectedPieceId = instance.Id;
                ActiveTab!.HasUnsavedChanges = true;
            }
            
            // TODO: Add drag-and-drop support
        }
        
        ImGui.EndChild();
    }
}



