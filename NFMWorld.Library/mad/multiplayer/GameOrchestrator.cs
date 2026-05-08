using System.Collections.Concurrent;
using Maxine.Extensions;
using NFMWorldLibrary.Mad.Multiplayer.packets.c2s;
using NFMWorldLibrary.Mad.Multiplayer.packets.s2c;

namespace NFMWorldLibrary.Mad.Multiplayer;

public class GameOrchestrator
{
    private ConcurrentDictionary<uint, ClientInfo> _connectedClients = new();
    private Thread _lobbyThread;
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
    }

    private void LobbyExec()
    {
        while (_lobbyIsRunning)
        {
            UpdateLobbyStates();
            
            foreach (var (id, session) in _activeSessions)
            {
                if (session.State == SessionState.WaitingToLoad &&
                    session.StartTime is {} startTime &&
                    DateTimeOffset.Now >= startTime)
                {
                    session.State = SessionState.Finished;
                    _transport.SendPacketToClients(session.PlayerClientIds.Values.ToArray(), new S2C_RaceFailedToStart(), false);
                    foreach (var (index, clientId) in session.PlayerClientIds)
                    {
                        if (_connectedClients.TryGetValue(clientId, out var client))
                        {
                            client.InSession = null;
                            client.IsInGame = false;
                        }
                    }
                }
            }

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
            sessions.Add(new S2C_LobbyState.GameSession
            {
                Id = session.Id,
                CreatorId = session.CreatorId,
                CreatorName = session.CreatorName,
                StageName = session.StageName,
                MaxPlayers = session.MaxPlayers,
                PlayerClientIds = session.PlayerClientIds,
                State = session.State
            });
        }

        return new S2C_LobbyState
        {
            PlayerClientId = playerClientId,
            Players = players,
            ActiveSessions = sessions
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
            {
                if (_activeSessions.TryGetValue(startRace.SessionId, out var session) && 
                    session.PlayerClientIds.Any(e1 => e1.Value == e.ClientIndex) &&
                    session.State == SessionState.NotStarted)
                {
                    session.State = SessionState.WaitingToLoad;
                    session.StartTime = DateTimeOffset.Now.AddSeconds(20);
                    BroadcastSystemMessage($"{session.CreatorName} has started the race on {session.StageName}!");
                    UpdateLobbyStates();
                    
                    foreach (var (index, id) in session.PlayerClientIds)
                    {
                        _connectedClients[id].IsInGame = true;
                    }
                    
                    _transport.SendPacketToClients(session.PlayerClientIds.Values.ToArray(), new S2C_RaceStarted
                    {
                        Session = new S2C_RaceStarted.GameSession
                        {
                            StageName = session.StageName,
                            State = session.State,
                            Gamemode = session.Gamemode,
                            Players = session.PlayerClientIds.ToDictionary(
                                e1 => e1.Key,
                                e1 => new S2C_RaceStarted.PlayerInfo
                                {
                                    Id = e1.Value,
                                    Name = _connectedClients.TryGetValue(e1.Value, out var ci) ? ci.Name : "Unknown",
                                    Vehicle = _connectedClients.TryGetValue(e1.Value, out ci) ? ci.Vehicle : "nfmm/radicalone",
                                    Color = _connectedClients.TryGetValue(e1.Value, out ci) ? ci.Color : new Color3()
                                })
                        }
                    });
                }
                break;
            }
            case C2S_RaceLoaded raceLoaded:
            {
                if (_connectedClients.TryGetValue(e.ClientIndex, out var client) &&
                    client.InSession is { } inSession &&
                    _activeSessions.TryGetValue(inSession.SessionIndex, out var session) &&
                    session.State == SessionState.WaitingToLoad)
                {
                    var allLoaded = true;
                    foreach (var id in session.PlayerClientIds.Values)
                    {
                        if (!_connectedClients.TryGetValue(id, out var ci) || !ci.IsInGame)
                        {
                            allLoaded = false;
                            break;
                        }
                    }

                    if (allLoaded)
                    {
                        session.State = SessionState.Started;
                        _transport.SendPacketToClients(session.PlayerClientIds.Values.ToArray(), new S2C_RaceCanStart(), false);
                    }
                }

                break;
            }
            case C2S_PlayerState playerState:
            {
                if (_connectedClients.TryGetValue(e.ClientIndex, out var client) &&
                    client.InSession is { } inSession &&
                    _activeSessions.TryGetValue(inSession.SessionIndex, out var session) &&
                    session.State == SessionState.Started)
                {
                    _transport.SendPacketToClients(session.PlayerClientIds.Values.Except(e.ClientIndex).ToArray(), new S2C_PlayerState
                    {
                        PlayerClientId = e.ClientIndex,
                        State = playerState.State,
                        CurrentServerTime = DateTimeOffset.UtcNow
                    }, false);
                }
                break;
            }
            case C2S_LobbyChatMessage chatMessage:
            {
                if (_connectedClients.TryGetValue(e.ClientIndex, out var clientInfo) &&
                    !clientInfo.IsInGame)
                {
                    _transport.BroadcastPacket(new S2C_LobbyChatMessage()
                    {
                        SenderClientId = e.ClientIndex,
                        Sender = clientInfo.Name,
                        Message = chatMessage.Message
                    });
                }
                break;
            }
            case C2S_PlayerIdentity playerIdentity:
            {
                if (_connectedClients.TryGetValue(e.ClientIndex, out var clientInfo))
                {
                    clientInfo.Name = playerIdentity.PlayerName;
                    clientInfo.Vehicle = playerIdentity.SelectedVehicle;
                    clientInfo.Color = playerIdentity.Color;
                }
                break;
            }
            case C2S_CreateSession createSession:
            {
                if (!_connectedClients.TryGetValue(e.ClientIndex, out var connectedClient))
                {
                    break;
                }
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
                connectedClient.InSession = (0, newSession.Id);
                _activeSessions.TryAdd(newSession.Id, newSession);
                BroadcastSystemMessage($"{newSession.CreatorName} has started a session for {newSession.StageName}!");
                UpdateLobbyStates();
                break;
            }
            case C2S_JoinSession joinSession:
            {
                if (!_connectedClients.TryGetValue(e.ClientIndex, out var connectedClient))
                {
                    break;
                }
                if (connectedClient.InSession is { } inSession &&
                    _activeSessions.TryGetValue(inSession.SessionIndex, out var leavingSession))
                {
                    leavingSession.PlayerClientIds.TryRemove(KeyValuePair.Create(inSession.PlayerIndex, e.ClientIndex));
                    connectedClient.InSession = null;
                    BroadcastSystemMessage($"{connectedClient.Name} has left {leavingSession.CreatorName}'s session!");
                }
                
                if (_activeSessions.TryGetValue(joinSession.SessionId, out var sessionInfo))
                {
                    if (sessionInfo.PlayerClientIds.Count < sessionInfo.MaxPlayers)
                    {
                        byte playerIndex = 0;
                        while (sessionInfo.PlayerClientIds.ContainsKey(playerIndex))
                        {
                            playerIndex++;
                        }

                        sessionInfo.PlayerClientIds[playerIndex] = e.ClientIndex;
                        _connectedClients[e.ClientIndex].InSession = (playerIndex, sessionInfo.Id);
                        BroadcastSystemMessage($"{_connectedClients[e.ClientIndex].Name} has joined {sessionInfo.CreatorName}'s session!");
                    }
                }

                UpdateLobbyStates();

                break;
            }
            case C2S_LeaveSession leaveSession:
            {
                if (_connectedClients.TryGetValue(e.ClientIndex, out var leavingClient) &&
                    leavingClient.InSession is { } leaveInSession &&
                    _activeSessions.TryGetValue(leaveInSession.SessionIndex, out var leavingSession) &&
                    leaveSession.SessionId == leavingSession.Id)
                {
                    leavingSession.PlayerClientIds.TryRemove(KeyValuePair.Create(leaveInSession.PlayerIndex, e.ClientIndex));
                    leavingClient.InSession = null;
                    BroadcastSystemMessage($"{leavingClient.Name} has left {leavingSession.CreatorName}'s session!");
                    UpdateLobbyStates();
                }
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException();
            }
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
        public DateTimeOffset? StartTime { get; set; }
        public SessionState State { get; set; } = SessionState.NotStarted;
        public GameModes Gamemode { get; set; } = GameModes.Sandbox;
    }

    private class ClientInfo
    {
        public ClientState State { get; set; }
        public string Name { get; set; } = "hogan rewish";
        public string Vehicle { get; set; } = "nfmm/radicalone";
        public Color3 Color { get; set; } = new Color3();
        public (byte PlayerIndex, uint SessionIndex)? InSession { get; set; }
        public bool IsInGame { get; set; }
    }
}