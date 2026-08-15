using Lua;
using Lua.Standard;
using MemoryPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;
using NFMWorld.LuaSourceGenerator.Generator;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Runs a Lua server gamemode script (<c>data/gamemodes/{path}/server.lua</c>).
///
/// The script receives:
/// <list type="bullet">
/// <item><c>server</c> — <see cref="LuaServerData"/> (players, positions)</item>
/// <item><c>stage</c> — <see cref="LuaStage"/></item>
/// <item><c>broadcast_event(type, table)</c> — send an event to all clients</item>
/// <item><c>finish_race(standings)</c> — end the race with results</item>
/// </list>
///
/// Lifecycle callbacks: <c>on_begin</c>, <c>on_start_race</c>, <c>on_end</c>,
/// <c>on_game_tick</c>, and <c>on_client_event(playerId, type, table)</c>.
/// </summary>
public class LuaServerGamemode : IServerGamemode
{
    private readonly string _scriptPath;
    private LuaState? _state;
    private IServerGamemodeData? _data;
    private GameStateSnapshot? _snapshot;

    public string GamemodeId { get; }

    public LuaServerGamemode(string gamemodeId, string scriptPath)
    {
        GamemodeId = gamemodeId;
        _scriptPath = scriptPath;
    }

    public void Begin(IServerGamemodeData data)
    {
        _data = data;
        _snapshot = null;

        _state = LuaState.Create(LuaNfmwPlatform.Instance);
        _state.OpenStandardLibraries();
        LuaVisibleTypeRegistry.RegisterAll(_state);

        _state.Environment["server"] = LuaValue.FromObject(new LuaServerData(data));
        _state.Environment["stage"] = LuaValue.FromObject(new LuaStage(data.CurrentStage));

        RegisterFunction("broadcast_event", (context, ct) =>
        {
            var type = context.GetArgument<string>(0);
            var payload = context.HasArgument(1) ? context.GetArgument(1) : LuaValue.Nil;

            var json = payload.Type == LuaValueType.Table
                ? LuaJson.ToJson(payload.Read<LuaTable>())
                : Array.Empty<byte>();

            data.BroadcastEvent(MemoryPackSerializer.Serialize(new LuaEventEnvelope
            {
                Type = type,
                JsonPayload = json
            }));

            return new(context.Return());
        });

        RegisterFunction("finish_race", (context, ct) =>
        {
            var standings = context.GetArgument<LuaTable>(0);
            _snapshot = new GameStateSnapshot
            {
                IsFinished = true,
                Results = new RaceResults
                {
                    GamemodeId = GamemodeId,
                    RaceDuration = TimeSpan.Zero,
                    Standings = ParseStandings(standings)
                }
            };
            return new(context.Return());
        });

        RegisterFunction("countdown_interval", (context, ct) =>
            new(context.Return((int)(10 * (1 / Physics.PHYSICS_MULTIPLIER)))));

        _state.DoFile($"data/gamemodes/{_scriptPath}/server.lua");
        Call("on_begin");
    }

    public void StartRace() => Call("on_start_race");

    public void End()
    {
        Call("on_end");
        _state?.Dispose();
        _state = null;
    }

    public void GameTick() => Call("on_game_tick");

    public void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        var envelope = MemoryPackSerializer.Deserialize<LuaEventEnvelope>(payload);

        var table = envelope.JsonPayload is { Length: > 0 } json
            ? LuaJson.FromJson(json)
            : new LuaTable();

        Call("on_client_event", new LuaValue(clientId.ToString()), new LuaValue(envelope.Type), new LuaValue(table));
    }

    public GameStateSnapshot? GetStateSnapshot() => _snapshot;

    private static RaceStanding[] ParseStandings(LuaTable table)
    {
        var standings = new List<RaceStanding>();
        foreach (var (_, value) in table)
        {
            if (!value.TryRead<LuaTable>(out var entry))
                continue;

            var playerId = entry.TryGetValue("playerId", out var id) && id.TryRead<string>(out var s) && Guid.TryParse(s, out var guid)
                ? guid
                : Guid.Empty;
            var position = entry.TryGetValue("position", out var pos) && pos.TryRead<double>(out var d)
                ? (int)d
                : standings.Count;
            var finished = entry.TryGetValue("finished", out var fin) && fin.TryRead<bool>(out var b) && b;

            standings.Add(new RaceStanding
            {
                PlayerId = playerId,
                FinishPosition = position,
                FinishTime = finished ? TimeSpan.Zero : null,
                IsClientPlayer = false
            });
        }

        return standings.OrderBy(s => s.FinishPosition).ToArray();
    }

    private void RegisterFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> fn)
        => _state!.Environment[name] = new LuaFunction(name, fn);

    private void Call(string name, params LuaValue[] arguments)
    {
        var state = _state;
        if (state == null ||
            !state.Environment.TryGetValue(name, out var value) ||
            !value.TryRead<LuaFunction>(out var function))
        {
            return;
        }

        try
        {
            foreach (var argument in arguments)
                state.Push(argument);

            var resultCount = state.Run(function, arguments.Length);
            if (resultCount > 0)
                state.Pop(resultCount);
        }
        catch (Exception ex)
        {
            Logging.Error($"[LuaServerGamemode:{_scriptPath}] {name} failed: {ex.Message}");
        }
    }
}
