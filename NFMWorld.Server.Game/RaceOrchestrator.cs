using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using TightPack;
using NFMWorld.Gameplay;
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

    /// <summary>Reuse the connection pool for live standings and chat relays.</summary>
    private static readonly HttpClient LobbyHttp = new() { Timeout = TimeSpan.FromSeconds(5) };

    // join token → (session, player index, player id, whether this token only watches)
    private readonly ConcurrentDictionary<Guid, (RaceSession Session, byte PlayerIndex, Guid ClientId, bool Spectator)>
        _joinTokens = new();

    private readonly ConcurrentDictionary<uint, RaceSession> _sessions = new();

    // Server gamemode tick loop
    private CancellationTokenSource? _tickCts;
    private Thread? _tickThread;

    // Public archive downloads for race content. Created on first preparation and kept, so the
    // connection pool survives between races.
    private ArchiveContentClient? _contentClient;
    private readonly Lock _contentClientLock = new();

    private ArchiveContentClient ContentClient
    {
        get
        {
            lock (_contentClientLock)
            {
                return _contentClient ??= new ArchiveContentClient(
                    Environment.GetEnvironmentVariable("ARCHIVE_URL") ?? ArchiveEndpoint.Url
                );
            }
        }
    }

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
            if (gm == null)
            {
                while (session.PendingImpacts.TryDequeue(out _))
                {
                }

                while (session.PendingAttach.TryDequeue(out var finishedClient))
                    SendAttach(session, null, finishedClient);
                continue;
            }

            // Before the tick, so a baseline goes out ahead of the first delta this tick produces,
            // and on this thread, which is the only one allowed to read or start the gamemode.
            while (session.PendingAttach.TryDequeue(out var clientIndex))
                SendAttach(session, gm, clientIndex);

            ProcessStageImpacts(session);

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
            if (!session.ResultsBroadcasted && snapshot is { IsFinished: true, IsProvisional: false, Results: { } results })
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

                ReportToLobby(session, results, CaptureLiveState(session, gm));

                // The race is over, so the gamemode and the plugin assembly behind it are
                // done: release both now rather than holding them until shutdown. Sessions
                // are not removed here, so player state keeps relaying as before - the
                // gamemode is simply no longer on the receiving end of it.
                session.ServerGamemode = null;
                session.Factory?.End();
                session.Factory = null;
                continue;
            }

            ReportLiveStateIfDue(session, gm);

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
    private void SendAttach(RaceSession session, IServerGamemode? gm, uint clientIndex)
    {
        if (session.Stage is { } stage)
            foreach (var piece in stage.CaptureDestruction())
                _transport.SendPacketToClient(
                    clientIndex,
                    new S2C_StagePieceState { State = piece, Snapshot = true },
                    reliable: true
                );

        if (session.ResultsBroadcasted)
        {
            _transport.SendPacketToClient(
                clientIndex,
                new S2C_GameFinished { Results = session.LastResults },
                reliable: true
            );
            return;
        }

        if (gm?.CaptureState() is { Length: > 0 } state)
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
    private void ReportToLobby(RaceSession session, RaceResults results, RaceLiveState? finalState = null)
    {
        var lobby = Environment.GetEnvironmentVariable("LOBBY_RESULTS_URL");
        if (string.IsNullOrEmpty(lobby))
        {
            Logging.Info("[GM] No LOBBY_RESULTS_URL configured; the lobby is not told this race ended.");
            return;
        }

        var body = TightPackSerializer.Serialize(
            new RaceServer2Lobby_RaceResults
            {
                MatchKey = session.MatchKey.ToString(),
                Results = results,
                FinalState = finalState
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
                    request.Headers.Add(Protocol.HeaderName, Protocol.Version.ToString());

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
            session.LastChat.TryRemove(clientId, out _);

            // Only a racer was counted as loaded. A spectator was given a throwaway id and
            // never incremented the count, so decrementing for one would undercount the race.
            var isRacer = session.PlayerInfos.Values.Any(p => p.Id == clientId);
            if (isRacer)
            {
                lock (session.ChatLock)
                {
                    if (session.Departures.ContainsKey(clientId)) return;
                    var reason = _transport.GetDisconnectReason(clientIndex);
                    foreach (var info in session.PlayerInfos.Values.Where(
                        p => p.Id == clientId || (p.IsBot && p.OwnerId == clientId)
                    ))
                    {
                        var departure =
                            new S2C_PlayerLeft { PlayerId = info.Id, Reason = reason, Announce = info.Id == clientId };
                        if (!session.Departures.TryAdd(info.Id, departure)) continue;
                        session.PlayerWasted[info.Id] = true;
                        _transport.SendPacketToClients(session.Clients.Keys.ToArray(), departure, reliable: true);
                        if (departure.Announce) SendSystemChat(session, PlayerDeparture.Message(info.PlayerName, reason));
                    }

                    session.LoadedCount = Math.Max(0, session.LoadedCount - 1);
                    session.ServerGamemode?.OnClientDisconnected(clientId);
                }
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
            case C2S_StageImpact impact:
                if (TryFindSession(e.ClientIndex, out var hitSession, out var hitClient)
                    && hitSession.PendingImpacts.Count < 256)
                    hitSession.PendingImpacts.Enqueue((hitClient, impact));
                break;
            case C2S_RaceLoaded raceLoaded:
                HandleRaceLoaded(e.ClientIndex, raceLoaded);
                break;
            case C2S_PlayerState playerState:
                HandlePlayerState(e.ClientIndex, playerState);
                break;
            case C2S_ClientEvent clientEvent:
                HandleClientEvent(e.ClientIndex, clientEvent);
                break;
            case C2S_RaceChat chat:
                HandleRaceChat(e.ClientIndex, chat);
                break;
        }
    }

    // ── Packet handlers ──────────────────────────────────────────────

    private void ProcessStageImpacts(RaceSession session)
    {
        while (session.PendingImpacts.TryDequeue(out var entry))
        {
            if (!session.RaceStarted || session.ResultsBroadcasted || session.Stage is not { } stage) continue;
            var packet = entry.Packet;
            var playerId = packet.PlayerId == Guid.Empty ? entry.Sender : packet.PlayerId;
            if (!session.PlayerInfos.Values.Any(p => p.Id == playerId
                                                     && (p.Id == entry.Sender || p.IsBot && p.OwnerId == entry.Sender)
            )) continue;
            if (session.Departures.ContainsKey(playerId)
                || !session.PlayerPositions.TryGetValue(playerId, out var position)) continue;
            if (stage.GetDestructible(packet.Impact.PieceId) is not { IsDestroyed: false } obj) continue;
            var velocity = packet.Impact.Velocity;
            var impulse = packet.Impact.Impulse;
            static bool Bounded(f64Vector3 v) => Math.Abs((double)v.X) <= 10_000
                                                 && Math.Abs((double)v.Y) <= 10_000 && Math.Abs((double)v.Z) <= 10_000;

            if (!Bounded(velocity) || !Bounded(impulse)) continue;
            if ((position - obj.Position).LengthNoOverflow() > obj.MaxRadius + 1500) continue;
            if (impulse.LengthNoOverflow() > velocity.LengthNoOverflow() * 2 + 20) continue;
            var key = (playerId, packet.Impact.PieceId);
            var now = Environment.TickCount64;
            if (session.LastImpacts.TryGetValue(key, out var last) && now - last < 150) continue;
            session.LastImpacts[key] = now;
            if (stage.DamageObject(packet.Impact) is { } state)
                _transport.SendPacketToClients(
                    session.Clients.Keys.ToArray(),
                    new S2C_StagePieceState { State = state },
                    reliable: true
                );
        }
    }

    private void HandleRaceChat(uint clientIndex, C2S_RaceChat chat)
    {
        if (!TryFindSession(clientIndex, out var session, out var clientId)) return;
        var message = RaceChat.Normalize(chat.Message);
        if (message.Length == 0) return;
        lock (session.ChatLock)
        {
            var player = session.PlayerInfos.Values.FirstOrDefault(p => p.Id == clientId);
            var spectator = session.Spectators.TryGetValue(clientId, out var identity);
            if (spectator ? identity.Muted : player.Id != clientId || player.Banned || player.Muted) return;

            var senderId = spectator ? identity.PlayerId : clientId;
            var now = Environment.TickCount64;
            if (session.LastChat.TryGetValue(senderId, out var last) && now - last < 500) return;
            session.LastChat[senderId] = now;
            var watching = spectator || session.PlayerWasted.TryGetValue(clientId, out var wasted) && wasted;
            var name = spectator ? identity.Name : player.PlayerName;
            var packet = new S2C_RaceChat
            {
                SenderId = senderId,
                Sender = watching ? $"(SPEC) {name}" : name,
                Color = spectator ? identity.Color : player.Color,
                Message = message,
                MessageId = ++session.NextChatId
            };
            session.ChatHistory.Enqueue(packet);
            while (session.ChatHistory.Count > RaceChat.HistoryLimit) session.ChatHistory.Dequeue();
            _transport.SendPacketToClients(session.Clients.Keys.ToArray(), packet, reliable: true);
            ReportChatToLobby(session, packet);
        }
    }

    /// <summary>
    /// One match-lobby line arrives from the lobby. Racers see it, and it joins the race's
    /// history, but it is not reported back - the lobby already typed it.
    /// </summary>
    public void HandleLobbyChat(Lobby2RaceServer_Chat chat)
    {
        var session = _sessions.Values.FirstOrDefault(s => s.MatchKey == chat.MatchKey);
        if (session is null)
            return;

        var packet = new S2C_RaceChat
        {
            SenderId = chat.SenderId,
            Sender = chat.Sender,
            Message = RaceChat.Normalize(chat.Message),
            Color = chat.Color
        };
        if (packet.Message.Length == 0)
            return;

        lock (session.ChatLock)
        {
            packet.MessageId = ++session.NextChatId;
            session.ChatHistory.Enqueue(packet);
            while (session.ChatHistory.Count > RaceChat.HistoryLimit) session.ChatHistory.Dequeue();
            _transport.SendPacketToClients(session.Clients.Keys.ToArray(), packet, reliable: true);
        }
    }

    /// <summary>How often a running race's order is pushed to the match lobby.</summary>
    private const long LiveReportIntervalMs = 5000;

    /// <summary>
    /// Pushes the latest race stats to the match lobby every five seconds.
    /// </summary>
    private static void ReportLiveStateIfDue(RaceSession session, IServerGamemode gamemode)
    {
        if (!session.RaceStarted || session.ResultsBroadcasted)
            return;

        var now = Environment.TickCount64;
        if (now - session.LastLiveReport < LiveReportIntervalMs)
            return;

        session.LastLiveReport = now;
        PostToLobby(
            "/race-live",
            "LOBBY_LIVE_URL",
            TightPackSerializer.Serialize(
                new RaceServer2Lobby_LiveState
                {
                    MatchKey = session.MatchKey.ToString(),
                    State = CaptureLiveState(session, gamemode)
                }
            )
        );
    }

    private static RaceLiveState CaptureLiveState(RaceSession session, IServerGamemode gamemode)
    {
        var standings = (gamemode as PluginServerGamemode)?.GetLiveState()?.Standings;
        lock (session.ChatLock)
        {
            standings ??= StandingsFromFrames(session);
            return new RaceLiveState
            {
                Standings = [.. standings.Select(standing =>
                    {
                        var hasFrame = session.CarReadings.TryGetValue(standing.PlayerId, out var reading);
                        var maxDamage = session.CarDamageLimits.GetValueOrDefault(standing.CarName);
                        return standing with
                    {
                        TotalLaps = session.Stage?.Nlaps ?? 0,
                        Wasted = hasFrame ? session.PlayerWasted.GetValueOrDefault(standing.PlayerId)
                            : standing.Wasted && !standing.Forfeited,
                        PowerPercent = hasFrame ? reading.Power : null,
                        DamagePercent = hasFrame && maxDamage > 0
                            ? (byte)Math.Round(Math.Clamp(reading.Damage * 100d / maxDamage, 0, 100))
                            : null
                    };
                    }
                )]
            };
        }
    }

    private static RaceLiveStanding[] StandingsFromFrames(RaceSession session)
        => [.. session.PlayerInfos.OrderBy(slot => slot.Key).Select(slot =>
            {
                var player = slot.Value;
                session.CarReadings.TryGetValue(player.Id, out var reading);
                return new RaceLiveStanding
            {
                PlayerId = player.Id,
                PlayerName = player.PlayerName,
                CarName = player.CarName,
                Color = player.Color,
                IsBot = player.IsBot,
                Position = 0,
                Lap = reading.Lap,
                Checkpoint = reading.Checkpoint,
                Wasted = session.PlayerWasted.GetValueOrDefault(player.Id),
                Forfeited = session.Departures.ContainsKey(player.Id)
            };
            }
        )
            .OrderBy(standing => standing.Wasted || standing.Forfeited)
            .ThenByDescending(standing => standing.Lap)
            .ThenByDescending(standing => standing.Checkpoint)
            .ThenBy(standing => DistanceToCheckpoint(session, standing))
            .Select((standing, index) => standing with { Position = index })];

    private static double DistanceToCheckpoint(RaceSession session, RaceLiveStanding standing)
    {
        if (session.Stage is not { } stage || standing.Checkpoint >= stage.Checkpoints.Count
            || !session.PlayerPositions.TryGetValue(standing.PlayerId, out var position))
            return double.PositiveInfinity;

        var target = stage.Checkpoints[standing.Checkpoint].Position;
        var dx = (double)position.X - (double)target.X;
        var dz = (double)position.Z - (double)target.Z;
        return dx * dx + dz * dz;
    }

    /// <summary>Relays a race chat line to the lobby's game transcript.</summary>
    private static void ReportChatToLobby(RaceSession session, S2C_RaceChat chat)
        => PostToLobby(
            "/race-chat",
            "LOBBY_CHAT_URL",
            TightPackSerializer.Serialize(
                new RaceServer2Lobby_Chat
                {
                    MatchKey = session.MatchKey.ToString(),
                    SenderId = chat.SenderId,
                    Sender = chat.Sender,
                    Message = chat.Message,
                    Color = chat.Color
                }
            )
        );

    /// <summary>
    /// Posts one signed, versioned body to the lobby. The endpoint comes from the matching
    /// override variable, or the results URL's origin plus <paramref name="path"/>.
    /// </summary>
    private static void PostToLobby(string path, string overrideVariable, byte[] body)
    {
        var endpoint = Environment.GetEnvironmentVariable(overrideVariable);
        if (string.IsNullOrEmpty(endpoint))
        {
            var results = Environment.GetEnvironmentVariable("LOBBY_RESULTS_URL");
            if (string.IsNullOrEmpty(results) || !Uri.TryCreate(results, UriKind.Absolute, out var resultsUri))
                return;
            endpoint = new Uri(resultsUri.GetLeftPart(UriPartial.Authority) + path).ToString();
        }

        var keyId = Environment.GetEnvironmentVariable("HMAC_KEY_ID") ?? "primary";
        var secretText = Environment.GetEnvironmentVariable("HMAC_SECRET_KEY");
        if (string.IsNullOrEmpty(secretText))
        {
            Logging.Warning("[GM] HMAC_SECRET_KEY is not configured; the lobby relay is off.");
            return;
        }

        byte[] secret;
        try
        {
            secret = Convert.FromBase64String(secretText);
        }
        catch (FormatException)
        {
            Logging.Warning("[GM] HMAC_SECRET_KEY is not valid base64; the lobby relay is off.");
            return;
        }

        var auth = HmacAuth.SignWithNonce(
            "POST",
            path,
            body,
            keyId,
            secret,
            Guid.NewGuid().ToString("N")
        );

        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = new ByteArrayContent(body)
                    };
                    request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(auth);
                    request.Headers.Add(Protocol.HeaderName, Protocol.Version.ToString());

                    using var response = await LobbyHttp.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                        Logging.Warning($"[GM] Lobby rejected {path}: {(int)response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Logging.Warning($"[GM] Could not post {path} to the lobby: {ex.Message}");
                }
            }
        );
    }

    private void HandleClientEvent(uint clientIndex, C2S_ClientEvent clientEvent)
    {
        try
        {
            if (!TryFindSession(clientIndex, out var session, out var clientId)) return;
            if (session.Departures.ContainsKey(clientId)) return;
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
        if (entry.ClientId == Guid.Empty) return;
        lock (session.ChatLock)
        {
            foreach (var message in session.ChatHistory)
                _transport.SendPacketToClient(clientIndex, message, reliable: true);
            foreach (var departure in session.Departures.Values)
            {
                var history = departure;
                history.Announce = false;
                _transport.SendPacketToClient(clientIndex, history, reliable: true);
            }

            AttachLoadedClient(session, clientIndex, entry.ClientId, entry.Spectator);
        }
    }

    private void AttachLoadedClient(RaceSession session, uint clientIndex, Guid clientId, bool spectator)
    {
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
        if (!spectator && !session.Departures.ContainsKey(clientId)) session.LoadedCount++;

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
        if (!session.PlayerInfos.Values.Any(p => p.Id == reportedId)) return;
        lock (session.ChatLock)
        {
            if (session.Departures.ContainsKey(reportedId)) return;
            if (!session.WastedTracker.TryUpdate(
                reportedId,
                playerState.State.Ticks,
                frame.TheBitFlags.Wasted,
                    out var newlyWasted
            )) return;
            session.PlayerPositions[reportedId] = new f64Vector3(pos.X, pos.Y, pos.Z);
            session.PlayerWasted[reportedId] = frame.TheBitFlags.Wasted;
            session.CarReadings[reportedId] = (frame.RacePosition.Lap, frame.RacePosition.CheckpointInlap,
                (byte)Math.Round(Math.Clamp((float)frame.Power, 0, 100)), frame.Damage);
            if (newlyWasted && RaceWastedTracker.Message(session.PlayerInfos, reportedId, playerState.State
                .DamageSource
            ) is { } message)
                SendSystemChat(session, message);
        }

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

    private void SendSystemChat(RaceSession session, string message)
    {
        var announcement = new S2C_RaceChat
        {
            SenderId = Guid.Empty, Sender = "GAME", Message = message,
            Color = new Color3(255, 230, 80), MessageId = ++session.NextChatId
        };
        session.ChatHistory.Enqueue(announcement);
        while (session.ChatHistory.Count > RaceChat.HistoryLimit) session.ChatHistory.Dequeue();
        _transport.SendPacketToClients(session.Clients.Keys.ToArray(), announcement, reliable: true);
        ReportChatToLobby(session, announcement);
    }

    /// <summary>Rejects state that could freeze a receiving client's physics loops.</summary>
    private static bool IsSanePlayerState(PlayerState state)
    {
        const double maxAngle = 1_000_000;
        const double maxSpeed = 10_000;

        var rotation = state.CarFrame.CarRotation;
        var speed = (double)state.CarFrame.Speed;
        var stunt = state.CompletedStunt;

        return double.IsFinite((double)rotation.Xz)
               && Math.Abs((double)stunt.TotalStuntXz) <= maxAngle
               && Math.Abs((double)stunt.TotalStuntXy) <= maxAngle
               && Math.Abs((double)stunt.TotalStuntZy) <= maxAngle
               && stunt.Powerup >= 0 && (double)stunt.Powerup <= maxAngle
               && state.DamageSource >= 0 && state.DamageSource <= MatchGameplayInfo.MaxPlayers
               && Math.Abs((double)rotation.Xz) <= maxAngle
               && double.IsFinite((double)rotation.Pzy)
               && Math.Abs((double)rotation.Pzy) <= maxAngle
               && double.IsFinite((double)rotation.Pxy)
               && Math.Abs((double)rotation.Pxy) <= maxAngle
               && double.IsFinite(speed)
               && Math.Abs(speed) <= maxSpeed;
    }

    // ── HTTP handler ─────────────────────────────────────────────────

    /// <summary>
    /// Downloads the race's canonical content onto this server, ahead of
    /// <see cref="CreateRace"/>. Every asset must verify against its hash and kind; a failure
    /// fails the request so the lobby can call the race off.
    /// </summary>
    public async Task PrepareContentAsync(Lobby2RaceServer_PrepareContent request, CancellationToken ct = default)
    {
        ContentCache.Mount();

        foreach (var item in request.Content)
        {
            var result = await ContentCache.PrepareAsync(
                ContentClient,
                item.Id,
                (AssetKind)item.Kind,
                ct
            );

            if (!result.Ok)
                throw new InvalidOperationException($"Could not prepare '{item.Id}': {result.Error}");
        }

        Logging.Info($"[GM] Prepared {request.Content.Length} asset(s) for {request.MatchKey}");
    }

    public Guid RegisterSpectator(Lobby2RaceServer_RegisterSpectator request)
    {
        if (!_joinTokens.TryGetValue(request.SessionToken, out var entry)
            || !entry.Spectator || entry.ClientId != Guid.Empty || entry.Session.ResultsBroadcasted
            || request.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("The race is no longer available to watch.");
        var session = entry.Session;
        lock (session.ChatLock)
        {
            var identity = (request.PlayerId, request.Name, request.Color, request.Muted);
            if (session.SpectatorTokens.TryGetValue(request.PlayerId, out var existing)
                && _joinTokens.TryGetValue(existing, out var registered))
            {
                session.Spectators[registered.ClientId] = identity;
                return existing;
            }

            var token = Guid.NewGuid();
            var spectatorId = Guid.NewGuid();
            session.SpectatorTokens[request.PlayerId] = token;
            session.Spectators.Add(spectatorId, identity);
            _joinTokens.TryAdd(token, (session, 0, spectatorId, true));
            return token;
        }
    }

    private static int ReadMaxDamage(string carName)
    {
        try
        {
            var model = BackendGameSparker.FindLoadedCar(carName)
                        ?? RadpackReader.TryReadModel(carName, RadpackType.Car, out _);
            return model?.Stats.Maxmag ?? 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            return 0;
        }
    }

    public Lobby2RaceServer_CreateRaceResponse CreateRace(Lobby2RaceServer_CreateRace raceParams)
    {
        // The lobby is trusted infrastructure, but its roster is still assembled from client
        // claims. Reject before any stage object is constructed, so nothing can be loaded from a
        // path a player chose.
        if (MultiplayerContentPolicy.ValidateMatch(raceParams.MatchGameplayInfo) is { } refusal)
            throw new InvalidOperationException(refusal);

        EnsureContentPrepared(raceParams.MatchGameplayInfo);

        var damageLimits = raceParams.MatchGameplayInfo.Players.Values.Select(player => player.CarName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => name, ReadMaxDamage, StringComparer.OrdinalIgnoreCase);

        var sessionId = Interlocked.Increment(ref _sessionIdCounter);
        var joinTokens = new Dictionary<byte, Guid>();

        var session = new RaceSession
        {
            Id = sessionId,
            MatchKey = raceParams.MatchKey,
            // Only a player with a connection loads. A bot never sends C2S_RaceLoaded, so
            // counting it here would leave the race waiting for a player that cannot arrive.
            PlayerCount = raceParams.MatchGameplayInfo.Players.Values.Count(p => !p.IsBot && !p.Banned),
            Clients = [],
            PlayerInfos = raceParams.MatchGameplayInfo.Players,
            CarDamageLimits = damageLimits,
            Gamemode = raceParams.MatchGameplayInfo.Gamemode,
            // From creation, not from the first load: a race whose players never reach this
            // server at all would otherwise wait on them forever.
            LoadDeadline = DateTimeOffset.UtcNow.AddSeconds(LoadTimeoutSeconds)
        };

        foreach (var (playerIndex, playerInfo) in raceParams.MatchGameplayInfo.Players)
        {
            // A bot has no client to hand a token to; its state arrives from the client that
            // simulates it, under the bot's own player id.
            if (playerInfo.IsBot || playerInfo.Banned)
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

        session.Stage = context.CurrentStage;

        // TODO this is a bit janky and could probably be improved
        var gm = factory.CreateServerGamemode(
            new ServerGamemodeParameters()
            {
                Players = raceParams.MatchGameplayInfo.InSlotOrder()
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

        // The lobby exchanges this registration token for an individual spectator's join token.
        var spectatorToken = Guid.NewGuid();
        _joinTokens.TryAdd(spectatorToken, (session, 0, Guid.Empty, true));

        return new Lobby2RaceServer_CreateRaceResponse
        {
            PlayerSecretIds = joinTokens,
            SpectatorToken = spectatorToken
        };
    }

    /// <summary>
    /// The stage this race will read must already be in the prepared cache or be genuine shipped
    /// content: constructing a <see cref="BackendStage"/> from an unprepared archive id would do
    /// synchronous HTTP on the server's request thread. Cars are not checked here - the game
    /// master relays car state and never constructs one, so downloading them would be dead weight.
    /// </summary>
    private static void EnsureContentPrepared(MatchGameplayInfo info)
    {
        if (RadpackReader.IsArchiveId(info.StageName) && !ContentCache.IsPrepared(info.StageName))
            throw new InvalidOperationException($"Stage '{info.StageName}' has not been prepared.");

        if (!RadpackReader.IsArchiveId(info.StageName)
            && !VFS.FileExists($"data/stages/{info.StageName}.txt")
            && !VFS.FileExists($"data/stages/{info.StageName}.radpack"))
            throw new InvalidOperationException($"Stage '{info.StageName}' is not installed on this server.");
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
        _contentClient?.Dispose();
        _contentClient = null;
    }

    private class ClientInfo
    {
        public ClientState State { get; set; }
    }

    private class RaceSession
    {
        public BackendStage? Stage { get; set; }
        public ConcurrentQueue<(Guid Sender, C2S_StageImpact Packet)> PendingImpacts { get; } = new();
        public Dictionary<(Guid Player, int Piece), long> LastImpacts { get; } = [];

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
        public ConcurrentDictionary<Guid, long> LastChat { get; } = [];
        public Lock ChatLock { get; } = new();
        public Queue<S2C_RaceChat> ChatHistory { get; } = new();
        public RaceWastedTracker WastedTracker { get; } = new();
        public ConcurrentDictionary<Guid, S2C_PlayerLeft> Departures { get; } = new();
        public long NextChatId;
        public Dictionary<Guid, (Guid PlayerId, string Name, Color3 Color, bool Muted)> Spectators { get; } = [];
        public Dictionary<Guid, Guid> SpectatorTokens { get; } = [];

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
        public Dictionary<string, int> CarDamageLimits { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<Guid, (byte Lap, ushort Checkpoint, byte Power, int Damage)> CarReadings { get; } = [];

        /// <summary>Set once every player has loaded and the gamemode has been started.</summary>
        public bool RaceStarted { get; set; }

        /// <summary>Tick timestamp of the last live order pushed to the lobby.</summary>
        public long LastLiveReport { get; set; }

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
