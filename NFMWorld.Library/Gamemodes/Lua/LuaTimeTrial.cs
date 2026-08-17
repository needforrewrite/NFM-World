using Lua;
using nfm_world_library.Lua;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Files;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// C# companion for the Lua time trial: ghost replay and best-time recording.
/// Demo serialization stays in C#; the script drives it per tick.
/// </summary>
[LuaVisible]
[LuaName("TimeTrial")]
[method: LuaName]
public sealed partial class LuaTimeTrial(BackendStage stage)
{
    private SavedTimeTrial? _best;
    private SavedTimeTrial? _current;

    /// <summary>Loads the best saved run (ghost source) and starts recording.</summary>
    [LuaName("begin")]
    public void Begin(BackendCar car)
    {
        _best = SavedTimeTrial.Load(car.Player.CarName, stage.Path);
        _current = new SavedTimeTrial(car.Player.CarName, stage.Path, stage.StageLoader, car.Rad);
    }

    [LuaName("hasGhost")]
    public bool HasGhost => _best is not null;

    /// <summary>Applies the ghost's recorded controls for the given tick.</summary>
    [LuaName("applyGhost")]
    public void ApplyGhost(BackendCar ghostCar, int tick)
    {
        if (_best?.GetTick(tick) is { } controls)
            ghostCar.Control.Decode(controls);
    }

    [LuaName]
    public double? GetSplitDiff(int splitIndex)
    {
        if (_current is null || _best is null) return null;
        return TimeSpan.FromMilliseconds(_current.GetSplitDiff(_best, splitIndex)).TotalSeconds;
    }

    [LuaName]
    public double? GetLastSplitDiff()
    {
        if (_current is null || _best is null) return null;
        return TimeSpan.FromMilliseconds(_current.GetSplitDiff(_best, _current.Splits.SplitTimes.Count - 1)).TotalSeconds;
    }

    [LuaName]
    public double? GetLapDiff(int lapIndex)
    {
        if (_current is null || _best is null) return null;
        var diff = _current.GetLapTime(stage.Checkpoints.Count, lapIndex) -
                   _best.GetLapTime(stage.Checkpoints.Count, lapIndex - 1);
        return TimeSpan.FromMilliseconds(diff).TotalSeconds;
    }

    [LuaName]
    public void RecordSplit(double splitTime)
        => _current?.RecordSplit((long) TimeSpan.FromSeconds(splitTime).TotalMilliseconds);

    [LuaName]
    public double? GetLapTime(int lapIndex)
    {
        if (_current is null) return null;
        var time = _current.GetLapTime(stage.Checkpoints.Count, lapIndex);
        return TimeSpan.FromMilliseconds(time).TotalSeconds;
    }

    /// <summary>Total elapsed time of the current run at the last recorded split (seconds).</summary>
    [LuaName]
    public double? GetLastSplitTime()
    {
        if (_current is null || _current.Splits.SplitTimes.Count == 0) return null;
        return TimeSpan.FromMilliseconds(_current.Splits.SplitTimes[^1]).TotalSeconds;
    }

    /// <summary>Total elapsed time of the best saved run at its last split (seconds).</summary>
    [LuaName]
    public double? GetBestLastSplitTime()
    {
        if (_best is null || _best.Splits.SplitTimes.Count == 0) return null;
        return TimeSpan.FromMilliseconds(_best.Splits.SplitTimes[^1]).TotalSeconds;
    }

    /// <summary>Records the player car's state for the current tick.</summary>
    [LuaName("record")]
    public void Record(BackendCar car)
        => _current?.RecordTick(car);

    /// <summary>Saves the run if it beats the previous best.</summary>
    [LuaName("save")]
    public void Save()
    {
        if (_best is null ||
            (_current is not null &&
             _current.Splits.SplitTimes.Count > 0 &&
             _current.GetSplitDiff(_best, _current.Splits.SplitTimes.Count - 1) < 0))
        {
            _current?.Save();
        }
    }
}
