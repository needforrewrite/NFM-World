using NFMWorld.PluginSystem;
using NFMWorldLibrary.Hud;

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
    /// <summary>One-based position; zero means the gamemode has no position to report.</summary>
    [PluginVisible] public int Position { get; set; }
    [PluginVisible] public int TotalRacers { get; set; }
    [PluginVisible] public string? StateText { get; set; }
    [PluginVisible] public DateTime? StateTextEndsAt { get; set; }
    /// <summary>The race is over; the HUD can show its outcome.</summary>
    [PluginVisible] public bool Finished { get; set; }
    /// <summary>The client crossed the line first.</summary>
    [PluginVisible] public bool ClientWon { get; set; }
    /// <summary>Another car crossed the line first.</summary>
    [PluginVisible] public bool ClientLost { get; set; }
    /// <summary>Transient status line from <see cref="NFMWorldLibrary.Hud.RaceAnnouncer"/>.</summary>
    [PluginVisible] public HudMessage? StatusMessage { get; set; }
    /// <summary>Transient stunt line, shown above the status line.</summary>
    [PluginVisible] public HudMessage? StuntMessage { get; set; }
    [PluginVisible] public int CountdownTimer { get; set; }

    [PluginVisible]
    public double? LuaStateTextEndsAt
    {
        get => (StateTextEndsAt?.ToUniversalTime() - DateTime.UnixEpoch)?.TotalSeconds;
        set => StateTextEndsAt = value != null ? DateTime.UnixEpoch.AddSeconds(value.Value) : null;
    }
}
