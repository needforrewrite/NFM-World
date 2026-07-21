using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Gameplay;

public class LobbyPhase(GraphicsDevice graphicsDevice, IMultiplayerClientTransport transport) : BasePhase
{
    private Player _player = new();
    
    private struct ChatMessage
    {
        public required Guid SenderId { get; set; }
        public required string Sender { get; set; }
        public required string Message { get; set; }
        public Color3 Color { get; set; }
    }

    // Dummy data
    private List<PlayerInfo> _players = [];

    private List<S2C_LobbyState.GameSession> _activeSessions = [];

    private List<ChatMessage> _chatMessages = [];

    private string _chatInput = "";
    private float _sidebarWidth = 250f;
    private float _gameListHeight = 200f;

    private bool _sentClientIdentity = false;
    private bool _showCreateGameDialog = false;

    public override void GameTick()
    {
        base.GameTick();

        if (!_sentClientIdentity && transport.State == ClientState.Connected)
        {
            _sentClientIdentity = true;
            SendUpdatePlayerIdentity();
        }

        foreach (var packet in transport.GetNewPackets())
        {
            switch (packet)
            {
                case S2C_LobbyChatMessage chatMessage:
                {
                    _chatMessages.Add(new ChatMessage 
                    {
                        SenderId = chatMessage.SenderId,
                        Sender = chatMessage.Sender, 
                        Message = chatMessage.Message,
                        Color = _players
                            .Select(e => (PlayerInfo?)e)
                            .FirstOrDefault(p => p!.Value.Id == chatMessage.SenderId, null)
                            ?.Color ?? new Color3(255, 255, 255)
                    });
                    break;
                }
                case S2C_LobbyState lobbyState:
                {
                    _player.Id = lobbyState.ClientId;
                    _players = lobbyState.Players.ToList();
                    _activeSessions = lobbyState.ActiveSessions.ToList();
                    break;
                }
                case S2C_RaceStarted raceStarted:
                {
                    // Create a NEW transport to the Game Master for in-game traffic.
                    // Keep the lobby transport alive — we return to THIS lobby instance after the race.
                    var gameAddr = raceStarted.JoinInfo.RaceServerIpAddress;
                    var ipString = ((System.Net.IPAddress)gameAddr.Address).ToString();
                    var gameTransport = new ENetMultiplayerClientTransport(ipString, gameAddr.Port);

                    var phase = new InMultiplayerRacePhase(
                        graphicsDevice, gameTransport,
                        raceStarted.MatchGameplayInfo, _player.Id,
                        raceStarted.JoinInfo.JoinToken);

                    // Navigation back to lobby is handled automatically by BaseRacePhase
                    // when the race finishes or fails — no manual RaceStateChanged wiring needed.
                    GameSparker.PushPhase(phase);
                    break;
                }
            }
        }
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos);
        ImGui.SetNextWindowSize(viewport.Size);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        var windowFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | 
                         ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                         ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;

        if (ImGui.Begin("Lobby", windowFlags))
        {
            ImGui.PopStyleVar(3);

            var availSize = ImGui.GetContentRegionAvail();
            
            // Left sidebar - Players
            RenderPlayerSidebar();

            ImGui.SameLine();

            // Right side - Game list and chat
            ImGui.BeginGroup();
            {
                var rightSideWidth = availSize.X - _sidebarWidth - ImGui.GetStyle().ItemSpacing.X;
                
                // Top section - Active games
                RenderActiveGames(rightSideWidth);

                // Bottom section - Chat
                RenderChat(rightSideWidth);
            }
            ImGui.EndGroup();
        }
        else
        {
            ImGui.PopStyleVar(3);
        }
        ImGui.End();
        RenderCreateGameDialog();
    }

    private void RenderPlayerSidebar()
    {
        ImGui.BeginChild("PlayerSidebar", new Vector2(_sidebarWidth, 0), (ImGuiChildFlags)1);
        
        ImGui.Text("Players");
        ImGui.Separator();
        
        foreach (var player in _players)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, player.Color);
            ImGui.Text($"� {player.Name}");
            ImGui.PopStyleColor();
            
            ImGui.Indent(20);
            ImGui.TextDisabled($"Vehicle: {BackendGameSparker.GetCar(player.Vehicle).Rad?.Stats.Name}");
            ImGui.Unindent(20);
            ImGui.Spacing();
        }
        
        ImGui.Separator();
        
        if (ImGui.Button("Change Vehicle", new Vector2(-1, 0)))
        {
            ImGui.OpenPopup("VehicleSelection");
        }
        RenderVehicleSelectionDialog();
        
        if (ImGui.Button(_player.IsReady ? "Unready" : "Ready", new Vector2(-1, 0)))
        {
            // TODO: Toggle ready status
        }
        
        if (_activeSessions.FirstOrDefault(e => e.Players.Any(e1 => e1.Value == _player.Id)) is {} session)
        {
            if (ImGui.Button("Start Race", new Vector2(-1, 0)))
            {
                transport.SendPacketToServer(new C2S_LobbyStartRace
                {
                    SessionId = session.Id 
                });
            }
        }

        ImGui.EndChild();
    }
    
    private void RenderVehicleSelectionDialog()
    {
        if (ImGui.BeginPopupModal("VehicleSelection", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Select a car:");
            ImGui.Separator();

            // Dummy vehicle list
            foreach (var vehicle in (Span<Rad3d>)[..BackendGameSparker.cars.Values.SelectMany(i => i)])
            {
                if (ImGui.Selectable(vehicle.Stats.Name + "##" + vehicle.FileName))
                {
                    _player.Vehicle = vehicle.FileName;
                    SendUpdatePlayerIdentity();
                    ImGui.CloseCurrentPopup();
                }
            }

            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void SendUpdatePlayerIdentity()
    {
        transport.SendPacketToServer(new C2S_PlayerIdentity
        {
            PlayerName = _player.Name,
            SelectedVehicle = _player.Vehicle,
            Color = _player.Color
        });
    }

    private void RenderActiveGames(float width)
    {
        ImGui.BeginChild("ActiveGames", new Vector2(width, _gameListHeight), (ImGuiChildFlags)1);
        
        ImGui.Text("Active Games");
        ImGui.Separator();
        
        ImGui.Columns(4, "GamesColumns");
        ImGui.SetColumnWidth(0, width * 0.4f);
        ImGui.SetColumnWidth(1, width * 0.2f);
        ImGui.SetColumnWidth(2, width * 0.2f);
        ImGui.SetColumnWidth(3, width * 0.2f);
        
        // Header
        ImGui.Text("Stage Name");
        ImGui.NextColumn();
        ImGui.Text("Players");
        ImGui.NextColumn();
        ImGui.Text("Status");
        ImGui.NextColumn();
        ImGui.Text("Action");
        ImGui.NextColumn();
        ImGui.Separator();
        
        // Game sessions
        for (int i = 0; i < _activeSessions.Count; i++)
        {
            var session = _activeSessions[i];
            
            ImGui.Text(session.StageName);
            ImGui.NextColumn();
            
            ImGui.Text($"{session.PlayerCount}/{session.MaxPlayers}");
            ImGui.NextColumn();

            if (session.State == SessionState.Started)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 1.0f, 1.0f), "Started");
            }
            else if (session.PlayerCount >= session.MaxPlayers)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Full - Waiting to Start");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Open");
            }
            ImGui.NextColumn();
            
            if (session.PlayerCount >= session.MaxPlayers)
            {
                ImGui.BeginDisabled();
            }

            if (session.State == SessionState.Started)
            {
                if (ImGui.Button($"Spectate##{i}"))
                {
                    // TODO: Spectate game session
                }
            }
            else if (session.Players.Any(e => e.Value == _player.Id))
            {
                if (ImGui.Button($"Leave##{i}"))
                {
                    transport.SendPacketToServer(new C2S_LeaveSession
                    {
                        SessionId = session.Id
                    });
                }
            }
            else
            {
                if (ImGui.Button($"Join##{i}"))
                {
                    transport.SendPacketToServer(new C2S_JoinSession
                    {
                        SessionId = session.Id
                    });
                }
            }
            
            if (session.PlayerCount >= session.MaxPlayers)
            {
                ImGui.EndDisabled();
            }
            
            ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
        ImGui.Separator();
        
        if (ImGui.Button("Create New Game"))
        {
            _showCreateGameDialog = true;
        }
        
        ImGui.SameLine();
        
        if (ImGui.Button("Refresh"))
        {
            // TODO: Refresh game list
        }
        
        ImGui.EndChild();
    }
    
    private int _selectedStage = 0;
    private int _maxPlayers = 4;
    private void RenderCreateGameDialog()
    {
        if (!_showCreateGameDialog) return;

        ImGui.OpenPopup("CreateGameDialog");
        if (ImGui.BeginPopupModal("CreateGameDialog", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Create New Game");
            ImGui.Separator();

            // Dummy stage list
            var items = new List<string>();
            foreach (var stage in GameSparker.GetAvailableStages())
            {
                items.Add($"{stage}##{stage}");
            }
            var stages = items.ToArray();
            ImGui.Combo("Stage", ref _selectedStage, stages, stages.Length);

            ImGui.SliderInt("Max Players", ref _maxPlayers, 1, 127);

            if (ImGui.Button("Create"))
            {
                transport.SendPacketToServer(new C2S_CreateSession()
                {
                    GameMode = DefaultGamemodes.Racing,
                    StageName = stages[_selectedStage].Split("##")[0],
                    MaxPlayers = (byte)_maxPlayers
                });
                _showCreateGameDialog = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                _showCreateGameDialog = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void RenderChat(float width)
    {
        ImGui.BeginChild("Chat", new Vector2(width, 0), (ImGuiChildFlags)1);
        
        ImGui.Text("Chat");
        ImGui.Separator();
        
        // Chat message area
        var chatMessagesHeight = ImGui.GetContentRegionAvail().Y - 60;
        ImGui.BeginChild("ChatMessages", new Vector2(0, chatMessagesHeight), (ImGuiChildFlags)1, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        
        foreach (var msg in _chatMessages)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, msg.Color);
            ImGui.Text($"{msg.Sender}:");
            ImGui.PopStyleColor();
            
            ImGui.SameLine();
            ImGui.TextWrapped(msg.Message);
        }
        
        // Auto-scroll to bottom
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
        {
            ImGui.SetScrollHereY(1.0f);
        }
        
        ImGui.EndChild();
        
        // Chat input
        ImGui.SetNextItemWidth(-80);
        if (ImGui.InputText("##ChatInput", ref _chatInput, 256, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (!string.IsNullOrWhiteSpace(_chatInput))
            {
                SendChatMessage(_chatInput);
                _chatInput = "";
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Send", new Vector2(70, 0)))
        {
            if (!string.IsNullOrWhiteSpace(_chatInput))
            {
                SendChatMessage(_chatInput);
                _chatInput = "";
            }
        }
        
        ImGui.EndChild();
    }

    private void SendChatMessage(string chatInput)
    {
        transport.SendPacketToServer(new C2S_LobbyChatMessage
        {
            Message = chatInput
        });
    }
}

internal class Player
{
    public Guid Id { get; set; }
    public string Name { get; set; } = System.Environment.UserName;
    public string Vehicle { get; set; } = "nfmm/radicalone";
    public Color3 Color { get; set; } = new(0, 128, 255);
    public bool IsReady { get; set; }
    public int? InEventId { get; set; }
}