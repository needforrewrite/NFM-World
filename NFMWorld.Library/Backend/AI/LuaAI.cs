using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.AI;

[LuaVisible, LuaName("AiContext")]
public partial class LuaAiContext(BaseClientGamemode gamemode, ClientSidePlayer aiPlayer, LuaAi ai)
{
    [PluginVisible]
    public LuaList<ClientSidePlayer> Players { get; } = new(gamemode.Players);

    [PluginVisible]
    public ClientSidePlayer Player => aiPlayer;
    
    [PluginVisible]
    public BackendStage CurrentStage => gamemode.CurrentStage;
    
    [PluginVisible]
    public LuaTable? Config { get; } = ai.Config;
}

[PluginVisible]
public class LuaAi : BaseAi
{
    private readonly string _scriptPath;
    private readonly LuaState _state;
    private readonly LuaTable? _moduleTable;

    public LuaTable? Config { get; set; }

    public LuaAi(BaseClientGamemode gamemode, ClientSidePlayer aiPlayer, string scriptPath, LuaTable? config = null)
    {
        _scriptPath = scriptPath;

        _state = LuaHelpers.OpenState();

        _state.Environment["AI"] = new LuaAiContext(gamemode, aiPlayer, this);

        Config = config;

        var results = _state.DoFile($"data/ais/{_scriptPath}.luau");
        if (results is [var value] && value.TryRead<LuaTable>(out var resultTable))
        {
            _moduleTable = resultTable;
        }
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
        if (_moduleTable == null ||
            !_moduleTable.TryGetValue(name, out var value) ||
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
            Logging.Error($"[LuaGamemode:{_scriptPath}] {name} failed: {ex.Message}", ex);
        }
        return [LuaValue.Nil];
    }
}