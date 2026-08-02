using System.Text.Json;
using MemoryPack;
using NFMWorld.DriverInterface;

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
    private CefRenderer? _renderer;
    private string? _capturingAction;
    private string? _originalConfig;

    public bool IsCapturing => _capturingAction != null;

    // ── ISubHandler ──────────────────────────────────────────────

    public bool TryHandleMessage(string type, JsonElement? args)
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
                if (args is { } a && a.TryGetProperty("action", out var action))
                    _capturingAction = action.GetString();
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

    public void OnActivated(CefRenderer renderer)
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
        PushMemoryPack("config", snapshot);

        var options = SettingsMenu.GetAvailableOptions();
        PushMemoryPack("options", options);
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
            Push("keyCaptured", new { action = (string?)null, keyCode = (int)Key.None, cancelled = true });
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
        Push("keyCaptured", new { action = capturedAction, keyCode = (int)key, cancelled = false });
    }

    // ── Private helpers ───────────────────────────────────────────

    private void ApplySettingFromJs(JsonElement? args)
    {
        if (args is not { } a || !a.TryGetProperty("key", out var keyProp))
            return;

        var key = keyProp.GetString() ?? "";
        SettingsMenu.ApplySetting(key, a);
    }

    private void HandleResetDefaults(JsonElement? args)
    {
        if (args is not { } a || !a.TryGetProperty("section", out var sectionProp))
            return;

        var section = sectionProp.GetString() ?? "";
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
        PushMemoryPack("options", options);
        var snapshot = SettingsMenu.GetCurrentSnapshot();
        PushMemoryPack("config", snapshot);
    }

    // ── Push helpers (hardcoded "settings" prefix) ────────────────

    private void PushMemoryPack<T>(string eventType, T? data)
    {
        _renderer?.PushToJs("settings", eventType, MemoryPackSerializer.Serialize(data));
    }

    private void Push(string eventType, object? data)
    {
        _renderer?.PushToJs("settings", eventType, data);
    }

    // ── Events ────────────────────────────────────────────────────

    /// <summary>Fired when the user closes settings (Cancel/Back).</summary>
    public event Action? CloseRequested;

    /// <summary>Fired when the user confirms they want to restart now.</summary>
    public event Action? RestartConfirmed;
}

// ── MemoryPack data models ────────────────────────────────────────

/// <summary>
/// Complete snapshot of all current settings, sent from C# to JS.
/// </summary>
[MemoryPackable]
[GenerateTypeScript]
public sealed partial class SettingsSnapshot
{
    // Video
    public int SelectedRenderer { get; set; }
    public int SelectedResolution { get; set; }
    public int SelectedDisplayMode { get; set; }
    public bool Vsync { get; set; }
    public int FpsLimit { get; set; }
    public int Antialias { get; set; }
    public int ShadowCascadeLevel { get; set; }
    public int ShadowResolution { get; set; }
    public int RenderDistance { get; set; }
    public bool LowLatency { get; set; }
    public float LineWidth { get; set; }

    // Audio
    public float MasterVolume { get; set; }
    public float MusicVolume { get; set; }
    public float EffectsVolume { get; set; }
    public bool MuteAll { get; set; }
    public bool RemasteredMusic { get; set; }

    // Game (Camera)
    public float Fov { get; set; }
    public int FollowY { get; set; }
    public int FollowZ { get; set; }
    public bool SmoothFov { get; set; }

    // Key bindings
    public KeyBindingData[] KeyBindings { get; set; } = [];

    // Appended for MemoryPack schema compatibility with older snapshots.
    public int DistantOutlineBehavior { get; set; }
}

/// <summary>
/// Single key binding sent to JS.
/// </summary>
[MemoryPackable]
[GenerateTypeScript]
public sealed partial class KeyBindingData
{
    /// <summary>Property name on KeyBindings (e.g., "Accelerate").</summary>
    public string Action { get; set; } = "";

    /// <summary>Human-readable display name (e.g., "Accelerate").</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>SDL Key enum integer value.</summary>
    public int KeyCode { get; set; }
}

/// <summary>
/// Lists of valid choices for each dropdown/slider, sent once on enter.
/// </summary>
[MemoryPackable]
[GenerateTypeScript]
public sealed partial class AvailableOptions
{
    public string[] Renderers { get; set; } = [];
    public string[] Resolutions { get; set; } = [];
    public string[] DisplayModes { get; set; } = [];
    public string[] AntialiasModes { get; set; } = [];
    public string[] ShadowCascadeLevels { get; set; } = [];
    public string[] ShadowResolutions { get; set; } = [];
    public string[] RenderDistanceNames { get; set; } = [];
}
