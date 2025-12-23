using ImGuiNET;
using System.Numerics;
using System.IO;
using System.Globalization;
using NFMWorld.Util;
using NFMWorld.DriverInterface;
using SDL3;

namespace NFMWorld.Mad.UI;

/// <summary>
/// Settings menu with tabs, similar to Half-Life 1 style
/// </summary>
public class SettingsMenu(Program game)
{
    private bool _isOpen;
    private int _selectedTab = 0;
    
    private readonly string[] _tabNames = { "Keyboard", "Video", "Audio", "Game" };

    // Keyboard bindings
    public class KeyBindings
    {
        public Keys Accelerate { get; set; } = Keys.Up;
        public Keys Brake { get; set; } = Keys.Down;
        public Keys TurnLeft { get; set; } = Keys.Left;
        public Keys TurnRight { get; set; } = Keys.Right;
        public Keys Handbrake { get; set; } = Keys.Space;
        public Keys Enter { get; set; } = Keys.Enter;
        public Keys AerialBounce { get; set; } = Keys.Q;
        public Keys AerialStrafe { get; set; } = Keys.E;
        public Keys LookLeft { get; set; } = Keys.Z;
        public Keys LookBack { get; set; } = Keys.X;
        public Keys LookRight { get; set; } = Keys.C;
        public Keys ToggleMusic { get; set; } = Keys.M;
        public Keys ToggleSFX { get; set; } = Keys.N;
        public Keys ToggleArrace { get; set; } = Keys.A;
        public Keys ToggleRadar { get; set; } = Keys.S;
        public Keys ToggleCarCam { get; set; } = Keys.W;
        public Keys ToggleDevConsole { get; set; } = Keys.Oemtilde;
        public Keys CycleView { get; set; } = Keys.V;
    }

    public static KeyBindings Bindings = new KeyBindings();
    private string? _capturingAction = null;
    private int _selectedBindingIndex = -1;

    // Video settings
    private int _selectedRenderer = 1;
    private readonly string[] _renderers = { "OpenGL", "SDL_GPU", "D3D11"};
    private int _selectedResolution = 2;
    private readonly string[] _resolutions = { "800 x 600", "1024 x 768", "1280 x 720", "1280 x 1024", "1920 x 1080", "2560 x 1440" };
    private int _selectedDisplayMode = 1;
    private readonly string[] _displayModes = { "Fullscreen", "Windowed", "Borderless" };
    private bool _vsync = true;
    private int _fpsLimit = 63;
    private float _brightness = 0.5f;
    private float _gamma = 0.5f;
    private float _lineWidth = 0.002f;

    // Audio settings
    private float _masterVolume = 1.0f;
    private float _musicVolume = 0.8f;
    private float _effectsVolume = 0.9f;
    private bool _muteAll = false;
    private bool _remasteredMusic = false;

    // Game settings (Camera)
    private float _fov = 90.0f;
    private int _followY = 0;
    private int _followZ = 0;

    // Keyboard settings
    private string _settingMessage = "";

    public bool IsOpen => _isOpen;

    Vector4 RGB(int r, int g, int b, float a = 1.0f) => new Vector4(r / 255f, g / 255f, b / 255f, a);

    public void Open()
    {
        _isOpen = true;
        
        // Load current game settings
        _fov = InRacePhase.camera.Fov;
        _followY = InRacePhase.PlayerFollowCamera.FollowYOffset;
        _followZ = InRacePhase.PlayerFollowCamera.FollowZOffset;
    }

    public void Close()
    {
        _isOpen = false;
    }

    public void Render()
    {
        if (!_isOpen)
            return;

        // Set window size and position
        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(570, 390), ImGuiCond.Appearing);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("Options", ref _isOpen, flags))
        {
            DrawTabs();
            
            ImGui.Spacing();

            // Calculate height for scrollable content area (leave room for bottom buttons)
            float bottomButtonsHeight = 60f; // Height for separator + buttons + padding
            float availableHeight = ImGui.GetContentRegionAvail().Y - bottomButtonsHeight;

            // Scrollable content area
            if (ImGui.BeginChild("SettingsContent", new Vector2(0, availableHeight)))
            {
                // Draw content based on selected tab
                switch (_selectedTab)
                {
                    case 0: DrawKeyboardTab(); break;
                    case 1: DrawVideoTab(); break;
                    case 2: DrawAudioTab(); break;
                    case 3: DrawGameTab(); break;
                }
            }
            ImGui.EndChild();

            // Static bottom section
            ImGui.Separator();
            DrawBottomButtons();

            ImGui.End();
        }
    }

    private void DrawTabs()
    {
        if (ImGui.BeginTabBar("SettingsTabs", ImGuiTabBarFlags.None))
        {
            for (int i = 0; i < _tabNames.Length; i++)
            {
                if (ImGui.BeginTabItem(_tabNames[i]))
                {
                    _selectedTab = i;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawAudioTab()
    {
        ImGui.Text("Audio Settings");
        ImGui.Spacing();

        ImGui.Checkbox("Mute All", ref _muteAll);
        ImGui.Spacing();

        ImGui.Checkbox("Use Remastered Music if Available", ref _remasteredMusic);
        ImGui.Spacing();

        ImGui.Text("Master Volume");
        ImGui.SliderFloat("##MasterVolume", ref _masterVolume, 0.0f, 1.0f, "%.2f");
        
        ImGui.Text("Music Volume");
        ImGui.SliderFloat("##MusicVolume", ref _musicVolume, 0.0f, 1.0f, "%.2f");
        
        ImGui.Text("Effects Volume");
        ImGui.SliderFloat("##EffectsVolume", ref _effectsVolume, 0.0f, 1.0f, "%.2f");
    }

    public void HandleKeyCapture(Keys key)
    {
        if (_capturingAction == null || !_isOpen)
            return;

        // Cancel capture on ESC
        if (key == Keys.Escape)
        {
            _capturingAction = null;
            _selectedBindingIndex = -1;
            return;
        }

        // Clear any existing binding that uses this key
        var allProperties = typeof(KeyBindings).GetProperties();
        foreach (var prop in allProperties)
        {
            if (prop.Name != _capturingAction && prop.GetValue(Bindings) is Keys existingKey && existingKey == key)
            {
                // Clear the conflicting binding by setting it to None
                prop.SetValue(Bindings, Keys.None);
                GameSparker.Writer?.WriteLine($"Cleared {prop.Name} (was {key})", "debug");
            }
        }

        // Set the new binding
        var property = typeof(KeyBindings).GetProperty(_capturingAction);
        if (property != null)
        {
            property.SetValue(Bindings, key);
            GameSparker.Writer?.WriteLine($"Bound {_capturingAction} to {key}", "debug");
        }

        _capturingAction = null;
        _selectedBindingIndex = -1;
    }

    private void ResetKeyBindings()
    {
        Bindings = new KeyBindings();
        _capturingAction = null;
        _selectedBindingIndex = -1;
    }

    public bool IsCapturingKey() => _capturingAction != null;

    private void DrawVideoTab()
    {
        ImGui.Text("Video Settings");
        ImGui.Spacing();

        ImGui.Text("Renderer");
        ImGui.Combo("##Renderer", ref _selectedRenderer, _renderers, _renderers.Length);
        
        ImGui.Text("Resolution");
        ImGui.Combo("##Resolution", ref _selectedResolution, _resolutions, _resolutions.Length);
        
        ImGui.Text("Display Mode");
        ImGui.Combo("##DisplayMode", ref _selectedDisplayMode, _displayModes, _displayModes.Length);
        
        ImGui.Spacing();
        ImGui.Checkbox("Wait for vertical sync", ref _vsync);
        
        ImGui.Text("FPS Limit");
        float sliderWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.SliderInt("##FPSLimit", ref _fpsLimit, 0, 240, "%d FPS (0 = Unlimited)");
        
        ImGui.Spacing();
        ImGui.Text("Brightness");
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.SliderFloat("##Brightness", ref _brightness, 0.0f, 1.0f, "%.2f");
        float startX = ImGui.GetCursorPosX();
        ImGui.TextDisabled("Dark");
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + sliderWidth - ImGui.CalcTextSize("Light").X);
        ImGui.TextDisabled("Light");
        
        ImGui.Text("Gamma");
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.SliderFloat("##Gamma", ref _gamma, 0.0f, 1.0f, "%.2f");
        ImGui.TextDisabled("Low");
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + sliderWidth - ImGui.CalcTextSize("High").X);
        ImGui.TextDisabled("High");

        ImGui.Spacing();
        ImGui.Text("Outline Width");
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.SliderFloat("##LineWidth", ref _lineWidth, 0.001f, 0.005f, "%.4f");
        // ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.4f, 1.0f), 
        //     "Note: changing some video options will cause the game to exit and restart.");
    }

    private void DrawKeyboardTab()
    {
        ImGui.Text("Key Bindings");
        ImGui.Spacing();

        if (ImGui.Button("Reset All to Defaults", new Vector2(-1, 0)))
        {
            GameSparker.MessageWindow.ShowYesNo("Reset Key Binds", "Are you sure you want to reset key binds to default?",
            result => {
                if (result == MessageWindow.MessageResult.Yes) {
                    ResetKeyBindings();
                }
            });
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Draw key binding table
        var bindings = new (string Action, string PropertyName, Keys Key)[] 
        {
            ("Accelerate", "Accelerate", Bindings.Accelerate),
            ("Brake / Reverse", "Brake", Bindings.Brake),
            ("Turn Left", "TurnLeft", Bindings.TurnLeft),
            ("Turn Right", "TurnRight", Bindings.TurnRight),
            ("Handbrake / Stunt", "Handbrake", Bindings.Handbrake),
            ("Cycle View", "CycleView", Bindings.CycleView),
            ("Aerial boost / bounce", "AerialBounce", Bindings.AerialBounce),
            ("Aerial strafe, Smooth turn", "AerialStrafe", Bindings.AerialStrafe),
            //("Enter", "Enter", Bindings.Enter),       //iirc previously this would bring up pause menu in game and also used as keyboard navigation through menus, perhaps not needed to be able to be binded here
            ("Look Back", "LookBack", Bindings.LookBack),
            ("Look Left", "LookLeft", Bindings.LookLeft),
            ("Look Right", "LookRight", Bindings.LookRight),
            ("Toggle Music", "ToggleMusic", Bindings.ToggleMusic),
            ("Toggle SFX", "ToggleSFX", Bindings.ToggleSFX),
            ("Toggle Arrow Mode", "ToggleArrace", Bindings.ToggleArrace),
            ("Toggle Radar", "ToggleRadar", Bindings.ToggleRadar),
            ("Toggle Developer Console", "ToggleDevConsole", Bindings.ToggleDevConsole),
        };

        ImGui.Columns(2, "KeyBindings", true);
        ImGui.SetColumnWidth(0, 200);
        
        for (int i = 0; i < bindings.Length; i++)
        {
            var (action, propName, key) = bindings[i];
            
            ImGui.Text(action);
            ImGui.NextColumn();
            
            bool isCapturing = _capturingAction == propName;
            string buttonLabel = isCapturing ? "Press any key..." : key.ToString();
            
            if (isCapturing)
                ImGui.PushStyleColor(ImGuiCol.Button, RGB(128, 77, 3, 0.8f));
            
            if (ImGui.Button($"{buttonLabel}##{propName}", new Vector2(-1, 0)))
            {
                _capturingAction = propName;
                _selectedBindingIndex = i;
            }
            
            if (isCapturing)
                ImGui.PopStyleColor();
            
            ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
    }

    private void DrawGameTab()
    {
        ImGui.Text("Camera Settings");
        ImGui.Spacing();
        
        ImGui.Text("Field of View");
        ImGui.SliderFloat("##FOV", ref _fov, 70.0f, 120.0f, "%.1f°");
        
        ImGui.Spacing();
        ImGui.Text("Follow Y Offset");
        ImGui.SliderInt("##FollowY", ref _followY, -160, 500);
        
        ImGui.Spacing();
        ImGui.Text("Follow Z Offset");
        ImGui.SliderInt("##FollowZ", ref _followZ, -500, 500);
        
        ImGui.Spacing();
        if (ImGui.Button("Reset Camera Defaults", new Vector2(-1, 0)))
        {
            GameSparker.MessageWindow.ShowYesNo("Reset Camera", "Are you sure you want to reset camera settings to default?",
            result => {
                if (result == MessageWindow.MessageResult.Yes) {
                    _fov = 90.0f;
                    _followY = 0;
                    _followZ = 0;
                }
            });
        }
    }

    private void DrawBottomButtons()
    {
        float buttonWidth = 100f;
        float spacing = 10f;
        float totalWidth = buttonWidth * 3 + spacing * 2;
        
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);

        if (ImGui.Button("OK", new Vector2(buttonWidth, 30)))
        {
            ApplySettingsAndSave();
            _isOpen = false;
        }

        ImGui.SameLine(0, spacing);

        if (ImGui.Button("Cancel", new Vector2(buttonWidth, 30)))
        {
            _isOpen = false;
        }

        ImGui.SameLine(0, spacing);

        if (ImGui.Button("Apply", new Vector2(buttonWidth, 30)))
        {
            ApplySettingsAndSave();
        }

        if (_capturingAction != null)
        {
            if (!string.IsNullOrEmpty(_settingMessage))
            {
                _settingMessage = "";
            }
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.2f, 1.0f), 
                "Press any key to bind, or ESC to cancel...");
        }

        // Show message if settings were applied
        if (!string.IsNullOrEmpty(_settingMessage))
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), _settingMessage);
        }
    }

    private void ApplySettingsAndSave()
    {
        // Here you would actually apply the settings to the game
        // For now, just show a confirmation message
        _settingMessage = "Settings applied successfully!";
        
        ApplySettings(out var requireRestart);

        // Save config to file
        SaveConfig();
    }

    private void ApplySettings(out bool requireRestart)
    {
        // Apply audio settings
        if (_muteAll)
        {
            // Mute all sounds
            IBackend.Backend.SetAllVolumes(0);
            GameSparker.CurrentMusic?.SetVolume(0);
            IRadicalMusic.CurrentVolume = 0;
        }
        else
        {
            // Apply volume settings
            IBackend.Backend.SetAllVolumes(_effectsVolume * _masterVolume);
            GameSparker.CurrentMusic?.SetVolume(_musicVolume * _masterVolume);
            IRadicalMusic.CurrentVolume = _musicVolume * _masterVolume;
            GameSparker.UseRemasteredMusic = _remasteredMusic;
        }

        // Apply camera settings
        InRacePhase.camera.Fov = _fov;
        InRacePhase.PlayerFollowCamera.FollowYOffset = _followY;
        InRacePhase.PlayerFollowCamera.FollowZOffset = _followZ;

        bool graphicsChanged = false;
        requireRestart = false;
        if (game._graphics.SynchronizeWithVerticalRetrace != _vsync)
        {
            game._graphics.SynchronizeWithVerticalRetrace = _vsync;
            graphicsChanged = true;
        }
        
        if (_renderers[_selectedRenderer] != SDL.SDL_GetHint("FNA3D_FORCE_DRIVER"))
        {
            SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", _renderers[_selectedRenderer]); // TODO this only ever gets executed too late to do anything.
            requireRestart = true;
        }

        if (graphicsChanged)
        {
            game._graphics.ApplyChanges();
        }

        if (_fpsLimit != 0)
        {
            game.TargetElapsedTime = TimeSpan.FromMilliseconds(1000d / _fpsLimit);
            game.IsFixedTimeStep = true;
        }
        else
        {
            game.IsFixedTimeStep = false;
        }

        World.OutlineThickness = _lineWidth;
    }

    private void SaveConfig()
    {
        try
        {
            string configPath = Path.Combine("data", "cfg", "config.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            
            using (StreamWriter cfgWriter = new StreamWriter(configPath))
            {
                cfgWriter.WriteLine("// NFM-World Configuration File");
                cfgWriter.WriteLine("// Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cfgWriter.WriteLine();
                
                // Video settings
                cfgWriter.WriteLine("// Video Settings");
                cfgWriter.WriteLine($"video_renderer {_selectedRenderer}");
                cfgWriter.WriteLine($"video_resolution {_selectedResolution}");
                cfgWriter.WriteLine($"video_displaymode {_selectedDisplayMode}");
                cfgWriter.WriteLine($"video_vsync {(_vsync ? 1 : 0)}");
                cfgWriter.WriteLine($"video_fps {_fpsLimit}");
                cfgWriter.WriteLine($"video_brightness {_brightness.ToString("F2", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"video_gamma {_gamma.ToString("F2", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"video_linewidth {_lineWidth.ToString("F4", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine();
                
                // Audio settings
                cfgWriter.WriteLine("// Audio Settings");
                cfgWriter.WriteLine($"audio_mute {(_muteAll ? 1 : 0)}");
                cfgWriter.WriteLine($"audio_master {_masterVolume.ToString("F2", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"audio_music {_musicVolume.ToString("F2", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"audio_effects {_effectsVolume.ToString("F2", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"audio_remaster {(_remasteredMusic ? 1 : 0)}");
                cfgWriter.WriteLine();
                
                // Camera settings
                cfgWriter.WriteLine("// Camera Settings");
                cfgWriter.WriteLine($"camera_fov {_fov.ToString("F1", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"camera_follow_y {_followY}");
                cfgWriter.WriteLine($"camera_follow_z {_followZ}");
                cfgWriter.WriteLine();
                
                // Key bindings
                cfgWriter.WriteLine("// Key Bindings");
                cfgWriter.WriteLine($"key_accelerate {(int)Bindings.Accelerate}");
                cfgWriter.WriteLine($"key_ab {(int)Bindings.AerialBounce}");
                cfgWriter.WriteLine($"key_smoothturn {(int)Bindings.AerialStrafe}");
                cfgWriter.WriteLine($"key_brake {(int)Bindings.Brake}");
                cfgWriter.WriteLine($"key_turnleft {(int)Bindings.TurnLeft}");
                cfgWriter.WriteLine($"key_turnright {(int)Bindings.TurnRight}");
                cfgWriter.WriteLine($"key_handbrake {(int)Bindings.Handbrake}");
                cfgWriter.WriteLine($"key_lookback {(int)Bindings.LookBack}");
                cfgWriter.WriteLine($"key_lookleft {(int)Bindings.LookLeft}");
                cfgWriter.WriteLine($"key_lookright {(int)Bindings.LookRight}");
                cfgWriter.WriteLine($"key_togglemusic {(int)Bindings.ToggleMusic}");
                cfgWriter.WriteLine($"key_togglesfx {(int)Bindings.ToggleSFX}");
                cfgWriter.WriteLine($"key_togglearrace {(int)Bindings.ToggleArrace}");
                cfgWriter.WriteLine($"key_toggleradar {(int)Bindings.ToggleRadar}");
                cfgWriter.WriteLine($"key_cycleview {(int)Bindings.CycleView}");
                cfgWriter.WriteLine($"key_console {(int)Bindings.ToggleDevConsole}");
                cfgWriter.WriteLine();
            }
            
            GameSparker.Writer?.WriteLine($"Config saved to {configPath}", "debug");
        }
        catch (Exception ex)
        {
            GameSparker.Writer?.WriteLine($"Error saving config: {ex.Message}", "error");
        }
    }
    
    public void LoadConfig()
    {
        try
        {
            string configPath = Path.Combine("data", "cfg", "config.cfg");
            
            if (!System.IO.File.Exists(configPath))
            {
                GameSparker.Writer?.WriteLine("No config file found, using defaults.", "warning");
                return;
            }
            
            string[] lines = System.IO.File.ReadAllLines(configPath);
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                    continue;
                
                string[] parts = trimmed.Split(' ', 2);
                if (parts.Length != 2)
                    continue;
                
                string key = parts[0];
                string value = parts[1];
                
                try
                {
                    switch (key)
                    {
                        // Video settings
                        case "video_renderer":
                            _selectedRenderer = int.Parse(value);
                            break;
                        case "video_resolution":
                            _selectedResolution = int.Parse(value);
                            break;
                        case "video_displaymode":
                            _selectedDisplayMode = int.Parse(value);
                            break;
                        case "video_vsync":
                            _vsync = int.Parse(value) != 0;
                            break;
                        case "video_fps":
                            _fpsLimit = int.Parse(value);
                            break;
                        case "video_brightness":
                            _brightness = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_gamma":
                            _gamma = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_linewidth":
                            _lineWidth = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        
                        // Audio settings
                        case "audio_mute":
                            _muteAll = int.Parse(value) != 0;
                            break;
                        case "audio_master":
                            _masterVolume = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "audio_music":
                            _musicVolume = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "audio_effects":
                            _effectsVolume = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "audio_remaster":
                            _remasteredMusic = int.Parse(value) != 0;
                            break;
                        
                        // Camera settings
                        case "camera_fov":
                            _fov = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "camera_follow_y":
                            _followY = int.Parse(value);
                            break;
                        case "camera_follow_z":
                            _followZ = int.Parse(value);
                            break;
                        
                        // Key bindings
                        case "key_accelerate":
                            Bindings.Accelerate = (Keys)int.Parse(value);
                            break;
                        case "key_ab":
                            Bindings.AerialBounce = (Keys)int.Parse(value);
                            break;
                        case "key_smoothturn":
                            Bindings.AerialStrafe = (Keys)int.Parse(value);
                            break;
                        case "key_brake":
                            Bindings.Brake = (Keys)int.Parse(value);
                            break;
                        case "key_turnleft":
                            Bindings.TurnLeft = (Keys)int.Parse(value);
                            break;
                        case "key_turnright":
                            Bindings.TurnRight = (Keys)int.Parse(value);
                            break;
                        case "key_handbrake":
                            Bindings.Handbrake = (Keys)int.Parse(value);
                            break;
                        case "key_lookback":
                            Bindings.LookBack = (Keys)int.Parse(value);
                            break;
                        case "key_lookleft":
                            Bindings.LookLeft = (Keys)int.Parse(value);
                            break;
                        case "key_lookright":
                            Bindings.LookRight = (Keys)int.Parse(value);
                            break;
                        case "key_togglemusic":
                            Bindings.ToggleMusic = (Keys)int.Parse(value);
                            break;
                        case "key_togglesfx":
                            Bindings.ToggleSFX = (Keys)int.Parse(value);
                            break;
                        case "key_togglearrace":
                            Bindings.ToggleArrace = (Keys)int.Parse(value);
                            break;
                        case "key_console":
                            Bindings.ToggleDevConsole = (Keys)int.Parse(value);
                            break;
                        case "key_toggleradar":
                            Bindings.ToggleRadar = (Keys)int.Parse(value);
                            break;
                        case "key_cycleview":
                            Bindings.CycleView = (Keys)int.Parse(value);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    GameSparker.Writer?.WriteLine($"Error parsing config line '{line}': {ex.Message}", "error");
                }
            }
            
            // Apply loaded settings immediately
            ApplySettings(out _);
            
            GameSparker.Writer?.WriteLine($"Config loaded from {configPath}", "debug");
        }
        catch (Exception ex)
        {
            GameSparker.Writer?.WriteLine($"Error loading config: {ex.Message}", "error");
        }
    }
}
