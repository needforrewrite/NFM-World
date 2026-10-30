using NFMWorld.PluginSystem;

namespace NFMWorld.DriverInterface;

/// <summary>
/// Per-frame HUD state sent from the gamemode to the CEF race overlay.
/// </summary>
[PluginVisible]
public partial class HudStateData
{
    [PluginVisible] public float Speed { get; set; }
    [PluginVisible] public float Power { get; set; }
    [PluginVisible] public float Damage { get; set; }
    [PluginVisible] public int Lap { get; set; }
    [PluginVisible] public int TotalLaps { get; set; }
    [PluginVisible] public int LapTime { get; set; }
    [PluginVisible] public int Position { get; set; }
    [PluginVisible] public int TotalRacers { get; set; }
    [PluginVisible] public string? StateText { get; set; }
    [PluginVisible] public DateTime? StateTextEndsAt { get; set; }
    [PluginVisible] public int CountdownTimer { get; set; }

    [PluginVisible]
    public double? LuaStateTextEndsAt
    {
        get => (StateTextEndsAt?.ToUniversalTime() - DateTime.UnixEpoch)?.TotalSeconds;
        set => StateTextEndsAt = value != null ? DateTime.UnixEpoch.AddSeconds(value.Value) : null;
    }
}
