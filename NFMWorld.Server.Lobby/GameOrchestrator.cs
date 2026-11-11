using System.Collections.Concurrent;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer.Archive;
using NFMWorldLibrary.Multiplayer.HttpMessages;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Lobby orchestrator — pure matchmaking, chat, and session management.
/// In-game relay has been moved to the Game Master.
/// </summary>
public class GameOrchestrator
{
    private readonly IMultiplayerServerTransport _transport;
    private readonly PlayerRegistry _players;
    private readonly SessionManager _sessions;
    private readonly ChatManager _chat;
    private readonly LobbyStateBroadcaster _broadcaster;
    private readonly GameMasterRegistry _gmRegistry;
    private readonly GameMasterHttpClient _gmClient;
    private readonly ArchiveClient? _archive;
    private readonly Lock _authenticationLock = new();

    /// <summary>Matches handed to a game master and not yet reported back, keyed by match key.</summary>
    private readonly ConcurrentDictionary<Guid, PendingMatch> _pendingMatches = new();

    /// <summary>Which match a session's race is, while it runs. An onlooker starts from the session.</summary>
    private readonly ConcurrentDictionary<uint, Guid> _sessionMatches = new();

    private sealed record PendingMatch(
        MatchGameplayInfo Info,
        uint SessionId,
        IpAndPort GameAddress,
        Guid SpectatorToken
    );

    private Thread? _lobbyThread;
    private bool _lobbyIsRunning = true;

    /// <summary>
    /// Turns a game master's result report into an archive report and sends it. Called from the
    /// /race-ended HTTP handler, which is the only place a trusted server proves itself.
    /// </summary>
    public async Task<bool> ReportMatchResultsAsync(Guid matchKey, RaceResults results, CancellationToken ct)
    {
        if (!_pendingMatches.TryRemove(matchKey, out var pending))
        {
            Logging.Warning($"[Lobby] Result for an unknown or already-reported match {matchKey}");
            return false;
        }

        // The race is over, so its players are back in the lobby and free to start another.
        // IsInGame is set when a race starts and was only ever cleared on timeout or failure.
        // The race is over, so nobody can be sent to watch it any more.
        _sessionMatches.TryRemove(pending.SessionId, out _);

        var session = _sessions.Get(pending.SessionId);
        if (session is not null)
        {
            session.State = SessionState.Finished;
            foreach (var (_, id) in session.Players)
            {
                var participant = _players.Get(id);
                if (participant is not null)
                    participant.IsInGame = false;
            }
        }

        if (_archive is not { CanReportResults: true })
            return false;

        // An abandoned race (nobody loaded) reports an empty standing order. There is nothing
        // to rate, and posting it would record a played match that was never played.
        if (results.Standings.Length == 0)
            return false;

        var players = new List<ResultPlayer>();
        foreach (var standing in results.Standings)
        {
            if (standing.PlayerId == Guid.Empty)
                continue;

            // Match the standing's player id back to the slot it was issued for. An id the
            // match does not know is dropped rather than attributed to slot 0.
            var slot = pending.Info.Players.FirstOrDefault(p => p.Value.Id == standing.PlayerId);
            if (slot.Value.Id != standing.PlayerId || slot.Value.Id == Guid.Empty)
                continue;

            players.Add(
                new ResultPlayer(
                    slot.Value.AccountId > 0 ? slot.Value.AccountId : null,
                    standing.FinishPosition,
                    standing.FinishTime?.Ticks,
                    IsBot: slot.Value.IsBot
                )
            );
        }

        return await _archive.ReportMatchAsync(
            new MatchReport(
                matchKey.ToString("N"),
                pending.Info.Gamemode,
                pending.Info.StageName,
                StageHash: null,
                results.RaceDuration.Ticks,
                players
            ),
            ct
        );
    }

    public GameOrchestrator(IMultiplayerServerTransport transport)
        : this(transport, CreateArchiveClient())
    {
    }

    public GameOrchestrator(IMultiplayerServerTransport transport, ArchiveClient? archive)
    {
        _transport = transport;
        _players = new PlayerRegistry();
        _sessions = new SessionManager(transport, _players);
        _chat = new ChatManager(transport, _players);
        _broadcaster = new LobbyStateBroadcaster(transport, _players, _sessions);

        _gmRegistry = GameMasterRegistry.FromEnvironment();
        _gmClient = new GameMasterHttpClient(
            Environment.GetEnvironmentVariable("HMAC_KEY_ID") ?? "primary",
            Environment.GetEnvironmentVariable("HMAC_SECRET_KEY") ?? ""
        );

        _archive = archive;

        transport.PacketReceived += TransportOnPacketReceived;
        transport.ClientConnected += TransportOnClientConnected;
        transport.ClientDisconnected += TransportOnClientDisconnected;
        transport.ClientConnecting += TransportOnClientConnecting;
    }

    private static ArchiveClient? CreateArchiveClient() =>
        Environment.GetEnvironmentVariable("ARCHIVE_URL") is { Length: > 0 } archiveUrl
            ? new ArchiveClient(
                archiveUrl,
                Environment.GetEnvironmentVariable("HMAC_KEY_ID"),
                Environment.GetEnvironmentVariable("ARCHIVE_SECRET_KEY")
            )
            : null;

    public void Start()
    {
        _transport.Start();
        _lobbyThread = new Thread(LobbyExec) { IsBackground = true };
        _lobbyThread.Start();
    }

    public void Stop()
    {
        _lobbyIsRunning = false;
        _transport.Stop();
        _gmRegistry.Dispose();
    }

    // ── Lobby tick loop ──────────────────────────────────────────────

    private void LobbyExec()
    {
        while (_lobbyIsRunning)
        {
            _sessions.PruneFinished();
            _broadcaster.BroadcastToAll();

            foreach (var session in _sessions.CheckTimeouts())
            {
                _transport.SendPacketToClients(
                    session.Players
                        .Select(kv => _players.Get(kv.Value)?.ClientIndex ?? null)
                        .Where(ci => ci.HasValue)
                        .Select(ci => ci!.Value)
                        .ToArray(),
                    new S2C_RaceFailedToStart(),
                    false
                );
            }

            Thread.Sleep(1000);
        }
    }

    // ── Transport events ─────────────────────────────────────────────

    /// <summary>
    /// Upper bound on simultaneous lobby connections. A public WebSocket endpoint with no
    /// limit lets one client exhaust memory and bandwidth cheaply.
    /// </summary>
    private const int MaxConnections = 512;

    private void TransportOnClientConnecting(object? sender, uint clientIndex)
    {
        if (_players.Count >= MaxConnections)
        {
            Logging.Warning($"[Lobby] Connection limit reached ({MaxConnections}); dropping client {clientIndex}");
            return;
        }

        _players.GetOrAdd(clientIndex, ClientState.Connecting);
        _broadcaster.BroadcastToAll();
    }

    private void TransportOnClientConnected(object? sender, uint clientIndex)
    {
        var client = _players.Get(clientIndex);
        client?.State = ClientState.Connected;

        _broadcaster.BroadcastToAll();
    }

    private void TransportOnClientDisconnected(object? sender, uint clientIndex)
    {
        if (_players.TryRemove(clientIndex, out var client) && client is not null)
        {
            // Leaving a race and returning to the lobby reuses this same connection, so the
            // disconnect only arrives when the client goes for good. Clearing the flag here is
            // what lets a player who quit a race start another one.
            client.IsInGame = false;
            if (client.InSession is { } inSession)
            {
                var session = _sessions.Get(inSession.SessionIndex);
                session?.Players.TryRemove(KeyValuePair.Create(inSession.PlayerIndex, client.Id));

                // Nobody is left in the session, so it can never start and nobody can join it.
                // Any state, not just the pre-race ones: a started race whose players all left
                // was left in the list as "1/2 racing" with START greyed out forever.
                if (session is not null && session.Players.IsEmpty)
                    _sessions.AbortSession(session.Id);
            }

            if (client.IsAuthenticated)
                _chat.BroadcastSystem($"{client.Name} has left...");
            _broadcaster.BroadcastToAll();
        }
    }

    // ── Packet dispatch ──────────────────────────────────────────────

    private void TransportOnPacketReceived(
        object? sender,
        (uint ClientIndex, IPacketClientToServer Packet) e
    )
    {
        if (e.Packet is not C2S_Authenticate
            && _players.Get(e.ClientIndex) is not { IsAuthenticated: true, AccountId: > 0 })
            return;

        switch (e.Packet)
        {
            case C2S_Authenticate authenticate:
                _ = HandleAuthenticateAsync(e.ClientIndex, authenticate);
                break;

            case C2S_PlayerIdentity identity:
                HandlePlayerIdentity(e.ClientIndex, identity);
                break;

            case C2S_CreateSession create:
                HandleCreateSession(e.ClientIndex, create);
                break;

            case C2S_JoinSession join:
                HandleJoinSession(e.ClientIndex, join);
                break;

            case C2S_SpectateSession spectate:
                HandleSpectateSession(e.ClientIndex, spectate);
                break;

            case C2S_LeaveSession leave:
                HandleLeaveSession(e.ClientIndex, leave);
                break;

            case C2S_LobbyChatMessage chat:
                HandleChatMessage(e.ClientIndex, chat);
                break;

            case C2S_LobbyPlayerReadyState ready:
                HandlePlayerReady(e.ClientIndex, ready);
                break;

            case C2S_LobbyStartRace startRace:
                _ = HandleStartRaceAsync(e.ClientIndex, startRace);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(e.Packet),
                    $"Unexpected packet type: {e.Packet.GetType().Name}"
                );
        }
    }

    // ── Packet handlers ──────────────────────────────────────────────

    private async Task HandleAuthenticateAsync(uint clientIndex, C2S_Authenticate authenticate)
    {
        PlayerRegistry.ClientInfo client;
        lock (_authenticationLock)
        {
            var connected = _players.Get(clientIndex);
            if (connected is null) return;
            client = connected;

            if (string.IsNullOrWhiteSpace(authenticate.Token))
            {
                RefuseAuthentication(clientIndex, "Sign in to an account before joining the lobby.");
                return;
            }

            if (client.PresentedToken == authenticate.Token)
                return;

            if (client.PresentedToken is not null)
            {
                RefuseAuthentication(clientIndex, "Reconnect to the lobby to change accounts.");
                return;
            }

            client.PresentedToken = authenticate.Token;
            if (_archive is null)
            {
                RefuseAuthentication(clientIndex, "The account server is not configured. Try again later.");
                return;
            }
        }

        var (status, account) = await _archive.ValidateTokenAsync(authenticate.Token, CancellationToken.None);
        lock (_authenticationLock)
        {
            // Verification may finish after disconnect, including after the index was reused.
            if (!ReferenceEquals(_players.Get(clientIndex), client))
                return;

            switch (status)
            {
                case TokenVerification.Valid:
                    if (_players.All.Any(p => p.Key != clientIndex && p.Value.AccountId == account!.AccountId))
                    {
                        RefuseAuthentication(clientIndex, "This account is already in the lobby on another connection.");
                        return;
                    }

                    client.AccountId = account!.AccountId;
                    client.Name = account.Username;
                    client.IsAuthenticated = true;
                    SendAuthenticated(clientIndex, true, account.Username, null, account.AccountId);
                    _broadcaster.BroadcastToAll();
                    break;

                case TokenVerification.Rejected:
                    RefuseAuthentication(clientIndex, "Your session has expired. Sign in again.", sessionRejected: true);
                    break;

                default:
                    RefuseAuthentication(clientIndex, "Could not reach the account server. Try again later.");
                    break;
            }
        }
    }

    private void RefuseAuthentication(uint clientIndex, string error, bool sessionRejected = false)
    {
        SendAuthenticated(clientIndex, false, "", error, accountId: 0, sessionRejected: sessionRejected);
        TransportOnClientDisconnected(this, clientIndex);
        _transport.DisconnectClient(clientIndex);
    }

    private void SendAuthenticated(
        uint clientIndex,
        bool accepted,
        string username,
        string? error,
        int? accountId = null,
        bool sessionRejected = false
    )
    {
        var client = _players.Get(clientIndex);
        _transport.SendPacketToClient(
            clientIndex,
            new S2C_Authenticated
            {
                Accepted = accepted,
                AccountId = accountId ?? client?.AccountId ?? 0,
                Username = username,
                Error = error,
                SessionRejected = sessionRejected
            }
        );
    }

    private void HandlePlayerIdentity(uint clientId, C2S_PlayerIdentity identity)
    {
        var client = _players.Get(clientId);
        if (client is null) return;

        client.Vehicle = identity.SelectedVehicle;
        client.Color = identity.Color;
    }

    private void HandleCreateSession(uint clientId, C2S_CreateSession create)
    {
        var player = _players.Get(clientId);
        if (player is null) return;

        var session = _sessions.CreateSession(
            clientId,
            create.StageName,
            (byte)Math.Clamp(create.MaxPlayers, 2, MatchGameplayInfo.MaxPlayers),
            create.GameMode,
            create.GameModeParameters,
            Math.Clamp(create.Bots, 0, MatchGameplayInfo.MaxPlayers - 1)
        );

        // The creator can be gone by the time this runs, and CreateSession reports that as null.
        if (session is null)
            return;

        _chat.BroadcastSystem(
            $"{session.CreatorName} has started a session for {session.StageName}!"
        );
        _broadcaster.BroadcastToAll();
    }

    private void HandleJoinSession(uint clientId, C2S_JoinSession join)
    {
        var player = _players.Get(clientId);
        if (player is null) return;

        var (joined, left) = _sessions.JoinSession(clientId, join.SessionId);

        if (left is not null)
            _chat.BroadcastSystem($"{player.Name} has left {left.CreatorName}'s session!");

        if (joined is not null)
            _chat.BroadcastSystem($"{player.Name} has joined {joined.CreatorName}'s session!");

        _broadcaster.BroadcastToAll();
    }

    /// <summary>
    /// Sends a client to watch a race that is already running. The client is not put in the
    /// session's player slots - the race is already under way and its roster is fixed - so this
    /// only hands over the game master's address and the spectator token.
    /// </summary>
    private void HandleSpectateSession(uint clientId, C2S_SpectateSession spectate)
    {
        var player = _players.Get(clientId);
        if (player is null || player.IsInGame) return;

        if (!_sessionMatches.TryGetValue(spectate.SessionId, out var matchKey)
            || !_pendingMatches.TryGetValue(matchKey, out var pending))
        {
            // The race is not running (or is already over), so there is nothing to watch.
            return;
        }

        _transport.SendPacketToClient(
            player.ClientIndex,
            new S2C_RaceStarted
            {
                MatchGameplayInfo = pending.Info,
                State = SessionState.Started,
                Spectator = true,
                SessionId = spectate.SessionId,
                JoinInfo = new S2C_RaceStarted.GameJoinInfo
                {
                    RaceServerIpAddress = pending.GameAddress,
                    JoinToken = pending.SpectatorToken
                }
            }
        );
    }

    private void HandleLeaveSession(uint clientId, C2S_LeaveSession leave)
    {
        var player = _players.Get(clientId);
        if (player is null) return;

        var session = _sessions.LeaveSession(clientId, leave.SessionId);
        if (session is not null)
        {
            // The client sends this on returning from a race, and it is already back in the
            // lobby: keeping IsInGame would leave it unable to start or watch anything again.
            player.IsInGame = false;

            // A session nobody is left in can never start, and nobody can join it either.
            if (session.Players.IsEmpty)
                _sessions.AbortSession(session.Id);

            _chat.BroadcastSystem(
                $"{player.Name} has left {session.CreatorName}'s session!"
            );
            _broadcaster.BroadcastToAll();
        }
    }

    private void HandleChatMessage(uint clientId, C2S_LobbyChatMessage chat)
    {
        var player = _players.Get(clientId);
        if (player is not null && !player.IsInGame)
            _chat.SendChatMessage(player.Id, chat.Message);
    }

    private void HandlePlayerReady(uint clientId, C2S_LobbyPlayerReadyState ready)
    {
        _sessions.SetPlayerReady(clientId, ready.SessionId, ready.IsReady);
    }

    private async Task HandleStartRaceAsync(uint clientId, C2S_LobbyStartRace startRace)
    {
        var sessionId = startRace.SessionId;
        if (!_sessions.StartRace(clientId, sessionId))
            return;

        var session = _sessions.Get(sessionId);
        if (session is null) return;

        var playerInfos = new Dictionary<byte, ServerSidePlayerInfo>();
        foreach (var (index, pid) in session.Players)
        {
            var p = _players.Get(pid);
            if (p is null)
                return;
            playerInfos[index] = new ServerSidePlayerInfo
            {
                Id = p.Id,
                PlayerName = p.Name,
                CarName = p.Vehicle,
                Color = p.Color,
                AccountId = p.AccountId
            };
        }

        // Bots occupy real slots in the match, so they are in the roster the clients build -
        // but they have no connection, so they get no join token and are sent nothing.
        foreach (var (index, bot) in session.Bots)
            playerInfos[index] = bot;

        var matchInfo = new MatchGameplayInfo
        {
            StageName = session.StageName,
            Gamemode = session.Gamemode,
            Parameters = new(session.GamemodeParameters),
            Players = playerInfos
        };

        ResolvedGameMaster? gm = null;
        try
        {
            gm = _gmRegistry.SelectGameMaster();
            var createRequest = new Lobby2RaceServer_CreateRace
            {
                MatchKey = Guid.NewGuid(),
                MatchGameplayInfo = matchInfo
            };

            var createResponse = await _gmClient.CreateRaceAsync(gm, createRequest);
            _gmRegistry.MarkSuccess(gm);

            // Use the GM's SRV-resolved game address (Lobby already knows it)
            var gameAddress = gm.GameAddress;

            // Kept until the game master reports the race over. This is what lets the lobby turn
            // the GM's player slots into accounts when the result comes back - and what lets a
            // spectator be handed a way into the race while it is still running.
            _pendingMatches[createRequest.MatchKey] = new PendingMatch(
                matchInfo,
                session.Id,
                gameAddress,
                createResponse.SpectatorToken
            );
            _sessionMatches[session.Id] = createRequest.MatchKey;

            _chat.BroadcastSystem(
                $"{session.CreatorName} has started the race on {session.StageName}!"
            );

            foreach (var (index, id) in session.Players)
            {
                if (createResponse.PlayerSecretIds.TryGetValue(index, out var token))
                {
                    var player = _players.Get(id);
                    if (player != null)
                    {
                        _transport.SendPacketToClient(
                            player.ClientIndex,
                            new S2C_RaceStarted
                            {
                                MatchGameplayInfo = matchInfo,
                                State = session.State,
                                SessionId = session.Id,
                                JoinInfo = new S2C_RaceStarted.GameJoinInfo
                                {
                                    RaceServerIpAddress = gameAddress,
                                    JoinToken = token
                                }
                            }
                        );
                    }
                }
            }

            _broadcaster.BroadcastToAll();
        }
        catch (Exception ex)
        {
            // One dead GM must not keep being selected: enough failures take it out of
            // rotation until a refresh and a successful race bring it back.
            if (gm is not null)
                _gmRegistry.MarkFailure(gm);

            // Revert session state on failure
            session.State = SessionState.NotStarted;
            session.StartTime = null;

            foreach (var (_, pid) in session.Players)
            {
                var p = _players.Get(pid);
                if (p is not null) p.IsInGame = false;
            }

            _chat.BroadcastSystem(
                $"Failed to start race on {session.StageName}: {ex.Message}\n{ex.StackTrace}"
            );
        }
    }
}
