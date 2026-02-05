using nfm_world_library.Lua;
using nfm_world_library.SoftFloat;

namespace nfm_world_library.backend.gamemodes;

[LuaVisible]
[method: LuaHidden]
public class LuaGamemode(string path, BaseGamemodeParameters gamemodeParameters, IRaceValues raceValues, bool isClient = false)
    : BaseGamemode(gamemodeParameters, raceValues)
{
    [LuaHidden]
    public override event EventHandler<byte[]>? RaceFinished;

    public event Action? OnEnter;
    public event Action? OnExit;
    public event Action? OnGameTick;
    public event Action? OnReset;
    
    public readonly bool IsClient = isClient;

    [LuaHidden] public LuaTable GamemodeTable;
    private readonly string _path = path;

    public void FinishRace(byte[] playerStandings)
    {
        RaceFinished?.Invoke(this, playerStandings);
    }

    public BackendCar CreateBackendCar(string name, int idx, fix64 x, fix64 y)
    {
        return new BackendCar(BackendGameSparker.GetCar(name).Rad!, idx, x, y, idx == playerCarIndex);
    }
    
    [LuaHidden]
    public override void Enter()
    {
        GamemodeTable = LuaManager.LoadLuaWithContext(path, new Dictionary<string, LuaGamemode>()
        {
            ["GM"] = this
        });
        base.Enter();
        OnEnter?.Invoke();
    }

    [LuaHidden]
    public override void Exit()
    {
        base.Exit();
        OnExit?.Invoke();
    }

    [LuaHidden]
    public override void GameTick()
    {
        base.GameTick();
        OnGameTick?.Invoke();
    }

    public override void Reset()
    {
        base.Reset();
        OnReset?.Invoke();
    }
}