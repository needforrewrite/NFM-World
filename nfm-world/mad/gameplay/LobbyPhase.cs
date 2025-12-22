using ImGuiNET;
using Microsoft.Xna.Framework;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class LobbyPhase(IMultiplayerClientTransport transport) : BasePhase
{
    private Player _player = new();
    
    private struct ChatMessage
    {
        public required uint PlayerId { get; set; }
        public required string PlayerName { get; set; }
        public required string Message { get; set; }
        public Color3 Color { get; set; }
    }

    // Dummy data
    private List<S2C_LobbyState.PlayerInfo> _players = [];

    private List<S2C_LobbyState.GameSession> _activeSessions = [];

    private List<ChatMessage> _chatMessages = [];

    private string _chatInput = "";
    private float _sidebarWidth = 250f;
    private float _gameListHeight = 200f;

    private bool _sentClientIdentity = false;

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
                    _chatMessages.Add(new ChatMessage 
                    {
                        PlayerId = chatMessage.SenderClientId,
                        PlayerName = chatMessage.Sender, 
                        Message = chatMessage.Message,
                        Color = _players
                            .Select(e => (S2C_LobbyState.PlayerInfo?)e)
                            .FirstOrDefault(p => p!.Value.Id == chatMessage.SenderClientId, null)
                            ?.Color ?? new Color3(255, 255, 255)
                    });
                    break;
                case S2C_LobbyState lobbyState:
                    _player.ClientId = lobbyState.PlayerClientId;
                    _players = lobbyState.Players.ToList();
                    _activeSessions = lobbyState.ActiveSessions.ToList();
                    break;
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
    }

    private void RenderPlayerSidebar()
    {
        ImGui.BeginChild("PlayerSidebar", new Vector2(_sidebarWidth, 0), (ImGuiChildFlags)1);
        
        ImGui.Text("Players");
        ImGui.Separator();
        
        foreach (var player in _players)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, player.Color.ToVector4());
            ImGui.Text($"• {player.Name}");
            ImGui.PopStyleColor();
            
            ImGui.Indent(20);
            ImGui.TextDisabled($"Vehicle: {GameSparker.GetCar(player.Vehicle).Car?.Stats.Name}");
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

        ImGui.EndChild();
    }
    
    private void RenderVehicleSelectionDialog()
    {
        if (ImGui.BeginPopupModal("VehicleSelection", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Select a car:");
            ImGui.Separator();

            // Dummy vehicle list
            foreach (var vehicleArr in (Span<UnlimitedArray<Car>>)[GameSparker.cars, GameSparker.vendor_cars, GameSparker.user_cars])
            foreach (var vehicle in vehicleArr)
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
            
            if (session.PlayerCount >= session.MaxPlayers)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Full");
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
            
            if (ImGui.Button($"Join##{i}"))
            {
                // TODO: Join game session
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
            // TODO: Open create game dialog
        }
        
        ImGui.SameLine();
        
        if (ImGui.Button("Refresh"))
        {
            // TODO: Refresh game list
        }
        
        ImGui.EndChild();
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
            ImGui.PushStyleColor(ImGuiCol.Text, msg.Color.ToVector4());
            ImGui.Text($"{msg.PlayerName}:");
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
    public uint ClientId { get; set; }
    public string Name { get; set; } = System.Environment.UserName;
    public string Vehicle { get; set; } = "nfmm/radicalone";
    public Color3 Color { get; set; } = new(0, 128, 255);
    public bool IsReady { get; set; }
    public int? InEventId { get; set; }
}