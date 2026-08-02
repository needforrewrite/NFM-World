using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.UI.Cef;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using SDL3;
using NFMWorld.Sentry;

namespace NFMWorld.UI;

/// <summary>
/// Settings menu with tabs, similar to Half-Life 1 style.
/// Also serves as the static settings backend used by SettingsHandler for CEF-based settings.
/// </summary>
public class SettingsMenu(WorldGame game)
{
    private bool _isOpen;
    private int _selectedTab = 0;

    private readonly string[] _tabNames = { "Keyboard", "Video", "Audio", "Game" };

    // Keyboard bindings
    public class KeyBindings
    {
        public Key Accelerate { get; set; } = Key.Up;
        public Key Brake { get; set; } = Key.Down;
        public Key TurnLeft { get; set; } = Key.Left;
        public Key TurnRight { get; set; } = Key.Right;
        public Key Handbrake { get; set; } = Key.Space;
        public Key Enter { get; set; } = Key.Enter;
        public Key AerialBounce { get; set; } = Key.Q;
        public Key AerialStrafe { get; set; } = Key.E;
        public Key LookLeft { get; set; } = Key.Z;
        public Key LookBack { get; set; } = Key.X;
        public Key LookRight { get; set; } = Key.C;
        public Key ToggleMusic { get; set; } = Key.M;
        public Key ToggleSFX { get; set; } = Key.N;
        public Key ToggleArrace { get; set; } = Key.A;
        public Key ToggleRadar { get; set; } = Key.S;
        public Key ToggleCarCam { get; set; } = Key.W;
        public Key ToggleDevConsole { get; set; } = Key.Oemtilde;
        public Key CycleView { get; set; } = Key.V;
    }

    public static KeyBindings Bindings = new KeyBindings();
    private string? _capturingAction = null;
    private int _selectedBindingIndex = -1;

    // Video settings (static — shared between ImGui and CEF bridge)
    public static readonly string[] Renderers = false switch
    {
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => ["Auto", "Metal", "OpenGL 2.1", "OpenGL 4.6", "OpenGL ES 3.0"],
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => ["Auto", "D3D11", "D3D12", "Vulkan", "OpenGL 2.1", "OpenGL 4.6", "Metal", "OpenGL ES 3.0"],
        _ => ["Auto", "Vulkan", "OpenGL 2.1", "OpenGL 4.6", "OpenGL ES 3.0"]
    };
    private static int _selectedRenderer = 0;
    private static string[] _resolutions = GetSupportedResolutions();
    public static string[] Resolutions => _resolutions;
    private static int _selectedResolution = Array.FindIndex(_resolutions, e => e == "1280 x 720");
    public static readonly string[] DisplayModes = ["Fullscreen", "Windowed", "Borderless"];
    private static int _selectedDisplayMode = 1;
    private static bool _vsync = true;
    public static readonly string[] AntialiasModes = ["Off", "MSAA 1x", "MSAA 2x", "MSAA 4x", "MSAA 8x"]; // must be powers of 2
    private static int _antialias = 4; // 8x
    private static int _shadowCascadeLevel = 3;
    public static readonly string[] ShadowCascadeLevelNames = ["Off", "Close", "Far", "Further"];
    private static int _shadowResolution = 2; // 2048x
    public static readonly string[] ShadowResolutionNames = ["512", "1024", "2048", "4096", "8192"]; // must be powers of 2 starting at 2^9
    private static int _fpsLimit = 63;
    private static float _lineWidth = 1;
    private static readonly DistantOutlineBehavior[] DistantOutlineBehaviors = Enum.GetValues<DistantOutlineBehavior>();
    private static DistantOutlineBehavior _distantOutlineBehavior = DistantOutlineBehavior.DistanceFalloffWithCutoff;
    private static bool _lowLatency = false;
    public static readonly string[] RenderDistanceNames = ["Tiny", "Short", "Medium", "Far", "Very Far", "Unlimited"];
    private static readonly float[] RenderDistances = [22500, 45000, 90000, 180000, 360000, int.MaxValue];
    private static int _renderDistance = 5; // default to max distance

    // Audio settings (static)
    private static float _masterVolume = 1.0f;
    private static float _musicVolume = 0.8f;
    private static float _effectsVolume = 0.9f;
    private static bool _muteAll = false;
    private static bool _remasteredMusic = false;

    // Game settings — Camera (static)
    private static float _fov = PerspectiveCamera.DefaultFov;
    private static int _followY = 0;
    private static int _followZ = 0;
    private static bool _smoothFov;

    // Keyboard settings
    private string _settingMessage = "";

    /// <summary>Fired when the resolutions list changes (window resize, fullscreen toggle).</summary>
    public static event Action? ResolutionsChanged;

    public bool IsOpen => _isOpen;

    private static Vector4 RGB(int r, int g, int b, float a = 1.0f) => new Vector4(r / 255f, g / 255f, b / 255f, a);

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

    /// <summary>
    /// Register a new resolution and select it. Called automatically when the
    /// window is resized or fullscreen is toggled. Adds the resolution to the
    /// list if it doesn't already exist.
    /// </summary>
    public static void RegisterResolution(int width, int height)
    {
        var res = $"{width} x {height}";

        // Already exists — just select it
        var idx = Array.IndexOf(_resolutions, res);
        if (idx >= 0)
        {
            _selectedResolution = idx;
            return;
        }

        // Add and sort by total pixels
        var list = new List<string>(_resolutions) { res };
        list.Sort((a, b) =>
        {
            var aParts = a.Split('x', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
            var bParts = b.Split('x', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
            var aPixels = aParts[0] * aParts[1];
            var bPixels = bParts[0] * bParts[1];
            return aPixels.CompareTo(bPixels);
        });
        _resolutions = list.ToArray();
        _selectedResolution = Array.IndexOf(_resolutions, res);
        ResolutionsChanged?.Invoke();
    }

    public void Open()
    {
        _isOpen = true;

        // Load current game settings
        _fov = CameraSettings.Fov;
        _followY = FollowCamera.FollowYOffset;
        _followZ = FollowCamera.FollowZOffset;
        _smoothFov = CameraSettings.SmoothFov;
        _lineWidth = World.OutlineThickness;
        _distantOutlineBehavior = World.DistantOutlineBehavior;
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

    public void HandleKeyCapture(Key key)
    {
        if (_capturingAction == null || !_isOpen)
            return;

        // Cancel capture on ESC
        if (key == Key.Escape)
        {
            _capturingAction = null;
            _selectedBindingIndex = -1;
            return;
        }

        // Clear any existing binding that uses this key
        var allProperties = typeof(KeyBindings).GetProperties();
        foreach (var prop in allProperties)
        {
            if (prop.Name != _capturingAction && prop.GetValue(Bindings) is Key existingKey && existingKey == key)
            {
                // Clear the conflicting binding by setting it to None
                prop.SetValue(Bindings, Key.None);
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

        ImGui.Text("Shadow Distance");
        ImGui.Combo("##ShadowCascadeLevel", ref _shadowCascadeLevel, ShadowCascadeLevelNames, ShadowCascadeLevelNames.Length);

        ImGui.Text("Shadow Resolution");
        ImGui.Combo("##ShadowResolution", ref _shadowResolution, ShadowResolutionNames, ShadowResolutionNames.Length);

        ImGui.Text("Render Distance");
        ImGui.Combo("##RenderDistance", ref _renderDistance, RenderDistanceNames, RenderDistanceNames.Length);

        ImGui.Checkbox("Low Latency (Disable interpolation)", ref _lowLatency);

        ImGui.Spacing();
        ImGui.Text("Outline Width");
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.SliderFloat("##LineWidth", ref _lineWidth, 0.5f, 4f, "%.1f");

        ImGui.Text("Distant Outline Behavior");
        if (ImGui.BeginCombo("##DistantOutlineBehavior", GetDistantOutlineBehaviorDisplayName(_distantOutlineBehavior)))
        {
            foreach (var behavior in DistantOutlineBehaviors)
            {
                if (ImGui.Selectable(GetDistantOutlineBehaviorDisplayName(behavior), behavior == _distantOutlineBehavior))
                    _distantOutlineBehavior = behavior;
            }

            ImGui.EndCombo();
        }
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
        var bindings = new (string Action, string PropertyName, Key Key)[]
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
        ImGui.SliderFloat("##FOV", ref _fov, 58.7f, 120.0f, "%.1f°");

        ImGui.Spacing();
        ImGui.Checkbox("Smooth FOV Changes", ref _smoothFov);

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
                    _smoothFov = true;
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
        _settingMessage = "Settings applied successfully!";

        ApplySettings(out var requireRestart);

        // Save config to file
        SaveConfig();
    }

    public static void ApplySettings(out bool requireRestart)
    {
        var game = GameSparker.Game;

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
        CameraSettings.SmoothFov = _smoothFov;
        FollowCamera.FollowYOffset = _followY;
        FollowCamera.FollowZOffset = _followZ;
        CameraSettings.RenderDistanceSqr = RenderDistances[_renderDistance] * RenderDistances[_renderDistance];

        WorldGame.LowLatency = _lowLatency;

        var graphicsChanged = false;
        requireRestart = false;
        if (game.Graphics.SynchronizeWithVerticalRetrace != _vsync)
        {
            game.Graphics.SynchronizeWithVerticalRetrace = _vsync;
            graphicsChanged = true;
        }

        if (_antialias > 0)
        {
            if (!game.Graphics.PreferMultiSampling)
            {
                game.Graphics.PreferMultiSampling = true;
                graphicsChanged = true;
            }

            var msaaCount = (int) MathF.Round(MathF.Pow(2, _antialias - 1));

            if (game.Graphics.GraphicsDevice.PresentationParameters.MultiSampleCount != msaaCount)
            {
                game.Graphics.GraphicsDevice.PresentationParameters.MultiSampleCount = msaaCount;
                graphicsChanged = true;
            }
        }
        else
        {
            if (game.Graphics.PreferMultiSampling)
            {
                game.Graphics.PreferMultiSampling = false;
                graphicsChanged = true;
            }
        }

        if (_selectedDisplayMode == 0) // fullscreen
        {
            if (!game.Graphics.IsFullScreen)
            {
                game.Graphics.IsFullScreen = true;
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
            if (game.Graphics.IsFullScreen)
            {
                game.Graphics.IsFullScreen = false;
                graphicsChanged = true;
            }

            if (game.Window.IsBorderlessEXT) {
                game.Window.IsBorderlessEXT = false;
                graphicsChanged = true;
            }
        }
        else // borderless
        {
            if (game.Graphics.IsFullScreen)
            {
                game.Graphics.IsFullScreen = false;
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
        if (game.Graphics.PreferredBackBufferWidth != width || game.Graphics.PreferredBackBufferHeight != height)
        {
            game.Graphics.PreferredBackBufferWidth = width;
            game.Graphics.PreferredBackBufferHeight = height;
            graphicsChanged = true;
        }

        if (WorldGame.NumCascades != _shadowCascadeLevel || WorldGame.ShadowResolution != (int)MathF.Round(MathF.Pow(2, _shadowResolution + 9)))
        {
            WorldGame.NumCascades = _shadowCascadeLevel;
            WorldGame.ShadowResolution = (int)MathF.Round(MathF.Pow(2, _shadowResolution + 9));
            game.RebuildCascades();
        }

        if (Renderers[_selectedRenderer] != GetFna3DRenderer())
        {
            requireRestart = true;
        }

        if (graphicsChanged)
        {
            game.Graphics.ApplyChanges();
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
        World.DistantOutlineBehavior = _distantOutlineBehavior;
    }

    /// <summary>
    /// Saves config and returns whether a restart is required (e.g., renderer change).
    /// Call from SettingsHandler when the user clicks OK or Apply.
    /// </summary>
    public static bool SaveConfigAndCheckRestart()
    {
        ApplySettings(out var requireRestart);
        SaveConfig();
        return requireRestart;
    }

    public static void SaveConfig()
    {
        var content = SaveConfigToString();
        try
        {
            var configPath = Path.Combine("data", "cfg", "config.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, content);
            Logging.Debug($"Config saved to {configPath}");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error saving config: {ex.Message}");
        }
    }

    /// <summary>
    /// Serialize current settings to the config.cfg format as a string.
    /// Used to snapshot state before editing so Cancel can revert.
    /// </summary>
    public static string SaveConfigToString()
    {
        SyncRuntimeOutlineSettings();

        using var sw = new StringWriter();
        sw.WriteLine("// NFM-World Configuration File");
        sw.WriteLine();
        sw.WriteLine("// Video Settings");
        sw.WriteLine($"video_renderer2 {Renderers[_selectedRenderer]}");
        sw.WriteLine($"video_resolution3 {Resolutions[_selectedResolution]}");
        sw.WriteLine($"video_displaymode {_selectedDisplayMode}");
        sw.WriteLine($"video_vsync {(_vsync ? 1 : 0)}");
        sw.WriteLine($"video_antialias {_antialias}");
        sw.WriteLine($"video_fps {_fpsLimit}");
        sw.WriteLine($"video_linewidth2 {_lineWidth.ToString("F4", CultureInfo.InvariantCulture)}");
        sw.WriteLine($"video_distant_outline_behavior {GetDistantOutlineBehaviorConfigValue()}");
        sw.WriteLine($"video_shadow_cascade {_shadowCascadeLevel}");
        sw.WriteLine($"video_shadow_res {_shadowResolution}");
        sw.WriteLine($"video_low_latency {(_lowLatency ? 1 : 0)}");
        sw.WriteLine($"video_render_distance {_renderDistance}");
        sw.WriteLine();
        sw.WriteLine("// Audio Settings");
        sw.WriteLine($"audio_mute {(_muteAll ? 1 : 0)}");
        sw.WriteLine($"audio_master {_masterVolume.ToString("F2", CultureInfo.InvariantCulture)}");
        sw.WriteLine($"audio_music {_musicVolume.ToString("F2", CultureInfo.InvariantCulture)}");
        sw.WriteLine($"audio_effects {_effectsVolume.ToString("F2", CultureInfo.InvariantCulture)}");
        sw.WriteLine($"audio_remaster {(_remasteredMusic ? 1 : 0)}");
        sw.WriteLine();
        sw.WriteLine("// Camera Settings");
        sw.WriteLine($"camera_fov {_fov.ToString("F1", CultureInfo.InvariantCulture)}");
        sw.WriteLine($"camera_follow_y {_followY}");
        sw.WriteLine($"camera_follow_z {_followZ}");
        sw.WriteLine($"camera_smooth_fov {(_smoothFov ? 1 : 0)}");
        sw.WriteLine();
        sw.WriteLine("// Key Bindings");
        sw.WriteLine($"key_accelerate {(int)Bindings.Accelerate}");
        sw.WriteLine($"key_ab {(int)Bindings.AerialBounce}");
        sw.WriteLine($"key_smoothturn {(int)Bindings.AerialStrafe}");
        sw.WriteLine($"key_brake {(int)Bindings.Brake}");
        sw.WriteLine($"key_turnleft {(int)Bindings.TurnLeft}");
        sw.WriteLine($"key_turnright {(int)Bindings.TurnRight}");
        sw.WriteLine($"key_handbrake {(int)Bindings.Handbrake}");
        sw.WriteLine($"key_lookback {(int)Bindings.LookBack}");
        sw.WriteLine($"key_lookleft {(int)Bindings.LookLeft}");
        sw.WriteLine($"key_lookright {(int)Bindings.LookRight}");
        sw.WriteLine($"key_togglemusic {(int)Bindings.ToggleMusic}");
        sw.WriteLine($"key_togglesfx {(int)Bindings.ToggleSFX}");
        sw.WriteLine($"key_togglearrace {(int)Bindings.ToggleArrace}");
        sw.WriteLine($"key_toggleradar {(int)Bindings.ToggleRadar}");
        sw.WriteLine($"key_cycleview {(int)Bindings.CycleView}");
        sw.WriteLine($"key_console {(int)Bindings.ToggleDevConsole}");
        return sw.ToString();
    }

    /// <summary>
    /// Restore settings from a previously-saved config string.
    /// Used to revert changes when Cancel is clicked.
    /// </summary>
    public static void LoadConfigFromSnapshot(string configString)
    {
        using var sr = new StringReader(configString);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                continue;

            var parts = trimmed.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            ParseConfigLine(parts[0], parts[1]);
        }
        ApplySettings(out _);
    }

    /// <summary>
    /// Parse a single "key value" config line and update the corresponding static field.
    /// </summary>
    private static void ParseConfigLine(string key, string value)
    {
        try
        {
            switch (key)
            {
                // Video
                case "video_renderer2":
                    _selectedRenderer = Array.IndexOf(_resolutions, value) is var r and > -1 ? r : _selectedRenderer;
                    break;
                case "video_resolution3":
                    _selectedResolution = Array.IndexOf(_resolutions, value) is var resIdx and > -1 ? resIdx : _selectedResolution;
                    break;
                case "video_displaymode":
                    _selectedDisplayMode = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "video_vsync":
                    _vsync = int.Parse(value) != 0;
                    break;
                case "video_antialias":
                    _antialias = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "video_fps":
                    _fpsLimit = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "video_linewidth2":
                    _lineWidth = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "video_distant_outline_behavior":
                    _distantOutlineBehavior = ParseDistantOutlineBehavior(value, _distantOutlineBehavior);
                    break;
                case "video_shadow_cascade":
                    _shadowCascadeLevel = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "video_shadow_res":
                    _shadowResolution = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "video_low_latency":
                    _lowLatency = int.Parse(value) != 0;
                    break;
                case "video_render_distance":
                    _renderDistance = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                // Audio
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
                // Camera
                case "camera_fov":
                    _fov = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "camera_follow_y":
                    _followY = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "camera_follow_z":
                    _followZ = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "camera_smooth_fov":
                    _smoothFov = int.Parse(value) != 0;
                    break;
                // Key bindings
                case "key_accelerate":
                    Bindings.Accelerate = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_ab":
                    Bindings.AerialBounce = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_smoothturn":
                    Bindings.AerialStrafe = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_brake":
                    Bindings.Brake = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_turnleft":
                    Bindings.TurnLeft = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_turnright":
                    Bindings.TurnRight = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_handbrake":
                    Bindings.Handbrake = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_lookback":
                    Bindings.LookBack = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_lookleft":
                    Bindings.LookLeft = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_lookright":
                    Bindings.LookRight = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_togglemusic":
                    Bindings.ToggleMusic = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_togglesfx":
                    Bindings.ToggleSFX = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_togglearrace":
                    Bindings.ToggleArrace = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_toggleradar":
                    Bindings.ToggleRadar = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_cycleview":
                    Bindings.CycleView = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "key_console":
                    Bindings.ToggleDevConsole = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                    break;
            }
        }
        catch { /* skip malformed lines */ }
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
                    SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", selectedRenderer);
                    break;
                case "OpenGL 2.1":
                    Logging.Info($"Overriding FNA3D renderer to {selectedRenderer}");
                    SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    break;
                case "OpenGL 4.6":
                    Logging.Info($"Overriding FNA3D renderer to {selectedRenderer} (Core Profile)");
                    SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_CORE_PROFILE", "1");
                    break;
                case "OpenGL ES 3.0":
                    Logging.Info($"Overriding FNA3D renderer to {selectedRenderer} (ES3)");
                    SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "OpenGL");
                    SDL.SDL_SetHint("FNA3D_OPENGL_FORCE_ES3", "1");
                    break;
            }
        }
    }

    private static string GetFna3DRenderer()
    {
        var driver = SDL.SDL_GetHint("FNA3D_FORCE_DRIVER");

        return driver switch
        {
            "D3D11" or "D3D12" or "Vulkan" => driver,
            "OpenGL" when SDL.SDL_GetHint("FNA3D_OPENGL_FORCE_CORE_PROFILE") == "1" => "OpenGL 4.6",
            "OpenGL" when SDL.SDL_GetHint("FNA3D_OPENGL_FORCE_ES3") == "1" => "OpenGL ES 3.0",
            "OpenGL" => "OpenGL 2.1",
            _ => "Auto"
        };
    }

    private static string GetDistantOutlineBehaviorConfigValue()
    {
        return _distantOutlineBehavior switch
        {
            DistantOutlineBehavior.DistanceFalloff => "distance_falloff",
            DistantOutlineBehavior.DistanceFalloffWithCutoff => "distance_falloff_with_cutoff",
            DistantOutlineBehavior.ClassicCutoff => "classic_cutoff",
            DistantOutlineBehavior.AlwaysRender => "always_render",
            DistantOutlineBehavior.HideOutlines => "hide_outlines",
            _ => "distance_falloff_with_cutoff"
        };
    }

    private static string GetDistantOutlineBehaviorDisplayName(DistantOutlineBehavior behavior)
    {
        return behavior switch
        {
            DistantOutlineBehavior.DistanceFalloff => "Distance Falloff",
            DistantOutlineBehavior.DistanceFalloffWithCutoff => "Distance Falloff (With Cutoff)",
            DistantOutlineBehavior.ClassicCutoff => "Classic Cutoff (NFM)",
            DistantOutlineBehavior.AlwaysRender => "Always Render",
            DistantOutlineBehavior.HideOutlines => "Hide Outlines",
            _ => behavior.ToString()
        };
    }

    private static DistantOutlineBehavior ParseDistantOutlineBehavior(
        string value,
        DistantOutlineBehavior fallback)
    {
        return value.ToLowerInvariant() switch
        {
            "distance_falloff" => DistantOutlineBehavior.DistanceFalloff,
            "distance_falloff_with_cutoff" => DistantOutlineBehavior.DistanceFalloffWithCutoff,
            "classic_cutoff" => DistantOutlineBehavior.ClassicCutoff,
            "always_render" => DistantOutlineBehavior.AlwaysRender,
            "hide_outlines" => DistantOutlineBehavior.HideOutlines,
            _ => fallback
        };
    }

    public static void LoadConfig()
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
                            _selectedDisplayMode = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_vsync":
                            _vsync = int.Parse(value) != 0;
                            break;
                        case "video_antialias":
                            _antialias = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_fps":
                            _fpsLimit = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_linewidth2":
                            _lineWidth = float.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_distant_outline_behavior":
                            _distantOutlineBehavior = ParseDistantOutlineBehavior(value, _distantOutlineBehavior);
                            break;
                        case "video_shadow_cascade":
                            _shadowCascadeLevel = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_shadow_res":
                            _shadowResolution = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "video_low_latency":
                            _lowLatency = int.Parse(value) != 0;
                            break;
                        case "video_render_distance":
                            _renderDistance = int.Parse(value, CultureInfo.InvariantCulture);
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
                            _followY = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "camera_follow_z":
                            _followZ = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "camera_smooth_fov":
                            _smoothFov = int.Parse(value) != 0;
                            break;

                        // Key bindings
                        case "key_accelerate":
                            Bindings.Accelerate = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_ab":
                            Bindings.AerialBounce = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_smoothturn":
                            Bindings.AerialStrafe = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_brake":
                            Bindings.Brake = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_turnleft":
                            Bindings.TurnLeft = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_turnright":
                            Bindings.TurnRight = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_handbrake":
                            Bindings.Handbrake = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_lookback":
                            Bindings.LookBack = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_lookleft":
                            Bindings.LookLeft = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_lookright":
                            Bindings.LookRight = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_togglemusic":
                            Bindings.ToggleMusic = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_togglesfx":
                            Bindings.ToggleSFX = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_togglearrace":
                            Bindings.ToggleArrace = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_console":
                            Bindings.ToggleDevConsole = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_toggleradar":
                            Bindings.ToggleRadar = (Key)int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        case "key_cycleview":
                            Bindings.CycleView = (Key)int.Parse(value, CultureInfo.InvariantCulture);
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

    // ── Static API for CEF SettingsHandler ──────────────────────────

    /// <summary>
    /// Snapshot of all current settings for serialization to JS.
    /// </summary>
    public static SettingsSnapshot GetCurrentSnapshot()
    {
        SyncRuntimeOutlineSettings();

        var bindingProps = typeof(KeyBindings).GetProperties();
        var keyBindings = new KeyBindingData[bindingProps.Length];
        for (var i = 0; i < bindingProps.Length; i++)
        {
            var prop = bindingProps[i];
            keyBindings[i] = new KeyBindingData
            {
                Action = prop.Name,
                DisplayName = prop.Name, // JS can localize
                KeyCode = (int)(prop.GetValue(Bindings) as Key? ?? Key.None)
            };
        }

        return new SettingsSnapshot
        {
            SelectedRenderer = _selectedRenderer,
            SelectedResolution = _selectedResolution,
            SelectedDisplayMode = _selectedDisplayMode,
            Vsync = _vsync,
            FpsLimit = _fpsLimit,
            Antialias = _antialias,
            ShadowCascadeLevel = _shadowCascadeLevel,
            ShadowResolution = _shadowResolution,
            RenderDistance = _renderDistance,
            LowLatency = _lowLatency,
            LineWidth = _lineWidth,
            DistantOutlineBehavior = (int)_distantOutlineBehavior,
            MasterVolume = _masterVolume,
            MusicVolume = _musicVolume,
            EffectsVolume = _effectsVolume,
            MuteAll = _muteAll,
            RemasteredMusic = _remasteredMusic,
            Fov = _fov,
            FollowY = _followY,
            FollowZ = _followZ,
            SmoothFov = _smoothFov,
            KeyBindings = keyBindings
        };
    }

    private static void SyncRuntimeOutlineSettings()
    {
        // Console commands can change these directly, so refresh the model before snapshotting or saving it.
        _lineWidth = World.OutlineThickness;
        _distantOutlineBehavior = World.DistantOutlineBehavior;
    }

    /// <summary>
    /// Lists of valid choices for dropdowns, for the current OS platform.
    /// </summary>
    public static AvailableOptions GetAvailableOptions()
    {
        return new AvailableOptions
        {
            Renderers = Renderers,
            Resolutions = Resolutions,
            DisplayModes = DisplayModes,
            AntialiasModes = AntialiasModes,
            ShadowCascadeLevels = ShadowCascadeLevelNames,
            ShadowResolutions = ShadowResolutionNames,
            RenderDistanceNames = RenderDistanceNames
        };
    }

    /// <summary>
    /// Apply a single setting change from JS. Key is the setting name,
    /// value is parsed from the JsonElement.
    /// </summary>
    public static void ApplySetting(string key, System.Text.Json.JsonElement args)
    {
        switch (key)
        {
            // Video
            case "selectedRenderer":
                if (args.TryGetProperty("value", out var v) && v.TryGetInt32(out var iv))
                    _selectedRenderer = iv;
                break;
            case "selectedResolution":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _selectedResolution = iv;
                break;
            case "selectedDisplayMode":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _selectedDisplayMode = iv;
                break;
            case "vsync":
                if (args.TryGetProperty("value", out v) && v.ValueKind == System.Text.Json.JsonValueKind.True || v.ValueKind == System.Text.Json.JsonValueKind.False)
                    _vsync = v.GetBoolean();
                break;
            case "fpsLimit":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _fpsLimit = iv;
                break;
            case "antialias":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _antialias = iv;
                break;
            case "shadowCascadeLevel":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _shadowCascadeLevel = iv;
                break;
            case "shadowResolution":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _shadowResolution = iv;
                break;
            case "renderDistance":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _renderDistance = iv;
                break;
            case "lowLatency":
                if (args.TryGetProperty("value", out v))
                    _lowLatency = v.GetBoolean();
                break;
            case "lineWidth":
                if (args.TryGetProperty("value", out v) && v.TryGetSingle(out var fv))
                    _lineWidth = fv;
                break;
            case "distantOutlineBehavior":
                if (args.TryGetProperty("value", out v) &&
                    v.TryGetInt32(out iv) &&
                    Enum.IsDefined(typeof(DistantOutlineBehavior), iv))
                    _distantOutlineBehavior = (DistantOutlineBehavior)iv;
                break;

            // Audio
            case "masterVolume":
                if (args.TryGetProperty("value", out v) && v.TryGetSingle(out fv))
                    _masterVolume = fv;
                break;
            case "musicVolume":
                if (args.TryGetProperty("value", out v) && v.TryGetSingle(out fv))
                    _musicVolume = fv;
                break;
            case "effectsVolume":
                if (args.TryGetProperty("value", out v) && v.TryGetSingle(out fv))
                    _effectsVolume = fv;
                break;
            case "muteAll":
                if (args.TryGetProperty("value", out v))
                    _muteAll = v.GetBoolean();
                break;
            case "remasteredMusic":
                if (args.TryGetProperty("value", out v))
                    _remasteredMusic = v.GetBoolean();
                break;

            // Camera
            case "fov":
                if (args.TryGetProperty("value", out v) && v.TryGetSingle(out fv))
                    _fov = fv;
                break;
            case "followY":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _followY = iv;
                break;
            case "followZ":
                if (args.TryGetProperty("value", out v) && v.TryGetInt32(out iv))
                    _followZ = iv;
                break;
            case "smoothFov":
                if (args.TryGetProperty("value", out v))
                    _smoothFov = v.GetBoolean();
                break;

            // Key binding
            case "keyBinding":
                if (args.TryGetProperty("action", out var actionProp)
                    && args.TryGetProperty("keyCode", out var codeProp)
                    && codeProp.TryGetInt32(out var keyCode))
                {
                    var action = actionProp.GetString() ?? "";
                    var prop = typeof(KeyBindings).GetProperty(action);
                    prop?.SetValue(Bindings, (Key)keyCode);
                }
                break;
        }

        // Apply immediately for live preview
        ApplySettings(out _);
    }

    /// <summary>
    /// Reset camera settings to defaults.
    /// </summary>
    public static void ResetCameraDefaults()
    {
        _fov = 90.0f;
        _smoothFov = true;
        _followY = 0;
        _followZ = 0;
        ApplySettings(out _);
    }
}
