using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using MemoryPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;
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

    // join token → (session, player index, player id, whether this token only watches)
    private readonly ConcurrentDictionary<Guid, (RaceSession Session, byte PlayerIndex, Guid ClientId, bool Spectator)>
        _joinTokens = new();

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
            }
        )
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

            // Before the tick, so a baseline goes out ahead of the first delta this tick produces,
            // and on this thread, which is the only one allowed to read or start the gamemode.
            while (session.PendingAttach.TryDequeue(out var clientIndex))
                SendAttach(session, gm, clientIndex);

            // The last racer to load starts the race from here rather than from its attach, so
            // its baseline is sent ahead of the start notice like every other attach.
            if (!session.RaceStarted
                && !session.ResultsBroadcasted
                && session.LoadedCount >= session.PlayerCount)
            {
                session.RaceStarted = true;
                gm.StartRace();
                _transport.SendPacketToClients(
                    session.Clients.Keys.ToArray(),
                    new S2C_RaceCanStart(),
                    reliable: true
                );
                Logging.Info($"[GM] Race starting: {session.PlayerCount} players");
            }

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
                    reliable: true
                );

                ReportToLobby(session, results);

                // The race is over, so the gamemode and the plugin assembly behind it are
                // done: release both now rather than holding them until shutdown. Sessions
                // are not removed here, so player state keeps relaying as before - the
                // gamemode is simply no longer on the receiving end of it.
                session.ServerGamemode = null;
                session.Factory?.End();
                session.Factory = null;
                continue;
            }

            // Players never arrived. Without this the race waits on them for the life of the
            // process, and every one of them sits on "waiting for other players to load".
            //
            // Only before the race starts: a racer leaving mid-race lowers LoadedCount below
            // PlayerCount for good, and without RaceStarted here the abandoned race fired at the
            // deadline and told everyone still driving that the Game Master had gone.
            if (!session.RaceStarted
                && !session.ResultsBroadcasted
                && session.ServerGamemode is not null
                && session.LoadedCount < session.PlayerCount
                && DateTimeOffset.UtcNow >= session.LoadDeadline)
            {
                AbandonSession(session);
            }
        }

        // Finished sessions released their plugin in a frame of this tick loop, which is
        // still on the stack; the check only sees the truth on a later tick.
        PluginUnloadVerifier.Verify();
    }

    /// <summary>
    /// Hands a newly attached connection the game's current state, then its start notice. Both
    /// go out here rather than at attach time so the baseline is ordered ahead of the first
    /// delta the client would otherwise apply on top of nothing.
    /// </summary>
    private void SendAttach(RaceSession session, IServerGamemode gm, uint clientIndex)
    {
        if (session.ResultsBroadcasted)
        {
            _transport.SendPacketToClient(
                clientIndex,
                new S2C_GameFinished { Results = session.LastResults },
                reliable: true
            );
            return;
        }

        if (gm.CaptureState() is { Length: > 0 } state)
            _transport.SendPacketToClient(
                clientIndex,
                new S2C_ServerState { Payload = state },
                reliable: true
            );

        if (session.LoadedCount >= session.PlayerCount)
            _transport.SendPacketToClient(clientIndex, new S2C_RaceCanStart(), reliable: true);
    }

    /// <summary>
    /// Ends a race nobody loaded into. Told about it explicitly so a waiting client leaves the
    /// screen, and reported to the lobby so the match stops being listed and its players are
    /// released - an abandoned race is not rated.
    /// </summary>
    private void AbandonSession(RaceSession session)
    {
        session.ResultsBroadcasted = true;
        session.LoadDeadline = DateTimeOffset.MaxValue;

        _transport.SendPacketToClients(
            session.Clients.Keys.ToArray(),
            new S2C_RaceFailedToStart(),
            reliable: true
        );

        Logging.Warning(
            $"[GM] Race {session.MatchKey} abandoned: {session.LoadedCount}/{session.PlayerCount} loaded before the deadline"
        );

        // An empty standing order: the race produced no result, but the lobby has to learn that
        // it is over or the session stays listed and its players stay flagged in a race.
        ReportToLobby(
            session,
            new RaceResults
            {
                Standings = [],
                RaceDuration = TimeSpan.Zero,
                GamemodeId = session.Gamemode
            }
        );

        session.ServerGamemode = null;
        session.Factory?.End();
        session.Factory = null;
    }

    /// <summary>
    /// How long a created race waits for its players to reach this server before it is
    /// abandoned. Matches the lobby's own load deadline, which no longer covers this phase.
    /// </summary>
    private const int LoadTimeoutSeconds = 20;

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

        var body = MemoryPackSerializer.Serialize(
            new RaceServer2Lobby_RaceResults
            {
                MatchKey = session.MatchKey.ToString(),
                Results = results
            }
        );

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
            }
        );
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
    {
        _clients.TryRemove(clientIndex, out _);

        // A connection leaving is the only notice this server gets that a racer walked out:
        // the client drops the transport on quit instead of sending a forfeit. The gamemode
        // must hear it, or the race keeps ticking for a player who is not there.
        foreach (var (_, session) in _sessions)
        {
            if (!session.Clients.TryRemove(clientIndex, out var clientId))
                continue;

            // Only a racer was counted as loaded. A spectator was given a throwaway id and
            // never incremented the count, so decrementing for one would undercount the race.
            var isRacer = session.PlayerInfos.Values.Any(p => p.Id == clientId);
            if (isRacer)
            {
                session.LoadedCount = Math.Max(0, session.LoadedCount - 1);
                session.ServerGamemode?.OnClientDisconnected(clientId);
            }

            Logging.Info($"[GM] {(isRacer ? "Client" : "Spectator")} {clientId} left the race");
            return;
        }
    }

    // ── Packet dispatch ──────────────────────────────────────────────

    private void TransportOnPacketReceived(
        object? sender,
        (uint ClientIndex, IPacketClientToServer Packet) e
    )
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

        var session = entry.Session;

        // A spectator attaches under its token but is not a racer: it must not advance
        // LoadedCount, or the race would start waiting on a player who never drives, and it
        // owns no player id, so it can never be taken for one of the people racing. It still
        // goes into Clients, which is the list every S2C_PlayerState is relayed to - that is
        // how it sees the cars.
        if (entry.Spectator)
        {
            session.Clients[clientIndex] = Guid.NewGuid();
            Logging.Info($"[GM] Spectator attached on connection {clientIndex}");
            session.PendingAttach.Enqueue(clientIndex);
            return;
        }

        var clientId = entry.ClientId;

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
            // Reconnected mid-race, so it is as behind as a fresh attach and needs the same thing.
            session.PendingAttach.Enqueue(clientIndex);
            return;
        }

        session.Clients[clientIndex] = clientId;
        session.LoadedCount++;

        Logging.Info(
            $"[GM] Client {clientId} loaded ({session.LoadedCount}/{session.PlayerCount})"
        );

        // The tick starts the race once LoadedCount is complete, after catching this connection up.
        session.PendingAttach.Enqueue(clientIndex);
    }

    private void HandlePlayerState(uint clientIndex, C2S_PlayerState playerState)
    {
        if (!TryFindSession(clientIndex, out var session, out var clientId)) return;

        // A peer's state is relayed verbatim to the other clients and fed to the physics
        // angle-normalisation loops, so an absurd angle (or speed) from one client would
        // freeze every client in the race. Drop the packet rather than pass it on.
        if (!IsSanePlayerState(playerState.State))
            return;

        // An empty id means the sender's own car. Anything else names a player, and a client
        // may only speak for the bots it owns - otherwise it could drive another human's car.
        var reportedId = clientId;
        if (playerState.PlayerId != Guid.Empty)
        {
            var owned = false;
            foreach (var info in session.PlayerInfos.Values)
            {
                if (info.Id == playerState.PlayerId && info.IsBot && info.OwnerId == clientId)
                {
                    owned = true;
                    break;
                }
            }

            if (!owned)
                return;

            reportedId = playerState.PlayerId;
        }

        // Store the latest state for server gamemode queries. Wasted comes out of the same
        // frame: the gamemode decides who died when, but only the car's owner knows it died.
        var frame = playerState.State.CarFrame;
        var pos = frame.CarPosition;
        session.PlayerPositions[reportedId] = new f64Vector3(pos.X, pos.Y, pos.Z);
        session.PlayerWasted[reportedId] = frame.TheBitFlags.Wasted;

        var others = session.Clients.Keys.Where(idx => idx != clientIndex).ToArray();
        if (others.Length > 0)
        {
            _transport.SendPacketToClients(
                others,
                new S2C_PlayerState
                {
                    PlayerId = reportedId,
                    State = playerState.State,
                    CurrentServerTime = DateTimeOffset.UtcNow
                },
                false
            );
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

        return double.IsFinite((double)rotation.Xz)
               && Math.Abs((double)rotation.Xz) <= maxAngle
               && double.IsFinite((double)rotation.Pzy)
               && Math.Abs((double)rotation.Pzy) <= maxAngle
               && double.IsFinite((double)rotation.Pxy)
               && Math.Abs((double)rotation.Pxy) <= maxAngle
               && double.IsFinite(speed)
               && Math.Abs(speed) <= maxSpeed;
    }

    // ── HTTP handler ─────────────────────────────────────────────────

    public Lobby2RaceServer_CreateRaceResponse CreateRace(Lobby2RaceServer_CreateRace raceParams)
    {
        // The lobby is trusted infrastructure, but its roster is still assembled from client
        // claims. Reject before any stage object is constructed, so nothing can be loaded from a
        // path a player chose.
        if (MultiplayerContentPolicy.ValidateMatch(raceParams.MatchGameplayInfo) is { } refusal)
            throw new InvalidOperationException(refusal);

        EnsureContentPrepared(raceParams.MatchGameplayInfo);

        var sessionId = Interlocked.Increment(ref _sessionIdCounter);
        var joinTokens = new Dictionary<byte, Guid>();

        var session = new RaceSession
        {
            Id = sessionId,
            MatchKey = raceParams.MatchKey,
            // Only a player with a connection loads. A bot never sends C2S_RaceLoaded, so
            // counting it here would leave the race waiting for a player that cannot arrive.
            PlayerCount = raceParams.MatchGameplayInfo.Players.Values.Count(p => !p.IsBot),
            Clients = [],
            PlayerInfos = raceParams.MatchGameplayInfo.Players,
            Gamemode = raceParams.MatchGameplayInfo.Gamemode,
            // From creation, not from the first load: a race whose players never reach this
            // server at all would otherwise wait on them forever.
            LoadDeadline = DateTimeOffset.UtcNow.AddSeconds(LoadTimeoutSeconds)
        };

        foreach (var (playerIndex, playerInfo) in raceParams.MatchGameplayInfo.Players)
        {
            // A bot has no client to hand a token to; its state arrives from the client that
            // simulates it, under the bot's own player id.
            if (playerInfo.IsBot)
                continue;

            var token = Guid.NewGuid();
            joinTokens[playerIndex] = token;
            _joinTokens.TryAdd(token, (session, playerIndex, playerInfo.Id, false));
        }

        var factory = GamemodeFactory.Create(raceParams.MatchGameplayInfo.Gamemode, raceParams.MatchGameplayInfo.Parameters);
        var context = new OrchestratorServerContext(
            session,
            raceParams.MatchGameplayInfo.StageName,
            payload => _transport.SendPacketToClients(
                session.Clients.Keys.ToArray(),
                new S2C_ServerEvent { Payload = payload },
                reliable: true
            )
        );

        // TODO this is a bit janky and could probably be improved
        var gm = factory.CreateServerGamemode(
            new ServerGamemodeParameters()
            {
                Players = raceParams.MatchGameplayInfo.Players.Values.ToArray()
            },
            context
        );

        session.ServerGamemode = gm;
        session.Factory = factory;

        gm.Begin();

        _sessions.TryAdd(sessionId, session);

        Logging.Info(
            $"[GM] Race created: {raceParams.MatchKey}, {joinTokens.Count} players"
        );

        // One token for every onlooker: a spectator takes no slot and speaks for no car, so
        // there is nothing per-person to keep apart. PlayerIndex is unused for it.
        var spectatorToken = Guid.NewGuid();
        _joinTokens.TryAdd(spectatorToken, (session, 0, Guid.Empty, true));

        return new Lobby2RaceServer_CreateRaceResponse
        {
            PlayerSecretIds = joinTokens,
            SpectatorToken = spectatorToken
        };
    }

    /// <summary>
    /// Every asset this race will read must already be in the prepared cache or be genuine
    /// shipped content: constructing a <see cref="BackendStage"/> or a car from an unprepared
    /// archive id would do synchronous HTTP on the server's request thread.
    /// </summary>
    private static void EnsureContentPrepared(MatchGameplayInfo info)
    {
        if (RadpackReader.IsArchiveId(info.StageName) && !ContentCache.IsPrepared(info.StageName))
            throw new InvalidOperationException($"Stage '{info.StageName}' has not been prepared.");

        if (!RadpackReader.IsArchiveId(info.StageName)
            && !VFS.FileExists($"data/stages/{info.StageName}.txt")
            && !VFS.FileExists($"data/stages/{info.StageName}.radpack"))
            throw new InvalidOperationException($"Stage '{info.StageName}' is not installed on this server.");

        foreach (var player in info.Players.Values)
        {
            if (RadpackReader.IsArchiveId(player.CarName))
            {
                if (!ContentCache.IsPrepared(player.CarName))
                    throw new InvalidOperationException($"Car '{player.CarName}' has not been prepared.");
                continue;
            }

            if (BackendGameSparker.GetCar(player.CarName).Rad is null)
                throw new InvalidOperationException($"Car '{player.CarName}' is not installed on this server.");
        }
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

        /// <summary>
        /// When a race still waiting for players to load is abandoned. Nothing else ends a race
        /// whose clients never arrive: the lobby's own load deadline applies to a different phase
        /// and is cleared before this one starts.
        /// </summary>
        public DateTimeOffset LoadDeadline { get; set; }

        public int PlayerCount { get; set; }
        public int LoadedCount { get; set; }
        public ConcurrentDictionary<uint, Guid> Clients { get; set; } = [];

        /// <summary>
        /// Connections that have loaded but not yet been handed the gamemode's state. Handled on
        /// the tick thread: the attach arrives on the transport thread, and both reading the
        /// gamemode and sending before its first delta have to happen where the tick runs.
        /// </summary>
        public ConcurrentQueue<uint> PendingAttach { get; } = new();

        public IServerGamemode? ServerGamemode { get; set; }

        /// <summary>Owns the gamemode's plugin load context; released alongside it.</summary>
        public BaseGamemodeFactory? Factory { get; set; }

        public ConcurrentDictionary<Guid, f64Vector3> PlayerPositions { get; set; } = [];

        /// <summary>Last relayed wasted flag per player; absent until their first state.</summary>
        public ConcurrentDictionary<Guid, bool> PlayerWasted { get; set; } = [];

        public IDictionary<byte, ServerSidePlayerInfo> PlayerInfos { get; set; } = new Dictionary<byte, ServerSidePlayerInfo>();

        /// <summary>Set once every player has loaded and the gamemode has been started.</summary>
        public bool RaceStarted { get; set; }

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
        Action<ReadOnlyMemory<byte>> broadcast
    ) : IServerGamemodeContext
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
