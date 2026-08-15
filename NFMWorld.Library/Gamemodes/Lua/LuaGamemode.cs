using Lua;
using Lua.Standard;
using MemoryPack;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Util;
using NFMWorld.LuaSourceGenerator.Generator;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Runs a Lua gamemode script (<c>data/gamemodes/{path}/client.lua</c>).
///
/// The script receives these globals:
/// <list type="bullet">
/// <item><c>stage</c> — <see cref="LuaStage"/> (lap count, checkpoints)</item>
/// <item><c>players</c> — the gamemode's <c>ObservableUnlimitedArray&lt;ClientSidePlayer&gt;</c></item>
/// <item><c>hud</c> — <see cref="LuaHudState"/> (writes through to the CEF HUD)</item>
/// <item><c>physics</c> — <see cref="PhysicsController"/> driver</item>
/// <item><c>create_car(index, x, z)</c>, <c>drive(index)</c>, <c>physics_tick()</c>,
/// <c>calculate_positions()</c>, <c>handle_checkpoint(index)</c>,
/// <c>handle_fix_hoops(index)</c>, <c>send_event(type, table)</c></item>
/// </list>
///
/// Lifecycle callbacks are invoked synchronously each tick:
/// <c>on_begin</c>, <c>on_end</c>, <c>on_reset</c>, <c>on_game_tick</c>,
/// <c>on_render</c>, <c>on_key_pressed(key)</c>, <c>on_key_released(key)</c>,
/// <c>on_key_typed(char)</c>, <c>on_server_event(type, table)</c> and
/// <c>on_ai_tick(car, index)</c> (for <see cref="LuaBot"/>s).
/// </summary>
public class LuaGamemode : BaseClientGamemode
{
    private readonly string _scriptPath;
    private readonly LuaState _state;
    private readonly PhysicsController _physics;
    private readonly LuaTimeTrial _timeTrial;

    public LuaGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData, string scriptPath, string? configJson = null)
        : base(gamemodeParameters, gamemodeData)
    {
        _scriptPath = scriptPath;

        _state = LuaState.Create(LuaNfmwPlatform.Instance);
        _state.OpenStandardLibraries();
        LuaVisibleTypeRegistry.RegisterAll(_state);

        _physics = new PhysicsController(Players, CurrentStage);
        _timeTrial = new LuaTimeTrial(CurrentStage);

        _state.Environment["stage"] = LuaValue.FromObject(new LuaStage(CurrentStage));
        _state.Environment["players"] = LuaValue.FromObject(new LuaPlayers(Players));
        _state.Environment["hud"] = LuaValue.FromObject(new LuaHudState(HudState));
        _state.Environment["physics"] = LuaValue.FromObject(_physics);
        _state.Environment["time_trial"] = LuaValue.FromObject(_timeTrial);

        if (!string.IsNullOrEmpty(configJson))
            _state.Environment["config"] = new LuaValue(LuaJson.FromJson(System.Text.Encoding.UTF8.GetBytes(configJson)));

        RegisterFunction("create_car", (context, ct) =>
        {
            var playerIndex = context.GetArgument<int>(0);
            var x = context.GetArgument<double>(1);
            var z = context.GetArgument<double>(2);

            var player = Players[playerIndex];
            var car = new BackendCar(player.Parameters, playerIndex, (fix64)x, (fix64)z);
            player.Car = car;
            return new(context.Return(ToLua(car)));
        });

        RegisterFunction("drive", (context, ct) =>
        {
            var playerIndex = context.GetArgument<int>(0);
            Players[playerIndex].Car?.Drive(CurrentStage);
            return new(context.Return());
        });

        RegisterFunction("physics_tick", (context, ct) =>
        {
            _physics.GameTick();
            return new(context.Return());
        });

        RegisterFunction("calculate_positions", (context, ct) =>
        {
            CheckPointHelper.CalculatePositions(CurrentStage, Players);
            return new(context.Return());
        });

        RegisterFunction("handle_checkpoint", (context, ct) =>
        {
            var playerIndex = context.GetArgument<int>(0);
            var car = Players[playerIndex].Car;
            var handled = car is not null && CheckPointHelper.HandleCheckPoint(CurrentStage, car);
            return new(context.Return(handled));
        });

        RegisterFunction("handle_fix_hoops", (context, ct) =>
        {
            var playerIndex = context.GetArgument<int>(0);
            var car = Players[playerIndex].Car;
            var handled = car is not null && FixHoopHelper.HandleFixHoops(CurrentStage, car);
            return new(context.Return(handled));
        });

        RegisterFunction("send_event", (context, ct) =>
        {
            var type = context.GetArgument<string>(0);
            var payload = context.HasArgument(1) ? context.GetArgument(1) : LuaValue.Nil;

            var json = payload.Type == LuaValueType.Table
                ? LuaJson.ToJson(payload.Read<LuaTable>())
                : Array.Empty<byte>();

            SendServerEvent(MemoryPackSerializer.Serialize(new LuaEventEnvelope
            {
                Type = type,
                JsonPayload = json
            }));

            return new(context.Return());
        });

        RegisterFunction("countdown_interval", (context, ct) =>
            new(context.Return((int)(10 * (1 / Physics.PHYSICS_MULTIPLIER)))));

        RegisterFunction("client_index", (context, ct) =>
            new(context.Return(ClientPlayer.Index)));

        RegisterFunction("attach_bot", (context, ct) =>
        {
            var playerIndex = context.GetArgument<int>(0);
            Players[playerIndex].Bot = new ElStupido(this, GamemodeData);
            return new(context.Return());
        });

        RegisterFunction("reset_client_state", (context, ct) =>
        {
            ClientReset();
            return new(context.Return());
        });

        RegisterFunction("update_hud", (context, ct) =>
        {
            var playerIndex = context.GetArgument<int>(0);
            if (Players[playerIndex].Car is { } car)
                UpdateHudAndSounds(car);
            return new(context.Return());
        });

        RegisterFunction("stop_all_sounds", (context, ct) =>
        {
            IBackend.Backend.StopAllSounds();
            return new(context.Return());
        });

        RegisterFunction("reset_checkpoint_glow", (context, ct) =>
        {
            GamemodeData.ClientCallbacks.ResetCheckpointGlow();
            return new(context.Return());
        });

        RegisterFunction("update_checkpoint_glow", (context, ct) =>
        {
            var checkpoint = context.GetArgument<int>(0);
            var isFinish = context.HasArgument(1) && context.GetArgument<bool>(1);
            GamemodeData.ClientCallbacks.UpdateCheckpointGlow((ushort)checkpoint, isFinish);
            return new(context.Return());
        });

        RegisterFunction("add_ghost_player", (context, ct) =>
        {
            var ghostIndex = Players.Count;
            var source = Players[0].Car;
            var ghost = new ClientSidePlayer(Players[0].Parameters, ghostIndex, isFake: true);
            if (source is not null)
                ghost.Car = new BackendCar(source, ghostIndex, false);
            Players.Add(ghost);
            return new(context.Return(ghostIndex));
        });

        RegisterFunction("remove_fake_players", (context, ct) =>
        {
            for (var i = Players.Count - 1; i >= 0; i--)
            {
                if (Players[i].IsFake)
                    Players.RemoveAt(i);
            }
            return new(context.Return());
        });

        _state.DoFile($"data/gamemodes/{_scriptPath}/client.lua");
    }

    /// <summary>Creates a bot whose decisions come from the script's <c>on_ai_tick</c>.</summary>
    public LuaBot CreateBot() => new(this);

    internal void OnAiTick(IInGameCar car, int index)
        => Call("on_ai_tick", ToLua(car), LuaValue.FromObject(index));

    // ── Lifecycle callbacks ────────────────────────────────────────

    public override void Begin()
    {
        base.Begin();
        Call("on_begin");
    }

    public override void End()
    {
        Call("on_end");
        base.End();
        _state.Dispose();
    }

    public override void Reset()
    {
        base.Reset();
        Call("on_reset");
    }

    public override void GameTick()
        => Call("on_game_tick");

    public override void Render()
        => Call("on_render");

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);
        Call("on_key_pressed", LuaValue.FromObject((int)key));
    }

    public override void KeyReleased(Key key, in Keys keys)
    {
        base.KeyReleased(key, keys);
        Call("on_key_released", LuaValue.FromObject((int)key));
    }

    public override void KeyTyped(char character)
    {
        base.KeyTyped(character);
        Call("on_key_typed", new LuaValue(character.ToString()));
    }

    public override void OnServerEvent(ReadOnlySpan<byte> payload)
    {
        var envelope = MemoryPackSerializer.Deserialize<LuaEventEnvelope>(payload);

        var table = envelope.JsonPayload is { Length: > 0 } json
            ? LuaJson.FromJson(json)
            : new LuaTable();

        Call("on_server_event", new LuaValue(envelope.Type), new LuaValue(table));
    }

    // ── Script invocation ──────────────────────────────────────────

    /// <summary>
    /// Marshals a backend car as an <see cref="IInGameCar"/> userdata with
    /// the generated interface metatable, so scripts see carPhysics/control/etc.
    /// </summary>
    private static LuaValue ToLua(IInGameCar car)
        => LuaValue.FromUserData(car, LuaVisibleTypeMetatableRegistry<IInGameCar>.Metatable!);

    private void RegisterFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> fn)
        => _state.Environment[name] = new LuaFunction(name, fn);

    private void Call(string name, params LuaValue[] arguments)
    {
        if (!_state.Environment.TryGetValue(name, out var value) ||
            !value.TryRead<LuaFunction>(out var function))
        {
            return;
        }

        try
        {
            foreach (var argument in arguments)
                _state.Push(argument);

            var resultCount = _state.Run(function, arguments.Length);
            if (resultCount > 0)
                _state.Pop(resultCount);
        }
        catch (Exception ex)
        {
            Logging.Error($"[LuaGamemode:{_scriptPath}] {name} failed: {ex.Message}");
        }
    }
}
