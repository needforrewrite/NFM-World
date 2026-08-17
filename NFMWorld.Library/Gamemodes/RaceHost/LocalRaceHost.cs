using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Multiplayer;

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

    private LocalRaceHost(string stageName, GamemodeParameters parameters)
    {
        _context = new LocalServerContext(stageName, parameters, BroadcastToClient);
        _localPlayerId = _context.PlayerIds[
            parameters.Players
                .Select((p, i) => (p, i))
                .First(e => e.p.IsClientPlayer).i];
    }

    /// <summary>
    /// Creates a local host for the given factory. If the factory has no
    /// server gamemode (e.g., time trial), the host runs client-only.
    /// </summary>
    public static LocalRaceHost Create(
        string stageName,
        BaseGamemodeFactory factory,
        GamemodeParameters parameters)
    {
        var host = new LocalRaceHost(stageName, parameters);

        // TODO this is a bit janky and could probably be improved
        var serverGamemode = factory.CreateServerGamemode(parameters, host._context);
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
    }

    private void BroadcastToClient(ReadOnlyMemory<byte> payload)
        => ServerEventReceived?.Invoke(payload);

    /// <summary>
    /// Server-side data context for the local race. Backs
    /// <see cref="IServerGamemodeData"/> without any networking.
    /// </summary>
    private sealed class LocalServerContext : IServerGamemodeData
    {
        private readonly Dictionary<Guid, f64Vector3> _positions = new();
        private readonly Action<ReadOnlyMemory<byte>> _broadcast;

        public BackendStage CurrentStage { get; }
        public IReadOnlyList<Guid> PlayerIds { get; }
        public IReadOnlyDictionary<byte, PlayerInfo> PlayerInfos { get; }

        public LocalServerContext(string stageName, GamemodeParameters parameters, Action<ReadOnlyMemory<byte>> broadcast)
        {
            CurrentStage = new BackendStage(stageName);
            _broadcast = broadcast;

            var ids = new List<Guid>(parameters.Players.Count);
            var infos = new Dictionary<byte, PlayerInfo>();
            for (byte i = 0; i < parameters.Players.Count; i++)
            {
                var player = parameters.Players[i];
                var id = Guid.NewGuid();
                ids.Add(id);
                infos[i] = new PlayerInfo
                {
                    Id = id,
                    Name = player.PlayerName,
                    Vehicle = player.CarName,
                    Color = player.Color
                };
            }

            PlayerIds = ids;
            PlayerInfos = infos;
        }

        public f64Vector3? GetPlayerPosition(Guid playerId)
            => _positions.TryGetValue(playerId, out var pos) ? pos : null;

        public void BroadcastEvent(ReadOnlyMemory<byte> payload)
            => _broadcast(payload);

        public void RecordPlayerState(Guid playerId, fix64 x, fix64 y, fix64 z)
            => _positions[playerId] = new f64Vector3(x, y, z);
    }
}
