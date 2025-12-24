using System.Collections.Concurrent;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class GameOrchestrator
{
    private ConcurrentDictionary<uint, ClientInfo> _connectedClients = new();
    private readonly Thread _lobbyThread;
    private bool _lobbyIsRunning = true;
    private readonly IMultiplayerServerTransport _transport;
    
    private ConcurrentDictionary<uint, GameSession> _activeSessions = new();

    private uint _maxSessionId = 0;

    public GameOrchestrator(IMultiplayerServerTransport transport)
    {
        _transport = transport;
        transport.PacketReceived += TransportOnPacketReceived;
        transport.ClientConnected += TransportOnClientConnected;
        transport.ClientDisconnected += TransportOnClientDisconnected;
        transport.ClientConnecting += TransportOnClientConnecting;

        _lobbyThread = new Thread(LobbyExec) { IsBackground = true };
        _lobbyThread.Start();
    }

    public void Stop()
    {
        _lobbyIsRunning = false;
        _transport.Stop();
    }

    private void LobbyExec()
    {
        while (_lobbyIsRunning)
        {
            UpdateLobbyStates();

            Thread.Sleep(1000);
        }
    }

    private void UpdateLobbyStates()
    {
        foreach (var (id, client) in _connectedClients)
        {
            var packet = GetLobbyState(id);

            _transport.SendPacketToClient(id, packet);
        }
    }

    private S2C_LobbyState GetLobbyState(uint playerClientId)
    {
        var players = new List<S2C_LobbyState.PlayerInfo>();
        var sessions = new List<S2C_LobbyState.GameSession>();
            
        foreach (var (id, client) in _connectedClients)
        {
            players.Add(new S2C_LobbyState.PlayerInfo
            {
                Id = id,
                Name = client.Name,
                Vehicle = client.Vehicle,
                Color = client.Color
            });
        }
            
        foreach (var (id, session) in _activeSessions)
        {
            sessions.Add(GetGameSession(session));
        }

        return new S2C_LobbyState
        {
            PlayerClientId = playerClientId,
            Players = players,
            ActiveSessions = sessions
        };
    }

    private static S2C_LobbyState.GameSession GetGameSession(GameSession session)
    {
        return new S2C_LobbyState.GameSession
        {
            Id = session.Id,
            CreatorId = session.CreatorId,
            CreatorName = session.CreatorName,
            StageName = session.StageName,
            PlayerCount = session.PlayerClientIds.Count,
            MaxPlayers = session.MaxPlayers,
            PlayerClientIds = session.PlayerClientIds,
            State = session.State
        };
    }

    private void TransportOnClientConnecting(object? sender, uint clientIndex)
    {
        _connectedClients.TryAdd(clientIndex, new ClientInfo()
        {
            State = ClientState.Connecting
        });
        
        UpdateLobbyStates();
    }

    private void TransportOnClientDisconnected(object? sender, uint clientIndex)
    {
        if (_connectedClients.TryRemove(clientIndex, out var client))
        {
            if (client.InSession is {} inSession && _activeSessions.TryGetValue(inSession.SessionIndex, out var session))
            {
                session.PlayerClientIds.TryRemove(KeyValuePair.Create(inSession.PlayerIndex, clientIndex));
            }

            BroadcastSystemMessage($"{client.Name} has left...");

            UpdateLobbyStates();
        }
    }

    private void BroadcastSystemMessage(string message)
    {
        _transport.BroadcastPacket(new S2C_LobbyChatMessage
        {
            Message = message,
            Sender = "<System>",
            SenderClientId = uint.MaxValue
        });
    }

    private void TransportOnClientConnected(object? sender, uint clientIndex)
    {
        if (_connectedClients.TryGetValue(clientIndex, out var clientInfo))
        {
            clientInfo.State = ClientState.Connected;
        }
        
        UpdateLobbyStates();
    }

    private void TransportOnPacketReceived(object? sender, (uint ClientIndex, IPacketClientToServer Packet) e)
    {
        switch (e.Packet)
        {
            case C2S_LobbyStartRace startRace:
                if (_activeSessions.TryGetValue(startRace.SessionId, out var session) && 
                    session.PlayerClientIds.Any(e1 => e1.Value == e.ClientIndex))
                {
                    session.State = SessionState.Started;
                    BroadcastSystemMessage($"{session.CreatorName} has started the race on {session.StageName}!");
                    UpdateLobbyStates();
                    
                    _transport.SendPacketToClients(session.PlayerClientIds.Values.ToArray(), new S2C_RaceStarted
                    {
                        Session = GetGameSession(session)
                    });
                }
                break;
            case C2S_PlayerState playerState:
                if (_connectedClients.TryGetValue(e.ClientIndex, out var client) &&
                    client.InSession is {} inSession &&
                    _activeSessions.TryGetValue(inSession.SessionIndex, out session))
                {
                    _transport.SendPacketToClients(session.PlayerClientIds.Values.ToArray(), new S2C_PlayerState()
                    {
                        PlayerClientId = e.ClientIndex,
                        State = playerState.State
                    }, false);
                }
                break;
            case C2S_LobbyChatMessage chatMessage:
                _transport.BroadcastPacket(new S2C_LobbyChatMessage()
                {
                    SenderClientId = e.ClientIndex,
                    Sender = _connectedClients.TryGetValue(e.ClientIndex, out var clientInfo) ? clientInfo.Name : "Unknown",
                    Message = chatMessage.Message
                });
                break;
            case C2S_PlayerIdentity playerIdentity:
                if (_connectedClients.TryGetValue(e.ClientIndex, out clientInfo))
                {
                    clientInfo.Name = playerIdentity.PlayerName;
                    clientInfo.Vehicle = playerIdentity.SelectedVehicle;
                    clientInfo.Color = playerIdentity.Color;
                }
                break;
            case C2S_CreateSession createSession:
                var newSession = new GameSession()
                {
                    Id = ++_maxSessionId,
                    CreatorId = e.ClientIndex,
                    CreatorName = _connectedClients.TryGetValue(e.ClientIndex, out var creatorInfo) ? creatorInfo.Name : "Unknown",
                    StageName = createSession.StageName,
                    MaxPlayers = createSession.MaxPlayers,
                    PlayerClientIds = new ConcurrentDictionary<byte, uint>
                    {
                        [0] = e.ClientIndex
                    }
                };
                _activeSessions.TryAdd(newSession.Id, newSession);
                BroadcastSystemMessage($"{newSession.CreatorName} has started a session for {newSession.StageName}!");
                UpdateLobbyStates();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private class GameSession
    {
        public required uint Id { get; set; }
        public required uint CreatorId { get; set; }
        public required string CreatorName { get; set; }
        public required string StageName { get; set; }
        public int MaxPlayers { get; set; }
        
        public ConcurrentDictionary<byte, uint> PlayerClientIds { get; set; } = [];
        public SessionState State { get; set; } = SessionState.NotStarted;
    }

    private class ClientInfo
    {
        public ClientState State { get; set; }
        public string Name { get; set; } = "hogan rewish";
        public string Vehicle { get; set; } = "nfmm/radicalone";
        public Color3 Color { get; set; } = new Color3();
        public (byte PlayerIndex, uint SessionIndex)? InSession { get; set; }
    }
}