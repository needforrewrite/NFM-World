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
public class LobbyPhase(IMultiplayerClientTransport transport) : BasePhase
{
    private readonly Player _player = new();
    private List<ServerSidePlayerInfo> _players = [];
    private List<S2C_LobbyState.GameSession> _activeSessions = [];
    private readonly List<UiState.LobbyChatLine> _chatMessages = [];
    private long _nextChatLineId;
    private bool _sentAuthentication;
    private bool _authenticated;
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

    private LobbyView.Actions Actions => new(
        Join: OnJoin,
        Leave: OnLeave,
        StartRace: OnStartRace,
        Create: OnCreate,
        Refresh: OnRefresh,
        SendChat: OnSendChat,
        Back: OnBack,
        OpenCreate: OnOpenCreate,
        CloseCreate: () => Lobby.ShowCreateDialog.Value = false,
        Select: id => Lobby.SelectedSessionId.Value = id,
        Spectate: OnSpectate,
        SetStage: stage => Lobby.SelectedStage.Value = stage,
        SetMaxPlayers: count => Lobby.MaxPlayers.Value = count,
        SetBots: count => Lobby.BotCount.Value = count,
        SetConfig: OnSetConfig,
        SetGamemode: OnSetGamemode
    );

    public override Node BuildUi() => LobbyView.Build(Actions);

    public override void Enter()
    {
        LobbyView.Reset();
        Lobby.Reset();
        // Reset emptied the status signal, so the cached text would make UpdateStatus a no-op
        // and leave the line blank on re-entry.
        _lastStatus = null;
        // Enter runs on every return to the lobby, so a race that already happened is stale
        // from here on: the next session's start failure must still be reported.
        _racedThisSession = false;
        // Returning from a race re-enters this phase, and the server only resends lobby state
        // on the next change; the transcript this client already received stays put.
        Lobby.Chat.Value = _chatMessages.ToArray();
        UpdateStatus();
        // Signing in from the account screen while sitting in the lobby has to be presented too;
        // Enter is paired with the Unsubscribe in Dispose, and pop/re-enter is balanced.
        GameSparker.AccountManager.ActiveAccountChanged += OnAccountChanged;
        // A reconnected socket is a new session on the server, so the handshake is replayed.
        transport.Reconnected += OnReconnected;
        if (_authenticated && transport.State == ClientState.Connected)
            SendUpdatePlayerIdentity();
        base.Enter();
    }

    private void OnReconnected()
    {
        // A reconnected transport is a brand-new connection: the lobby knows nothing about
        // this player until it authenticates and identifies itself again.
        ResetHandshake();
        _player.Id = Guid.Empty;
        _players = [];
        _activeSessions = [];
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

        // A refused version is final for this connection: the server will keep refusing it, so
        // say why and leave rather than sitting on a handshake that can never finish.
        if (!_reportedVersionRefusal && transport.HandshakeError is { Length: > 0 } versionRefusal)
        {
            _reportedVersionRefusal = true;
            Logging.Warning($"Lobby refused this build: {versionRefusal}");
            GameSparker.PopPhase();
            GameSparker.MessageWindow.ShowMessage("Update required", versionRefusal);
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

        foreach (var packet in transport.GetNewPackets())
        {
            switch (packet)
            {
                case S2C_LobbyChatMessage chatMessage:
                    _chatMessages.Add(ChatLine(chatMessage));
                    Lobby.Chat.Value = _chatMessages.ToArray();
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

                case S2C_RaceStarted raceStarted:
                    StartRace(raceStarted);
                    break;

                case S2C_PrepareContent prepareContent:
                    PrepareContent(prepareContent);
                    break;

                // The session's load window elapsed (or the game master refused it). Nothing
                // else resets the row, so say so rather than leaving it on "LOADING". The lobby
                // sends this to everyone in the session, including a client that has already
                // gone back to the lobby after racing, so it must not overwrite a race that
                // actually ran.
                case S2C_RaceFailedToStart when !_racedThisSession:
                    SetStatus("The race could not start. Try again.");
                    break;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // A preparation outliving the phase would keep downloading for a race this client
            // has left, and its acknowledgement would answer a request nobody is waiting on.
            _preparationCancellation?.Cancel();
            _preparationCancellation?.Dispose();
            _preparationCancellation = null;
            _pendingSpectate = null;
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
        // GameSession is a struct, so FirstOrDefault yields a default that looks like a real
        // session; the predicate has to be the only source of truth.
        UiState.LobbySession? current = null;
        foreach (var s in _activeSessions)
        {
            if (!s.Players.Values.Contains(_player.Id))
                continue;
            current = new UiState.LobbySession(
                s.Id,
                s.CreatorName,
                s.StageName,
                s.PlayerCount,
                s.MaxPlayers,
                s.State,
                IsMember: true,
                IsCreator: s.CreatorId == _player.Id
            );
            break;
        }

        Lobby.Players.Value = _players
            .Select(p => new UiState.LobbyPlayer(
                    p.Id,
                    p.PlayerName,
                    p.CarName,
                    ToColor(p.Color),
                    p.Id == _player.Id
                )
            )
            .ToArray();

        Lobby.Sessions.Value = _activeSessions
            .Select(s => new UiState.LobbySession(
                    s.Id,
                    s.CreatorName,
                    s.StageName,
                    s.PlayerCount,
                    s.MaxPlayers,
                    s.State,
                    IsMember: s.Players.Values.Contains(_player.Id),
                    IsCreator: s.CreatorId == _player.Id
                )
            )
            .ToArray();

        Lobby.CurrentSession.Value = current;

        // A highlight has to point at a row that still exists, or JOIN acts on a stale id.
        if (Lobby.SelectedSessionId.Value is { } selected
            && !_activeSessions.Any(s => s.Id == selected))
            Lobby.SelectedSessionId.Value = null;

        Lobby.MaxPlayers.Value = Math.Clamp(Lobby.MaxPlayers.Value, 2, MatchGameplayInfo.MaxPlayers);
        Lobby.BotCount.Value = Math.Clamp(
            Lobby.BotCount.Value,
            0,
            MatchGameplayInfo.MaxPlayers - Lobby.MaxPlayers.Value
        );
        if (string.IsNullOrEmpty(Lobby.SelectedStage.Value))
            Lobby.SelectedStage.Value = GameSparker.GetAvailableStages().FirstOrDefault() ?? "";
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
        ClientState.Connected when !transport.HandshakeAccepted => "Checking game version...",
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
        var color = _players.FirstOrDefault(p => p.Id == message.SenderId) is { } sender
            ? ToColor(sender.Color)
            : new Color(185, 211, 255);
        return new UiState.LobbyChatLine(++_nextChatLineId, message.Sender, message.Message, color, isSystem);
    }

    // ── Actions ────────────────────────────────────────────────────

    /// <summary>
    /// Whether an action can reach the lobby. Everything but BACK needs a live connection;
    /// while reconnecting a packet would be written into a closed socket and dropped.
    /// </summary>
    private bool CanAct => _authenticated
                           && transport.State == ClientState.Connected
                           && transport.HandshakeAccepted;

    private void OnJoin(uint id)
    {
        // Never the session we are already in: joining is what changes membership, so reading
        // CurrentSession here would make JOIN a no-op.
        if (!CanAct || Lobby.CurrentSession.Value?.Id == id)
            return;

        transport.SendPacketToServer(new C2S_JoinSession { SessionId = id });
    }

    /// <summary>
    /// Watch a running race. The lobby answers with an <see cref="S2C_RaceStarted"/> marked as a
    /// spectator, which <see cref="OnRaceStarted"/> turns into a race with no client player.
    /// </summary>
    private void OnSpectate(uint id)
    {
        if (CanAct)
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
        Lobby.ShowCreateDialog.Value = false;
    }

    private void OnSendChat(string message)
    {
        if (!CanAct || string.IsNullOrWhiteSpace(message))
            return;
        transport.SendPacketToServer(new C2S_LobbyChatMessage { Message = message });
    }

    private void OnOpenCreate()
    {
        OnSetGamemode(Lobby.SelectedGamemode.Value);
        Lobby.BotCount.Value = 0;
        Lobby.ShowCreateDialog.Value = true;
    }

    private void OnSetGamemode(string id)
    {
        if (!GamemodeCatalog.Ids.Contains(id)) return;
        Lobby.SelectedGamemode.Value = id;
        var defaults = new Dictionary<string, string>();
        foreach (var property in GamemodeCatalog.ConfigProperties(id))
        {
            if (property.Options.Count > 0 && !defaults.ContainsKey(property.Name))
                defaults[property.Name] = property.Options[0].Value;
        }

        Lobby.GamemodeConfig.Value = defaults;
        if (id == DefaultGamemodes.TimeTrial)
            Lobby.BotCount.Value = 0;
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
                Color = _player.Color
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
