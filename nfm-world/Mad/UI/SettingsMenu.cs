using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Camera;
using NFMWorld.DriverInterface;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using SDL3;

namespace NFMWorld.UI;

/// <summary>
/// Settings menu with tabs, similar to Half-Life 1 style
/// </summary>
public class SettingsMenu(WorldGame game)
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
    private static readonly string[] Renderers = false switch
    {
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => ["Auto", "Metal", "OpenGL 2.1", "OpenGL 4.6", "OpenGL ES 3.0"],
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => ["Auto", "D3D11", "D3D12", "Vulkan", "OpenGL 2.1", "OpenGL 4.6", "Metal", "OpenGL ES 3.0"],
        _ => ["Auto", "Vulkan", "OpenGL 2.1", "OpenGL 4.6", "OpenGL ES 3.0"]
    };
    private int _selectedRenderer = 0;
    private static readonly string[] Resolutions = GetSupportedResolutions();
    private int _selectedResolution = Resolutions.FindIndex(e => e == "1280 x 720");
    private static readonly string[] DisplayModes = ["Fullscreen", "Windowed", "Borderless"];
    private int _selectedDisplayMode = 1;
    private bool _vsync = true;
    private static readonly string[] AntialiasModes = ["Off", "MSAA 1x", "MSAA 2x", "MSAA 4x", "MSAA 8x"]; // must be powers of 2
    private int _antialias = 4; // 8x
    private int _fpsLimit = 63;
    private float _brightness = 0.5f;
    private float _gamma = 0.5f;
    private float _lineWidth = 1;

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

    private static string[] GetSupportedResolutions()
    {
        // Everybody should be able to use these
        SortedSet<string> resolutions = new(Comparer<string>.Create((a, b) => {
            var aParts = a.Split('x', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
            var bParts = b.Split('x', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
            var aPixels = aParts[0] * aParts[1];
            var bPixels = bParts[0] * bParts[1];
            return aPixels.CompareTo(bPixels);
        }))
        { 
            "640 x 480", "800 x 600", "1024 x 768", "1280 x 720", "1280 x 1024", "1920 x 1080", "2560 x 1440",
            "3840 x 2160"
        };
        foreach (var displayMode in GraphicsAdapter.DefaultAdapter.SupportedDisplayModes)
        {
            resolutions.Add($"{displayMode.Width} x {displayMode.Height}");
        }
        return resolutions.ToArray();
    }

    public void Open()
    {
        _isOpen = true;
        
        // Load current game settings
        _fov = CameraSettings.Fov;
        _followY = FollowCamera.FollowYOffset;
        _followZ = FollowCamera.FollowZOffset;
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
        var center = ImGui.GetCenter(viewport);
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(570, 390), ImGuiCond.Appearing);

        var flags = ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("Options", ref _isOpen, flags))
        {
            DrawTabs();
            
            ImGui.Spacing();

            // Calculate height for scrollable content area (leave room for bottom buttons)
            var bottomButtonsHeight = 60f; // Height for separator + buttons + padding
            var availableHeight = ImGui.GetContentRegionAvail().Y - bottomButtonsHeight;

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
            for (var i = 0; i < _tabNames.Length; i++)
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
                Logging.Debug($"Cleared {prop.Name} (was {key})");
            }
        }

        // Set the new binding
        var property = typeof(KeyBindings).GetProperty(_capturingAction);
        if (property != null)
        {
            property.SetValue(Bindings, key);
            Logging.Debug($"Bound {_capturingAction} to {key}");
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
        ImGui.Combo("##Renderer", ref _selectedRenderer, Renderers, Renderers.Length);
        
        ImGui.Text("Resolution");
        ImGui.Combo("##Resolution", ref _selectedResolution, Resolutions, Resolutions.Length);
        
        ImGui.Text("Display Mode");
        ImGui.Combo("##DisplayMode", ref _selectedDisplayMode, DisplayModes, DisplayModes.Length);
        
        ImGui.Spacing();
        ImGui.Checkbox("Wait for vertical sync", ref _vsync);
        
        ImGui.Text("FPS Limit");
        var sliderWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.SliderInt("##FPSLimit", ref _fpsLimit, 0, 240, "%d FPS (0 = Unlimited)");
        
        ImGui.Text("Antialiasing");
        ImGui.Combo("##Antialiasing", ref _antialias, AntialiasModes, AntialiasModes.Length);

        ImGui.Spacing();
        ImGui.Text("Brightness");
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.SliderFloat("##Brightness", ref _brightness, 0.0f, 1.0f, "%.2f");
        var startX = ImGui.GetCursorPosX();
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
        ImGui.SliderFloat("##LineWidth", ref _lineWidth, 0.5f, 4f, "%.1f");
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
        
        for (var i = 0; i < bindings.Length; i++)
        {
            var (action, propName, key) = bindings[i];
            
            ImGui.Text(action);
            ImGui.NextColumn();
            
            var isCapturing = _capturingAction == propName;
            var buttonLabel = isCapturing ? "Press any key..." : key.ToString();
            
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
        var buttonWidth = 100f;
        var spacing = 10f;
        var totalWidth = buttonWidth * 3 + spacing * 2;
        
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
        CameraSettings.Fov = _fov;
        FollowCamera.FollowYOffset = _followY;
        FollowCamera.FollowZOffset = _followZ;

        var graphicsChanged = false;
        requireRestart = false;
        if (game._graphics.SynchronizeWithVerticalRetrace != _vsync)
        {
            game._graphics.SynchronizeWithVerticalRetrace = _vsync;
            graphicsChanged = true;
        }

        if (_antialias > 0)
        {
            if (!game._graphics.PreferMultiSampling)
            {
                game._graphics.PreferMultiSampling = true;
                graphicsChanged = true;
            }
            
            var msaaCount = (int) MathF.Round(MathF.Pow(2, _antialias - 1));

            if (game._graphics.GraphicsDevice.PresentationParameters.MultiSampleCount != msaaCount)
            {
                game._graphics.GraphicsDevice.PresentationParameters.MultiSampleCount = msaaCount;
                graphicsChanged = true;
            }
        }
        
        if (_selectedDisplayMode == 0) // fullscreen
        {
            if (!game._graphics.IsFullScreen)
            {
                game._graphics.IsFullScreen = true;
                graphicsChanged = true;
            }

            if (game.Window.IsBorderlessEXT)
            {
                game.Window.IsBorderlessEXT = false;
                graphicsChanged = true;
            }
        }
        else if (_selectedDisplayMode == 1) // windowed
        {
            if (game._graphics.IsFullScreen)
            {
                game._graphics.IsFullScreen = false;
                graphicsChanged = true;
            }

            if (game.Window.IsBorderlessEXT) {
                game.Window.IsBorderlessEXT = false;
                graphicsChanged = true;
            }
        }
        else // borderless
        {
            if (game._graphics.IsFullScreen)
            {
                game._graphics.IsFullScreen = false;
                graphicsChanged = true;
            }

            if (!game.Window.IsBorderlessEXT)
            {
                game.Window.IsBorderlessEXT = true;
                graphicsChanged = true;
            }
        }
        
        var widthHeight = Resolutions[_selectedResolution].Split('x', StringSplitOptions.TrimEntries);
        var (width, height) = (int.Parse(widthHeight[0]), int.Parse(widthHeight[1]));
        if (game._graphics.PreferredBackBufferWidth != width || game._graphics.PreferredBackBufferHeight != height)
        {
            game._graphics.PreferredBackBufferWidth = width;
            game._graphics.PreferredBackBufferHeight = height;
            graphicsChanged = true;
        }

        if (Renderers[_selectedRenderer] != GetFna3DRenderer())
        {
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
            var configPath = Path.Combine("data", "cfg", "config.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            
            using (var cfgWriter = new StreamWriter(configPath))
            {
                cfgWriter.WriteLine("// NFM-World Configuration File");
                cfgWriter.WriteLine("// Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cfgWriter.WriteLine();
                
                // Video settings
                cfgWriter.WriteLine("// Video Settings");
                cfgWriter.WriteLine($"video_renderer2 {Renderers[_selectedRenderer]}");
                cfgWriter.WriteLine($"video_resolution3 {Resolutions[_selectedResolution]}");
                cfgWriter.WriteLine($"video_displaymode {_selectedDisplayMode}");
                cfgWriter.WriteLine($"video_vsync {(_vsync ? 1 : 0)}");
                cfgWriter.WriteLine($"video_antialias {_antialias}");
                cfgWriter.WriteLine($"video_fps {_fpsLimit}");
                cfgWriter.WriteLine($"video_brightness {_brightness.ToString("F2", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"video_gamma {_gamma.ToString("F2", CultureInfo.InvariantCulture)}");
                cfgWriter.WriteLine($"video_linewidth2 {_lineWidth.ToString("F4", CultureInfo.InvariantCulture)}");
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
            
            Logging.Debug($"Config saved to {configPath}");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error saving config: {ex.Message}");
        }
    }

    public static void LoadFnaRenderer()
    {
        var configPath = Path.Combine("data", "cfg", "config.cfg");

        string? selectedRenderer = null;
        if (File.Exists(configPath))
        {
            foreach (var line in File.ReadLines(configPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                    continue;

                var parts = trimmed.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    continue;

                var key = parts[0];
                var value = parts[1];

                try
                {
                    switch (key)
                    {
                        // Video settings
                        case "video_renderer2":
                            selectedRenderer = value;
                            break;
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        if (selectedRenderer != null && Renderers.Contains(selectedRenderer))
        {
            switch (selectedRenderer)
            {
                case "D3D11" or "D3D12" or "Vulkan":
                    Logging.Info($"Overriding FNA3D renderer to {selectedRenderer}");
                    SDL2.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", selectedRenderer);
                    SDL3.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", selectedRenderer);
                    break;
                case "OpenGL 2.1":
                    Logging.Info($"Overriding FNA3D renderer to {selectedRenderer}");
                    SDL2.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    SDL3.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    break;
                case "OpenGL 4.6":
                    Logging.Info($"Overriding FNA3D renderer to {selectedRenderer} (Core Profile)");
                    SDL2.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    SDL2.SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_CORE_PROFILE", "1");
                    SDL3.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    SDL3.SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_CORE_PROFILE", "1");
                    break;
                case "OpenGL ES 3.0":
                    Logging.Info($"Overriding FNA3D renderer to {selectedRenderer} (ES3)");
                    SDL2.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    SDL2.SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_ES3", "1");
                    SDL3.SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    SDL3.SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_ES3", "1");
                    break;
            }
        }
    }

    private static string GetFna3DRenderer()
    {
        var driver = SDL3.SDL.SDL_GetHint("FNA3D_FORCE_DRIVER") ?? SDL2.SDL.SDL_GetHint("FNA3D_FORCE_DRIVER");

        return driver switch
        {
            "D3D11" or "D3D12" or "Vulkan" => driver,
            "OpenGL" when SDL2.SDL.SDL_GetHint("FNA3D_OPENGL_FORCE_CORE_PROFILE") == "1" || SDL3.SDL.SDL_GetHint("FNA3D_OPENGL_FORCE_CORE_PROFILE") == "1" => "OpenGL 4.6",
            "OpenGL" when SDL2.SDL.SDL_GetHint("FNA3D_OPENGL_FORCE_ES3") == "1" || SDL3.SDL.SDL_GetHint("FNA3D_OPENGL_FORCE_ES3") == "1" => "OpenGL ES 3.0",
            "OpenGL" => "OpenGL 2.1",
            _ => "Auto"
        };
    }
    
    public void LoadConfig()
    {
        _selectedRenderer = Renderers.IndexOf(GetFna3DRenderer());
        
        try
        {
            var configPath = Path.Combine("data", "cfg", "config.cfg");
            
            if (!File.Exists(configPath))
            {
                Logging.Warning("No config file found, using defaults.");
                return;
            }

            foreach (var line in File.ReadLines(configPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                    continue;
                
                var parts = trimmed.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    continue;
                
                var key = parts[0];
                var value = parts[1];
                
                try
                {
                    switch (key)
                    {
                        // Video settings
                        case "video_renderer2":
                            _selectedRenderer = Renderers.IndexOf(value) is var rend and > -1 ? rend : _selectedRenderer;
                            break;
                        case "video_resolution3":
                            _selectedResolution = Resolutions.IndexOf(value) is var res and > -1 ? res : _selectedResolution;
                            break;
                        case "video_displaymode":
                            _selectedDisplayMode = int.Parse(value);
                            break;
                        case "video_vsync":
                            _vsync = int.Parse(value) != 0;
                            break;
                        case "video_antialias":
                            _antialias = int.Parse(value);
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
                        case "video_linewidth2":
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
                    SentrySdk.CaptureException(ex);
                    Logging.Error($"Error parsing config line '{line}': {ex.Message}");
                }
            }
            
            // Apply loaded settings immediately
            ApplySettings(out _);
            
            Logging.Debug($"Config loaded from {configPath}");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error loading config: {ex.Message}");
        }
    }
}
