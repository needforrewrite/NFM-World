namespace NFMWorld.UI.Settings;

/// <summary>
/// Complete snapshot of all current settings.
/// </summary>
public sealed class SettingsSnapshot
{
    // Video
    public int SelectedRenderer { get; set; }
    public int SelectedResolution { get; set; }
    public string BorderlessResolution { get; set; } = "";
    public int SelectedDisplayMode { get; set; }
    public bool Vsync { get; set; }
    public int FpsLimit { get; set; }
    public int Antialias { get; set; }
    public int AppliedAntialias { get; set; }
    public bool AntialiasRequiresRestart { get; set; }
    public int ShadowCascadeLevel { get; set; }
    public int ShadowResolution { get; set; }
    public int RenderDistance { get; set; }
    public bool LowLatency { get; set; }
    public float LineWidth { get; set; }
    public int DistantOutlineBehavior { get; set; }

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
    public IReadOnlyList<KeyBindingData> KeyBindings { get; set; } = [];
}

/// <summary>A single key binding.</summary>
public sealed class KeyBindingData
{
    /// <summary>Property name on <c>SettingsMenu.KeyBindings</c> (e.g. "Accelerate").</summary>
    public string Action { get; set; } = "";

    /// <summary>Display name, shown in the settings list.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>SDL <c>Key</c> enum value.</summary>
    public int KeyCode { get; set; }
}

/// <summary>Valid choices for each dropdown, for the current platform.</summary>
public sealed class AvailableOptions
{
    public IReadOnlyList<string> Renderers { get; set; } = [];
    public IReadOnlyList<string> Resolutions { get; set; } = [];
    public IReadOnlyList<string> DisplayModes { get; set; } = [];
    public IReadOnlyList<string> AntialiasModes { get; set; } = [];
    public IReadOnlyList<string> ShadowCascadeLevels { get; set; } = [];
    public IReadOnlyList<string> ShadowResolutions { get; set; } = [];
    public IReadOnlyList<string> RenderDistanceNames { get; set; } = [];
}
