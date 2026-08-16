using Lua;
using Lua.Standard;
using MemoryPack;
using nfm_world_library.Lua;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Util;
using NFMWorld.LuaSourceGenerator.Generator;

namespace NFMWorldLibrary.Gamemodes.Lua;

[LuaVisible]
public partial class LuaClientContext(IClientCallbacks callbacks)
{
    [LuaName]
    public void ResetCheckpointGlow()
    {
        callbacks.ResetCheckpointGlow();
    }

    [LuaName]
    public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
    {
        callbacks.UpdateCheckpointGlow(currentCheckpoint, isFinish);
    }

    [LuaName]
    public LuaClientCarContext GetClientCarCallbacks(BackendCar car)
    {
        return LuaProxies.GetOrAdd(callbacks.GetClientCarCallbacks(car), static cb => new LuaClientCarContext(cb));
    }
}

[LuaVisible]
public partial class LuaClientCarContext(IClientCarCallbacks callbacks)
{
    [LuaName] public bool CastsShadow { get => callbacks.CastsShadow; set => callbacks.CastsShadow = value; }
    [LuaName] public bool? GetsShadowed { get => callbacks.GetsShadowed; set => callbacks.GetsShadowed = value; }
    [LuaName] public float? AlphaOverride { get => callbacks.AlphaOverride; set => callbacks.AlphaOverride = value; }
    [LuaName] public bool? Glow { get => callbacks.Glow; set => callbacks.Glow = value; }
    [LuaName] public bool? Finish { get => callbacks.Finish; set => callbacks.Finish = value; }
}

[LuaVisible, LuaName("GamemodeContext")]
public partial class LuaGamemodeContext(LuaGamemode gamemode)
{
    [LuaName("stage")]
    public BackendStage CurrentStage => gamemode.CurrentStage;

    [LuaName("players")]
    public LuaList<ClientSidePlayer> Players { get; } = new(gamemode.Players);

    [LuaName]
    public ClientSidePlayer ClientPlayer => gamemode.ClientPlayer;

    [LuaName("hudState")]
    public HudStateData HudState
    {
        get => gamemode.HudState;
        set => gamemode.HudState = value;
    }

    [LuaName("physics")]
    public PhysicsController Physics { get; } = new(gamemode.Players, gamemode.CurrentStage);

    [LuaName("timeTrial")]
    public LuaTimeTrial TimeTrial { get; } = new(gamemode.CurrentStage);

    [LuaName]
    public LuaTable? Config { get; } = gamemode.Config;

    [LuaName]
    public LuaClientContext Client { get; } = new(gamemode.GamemodeData.ClientCallbacks);
    
    [LuaName]
    public BackendCar CreateCar(int playerIndex, fix64 x, fix64 z)
    {
        var player = Players[playerIndex];
        var car = new BackendCar(player.Parameters, playerIndex, (fix64)x, (fix64)z);
        player.Car = car;
        return car;
    }

    [LuaName]
    public void CalculatePositions()
    {
        CheckPointHelper.CalculatePositions(CurrentStage, Players);
    }

    [LuaName]
    public bool HandleCheckPoint(BackendCar car)
    {
        return CheckPointHelper.HandleCheckPoint(CurrentStage, car);
    }

    [LuaName]
    public bool HandleFixHoops(BackendCar car)
    {
        return FixHoopHelper.HandleFixHoops(CurrentStage, car);
    }

    [LuaName]
    public void ClientReset()
    {
        gamemode.ClientReset();
    }

    [LuaName]
    public int CountdownInterval => (int)(10 * (1 / NFMWorldLibrary.Physics.PHYSICS_MULTIPLIER));

    [LuaName]
    public void SendEvent(string type, LuaTable payload)
    {
        gamemode.SendServerEvent(MemoryPackSerializer.Serialize(new LuaEventEnvelope
        {
            Type = type,
            Payload = payload
        }));
    }

    [LuaName]
    public void UpdateHudAndSounds(BackendCar car)
    {
        gamemode.UpdateHudAndSounds(car);
    }

    [LuaName]
    public void RemoveFakePlayers()
    {
        for (var i = Players.Count - 1; i >= 0; i--)
        {
            if (Players[i].IsFake)
                Players.RemoveAt(i);
        }
    }

    [LuaName]
    public ClientSidePlayer AddGhostPlayer(ClientSidePlayer basedOnPlayer)
    {
        var ghostIndex = Players.Count;
        var source = basedOnPlayer.Car;
        var ghost = new ClientSidePlayer(basedOnPlayer.Parameters, ghostIndex, isFake: true);
        if (source is not null)
            ghost.Car = new BackendCar(source, ghostIndex, false);
        Players.Add(ghost);
        return ghost;
    }
}

/// <summary>
/// Runs a Lua gamemode script (<c>data/gamemodes/{path}/client.lua</c>).
///
/// The script receives these globals:
/// <list type="bullet">
/// <item><c>GM</c> — <see cref="LuaGamemodeContext"/></item>
/// </list>
///
/// Lifecycle callbacks are invoked synchronously each tick:
/// <c>OnBegin</c>, <c>OnEnd</c>, <c>OnReset</c>, <c>OnGameTick</c>,
/// <c>OnRender</c>, <c>OnKeyPressed(key)</c>, <c>OnKeyReleased(key)</c>,
/// <c>OnKeyTyped(char)</c>, <c>OnServerEvent(type, table)</c>.
/// </summary>
public class LuaGamemode : BaseClientGamemode
{
    private readonly string _scriptPath;
    private readonly LuaState _state;

    public LuaTable? Config { get; set; }

    public LuaGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData, string scriptPath, string? configJson = null)
        : base(gamemodeParameters, gamemodeData)
    {
        _scriptPath = scriptPath;

        _state = LuaState.Create(LuaNfmwPlatform.Instance);
        _state.OpenStandardLibraries();
        LuaVisibleTypeRegistry.RegisterAll(_state);

        _state.Environment["GM"] = new LuaGamemodeContext(this);

        if (!string.IsNullOrEmpty(configJson))
            Config = LuaJson.FromJson(System.Text.Encoding.UTF8.GetBytes(configJson));

        _state.DoFile($"data/gamemodes/{_scriptPath}/client.lua");
    }

    // ── Lifecycle callbacks ────────────────────────────────────────

    public override void Begin()
    {
        base.Begin();
        Call("OnBegin");
    }

    public override void End()
    {
        Call("OnEnd");
        base.End();
        _state.Dispose();
    }

    public override void Reset()
    {
        base.Reset();
        Call("OnReset");
    }

    public override void GameTick()
        => Call("OnGameTick");

    public override void Render()
        => Call("OnRender");

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);
        Call("OnKeyPressed", (int)key);
    }

    public override void KeyReleased(Key key, in Keys keys)
    {
        base.KeyReleased(key, keys);
        Call("OnKeyReleased", (int)key);
    }

    public override void KeyTyped(char character)
    {
        base.KeyTyped(character);
        Call("OnKeyTyped", character.ToString());
    }

    public override void OnServerEvent(ReadOnlySpan<byte> payload)
    {
        var envelope = MemoryPackSerializer.Deserialize<LuaEventEnvelope>(payload);
        Call("OnServerEvent", envelope.Type, envelope.Payload);
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
