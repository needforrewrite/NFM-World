using System.Text;
using Lua;
using Lua.Runtime;
using Lua.Standard;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Server-side gamemode whose authoritative logic is defined by a Lua script loaded at runtime.
/// All <see cref="IServerGamemode"/> methods are delegated to convention-based Lua callbacks.
/// </summary>
/// <remarks>
/// <para>Scripts are loaded from <c>data/gamemodes/{scriptRelativePath}</c> via <c>VFS.ReadAllText</c>.</para>
/// <para>Expected Lua callbacks (all optional):
/// <c>on_begin(ctx)</c>, <c>on_start_race()</c>, <c>on_end()</c>, <c>on_tick()</c>,
/// <c>on_client_event(client_id, payload)</c>, <c>on_get_state_snapshot()</c>.</para>
/// <para>Globals available to the script: <c>ctx</c> (server context table), <c>broadcast</c> (fn).</para>
/// </remarks>
public class LuaServerGamemode : BaseServerGamemode
{
    private readonly string _gamemodeId;
    private readonly string _scriptPath;
    private LuaState? _luaState;
    private bool _luaError;

    /// <summary>
    /// Creates a new Lua-driven server gamemode.
    /// </summary>
    /// <param name="gamemodeId">Unique gamemode identifier (e.g. "nfmm/lua:race").</param>
    /// <param name="scriptRelativePath">
    /// Path to the .lua file, relative to <c>data/gamemodes/</c>.
    /// </param>
    public LuaServerGamemode(string gamemodeId, string scriptRelativePath)
    {
        _gamemodeId = gamemodeId;
        _scriptPath = scriptRelativePath;
    }

    public override string GamemodeId => _gamemodeId;

    // ── Lifecycle ──────────────────────────────────────────────────

    public override void Begin(IServerGamemodeContext context)
    {
        try
        {
            _luaState = LuaState.Create();
            _luaState.OpenBasicLibrary();

            // Register all [LuaVisible] game types as Lua globals.
            LuaVisibleTypeRegistry.RegisterAll(_luaState);

            // Load and execute the gamemode script.
            var fullPath = $"data/gamemodes/{_scriptPath}";
            var scriptContent = VFS.ReadAllText(fullPath);
            _luaState.DoStringAsync(scriptContent, $"@{fullPath}").GetAwaiter().GetResult();

            // Inject server-specific globals.
            SetupServerGlobals(context);

            // Fire on_begin if the script defines it.
            CallLuaCallback("on_begin(ctx)");
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaServerGamemode [{_scriptPath}] Begin failed: {ex.Message}");
            _luaError = true;
        }
    }

    public override void StartRace()
    {
        if (_luaError) return;
        try { CallLuaCallback("on_start_race()"); }
        catch (Exception ex) { Logging.Error($"LuaServerGamemode [{_scriptPath}] on_start_race: {ex.Message}"); _luaError = true; }
    }

    public override void End()
    {
        if (!_luaError)
        {
            try { CallLuaCallback("on_end()"); }
            catch (Exception ex) { Logging.Error($"LuaServerGamemode [{_scriptPath}] on_end: {ex.Message}"); }
        }

        _luaState?.Dispose();
        _luaState = null;
        _luaError = false;
    }

    // ── Tick ───────────────────────────────────────────────────────

    public override void GameTick()
    {
        if (_luaError) return;
        try { CallLuaCallback("on_tick()"); }
        catch (Exception ex) { Logging.Error($"LuaServerGamemode [{_scriptPath}] on_tick: {ex.Message}"); _luaError = true; }
    }

    // ── Client events ──────────────────────────────────────────────

    public override void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        if (_luaError || _luaState == null) return;
        try
        {
            var payloadStr = Convert.ToBase64String(payload);
            _luaState.Environment["_client_id"] = clientId.ToString();
            _luaState.Environment["_payload"] = payloadStr;
            CallLuaCallback("on_client_event(_client_id, _payload)");
        }
        catch (Exception ex) { Logging.Error($"LuaServerGamemode [{_scriptPath}] on_client_event: {ex.Message}"); _luaError = true; }
    }

    // ── State snapshot ─────────────────────────────────────────────

    public override GameStateSnapshot? GetStateSnapshot()
    {
        if (_luaError || _luaState == null) return null;

        try
        {
            var results = _luaState.DoStringAsync("return on_get_state_snapshot()",
                $"@{_scriptPath}").GetAwaiter().GetResult();

            if (results.Length == 0) return null;
            return LuaResultsConverter.ToGameStateSnapshot(results[0]);
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaServerGamemode [{_scriptPath}] on_get_state_snapshot: {ex.Message}");
            _luaError = true;
            return null;
        }
    }

    // ── Broadcasting ───────────────────────────────────────────────

    public override void SetEventBroadcaster(Action<ReadOnlyMemory<byte>> broadcast)
    {
        base.SetEventBroadcaster(broadcast);

        if (_luaError || _luaState == null) return;

        // Expose broadcast as a global Lua function.
        _luaState.Environment["broadcast"] = new LuaFunction("broadcast",
            (ctx, ct) =>
            {
                var payloadStr = ctx.GetArgument<string>(0);
                var payload = Encoding.UTF8.GetBytes(payloadStr);
                BroadcastEvent(payload);
                return new ValueTask<int>(ctx.Return());
            });
    }

    // ── Globals setup ──────────────────────────────────────────────

    /// <summary>Sets up server-specific globals in the Lua environment.</summary>
    private void SetupServerGlobals(IServerGamemodeContext context)
    {
        if (_luaState == null) return;

        // ctx — context table with player info and stage data.
        _luaState.Environment["ctx"] = CreateContextTable(context);
    }

    /// <summary>
    /// Creates a Lua table representing the <see cref="IServerGamemodeContext"/>.
    /// Fields: <c>player_ids</c> (array of string GUIDs), <c>player_infos</c> (table of PlayerInfo),
    /// <c>current_stage</c> (stage wrapper table), <c>get_player_position(id)</c> (function).
    /// </summary>
    private static LuaValue CreateContextTable(IServerGamemodeContext context)
    {
        var table = new LuaTable(0, 4);

        // player_ids — 1-indexed Lua array of string GUIDs.
        var ids = context.PlayerIds;
        var idsArray = new LuaTable(ids.Count, 0);
        for (int i = 0; i < ids.Count; i++)
            idsArray[i + 1] = ids[i].ToString();
        table["player_ids"] = idsArray;

        // player_infos — map of index → { id, name, vehicle, ... }.
        var infosTable = new LuaTable(0, context.PlayerInfos.Count);
        foreach (var (idx, info) in context.PlayerInfos)
        {
            var infoTable = new LuaTable(0, 4);
            infoTable["id"] = info.Id.ToString();
            infoTable["name"] = info.Name;
            infoTable["vehicle"] = info.Vehicle;
            infosTable[idx] = infoTable;
        }
        table["player_infos"] = infosTable;

        // current_stage — lightweight stage wrapper.
        var stage = context.CurrentStage;
        var stageTable = new LuaTable(0, 3);
        stageTable["name"] = stage.Name;
        stageTable["nlaps"] = (double)stage.nlaps;

        var checkpoints = stage.checkpoints;
        var cpArray = new LuaTable(checkpoints.Count, 0);
        for (int i = 0; i < checkpoints.Count; i++)
        {
            var pos = checkpoints[i].Position;
            var cpTable = new LuaTable(0, 3);
            cpTable["x"] = (double)(float)pos.X;
            cpTable["y"] = (double)(float)pos.Y;
            cpTable["z"] = (double)(float)pos.Z;
            cpArray[i + 1] = cpTable;
        }
        stageTable["checkpoints"] = cpArray;
        table["current_stage"] = stageTable;

        // get_player_position(id_str) → { x, y, z } or nil.
        table["get_player_position"] = new LuaFunction("get_player_position", (ctx, ct) =>
        {
            var idStr = ctx.GetArgument<string>(0);
            var pos = context.GetPlayerPosition(Guid.Parse(idStr));
            if (pos is null)
                return new ValueTask<int>(ctx.Return());

            var posTable = new LuaTable(0, 3);
            posTable["x"] = (double)(float)pos.Value.X;
            posTable["y"] = (double)(float)pos.Value.Y;
            posTable["z"] = (double)(float)pos.Value.Z;
            return new ValueTask<int>(ctx.Return(posTable));
        });

        return table;
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Executes a Lua callback expression in the gamemode's Lua state.
    /// </summary>
    private void CallLuaCallback(string callExpression)
    {
        if (_luaState == null) return;
        _luaState.DoStringAsync(callExpression, $"@{_scriptPath}").GetAwaiter().GetResult();
    }
}
