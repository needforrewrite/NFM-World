using ImGuiNET;
using NFMWorld.Util;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad.UI;

public class ModelEditor
{
    private bool _isOpen = false;
    private int _selectedModelIndex = 0;
    private string[] _modelNames;
    private Mesh[] _models;
    
    // Camera/view controls
    private Vector3 _modelRotation = new Vector3(0, 0, 0); // Pitch, Yaw, Roll
    private float _modelHeight = 0f;
    private float _cameraDistance = 500f;
    private Vector3 _cameraPosition = new Vector3(0, 200, -500);
    
    // Control states
    private bool _rotateLeft = false;
    private bool _rotateRight = false;
    private bool _rotateUp = false;
    private bool _rotateDown = false;
    private bool _raiseUp = false;
    private bool _lowerDown = false;

    private bool _lightsOn = true;
    
    // Rotation speeds
    private const float ROTATION_SPEED = 2.0f;
    private const float HEIGHT_SPEED = 5.0f;
    
    public ModelEditor()
    {
        // Initialize with car models
        _modelNames = GameSparker.CarRads;
        _models = new Mesh[_modelNames.Length];
        for (int i = 0; i < _modelNames.Length; i++)
        {
            _models[i] = GameSparker.cars[i];
        }
    }
    
    public bool IsOpen => _isOpen;
    
    public void Open()
    {
        _isOpen = true;
        ResetView();
    }
    
    public void Close()
    {
        _isOpen = false;
    }
    
    private void ResetView()
    {
        _modelRotation = new Vector3(0, 0, 0);
        _modelHeight = 0f;
        _cameraDistance = 500f;
        UpdateCameraPosition();
    }
    
    private void UpdateCameraPosition()
    {
        // Position camera to look at the model
        _cameraPosition = new Vector3(0, 200 + _modelHeight, -_cameraDistance);
    }
    
    public void HandleKeyPress(Keys key)
    {
        if (!_isOpen) return;
        
        switch (key)
        {
            case Keys.W:
                _rotateUp = true;
                break;
            case Keys.S:
                _rotateDown = true;
                break;
            case Keys.A:
                _rotateLeft = true;
                break;
            case Keys.D:
                _rotateRight = true;
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
                _rotateUp = false;
                break;
            case Keys.S:
                _rotateDown = false;
                break;
            case Keys.A:
                _rotateLeft = false;
                break;
            case Keys.D:
                _rotateRight = false;
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
        
        // Apply rotation from keyboard
        if (_rotateLeft)
            _modelRotation.Y -= ROTATION_SPEED;
        if (_rotateRight)
            _modelRotation.Y += ROTATION_SPEED;
        if (_rotateUp)
            _modelRotation.X -= ROTATION_SPEED;
        if (_rotateDown)
            _modelRotation.X += ROTATION_SPEED;
        
        // Apply height changes
        if (_raiseUp)
            _modelHeight += HEIGHT_SPEED;
        if (_lowerDown)
            _modelHeight -= HEIGHT_SPEED;
        
        UpdateCameraPosition();
    }
    
    public void Render()
    {
        if (!_isOpen) return;
        
        var io = ImGui.GetIO();
        var displaySize = io.DisplaySize;
        
        // Top tabs bar (fixed position, full width)
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 0));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, 60));
        ImGui.Begin("##TopTabs", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse);
        
        if (ImGui.Button("Car", new System.Numerics.Vector2(100, 30)))
        {
            // Car tab
        }
        ImGui.SameLine();
        if (ImGui.Button("Code Edit", new System.Numerics.Vector2(100, 30)))
        {
            // Code Edit tab
        }
        ImGui.SameLine();
        if (ImGui.Button("3D Edit", new System.Numerics.Vector2(100, 30)))
        {
            // 3D Edit tab (current)
        }
        ImGui.SameLine();
        if (ImGui.Button("Publish", new System.Numerics.Vector2(100, 30)))
        {
            // Publish tab
        }
        
        // Right-aligned back button
        ImGui.SameLine(displaySize.X - 180);
        if (ImGui.Button("BACK TO GAME", new System.Numerics.Vector2(170, 30)))
        {
            GameSparker.ExitModelViewer();
        }
        
        ImGui.End();
        
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
                ImGui.Text("Car Model:");
                if (ImGui.BeginCombo("##Model", _modelNames[_selectedModelIndex]))
                {
                    for (int i = 0; i < _modelNames.Length; i++)
                    {
                        bool isSelected = (_selectedModelIndex == i);
                        if (ImGui.Selectable(_modelNames[i], isSelected))
                        {
                            _selectedModelIndex = i;
                            ResetView();
                        }
                        
                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
                
                ImGui.Spacing();
                ImGui.Text("Rotate on X / Z axis:");
                ImGui.Text("Move Up / Down:");
                ImGui.Spacing();
                ImGui.Text("Car Wheels:");
                ImGui.Text("Spin Forward / Back:");
                ImGui.Text("Turn Left / Right:");
                
                ImGui.NextColumn();
                
                // Right column
                float rotX = _modelRotation.X;
                float rotY = _modelRotation.Y;
                float rotZ = _modelRotation.Z;
                
                ImGui.Text("Rotation Controls:");
                if (ImGui.SliderFloat("X (Pitch)", ref rotX, -180f, 180f))
                    _modelRotation.X = rotX;
                if (ImGui.SliderFloat("Y (Yaw)", ref rotY, -180f, 180f))
                    _modelRotation.Y = rotY;
                if (ImGui.SliderFloat("Z (Roll)", ref rotZ, -180f, 180f))
                    _modelRotation.Z = rotZ;
                
                if (ImGui.SliderFloat("Height", ref _modelHeight, -500f, 500f))
                {
                    UpdateCameraPosition();
                }
                
                if (ImGui.SliderFloat("Camera Distance", ref _cameraDistance, 100f, 2000f))
                {
                    UpdateCameraPosition();
                }
                
                ImGui.Columns(1);
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
        
        // Left sidebar - Lights toggle
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 70));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(120, 50));
        ImGui.Begin("##Lights", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse);
        
        ImGui.Checkbox("Lights", ref _lightsOn);
        
        ImGui.End();
        
        // Right sidebar - Reset view
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(displaySize.X - 130, 70));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(120, 50));
        ImGui.Begin("##ResetView", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse);
        
        if (ImGui.Button("Reset view"))
        {
            ResetView();
        }
        
        ImGui.End();
    }
    
    public void RenderModel(PerspectiveCamera camera)
    {
        if (!_isOpen || _models[_selectedModelIndex] == null) return;
        
        // Set up camera to look at the model
        camera.Position = _cameraPosition;
        camera.LookAt = new Vector3(0, _modelHeight, 0);
        camera.OnBeforeRender();
        
        // Create a temporary mesh instance with the rotation and position
        var mesh = _models[_selectedModelIndex];
        
        // Store original transform
        var originalPosition = mesh.Position;
        var originalRotation = mesh.Rotation;
        
        // Apply our transform
        mesh.Position = new Vector3(0, _modelHeight, 0);
        mesh.Rotation = new Euler(
            AngleSingle.FromDegrees(_modelRotation.Y),  // Yaw
            AngleSingle.FromDegrees(_modelRotation.X),  // Pitch
            AngleSingle.FromDegrees(_modelRotation.Z)   // Roll
        );
        
        // Render the model
        mesh.Render(camera, null, false);
        
        // Restore original transform
        mesh.Position = originalPosition;
        mesh.Rotation = originalRotation;
    }
}
