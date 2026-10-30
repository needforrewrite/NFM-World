using NFMWorld.DriverInterface;
using NFMWorldLibrary.Util;
using WorldXaml.UI.Yoga;

namespace NFMWorld.UI.Cef;

/// <summary>
/// Settings sub-handler — handles settings read/write, key binding capture,
/// and confirmation dialogs. Composable into any <see cref="PhaseBridge"/>
/// via <see cref="PhaseBridge.AddSubHandler"/>.
///
/// Uses a hardcoded <c>"settings"</c> event prefix for all C#→JS pushes,
/// so the frontend listens to <c>"settings:config"</c>, <c>"settings:options"</c>,
/// etc. regardless of which parent phase hosts it.
/// </summary>
public sealed class SettingsHandler : ISubHandler
{
    private UiRenderer? _renderer;
    private string? _capturingAction;
    private string? _originalConfig;

    public bool IsCapturing => _capturingAction != null;

    // ── ISubHandler ──────────────────────────────────────────────

    public bool TryHandleMessage(string type, LuaValue args)
    {
        switch (type)
        {
            case "getConfig":
                PushInitialState();
                return true;
            case "applySetting":
                ApplySettingFromJs(args);
                return true;
            case "saveConfig":
                var requireRestart = SettingsMenu.SaveConfigAndCheckRestart();
                _originalConfig = null; // re-captured on next getConfig if needed
                if (requireRestart)
                    Push("requireRestart", true);
                else
                    Push("saved", true);
                return true;
            case "close":
                if (_originalConfig != null)
                    SettingsMenu.LoadConfigFromSnapshot(_originalConfig);
                CloseRequested?.Invoke();
                return true;
            case "restartNow":
                RestartConfirmed?.Invoke();
                return true;
            case "startCapture":
                if (args.TryRead<LuaTable>(out var a) && a.TryGetValue("action", out var action))
                    _capturingAction = action.ReadOrDefault<string>();
                return true;
            case "stopCapture":
                _capturingAction = null;
                return true;
            case "resetDefaults":
                HandleResetDefaults(args);
                return true;
            default:
                return false;
        }
    }

    public void OnActivated(UiRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        SettingsMenu.ResolutionsChanged += OnResolutionsChanged;
    }

    public void OnDeactivated()
    {
        SettingsMenu.ResolutionsChanged -= OnResolutionsChanged;
        _renderer = null;
    }

    public bool TryHandleKeyPress(Key key)
    {
        if (!IsCapturing) return false;
        HandleCapturedKey(key);
        return true;
    }

    // ── Public API (called by hosting phase / bridge) ─────────────

    /// <summary>
    /// Push current settings snapshot and available options to JS.
    /// Called on initial activation and when JS requests a refresh.
    /// </summary>
    public void PushInitialState()
    {
        _originalConfig ??= SettingsMenu.SaveConfigToString();

        var snapshot = SettingsMenu.GetCurrentSnapshot();
        Push("config", snapshot.ToLuaValue());

        var options = SettingsMenu.GetAvailableOptions();
        Push("options", options);
    }

    /// <summary>
    /// Set the current key capture action. Called by the hosting phase's
    /// KeyPressed when a key is pressed during capture (routed via
    /// <see cref="TryHandleKeyPress"/>).
    /// </summary>
    public void HandleCapturedKey(Key key)
    {
        if (_capturingAction == null) return;

        if (key == Key.Escape)
        {
            _capturingAction = null;
            Push("keyCaptured", new CapturedKey { Action = null, KeyCode = (int)Key.None, Cancelled = true });
            return;
        }

        // Resolve conflicts: clear any existing binding that uses this key
        var allProps = typeof(SettingsMenu.KeyBindings).GetProperties();
        foreach (var prop in allProps)
        {
            if (prop.Name != _capturingAction
                && prop.GetValue(SettingsMenu.Bindings) is Key existingKey
                && existingKey == key)
            {
                prop.SetValue(SettingsMenu.Bindings, Key.None);
            }
        }

        // Set the new binding
        var property = typeof(SettingsMenu.KeyBindings).GetProperty(_capturingAction);
        property?.SetValue(SettingsMenu.Bindings, key);

        var capturedAction = _capturingAction;
        _capturingAction = null;
        Push("keyCaptured", new CapturedKey { Action = capturedAction, KeyCode = (int)key, Cancelled = false });
    }

    // ── Private helpers ───────────────────────────────────────────

    private void ApplySettingFromJs(LuaValue args)
    {
        if (!args.TryRead<LuaTable>(out var a) || !a.TryGetValue("key", out var keyProp))
            return;

        var key = keyProp.ReadOrDefault<string>() ?? "";
        SettingsMenu.ApplySetting(key, a);
    }

    private void HandleResetDefaults(LuaValue args)
    {
        if (!args.TryRead<LuaTable>(out var a) || !a.TryGetValue("section", out var sectionProp))
            return;

        var section = sectionProp.ReadOrDefault<string>() ?? "";
        switch (section)
        {
            case "keyboard":
                SettingsMenu.Bindings = new SettingsMenu.KeyBindings();
                break;
            case "camera":
                SettingsMenu.ResetCameraDefaults();
                break;
        }
        PushInitialState();
    }

    private void OnResolutionsChanged()
    {
        var options = SettingsMenu.GetAvailableOptions();
        Push("options", options);
        var snapshot = SettingsMenu.GetCurrentSnapshot();
        Push("config", snapshot.ToLuaValue());
    }

    // ── Push helpers (hardcoded "settings" prefix) ────────────────

    private void Push(string eventType, LuaValue data)
    {
        _renderer?.PushToLua("settings", eventType, data);
    }

    // ── Events ────────────────────────────────────────────────────

    /// <summary>Fired when the user closes settings (Cancel/Back).</summary>
    public event Action? CloseRequested;

    /// <summary>Fired when the user confirms they want to restart now.</summary>
    public event Action? RestartConfirmed;
}

// ── MemoryPack data models ────────────────────────────────────────

[PluginVisible]
public sealed partial class CapturedKey
{
    [PluginVisible] public string? Action { get; set; }
    [PluginVisible] public int KeyCode { get; set; }
    [PluginVisible] public bool Cancelled { get; set; }
}

/// <summary>
/// Complete snapshot of all current settings, sent from C# to JS.
/// </summary>
[LuaTableConvertible]
public sealed partial class SettingsSnapshot
{
    // Video
    [PluginVisible] public int SelectedRenderer { get; set; }
    [PluginVisible] public int SelectedResolution { get; set; }
    [PluginVisible] public int SelectedDisplayMode { get; set; }
    [PluginVisible] public bool Vsync { get; set; }
    [PluginVisible] public int FpsLimit { get; set; }
    [PluginVisible] public int Antialias { get; set; }
    [PluginVisible] public int ShadowCascadeLevel { get; set; }
    [PluginVisible] public int ShadowResolution { get; set; }
    [PluginVisible] public int RenderDistance { get; set; }
    [PluginVisible] public bool LowLatency { get; set; }
    [PluginVisible] public float LineWidth { get; set; }

    // Audio
    [PluginVisible] public float MasterVolume { get; set; }
    [PluginVisible] public float MusicVolume { get; set; }
    [PluginVisible] public float EffectsVolume { get; set; }
    [PluginVisible] public bool MuteAll { get; set; }
    [PluginVisible] public bool RemasteredMusic { get; set; }

    // Game (Camera)
    [PluginVisible] public float Fov { get; set; }
    [PluginVisible] public int FollowY { get; set; }
    [PluginVisible] public int FollowZ { get; set; }
    [PluginVisible] public bool SmoothFov { get; set; }

    // Key bindings
    [PluginVisible] public KeyBindingData[] KeyBindings { get; set; } = [];

    [PluginVisible] public int DistantOutlineBehavior { get; set; }
}

/// <summary>
/// Single key binding sent to JS.
/// </summary>
[LuaTableConvertible]
public sealed partial class KeyBindingData
{
    /// <summary>Property name on KeyBindings (e.g., "Accelerate").</summary>
    [PluginVisible] public string Action { get; set; } = "";

    /// <summary>Human-readable display name (e.g., "Accelerate").</summary>
    [PluginVisible] public string DisplayName { get; set; } = "";

    /// <summary>SDL Key enum integer value.</summary>
    [PluginVisible] public int KeyCode { get; set; }
}

/// <summary>
/// Lists of valid choices for each dropdown/slider, sent once on enter.
/// </summary>
[PluginVisible]
public sealed partial class AvailableOptions
{
    [PluginVisible] public LuaArray<string> Renderers { get; set; } = [];
    [PluginVisible] public LuaArray<string> Resolutions { get; set; } = [];
    [PluginVisible] public LuaArray<string> DisplayModes { get; set; } = [];
    [PluginVisible] public LuaArray<string> AntialiasModes { get; set; } = [];
    [PluginVisible] public LuaArray<string> ShadowCascadeLevels { get; set; } = [];
    [PluginVisible] public LuaArray<string> ShadowResolutions { get; set; } = [];
    [PluginVisible] public LuaArray<string> RenderDistanceNames { get; set; } = [];
}
