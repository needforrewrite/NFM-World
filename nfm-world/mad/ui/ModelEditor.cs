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

public class ModelEditorPhase : BasePhase
{
    private readonly GraphicsDevice _graphicsDevice;
    private bool _isOpen = false;
    private string[] _userModelNames = [];
    private string? _currentModelPath = null;
    private Mesh? _currentModel = null;
    private Stage? _modelViewerStage;
    
    // UI state
    private bool _showNewModelDialog = false;
    private bool _showLoadDialog = false;
    private bool _isCreatingCar = true; // true = car, false = stage
    private string _newModelName = "";
    private int _selectedReferenceModel = 0;
    private bool _importReference = false;
    
    // Text editor state
    private string _modelTextContent = "";
    private bool _textEditorDirty = false;
    private bool _textEditorExpanded = false;
    
    // Camera/view controls
    private Vector3 _modelRotation = new Vector3(0, 0, 0); // Pitch, Yaw, Roll
    private Vector3 _modelPosition = new Vector3(0, 0, 0); // X, Y, Z position
    private float _cameraDistance = 800f;
    private Vector3 _cameraPosition = new Vector3(0, 0, -800);
    private float _cameraYaw = 0f;
    private float _cameraPitch = -10f;
    
    // Control states
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
    
    // Mouse and selection state
    private int _mouseX;
    private int _mouseY;
    private int _selectedPolygonIndex = -1; // -1 means no selection
    
    // Text editor highlighting
    private int _textEditorSelectionStart = -1;
    private int _textEditorSelectionEnd = -1;
    
    // Polygon editor window
    private bool _showPolygonEditor = false;
    private string _polygonEditorContent = "";
    private bool _polygonEditorDirty = false;
    private int _polygonEditorLastSelectedIndex = -1; // Track which polygon is being edited
    
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
        
        _currentModel = null;
        _currentModelPath = null;
        RefreshUserModels();
        
        ResetView();
    }

    public override void Exit()
    {
        base.Exit();

        _isOpen = false;
        _currentModel = null;
        _currentModelPath = null;
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
    
    private void LoadModel(string filePath)
    {
        try
        {
            var radContent = System.IO.File.ReadAllText(filePath);
            _currentModelPath = filePath;
            _modelTextContent = radContent;
            _textEditorDirty = false;
            
            // Try to parse the model, but keep the file loaded even if it fails
            try
            {
                _currentModel = new Mesh(GameSparker._graphicsDevice, radContent);
                ResetView();
            }
            catch (Exception parseEx)
            {
                var err = $"Error parsing model: {parseEx.Message}\n\nFile loaded in text editor for correction.";
                GameSparker.MessageWindow.ShowMessage("Parse Error", err);
                if (GameSparker.Writer != null)
                {
                    GameSparker.Writer.WriteLine($"Parse error in {Path.GetFileName(filePath)}: {parseEx.Message}", "error");
                }
                _currentModel = null;
            }
        }
        catch (Exception ex)
        {
            var err = $"Error loading file: {ex.Message}";
            GameSparker.MessageWindow.ShowMessage("Error", err);
            if (GameSparker.Writer != null)
            {
                GameSparker.Writer.WriteLine(err, "error");
            }
            _currentModel = null;
            _currentModelPath = null;
        }
    }
    
    private void ResetView()
    {
        _modelRotation = new Vector3(0, 0, 0);
        _modelPosition = new Vector3(0, 0, 0);
        _cameraDistance = 800f;
        _cameraYaw = 0f;
        _cameraPitch = 10f;
        UpdateCameraPosition();
    }
    
    private void UpdateCameraPosition()
    {
        // Position camera in orbit around the origin (not following model height)
        // Camera orbits at the specified distance
        var yawRad = MathUtil.DegreesToRadians(_cameraYaw);
        var pitchRad = MathUtil.DegreesToRadians(_cameraPitch);
        
        var x = _cameraDistance * (float)(Math.Cos(pitchRad) * Math.Sin(yawRad));
        var y = -_cameraDistance * (float)Math.Sin(pitchRad);
        var z = _cameraDistance * (float)(Math.Cos(pitchRad) * Math.Cos(yawRad));
        
        _cameraPosition = new Vector3(x, y, -z);
    }
    
    public void HandleKeyPress(Keys key)
    {
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
    
    public void HandleKeyRelease(Keys key)
    {
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
    
    public void HandleMouseMove(int x, int y)
    {
        if (!_isOpen) return;
        _mouseX = x;
        _mouseY = y;
    }
    
    public void HandleMouseDown(int x, int y)
    {
        if (!_isOpen) return;
    }
    
    private void ProcessMouseClick()
    {
        if (!MouseDownThisFrame || _currentModel == null) return;
        
        var io = ImGui.GetIO();
        
        // Don't process clicks if ImGui wants the mouse
        if (io.WantCaptureMouse) return;
        
        // Perform ray casting to find clicked polygon
        var pickedIndex = PerformRayPicking(_mouseX, _mouseY);
        
        if (pickedIndex >= 0)
        {
            _selectedPolygonIndex = pickedIndex;
        }
        else
        {
            // Clicked on background, deselect
            _selectedPolygonIndex = -1;
        }
    }
    
    private int PerformRayPicking(int screenX, int screenY)
    {
        if (_currentModel == null) return -1;
        
        var viewport = GameSparker._graphicsDevice.Viewport;
        
        // Set up the model's transform exactly as RenderModel does
        var originalPosition = _currentModel.Position;
        var originalRotation = _currentModel.Rotation;
        
        _currentModel.Position = _modelPosition;
        _currentModel.Rotation = new Euler(
            AngleSingle.FromDegrees(_modelRotation.Y),  // Yaw
            AngleSingle.FromDegrees(-_modelRotation.X), // Pitch (negated)
            AngleSingle.FromDegrees(_modelRotation.Z)   // Roll
        );
        
        // Get the ACTUAL MatrixWorld that will be used for rendering
        var modelWorld = _currentModel.MatrixWorld;
        
        // Restore transform
        _currentModel.Position = originalPosition;
        _currentModel.Rotation = originalRotation;
        
        // Set up camera exactly as RenderModel does
        // Use GameSparker.camera's actual Width/Height (not viewport, which might differ)
        var tempCamera = new PerspectiveCamera
        {
            Position = _cameraPosition,
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
        
        for (int i = 0; i < _currentModel.Polys.Length; i++)
        {
            var poly = _currentModel.Polys[i];
            var triangulation = _currentModel.Triangulation[i];
            
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
        if (_selectedPolygonIndex < 0 || _currentModel == null) return;
        
        var poly = _currentModel.Polys[_selectedPolygonIndex];
        Array.Reverse(poly.Points);
        
        // Rebuild the mesh with the flipped polygon
        _currentModel.RebuildMesh();
        
        // Update the text content to reflect the change
        UpdateTextContentFromModel();
    }
    
    private void RemoveSelectedPolygon()
    {
        if (_selectedPolygonIndex < 0 || _currentModel == null) return;
        
        var polyList = _currentModel.Polys.ToList();
        polyList.RemoveAt(_selectedPolygonIndex);
        _currentModel.Polys = polyList.ToArray();
        
        // Rebuild the mesh
        _currentModel.RebuildMesh();
        
        // Update the text content to reflect the change
        UpdateTextContentFromModel();
        
        // Clear selection
        _selectedPolygonIndex = -1;
    }
    
    private void JumpToSelectedPolygonInText()
    {
        if (_selectedPolygonIndex < 0 || _currentModel == null) return;
        
        // // Expand the text editor if not already visible
        // if (!_textEditorExpanded)
        // {
        //     _textEditorExpanded = true;
        // }
        
        // Find the polygon in the text by searching for <p> tags and counting
        int polygonCount = 0;
        int selectionStart = -1;
        int selectionEnd = -1;
        
        for (int i = 0; i < _modelTextContent.Length; i++)
        {
            // Check if we're at the start of a <p> tag
            if (i + 3 <= _modelTextContent.Length && 
                _modelTextContent.Substring(i, 3) == "<p>")
            {
                if (polygonCount == _selectedPolygonIndex)
                {
                    // Found the start of our target polygon
                    selectionStart = i;
                    
                    // Now find the closing </p> tag
                    int searchPos = i + 3;
                    while (searchPos + 4 <= _modelTextContent.Length)
                    {
                        if (_modelTextContent.Substring(searchPos, 4) == "</p>")
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
                        while (searchPos + 3 <= _modelTextContent.Length)
                        {
                            if (_modelTextContent.Substring(searchPos, 3) == "<p>")
                            {
                                selectionEnd = searchPos;
                                break;
                            }
                            searchPos++;
                        }
                        if (selectionEnd == -1)
                        {
                            selectionEnd = _modelTextContent.Length;
                        }
                    }
                    
                    break;
                }
                polygonCount++;
            }
        }
        
        // Store selection range for future use (currently just for debugging)
        _textEditorSelectionStart = selectionStart;
        _textEditorSelectionEnd = selectionEnd;
        
        // Extract the polygon code and open the editor window
        if (selectionStart >= 0 && selectionEnd >= 0 && selectionEnd <= _modelTextContent.Length)
        {
            _polygonEditorContent = _modelTextContent.Substring(selectionStart, selectionEnd - selectionStart);
            _showPolygonEditor = true;
            _polygonEditorDirty = false;
            _polygonEditorLastSelectedIndex = _selectedPolygonIndex; // Remember which polygon we're editing
        }
        
        // Debug output
        if (GameSparker.Writer != null && selectionStart >= 0)
        {
            GameSparker.Writer.WriteLine($"Polygon {_selectedPolygonIndex + 1}: selection range {selectionStart}-{selectionEnd}", "info");
        }
    }
    
    private void UpdateTextContentFromModel()
    {
        if (_currentModel == null) return;
        
        // Don't regenerate from scratch - this would lose comments and formatting
        // Instead, this method should only be called when we've done structural changes
        // like removing/flipping polygons, and we accept losing the original formatting
        
        var sb = new StringBuilder();
        sb.AppendLine("// Modified in Model Editor");
        sb.AppendLine();
        
        foreach (var poly in _currentModel.Polys)
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
        
        _modelTextContent = sb.ToString();
        _textEditorDirty = true;
    }

    public override void GameTick()
    {
        base.GameTick();

        if (!_isOpen) return;
        
        // Apply movement
        if (_moveForward)
            _modelPosition.Z += HEIGHT_SPEED;
        if (_moveBackward)
            _modelPosition.Z -= HEIGHT_SPEED;
        
        // Apply yaw rotation (A/D)
        if (_rotateLeft)
            _modelRotation.Y -= ROTATION_SPEED;
        if (_rotateRight)
            _modelRotation.Y += ROTATION_SPEED;
        
        // Apply pitch rotation (Up/Down arrows)
        if (_rotatePitchUp)
            _modelRotation.X -= ROTATION_SPEED;
        if (_rotatePitchDown)
            _modelRotation.X += ROTATION_SPEED;
        
        // Apply roll rotation (Left/Right arrows)
        if (_rotateRollLeft)
            _modelRotation.Z -= ROTATION_SPEED;
        if (_rotateRollRight)
            _modelRotation.Z += ROTATION_SPEED;
        
        // Apply height changes (+/-)
        if (_raiseUp)
            _modelPosition.Y += HEIGHT_SPEED;
        if (_lowerDown)
            _modelPosition.Y -= HEIGHT_SPEED;
        
        UpdateCameraPosition();
        
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
                    if (_currentModelPath != null)
                    {
                        if (_textEditorDirty)
                        {
                            GameSparker.MessageWindow.ShowCustom("Unsaved changes", "Are you sure you want to exit without saving first?",
                                new[] { "Save", "Don't Save", "Cancel" },
                                result => {
                                    if (result == MessageWindow.MessageResult.Custom1) {
                                        System.IO.File.WriteAllText(_currentModelPath, _modelTextContent);
                                        ExitModelViewer();
                                    }
                                    if (result == MessageWindow.MessageResult.Custom2) {
                                        ExitModelViewer();
                                    }
                            });
                        } else {
                            ExitModelViewer();
                        }
                    } else {
                        ExitModelViewer();
                    }
                }
                
                ImGui.EndMenu();
            }
            
            ImGui.EndMainMenuBar();
        }
        
        var menuBarHeight = ImGui.GetFrameHeight();
        
        // Second toolbar layer - always visible when model is loaded
        float toolbarHeight = 0;
        if (_currentModelPath != null)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, menuBarHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, 0));
            ImGui.Begin("##Toolbar", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            
            if (ImGui.Button(_textEditorExpanded ? "Hide Code Editor" : "Show Code Editor"))
            {
                _textEditorExpanded = !_textEditorExpanded;
            }
            
            ImGui.SameLine();
            ImGui.Text($"Editing: {Path.GetFileName(_currentModelPath)}");
            
            if (_textEditorExpanded)
            {
                ImGui.SameLine();
                
                if (ImGui.Button("Save"))
                {
                    try
                    {
                        System.IO.File.WriteAllText(_currentModelPath, _modelTextContent);
                        _textEditorDirty = false;
                        // Reload model
                        _currentModel = new Mesh(GameSparker._graphicsDevice, _modelTextContent);
                    }
                    catch (Exception ex)
                    {
                        var err = $"Error saving/reloading model:\n{ex.Message}";
                        GameSparker.MessageWindow.ShowMessage("Error", err);
                        GameSparker.Writer.WriteLine(err, "error");
                    }
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Save & Preview"))
                {
                    try
                    {
                        System.IO.File.WriteAllText(_currentModelPath, _modelTextContent);
                        _textEditorDirty = false;
                        // Reload model
                        _currentModel = new Mesh(GameSparker._graphicsDevice, _modelTextContent);
                        ResetView();
                        _textEditorExpanded = !_textEditorExpanded;
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
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = _currentModelPath,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
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
                ImGui.Text(_textEditorDirty ? "(Modified)" : "");
            }
            
            // Polygon selection info and controls (always visible when model is loaded)
            if (_currentModel != null)
            {
                ImGui.Separator();
                
                if (_selectedPolygonIndex >= 0 && _selectedPolygonIndex < _currentModel.Polys.Length)
                {
                    ImGui.Text($"[ Piece {_selectedPolygonIndex + 1} of {_currentModel.Polys.Length} ({_currentModel.Polys.Length}) selected ]");
                    ImGui.SameLine();
                    
                    if (ImGui.Button("Edit Polygon"))
                    {
                        JumpToSelectedPolygonInText();
                    }
                    ImGui.SameLine();
                    
                    if (ImGui.Button("Flip Vertex Order"))
                    {
                        FlipSelectedPolygonVertexOrder();
                    }
                    ImGui.SameLine();
                    
                    if (ImGui.Button("Remove Polygon"))
                    {
                        RemoveSelectedPolygon();
                    }
                    ImGui.SameLine();
                    
                    if (ImGui.Button("X"))
                    {
                        _selectedPolygonIndex = -1; // Deselect
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
        var showTextEditorPanel = _currentModelPath != null && _textEditorExpanded;
        float topPanelHeight = showTextEditorPanel ? displaySize.Y * 0.6f : 0;
        
        if (showTextEditorPanel)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, menuBarHeight + toolbarHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, topPanelHeight - toolbarHeight));
            ImGui.Begin("##TextEditorPanel", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            
            // Text editor
            var editorHeight = topPanelHeight - toolbarHeight - 10;
            
            var flags = ImGuiInputTextFlags.AllowTabInput;
            var textChanged = ImGui.InputTextMultiline("##TextEditor", ref _modelTextContent, 200000, 
                new System.Numerics.Vector2(-1, -1), flags);
                
            if (textChanged)
            {
                _textEditorDirty = true;
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
                float rotX = _modelRotation.X;
                float rotY = _modelRotation.Y;
                float rotZ = _modelRotation.Z;
                float posY = _modelPosition.Y;
                
                ImGui.Text("Rotation Controls:");
                if (ImGui.SliderFloat("X (Pitch)", ref rotX, -180f, 180f))
                    _modelRotation.X = rotX;
                if (ImGui.SliderFloat("Y (Yaw)", ref rotY, -180f, 180f))
                    _modelRotation.Y = rotY;
                if (ImGui.SliderFloat("Z (Roll)", ref rotZ, -180f, 180f))
                    _modelRotation.Z = rotZ;
                
                if (ImGui.SliderFloat("Height", ref posY, -500f, 500f))
                {
                    _modelPosition.Y = posY;
                }
                
                if (ImGui.SliderFloat("Camera Distance", ref _cameraDistance, 300f, 5000f))
                {
                    UpdateCameraPosition();
                }
                
                ImGui.Spacing();
                
                if (ImGui.Button("Reset View"))
                {
                    ResetView();
                }
                
                ImGui.Columns(1);
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
            ImGui.Begin("Load Model", ref _showLoadDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            
            ImGui.Text("Select a model to load:");
            ImGui.Separator();
            ImGui.Spacing();
            
            // Create a child window for the file list
            if (ImGui.BeginChild("FileList", new System.Numerics.Vector2(480, 300), (ImGuiChildFlags)1, ImGuiWindowFlags.NoScrollbar))
            {
                // Show cars folder
                if (ImGui.CollapsingHeader("cars", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var carsPath = "./data/models/user/cars";
                    if (Directory.Exists(carsPath))
                    {
                        foreach (var file in Directory.GetFiles(carsPath, "*.rad"))
                        {
                            var fileName = Path.GetFileName(file);
                            var isCurrentModel = _currentModelPath != null && 
                                _currentModelPath.EndsWith(file.Replace('/', '\\'));
                            
                            if (ImGui.Selectable($"  {fileName}", isCurrentModel))
                            {
                                LoadModel(file);
                                _showLoadDialog = false;
                            }
                        }
                    }
                }
                
                // Show stage folder
                if (ImGui.CollapsingHeader("stage", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var stagePath = "./data/models/user/stage";
                    if (Directory.Exists(stagePath))
                    {
                        foreach (var file in Directory.GetFiles(stagePath, "*.rad"))
                        {
                            var fileName = Path.GetFileName(file);
                            var isCurrentModel = _currentModelPath != null && 
                                _currentModelPath.EndsWith(file.Replace('/', '\\'));
                            
                            if (ImGui.Selectable($"  {fileName}", isCurrentModel))
                            {
                                LoadModel(file);
                                _showLoadDialog = false;
                            }
                        }
                    }
                }
                
                ImGui.EndChild();
            }
            
            ImGui.Spacing();
            
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(480, 30)))
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
        if (!_showPolygonEditor) return;
        
        // Check if user selected a different polygon while editor is open
        if (_selectedPolygonIndex >= 0 && _selectedPolygonIndex != _polygonEditorLastSelectedIndex)
        {
            // Update the editor content to the newly selected polygon
            JumpToSelectedPolygonInText();
            return; // JumpToSelectedPolygonInText will refresh the window
        }
        
        var io = ImGui.GetIO();
        var displaySize = io.DisplaySize;
        
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(displaySize.X / 2 - 300, displaySize.Y / 2 - 250), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(100, 400), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin($"Edit Polygon", ref _showPolygonEditor, ImGuiWindowFlags.None))
        {
            ImGui.Text($"Editing polygon {_selectedPolygonIndex + 1} of {_currentModel?.Polys.Length ?? 0}");
            ImGui.Separator();
            
            // Calculate available space for text editor
            var contentRegionAvail = ImGui.GetContentRegionAvail();
            
            // Reserve space for the bottom controls (special flags + buttons)
            var bottomControlsHeight = 120f; // Space for flags section + buttons
            var textEditorHeight = contentRegionAvail.Y - bottomControlsHeight;
            
            // Main text editor
            var flags = ImGuiInputTextFlags.AllowTabInput;
            var textChanged = ImGui.InputTextMultiline("##PolygonCode", ref _polygonEditorContent, 50000,
                new System.Numerics.Vector2(-1, textEditorHeight), flags);
            
            if (textChanged)
            {
                _polygonEditorDirty = true;
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
                _showPolygonEditor = false;
            }

            if (_polygonEditorDirty)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "(Modified)");
            }
        }
        
        ImGui.End();
    }
    
    private void Insert(string command)
    {
        // Find the c(...) line in the polygon editor content
        var lines = _polygonEditorContent.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("c(") && trimmed.EndsWith(")"))
            {
                // Insert the command on the next line
                lines[i] = lines[i] + "\n" + command;
                _polygonEditorContent = string.Join("\n", lines);
                _polygonEditorDirty = true;
                break;
            }
        }
    }
    
    private void ApplyPolygonEditorChanges()
    {
        if (_textEditorSelectionStart < 0 || _textEditorSelectionEnd < 0) return;
        
        // Replace the polygon code in the main text content
        var before = _modelTextContent.Substring(0, _textEditorSelectionStart);
        var after = _modelTextContent.Substring(_textEditorSelectionEnd);
        
        _modelTextContent = before + _polygonEditorContent + after;
        _textEditorDirty = true;
        
        // Try to reload the model with the new code
        try
        {
            _currentModel = new Mesh(GameSparker._graphicsDevice, _modelTextContent);
            //_showPolygonEditor = false;
            _polygonEditorDirty = false;
            
            GameSparker.Writer.WriteLine($"Polygon {_selectedPolygonIndex + 1} updated successfully", "info");
        }
        catch (Exception ex)
        {
            var err = $"Error parsing updated polygon:\n{ex.Message}";
            GameSparker.MessageWindow.ShowMessage("Parse Error", err);
            GameSparker.Writer.WriteLine(err, "error");
        }
    }

    public void ExitModelViewer()
    {
        _textEditorDirty = false;
        _showPolygonEditor = false;
        GameSparker.ExitModelViewer();
    }
    
    public override void RenderAfterSkia()
    {
        base.RenderAfterSkia();
        
        if (!_isOpen || _currentModel == null) return;
        
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        // Set up camera with fixed orientation (looking at origin)
        camera.Position = _cameraPosition;
        camera.LookAt = Vector3.Zero; // Always look at origin, not the model position
        
        // Set up light camera (positioned above and to the side for good lighting)
        lightCamera.Position = camera.Position + new Vector3(0, -5000, 0);
        lightCamera.LookAt = camera.Position + new Vector3(1f, 0, 0);
        
        // Store original transform
        var originalPosition = _currentModel.Position;
        var originalRotation = _currentModel.Rotation;
        
        // Apply our transform
        _currentModel.Position = _modelPosition;
        // Euler constructor is (yaw, pitch, roll)
        _currentModel.Rotation = new Euler(
            AngleSingle.FromDegrees(_modelRotation.Y),  // Yaw (Y-axis rotation)
            AngleSingle.FromDegrees(-_modelRotation.X), // Pitch (X-axis rotation, negated for correct direction)
            AngleSingle.FromDegrees(_modelRotation.Z)   // Roll (Z-axis rotation)
        );
        
        // TODO maybe cache this scene instead of making it every time
        var scene = new Scene(GameSparker._graphicsDevice, [_currentModel], camera, lightCamera);
        scene.Render(false);
        
        // Render selected polygon overlay with transparency
        if (_selectedPolygonIndex >= 0 && _selectedPolygonIndex < _currentModel.Polys.Length)
        {
            RenderSelectionOverlay(camera, lightCamera);
        }
        
        // Restore original transform
        _currentModel.Position = originalPosition;
        _currentModel.Rotation = originalRotation;
    }
    
    private void RenderSelectionOverlay(PerspectiveCamera camera, Camera lightCamera)
    {
        if (_currentModel == null || _selectedPolygonIndex < 0) return;
        
        // Create a temporary mesh with only the selected polygon
        var selectedPoly = _currentModel.Polys[_selectedPolygonIndex];
        
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
        overlayMesh.Position = _currentModel.Position;
        overlayMesh.Rotation = _currentModel.Rotation;
        
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
