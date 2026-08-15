using nfm_world_library.Lua;
using NFMWorld.DriverInterface;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Lua-facing view over the race HUD state. Writes through to the
/// gamemode's <see cref="HudStateData"/>, which the race phase pushes to
/// the CEF overlay each frame.
/// </summary>
[LuaVisible]
public sealed partial class LuaHudState
{
    private readonly HudStateData _target;

    [LuaHidden]
    public LuaHudState(HudStateData target) => _target = target;

    [LuaName("speed")] public float Speed { get => _target.Speed; set => _target.Speed = value; }

    [LuaName("power")] public float Power { get => _target.Power; set => _target.Power = value; }

    [LuaName("damage")] public float Damage { get => _target.Damage; set => _target.Damage = value; }

    [LuaName("lap")] public int Lap { get => _target.Lap; set => _target.Lap = value; }

    [LuaName("totalLaps")] public int TotalLaps { get => _target.TotalLaps; set => _target.TotalLaps = value; }

    [LuaName("lapTime")] public int LapTime { get => _target.LapTime; set => _target.LapTime = value; }

    [LuaName("position")] public int Position { get => _target.Position; set => _target.Position = value; }

    [LuaName("totalRacers")] public int TotalRacers { get => _target.TotalRacers; set => _target.TotalRacers = value; }

    [LuaName("stateText")] public string? StateText { get => _target.StateText; set => _target.StateText = value; }

    [LuaName("countdownTimer")] public int CountdownTimer { get => _target.CountdownTimer; set => _target.CountdownTimer = value; }
}
