using System.Text;
using Lua;
using Lua.Runtime;
using Lua.Standard;
using MessagePack;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sfx;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Client-side gamemode whose logic is defined by a Lua script loaded at runtime.
/// All <see cref="IGamemode"/> methods are delegated to convention-based Lua callbacks.
/// </summary>
/// <remarks>
/// <para>Scripts are loaded from <c>data/gamemodes/{scriptRelativePath}</c> via <c>VFS.ReadAllText</c>.</para>
/// <para>Expected Lua callbacks (all optional — missing ones are silently skipped):</para>
/// <list type="bullet">
///   <item><c>on_begin(players, cars, stage)</c> — called in <see cref="Begin"/></item>
///   <item><c>on_end()</c> — called in <see cref="End"/></item>
///   <item><c>on_tick()</c> — called in <see cref="GameTick"/></item>
///   <item><c>on_reset()</c> — called in <see cref="Reset"/></item>
///   <item><c>on_get_results()</c> — called in <see cref="GetResults"/></item>
///   <item><c>on_set_server_results(results)</c> — called in <see cref="SetServerResults"/></item>
///   <item><c>on_server_event(payload_str)</c> — called in <see cref="OnServerEvent"/></item>
///   <item><c>on_key_pressed(key, keys)</c> — called in <see cref="KeyPressed"/></item>
///   <item><c>on_key_released(key, keys)</c> — called in <see cref="KeyReleased"/></item>
///   <item><c>on_key_typed(char)</c> — called in <see cref="KeyTyped"/></item>
///   <item><c>on_mouse_moved(x, y, buttons, ctrl, shift, alt)</c> — called in <see cref="MouseMoved"/></item>
///   <item><c>on_mouse_pressed(x, y, button, buttons, ctrl, shift, alt)</c> — called in <see cref="MousePressed"/></item>
///   <item><c>on_mouse_released(x, y, button, buttons, ctrl, shift, alt)</c> — called in <see cref="MouseReleased"/></item>
///   <item><c>on_mouse_scrolled(x, y, delta, ctrl, shift, alt)</c> — called in <see cref="MouseScrolled"/></item>
///   <item><c>on_render()</c> — called in <see cref="Render"/></item>
/// </list>
/// <para>Globals available to the script:</para>
/// <list type="bullet">
///   <item><c>players</c> — <see cref="LuaArray{T}"/> of <see cref="PlayerParameters"/> (1-indexed)</item>
///   <item><c>cars</c> — <see cref="LuaArray{T}"/> of <see cref="IInGameCar"/> (1-indexed)</item>
///   <item><c>stage</c> — table with <c>name</c>, <c>nlaps</c>, <c>checkpoints</c> (array of {x,y,z})</item>
///   <item><c>hud_state</c> — proxy table for <see cref="HudStateData"/> fields</item>
///   <item><c>send_to_server(payload_str)</c> — send a UTF-8 event string to the server</item>
/// </list>
/// <para>All <c>[LuaVisible]</c> types are also registered as globals via <c>LuaVisibleTypeRegistry.RegisterAll</c>.</para>
/// </remarks>
public class LuaGamemode : BaseGamemode
{
    private readonly string _scriptPath;
    private LuaState? _luaState;
    private bool _luaError;

    /// <summary>
    /// Creates a new Lua-driven client gamemode.
    /// </summary>
    /// <param name="gamemodeParameters">Player configuration for this race.</param>
    /// <param name="gamemodeData">Cars, stage, and race state.</param>
    /// <param name="scriptRelativePath">
    /// Path to the .lua file, relative to <c>data/gamemodes/</c>.
    /// Example: <c>"race.lua"</c> resolves to <c>data/gamemodes/race.lua</c>.
    /// </param>
    public LuaGamemode(
        GamemodeParameters gamemodeParameters,
        IGamemodeData gamemodeData,
        string scriptRelativePath)
        : base(gamemodeParameters, gamemodeData)
    {
        _scriptPath = scriptRelativePath;
    }

    // ── Lifecycle ──────────────────────────────────────────────────

    public override void Begin()
    {
        base.Begin();

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

            // Inject gamemode-specific globals.
            GetExports(out var players, out var cars, out var stage, out var hudState);

            // Fire on_begin if the script defines it.
            _luaState.Environment["on_begin"].TryRead<LuaFunction>(out var onBeginFunction);
            if (onBeginFunction != null)
            {
                _luaState
                    .CallAsync(onBeginFunction, [players, cars, stage, hudState])
                    .GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaGamemode [{_scriptPath}] Begin failed: {ex.Message}");
            _luaError = true;
        }
    }

    public override void End()
    {
        if (!_luaError)
        {
            try
            {
                CallLuaCallback("on_end()");
            }
            catch (Exception ex)
            {
                Logging.Error($"LuaGamemode [{_scriptPath}] on_end failed: {ex.Message}");
            }
        }

        _luaState?.Dispose();
        _luaState = null;
        _luaError = false;

        base.End();
    }

    public override void Reset()
    {
        base.Reset();

        if (_luaError) return;

        try
        {
            CallLuaCallback("on_reset()");
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaGamemode [{_scriptPath}] on_reset failed: {ex.Message}");
            _luaError = true;
        }
    }

    // ── Tick ───────────────────────────────────────────────────────

    public override void GameTick()
    {
        if (_luaError) return;

        try
        {
            CallLuaCallback("on_tick()");
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaGamemode [{_scriptPath}] on_tick failed: {ex.Message}");
            _luaError = true;
        }
    }

    // ── Results ────────────────────────────────────────────────────

    public override RaceResults? GetResults()
    {
        if (_luaError || _luaState == null) return null;

        try
        {
            // Call the Lua callback and collect return values.
            var results = _luaState.DoStringAsync("return on_get_results()",
                $"@{_scriptPath}").GetAwaiter().GetResult();

            if (results.Length == 0 || results[0].Type == LuaValueType.Nil)
                return null;

            return LuaResultsConverter.ToRaceResults(results[0]);
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaGamemode [{_scriptPath}] on_get_results failed: {ex.Message}");
            _luaError = true;
            return null;
        }
    }

    public override void SetServerResults(RaceResults results)
    {
        if (_luaError || _luaState == null) return;

        try
        {
            var luaResults = LuaResultsConverter.FromRaceResults(results);
            _luaState.Environment["_server_results"] = luaResults;
            CallLuaCallback("on_set_server_results(_server_results)");
            _luaState.Environment["_server_results"] = LuaValue.Nil;
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaGamemode [{_scriptPath}] on_set_server_results failed: {ex.Message}");
            _luaError = true;
        }
    }

    // ── Server events ──────────────────────────────────────────────

    public override void OnServerEvent(ReadOnlySpan<byte> payload)
    {
        if (_luaError || _luaState == null) return;

        try
        {
            // Pass the payload as a base64 string for Lua compatibility.
            var payloadStr = Convert.ToBase64String(payload);
            _luaState.Environment["_payload"] = payloadStr;
            CallLuaCallback("on_server_event(_payload)");
        }
        catch (Exception ex)
        {
            Logging.Error($"LuaGamemode [{_scriptPath}] on_server_event failed: {ex.Message}");
            _luaError = true;
        }
    }

    public override void SetEventSender(Action<ReadOnlyMemory<byte>> sendToServer)
    {
        base.SetEventSender(sendToServer);

        if (_luaError || _luaState == null) return;

        // Expose send_to_server as a global Lua function.
        _luaState.Environment["send_to_server"] = new LuaFunction("send_to_server",
            (ctx, ct) =>
            {
                var payloadStr = ctx.GetArgument<string>(0);
                var payload = Encoding.UTF8.GetBytes(payloadStr);
                SendToServer(payload);
                return new ValueTask<int>(ctx.Return());
            });
    }

    // ── Input (client-only) ────────────────────────────────────────

    public override void KeyPressed(Key key, in Keys keys)
    {
        if (_luaError) return;
        try { CallLuaCallback($"on_key_pressed({(int)key})"); }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_key_pressed: {ex.Message}"); _luaError = true; }
    }

    public override void KeyReleased(Key key, in Keys keys)
    {
        if (_luaError) return;
        try { CallLuaCallback($"on_key_released({(int)key})"); }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_key_released: {ex.Message}"); _luaError = true; }
    }

    public override void KeyTyped(char character)
    {
        if (_luaError) return;
        try { CallLuaCallback($"on_key_typed({(int)character})"); }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_key_typed: {ex.Message}"); _luaError = true; }
    }

    public override void MouseMoved(int x, int y, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (_luaError) return;
        try
        {
            CallLuaCallback($"on_mouse_moved({x}, {y}, {(int)buttons}, " +
                            $"{(ctrlKey ? "true" : "false")}, {(shiftKey ? "true" : "false")}, {(altKey ? "true" : "false")})");
        }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_mouse_moved: {ex.Message}"); _luaError = true; }
    }

    public override void MousePressed(int x, int y, MouseButton button, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (_luaError) return;
        try
        {
            CallLuaCallback($"on_mouse_pressed({x}, {y}, {(int)button}, {(int)buttons}, " +
                            $"{(ctrlKey ? "true" : "false")}, {(shiftKey ? "true" : "false")}, {(altKey ? "true" : "false")})");
        }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_mouse_pressed: {ex.Message}"); _luaError = true; }
    }

    public override void MouseReleased(int x, int y, MouseButton button, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (_luaError) return;
        try
        {
            CallLuaCallback($"on_mouse_released({x}, {y}, {(int)button}, {(int)buttons}, " +
                            $"{(ctrlKey ? "true" : "false")}, {(shiftKey ? "true" : "false")}, {(altKey ? "true" : "false")})");
        }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_mouse_released: {ex.Message}"); _luaError = true; }
    }

    public override void MouseScrolled(int x, int y, int delta, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (_luaError) return;
        try
        {
            CallLuaCallback($"on_mouse_scrolled({x}, {y}, {delta}, {(int)buttons}, " +
                            $"{(ctrlKey ? "true" : "false")}, {(shiftKey ? "true" : "false")}, {(altKey ? "true" : "false")})");
        }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_mouse_scrolled: {ex.Message}"); _luaError = true; }
    }

    // ── Render (client-only) ───────────────────────────────────────

    public override void Render()
    {
        if (_luaError) return;
        try { CallLuaCallback("on_render()"); }
        catch (Exception ex) { Logging.Error($"LuaGamemode on_render: {ex.Message}"); _luaError = true; }
    }

    // ── Globals setup ──────────────────────────────────────────────

    /// <summary>Sets up gamemode-specific globals in the Lua environment.</summary>
    /// <param name="players"></param>
    /// <param name="cars"></param>
    /// <param name="stage"></param>
    /// <param name="hudState"></param>
    private void GetExports(out LuaValue players, out LuaValue cars, out LuaValue stage, out LuaValue hudState)
    {
        if (_luaState == null)
        {
            players = default;
            cars = default;
            stage = default;
            hudState = default;
            return;
        }

        // players — 1-indexed LuaArray of PlayerParameters.
        var playersArray = new LuaTable(Players.Count, 0);
        for (int i = 0; i < Players.Count; i++)
            playersArray[i] = LuaValue.FromUserData(Players[i]);
        players = new LuaValue(playersArray);

        // cars — 1-indexed LuaArray of IInGameCar.
        var carsIn = GamemodeData.CarsInRace;
        var carsArray = new LuaTable(carsIn.Count, 0);
        for (int i = 0; i < carsIn.Count; i++)
            carsArray[i] = LuaValue.FromUserData(carsIn[i]);
        cars = new LuaValue(carsArray);

        // stage — wrapper table with name, nlaps, checkpoints.
        stage = LuaValue.FromUserData(GamemodeData.CurrentStage);

        // hud_state — proxy table with __index/__newindex for HudStateData fields.
        hudState = CreateHudStateTable();
    }

    /// <summary>
    /// Creates a Lua proxy table that reads/writes <see cref="HudStateData"/> fields
    /// via <c>__index</c> and <c>__newindex</c> metamethods.
    /// </summary>
    private LuaValue CreateHudStateTable()
    {
        var mt = new LuaTable(0, 2);
        var hudRef = HudState; // capture reference to the instance's HudStateData

        mt[Metamethods.Index] = new LuaFunction("__index", (ctx, ct) =>
        {
            var key = ctx.GetArgument<string>(1);
            return new ValueTask<int>(key switch
            {
                "speed" => ctx.Return((double)hudRef.Speed),
                "power" => ctx.Return((double)hudRef.Power),
                "damage" => ctx.Return((double)hudRef.Damage),
                "lap" => ctx.Return(hudRef.Lap),
                "total_laps" => ctx.Return(hudRef.TotalLaps),
                "lap_time" => ctx.Return(hudRef.LapTime),
                "position" => ctx.Return(hudRef.Position),
                "total_racers" => ctx.Return(hudRef.TotalRacers),
                "state_text" => hudRef.StateText != null ? ctx.Return(hudRef.StateText) : ctx.Return(),
                "countdown_timer" => ctx.Return(hudRef.CountdownTimer),
                _ => ctx.Return(),
            });
        });

        mt[Metamethods.NewIndex] = new LuaFunction("__newindex", (ctx, ct) =>
        {
            var key = ctx.GetArgument<string>(1);
            var val = ctx.GetArgument(2);
            switch (key)
            {
                case "speed": hudRef.Speed = (float)val.Read<double>(); break;
                case "power": hudRef.Power = (float)val.Read<double>(); break;
                case "damage": hudRef.Damage = (float)val.Read<double>(); break;
                case "lap": hudRef.Lap = val.Read<int>(); break;
                case "total_laps": hudRef.TotalLaps = val.Read<int>(); break;
                case "lap_time": hudRef.LapTime = val.Read<int>(); break;
                case "position": hudRef.Position = val.Read<int>(); break;
                case "total_racers": hudRef.TotalRacers = val.Read<int>(); break;
                case "state_text": hudRef.StateText = val.Type == LuaValueType.Nil ? null : val.Read<string>(); break;
                case "countdown_timer": hudRef.CountdownTimer = val.Read<int>(); break;
            }
            return new ValueTask<int>(ctx.Return());
        });

        var proxy = new LuaTable(0, 0);
        proxy.Metatable = mt;
        return proxy;
    }

    /// <summary>
    /// Executes a Lua callback expression (e.g. <c>"on_tick()"</c>) in the gamemode's Lua state.
    /// Does nothing if the Lua state is null or in an error state.
    /// </summary>
    private void CallLuaCallback(string callExpression)
    {
        if (_luaState == null) return;
        _luaState.DoStringAsync(callExpression, $"@{_scriptPath}").GetAwaiter().GetResult();
    }
}
