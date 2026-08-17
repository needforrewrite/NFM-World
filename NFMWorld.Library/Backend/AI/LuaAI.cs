using Lua;
using Lua.Standard;
using nfm_world_library.Lua;
using NFMWorld.LuaSourceGenerator.Generator;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.Lua;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.AI;

[LuaVisible, LuaName("AiContext")]
public partial class LuaAiContext(BaseClientGamemode gamemode, ClientSidePlayer aiPlayer, LuaAi ai)
{
    [LuaName("players")]
    public LuaList<ClientSidePlayer> Players { get; } = new(gamemode.Players);

    [LuaName]
    public ClientSidePlayer Player => aiPlayer;
    
    [LuaName("stage")]
    public BackendStage CurrentStage => gamemode.CurrentStage;
    
    [LuaName]
    public LuaTable? Config { get; } = ai.Config;
}

public class LuaAi : BaseAi
{
    private readonly string _scriptPath;
    private readonly LuaState _state;

    public LuaTable? Config { get; set; }

    public LuaAi(BaseClientGamemode gamemode, ClientSidePlayer aiPlayer, string scriptPath, LuaTable? config = null)
    {
        _scriptPath = scriptPath;

        _state = LuaState.Create(LuaNfmwPlatform.Instance);
        _state.OpenStandardLibraries();
        LuaVisibleTypeRegistry.RegisterAll(_state);

        _state.Environment["AI"] = new LuaAiContext(gamemode, aiPlayer, this);

        Config = config;

        _state.DoFile($"data/ais/{_scriptPath}.lua");
    }

    public override void RunAi()
    {
        Call("RunAi");
    }

    // ── Script invocation ──────────────────────────────────────────

    private void RegisterFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> fn)
        => _state.Environment[name] = new LuaFunction(name, fn);

    private LuaValue[] Call(string name, params ReadOnlySpan<LuaValue> arguments)
    {
        if (!_state.Environment.TryGetValue(name, out var value) ||
            !value.TryRead<LuaFunction>(out var function))
        {
            return [LuaValue.Nil];
        }

        try
        {
            return _state.Call(function, arguments);
        }
        catch (Exception ex)
        {
            Logging.Error($"[LuaGamemode:{_scriptPath}] {name} failed: {ex.Message}");
        }
        return [LuaValue.Nil];
    }
}