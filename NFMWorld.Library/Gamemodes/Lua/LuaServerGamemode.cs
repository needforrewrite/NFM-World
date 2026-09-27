using Lua;
using Lua.Loaders;
using Lua.Standard;
using MemoryPack;
using NFMWorld.Lua;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;
using NFMWorld.LuaSourceGenerator.Generator;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Radpack;

namespace NFMWorldLibrary.Gamemodes.Lua;

[LuaVisible, LuaName("ServerGamemodeContext")]
public partial class LuaServerGamemodeContext(LuaServerGamemode gamemode, IServerGamemodeContext context)
{
    /// <summary>The stage being raced on (checkpoints, lap count, geometry).</summary>
    [LuaName]
    public BackendStage CurrentStage => context.CurrentStage;

    /// <summary>
    /// Ordered list of players participating in the race.
    /// </summary>
    [LuaName]
    public ReadOnlyLuaArray<ServerSidePlayerInfo> Players { get; } = new(gamemode.Players);

    [LuaName]
    public LuaTable? Config { get; } = gamemode.Config;

    /// <summary>
    /// Gets the latest relayed position for a player, or null if not yet received.
    /// Position data flows from <see cref="C2S_PlayerState"/> relay.
    /// </summary>
    [LuaName]
    public f64Vector3? GetPlayerPosition(string playerId)
    {
        return context.GetPlayerPosition(Guid.Parse(playerId));
    }

    [LuaName]
    public void BroadcastEvent(string type, LuaTable payload)
    {
        context.BroadcastEvent(MemoryPackSerializer.Serialize(new LuaEventEnvelope
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
                FinishTime = finished ? TimeSpan.Zero : null
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
public sealed class LuaServerGamemode : BaseServerGamemode
{
    private LuaState _state;
    private IServerGamemodeContext? _context;
    internal GameStateSnapshot? _snapshot;
    private readonly LuaTable? _moduleTable;
    public LuaTable? Config { get; }

    public override string GamemodeId { get; }

    public IReadOnlyList<ServerSidePlayerInfo> Players { get; set; }

    public LuaServerGamemode(ServerGamemodeParameters parameters, IServerGamemodeContext context, string gamemodeId,
        LuaTable? config = null)
    {
        _context = context;
        GamemodeId = gamemodeId;
        Config = config;
        Players = parameters.Players;
        
        _state = LuaHelpers.OpenState();

        _state.Environment["SGM"] = new LuaServerGamemodeContext(this, context);

        var results = _state.DoFile($"data/gamemodes/{gamemodeId}/server.luau");
        if (results is [var value] && value.TryRead<LuaTable>(out var resultTable))
        {
            _moduleTable = resultTable;
        }
    }

    public LuaServerGamemode(ServerGamemodeParameters parameters, IServerGamemodeContext context, string gamemodeId,
        RadpackLua radpack, LuaTable? config = null)
    {
        _context = context;
        GamemodeId = gamemodeId;
        Config = config;
        Players = parameters.Players;
        
        _state = LuaHelpers.OpenState();

        _state.Environment["SGM"] = new LuaServerGamemodeContext(this, context);

        _state.ModuleLoader = CompositeModuleLoader.Create(new RadpackModuleLoader(radpack.Files), _state.ModuleLoader!);
        var results = _state.DoString(radpack.Files["server"]);
        if (results is [var value] && value.TryRead<LuaTable>(out var resultTable))
        {
            _moduleTable = resultTable;
        }
    }

    public override void Begin()
    {
        _snapshot = null;
        Call("OnBegin");
    }

    public override void StartRace() => Call("OnStartRace");

    public override void End()
    {
        Call("OnEnd");
        _state?.Dispose();
        _state = null;
    }

    public override void GameTick() => Call("OnGameTick");

    public override void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload)
    {
        var envelope = MemoryPackSerializer.Deserialize<LuaEventEnvelope>(payload);

        Call("OnClientEvent", clientId.ToString(), envelope.Type, envelope.Payload);
    }

    public override GameStateSnapshot? GetStateSnapshot() => _snapshot;

    private void RegisterFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> fn)
        => _state!.Environment[name] = new LuaFunction(name, fn);

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
            Logging.Error($"[LuaServerGamemode:{GamemodeId}] {name} failed: {ex.Message}", ex);
        }
        return [LuaValue.Nil];
    }
}