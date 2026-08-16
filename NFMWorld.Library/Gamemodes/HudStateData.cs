using Lua;
using MemoryPack;
using nfm_world_library.Lua;

namespace NFMWorld.DriverInterface;

/// <summary>
/// Per-frame HUD state sent from the gamemode to the CEF race overlay.
/// </summary>
[MemoryPackable]
[GenerateTypeScript]
[LuaVisible]
public partial class HudStateData
{
    [LuaName("speed")] public float Speed { get; set; }
    [LuaName("power")] public float Power { get; set; }
    [LuaName("damage")] public float Damage { get; set; }
    [LuaName("lap")] public int Lap { get; set; }
    [LuaName("totalLaps")] public int TotalLaps { get; set; }
    [LuaName("lapTime")] public int LapTime { get; set; }
    [LuaName("position")] public int Position { get; set; }
    [LuaName("totalRacers")] public int TotalRacers { get; set; }
    [LuaName("stateText")] public string? StateText { get; set; }
    public DateTime? StateTextEndsAt { get; set; }
    [LuaName("lapDiffMs")] public int? LapDiffMs { get; set; }
    [LuaName("lastLapDiffMs")] public int? LastLapDiffMs { get; set; }
    [LuaName("chkDiffMs")] public int? ChkDiffMs { get; set; }
    [LuaName("lastChkDiffMs")] public int? LastChkDiffMs { get; set; }
    [LuaName("countdownTimer")] public int CountdownTimer { get; set; }

    [MemoryPackIgnore]
    [LuaName("stateTextEndsAt")]
    public double? LuaStateTextEndsAt
    {
        get => (StateTextEndsAt?.ToUniversalTime() - DateTime.UnixEpoch)?.TotalSeconds;
        set => StateTextEndsAt = value != null ? DateTime.UnixEpoch.AddSeconds(value.Value) : null;
    }
}
