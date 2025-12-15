using ImGuiNET;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using System.Text;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad.UI;

public class ModelEditor
{
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
    
    public ModelEditor()
    {
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
    
    public void Open()
    {
        _isOpen = true;
        
        // render an empty stage for 1 frame to init shaders
        _modelViewerStage = new Stage("empty", GameSparker._graphicsDevice);
        World.Snap = new Color3(100, 100, 100);
        
        GameSparker.camera.Position = new Vector3(0, -800, -800);
        GameSparker.camera.LookAt = Vector3.Zero;
        GameSparker.lightCamera.Position = GameSparker.camera.Position + new Vector3(0, -5000, 0);
        GameSparker.lightCamera.LookAt = GameSparker.camera.Position + new Vector3(1f, 0, 0);
        
        GameSparker.camera.OnBeforeRender();
        GameSparker.lightCamera.OnBeforeRender();
        
        GameSparker._graphicsDevice.BlendState = BlendState.Opaque;
        GameSparker._graphicsDevice.DepthStencilState = DepthStencilState.Default;
        
        _modelViewerStage.Render(GameSparker.camera, GameSparker.lightCamera, false);
        
        _currentModel = null;
        _currentModelPath = null;
        RefreshUserModels();
        
        ResetView();
    }
    
    public void Close()
    {
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
    
    public void Update()
    {
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
    }
    
    public void Render()
    {
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
                    GameSparker.ExitModelViewer();
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
            
            if (ImGui.Button(_textEditorExpanded ? "Hide Text Editor" : "Show Text Editor"))
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
                        if (GameSparker.Writer != null)
                        {
                            GameSparker.Writer.WriteLine(err, "error");
                        }
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
            
            var textChanged = ImGui.InputTextMultiline("##TextEditor", ref _modelTextContent, 200000, 
                new System.Numerics.Vector2(-1, -1), 
                ImGuiInputTextFlags.AllowTabInput);
            
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
                if (ImGui.Checkbox("Display Trackers", ref showTrackers))
                {
                    GameSparker.devRenderTrackers = showTrackers;
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Show collision/tracking boxes on models");
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
    }
    
    public void RenderModel(PerspectiveCamera camera, Camera lightCamera)
    {
        if (!_isOpen || _currentModel == null) return;
        
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
        
        // Render the model with lighting
        scene.Render(false);
        
        // Restore original transform
        _currentModel.Position = originalPosition;
        _currentModel.Rotation = originalRotation;
    }
}
