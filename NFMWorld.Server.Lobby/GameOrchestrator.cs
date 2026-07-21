using NFMWorldLibrary.Multiplayer.HttpMessages;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

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

    private Thread? _lobbyThread;
    private bool _lobbyIsRunning = true;

    public GameOrchestrator(IMultiplayerServerTransport transport)
    {
        _transport = transport;
        _players = new PlayerRegistry();
        _sessions = new SessionManager(transport, _players);
        _chat = new ChatManager(transport, _players);
        _broadcaster = new LobbyStateBroadcaster(transport, _players, _sessions);

        _gmRegistry = GameMasterRegistry.FromEnvironment();
        _gmClient = new GameMasterHttpClient(
            Environment.GetEnvironmentVariable("HMAC_KEY_ID") ?? "primary",
            Environment.GetEnvironmentVariable("HMAC_SECRET_KEY") ?? "");

        transport.PacketReceived += TransportOnPacketReceived;
        transport.ClientConnected += TransportOnClientConnected;
        transport.ClientDisconnected += TransportOnClientDisconnected;
        transport.ClientConnecting += TransportOnClientConnecting;
    }

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
                    false);
            }

            Thread.Sleep(1000);
        }
    }

    // ── Transport events ─────────────────────────────────────────────

    private void TransportOnClientConnecting(object? sender, uint clientIndex)
    {
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
            if (client.InSession is { } inSession)
            {
                var session = _sessions.Get(inSession.SessionIndex);
                session?.Players.TryRemove(KeyValuePair.Create(inSession.PlayerIndex, client.Id));
            }

            _chat.BroadcastSystem($"{client.Name} has left...");
            _broadcaster.BroadcastToAll();
        }
    }

    // ── Packet dispatch ──────────────────────────────────────────────

    private void TransportOnPacketReceived(object? sender,
        (uint ClientIndex, IPacketClientToServer Packet) e)
    {
        switch (e.Packet)
        {
            case C2S_PlayerIdentity identity:
                HandlePlayerIdentity(e.ClientIndex, identity);
                break;

            case C2S_CreateSession create:
                HandleCreateSession(e.ClientIndex, create);
                break;

            case C2S_JoinSession join:
                HandleJoinSession(e.ClientIndex, join);
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
                throw new ArgumentOutOfRangeException(nameof(e.Packet),
                    $"Unexpected packet type: {e.Packet.GetType().Name}");
        }
    }

    // ── Packet handlers ──────────────────────────────────────────────

    private void HandlePlayerIdentity(uint clientId, C2S_PlayerIdentity identity)
    {
        var client = _players.Get(clientId);
        if (client is null) return;

        client.Name = identity.PlayerName;
        client.Vehicle = identity.SelectedVehicle;
        client.Color = identity.Color;
    }

    private void HandleCreateSession(uint clientId, C2S_CreateSession create)
    {
        var player = _players.Get(clientId);
        if (player is null) return;

        var session = _sessions.CreateSession(clientId, create.StageName, (byte)create.MaxPlayers, create.GameMode);

        _chat.BroadcastSystem(
            $"{session.CreatorName} has started a session for {session.StageName}!");
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

    private void HandleLeaveSession(uint clientId, C2S_LeaveSession leave)
    {
        var player = _players.Get(clientId);
        if (player is null) return;

        var session = _sessions.LeaveSession(clientId, leave.SessionId);
        if (session is not null)
        {
            _chat.BroadcastSystem(
                $"{player.Name} has left {session.CreatorName}'s session!");
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

        var playerInfos = new Dictionary<byte, PlayerInfo>();
        foreach (var (index, pid) in session.Players)
        {
            var p = _players.Get(pid);
            if (p is null)
                return;
            playerInfos[index] = new PlayerInfo
            {
                Id = p.Id,
                Name = p.Name,
                Vehicle = p.Vehicle,
                Color = p.Color
            };
        }

        var matchInfo = new MatchGameplayInfo
        {
            StageName = session.StageName,
            Gamemode = session.Gamemode,
            Players = playerInfos
        };

        try
        {
            var gm = _gmRegistry.SelectGameMaster();
            var createRequest = new Lobby2RaceServer_CreateRace
            {
                MatchKey = Guid.NewGuid(),
                MatchGameplayInfo = matchInfo
            };

            var createResponse = await _gmClient.CreateRaceAsync(gm, createRequest);
            _gmRegistry.MarkSuccess(gm);

            _chat.BroadcastSystem(
                $"{session.CreatorName} has started the race on {session.StageName}!");

            // Use the GM's SRV-resolved game address (Lobby already knows it)
            var gameAddress = gm.GameAddress;

            foreach (var (index, id) in session.Players)
            {
                if (createResponse.PlayerSecretIds.TryGetValue(index, out var token))
                {
                    var player = _players.Get(id);
                    if (player != null)
                    {
                        _transport.SendPacketToClient(player.ClientIndex, new S2C_RaceStarted
                        {
                            MatchGameplayInfo = matchInfo,
                            State = session.State,
                            JoinInfo = new S2C_RaceStarted.GameJoinInfo
                            {
                                RaceServerIpAddress = gameAddress,
                                JoinToken = token
                            }
                        });
                    }
                }
            }

            _broadcaster.BroadcastToAll();
        }
        catch (Exception ex)
        {
            // Revert session state on failure
            session.State = SessionState.NotStarted;
            session.StartTime = null;

            foreach (var (_, pid) in session.Players)
            {
                var p = _players.Get(pid);
                if (p is not null) p.IsInGame = false;
            }

            _chat.BroadcastSystem(
                $"Failed to start race on {session.StageName}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}