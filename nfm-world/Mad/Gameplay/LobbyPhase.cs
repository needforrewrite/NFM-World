using NFMWorld.Gameplay.RaceHost;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

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
    private bool _sentClientIdentity;
    private string? _lastStatus;

    private static UiState.LobbyState Lobby => UiState.Lobby;

    private LobbyView.Actions Actions => new(
        Join: OnJoin,
        Leave: OnLeave,
        StartRace: OnStartRace,
        Create: OnCreate,
        Refresh: () => Lobby.Status.Value = ConnectionStatus(),
        SendChat: OnSendChat,
        Back: OnBack,
        OpenCreate: () => Lobby.ShowCreateDialog.Value = true,
        CloseCreate: () => Lobby.ShowCreateDialog.Value = false,
        Select: id => Lobby.SelectedSessionId.Value = id,
        SetStage: stage => Lobby.SelectedStage.Value = stage,
        SetMaxPlayers: count => Lobby.MaxPlayers.Value = count);

    public override Node BuildUi() => LobbyView.Build(Actions);

    public override void Enter()
    {
        LobbyView.Reset();
        Lobby.Reset();
        // Reset emptied the status signal, so the cached text would make UpdateStatus a no-op
        // and leave the line blank on re-entry.
        _lastStatus = null;
        // Returning from a race re-enters this phase, and the server only resends lobby state
        // on the next change; the transcript this client already received stays put.
        Lobby.Chat.Value = _chatMessages.ToArray();
        UpdateStatus();
        // Signing in from the account screen while sitting in the lobby has to be presented too;
        // Enter is paired with the Unsubscribe in Dispose, and pop/re-enter is balanced.
        GameSparker.AccountManager.ActiveAccountChanged += OnAccountChanged;
        // A reconnected socket is a new session on the server, so the handshake is replayed.
        transport.Reconnected += OnReconnected;
        base.Enter();
    }

    private void OnReconnected()
    {
        // A reconnected transport is a brand-new connection: the lobby knows nothing about
        // this player until it authenticates and identifies itself again.
        _sentClientIdentity = false;
        _player.Id = Guid.Empty;
        _players = [];
        _activeSessions = [];
        Publish();
    }

    private void OnAccountChanged(NFMWorld.Accounts.Account? account)
    {
        // Empty token = signing out, which the lobby models as a guest.
        if (transport.State != ClientState.Connected)
            return;
        transport.SendPacketToServer(new C2S_Authenticate { Token = account?.Token ?? "" });
        UpdateStatus();
    }

    public override void GameTick()
    {
        base.GameTick();

        if (!_sentClientIdentity && transport.State == ClientState.Connected)
        {
            _sentClientIdentity = true;

            // Presented before the identity packet: a signed-in player's name and account come
            // from the account, and the identity packet's name is ignored for them.
            transport.SendPacketToServer(new C2S_Authenticate
            {
                Token = GameSparker.AccountManager.ActiveAccount?.Token ?? ""
            });
            SendUpdatePlayerIdentity();
        }

        // The transport can drop at any point (server restart, network loss); the status line
        // has to follow it or it stays on "Connecting..." forever after a failed connect.
        UpdateStatus();

        foreach (var packet in transport.GetNewPackets())
        {
            switch (packet)
            {
                case S2C_LobbyChatMessage chatMessage:
                    _chatMessages.Add(ChatLine(chatMessage));
                    Lobby.Chat.Value = _chatMessages.ToArray();
                    break;

                case S2C_Authenticated authenticated:
                    if (!authenticated.Accepted)
                    {
                        // The lobby holds the authority here, so a token it refuses is treated as
                        // signed out locally too. The lobby also disconnects us, and reconnecting
                        // would only repeat the refusal.
                        Logging.Info($"Lobby rejected the session: {authenticated.Error}");
                        GameSparker.AccountManager.LogOut();
                        transport.StopReconnecting();
                        GameSparker.PopPhase();
                        GameSparker.MessageWindow.ShowMessage("Session refused",
                            authenticated.Error is { Length: > 0 }
                                ? authenticated.Error
                                : "Your session was rejected. Sign in again.");
                    }
                    else if (authenticated.AccountId == 0 && GameSparker.AccountManager.LoggedIn)
                    {
                        // Admitted as a guest even though we presented a token: the lobby could
                        // not check it. Keep the token - it is not known to be bad.
                        SetStatus(authenticated.Error is { Length: > 0 }
                            ? authenticated.Error
                            : "Playing as a guest.");
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

                // The session's load window elapsed (or the game master refused it). Nothing
                // else resets the row, so say so rather than leaving it on "LOADING".
                case S2C_RaceFailedToStart:
                    SetStatus("The race could not start. Try again.");
                    break;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Leaving the lobby must close the socket: the server only drops the ghost player when
            // the connection goes, and the receive loop would otherwise run for the process lifetime.
            GameSparker.AccountManager.ActiveAccountChanged -= OnAccountChanged;
            transport.Reconnected -= OnReconnected;
            transport.Stop();
        }
        base.Dispose(disposing);
    }

    private void StartRace(S2C_RaceStarted raceStarted)
    {
        // A NEW transport to the Game Master for in-game traffic. The lobby transport stays
        // alive, so exiting the race returns to this lobby.
        var gameAddr = raceStarted.JoinInfo.RaceServerIpAddress;
        var ipString = ((System.Net.IPAddress)gameAddr.Address).ToString();
        var gameTransport = new ENetMultiplayerClientTransport(ipString, gameAddr.Port);

        var session = raceStarted.MatchGameplayInfo;
        var players = session.Players
            .Select(c => new ClientSidePlayerInfo
            {
                CarName = c.Value.CarName,
                Color = c.Value.Color,
                PlayerName = c.Value.PlayerName,
                IsBot = c.Value.IsBot,
                IsClientPlayer = c.Value.Id == _player.Id
            })
            .ToArray();

        var phase = new RacePhase(
            session.StageName,
            GetGameModeFactory(session),
            players,
            new NetworkRaceHost(gameTransport, session, raceStarted.JoinInfo.JoinToken));

        phase.Exited += (_, _) => GameSparker.PopGroup(PhaseManager.Groups.Event);
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
            current = new UiState.LobbySession(s.Id, s.CreatorName, s.StageName, s.PlayerCount,
                s.MaxPlayers, s.State, IsMember: true, IsCreator: s.CreatorId == _player.Id);
            break;
        }

        Lobby.Players.Value = _players
            .Select(p => new UiState.LobbyPlayer(
                p.Id, p.PlayerName, p.CarName, ToColor(p.Color), p.Id == _player.Id))
            .ToArray();

        Lobby.Sessions.Value = _activeSessions
            .Select(s => new UiState.LobbySession(
                s.Id, s.CreatorName, s.StageName, s.PlayerCount, s.MaxPlayers, s.State,
                IsMember: s.Players.Values.Contains(_player.Id),
                IsCreator: s.CreatorId == _player.Id))
            .ToArray();

        Lobby.CurrentSession.Value = current;

        // A highlight has to point at a row that still exists, or JOIN acts on a stale id.
        if (Lobby.SelectedSessionId.Value is { } selected
            && !_activeSessions.Any(s => s.Id == selected))
            Lobby.SelectedSessionId.Value = null;

        Lobby.MaxPlayers.Value = Math.Clamp(Lobby.MaxPlayers.Value, 2, 8);
        if (string.IsNullOrEmpty(Lobby.SelectedStage.Value))
            Lobby.SelectedStage.Value = GameSparker.GetAvailableStages().FirstOrDefault() ?? "";
    }

    private void UpdateStatus()
    {
        Lobby.Connected.Value = transport.State == ClientState.Connected;
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
        ClientState.Connected when GameSparker.AccountManager.LoggedIn =>
            $"Signed in as {GameSparker.AccountManager.ActiveAccount!.Username}",
        ClientState.Connected => "Connected as a guest",
        ClientState.Connecting => "Connecting...",
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
    private bool CanAct => transport.State == ClientState.Connected;

    private void OnJoin(uint id)
    {
        // Never the session we are already in: joining is what changes membership, so reading
        // CurrentSession here would make JOIN a no-op.
        if (!CanAct || Lobby.CurrentSession.Value?.Id == id)
            return;

        transport.SendPacketToServer(new C2S_JoinSession { SessionId = id });
    }

    private void OnLeave()
    {
        if (CanAct && Lobby.CurrentSession.Value is { } session)
            transport.SendPacketToServer(new C2S_LeaveSession { SessionId = session.Id });
    }

    private void OnStartRace()
    {
        if (CanAct && Lobby.CurrentSession.Value is { IsCreator: true } session)
            transport.SendPacketToServer(new C2S_LobbyStartRace { SessionId = session.Id });
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

        transport.SendPacketToServer(new C2S_CreateSession
        {
            GameMode = DefaultGamemodes.PvP,
            StageName = stage,
            // The wire field is an int; the server clamps it to its own maximum.
            MaxPlayers = Lobby.MaxPlayers.Value,
            GameModeParameters = new Dictionary<string, string> { ["constraint"] = "both" }
        });
        Lobby.ShowCreateDialog.Value = false;
    }

    private void OnSendChat(string message)
    {
        if (!CanAct || string.IsNullOrWhiteSpace(message))
            return;
        transport.SendPacketToServer(new C2S_LobbyChatMessage { Message = message });
    }

    private void OnBack() => GameSparker.PopPhase();

    private void SendUpdatePlayerIdentity()
    {
        transport.SendPacketToServer(new C2S_PlayerIdentity
        {
            PlayerName = _player.Name,
            SelectedVehicle = _player.Vehicle,
            Color = _player.Color
        });
    }

    private static BaseGamemodeFactory GetGameModeFactory(MatchGameplayInfo matchGameplayInfo)
        => GamemodeFactory.Create(matchGameplayInfo.Gamemode, matchGameplayInfo.Parameters);
}

internal class Player
{
    public Guid Id { get; set; }
    public string Name { get; set; } = System.Environment.UserName;
    public string Vehicle { get; set; } = "nfmm/radicalone";
    public Color3 Color { get; set; } = new(0, 128, 255);
}
