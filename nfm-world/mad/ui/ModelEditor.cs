using ImGuiNET;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using System.Text;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Microsoft.Xna.Framework.Graphics;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using Microsoft.Xna.Framework;

namespace NFMWorld.Mad.UI;

// Class to hold the state for a single model editor tab
public class ModelEditorTab
{
    public string? ModelPath { get; set; }
    public Mesh? Model { get; set; }
    
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
    
    // Text editor highlighting
    public int TextEditorSelectionStart { get; set; } = -1;
    public int TextEditorSelectionEnd { get; set; } = -1;
    
    // Polygon editor window
    public bool ShowPolygonEditor { get; set; } = false;
    public string PolygonEditorContent { get; set; } = "";
    public bool PolygonEditorDirty { get; set; } = false;
    public int PolygonEditorLastSelectedIndex { get; set; } = -1;
    
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
    
    // 3D
    public static PerspectiveCamera camera = new();
    public static OrthoCamera lightCamera = new()
    {
        Width = 8192,
        Height = 8192
    };
    
    public ModelEditorPhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
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
        
        // render an empty stage for 1 frame to init shaders
        _modelViewerStage = new Stage("empty", GameSparker._graphicsDevice);
        World.Snap = new Color3(100, 100, 100);
        
        camera.Position = new Vector3(0, -800, -800);
        camera.LookAt = Vector3.Zero;
        lightCamera.Position = camera.Position + new Vector3(0, -5000, 0);
        lightCamera.LookAt = camera.Position + new Vector3(1f, 0, 0);
        
        camera.OnBeforeRender();
        lightCamera.OnBeforeRender();
        
        GameSparker._graphicsDevice.BlendState = BlendState.Opaque;
        GameSparker._graphicsDevice.DepthStencilState = DepthStencilState.Default;
        
        _modelViewerStage.Render(camera, lightCamera, false);
        
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
        LoadModel(filePath);
        
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
        return @"//NFM Model File
//Created with NFM World Model Editor

// Add your model data here
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
        
        // Try to parse the model, but keep the file loaded even if it fails
        try
        {
            tab.Model = new Mesh(GameSparker._graphicsDevice, radContent);
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
            tab.Model = null;
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
        // Position camera in orbit around the origin (not following model height)
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
    
        if (!_isOpen) return;
        _mouseX = x;
        _mouseY = y;
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse)
    {
        base.MousePressed(x, y, imguiWantsMouse);
        if (imguiWantsMouse) return;
        if (!_isOpen) return;
    }
    
    private void ProcessMouseClick()
    {
        var tab = ActiveTab;
        if (!MouseDownThisFrame || tab == null || tab.Model == null) return;
        
        var io = ImGui.GetIO();
        
        // Don't process clicks if ImGui wants the mouse
        if (io.WantCaptureMouse) return;
        
        // Perform ray casting to find clicked polygon
        var pickedIndex = PerformRayPicking(_mouseX, _mouseY, tab);
        
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
    
    private int PerformRayPicking(int screenX, int screenY, ModelEditorTab tab)
    {
        if (tab.Model == null) return -1;
        
        var viewport = GameSparker._graphicsDevice.Viewport;
        
        // Set up the model's transform exactly as RenderModel does
        var originalPosition = tab.Model.Position;
        var originalRotation = tab.Model.Rotation;
        
        tab.Model.Position = tab.ModelPosition;
        tab.Model.Rotation = new Euler(
            AngleSingle.FromDegrees(tab.ModelRotation.Y),  // Yaw
            AngleSingle.FromDegrees(-tab.ModelRotation.X), // Pitch (negated)
            AngleSingle.FromDegrees(tab.ModelRotation.Z)   // Roll
        );
        
        // Get the ACTUAL MatrixWorld that will be used for rendering
        var modelWorld = tab.Model.MatrixWorld;
        
        // Restore transform
        tab.Model.Position = originalPosition;
        tab.Model.Rotation = originalRotation;
        
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
            new XnaVector3(screenX, screenY, 0f),
            projection,
            view,
            XnaMatrix.Identity
        );
        
        var farPoint = viewport.Unproject(
            new XnaVector3(screenX, screenY, 1f),
            projection,
            view,
            XnaMatrix.Identity
        );
        
        var rayOrigin = nearPoint;
        var rayDirection = XnaVector3.Normalize(farPoint - nearPoint);
        
        // Test against all polygons
        float closestDistance = float.MaxValue;
        int closestPolyIndex = -1;
        
        for (int i = 0; i < tab.Model.Polys.Length; i++)
        {
            var poly = tab.Model.Polys[i];
            var triangulation = tab.Model.Triangulation[i];
            
            // Test each triangle in this polygon
            for (int t = 0; t < triangulation.Triangles.Length; t += 3)
            {
                var i0 = triangulation.Triangles[t];
                var i1 = triangulation.Triangles[t + 1];
                var i2 = triangulation.Triangles[t + 2];
                
                // Transform vertices by the model's world matrix
                var v0 = XnaVector3.Transform(poly.Points[i0].ToXna(), modelWorld);
                var v1 = XnaVector3.Transform(poly.Points[i1].ToXna(), modelWorld);
                var v2 = XnaVector3.Transform(poly.Points[i2].ToXna(), modelWorld);
                
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
        XnaVector3 rayOrigin,
        XnaVector3 rayDirection,
        XnaVector3 v0,
        XnaVector3 v1,
        XnaVector3 v2,
        out float distance)
    {
        distance = 0;
        const float EPSILON = 0.000001f;  // Smaller epsilon for better accuracy
        
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var h = XnaVector3.Cross(rayDirection, edge2);
        var a = XnaVector3.Dot(edge1, h);
        
        // Check if ray is parallel to triangle (with smaller tolerance)
        if (a > -EPSILON && a < EPSILON)
            return false;
        
        var f = 1.0f / a;
        var s = rayOrigin - v0;
        var u = f * XnaVector3.Dot(s, h);
        
        // Allow slightly outside bounds for edge cases
        if (u < -EPSILON || u > 1.0f + EPSILON)
            return false;
        
        var q = XnaVector3.Cross(s, edge1);
        var v = f * XnaVector3.Dot(rayDirection, q);
        
        // Allow slightly outside bounds for edge cases
        if (v < -EPSILON || u + v > 1.0f + EPSILON)
            return false;
        
        distance = f * XnaVector3.Dot(edge2, q);
        
        // Only accept intersections in front of the ray
        return distance > EPSILON;
    }
    
    private void FlipSelectedPolygonVertexOrder()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedPolygonIndex < 0 || tab.Model == null) return;
        
        var poly = tab.Model.Polys[tab.SelectedPolygonIndex];
        Array.Reverse(poly.Points);
        
        // Rebuild the mesh with the flipped polygon
        tab.Model.RebuildMesh();
        
        // Update the text content to reflect the change
        UpdateTextContentFromModel(tab);
    }
    
    private void RemoveSelectedPolygon()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedPolygonIndex < 0 || tab.Model == null) return;
        
        var polyList = tab.Model.Polys.ToList();
        polyList.RemoveAt(tab.SelectedPolygonIndex);
        tab.Model.Polys = polyList.ToArray();
        
        // Rebuild the mesh
        tab.Model.RebuildMesh();
        
        // Update the text content to reflect the change
        UpdateTextContentFromModel(tab);
        
        // Clear selection
        tab.SelectedPolygonIndex = -1;
    }
    
    private void JumpToSelectedPolygonInText()
    {
        var tab = ActiveTab;
        if (tab == null || tab.SelectedPolygonIndex < 0 || tab.Model == null) return;
        
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
    
    private void UpdateTextContentFromModel(ModelEditorTab tab)
    {
        if (tab.Model == null) return;
        
        // Don't regenerate from scratch - this would lose comments and formatting
        // Instead, this method should only be called when we've done structural changes
        // like removing/flipping polygons, and we accept losing the original formatting
        
        var sb = new StringBuilder();
        sb.AppendLine("// Modified in Model Editor");
        sb.AppendLine();
        
        foreach (var poly in tab.Model.Polys)
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
        
        // Process mouse clicks for polygon selection
        ProcessMouseClick();
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
                            tab.Model = new Mesh(GameSparker._graphicsDevice, tab.TextContent);
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
                            tab.Model = new Mesh(GameSparker._graphicsDevice, tab.TextContent);
                            ResetTabView(tab);
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
            
            // Polygon selection info and controls (always visible when model is loaded)
            if (tab.Model != null)
            {
                ImGui.Separator();
                
                if (tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex < tab.Model.Polys.Length)
                {
                    ImGui.Text($"[ Piece {tab.SelectedPolygonIndex + 1} of {tab.Model.Polys.Length} selected ]");
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
            if (ImGui.BeginTabItem("3D Controls"))
            {
                var activeTab = ActiveTab;
                if (activeTab != null)
                {
                    ImGui.Columns(2, "ControlColumns", false);
                    
                    // Left column
                    ImGui.Text("Keyboard Controls:");
                    ImGui.Spacing();
                    ImGui.Text("W/S: Move Forward/Back");
                    ImGui.Text("A/D: Rotate Yaw");
                    ImGui.Text("Up/Down: Rotate Pitch");
                    ImGui.Text("Left/Right: Rotate Roll");
                    ImGui.Text("+/-: Move Up/Down");
                    
                    ImGui.NextColumn();
                    
                    // Right column
                    float rotX = activeTab.ModelRotation.X;
                    float rotY = activeTab.ModelRotation.Y;
                    float rotZ = activeTab.ModelRotation.Z;
                    float posY = activeTab.ModelPosition.Y;
                    
                    ImGui.Text("Rotation Controls:");
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
                    
                    ImGui.Spacing();
                    
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
                ImGui.Text("Scale & Align not yet implemented");
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Wheels"))
            {
                ImGui.Text("Wheel editing not yet implemented");
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Stats & Class"))
            {
                ImGui.Text("Stats & Class not yet implemented");
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Physics"))
            {
                ImGui.Text("Physics not yet implemented");
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Test Drive"))
            {
                ImGui.Text("Test Drive not yet implemented");
                ImGui.EndTabItem();
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
                            
                            if (ImGui.Selectable($"  {fileName}", isCurrentModel))
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
                            
                            if (ImGui.Selectable($"  {fileName}", isCurrentModel))
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
        
        // Check if user selected a different polygon while editor is open
        if (tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex != tab.PolygonEditorLastSelectedIndex)
        {
            // Update the editor content to the newly selected polygon
            JumpToSelectedPolygonInText();
            return; // JumpToSelectedPolygonInText will refresh the window
        }
        
        var io = ImGui.GetIO();
        var displaySize = io.DisplaySize;
        
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(displaySize.X / 2 - 300, displaySize.Y / 2 - 250), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(600, 400), ImGuiCond.FirstUseEver);
        
        bool isOpen = tab.ShowPolygonEditor;
        if (ImGui.Begin($"Edit Polygon", ref isOpen, ImGuiWindowFlags.None))
        {
            ImGui.Text($"Editing polygon {tab.SelectedPolygonIndex + 1} of {tab.Model?.Polys.Length ?? 0}");
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
                ImGui.SetTooltip("Insert special polygon flags");
            }
            
            if (ImGui.Button("gr(-18)"))
            {
                Insert("gr(-18)");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("decal"))
            {
                Insert("decal");
            }
            
            // ImGui.SameLine();
            
            // if (ImGui.Button("TODO poly fx"))
            // {
            //     Insert("something_else");
            // }
            
            // ImGui.SameLine();
            
            // if (ImGui.Button("TODO poly fx"))
            // {
            //     Insert("fs(-1)");
            // }
            
            ImGui.Spacing();
            ImGui.Separator();
            
            // Action buttons
            var buttonWidth = (contentRegionAvail.X - 10f) / 2f; // Split width for 2 buttons with spacing
            
            
            
            if (ImGui.Button("Apply Changes", new System.Numerics.Vector2(buttonWidth, 30)))
            {
                ApplyPolygonEditorChanges();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(buttonWidth, 30)))
            {
                tab.ShowPolygonEditor = false;
            }

            if (tab.PolygonEditorDirty)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "(Modified)");
            }
        }
        
        tab.ShowPolygonEditor = isOpen;
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
        if (tab == null || tab.TextEditorSelectionStart < 0 || tab.TextEditorSelectionEnd < 0) return;
        
        // Replace the polygon code in the main text content
        var before = tab.TextContent.Substring(0, tab.TextEditorSelectionStart);
        var after = tab.TextContent.Substring(tab.TextEditorSelectionEnd);
        
        tab.TextContent = before + tab.PolygonEditorContent + after;
        tab.TextEditorDirty = true;
        
        // Try to reload the model with the new code
        try
        {
            tab.Model = new Mesh(GameSparker._graphicsDevice, tab.TextContent);
            tab.PolygonEditorDirty = false;
            
            if (GameSparker.Writer != null)
                GameSparker.Writer.WriteLine($"Polygon {tab.SelectedPolygonIndex + 1} updated successfully", "info");
        }
        catch (Exception ex)
        {
            var err = $"Error parsing updated polygon:\n{ex.Message}";
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
        if (!_isOpen || tab == null || tab.Model == null) return;
        
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        // Set up camera with fixed orientation (looking at origin)
        camera.Position = tab.CameraPosition;
        camera.LookAt = Vector3.Zero; // Always look at origin, not the model position
        
        // Set up light camera (positioned above and to the side for good lighting)
        lightCamera.Position = camera.Position + new Vector3(0, -5000, 0);
        lightCamera.LookAt = camera.Position + new Vector3(1f, 0, 0);
        
        // Store original transform
        var originalPosition = tab.Model.Position;
        var originalRotation = tab.Model.Rotation;
        
        // Apply our transform
        tab.Model.Position = tab.ModelPosition;
        // Euler constructor is (yaw, pitch, roll)
        tab.Model.Rotation = new Euler(
            AngleSingle.FromDegrees(tab.ModelRotation.Y),  // Yaw (Y-axis rotation)
            AngleSingle.FromDegrees(-tab.ModelRotation.X), // Pitch (X-axis rotation, negated for correct direction)
            AngleSingle.FromDegrees(tab.ModelRotation.Z)   // Roll (Z-axis rotation)
        );
        
        // TODO maybe cache this scene instead of making it every time
        var scene = new Scene(GameSparker._graphicsDevice, [tab.Model], camera, lightCamera);
        scene.Render(false);
        
        // Render selected polygon overlay with transparency
        if (tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex < tab.Model.Polys.Length)
        {
            RenderSelectionOverlay(camera, lightCamera, tab);
        }
        
        // Restore original transform
        tab.Model.Position = originalPosition;
        tab.Model.Rotation = originalRotation;
    }
    
    private void RenderSelectionOverlay(PerspectiveCamera camera, Camera lightCamera, ModelEditorTab tab)
    {
        if (tab.Model == null || tab.SelectedPolygonIndex < 0) return;
        
        // Create a temporary mesh with only the selected polygon
        var selectedPoly = tab.Model.Polys[tab.SelectedPolygonIndex];
        
        // Make it bright cyan/yellow with semi-transparency for visibility
        var highlightPoly = selectedPoly with { 
            Color = new Color3(255, 255, 0),
            PolyType = PolyType.Flat  // Ensure it renders as flat/solid
        };
        
        var overlayPolys = new Rad3dPoly[] { highlightPoly };
        
        // Create a temporary mesh for the overlay
        var overlayMesh = new Mesh(
            GameSparker._graphicsDevice,
            new Rad3d(overlayPolys, false)
        );
        
        // Match the main model's transform
        overlayMesh.Position = tab.Model.Position;
        overlayMesh.Rotation = tab.Model.Rotation;
        
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
        overlayMesh.Render(camera, lightCamera, false);
        
        // Restore previous states
        GameSparker._graphicsDevice.BlendState = oldBlendState;
        GameSparker._graphicsDevice.DepthStencilState = oldDepthStencilState;
    }
    
    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);
        
        camera.Width = width;
        camera.Height = height;
    }
}
