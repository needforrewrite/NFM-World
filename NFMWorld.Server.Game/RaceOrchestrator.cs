using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using MemoryPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Plugins;
using NFMWorldLibrary.Multiplayer.HttpMessages;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Game Master — dumb UDP relay for v1.
/// Validates join tokens, relays PlayerState between clients, handles race finish.
///
/// v2: <see cref="WorkerManager"/> and the Worker project will handle replay-based
/// validation. For now, the relay is direct.
/// </summary>
public class RaceOrchestrator : IDisposable
{
    private readonly ConcurrentDictionary<uint, ClientInfo> _clients = new();
    private readonly IMultiplayerServerTransport _transport;

    // join token → (session, player index)
    private readonly ConcurrentDictionary<Guid, (RaceSession Session, byte PlayerIndex, Guid ClientId)> _joinTokens = new();
    private readonly ConcurrentDictionary<uint, RaceSession> _sessions = new();

    // Server gamemode tick loop
    private CancellationTokenSource? _tickCts;
    private Thread? _tickThread;

    public RaceOrchestrator(IMultiplayerServerTransport transport)
    {
        _transport = transport;
        transport.PacketReceived += TransportOnPacketReceived;
        transport.ClientConnected += TransportOnClientConnected;
        transport.ClientDisconnected += TransportOnClientDisconnected;
        transport.ClientConnecting += TransportOnClientConnecting;
    }

    public void Start()
    {
        _transport.Start();
        StartServerTick();
    }

    public void Stop()
    {
        StopServerTick();
        _transport.Stop();
    }

    // ── Server gamemode tick loop (20 TPS) ────────────────────────────

    private void StartServerTick()
    {
        _tickCts = new CancellationTokenSource();
        _tickThread = new Thread(() =>
        {
            while (!_tickCts.Token.IsCancellationRequested)
            {
                try
                {
                    TickServerGamemodes();
                    Thread.Sleep(50); // 20 TPS
                }
                catch (Exception ex)
                {
                    Logging.Error($"[GM] Server tick error: {ex}");
                }
            }
        })
        {
            IsBackground = true,
            Name = "GMServerTick"
        };
        _tickThread.Start();
    }

    private void StopServerTick()
    {
        _tickCts?.Cancel();
        _tickThread?.Join(TimeSpan.FromSeconds(1));
        _tickCts?.Dispose();
        _tickCts = null;
        _tickThread = null;
    }

    private void TickServerGamemodes()
    {
        foreach (var (_, session) in _sessions)
        {
            var gm = session.ServerGamemode;
            if (gm == null) continue;

            gm.GameTick();

            var snapshot = gm.GetStateSnapshot();
            if (!session.ResultsBroadcasted && snapshot is { IsFinished: true, Results: { } results })
            {
                // To this race's clients only: the GM runs many sessions, and a broadcast
                // would end every other race on the host with these standings.
                session.ResultsBroadcasted = true;
                session.LastResults = results;
                _transport.SendPacketToClients(
                    session.Clients.Keys.ToArray(),
                    new S2C_GameFinished { Results = results },
                    reliable: true);

                ReportToLobby(session, results);

                // The race is over, so the gamemode and the plugin assembly behind it are
                // done: release both now rather than holding them until shutdown. Sessions
                // are not removed here, so player state keeps relaying as before - the
                // gamemode is simply no longer on the receiving end of it.
                session.ServerGamemode = null;
                session.Factory?.End();
                session.Factory = null;
            }
        }

        // Finished sessions released their plugin in a frame of this tick loop, which is
        // still on the stack; the check only sees the truth on a later tick.
        PluginUnloadVerifier.Verify();
    }

    /// <summary>
    /// Tells the lobby this race is over. The lobby is the only party that knows which account
    /// each player slot belongs to, so it composes the report to the archive; the game master
    /// deliberately has no archive key of its own.
    /// </summary>
    private void ReportToLobby(RaceSession session, RaceResults results)
    {
        var lobby = Environment.GetEnvironmentVariable("LOBBY_RESULTS_URL");
        if (string.IsNullOrEmpty(lobby))
        {
            Logging.Info("[GM] No LOBBY_RESULTS_URL configured; the lobby is not told this race ended.");
            return;
        }

        var body = MemoryPackSerializer.Serialize(new RaceServer2Lobby_RaceResults
        {
            MatchKey = session.MatchKey.ToString(),
            Results = results
        });

        var keyId = Environment.GetEnvironmentVariable("HMAC_KEY_ID") ?? "primary";
        var secret = Convert.FromBase64String(Environment.GetEnvironmentVariable("HMAC_SECRET_KEY") ?? "");
        var auth = HmacAuth.SignWithNonce("POST", "/race-ended", body, keyId, secret, Guid.NewGuid().ToString("N"));

        _ = Task.Run(async () =>
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                using var request = new HttpRequestMessage(HttpMethod.Post, lobby)
                {
                    Content = new ByteArrayContent(body)
                };
                request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(auth);

                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    Logging.Warning($"[GM] Lobby rejected the result report: {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                Logging.Warning($"[GM] Could not report the race result to the lobby: {ex.Message}");
            }
        });
    }

    // ── Transport events ─────────────────────────────────────────────

    private void TransportOnClientConnecting(object? sender, uint clientIndex)
        => _clients.TryAdd(clientIndex, new ClientInfo { State = ClientState.Connecting });

    private void TransportOnClientConnected(object? sender, uint clientIndex)
    {
        if (_clients.TryGetValue(clientIndex, out var c))
            c.State = ClientState.Connected;
    }

    private void TransportOnClientDisconnected(object? sender, uint clientIndex)
        => _clients.TryRemove(clientIndex, out _); // TODO send disconnect packet in session

    // ── Packet dispatch ──────────────────────────────────────────────

    private void TransportOnPacketReceived(object? sender,
        (uint ClientIndex, IPacketClientToServer Packet) e)
    {
        switch (e.Packet)
        {
            case C2S_RaceLoaded raceLoaded:
                HandleRaceLoaded(e.ClientIndex, raceLoaded);
                break;
            case C2S_PlayerState playerState:
                HandlePlayerState(e.ClientIndex, playerState);
                break;
            case C2S_ClientEvent clientEvent:
                HandleClientEvent(e.ClientIndex, clientEvent);
                break;
        }
    }

    // ── Packet handlers ──────────────────────────────────────────────

    private void HandleClientEvent(uint clientIndex, C2S_ClientEvent clientEvent)
    {
        try
        {
            if (!TryFindSession(clientIndex, out var session, out var clientId)) return;
            session.ServerGamemode?.OnClientEvent(clientId, clientEvent.Payload.Span);
        }
        finally
        {
            clientEvent.Dispose();
        }
    }

    private void HandleRaceLoaded(uint clientIndex, C2S_RaceLoaded raceLoaded)
    {
        // A token belongs to one player for the life of the session. It stays reclaimable so a
        // client whose connection dropped can attach again, but the *same* player may not use it
        // to occupy a second slot.
        if (!_joinTokens.TryGetValue(raceLoaded.JoinToken, out var entry))
        {
            Logging.Warning($"[GM] Invalid join token from client {clientIndex}");
            return;
        }

        var clientId = entry.ClientId;
        var session = entry.Session;

        // Already attached under another connection index: this is a reconnect. Move the
        // mapping rather than counting the player as loaded twice. A loop, not FirstOrDefault:
        // the struct default is 0, which is a real slot, so every second player looked
        // attached and was never counted as loaded.
        uint? existingIndex = null;
        foreach (var (index, id) in session.Clients)
        {
            if (id == clientId)
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex is { } attached)
        {
            if (attached != clientIndex)
                session.Clients.TryRemove(attached, out _);
            session.Clients[clientIndex] = clientId;

            Logging.Info($"[GM] Client {clientId} re-attached");
            // Everything it needs to rejoin the race as it was.
            if (session.ResultsBroadcasted)
                _transport.SendPacketToClient(clientIndex,
                    new S2C_GameFinished { Results = session.LastResults });
            else if (session.LoadedCount >= session.PlayerCount)
                _transport.SendPacketToClient(clientIndex, new S2C_RaceCanStart());
            return;
        }

        session.Clients[clientIndex] = clientId;
        session.LoadedCount++;

        Logging.Info(
            $"[GM] Client {clientId} loaded ({session.LoadedCount}/{session.PlayerCount})");

        if (session.LoadedCount >= session.PlayerCount)
        {
            session.ServerGamemode?.StartRace();
            _transport.SendPacketToClients(session.Clients.Keys.ToArray(), new S2C_RaceCanStart());
            Logging.Info($"[GM] Race starting: {session.PlayerCount} players");
        }
    }

    private void HandlePlayerState(uint clientIndex, C2S_PlayerState playerState)
    {
        if (!TryFindSession(clientIndex, out var session, out var clientId)) return;

        // A peer's state is relayed verbatim to the other clients and fed to the physics
        // angle-normalisation loops, so an absurd angle (or speed) from one client would
        // freeze every client in the race. Drop the packet rather than pass it on.
        if (!IsSanePlayerState(playerState.State))
            return;

        // Store the latest state for server gamemode queries. Wasted comes out of the same
        // frame: the gamemode decides who died when, but only the car's owner knows it died.
        var frame = playerState.State.CarFrame;
        var pos = frame.CarPosition;
        session.PlayerPositions[clientId] = new f64Vector3(pos.X, pos.Y, pos.Z);
        session.PlayerWasted[clientId] = frame.TheBitFlags.Wasted;

        var others = session.Clients.Keys.Where(idx => idx != clientIndex).ToArray();
        if (others.Length > 0)
        {
            _transport.SendPacketToClients(others, new S2C_PlayerState
            {
                PlayerId = clientId,
                State = playerState.State,
                CurrentServerTime = DateTimeOffset.UtcNow
            }, false);
        }
    }

    /// <summary>
    /// Cheap sanity check on a relayed car state. The angle fields are what the receiving
    /// physics walks down in 360-degree steps, and speed drives its correction loops, so
    /// bounds here are what stops one client freezing the others.
    /// </summary>
    private static bool IsSanePlayerState(PlayerState state)
    {
        const double maxAngle = 1_000_000;
        const double maxSpeed = 10_000;

        var rotation = state.CarFrame.CarRotation;
        var speed = (double)state.CarFrame.Speed;

        return double.IsFinite((double)rotation.Xz) && Math.Abs((double)rotation.Xz) <= maxAngle
               && double.IsFinite((double)rotation.Pzy) && Math.Abs((double)rotation.Pzy) <= maxAngle
               && double.IsFinite((double)rotation.Pxy) && Math.Abs((double)rotation.Pxy) <= maxAngle
               && double.IsFinite(speed) && Math.Abs(speed) <= maxSpeed;
    }

    // ── HTTP handler ─────────────────────────────────────────────────

    public Lobby2RaceServer_CreateRaceResponse CreateRace(Lobby2RaceServer_CreateRace raceParams)
    {
        var sessionId = Interlocked.Increment(ref _sessionIdCounter);
        var joinTokens = new Dictionary<byte, Guid>();

        var session = new RaceSession
        {
            Id = sessionId,
            MatchKey = raceParams.MatchKey,
            PlayerCount = raceParams.MatchGameplayInfo.Players.Count,
            Clients = [],
            PlayerInfos = raceParams.MatchGameplayInfo.Players,
            Gamemode = raceParams.MatchGameplayInfo.Gamemode
        };

        foreach (var (playerIndex, playerInfo) in raceParams.MatchGameplayInfo.Players)
        {
            var token = Guid.NewGuid();
            joinTokens[playerIndex] = token;
            _joinTokens.TryAdd(token, (session, playerIndex, playerInfo.Id));
        }

        var factory = GamemodeFactory.Create(raceParams.MatchGameplayInfo.Gamemode, raceParams.MatchGameplayInfo.Parameters);
        var context = new OrchestratorServerContext(
            session,
            raceParams.MatchGameplayInfo.StageName,
            payload => _transport.SendPacketToClients(
                session.Clients.Keys.ToArray(),
                new S2C_ServerEvent { Payload = payload },
                reliable: true));

        // TODO this is a bit janky and could probably be improved
        var gm = factory.CreateServerGamemode(new ServerGamemodeParameters()
        {
            Players = raceParams.MatchGameplayInfo.Players.Values.ToArray()
        }, context);
        
        session.ServerGamemode = gm;
        session.Factory = factory;
        
        gm.Begin();

        _sessions.TryAdd(sessionId, session);

        Logging.Info(
            $"[GM] Race created: {raceParams.MatchKey}, {joinTokens.Count} players");

        return new Lobby2RaceServer_CreateRaceResponse
        {
            PlayerSecretIds = joinTokens
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private bool TryFindSession(uint clientIndex, [NotNullWhen(true)] out RaceSession? session, out Guid clientId)
    {
        foreach (var (_, s) in _sessions)
        {
            if (s.Clients.TryGetValue(clientIndex, out var id))
            {
                session = s;
                clientId = id;
                return true;
            }
        }

        session = null;
        clientId = Guid.Empty;
        return false;
    }

    public void Dispose()
    {
        StopServerTick();

        // End() on all server gamemodes before tearing down. Sessions whose race
        // already finished have had both released already, so both are null-guarded
        // here (and End() itself is idempotent).
        foreach (var (_, session) in _sessions)
        {
            session.ServerGamemode?.End();
            session.Factory?.End();
        }

        _transport.PacketReceived -= TransportOnPacketReceived;
        _transport.ClientConnected -= TransportOnClientConnected;
        _transport.ClientDisconnected -= TransportOnClientDisconnected;
        _transport.ClientConnecting -= TransportOnClientConnecting;
    }

    private class ClientInfo
    {
        public ClientState State { get; set; }
    }

    private class RaceSession
    {
        public uint Id { get; set; }

        /// <summary>The lobby's key for this match; the report back to the lobby is identified by it.</summary>
        public Guid MatchKey { get; set; }

        public string Gamemode { get; set; } = DefaultGamemodes.PvP;
        public int PlayerCount { get; set; }
        public int LoadedCount { get; set; }
        public ConcurrentDictionary<uint, Guid> Clients { get; set; } = [];
        public IServerGamemode? ServerGamemode { get; set; }

        /// <summary>Owns the gamemode's plugin load context; released alongside it.</summary>
        public BaseGamemodeFactory? Factory { get; set; }
        public ConcurrentDictionary<Guid, f64Vector3> PlayerPositions { get; set; } = [];

        /// <summary>Last relayed wasted flag per player; absent until their first state.</summary>
        public ConcurrentDictionary<Guid, bool> PlayerWasted { get; set; } = [];
        public IDictionary<byte, ServerSidePlayerInfo> PlayerInfos { get; set; } = new Dictionary<byte, ServerSidePlayerInfo>();
        public bool ResultsBroadcasted { get; set; }

        /// <summary>Kept so a client that reconnects after the finish still gets the results.</summary>
        public RaceResults LastResults { get; set; } = default!;
    }

    /// <summary>
    /// Adapter that provides <see cref="IServerGamemodeContext"/> from
    /// the RaceSession data available in the orchestrator.
    /// </summary>
    private class OrchestratorServerContext(
        RaceSession session,
        string stageName,
        Action<ReadOnlyMemory<byte>> broadcast) : IServerGamemodeContext
    {
        public BackendStage CurrentStage { get; } = new(stageName);


        public f64Vector3? GetPlayerPosition(Guid playerId) =>
            session.PlayerPositions.TryGetValue(playerId, out var pos) ? pos : null;

        public bool? IsPlayerWasted(Guid playerId) =>
            session.PlayerWasted.TryGetValue(playerId, out var wasted) ? wasted : null;

        public void BroadcastEvent(ReadOnlyMemory<byte> payload) => broadcast(payload);
    }

    private static uint _sessionIdCounter;
}