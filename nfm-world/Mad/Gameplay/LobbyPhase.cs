using NFMWorld.Gameplay.RaceHost;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Plugins;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Gameplay;

/// <summary>
/// The multiplayer lobby. All state lives here and is published into <see cref="UiState.Lobby"/>;
/// <see cref="LobbyView"/> only reads it.
/// </summary>
public partial class LobbyPhase(IMultiplayerClientTransport transport) : BasePhase
{
    private readonly Player _player = new();
    private List<ServerSidePlayerInfo> _players = [];
    private List<S2C_LobbyState.GameSession> _activeSessions = [];
    private readonly List<UiState.LobbyChatLine> _chatMessages = [];
    private long _nextChatLineId;
    private bool _sentAuthentication;
    private bool _authenticated;
    private bool _subscribed;
    private int _reconnectPending;
    private string? _lastStatus;

    /// <summary>
    /// When the outstanding handshake was written, so a lobby that never answers is retried
    /// rather than leaving the player on "Verifying account..." for the rest of the session.
    /// </summary>
    private long _authenticationSentAt;

    private bool _authenticationFailed;

    /// <summary>This phase has launched a race, so a late start notice is stale.</summary>
    private bool _racedThisSession;

    /// <summary>The download in flight for a race's content, cancelled when it is superseded.</summary>
    private CancellationTokenSource? _preparationCancellation;

    /// <summary>
    /// A spectator join waiting on its downloads. Racers already prepared content as a group, so
    /// this is only ever the late onlooker's own copy; it must not hold anything else up.
    /// </summary>
    private (S2C_RaceStarted Started, Task<bool> Work)? _pendingSpectate;

    private static UiState.LobbyState Lobby => UiState.Lobby;
    private IMultiplayerClientTransport Transport => transport;

    /// <summary>Local stages the create dialog offers, read once so switching source is cheap.</summary>
    private IReadOnlyList<StageOption>? _localStages;

    /// <summary>Superseded archive searches and downloads are cancelled through this.</summary>
    private CancellationTokenSource? _stageSearchCancellation;
    private int _stageSearchGeneration;

    /// <summary>Opens the game the host just created once the server lists it.</summary>
    private bool _awaitingCreatedSession;
    private long _createdSessionDeadline;

    /// <summary>Config schemas read for match details, keyed by gamemode id.</summary>
    private readonly Dictionary<string, IReadOnlyList<GamemodeConfigField>> _configFields = [];

    /// <summary>The last live order per session, replaced by each forwarded snapshot.</summary>
    private readonly Dictionary<uint, RaceLiveStanding[]> _liveStates = [];
    private long _lastClockUpdate;

    private IReadOnlyList<StageOption> LocalStages => _localStages ??= LoadLocalStages();

    private static IReadOnlyList<StageOption> LoadLocalStages()
    {
        try
        {
            return LobbyStageSelection.LocalStages(
                GameSparker.GetAvailableStages(),
                GameSparker.GetAvailableStageNames()
            );
        }
        catch (Exception)
        {
            // A stage tree that failed to load must not take the lobby down with it.

            return [];
        }
    }

    private LobbyView.Actions Actions => new(
        Join: OnJoin,
        Leave: OnLeave,
        StartRace: OnStartRace,
        Create: OnCreate,
        Refresh: OnRefresh,
        SendChat: OnSendChat,
        Back: OnBack,
        OpenCreate: OnOpenCreate,
        CloseCreate: OnCloseCreate,
        OpenSession: OnOpenSession,
        CloseSession: OnCloseSession,
        Spectate: OnSpectate,
        SetStage: OnSelectStage,
        SetStageSearch: OnStageSearchChanged,
        SetStageSource: OnStageSourceSelected,
        SearchStagesOnline: query => _ = SearchStagesOnlineAsync(query),
        SetMaxPlayers: count =>
        {
            Lobby.MaxPlayers.Value = Math.Clamp(count, 2, MatchGameplayInfo.MaxPlayers);
            Lobby.BotCount.Value = Math.Clamp(Lobby.BotCount.Value, 0, Lobby.MaxPlayers.Value - 1);
        },
        SetBots: count => Lobby.BotCount.Value = Math.Clamp(count, 0, Lobby.MaxPlayers.Value - 1),
        SetConfig: OnSetConfig,
        SetGameRule: OnSetGameRule,
        KickGamePlayer: OnKickGamePlayer,
        InviteGamePlayer: OnInviteGamePlayer,
        SetGamemode: OnSetGamemode,
        ChangeCar: OpenCarPicker,
        CopyCar: id => CopyPlayerCar(id, colorsOnly: false),
        CopyColors: id => CopyPlayerCar(id, colorsOnly: true),
        PreviewStage: OpenStagePreview
    );

    public override Node BuildUi() => BuildLobbyUi();

    public override void Enter()
    {
        _carCancellation.Cancel();
        _carCancellation.Dispose();
        _carCancellation = new CancellationTokenSource();
        _carsActive = true;
        LobbyView.Reset();
        Lobby.Reset();
        // Reset emptied the status signal, so the cached text would make UpdateStatus a no-op
        // and leave the line blank on re-entry.
        _lastStatus = null;
        // Enter runs on every return to the lobby, so a race that already happened is stale
        // from here on: the next session's start failure must still be reported.
        _racedThisSession = false;
        _awaitingCreatedSession = false;
        // Returning from a race re-enters this phase, and the server only resends lobby state
        // on the next change; the transcript this client already received stays put.
        Lobby.Chat.Value = _chatMessages.ToArray();

        if (!_subscribed)
        {
            GameSparker.AccountManager.ActiveAccountChanged += OnAccountChanged;
            transport.Reconnected += OnReconnected;
            _subscribed = true;
        }

        ApplyPendingReconnect();
        UpdateStatus();
        if (_authenticated && transport.State == ClientState.Connected)
            SendUpdatePlayerIdentity();
        base.Enter();
    }

    private void OnReconnected() => Interlocked.Exchange(ref _reconnectPending, 1);

    private void ApplyPendingReconnect()
    {
        if (Interlocked.Exchange(ref _reconnectPending, 0) == 0)

            return;

        // A reconnected transport is a brand-new connection: the lobby knows nothing about
        // this player until it authenticates and identifies itself again.
        ResetHandshake();
        GameSparker.UiRenderer?.Notifications.Clear(this);
        _deferredLobbyPackets.Clear();
        _player.Id = Guid.Empty;
        _players = [];
        _activeSessions = [];
        // Session ids restart with the lobby process. Keeping old match lines could show them
        // in a new game that happens to reuse an id; room chat is process-independent enough.
        _chatMessages.RemoveAll(line => line.SessionId != 0);
        _liveStates.Clear();
        Lobby.Chat.Value = _chatMessages.ToArray();
        Publish();
    }

    /// <summary>How long to wait for an answer before asking again.</summary>
    private const long AuthenticationTimeoutMs = 5000;

    /// <summary>
    /// How many times to ask before telling the player instead of waiting forever. A reply already
    /// on the wire when the connection drops is gone, and a lobby behind a stale proxy can drop one
    /// without a trace; neither is visible to this side.
    /// </summary>
    private const int MaxAuthenticationAttempts = 3;

    private int _authenticationAttempts;

    /// <summary>Set once the refusal has been shown, so a frame that keeps seeing it does not stack message boxes.</summary>
    private bool _reportedVersionRefusal;

    private void ResetHandshake()
    {
        _sentAuthentication = false;
        _authenticated = false;
        _authenticationFailed = false;
        _authenticationAttempts = 0;
        _reportedVersionRefusal = false;
    }

    /// <summary>
    /// Drives the account handshake. It is not fire-and-forget: the phase waits on an answer, and
    /// re-asks once that answer is overdue, because the status line is otherwise the last thing
    /// the player ever sees.
    /// </summary>
    private void UpdateHandshake()
    {
        if (transport.State != ClientState.Connected)
        {
            // A drop redoes the handshake when the transport reconnects.
            _sentAuthentication = false;
            _authenticationAttempts = 0;
            _authenticationFailed = false;

            return;
        }

        // Nothing is sent before the lobby confirms it reads this build's packets: an older
        // server would parse an account packet as fields that mean something else.

        if (!transport.HandshakeAccepted)

            return;

        if (_authenticated)

            return;

        if (_sentAuthentication && Elapsed(_authenticationSentAt) < AuthenticationTimeoutMs)

            return;

        if (_authenticationAttempts >= MaxAuthenticationAttempts)
        {
            if (!_authenticationFailed)
            {
                _authenticationFailed = true;
                Logging.Warning("The lobby did not answer the account handshake.");
            }

            return;
        }

        // Identity is sent only after the server verifies this account.
        SendAuthenticate(GameSparker.AccountManager.ActiveAccount?.Token ?? "");
    }

    private void SendAuthenticate(string token)
    {
        _sentAuthentication = true;
        _authenticationSentAt = System.Environment.TickCount64;
        _authenticationAttempts++;
        transport.SendPacketToServer(new C2S_Authenticate { Token = token });
    }

    private static long Elapsed(long since) => System.Environment.TickCount64 - since;

    private void OnAccountChanged(NFMWorld.Accounts.Account? account)
    {
        // Empty token = signing out, which the lobby models as a guest.

        if (transport.State != ClientState.Connected || !transport.HandshakeAccepted)

            return;
        // A different account is a fresh handshake, so it gets its own retry budget.
        _authenticationAttempts = 0;
        _authenticationFailed = false;
        SendAuthenticate(account?.Token ?? "");
        UpdateStatus();
    }

    /// <summary>The REFRESH button, which retries an authentication the lobby never answered.</summary>
    private void OnRefresh()
    {
        if (transport.State == ClientState.Connected
            && !_authenticated
            && transport.HandshakeAccepted)
        {
            _authenticationAttempts = 0;
            _authenticationFailed = false;
            SendAuthenticate(GameSparker.AccountManager.ActiveAccount?.Token ?? "");
        }

        UpdateStatus();
    }

    public override void GameTick()
    {
        base.GameTick();
        ApplyPendingReconnect();

        // A refused version is final for this connection: the server will keep refusing it, so
        // say why and leave rather than sitting on a handshake that can never finish.

        if (!_reportedVersionRefusal && transport.HandshakeError is { Length: > 0 } versionRefusal)
        {
            _reportedVersionRefusal = true;
            Logging.Warning($"Lobby refused this build: {versionRefusal}");
            var message = transport.ServerProtocolVersion switch
            {
                > Protocol.Version => "The master server is using a newer version of the game. Update the game and try again.",
                < Protocol.Version => "The master server is using an older version of the game. Please wait til the server is updated.",
                _ => versionRefusal
            };
            GameSparker.PopPhase();
            GameSparker.MessageWindow.ShowMessage("Update required", message);

            return;
        }

        UpdateHandshake();

        // The transport can drop at any point (server restart, network loss); the status line
        // has to follow it or it stays on "Connecting..." forever after a failed connect.
        UpdateStatus();

        // Phases are pushed on the game tick, so a spectator's completed downloads are consumed
        // here rather than from the download's continuation.

        if (_pendingSpectate is { Work.IsCompleted: true } pending)
        {
            _pendingSpectate = null;
            if (pending.Work.Result)
                StartRaceCore(pending.Started);
            else
                SetStatus("The race content could not be downloaded.");
        }

        foreach (var packet in ReadLobbyPackets())
        {
            if (HandleLobbyNotification(packet))

                continue;

            switch (packet)
            {
                case S2C_LobbyChatMessage chatMessage:
                    AddChatLine(ChatLine(chatMessage));

                    break;

                case S2C_Authenticated authenticated:

                    if (!authenticated.Accepted || authenticated.AccountId <= 0)
                    {
                        _authenticated = false;
                        Logging.Info($"Lobby refused the connection: {authenticated.Error}");
                        if (authenticated.SessionRejected)
                            GameSparker.AccountManager.LogOut();
                        transport.StopReconnecting();
                        GameSparker.PopPhase();
                        GameSparker.MessageWindow.ShowMessage(
                            "Session refused",
                            authenticated.Error is { Length: > 0 }
                                ? authenticated.Error
                                : "Sign in to join multiplayer."
                        );

                        return;
                    }
                    else
                    {
                        _authenticated = true;
                        _authenticationFailed = false;
                        _authenticationAttempts = 0;
                        SendUpdatePlayerIdentity();
                        UpdateStatus();
                    }

                    break;

                case S2C_LobbyState lobbyState:
                    _player.Id = lobbyState.ClientId;
                    _players = lobbyState.Players.ToList();
                    _activeSessions = lobbyState.ActiveSessions.ToList();
                    Publish();

                    break;

                case S2C_SessionLiveState live:
                    _liveStates[live.SessionId] = [.. live.Standings];
                    if (Lobby.Page.Value == UiState.LobbyPage.Match
                        && Lobby.ViewedSessionId.Value == live.SessionId)
                        ApplyLiveState(live.SessionId);

                    break;

                case S2C_RaceStarted raceStarted:
                    StartRace(raceStarted);

                    break;

                case S2C_PrepareContent prepareContent:
                    PrepareContent(prepareContent);

                    break;

                case S2C_ModerationNotice notice:

                    if (notice.Fatal)
                    {
                        transport.StopReconnecting();
                        GameSparker.PopPhase();
                    }

                    GameSparker.MessageWindow.ShowMessage("Moderation", notice.Message);

                    break;

                case S2C_ModerationResult result:
                    SetStatus(result.Message);

                    break;

                case S2C_ChatPurge purge:
                    _chatMessages.RemoveAll(l => l.SenderId == purge.SenderId);
                    Lobby.Chat.Value = _chatMessages.ToArray();

                    break;
            }
        }

        // A create that never reaches the server gets no state broadcast to expire it.

        if (_awaitingCreatedSession && System.Environment.TickCount64 > _createdSessionDeadline)
        {
            _awaitingCreatedSession = false;
            SetStatus("The game was not created. Check the connection and try again.");
        }

        // A finished game's close countdown is client-side: the lobby only broadcasts state
        // when something changes, so a wall clock has to drive the visible timer.

        if (Lobby.Sessions.Value.Any(session => session.State == SessionState.Finished))
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (now - _lastClockUpdate >= 250)
            {
                _lastClockUpdate = now;
                Lobby.NowUnixMs.Value = now;
            }
        }

        _stagePreview.Value?.Tick();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _carsActive = false;
            CloseCarPicker();
            CloseStagePreview();
            GameSparker.UiRenderer?.Notifications.Clear(this);
            _carCancellation.Cancel();
            _carCancellation.Dispose();
            _carDetails.Clear();
            _sessionStages.Clear();
            // A preparation outliving the phase would keep downloading for a race this client
            // has left, and its acknowledgement would answer a request nobody is waiting on.
            _preparationCancellation?.Cancel();
            _preparationCancellation?.Dispose();
            _preparationCancellation = null;
            _pendingSpectate = null;
            // A create-dialog search outliving the phase would write into state nobody renders
            // and keep the HTTP request alive after the player has left the lobby.
            CancelStageSearch();
            // Leaving the lobby must close the socket: the server only drops the ghost player when
            // the connection goes, and the receive loop would otherwise run for the process lifetime.
            GameSparker.AccountManager.ActiveAccountChanged -= OnAccountChanged;
            transport.Reconnected -= OnReconnected;
            transport.Stop();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Downloads and verifies the race's canonical content, then tells the lobby. The race is
    /// only created once every member has answered, so nothing here can be skipped: a client
    /// that reaches the load handshake without these bytes cannot build the stage or the cars.
    /// </summary>
    private async void PrepareContent(S2C_PrepareContent request)
    {
        _preparationCancellation?.Cancel();
        _preparationCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _preparationCancellation = cancellation;

        // A new attempt at starting a race, so a failure notice is current again.
        _racedThisSession = false;
        SetStatus("Preparing race content...");

        foreach (var item in request.Content)
        {
            var result = await ContentCache.PrepareAsync(
                GarageController.ContentClient,
                item.Id,
                (AssetKind)item.Kind,
                cancellation.Token
            );

            if (cancellation.IsCancellationRequested)

                return;

            if (result.Ok)

                continue;

            transport.SendPacketToServer(
                new C2S_ContentReady
                {
                    SessionId = request.SessionId,
                    PreparationId = request.PreparationId,
                    Ready = false,
                    Reason = result.Error
                }
            );
            SetStatus($"Could not prepare the race: {result.Error}");

            return;
        }

        transport.SendPacketToServer(
            new C2S_ContentReady
            {
                SessionId = request.SessionId,
                PreparationId = request.PreparationId,
                Ready = true
            }
        );
        SetStatus("Content ready. Waiting for the race...");
    }

    private void StartRace(S2C_RaceStarted raceStarted)
    {
        // A late spectator was not part of the race's preparation, so its own missing assets are
        // fetched first. Racers always arrive here already prepared by the lobby handshake.
        var missing = MultiplayerContentPolicy.RequiredArchiveContent(raceStarted.MatchGameplayInfo)
            .Where(item => !ContentCache.IsPrepared(item.Id))
            .ToArray();

        if (missing.Length > 0)
        {
            _pendingSpectate = (raceStarted, PrepareSpectateContentAsync(missing));
            SetStatus("Preparing race content...");

            return;
        }

        StartRaceCore(raceStarted);
    }

    private async Task<bool> PrepareSpectateContentAsync(PreparedContent[] required)
    {
        // Never faults: the tick reads Result once the task completes, and an exception there
        // would escape the game loop.

        try
        {
            foreach (var item in required)
            {
                var result = await ContentCache.PrepareAsync(
                    GarageController.ContentClient,
                    item.Id,
                    (AssetKind)item.Kind,
                    CancellationToken.None
                );

                if (!result.Ok)
                {
                    SetStatus($"Could not prepare the race: {result.Error}");

                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Could not prepare the race: {ex.Message}");

            return false;
        }
    }

    private void StartRaceCore(S2C_RaceStarted raceStarted)
    {
        CloseCarPicker();
        _racedThisSession = true;

        // A NEW transport to the Game Master for in-game traffic. The lobby transport stays
        // alive, so exiting the race returns to this lobby.
        var gameAddr = raceStarted.JoinInfo.RaceServerIpAddress;
        var ipString = ((System.Net.IPAddress)gameAddr.Address).ToString();
        var gameTransport = new ENetMultiplayerClientTransport(ipString, gameAddr.Port);

        var session = raceStarted.MatchGameplayInfo;
        // Slot order, not dictionary enumeration order: the gamemode indexes players by array
        // position and relayed state arrives in an order that has to agree with it.
        var players = session.InSlotOrder()
            .Select(c => new ClientSidePlayerInfo
                {
                    CarName = c.CarName,
                    Color = c.Color,
                    CarColors = c.CarColors,
                    Wheels = c.Wheels,
                    PlayerName = c.PlayerName,
                    IsBot = c.IsBot,
                    // A spectator is in none of the slots: nobody is the client player, so no car
                    // is driven here and the camera rides a watched one instead.
                    IsClientPlayer = !raceStarted.Spectator && c.Id == _player.Id,
                    Id = c.Id,
                    OwnerId = c.OwnerId
                }
            )
            .ToArray();

        var phase = new RacePhase(
            session.StageName,
            GetGameModeFactory(session),
            players,
            new NetworkRaceHost(gameTransport, session, raceStarted.JoinInfo.JoinToken),
            spectating: raceStarted.Spectator
        );

        // Back from the race: the lobby still has us in that session and flagged in-game, on
        // the same socket - no disconnect ever reached it. Leaving here is what frees the
        // player and lets the emptied race drop off the list.
        phase.Exited += (_, _) =>
        {
            if (!raceStarted.Spectator && transport.State == ClientState.Connected)
                transport.SendPacketToServer(new C2S_LeaveSession { SessionId = raceStarted.SessionId });
            GameSparker.PopGroup(PhaseManager.Groups.Event);
        };
        GameSparker.PushPhase(phase, PhaseManager.Groups.Event);
    }

    // ── State publication ──────────────────────────────────────────

    private void Publish()
    {
        Lobby.Players.Value = _players
            .Select(p => new UiState.LobbyPlayer(
                p.Id,
                p.PlayerName,
                CarLabel(p.CarName),
                ToColor(p.Color),
                p.Id == _player.Id,
                PlayerColors(p),
                p.Role
            )
            )
            .ToArray();

        ResolvePlayerCars();
        ResolveSessionStageNames();

        var humans = _players.ToDictionary(p => p.Id);
        var sessions = _activeSessions
            .Select(s => ToUiSession(
                s,
                humans,
                IsMember(s),
                s.CreatorId == _player.Id
            )
            )
            .ToArray();
        Lobby.Sessions.Value = sessions;

        // GameSession is a struct, so FirstOrDefault yields a default that looks like a real
        // session; the predicate has to be the only source of truth.
        UiState.LobbySession? current = null;

        foreach (var s in _activeSessions)
        {
            if (!IsMember(s))

                continue;
            current = sessions.First(session => session.Id == s.Id);

            break;
        }

        Lobby.CurrentSession.Value = current;

        // The host's new game is opened as soon as the server lists it, so CREATE lands in the
        // waiting room rather than dropping back to the room list.

        if (_awaitingCreatedSession)
        {
            var created = sessions.FirstOrDefault(s => s.IsCreator && s.State == SessionState.NotStarted);

            if (created is not null)
            {
                _awaitingCreatedSession = false;
                OpenSession(created.Id);
                // The create notice has done its job; the header goes back to the connection.
                _lastStatus = null;
                UpdateStatus();
            }
            else if (System.Environment.TickCount64 > _createdSessionDeadline)
            {
                // The request never reached the server (a dropped socket, a refused mode).
                _awaitingCreatedSession = false;
                SetStatus("The game was not created. Check the connection and try again.");
            }
        }

        // A highlight or an open match has to point at a row that still exists, or JOIN acts
        // on a stale id and the match view has nothing to read.
        if (Lobby.SelectedSessionId.Value is { } selected && sessions.All(s => s.Id != selected))
            Lobby.SelectedSessionId.Value = null;

        if (Lobby.ViewedSessionId.Value is { } viewed && sessions.All(s => s.Id != viewed))
            Lobby.ViewedSessionId.Value = null;

        if (Lobby.Page.Value == UiState.LobbyPage.Match && Lobby.ViewedSessionId.Value is null)
            Lobby.Page.Value = UiState.LobbyPage.Room;

        if (_stagePreview.Value is { } preview && preview.SessionId != Lobby.ViewedSessionId.Value)
            CloseStagePreview();

        foreach (var id in _liveStates.Keys.Where(id => sessions.All(s => s.Id != id)).ToArray())
            _liveStates.Remove(id);

        if (_chatMessages.RemoveAll(line => line.SessionId != 0 && sessions.All(s => s.Id != line.SessionId)) > 0)
            Lobby.Chat.Value = _chatMessages.ToArray();

        if (sessions.Any(session => session.State == SessionState.Finished))
            Lobby.NowUnixMs.Value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Lobby.MaxPlayers.Value = Math.Clamp(Lobby.MaxPlayers.Value, 2, MatchGameplayInfo.MaxPlayers);
        Lobby.BotCount.Value = Math.Clamp(
            Lobby.BotCount.Value,
            0,
            Lobby.MaxPlayers.Value - 1
        );
    }

    private bool IsMember(S2C_LobbyState.GameSession session)
        => session.Players.Values.Contains(_player.Id);

    private UiState.LobbySession ToUiSession(
        S2C_LobbyState.GameSession session,
        IReadOnlyDictionary<Guid, ServerSidePlayerInfo> humans,
        bool isMember,
        bool isCreator
    )
    {
        return new UiState.LobbySession(
            session.Id,
            session.CreatorName,
            SessionStageName(session.StageName),
            session.PlayerCount,
            session.MaxPlayers,
            session.State,
            isMember,
            isCreator,
            session.Gamemode,
            GamemodeCatalog.DisplayName(session.Gamemode),
            SessionSettings(session),
            SessionSlots(session, humans),
            LiveStandingsFor(session),
            FinalStandingsFor(session),
            session.ClosesAtUnixMs,
            session.RequiresInvite
        );
    }

    /// <summary>The live order the game master last pushed, already ordered server-side.</summary>
    private IReadOnlyList<UiState.LobbyStanding> LiveStandingsFor(S2C_LobbyState.GameSession session)
    {
        if (session.State != SessionState.Started || !_liveStates.TryGetValue(session.Id, out var live))

            return [];

        return MapLiveStandings(live);
    }

    private static IReadOnlyList<UiState.LobbyStanding> MapLiveStandings(IReadOnlyList<RaceLiveStanding> live)
        => [.. live.Select(standing => new UiState.LobbyStanding(
            standing.PlayerId,
            standing.PlayerName,
            standing.Position,
            standing.Lap,
            standing.Wasted,
            standing.Forfeited,
            standing.TotalLaps,
            standing.PowerPercent,
            standing.DamagePercent
        )
        )];

    private static IReadOnlyList<UiState.LobbyStanding> FinalStandingsFor(S2C_LobbyState.GameSession session)
        => [.. session.FinalStandings
            .OrderBy(standing => standing.Position)
            .Select(standing => new UiState.LobbyStanding(
                standing.PlayerId,
                standing.PlayerName,
                standing.Position,
                standing.Lap,
                standing.Wasted,
                standing.Dnf,
                standing.TotalLaps,
                standing.PowerPercent,
                standing.DamagePercent
            )
            )];

    /// <summary>
    /// The host's gamemode options, labelled through the schema the create dialog also uses.
    /// The schema is cached per gamemode: reading it loads and releases a plugin.
    /// </summary>
    private IReadOnlyList<UiState.LobbySessionSetting> SessionSettings(S2C_LobbyState.GameSession session)
    {
        if (!_configFields.TryGetValue(session.Gamemode, out var fields))
        {
            try
            {
                fields = GamemodeCatalog.ConfigFields(session.Gamemode);
            }
            catch (Exception ex)
            {
                // A game can use a mode this client cannot load. Its raw options still show.
                Logging.Warning($"Could not read {session.Gamemode} options: {ex.Message}");
                fields = [];
            }

            _configFields[session.Gamemode] = fields;
        }

        var settings = new List<UiState.LobbySessionSetting>();

        foreach (var field in fields)
        {
            if (!field.IsVisible(key => session.GamemodeParameters.TryGetValue(key, out var setting) ? setting : null)
                || !session.GamemodeParameters.TryGetValue(field.Name, out var value))

                continue;

            var label = field.Choices.FirstOrDefault(choice => choice.Value == value)?.Label ?? value;
            settings.Add(new UiState.LobbySessionSetting(field.Label, label, field, value));
        }

        // A value the installed plugin no longer declares is still worth showing, not hiding.

        foreach (var (name, value) in session.GamemodeParameters)
        {
            if (fields.All(field => field.Name != name))
                settings.Add(new UiState.LobbySessionSetting(name, value));
        }

        return settings;
    }

    private IReadOnlyList<UiState.LobbySessionSlot> SessionSlots(
        S2C_LobbyState.GameSession session,
        IReadOnlyDictionary<Guid, ServerSidePlayerInfo> humans
    )
    {
        if (session.State == SessionState.Finished)
        {
            return [.. session.FinalStandings.OrderBy(standing => standing.Slot).Select(standing =>
                new UiState.LobbySessionSlot(
                    standing.Slot,
                    standing.PlayerName,
                    CarLabel(standing.CarName),
                    ToColor(standing.Color),
                    PlayerColors(standing.CarName, standing.CarColors),
                    IsBot: standing.IsBot,
                    IsEmpty: false,
                    IsSelf: standing.PlayerId == _player.Id,
                    PlayerId: standing.PlayerId
                )
            )];
        }

        var slots = new List<UiState.LobbySessionSlot>(session.MaxPlayers);

        for (byte slot = 0; slot < session.MaxPlayers; slot++)
        {
            if (session.Players.TryGetValue(slot, out var playerId)
                && humans.TryGetValue(playerId, out var human))
            {
                slots.Add(
                    new UiState.LobbySessionSlot(
                        slot,
                        human.PlayerName,
                        CarLabel(human.CarName),
                        ToColor(human.Color),
                        PlayerColors(human),
                        IsBot: false,
                        IsEmpty: false,
                        IsSelf: playerId == _player.Id,
                        PlayerId: playerId
                    )
                );

                continue;
            }

            if (session.Bots.TryGetValue(slot, out var bot))
            {
                slots.Add(
                    new UiState.LobbySessionSlot(
                        slot,
                        bot.PlayerName,
                        CarLabel(bot.CarName),
                        ToColor(bot.Color),
                        PlayerColors(bot),
                        IsBot: true,
                        IsEmpty: false,
                        IsSelf: false,
                        PlayerId: bot.Id
                    )
                );

                continue;
            }

            slots.Add(
                new UiState.LobbySessionSlot(
                    slot,
                    "Empty",
                    "Waiting for player...",
                    default,
                    null,
                    IsBot: false,
                    IsEmpty: true,
                    IsSelf: false
                )
            );
        }

        return slots;
    }

    private void UpdateStatus()
    {
        Lobby.Connected.Value = CanAct;
        // Only once retries are exhausted; while reconnecting, Connected is already false but
        // the player is not being asked to do anything yet.
        Lobby.Disconnected.Value = transport.ReconnectFailed;

        // Only written when the connection text itself changes: a notice the player just saw
        // (a rejected action, say) must not be wiped by the next frame.
        var status = ConnectionStatus();

        if (status == _lastStatus)

            return;
        _lastStatus = status;
        Lobby.Status.Value = status;
    }

    /// <summary>
    /// A notice shown until the connection text next changes: <see cref="_lastStatus"/> still
    /// holds the connection text, so the per-tick update leaves the notice alone.
    /// </summary>
    private static void SetStatus(string message) => Lobby.Status.Value = message;

    private string ConnectionStatus() => transport.State switch
    {
        ClientState.Connected when transport.HandshakeError is { Length: > 0 } => "This build does not match the lobby. Update the game.",
        ClientState.Connected when !transport.HandshakeAccepted => $"Checking protocol v{Protocol.Version}...",
        ClientState.Connected when _authenticationFailed => "The lobby did not answer. Press BACK and try again.",
        ClientState.Connected when _authenticated && GameSparker.AccountManager.ActiveAccount is { } account =>
            $"Signed in as {account.Username}",
        ClientState.Connected => "Verifying account...",
        ClientState.Connecting => "Connecting...",
        _ when transport.HandshakeError is { Length: > 0 } => "This build does not match the lobby.",
        _ when transport.IsReconnecting => "Connection lost - reconnecting...",
        _ => "Disconnected",
    };

    private static Color ToColor(Color3 color)
    {
        static byte Channel(short value) => (byte)Math.Clamp((int)value, 0, 255);

        return new Color(Channel(color.R), Channel(color.G), Channel(color.B));
    }

    private UiState.LobbyChatLine ChatLine(S2C_LobbyChatMessage message)
    {
        var isSystem = message.SenderId == Guid.Empty;

        return new UiState.LobbyChatLine(
            ++_nextChatLineId,
            message.SessionId,
            message.SenderId,
            message.Sender,
            message.Message,
            ToColor(message.Color),
            isSystem
        );
    }

    /// <summary>Keeps one transcript for room and match chat, bounded so a long lobby cannot grow it forever.</summary>
    private void AddChatLine(UiState.LobbyChatLine line)
    {
        _chatMessages.Add(line);
        var roomChat = line.SessionId == 0;
        if (_chatMessages.Count(entry => (entry.SessionId == 0) == roomChat) > 400)
            _chatMessages.RemoveAt(_chatMessages.FindIndex(entry => (entry.SessionId == 0) == roomChat));
        Lobby.Chat.Value = _chatMessages.ToArray();
    }

    // ── Actions ────────────────────────────────────────────────────

    /// <summary>
    /// Whether an action can reach the lobby. Everything but BACK needs a live connection;
    /// while reconnecting a packet would be written into a closed socket and dropped.
    /// </summary>
    private bool CanAct => _authenticated
                           && transport.State == ClientState.Connected
                           && transport.HandshakeAccepted;

    private void OnOpenSession(uint id)
    {
        if (!Lobby.Sessions.Value.Any(session => session.Id == id))

            return;

        OpenSession(id);
    }

    private void OpenSession(uint id)
    {
        if (_stagePreview.Value is { } preview && preview.SessionId != id)
            CloseStagePreview();
        Lobby.SelectedSessionId.Value = id;
        Lobby.ViewedSessionId.Value = id;
        Lobby.Page.Value = UiState.LobbyPage.Match;
        ApplyLiveState(id);
    }

    /// <summary>
    /// Replaces one session's live order without rebuilding slots/settings/rows. A live
    /// snapshot arrives every two seconds, and a full Publish would remount the match page.
    /// </summary>
    private void ApplyLiveState(uint sessionId)
    {
        if (!_liveStates.TryGetValue(sessionId, out var live))

            return;

        var rows = MapLiveStandings(live);
        Lobby.Sessions.Value = Lobby.Sessions.Value
            .Select(session => session.Id == sessionId && session.State == SessionState.Started
                ? session with { LiveStandings = rows }
                : session
            )
            .ToArray();
    }

    private void OnCloseSession()
    {
        CloseStagePreview();
        Lobby.ViewedSessionId.Value = null;
        Lobby.Page.Value = UiState.LobbyPage.Room;
    }

    private void OnJoin(uint id) => JoinGame(id, Guid.Empty);

    private void JoinGame(uint id, Guid inviteId)
    {
        // Never the session we are already in: joining is what changes membership, so reading
        // CurrentSession here would make JOIN a no-op.

        if (!_carsActive || !CanAct || Lobby.CurrentSession.Value?.Id == id)

            return;

        OpenSession(id);
        transport.SendPacketToServer(new C2S_JoinSession { SessionId = id, InviteId = inviteId });
    }

    /// <summary>
    /// Watch a running race. The lobby answers with an <see cref="S2C_RaceStarted"/> marked as a
    /// spectator, which <see cref="OnRaceStarted"/> turns into a race with no client player.
    /// </summary>
    private void OnSpectate(uint id)
    {
        if (!CanAct)

            return;

        OpenSession(id);
        transport.SendPacketToServer(new C2S_SpectateSession { SessionId = id });
    }

    private void OnLeave()
    {
        if (!CanAct || Lobby.CurrentSession.Value is not { } session)

            return;

        transport.SendPacketToServer(new C2S_LeaveSession { SessionId = session.Id });

        // The leave is fire-and-forget and nothing else re-reads membership until the server's
        // next state broadcast, so the row kept offering the session START acts on. Dropping our
        // membership here makes the buttons follow the leave immediately; the broadcast that
        // follows replaces this list with the server's.
        Lobby.CurrentSession.Value = null;
        Lobby.Sessions.Value = Lobby.Sessions.Value
            .Select(s => s.Id == session.Id ? s with { IsMember = false } : s)
            .ToArray();
        OnCloseSession();
    }

    /// <summary>
    /// Starts the session the button belongs to. Takes the row's id rather than reading
    /// CurrentSession, so a row left over from before a leave cannot start the wrong race.
    /// </summary>
    private void OnStartRace(uint id)
    {
        if (CanAct && RequireRaceableCar())
            transport.SendPacketToServer(new C2S_LobbyStartRace { SessionId = id });
    }

    /// <summary>
    /// The lobby refuses a local car and falls back to a stock one, so say so before the race
    /// instead of letting the player line up in a car they did not choose.
    /// </summary>
    private bool RequireRaceableCar()
    {
        var car = MenuHistory.LastPlayed.Car;

        if (MultiplayerContentPolicy.IsAcceptedCar(car))

            return true;

        SetStatus($"'{car}' is local-only and cannot be raced online. Pick a stock car or publish it.");

        return false;
    }

    private void OnCreate()
    {
        if (!CanAct)
        {
            SetStatus("Not connected to the lobby.");

            return;
        }

        var stage = Lobby.SelectedStage.Value;

        if (string.IsNullOrEmpty(stage))
        {
            SetStatus("Choose a stage first.");

            return;
        }

        if (!MultiplayerContentPolicy.IsAcceptedStage(stage))
        {
            SetStatus(LobbyStageSelection.LocalStageRestriction);

            return;
        }

        if (!RequireRaceableCar())

            return;

        transport.SendPacketToServer(
            new C2S_CreateSession
            {
                GameMode = Lobby.SelectedGamemode.Value,
                StageName = stage,
                MaxPlayers = Lobby.MaxPlayers.Value,
                GameModeParameters = new(Lobby.GamemodeConfig.Value),
                Bots = Lobby.SelectedGamemode.Value == DefaultGamemodes.TimeTrial ? 0 : Lobby.BotCount.Value
            }
        );
        _awaitingCreatedSession = true;
        _createdSessionDeadline = System.Environment.TickCount64 + 10_000;
        SetStatus("Creating game...");
        CancelStageSearch();
        Lobby.CreateStages.Busy.Value = false;
    }

    private void OnSendChat(string message)
    {
        if (!CanAct || string.IsNullOrWhiteSpace(message))

            return;

        uint sessionId = 0;

        if (Lobby.Page.Value == UiState.LobbyPage.Match)
        {
            var match = Lobby.Sessions.Value.FirstOrDefault(session => session.Id == Lobby.ViewedSessionId.Value);

            if (match is null)

                return;
            sessionId = match.Id;
        }

        transport.SendPacketToServer(new C2S_LobbyChatMessage { Message = message, SessionId = sessionId });
    }

    private void OnOpenCreate()
    {
        // Drop a previous failure notice; the page is a fresh attempt.
        _lastStatus = null;
        UpdateStatus();
        OnSetGamemode(Lobby.SelectedGamemode.Value);
        Lobby.BotCount.Value = 0;
        var stages = LocalStages;
        if (string.IsNullOrEmpty(Lobby.SelectedStage.Value))
            Lobby.SelectedStage.Value = stages.FirstOrDefault()?.Id ?? "";
        // The create page renders this state, so it has to be filled before it is shown.
        LobbyStageSelection.ShowLocal(Lobby.CreateStages, stages, Lobby.SelectedStage.Value);
        Lobby.Page.Value = UiState.LobbyPage.Create;
    }

    /// <summary>Leaves the create page and drops anything it was still doing, so a search cannot land later.</summary>
    private void OnCloseCreate()
    {
        _awaitingCreatedSession = false;
        Lobby.Page.Value = UiState.LobbyPage.Room;
        CancelStageSearch();
        Lobby.CreateStages.Busy.Value = false;
    }

    private void CancelStageSearch()
    {
        _stageSearchCancellation?.Cancel();
        _stageSearchCancellation?.Dispose();
        _stageSearchCancellation = null;
        _stageSearchGeneration++;
    }

    private void OnStageSourceSelected(bool online)
    {
        var state = Lobby.CreateStages;

        if (state.OnlineSource.Value == online)

            return;

        CancelStageSearch();

        if (!online)
        {
            LobbyStageSelection.ShowLocal(state, LocalStages, Lobby.SelectedStage.Value);

            return;
        }

        state.OnlineSource.Value = true;
        state.Stages.Value = [];
        state.Search.Value = "";
        state.Restriction.Value = "";
        StageSelectView.ClearSearch();
        _ = SearchStagesOnlineAsync("");
    }

    private void OnStageSearchChanged(string query)
    {
        var state = Lobby.CreateStages;

        if (state.OnlineSource.Value)
        {
            // Archive search is a request, not a filter: Enter starts it.
            state.Search.Value = query;

            return;
        }

        LobbyStageSelection.ApplyFilter(state, LocalStages, query);
    }

    private void OnSelectStage(string stageId)
    {
        var state = Lobby.CreateStages;
        var option = state.Stages.Value.FirstOrDefault(stage => stage.Id == stageId);

        if (option is null)

            return;

        // The session's stage is only replaced by one the lobby will accept, so a refused pick
        // leaves the previous choice in place rather than turning into a broken session.
        if (LobbyStageSelection.Select(state, stageId))
            Lobby.SelectedStage.Value = stageId;
    }

    private async Task SearchStagesOnlineAsync(string query)
    {
        var state = Lobby.CreateStages;

        if (!state.OnlineSource.Value)

            return;

        // A typed archive id is not a search term: it names one stage exactly.
        var typed = RadpackReader.Identity(query.Trim());

        if (MultiplayerContentPolicy.IsCanonicalArchiveId(typed))
        {
            CancelStageSearch();
            if (LobbyStageSelection.Select(state, typed))
                Lobby.SelectedStage.Value = typed;
            // The empty result list still shows the chosen row, so the pick is visible.
            LobbyStageSelection.ShowRemote(state, []);
            state.Status.Value = "Using the typed archive id.";

            return;
        }

        CancelStageSearch();

        var cancellation = new CancellationTokenSource();
        _stageSearchCancellation = cancellation;
        var generation = _stageSearchGeneration;

        state.Busy.Value = true;
        state.Status.Value = "Searching the archive...";

        try
        {
            var page = await GarageController.ContentClient.SearchAsync(query, "Stage", 40, null, cancellation.Token);

            if (generation != _stageSearchGeneration)

                return;

            var stages = LobbyStageSelection.RemoteStages(page.Items);
            LobbyStageSelection.ShowRemote(state, stages);
            state.Status.Value = stages.Count == 0
                ? "No public stages matched."
                : $"{stages.Count} public stage(s). Pick one to use it.";
        }
        catch (Exception ex)
        {
            if (generation != _stageSearchGeneration)

                return;

            // The chosen stage keeps its row, so a failed search does not look like a lost pick.
            LobbyStageSelection.ShowRemote(state, []);
            state.Status.Value = $"Could not search the archive: {ex.Message}";
        }
        finally
        {
            if (generation == _stageSearchGeneration)
                state.Busy.Value = false;
        }
    }

    private void OnSetGamemode(string id)
    {
        if (!GamemodeCatalog.Ids.Contains(id))

            return;
        Lobby.SelectedGamemode.Value = id;

        // Read the plugin's schema once, here, and publish a copy: the view must never load it.
        IReadOnlyList<GamemodeConfigField> fields;

        try
        {
            fields = GamemodeCatalog.ConfigFields(id);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not read {id} options: {ex.Message}");
            fields = [];
        }

        Lobby.GamemodeFields.Value = fields;

        var defaults = new Dictionary<string, string>();

        foreach (var field in fields)
        {
            if (!defaults.ContainsKey(field.Name))
                defaults[field.Name] = field.Choices[0].Value;
        }

        Lobby.GamemodeConfig.Value = defaults;
        if (id == DefaultGamemodes.TimeTrial)
            Lobby.BotCount.Value = 0;
    }

    private void OnSetGameRule(uint sessionId, string name, string value)
    {
        if (!CanAct
            || Lobby.ViewedSessionId.Value != sessionId
            || Lobby.Sessions.Value.FirstOrDefault(session => session.Id == sessionId)
                is not { IsCreator: true, IsMember: true, State: SessionState.NotStarted } match
            || !match.Settings.Any(setting => setting.Field is { } field
                                              && field.Name == name
                                              && field.Choices.Any(choice => choice.Value == value)
            ))

            return;

        transport.SendPacketToServer(new C2S_SetGameRule { SessionId = sessionId, Name = name, Value = value });
    }

    private void OnSetConfig(string name, string value)
    {
        var config = new Dictionary<string, string>(Lobby.GamemodeConfig.Value) { [name] = value };
        Lobby.GamemodeConfig.Value = config;
    }

    private void OnBack() => GameSparker.PopPhase();

    private void SendUpdatePlayerIdentity()
    {
        transport.SendPacketToServer(
            new C2S_PlayerIdentity
            {
                PlayerName = _player.Name,
                // The canonical selection, not the loaded model's name: an archive identity has
                // to survive even when the car has not been cached yet, and the lobby re-checks it.
                SelectedVehicle = MenuHistory.LastPlayed.Car,
                Color = _player.Color,
                CarColors = NFMWorldLibrary.Util.CarPaint.SavedColors(MenuHistory.LastPlayed.Car),
                Wheels = NFMWorldLibrary.Util.CarWheels.Get(MenuHistory.LastPlayed.Car)
            }
        );
    }

    private static BaseGamemodeFactory GetGameModeFactory(MatchGameplayInfo matchGameplayInfo)
        => GamemodeFactory.Create(matchGameplayInfo.Gamemode, matchGameplayInfo.Parameters);
}

internal class Player
{
    public Guid Id { get; set; }
    public string Name { get; set; } = System.Environment.UserName;
    public Color3 Color { get; set; } = new(0, 128, 255);
}
