using Lua;
using Lua.Standard;
using MemoryPack;
using nfm_world_library.Lua;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;
using NFMWorld.LuaSourceGenerator.Generator;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;

namespace NFMWorldLibrary.Gamemodes.Lua;

[LuaVisible, LuaName("ServerGamemodeContext")]
public partial class LuaServerGamemodeContext(LuaServerGamemode gamemode, IServerGamemodeData data)
{
    /// <summary>The stage being raced on (checkpoints, lap count, geometry).</summary>
    [LuaName]
    public BackendStage CurrentStage => data.CurrentStage;

    /// <summary>Ordered list of player IDs in this race.</summary>
    [LuaName]
    public ReadOnlyLuaView<Guid, string> PlayerIds { get; } = new(data.PlayerIds, static guid => guid.ToString("D"));

    /// <summary>Map of player index → player info (names, vehicles, etc.).</summary>
    [LuaName]
    public ReadOnlyLuaDictionary<byte, PlayerInfo> PlayerInfos { get; } = new(data.PlayerInfos);

    /// <summary>
    /// Gets the latest relayed position for a player, or null if not yet received.
    /// Position data flows from <see cref="C2S_PlayerState"/> relay.
    /// </summary>
    [LuaName]
    public f64Vector3? GetPlayerPosition(string playerId)
    {
        return data.GetPlayerPosition(Guid.Parse(playerId));
    }

    [LuaName]
    public void BroadcastEvent(string type, LuaTable payload)
    {
        data.BroadcastEvent(MemoryPackSerializer.Serialize(new LuaEventEnvelope
        {
            Type = type,
            Payload = payload
        }));
    }

    [LuaName]
    public void FinishRace([LuaShimType("RaceStandings")] LuaTable standings)
    {
        gamemode._snapshot = new GameStateSnapshot
        {
            IsFinished = true,
            Results = new RaceResults
            {
                GamemodeId = gamemode.GamemodeId,
                RaceDuration = TimeSpan.Zero,
                Standings = ParseStandings(standings)
            }
        };
    }
    
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
    
    [LuaName]
    public int CountdownInterval => (int)(10 * (1 / Physics.PHYSICS_MULTIPLIER));
}

/// <summary>
/// Runs a Lua server gamemode script (<c>data/gamemodes/{path}/server.lua</c>).
///
/// The script receives:
/// <list type="bullet">
/// <item><c>SGM</c> — <see cref="LuaServerGamemodeContext"/></item>
/// </list>
///
/// Lifecycle callbacks: <c>OnBegin</c>, <c>OnStartRace</c>, <c>OnEnd</c>,
/// <c>OnGameTick</c>, and <c>OnClientEvent(playerId, type, table)</c>.
/// </summary>
public class LuaServerGamemode : IServerGamemode
{
    private readonly string _scriptPath;
    private LuaState? _state;
    private IServerGamemodeData? _data;
    internal GameStateSnapshot? _snapshot;

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

        _state.Environment["SGM"] = new LuaServerGamemodeContext(this, data);

        _state.DoFile($"data/gamemodes/{_scriptPath}/server.lua");
        Call("OnBegin");
    }

    public void StartRace() => Call("OnStartRace");

    public void End()
    {
        Call("OnEnd");
        _state?.Dispose();
        _state = null;
    }

    public void GameTick() => Call("OnGameTick");

    public void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        var envelope = MemoryPackSerializer.Deserialize<LuaEventEnvelope>(payload);

        Call("OnClientEvent", clientId.ToString(), envelope.Type, envelope.Payload);
    }

    public GameStateSnapshot? GetStateSnapshot() => _snapshot;

    private void RegisterFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> fn)
        => _state!.Environment[name] = new LuaFunction(name, fn);

    private LuaValue[] Call(string name, params ReadOnlySpan<LuaValue> arguments)
    {
        var state = _state;
        if (state == null ||
            !state.Environment.TryGetValue(name, out var value) ||
            !value.TryRead<LuaFunction>(out var function))
        {
            return [LuaValue.Nil];
        }

        try
        {
            return state.Call(function, arguments);
        }
        catch (Exception ex)
        {
            Logging.Error($"[LuaGamemode:{_scriptPath}] {name} failed: {ex.Message}");
        }
        return [LuaValue.Nil];
    }
}
