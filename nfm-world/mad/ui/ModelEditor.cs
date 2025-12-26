using ImGuiNET;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad.UI;

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
    public Vector3 ModelRotation { get; set; } = new Vector3(0, 0, 0);
    public Vector3 ModelPosition { get; set; } = new Vector3(0, 0, 0);
    public float CameraDistance { get; set; } = 800f;
    public Vector3 CameraPosition { get; set; } = new Vector3(0, 0, -800);
    public float CameraYaw { get; set; } = 0f;
    public float CameraPitch { get; set; } = -10f;
    
    // Selection state
    public int SelectedPolygonIndex { get; set; } = -1;
    
    // Mouse drag state for camera control
    public bool IsDragging { get; set; } = false;
    public int DragStartX { get; set; } = 0;
    public int DragStartY { get; set; } = 0;
    public float DragStartCameraYaw { get; set; } = 0f;
    public float DragStartCameraPitch { get; set; } = 0f;
    public Vector3 DragStartModelPosition { get; set; } = Vector3.Zero;
    
    // Text editor highlighting
    public int TextEditorSelectionStart { get; set; } = -1;
    public int TextEditorSelectionEnd { get; set; } = -1;
    
    // Polygon editor window
    public bool ShowPolygonEditor { get; set; } = false;
    public string PolygonEditorContent { get; set; } = "";
    public bool PolygonEditorDirty { get; set; } = false;
    public int PolygonEditorLastSelectedIndex { get; set; } = -1;
    
    // Reference car overlay for scaling
    public bool ShowReferenceOverlay { get; set; } = false;
    public int ReferenceCarIndex { get; set; } = 0;
    public float ReferenceOpacity { get; set; } = 0.5f;
    
    // Model type (car or stage scenery)
    public enum ModelTypeEnum { Car, Stage }
    public ModelTypeEnum ModelType { get; set; } = ModelTypeEnum.Car;
    
    // Collision editing (for stage scenery)
    public enum EditModeEnum { Polygon, Collision }
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
    private readonly GraphicsDevice _graphicsDevice;
    private bool _isOpen = false;
    private string[] _userModelNames = [];
    private Stage? _modelViewerStage;
    
    // Tab management
    private List<ModelEditorTab> _tabs = new();
    private int _activeTabIndex = -1;
    
    // UI state
    private bool _showNewModelDialog = false;
    private bool _showLoadDialog = false;
    private bool _isCreatingCar = true; // true = car, false = stage
    private string _newModelName = "";
    private int _selectedReferenceModel = 0;
    private bool _importReference = false;
    private bool _openInNewTab = true;
    
    // Control states (shared across all tabs)
    private bool _moveForward = false;
    private bool _moveBackward = false;
    private bool _rotateLeft = false;
    private bool _rotateRight = false;
    private bool _rotatePitchUp = false;
    private bool _rotatePitchDown = false;
    private bool _rotateRollLeft = false;
    private bool _rotateRollRight = false;
    private bool _raiseUp = false;
    private bool _lowerDown = false;

    private bool _lightsOn = true;
    
    // Rotation speeds
    private const float ROTATION_SPEED = 3.5f;
    private const float HEIGHT_SPEED = 5.0f;
    
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

    public ModelEditorPhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        scene = new Scene(graphicsDevice, [], camera, []);
        overlayScene = new Scene(graphicsDevice, [], camera, []);
        RefreshUserModels();
    }
    
    private void RefreshUserModels()
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
    
    private ModelEditorTab? ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;

    public override void Enter()
    {
        base.Enter();

        _isOpen = true;
        
        World.ResetValues();
        
        camera.Position = new Vector3(0, -800, -800);
        camera.LookAt = Vector3.Zero;
        
        GameSparker._graphicsDevice.BlendState = BlendState.Opaque;
        GameSparker._graphicsDevice.DepthStencilState = DepthStencilState.Default;
        
        RefreshUserModels();
    }

    public override void Exit()
    {
        base.Exit();

        _isOpen = false;
        _tabs.Clear();
        _activeTabIndex = -1;
    }
    
    private ModelEditorTab CreateNewTab()
    {
        var tab = new ModelEditorTab();
        ResetTabView(tab);
        return tab;
    }
    
    private void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        
        var tab = _tabs[index];
        
        // Check for unsaved changes
        if (tab.TextEditorDirty && tab.ModelPath != null)
        {
            GameSparker.MessageWindow.ShowCustom("Unsaved changes", 
                $"Save changes to {Path.GetFileName(tab.ModelPath)}?",
                new[] { "Save", "Don't Save", "Cancel" },
                result => {
                    if (result == MessageWindow.MessageResult.Custom1)
                    {
                        // Save
                        try
                        {
                            System.IO.File.WriteAllText(tab.ModelPath!, tab.TextContent);
                            tab.TextEditorDirty = false;
                            PerformCloseTab(index);
                        }
                        catch (Exception ex)
                        {
                            GameSparker.MessageWindow.ShowMessage("Error", $"Error saving: {ex.Message}");
                        }
                    }
                    else if (result == MessageWindow.MessageResult.Custom2)
                    {
                        // Don't save
                        PerformCloseTab(index);
                    }
                    // Cancel - do nothing
                });
        }
        else
        {
            PerformCloseTab(index);
        }
    }
    
    private void PerformCloseTab(int index)
    {
        _tabs.RemoveAt(index);
        
        // Adjust active tab index
        if (_tabs.Count == 0)
        {
            _activeTabIndex = -1;
        }
        else if (_activeTabIndex >= _tabs.Count)
        {
            _activeTabIndex = _tabs.Count - 1;
        }
    }
    
    private void CreateNewModel()
    {
        if (string.IsNullOrWhiteSpace(_newModelName)) return;
        
        var category = _isCreatingCar ? "cars" : "stage";
        var userPath = $"./data/models/user/{category}";
        Directory.CreateDirectory(userPath);
        
        var filePath = Path.Combine(userPath, _newModelName + ".rad");
        
        if (System.IO.File.Exists(filePath))
        {
            // File already exists
            return;
        }
        
        string radContent;
        
        if (_importReference && _selectedReferenceModel >= 0)
        {
            // Import reference model
            var builtInPath = $"./data/models/{category}";
            var refModels = _isCreatingCar ? GameSparker.CarRads : GameSparker.StageRads;
            
            if (_selectedReferenceModel < refModels.Length)
            {
                var refFile = Path.Combine(builtInPath, refModels[_selectedReferenceModel] + ".rad");
                if (System.IO.File.Exists(refFile))
                {
                    radContent = System.IO.File.ReadAllText(refFile);
                }
                else
                {
                    radContent = CreateEmptyRadFile();
                }
            }
            else
            {
                radContent = CreateEmptyRadFile();
            }
        }
        else
        {
            radContent = CreateEmptyRadFile();
        }
        
        System.IO.File.WriteAllText(filePath, radContent);
        
        // Load the new model
        LoadModel(filePath, _openInNewTab);
        
        // Refresh list
        RefreshUserModels();
        
        // Close dialog
        _showNewModelDialog = false;
        _newModelName = "";
        _importReference = false;
    }
    
    private string CreateEmptyRadFile()
    {
        // Create a minimal valid .rad file
        return @"// Created with NFM World Model Editor

";
    }
    
    private void LoadModel(string filePath, bool openInNewTab = false)
    {
        // Check if already open
        var existingTabIndex = _tabs.FindIndex(t => t.ModelPath == filePath);
        if (existingTabIndex >= 0 && !openInNewTab)
        {
            // Switch to existing tab
            _activeTabIndex = existingTabIndex;
            return;
        }
        
        try
        {
            var radContent = System.IO.File.ReadAllText(filePath);
            
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
                // Use current tab
                tab = ActiveTab!;
                
                // Check for unsaved changes
                if (tab.TextEditorDirty && tab.ModelPath != null)
                {
                    var currentPath = tab.ModelPath;
                    GameSparker.MessageWindow.ShowCustom("Unsaved changes", 
                        $"Save changes to {Path.GetFileName(currentPath)}?",
                        new[] { "Save", "Don't Save", "Cancel" },
                        result => {
                            if (result == MessageWindow.MessageResult.Custom1)
                            {
                                try
                                {
                                    System.IO.File.WriteAllText(currentPath, tab.TextContent);
                                    LoadModelIntoTab(tab, filePath, radContent);
                                }
                                catch (Exception ex)
                                {
                                    GameSparker.MessageWindow.ShowMessage("Error", $"Error saving: {ex.Message}");
                                }
                            }
                            else if (result == MessageWindow.MessageResult.Custom2)
                            {
                                LoadModelIntoTab(tab, filePath, radContent);
                            }
                        });
                    return;
                }
            }
            
            LoadModelIntoTab(tab, filePath, radContent);
        }
        catch (Exception ex)
        {
            var err = $"Error loading file: {ex.Message}";
            GameSparker.MessageWindow.ShowMessage("Error", err);
            if (GameSparker.Writer != null)
            {
                GameSparker.Writer.WriteLine(err, "error");
            }
        }
    }
    
    private void LoadModelIntoTab(ModelEditorTab tab, string filePath, string radContent)
    {
        tab.ModelPath = filePath;
        tab.TextContent = radContent;
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
            tab.Object = new EditorObject(new EditorObjectInfo(GameSparker._graphicsDevice, RadParser.ParseRad(radContent), "editing"));
            ResetTabView(tab);
        }
        catch (Exception parseEx)
        {
            var err = $"Error parsing model: {parseEx.Message}\n\nFile loaded in text editor for correction.";
            GameSparker.MessageWindow.ShowMessage("Parse Error", err);
            if (GameSparker.Writer != null)
            {
                GameSparker.Writer.WriteLine($"Parse error in {Path.GetFileName(filePath)}: {parseEx.Message}", "error");
            }
            tab.Object = null;
        }
    }
    
    private void ResetTabView(ModelEditorTab tab)
    {
        tab.ModelRotation = new Vector3(0, 0, 0);
        tab.ModelPosition = new Vector3(0, 0, 0);
        tab.CameraDistance = 800f;
        tab.CameraYaw = 0f;
        tab.CameraPitch = 10f;
        UpdateTabCameraPosition(tab);
    }
    
    private void UpdateTabCameraPosition(ModelEditorTab tab)
    {
        // Position camera in orbit around the origin
        // Camera orbits at the specified distance
        var yawRad = MathUtil.DegreesToRadians(tab.CameraYaw);
        var pitchRad = MathUtil.DegreesToRadians(tab.CameraPitch);
        
        var x = tab.CameraDistance * (float)(Math.Cos(pitchRad) * Math.Sin(yawRad));
        var y = -tab.CameraDistance * (float)Math.Sin(pitchRad);
        var z = tab.CameraDistance * (float)(Math.Cos(pitchRad) * Math.Cos(yawRad));
        
        tab.CameraPosition = new Vector3(x, y, -z);
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);
        if (imguiWantsKeyboard) return;
        if (!_isOpen) return;
        
        switch (key)
        {
            case Keys.W:
                _moveForward = true;
                break;
            case Keys.S:
                _moveBackward = true;
                break;
            case Keys.A:
                _rotateLeft = true;
                break;
            case Keys.D:
                _rotateRight = true;
                break;
            case Keys.Up:
                _rotatePitchUp = true;
                break;
            case Keys.Down:
                _rotatePitchDown = true;
                break;
            case Keys.Left:
                _rotateRollLeft = true;
                break;
            case Keys.Right:
                _rotateRollRight = true;
                break;
            case Keys.Oemplus:
            case Keys.Add:
                _raiseUp = true;
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                _lowerDown = true;
                break;
        }
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);
        
        if (imguiWantsKeyboard) return;
        
        if (!_isOpen) return;
        
        switch (key)
        {
            case Keys.W:
                _moveForward = false;
                break;
            case Keys.S:
                _moveBackward = false;
                break;
            case Keys.A:
                _rotateLeft = false;
                break;
            case Keys.D:
                _rotateRight = false;
                break;
            case Keys.Up:
                _rotatePitchUp = false;
                break;
            case Keys.Down:
                _rotatePitchDown = false;
                break;
            case Keys.Left:
                _rotateRollLeft = false;
                break;
            case Keys.Right:
                _rotateRollRight = false;
                break;
            case Keys.Oemplus:
            case Keys.Add:
                _raiseUp = false;
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                _lowerDown = false;
                break;
        }
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse)
    {
        base.MouseMoved(x, y, imguiWantsMouse);

        if (imguiWantsMouse) return;
        if (!GameSparker._game.IsActive) return;
    
        if (!_isOpen) return;
        
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
                var panX = (-rightX * deltaX + upX * deltaY) * sensitivity;
                var panY = upY * deltaY * sensitivity;
                var panZ = (-rightZ * deltaX + upZ * deltaY) * sensitivity;
                
                tab.ModelPosition = tab.DragStartModelPosition + new Vector3(panX, panY, panZ);
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
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse)
    {
        base.MousePressed(x, y, imguiWantsMouse);
        if (imguiWantsMouse) return;
        if (!GameSparker._game.IsActive) return;
        if (!_isOpen) return;
        
        var tab = ActiveTab;
        if (tab != null)
        {
            // Check which button was pressed
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            _isLeftButtonDown = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            _isRightButtonDown = mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            
            // Check shift state
            var keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            _isShiftPressed = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || 
                             keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
            
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
    
    public override void MouseScrolled(int delta, bool imguiWantsMouse)
    {
        base.MouseScrolled(delta, imguiWantsMouse);
        if (imguiWantsMouse) return;
        if (!GameSparker._game.IsActive) return;
        if (!_isOpen) return;
        
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

    public override void MouseReleased(int x, int y, bool imguiWantsMouse)
    {
        base.MouseReleased(x, y, imguiWantsMouse);
        
        var tab = ActiveTab;
        if (tab != null)
        {
            // Check if this was a click (not a drag)
            bool wasClick = tab.IsDragging && 
                           GameSparker._game.IsActive &&
                           Math.Abs(x - tab.DragStartX) < 5 && 
                           Math.Abs(y - tab.DragStartY) < 5;
            
            // Stop dragging
            tab.IsDragging = false;
            
            // Update button states
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            _isLeftButtonDown = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            _isRightButtonDown = mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            
            // Process click for polygon/collision selection only if it was a simple click, not a drag
            if (wasClick && !imguiWantsMouse && tab.Object != null)
            {
                if (tab.EditMode == ModelEditorTab.EditModeEnum.Polygon)
                {
                    var pickedIndex = PerformRayPicking(x, y, tab);
                    
                    if (pickedIndex >= 0)
                    {
                        tab.SelectedPolygonIndex = pickedIndex;
                    }
                    else
                    {
                        // Clicked on background, deselect
                        tab.SelectedPolygonIndex = -1;
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
            }
        }
    }
    
    private void ProcessMouseClick()
    {
        // This method is no longer needed as click detection moved to MouseReleased
    }
    
    private int PerformRayPicking(int screenX, int screenY, ModelEditorTab tab)
    {
        if (tab.Object == null) return -1;
        
        var viewport = GameSparker._graphicsDevice.Viewport;
        
        // Set up the model's transform exactly as RenderModel does
        var originalPosition = tab.Object.Position;
        var originalRotation = tab.Object.Rotation;
        
        tab.Object.Position = tab.ModelPosition;
        tab.Object.Rotation = new Euler(
            AngleSingle.FromDegrees(tab.ModelRotation.Y),  // Yaw
            AngleSingle.FromDegrees(-tab.ModelRotation.X), // Pitch (negated)
            AngleSingle.FromDegrees(tab.ModelRotation.Z)   // Roll
        );
        
        // Get the ACTUAL MatrixWorld that will be used for rendering
        var modelWorld = tab.Object.MatrixWorld;
        
        // Restore transform
        tab.Object.Position = originalPosition;
        tab.Object.Rotation = originalRotation;
        
        // Set up camera exactly as RenderModel does
        // Use GameSparker.camera's actual Width/Height (not viewport, which might differ)
        var tempCamera = new PerspectiveCamera
        {
            Position = tab.CameraPosition,
            LookAt = Vector3.Zero,
            Up = -Vector3.UnitY,
            Width = camera.Width,
            Height = camera.Height,
            Fov = camera.Fov,  // Use actual FOV from settings
            Near = camera.Near,
            Far = camera.Far
        };
        
        // Call OnBeforeRender to compute the View and Projection matrices
        tempCamera.OnBeforeRender();
        
        var view = tempCamera.ViewMatrix;
        var projection = tempCamera.ProjectionMatrix;
        
        // Unproject screen coordinates to world space ray
        var nearPoint = viewport.Unproject(
            new Vector3(screenX, screenY, 0f),
            projection,
            view,
            Matrix.Identity
        );
        
        var farPoint = viewport.Unproject(
            new Vector3(screenX, screenY, 1f),
            projection,
            view,
            Matrix.Identity
        );
        
        var rayOrigin = nearPoint;
        var rayDirection = Vector3.Normalize(farPoint - nearPoint);
        
        // Test against all polygons
        float closestDistance = float.MaxValue;
        int closestPolyIndex = -1;
        
        for (int i = 0; i < tab.Object.Mesh.Polys.Length; i++)
        {
            var poly = tab.Object.Mesh.Polys[i];
            var triangulation = tab.Object.Mesh.Triangulation[i];
            
            // Test each triangle in this polygon
            for (int t = 0; t < triangulation.Triangles.Length; t += 3)
            {
                var i0 = triangulation.Triangles[t];
                var i1 = triangulation.Triangles[t + 1];
                var i2 = triangulation.Triangles[t + 2];
                
                // Transform vertices by the model's world matrix
                var v0 = Vector3.Transform(poly.Points[i0], modelWorld);
                var v1 = Vector3.Transform(poly.Points[i1], modelWorld);
                var v2 = Vector3.Transform(poly.Points[i2], modelWorld);
                
                if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v1, v2, out float distance))
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPolyIndex = i;
                    }
                }
            }
        }
        
        return closestPolyIndex;
    }
    
    // Möller–Trumbore ray-triangle intersection algorithm
    private bool RayIntersectsTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        out float distance)
    {
        distance = 0;
        const float EPSILON = 0.000001f;  // Smaller epsilon for better accuracy
        
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
        if (tab.Object == null || tab.Object.Boxes.Length == 0) return -1;
        
        var viewport = GameSparker._graphicsDevice.Viewport;
        
        // Set up camera exactly as RenderModel does
        var tempCamera = new PerspectiveCamera
        {
            Position = tab.CameraPosition,
            LookAt = Vector3.Zero,
            Up = -Vector3.UnitY,
            Width = camera.Width,
            Height = camera.Height,
            Fov = camera.Fov,
            Near = camera.Near,
            Far = camera.Far
        };
        
        tempCamera.OnBeforeRender();
        
        var view = tempCamera.ViewMatrix;
        var projection = tempCamera.ProjectionMatrix;
        
        // Unproject screen coordinates to world space ray
        var nearPoint = viewport.Unproject(
            new Vector3(screenX, screenY, 0f),
            projection,
            view,
            Matrix.Identity
        );
        
        var farPoint = viewport.Unproject(
            new Vector3(screenX, screenY, 1f),
            projection,
            view,
            Matrix.Identity
        );
        
        var rayOrigin = nearPoint;
        var rayDirection = Vector3.Normalize(farPoint - nearPoint);
        
        // Test against all collision boxes
        float closestDistance = float.MaxValue;
        int closestBoxIndex = -1;
        
        for (int i = 0; i < tab.Object.Boxes.Length; i++)
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
    
    private void JumpToSelectedPolygonInText()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedPolygonIndex < 0 || tab.Object == null) return;
        
        // Find the polygon in the text by searching for <p> tags and counting
        int polygonCount = 0;
        int selectionStart = -1;
        int selectionEnd = -1;
        
        for (int i = 0; i < tab.TextContent.Length; i++)
        {
            // Check if we're at the start of a <p> tag
            if (i + 3 <= tab.TextContent.Length && 
                tab.TextContent.Substring(i, 3) == "<p>")
            {
                if (polygonCount == tab.SelectedPolygonIndex)
                {
                    // Found the start of our target polygon
                    selectionStart = i;
                    
                    // Now find the closing </p> tag
                    int searchPos = i + 3;
                    while (searchPos + 4 <= tab.TextContent.Length)
                    {
                        if (tab.TextContent.Substring(searchPos, 4) == "</p>")
                        {
                            selectionEnd = searchPos + 4; // Include the closing tag
                            break;
                        }
                        searchPos++;
                    }
                    
                    // If we didn't find a closing tag, select until the next <p> or end of file
                    if (selectionEnd == -1)
                    {
                        searchPos = i + 3;
                        while (searchPos + 3 <= tab.TextContent.Length)
                        {
                            if (tab.TextContent.Substring(searchPos, 3) == "<p>")
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
                polygonCount++;
            }
        }
        
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
        }
        
        // Debug output
        if (GameSparker.Writer != null && selectionStart >= 0)
        {
            GameSparker.Writer.WriteLine($"Polygon {tab.SelectedPolygonIndex + 1}: selection range {selectionStart}-{selectionEnd}", "info");
        }
    }
    
    private void JumpToSelectedCollisionInText()
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
            if (i + 7 <= tab.TextContent.Length && 
                tab.TextContent.Substring(i, 7) == "<track>")
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
        }
        
        // Debug output
        if (GameSparker.Writer != null && selectionStart >= 0)
        {
            GameSparker.Writer.WriteLine($"Collision {tab.SelectedCollisionIndex + 1}: selection range {selectionStart}-{selectionEnd}", "info");
        }
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
        
        var pos = tab.ModelPosition;
        var rot = tab.ModelRotation;
        
        // Apply movement
        if (_moveForward)
            pos.Z += HEIGHT_SPEED;
        if (_moveBackward)
            pos.Z -= HEIGHT_SPEED;
        
        // Apply yaw rotation (A/D)
        if (_rotateLeft)
            rot.Y -= ROTATION_SPEED;
        if (_rotateRight)
            rot.Y += ROTATION_SPEED;
        
        // Apply pitch rotation (Up/Down arrows)
        if (_rotatePitchUp)
            rot.X -= ROTATION_SPEED;
        if (_rotatePitchDown)
            rot.X += ROTATION_SPEED;
        
        // Apply roll rotation (Left/Right arrows)
        if (_rotateRollLeft)
            rot.Z -= ROTATION_SPEED;
        if (_rotateRollRight)
            rot.Z += ROTATION_SPEED;
        
        // Apply height changes (+/-)
        if (_raiseUp)
            pos.Y += HEIGHT_SPEED;
        if (_lowerDown)
            pos.Y -= HEIGHT_SPEED;
        
        tab.ModelPosition = pos;
        tab.ModelRotation = rot;
        
        UpdateTabCameraPosition(tab);
    }

    public override void Render()
    {
        base.Render();

        _graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(135, 206, 235));
    }

    public override void RenderImgui()
    {
        base.RenderImgui();
        
        if (!_isOpen) return;
        
        var io = ImGui.GetIO();
        var displaySize = io.DisplaySize;
        
        // File menu bar (main menu bar at top of screen)
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New"))
                {
                    _showNewModelDialog = true;
                }
                
                if (ImGui.MenuItem("Open..."))
                {
                    _showLoadDialog = true;
                }
                
                ImGui.Separator();
                
                if (ImGui.MenuItem("Exit"))
                {
                    // Check for any unsaved tabs
                    var unsavedTabs = _tabs.Where(t => t.TextEditorDirty && t.ModelPath != null).ToList();
                    if (unsavedTabs.Any())
                    {
                        var tabNames = string.Join(", ", unsavedTabs.Select(t => Path.GetFileName(t.ModelPath)));
                        GameSparker.MessageWindow.ShowCustom("Unsaved changes", 
                            $"Save changes to: {tabNames}?",
                            new[] { "Save All", "Don't Save", "Cancel" },
                            result => {
                                if (result == MessageWindow.MessageResult.Custom1) {
                                    foreach (var tab in unsavedTabs)
                                    {
                                        try {
                                            System.IO.File.WriteAllText(tab.ModelPath!, tab.TextContent);
                                            tab.TextEditorDirty = false;
                                        } catch { }
                                    }
                                    ExitModelViewer();
                                }
                                if (result == MessageWindow.MessageResult.Custom2) {
                                    ExitModelViewer();
                                }
                        });
                    } else {
                        ExitModelViewer();
                    }
                }
                
                ImGui.EndMenu();
            }
            
            ImGui.EndMainMenuBar();
        }
        
        var menuBarHeight = ImGui.GetFrameHeight();
        
        // Tab bar for open models
        float tabBarHeight = 0;
        if (_tabs.Count > 0)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, menuBarHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, 0));
            ImGui.Begin("##TabBar", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            
            if (ImGui.BeginTabBar("ModelTabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs | ImGuiTabBarFlags.FittingPolicyScroll))
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    var tabItem = _tabs[i];
                    bool tabOpen = true;
                    var tabFlags = ImGuiTabItemFlags.None;
                    if (tabItem.TextEditorDirty)
                        tabFlags |= ImGuiTabItemFlags.UnsavedDocument;
                    
                    if (ImGui.BeginTabItem(tabItem.GetDisplayName() + "###Tab" + i, ref tabOpen, tabFlags))
                    {
                        _activeTabIndex = i;
                        ImGui.EndTabItem();
                    }
                    
                    if (!tabOpen)
                    {
                        CloseTab(i);
                        break; // Exit loop after closing to avoid index issues
                    }
                }
                
                ImGui.EndTabBar();
            }
            
            tabBarHeight = ImGui.GetWindowHeight();
            ImGui.End();
        }
        
        // Toolbar - always visible when model is loaded
        float toolbarHeight = 0;
        var tab = ActiveTab;
        if (tab != null)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, menuBarHeight + tabBarHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, 0));
            ImGui.Begin("##Toolbar", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            
            if (ImGui.Button(tab.TextEditorExpanded ? "Hide Code Editor" : "Show Code Editor"))
            {
                tab.TextEditorExpanded = !tab.TextEditorExpanded;
            }
            
            ImGui.SameLine();
            ImGui.Text($"Editing: {Path.GetFileName(tab.ModelPath ?? "Untitled")}");
            
            if (tab.TextEditorExpanded)
            {
                ImGui.SameLine();
                
                if (ImGui.Button("Save"))
                {
                    try
                    {
                        if (tab.ModelPath != null)
                        {
                            System.IO.File.WriteAllText(tab.ModelPath, tab.TextContent);
                            tab.TextEditorDirty = false;
                            // Reload model
                            tab.Object = new EditorObject(new EditorObjectInfo(GameSparker._graphicsDevice, RadParser.ParseRad(tab.TextContent), "editing"));
                        }
                    }
                    catch (Exception ex)
                    {
                        var err = $"Error saving/reloading model:\n{ex.Message}";
                        GameSparker.MessageWindow.ShowMessage("Error", err);
                        if (GameSparker.Writer != null)
                            GameSparker.Writer.WriteLine(err, "error");
                    }
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Save & Preview"))
                {
                    try
                    {
                        if (tab.ModelPath != null)
                        {
                            System.IO.File.WriteAllText(tab.ModelPath, tab.TextContent);
                            tab.TextEditorDirty = false;
                            // Reload model
                            tab.Object = new EditorObject(new EditorObjectInfo(GameSparker._graphicsDevice, RadParser.ParseRad(tab.TextContent), "editing"));
                            tab.TextEditorExpanded = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        var err = $"Error saving/reloading model:\n{ex.Message}";
                        GameSparker.MessageWindow.ShowMessage("Error", err);
                        if (GameSparker.Writer != null)
                        {
                            GameSparker.Writer.WriteLine(err, "error");
                        }
                    }
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Open in External Editor"))
                {
                    try
                    {
                        if (tab.ModelPath != null)
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = tab.ModelPath,
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                    }
                    catch (Exception ex)
                    {
                        var err = $"Error opening external editor: {ex.Message}";
                        GameSparker.MessageWindow.ShowMessage("Error", err);
                        if (GameSparker.Writer != null)
                        {
                            GameSparker.Writer.WriteLine(err, "error");
                        }
                    }
                }
                
                ImGui.SameLine();
                ImGui.Text(tab.TextEditorDirty ? "(Modified)" : "");
            }
            
            // Polygon/Collision selection info and controls (always visible when model is loaded)
            if (tab.Object != null)
            {
                ImGui.Separator();
                
                // Edit mode toggle for stage scenery
                if (tab.ModelType == ModelEditorTab.ModelTypeEnum.Stage)
                {
                    ImGui.Text("Edit Mode:");
                    ImGui.SameLine();
                    
                    int editMode = tab.EditMode == ModelEditorTab.EditModeEnum.Polygon ? 0 : 1;
                    if (ImGui.RadioButton("Polygon", editMode == 0))
                    {
                        tab.EditMode = ModelEditorTab.EditModeEnum.Polygon;
                        tab.SelectedCollisionIndex = -1;
                    }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Collision", editMode == 1))
                    {
                        tab.EditMode = ModelEditorTab.EditModeEnum.Collision;
                        tab.SelectedPolygonIndex = -1;
                    }
                    ImGui.SameLine();
                    // ImGui.TextDisabled($"({tab.Model.Boxes.Length} collision boxes)");
                }
                
                // Polygon editing UI
                if (tab.EditMode == ModelEditorTab.EditModeEnum.Polygon)
                {
                    if (tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex < tab.Object.Mesh.Polys.Length)
                    {
                        ImGui.Text($"[ Piece {tab.SelectedPolygonIndex + 1} of {tab.Object.Mesh.Polys.Length} selected ]");
                        ImGui.SameLine();
                        
                        if (ImGui.Button("Edit Polygon"))
                        {
                            JumpToSelectedPolygonInText();
                        }
                        ImGui.SameLine();
                        
                        // currently unstable and i cba rn
                        // if (ImGui.Button("Flip Vertex Order"))
                        // {
                        //     FlipSelectedPolygonVertexOrder();
                        // }
                        // ImGui.SameLine();
                        
                        // if (ImGui.Button("Remove Polygon"))
                        // {
                        //     RemoveSelectedPolygon();
                        // }
                        // ImGui.SameLine();
                        
                        if (ImGui.Button("X"))
                        {
                            tab.SelectedPolygonIndex = -1; // Deselect
                        }
                    }
                    else
                    {
                        ImGui.Text("Click on a polygon in the 3D view to select it");
                        if (_mouseX > 0 || _mouseY > 0)
                        {
                            ImGui.SameLine();
                            ImGui.TextDisabled($"(Mouse: {_mouseX}, {_mouseY})");
                        }
                    }
                }
                // Collision editing UI
                else if (tab.EditMode == ModelEditorTab.EditModeEnum.Collision)
                {
                    if (tab.SelectedCollisionIndex >= 0 && tab.SelectedCollisionIndex < tab.Object.Boxes.Length)
                    {
                        var box = tab.Object.Boxes[tab.SelectedCollisionIndex];
                        ImGui.Text($"[ Collision {tab.SelectedCollisionIndex + 1} of {tab.Object.Boxes.Length} selected ]");
                        ImGui.SameLine();
                        
                        if (ImGui.Button("Edit Collision"))
                        {
                            JumpToSelectedCollisionInText();
                        }
                        ImGui.SameLine();
                        
                        if (ImGui.Button("X"))
                        {
                            tab.SelectedCollisionIndex = -1; // Deselect
                        }
                        
                        // Display collision properties
                        ImGui.Text($"Angle: xy={box.Xy:F0}° zy={box.Zy:F0}°");
                        ImGui.SameLine();
                        ImGui.Text($"| Radius: [{box.Radius.X:F0}, {box.Radius.Y:F0}, {box.Radius.Z:F0}]");
                        ImGui.SameLine();
                        ImGui.Text($"| Offset: [{box.Translation.X:F0}, {box.Translation.Y:F0}, {box.Translation.Z:F0}]");
                    }
                    else
                    {
                        ImGui.Text("Click on a collision box in the 3D view to select it");
                        if (_mouseX > 0 || _mouseY > 0)
                        {
                            ImGui.SameLine();
                            ImGui.TextDisabled($"(Mouse: {_mouseX}, {_mouseY})");
                        }
                    }
                }
            }
            
            toolbarHeight = ImGui.GetWindowHeight();
            ImGui.End();
        }
        
        // Text editor panel (show when expanded)
        var showTextEditorPanel = tab != null && tab.TextEditorExpanded;
        float topPanelHeight = showTextEditorPanel ? displaySize.Y * 0.6f : 0;
        
        if (showTextEditorPanel && tab != null)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, menuBarHeight + tabBarHeight + toolbarHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, topPanelHeight - toolbarHeight));
            ImGui.Begin("##TextEditorPanel", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            
            // Text editor
            var flags = ImGuiInputTextFlags.AllowTabInput;
            var content = tab.TextContent;
            var textChanged = ImGui.InputTextMultiline("##TextEditor", ref content, 200000, 
                new System.Numerics.Vector2(-1, -1), flags);
                
            if (textChanged)
            {
                tab.TextContent = content;
                tab.TextEditorDirty = true;
            }
            
            ImGui.End();
        }
        
        // Bottom control panel (fixed position, full width)
        float bottomPanelHeight = 180;
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, displaySize.Y - bottomPanelHeight));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, bottomPanelHeight));
        ImGui.Begin("##BottomControls", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
        
        // Sub-tabs for bottom panel
        if (ImGui.BeginTabBar("ControlTabs"))
        {
            var activeTab = ActiveTab;
            
            if (ImGui.BeginTabItem("3D Controls"))
            {
                if (activeTab != null)
                {
                    ImGui.Columns(3, "ControlColumns", false);
                    
                    // Left column - Keyboard controls
                    ImGui.Text("Keyboard Controls:");
                    ImGui.Text("W/S: Move Forward/Back");
                    ImGui.Text("A/D: Rotate Yaw");
                    ImGui.Text("Up/Down: Rotate Pitch");
                    ImGui.Text("Left/Right: Rotate Roll");
                    ImGui.Text("+/-: Move Up/Down");
                    
                    ImGui.NextColumn();
                    
                    // Middle column - Mouse controls
                    ImGui.Text("Mouse Controls:");
                    ImGui.Text("Drag: Orbit camera");
                    ImGui.Text("Shift+Drag: Pan model");
                    ImGui.Text("Scroll Wheel: Zoom in/out");
                    ImGui.Text("Click: Select polygon");
                    
                    ImGui.NextColumn();
                    
                    // Right column - Sliders
                    float rotX = activeTab.ModelRotation.X;
                    float rotY = activeTab.ModelRotation.Y;
                    float rotZ = activeTab.ModelRotation.Z;
                    float posY = activeTab.ModelPosition.Y;
                    
                    if (ImGui.SliderFloat("X (Pitch)", ref rotX, -180f, 180f))
                    {
                        var rot = activeTab.ModelRotation;
                        rot.X = rotX;
                        activeTab.ModelRotation = rot;
                    }
                    if (ImGui.SliderFloat("Y (Yaw)", ref rotY, -180f, 180f))
                    {
                        var rot = activeTab.ModelRotation;
                        rot.Y = rotY;
                        activeTab.ModelRotation = rot;
                    }
                    if (ImGui.SliderFloat("Z (Roll)", ref rotZ, -180f, 180f))
                    {
                        var rot = activeTab.ModelRotation;
                        rot.Z = rotZ;
                        activeTab.ModelRotation = rot;
                    }
                    
                    if (ImGui.SliderFloat("Height", ref posY, -500f, 500f))
                    {
                        var pos = activeTab.ModelPosition;
                        pos.Y = posY;
                        activeTab.ModelPosition = pos;
                    }
                    
                    float camDist = activeTab.CameraDistance;
                    if (ImGui.SliderFloat("Camera Distance", ref camDist, 300f, 5000f))
                    {
                        activeTab.CameraDistance = camDist;
                        UpdateTabCameraPosition(activeTab);
                    }
                    
                    if (ImGui.Button("Reset View"))
                    {
                        ResetTabView(activeTab);
                    }
                    
                    ImGui.Columns(1);
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Render"))
            {
                bool showTrackers = GameSparker.devRenderTrackers;
                if (ImGui.Checkbox("Display Collision", ref showTrackers))
                {
                    GameSparker.devRenderTrackers = showTrackers;
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Show collision boxes for stage scenery");
                }
                
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Color Edit"))
            {
                ImGui.Text("Color editing not yet implemented");
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Scale & Align"))
            {
                if (activeTab != null)
                {
                    ImGui.Text("Reference Car Overlay:");
                    ImGui.Spacing();
                    
                    bool showOverlay = activeTab.ShowReferenceOverlay;
                    if (ImGui.Checkbox("Show Reference Car", ref showOverlay))
                    {
                        activeTab.ShowReferenceOverlay = showOverlay;
                    }
                    
                    if (activeTab.ShowReferenceOverlay)
                    {
                        ImGui.Spacing();
                        
                        // Car selection dropdown
                        var carRads = GameSparker.CarRads;
                        int selectedCar = activeTab.ReferenceCarIndex;
                        
                        if (ImGui.BeginCombo("Reference Car", selectedCar < carRads.Length ? carRads[selectedCar] : ""))
                        {
                            for (int i = 0; i < carRads.Length; i++)
                            {
                                bool isSelected = (selectedCar == i);
                                if (ImGui.Selectable(carRads[i], isSelected))
                                {
                                    activeTab.ReferenceCarIndex = i;
                                }
                                
                                if (isSelected)
                                    ImGui.SetItemDefaultFocus();
                            }
                            ImGui.EndCombo();
                        }
                        
                        ImGui.Spacing();
                        
                        // Opacity slider
                        float opacity = activeTab.ReferenceOpacity;
                        if (ImGui.SliderFloat("Overlay Opacity", ref opacity, 0.1f, 1.0f))
                        {
                            activeTab.ReferenceOpacity = opacity;
                        }
                    }
                }
                else
                {
                    ImGui.Text("No model loaded");
                }
                ImGui.EndTabItem();
            }
            
            // Wheels tab - only for cars
            if (activeTab != null && activeTab.ModelType == ModelEditorTab.ModelTypeEnum.Car)
            {
                if (ImGui.BeginTabItem("Wheels"))
                {
                    ImGui.Text("Wheel editing not yet implemented");
                    ImGui.EndTabItem();
                }
            }
            
            // Stats & Class tab - only for cars
            if (activeTab != null && activeTab.ModelType == ModelEditorTab.ModelTypeEnum.Car)
            {
                if (ImGui.BeginTabItem("Stats & Class"))
                {
                    ImGui.Text("Stats & Class not yet implemented");
                    ImGui.EndTabItem();
                }
            }
            
            // Physics tab - only for cars
            if (activeTab != null && activeTab.ModelType == ModelEditorTab.ModelTypeEnum.Car)
            {
                if (ImGui.BeginTabItem("Physics"))
                {
                    ImGui.Text("Physics not yet implemented");
                    ImGui.EndTabItem();
                }
            }
            
            // Test Drive tab - only for cars
            if (activeTab != null && activeTab.ModelType == ModelEditorTab.ModelTypeEnum.Car)
            {
                if (ImGui.BeginTabItem("Test Drive"))
                {
                    ImGui.Text("Test Drive not yet implemented");
                    ImGui.EndTabItem();
                }
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        
        // New Model Dialog
        if (_showNewModelDialog)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(displaySize.X / 2 - 200, displaySize.Y / 2 - 150));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 300));
            ImGui.Begin("Create New Model", ref _showNewModelDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            
            ImGui.Text("Model Type:");
            int modelType = _isCreatingCar ? 0 : 1;
            if (ImGui.RadioButton("Car", modelType == 0))
                _isCreatingCar = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("Stage Scenery", modelType == 1))
                _isCreatingCar = false;
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            ImGui.Text("Model Name:");
            ImGui.InputText("##ModelName", ref _newModelName, 100);
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            ImGui.Checkbox("Open in New Tab", ref _openInNewTab);
            
            ImGui.Spacing();
            
            ImGui.Checkbox("Import Reference Model", ref _importReference);
            
            if (_importReference)
            {
                ImGui.Spacing();
                ImGui.Text("Select Reference:");
                
                var refModels = _isCreatingCar ? GameSparker.CarRads : GameSparker.StageRads;
                
                if (ImGui.BeginCombo("##Reference", _selectedReferenceModel < refModels.Length ? refModels[_selectedReferenceModel] : ""))
                {
                    for (int i = 0; i < refModels.Length; i++)
                    {
                        bool isSelected = (_selectedReferenceModel == i);
                        if (ImGui.Selectable(refModels[i], isSelected))
                        {
                            _selectedReferenceModel = i;
                        }
                        
                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            var canCreate = !string.IsNullOrWhiteSpace(_newModelName);
            if (!canCreate)
            {
                ImGui.BeginDisabled();
            }
            
            if (ImGui.Button("Create", new System.Numerics.Vector2(190, 30)))
            {
                CreateNewModel();
            }
            
            if (!canCreate)
            {
                ImGui.EndDisabled();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(190, 30)))
            {
                _showNewModelDialog = false;
                _newModelName = "";
                _importReference = false;
            }
            
            ImGui.End();
        }
        
        // Load Existing Dialog
        if (_showLoadDialog)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(displaySize.X / 2 - 250, displaySize.Y / 2 - 200));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 400));
            ImGui.Begin("Load Model", ref _showLoadDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            
            ImGui.Text("Select a model to load:");
            ImGui.Checkbox("Open in New Tab", ref _openInNewTab);
            ImGui.Separator();
            ImGui.Spacing();
            
            // Calculate available space for file list (leave room for spacing and button)
            var availHeight = ImGui.GetContentRegionAvail().Y - 40; // Reserve space for button and spacing
            
            // Create a child window for the file list with fixed height
            if (ImGui.BeginChild("FileList", new System.Numerics.Vector2(-1, availHeight), (ImGuiChildFlags)1))
            {
                // Show cars folder
                if (ImGui.CollapsingHeader("cars", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoAutoOpenOnLog))
                {
                    var carsPath = "./data/models/user/cars";
                    if (Directory.Exists(carsPath))
                    {
                        foreach (var file in Directory.GetFiles(carsPath, "*.rad"))
                        {
                            var fileName = Path.GetFileName(file);
                            var activeTab = ActiveTab;
                            var isCurrentModel = activeTab != null && activeTab.ModelPath != null && 
                                activeTab.ModelPath.EndsWith(file.Replace('/', '\\'));
                            
                            if (ImGui.Selectable($"  {fileName}##{file}", isCurrentModel))
                            {
                                LoadModel(file, _openInNewTab);
                                _showLoadDialog = false;
                            }
                        }
                    }
                }
                
                // Show stage folder
                if (ImGui.CollapsingHeader("stage", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoAutoOpenOnLog))
                {
                    var stagePath = "./data/models/user/stage";
                    if (Directory.Exists(stagePath))
                    {
                        foreach (var file in Directory.GetFiles(stagePath, "*.rad"))
                        {
                            var fileName = Path.GetFileName(file);
                            var activeTab = ActiveTab;
                            var isCurrentModel = activeTab != null && activeTab.ModelPath != null && 
                                activeTab.ModelPath.EndsWith(file.Replace('/', '\\'));
                            
                            if (ImGui.Selectable($"  {fileName}##{file}", isCurrentModel))
                            {
                                LoadModel(file, _openInNewTab);
                                _showLoadDialog = false;
                            }
                        }
                    }
                }
                
                ImGui.EndChild();
            }
            
            ImGui.Spacing();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(-1, 30)))
            {
                _showLoadDialog = false;
            }
            
            ImGui.End();
        }
        
        // Polygon Editor Window
        RenderPolygonEditorWindow();
    }
    
    private void RenderPolygonEditorWindow()
    {
        var tab = ActiveTab;
        if (tab == null || !tab.ShowPolygonEditor) return;
        
        // Determine if we're editing a polygon or collision
        bool editingCollision = tab.EditMode == ModelEditorTab.EditModeEnum.Collision;
        
        // Check if user selected a different polygon/collision while editor is open
        if (!editingCollision && tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex != tab.PolygonEditorLastSelectedIndex)
        {
            // Update the editor content to the newly selected polygon
            JumpToSelectedPolygonInText();
            return; // JumpToSelectedPolygonInText will refresh the window
        }
        else if (editingCollision && tab.SelectedCollisionIndex >= 0 && tab.SelectedCollisionIndex != tab.PolygonEditorLastSelectedIndex)
        {
            // Update the editor content to the newly selected collision
            JumpToSelectedCollisionInText();
            return;
        }
        
        var io = ImGui.GetIO();
        var displaySize = io.DisplaySize;
        
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(displaySize.X / 2 - 300, displaySize.Y / 2 - 250), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(600, 400), ImGuiCond.FirstUseEver);
        
        bool isOpen = tab.ShowPolygonEditor;
        string windowTitle = editingCollision ? "Edit Collision" : "Edit Polygon";
        if (ImGui.Begin(windowTitle, ref isOpen, ImGuiWindowFlags.None))
        {
            if (editingCollision)
            {
                ImGui.Text($"Editing collision {tab.SelectedCollisionIndex + 1} of {tab.Object?.Boxes.Length ?? 0}");
            }
            else
            {
                ImGui.Text($"Editing polygon {tab.SelectedPolygonIndex + 1} of {tab.Object?.Mesh.Polys.Length ?? 0}");
            }
            ImGui.Separator();
            
            // Calculate available space for text editor
            var contentRegionAvail = ImGui.GetContentRegionAvail();
            
            // Reserve space for the bottom controls (special flags + buttons)
            var bottomControlsHeight = 120f; // Space for flags section + buttons
            var textEditorHeight = contentRegionAvail.Y - bottomControlsHeight;
            
            // Main text editor
            var flags = ImGuiInputTextFlags.AllowTabInput;
            var content = tab.PolygonEditorContent;
            var textChanged = ImGui.InputTextMultiline("##PolygonCode", ref content, 50000,
                new System.Numerics.Vector2(-1, textEditorHeight), flags);
            
            if (textChanged)
            {
                tab.PolygonEditorContent = content;
                tab.PolygonEditorDirty = true;
            }
            
            ImGui.Spacing();
            
            // Special flags section
            ImGui.Text("Special Flags:");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                var tooltip = editingCollision 
                    ? "Special flags for collision behavior" 
                    : "Special flags which may change appearance or behavior";
                ImGui.SetTooltip(tooltip);
            }
            
            if (editingCollision)
            {
                // Collision-specific flags
                if (ImGui.Button("dam()"))
                {
                    Insert("dam()");
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("notwall()"))
                {
                    Insert("notwall()");
                }
            }
            else
            {
                // Polygon-specific flags
                if (ImGui.Button("gr(-18)"))
                {
                    Insert("gr(-18)");
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("decal"))
                {
                    Insert("decal");
                }

                ImGui.SameLine();
                
                if (ImGui.Button("noOutline()"))
                {
                    Insert("noOutline()");
                }
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            
            // Action buttons
            var buttonWidth = (contentRegionAvail.X - 10f) / 2f; // Split width for 2 buttons with spacing
            
            
            
            if (ImGui.Button("Apply", new System.Numerics.Vector2(buttonWidth, 30)))
            {
                ApplyPolygonEditorChanges();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(buttonWidth, 30)))
            {
                if (tab.PolygonEditorDirty)
                {
                    // Ask user if they want to save changes
                    GameSparker.MessageWindow.ShowCustom("Unsaved changes",
                        "Apply changes to polygon before closing?",
                        new[] { "Apply", "Discard", "Cancel" },
                        result => {
                            if (result == MessageWindow.MessageResult.Custom1)
                            {
                                // Apply changes
                                ApplyPolygonEditorChanges();
                                tab.ShowPolygonEditor = false;
                            }
                            else if (result == MessageWindow.MessageResult.Custom2)
                            {
                                // Discard changes and close
                                tab.ShowPolygonEditor = false;
                                tab.PolygonEditorDirty = false;
                            }
                            // Custom3 (Cancel) - do nothing, keep editor open
                        });
                }
                else
                {
                    // No changes, just close
                    tab.ShowPolygonEditor = false;
                }
            }
            else
            {
                // Update the state if not closing
                tab.ShowPolygonEditor = isOpen;
            }

            if (tab.PolygonEditorDirty)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "(Modified)");
            }
        }
        
        ImGui.End();
    }
    
    private void Insert(string command)
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
    }
    
    private void ApplyPolygonEditorChanges()
    {
        var tab = ActiveTab;
        if (tab == null || tab.TextEditorSelectionStart < 0 || tab.TextEditorSelectionEnd < 0)
        {
            GameSparker.MessageWindow.ShowMessage("Error", "Invalid selection range. Please select a polygon again.");
            return;
        }
        
        // Validate selection indices are within bounds
        if (tab.TextEditorSelectionStart >= tab.TextContent.Length || 
            tab.TextEditorSelectionEnd > tab.TextContent.Length ||
            tab.TextEditorSelectionStart >= tab.TextEditorSelectionEnd)
        {
            var err = $"Selection range is out of sync with text content. Start: {tab.TextEditorSelectionStart}, End: {tab.TextEditorSelectionEnd}, Length: {tab.TextContent.Length}";
            GameSparker.MessageWindow.ShowMessage("Parse Error", err);
            if (GameSparker.Writer != null)
                GameSparker.Writer.WriteLine(err, "error");
            
            // Invalidate selection so user is forced to reselect
            tab.TextEditorSelectionStart = -1;
            tab.TextEditorSelectionEnd = -1;
            tab.ShowPolygonEditor = false;
            return;
        }
        
        // Check if content is empty or whitespace (user is removing the element)
        bool editingCollision = tab.EditMode == ModelEditorTab.EditModeEnum.Collision;
        int editedIndex = editingCollision ? tab.SelectedCollisionIndex : tab.SelectedPolygonIndex;
        
        if (string.IsNullOrWhiteSpace(tab.PolygonEditorContent))
        {
            var itemType = editingCollision ? "collision" : "polygon";
            GameSparker.MessageWindow.ShowCustom("Remove Element",
                $"Are you sure you want to remove this {itemType}?\n\nThis will delete {itemType} {editedIndex + 1} from the model.",
                new[] { "Remove", "Cancel" },
                result => {
                    if (result == MessageWindow.MessageResult.Custom1)
                    {
                        // User confirmed removal
                        PerformApplyChanges(tab, editingCollision, editedIndex, removeElement: true);
                    }
                });
            return;
        }
        
        // Normal apply
        PerformApplyChanges(tab, editingCollision, editedIndex, removeElement: false);
    }
    
    private void PerformApplyChanges(ModelEditorTab tab, bool editingCollision, int editedIndex, bool removeElement)
    {
        // Replace the polygon/collision code in the main text content
        var before = tab.TextContent.Substring(0, tab.TextEditorSelectionStart);
        var after = tab.TextContent.Substring(tab.TextEditorSelectionEnd);
        
        tab.TextContent = before + tab.PolygonEditorContent + after;
        tab.TextEditorDirty = true;
        
        // Try to reload the model with the new code
        try
        {
            tab.Object = new EditorObject(new EditorObjectInfo(GameSparker._graphicsDevice, RadParser.ParseRad(tab.TextContent), "editing"));
            tab.PolygonEditorDirty = false;
            
            if (removeElement)
            {
                // Element was removed, close editor and deselect
                tab.ShowPolygonEditor = false;
                if (editingCollision)
                {
                    tab.SelectedCollisionIndex = -1;
                }
                else
                {
                    tab.SelectedPolygonIndex = -1;
                }
                
                if (GameSparker.Writer != null)
                {
                    var itemType = editingCollision ? "Collision" : "Polygon";
                    GameSparker.Writer.WriteLine($"{itemType} {editedIndex + 1} removed successfully", "info");
                }
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
                else
                {
                    tab.SelectedPolygonIndex = editedIndex;
                    // Find the updated polygon in the text
                    JumpToSelectedPolygonInText();
                }
                
                // Keep the editor window open with updated content
                tab.ShowPolygonEditor = true;
                
                if (GameSparker.Writer != null)
                {
                    var itemType = editingCollision ? "Collision" : "Polygon";
                    GameSparker.Writer.WriteLine($"{itemType} {editedIndex + 1} updated successfully", "info");
                }
            }
        }
        catch (Exception ex)
        {
            var err = $"Error parsing updated code:\n{ex.Message}";
            GameSparker.MessageWindow.ShowMessage("Parse Error", err);
            if (GameSparker.Writer != null)
                GameSparker.Writer.WriteLine(err, "error");
        }
    }

    public void ExitModelViewer()
    {
        GameSparker.ExitModelViewer();
    }
    
    public override void RenderAfterSkia()
    {
        base.RenderAfterSkia();
        
        var tab = ActiveTab;
        if (!_isOpen || tab == null || tab.Object == null) return;
        
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        // Set up camera with fixed orientation (looking at origin)
        camera.Position = tab.CameraPosition;
        camera.LookAt = Vector3.Zero; // Always look at origin, not the model position
        
        // Store original transform
        var originalPosition = tab.Object.Position;
        var originalRotation = tab.Object.Rotation;
        
        // Apply our transform to the main model
        tab.Object.Position = tab.ModelPosition;
        // Euler constructor is (yaw, pitch, roll)
        tab.Object.Rotation = new Euler(
            AngleSingle.FromDegrees(tab.ModelRotation.Y),  // Yaw (Y-axis rotation)
            AngleSingle.FromDegrees(-tab.ModelRotation.X), // Pitch (X-axis rotation, negated for correct direction)
            AngleSingle.FromDegrees(tab.ModelRotation.Z)   // Roll (Z-axis rotation)
        );
        
        scene.Objects.Clear();
        scene.Objects.Add(tab.Object);
        
        // Render main model first
        scene.Render(false);
        
        // Render reference car overlay with transparency (rendered separately after main model)
        if (tab.ShowReferenceOverlay && tab.ReferenceCarIndex >= 0 && tab.ReferenceCarIndex < GameSparker.cars[Collection.NFMM].Count)
        {
            // TODO optimize by caching reference car object instead of recreating each frame
            var referenceCar = new Car(GameSparker.cars[Collection.NFMM][tab.ReferenceCarIndex]);
            if (referenceCar != null)
            {
                // Store original state
                var originalRefPosition = referenceCar.Position;
                var originalRefRotation = referenceCar.Rotation;
                var previousBlendState = _graphicsDevice.BlendState;
                var previousDepthState = _graphicsDevice.DepthStencilState;

                // Position reference car at same location as main model
                referenceCar.Position = tab.ModelPosition;
                referenceCar.Rotation = new Euler(
                    AngleSingle.FromDegrees(tab.ModelRotation.Y),
                    AngleSingle.FromDegrees(-tab.ModelRotation.X),
                    AngleSingle.FromDegrees(tab.ModelRotation.Z)
                );
                
                // Enable alpha blending
                _graphicsDevice.BlendState = BlendState.AlphaBlend;
                
                // Clear depth buffer and disable depth testing so reference car always renders in front
                _graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Transparent, 1.0f, 0);
                var depthOff = new DepthStencilState
                {
                    DepthBufferEnable = false,
                    DepthBufferWriteEnable = false
                };
                _graphicsDevice.DepthStencilState = depthOff;
                
                referenceCar.AlphaOverride = tab.ReferenceOpacity;
                
                overlayScene.Objects.Clear();
                overlayScene.Objects.Add(referenceCar);
                overlayScene.Render(false, false);
                
                // Restore states
                _graphicsDevice.BlendState = previousBlendState;
                _graphicsDevice.DepthStencilState = previousDepthState;
                referenceCar.Position = originalRefPosition;
                referenceCar.Rotation = originalRefRotation;
            }
        }
        
        // Render selected polygon overlay with transparency
        if (tab.EditMode == ModelEditorTab.EditModeEnum.Polygon && 
            tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex < tab.Object.Mesh.Polys.Length)
        {
            RenderSelectionOverlay(tab);
        }
        
        // Render selected collision box overlay
        if (tab.EditMode == ModelEditorTab.EditModeEnum.Collision && 
            tab.SelectedCollisionIndex >= 0 && tab.SelectedCollisionIndex < tab.Object.Boxes.Length)
        {
            RenderCollisionSelectionOverlay(camera, tab);
        }
        
        // Restore original transform
        tab.Object.Position = originalPosition;
        tab.Object.Rotation = originalRotation;
    }
    
    private void RenderSelectionOverlay(ModelEditorTab tab)
    {
        if (tab.Object == null || tab.SelectedPolygonIndex < 0) return;
        
        // Create a temporary mesh with only the selected polygon
        var selectedPoly = tab.Object.Mesh.Polys[tab.SelectedPolygonIndex];
        
        // Make it bright cyan/yellow with semi-transparency for visibility
        var highlightPoly = selectedPoly with { 
            Color = new Color3(255, 255, 0),
            PolyType = PolyType.Flat  // Ensure it renders as flat/solid
        };
        
        var overlayPolys = new Rad3dPoly[] { highlightPoly };
        
        // Create a temporary mesh for the overlay
        var overlayMesh = new EditorObject(new EditorObjectInfo(
            GameSparker._graphicsDevice,
            new Rad3d(overlayPolys, false),
            "overlay"
        ));
        
        // Match the main model's transform
        overlayMesh.Position = tab.Object.Position;
        overlayMesh.Rotation = tab.Object.Rotation;
        
        // Save current blend state
        var oldBlendState = GameSparker._graphicsDevice.BlendState;
        var oldDepthStencilState = GameSparker._graphicsDevice.DepthStencilState;
        
        // Enable alpha blending and disable depth write (but keep depth test)
        GameSparker._graphicsDevice.BlendState = BlendState.AlphaBlend;
        var depthRead = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false,  // Don't write to depth, just read
            DepthBufferFunction = CompareFunction.LessEqual
        };
        GameSparker._graphicsDevice.DepthStencilState = depthRead;
        
        // Render the overlay
        overlayScene.Objects.Clear();
        overlayScene.Objects.Add(overlayMesh);
        overlayScene.Render(false, false);
        
        // Restore previous states
        GameSparker._graphicsDevice.BlendState = oldBlendState;
        GameSparker._graphicsDevice.DepthStencilState = oldDepthStencilState;
    }
    
    private void RenderCollisionSelectionOverlay(PerspectiveCamera camera, ModelEditorTab tab)
    {
        if (tab.Object == null || tab.SelectedCollisionIndex < 0) return;
        
        // Create a highlighted collision box mesh for the selected collision
        var selectedBox = tab.Object.Boxes[tab.SelectedCollisionIndex];
        var highlightedBox = selectedBox with { 
            Color = new Color3(255, 255, 0) // Yellow highlight
        };
        
        var highlightBoxes = new[] { highlightedBox };
        var highlightMesh = new CollisionDebugMesh(highlightBoxes);
        
        // Match the main model's transform
        highlightMesh.Position = tab.Object.Position;
        highlightMesh.Rotation = tab.Object.Rotation;
        
        // Render with highlighting
        var oldDevRenderTrackers = GameSparker.devRenderTrackers;
        GameSparker.devRenderTrackers = true;
        highlightMesh.Render(camera, null);
        GameSparker.devRenderTrackers = oldDevRenderTrackers;
    }
    
    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);
        
        camera.Width = width;
        camera.Height = height;
    }
}
