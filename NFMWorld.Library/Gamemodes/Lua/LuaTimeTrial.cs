using nfm_world_library.Lua;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Files;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// C# companion for the Lua time trial: ghost replay and best-time recording.
/// Demo serialization stays in C#; the script drives it per tick.
/// </summary>
[LuaVisible]
public sealed partial class LuaTimeTrial
{
    private readonly BackendStage _stage;
    private SavedTimeTrial? _best;
    private SavedTimeTrial? _current;

    [LuaHidden]
    public LuaTimeTrial(BackendStage stage)
        => _stage = stage;

    /// <summary>Loads the best saved run (ghost source) and starts recording.</summary>
    [LuaName("begin")]
    public void Begin(IInGameCar car)
    {
        _best = SavedTimeTrial.Load(car.Player.CarName, _stage.Path);
        _current = new SavedTimeTrial(car.Player.CarName, _stage.Path, _stage.stageLoader, car.Rad);
    }

    [LuaName("hasGhost")]
    public bool HasGhost => _best is not null;

    /// <summary>Applies the ghost's recorded controls for the given tick.</summary>
    [LuaName("applyGhost")]
    public void ApplyGhost(IInGameCar ghostCar, int tick)
    {
        if (_best?.GetTick(tick) is { } controls)
            ghostCar.Control.Decode(controls);
    }

    /// <summary>Records the player car's state for the current tick.</summary>
    [LuaName("record")]
    public void Record(IInGameCar car)
        => _current?.RecordTick(car);

    /// <summary>Saves the run if it beats the previous best.</summary>
    [LuaName("save")]
    public void Save()
    {
        if (_best is null ||
            (_current is not null &&
             _current.GetSplitDiff(_best, _current.Splits.SplitTimes.Count - 1) < 0))
        {
            _current?.Save();
        }
    }
}
