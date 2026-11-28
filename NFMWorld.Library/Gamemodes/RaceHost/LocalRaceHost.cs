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
    private Guid[] _serverPlayerIds = [];
    private bool _started;
    private bool _finished;
    private BaseGamemodeFactory? _pendingFactory;
    private ServerGamemodeParameters? _pendingParameters;

    public bool IsConnected => true;

    public event Action? RaceCanStart;
    public event Action? RaceFailedToStart;
    public event Action<int, PlayerState>? PlayerStateReceived;
    public event Action<ReadOnlyMemory<byte>>? ServerEventReceived;
    public event Action<ReadOnlyMemory<byte>>? ServerStateReceived;
    public event Action<RaceResults>? GameFinished;

    private LocalRaceHost(
        string stageName,
        ClientGamemodeParameters parameters,
        ServerSidePlayerInfo serverSidePlayerInfo,
        StageLoader? stageData
    )
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
        StageLoader? stageData = null
    ) => CreateCore(stageName, factory, parameters, stageData, deferred: false);

    public static LocalRaceHost CreateDeferred(
        string stageName,
        BaseGamemodeFactory factory,
        ClientGamemodeParameters parameters
    )
        => CreateCore(stageName, factory, parameters, null, deferred: true);

    private static LocalRaceHost CreateCore(
        string stageName,
        BaseGamemodeFactory factory,
        ClientGamemodeParameters parameters,
        StageLoader? stageData,
        bool deferred
    )
    {
        var clientPlayerIndex = parameters.Players.FindIndex(e => e.IsClientPlayer);

        if (clientPlayerIndex < 0)

            throw new InvalidOperationException("A local race needs exactly one client player.");

        var ownerId = Guid.NewGuid();
        var ids = new List<ServerSidePlayerInfo>(parameters.Players.Count);

        for (var i = 0; i < parameters.Players.Count; i++)
        {
            var player = parameters.Players[i];
            ids.Add(
                new ServerSidePlayerInfo
                {
                    Id = i == clientPlayerIndex ? ownerId : Guid.NewGuid(),
                    PlayerName = player.PlayerName,
                    CarName = player.CarName,
                    Color = player.Color,
                    CarColors = player.CarColors,
                    Wheels = player.Wheels,
                    IsBot = player.IsBot,
                    OwnerId = player.IsBot ? ownerId : Guid.Empty
                }
            );
        }

        var host = new LocalRaceHost(stageName, parameters, ids[clientPlayerIndex], stageData);
        host._serverPlayerIds = ids.Select(player => player.Id).ToArray();

        host._pendingFactory = factory;
        host._pendingParameters = new ServerGamemodeParameters { Players = ids };
        if (!deferred)
            host.InitializeServer();

        return host;
    }

    public void InitializeStage(BackendStage stage)
    {
        _context.PreparedStage = stage;
        InitializeServer();
    }

    private void InitializeServer()
    {
        if (_pendingFactory is not { } factory)

            return;
        var parameters = _pendingParameters!;
        _pendingFactory = null;
        _pendingParameters = null;
        _serverGamemode = factory.CreateServerGamemode(parameters, _context);
        _serverGamemode?.Begin();
    }

    /// <summary>
    /// Starts the race immediately (singleplayer has no loading sync),
    /// firing <see cref="RaceCanStart"/> synchronously.
    /// </summary>
    public void Start()
    {
        if (_pendingFactory is not null)

            throw new InvalidOperationException("The local race stage is still loading.");

        if (_started)

            return;

        _started = true;

        // Singleplayer attaches in-process, so there is nothing to catch up on: the client
        // gamemode has been driving this session from the start. Sent anyway when the gamemode
        // has state, so the client's handling is exercised by the same path as online play.
        if (_serverGamemode?.CaptureState() is { Length: > 0 } state)
            ServerStateReceived?.Invoke(state);

        _serverGamemode?.StartRace();
        RaceCanStart?.Invoke();
    }

    public void Update()
    {
        if (_serverGamemode is null)

            return;

        _serverGamemode.GameTick();
        var snapshot = _serverGamemode.GetStateSnapshot();

        if (!_finished && snapshot is { IsFinished: true, IsProvisional: false, Results: { } results })
        {
            _finished = true;
            GameFinished?.Invoke(results);
        }
    }

    public void Restart()
    {
        _context.CurrentStage.ResetDestruction();
        _serverGamemode?.End();
        _context.ClearPlayerPositions();
        _finished = false;
        _started = false;
        _serverGamemode?.Begin();
        Start();
    }

    public void SendServerEvent(ReadOnlyMemory<byte> payload)
        => _serverGamemode?.OnClientEvent(_localPlayerId, payload.Span);

    public void SendPlayerState(PlayerState state, Guid? playerId = null)
        => _context.RecordPlayerState(
            playerId ?? _localPlayerId,
            state.CarFrame.CarPosition.X,
            state.CarFrame.CarPosition.Y,
            state.CarFrame.CarPosition.Z,
            state.CarFrame.TheBitFlags.Wasted
        );

    public void SendPlayerStateForSlot(int slot, PlayerState state)
    {
        if (slot >= 0 && slot < _serverPlayerIds.Length)
            SendPlayerState(state, _serverPlayerIds[slot]);
    }

    public void Dispose()
    {
        _pendingFactory = null;
        _pendingParameters = null;
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
        private readonly Dictionary<Guid, bool> _wasted = new();
        private BackendStage? _stage;

        public BackendStage? PreparedStage
        {
            set => _stage = value;
        }

        public BackendStage CurrentStage => _stage ??= stageData is { } data
            ? new BackendStage(stageName, data)
            : new BackendStage(stageName);

        public double TicksPerSecond => Physics.TargetTps;

        public f64Vector3? GetPlayerPosition(Guid playerId)
            => _positions.TryGetValue(playerId, out var pos) ? pos : null;

        public bool? IsPlayerWasted(Guid playerId)
            => _wasted.TryGetValue(playerId, out var wasted) ? wasted : null;

        public void BroadcastEvent(ReadOnlyMemory<byte> payload)
            => broadcast(payload);

        public void RecordPlayerState(Guid playerId, fix64 x, fix64 y, fix64 z, bool wasted)
        {
            _positions[playerId] = new f64Vector3(x, y, z);
            _wasted[playerId] = wasted;
        }

        public void ClearPlayerPositions()
        {
            _positions.Clear();
            _wasted.Clear();
        }
    }
}
