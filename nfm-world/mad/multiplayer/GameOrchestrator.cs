using System.Collections.Concurrent;

namespace NFMWorld.Mad;

public class GameOrchestrator
{
    private ConcurrentDictionary<uint, ClientInfo> _connectedClients = new();
    private readonly Thread _lobbyThread;
    private bool _lobbyIsRunning = true;
    private readonly IMultiplayerServerTransport _transport;
    
    private ConcurrentDictionary<uint, GameSession> _activeSessions = new();

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
            sessions.Add(new S2C_LobbyState.GameSession
            {
                Id = session.Id,
                CreatorId = session.CreatorId,
                CreatorName = session.CreatorName,
                StageName = session.StageName,
                PlayerCount = session.PlayerCount,
                MaxPlayers = session.MaxPlayers
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
                break;
            case C2S_PlayerState playerState:
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
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
    }

    private class ClientInfo
    {
        public ClientState State { get; set; }
        public string Name { get; set; } = "hogan rewish";
        public string Vehicle { get; set; } = "nfmm/radicalone";
        public Color3 Color { get; set; } = new Color3();
    }
}