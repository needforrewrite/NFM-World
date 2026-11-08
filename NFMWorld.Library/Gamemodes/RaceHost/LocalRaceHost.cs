using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Gamemodes.RaceHost;

/// <summary>
/// In-process race host for singleplayer. Runs the factory's server gamemode
/// locally (when the gamemode has one) so singleplayer exercises the same
/// client/server split as online play. Events and player states are routed
/// directly, without serialization.
/// </summary>
public sealed class LocalRaceHost : IRaceHost
{
    private IServerGamemode? _serverGamemode;
    private readonly LocalServerContext _context;
    private readonly Guid _localPlayerId;
    private bool _started;
    private bool _finished;

    public bool IsConnected => true;

    public event Action? RaceCanStart;
    public event Action? RaceFailedToStart;
    public event Action<int, PlayerState>? PlayerStateReceived;
    public event Action<ReadOnlyMemory<byte>>? ServerEventReceived;
    public event Action<RaceResults>? GameFinished;

    private LocalRaceHost(string stageName, ClientGamemodeParameters parameters, ServerSidePlayerInfo serverSidePlayerInfo, StageLoader? stageData)
    {
        _context = new LocalServerContext(stageName, stageData, BroadcastToClient);
        _localPlayerId = serverSidePlayerInfo.Id;
    }

    /// <summary>
    /// Creates a local host for the given factory. If the factory has no
    /// server gamemode, the host runs client-only.
    /// </summary>
    /// <param name="stageData">
    /// The stage to use instead of reading the current stage files, for replay
    /// verification where the recorded stage must match the simulation.
    /// </param>
    public static LocalRaceHost Create(
        string stageName,
        BaseGamemodeFactory factory,
        ClientGamemodeParameters parameters,
        StageLoader? stageData = null)
    {
        var ids = new List<ServerSidePlayerInfo>(parameters.Players.Count);
        for (var i = 0; i < parameters.Players.Count; i++)
        {
            var player = parameters.Players[i];
            var id = Guid.NewGuid();
            ids.Add(new ServerSidePlayerInfo
            {
                Id = id,
                PlayerName = player.PlayerName,
                CarName = player.CarName,
                Color = player.Color
            });
        }

        var clientPlayerIndex = parameters.Players.FindIndex(e => e.IsClientPlayer);
        if (clientPlayerIndex < 0)
            throw new InvalidOperationException("A local race needs exactly one client player.");

        var host = new LocalRaceHost(stageName, parameters, ids[clientPlayerIndex], stageData);

        // TODO this is a bit janky and could probably be improved
        var serverGamemode = factory.CreateServerGamemode(new ServerGamemodeParameters
        {
            Players = ids
        }, host._context);
        host._serverGamemode = serverGamemode;
        serverGamemode?.Begin();

        return host;
    }

    /// <summary>
    /// Starts the race immediately (singleplayer has no loading sync),
    /// firing <see cref="RaceCanStart"/> synchronously.
    /// </summary>
    public void Start()
    {
        if (_started)
            return;

        _started = true;
        _serverGamemode?.StartRace();
        RaceCanStart?.Invoke();
    }

    public void Update()
    {
        if (_serverGamemode is null)
            return;

        _serverGamemode.GameTick();
        var snapshot = _serverGamemode.GetStateSnapshot();
        if (!_finished && snapshot is { IsFinished: true, Results: { } results })
        {
            _finished = true;
            GameFinished?.Invoke(results);
        }
    }

    public void Restart()
    {
        _serverGamemode?.End();
        _context.ClearPlayerPositions();
        _finished = false;
        _started = false;
        _serverGamemode?.Begin();
        Start();
    }

    public void SendServerEvent(ReadOnlyMemory<byte> payload)
        => _serverGamemode?.OnClientEvent(_localPlayerId, payload.Span);

    public void SendPlayerState(PlayerState state)
        => _context.RecordPlayerState(
            _localPlayerId,
            state.CarFrame.CarPosition.X,
            state.CarFrame.CarPosition.Y,
            state.CarFrame.CarPosition.Z);

    public void Dispose()
    {
        _serverGamemode?.End();

        // The instance's type comes from the gamemode plugin's load context, so this
        // host must not hold it past the race or the assembly can never be collected.
        _serverGamemode = null;
    }

    private void BroadcastToClient(ReadOnlyMemory<byte> payload)
        => ServerEventReceived?.Invoke(payload);

    /// <summary>
    /// Server-side data context for the local race. Backs
    /// <see cref="IServerGamemodeContext"/> without any networking.
    /// </summary>
    private sealed class LocalServerContext(string stageName, StageLoader? stageData, Action<ReadOnlyMemory<byte>> broadcast)
        : IServerGamemodeContext
    {
        private readonly Dictionary<Guid, f64Vector3> _positions = new();
        private BackendStage? _stage;

        public BackendStage CurrentStage => _stage ??= stageData is { } data
            ? new BackendStage(stageName, data)
            : new BackendStage(stageName);

        public f64Vector3? GetPlayerPosition(Guid playerId)
            => _positions.TryGetValue(playerId, out var pos) ? pos : null;

        public void BroadcastEvent(ReadOnlyMemory<byte> payload)
            => broadcast(payload);

        public void RecordPlayerState(Guid playerId, fix64 x, fix64 y, fix64 z)
            => _positions[playerId] = new f64Vector3(x, y, z);

        public void ClearPlayerPositions() => _positions.Clear();
    }
}
