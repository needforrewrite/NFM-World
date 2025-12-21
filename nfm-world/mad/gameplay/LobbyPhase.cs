using ImGuiNET;
using System.Numerics;

namespace NFMWorld.Mad;

public class LobbyPhase : BasePhase
{
    // Dummy data structures
    private class PlayerInfo
    {
        public required string Name { get; set; }
        public required string Vehicle { get; set; }
        public Vector4 Color { get; set; }
    }

    private class GameSession
    {
        public required string StageName { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
    }

    private class ChatMessage
    {
        public required string PlayerName { get; set; }
        public required string Message { get; set; }
        public Vector4 Color { get; set; }
    }

    // Dummy data
    private List<PlayerInfo> _players = new()
    {
        new PlayerInfo { Name = "Player1", Vehicle = "Speedster", Color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f) },
        new PlayerInfo { Name = "Player2", Vehicle = "Cruiser", Color = new Vector4(0.2f, 0.5f, 1.0f, 1.0f) },
        new PlayerInfo { Name = "Player3", Vehicle = "Rally", Color = new Vector4(0.2f, 1.0f, 0.2f, 1.0f) },
        new PlayerInfo { Name = "Player4", Vehicle = "Not Selected", Color = new Vector4(0.5f, 0.5f, 0.5f, 1.0f) },
    };

    private List<GameSession> _activeSessions = new()
    {
        new GameSession { StageName = "Desert Circuit", PlayerCount = 3, MaxPlayers = 8 },
        new GameSession { StageName = "Mountain Pass", PlayerCount = 1, MaxPlayers = 4 },
        new GameSession { StageName = "City Streets", PlayerCount = 6, MaxPlayers = 8 },
        new GameSession { StageName = "Coastal Highway", PlayerCount = 2, MaxPlayers = 6 },
    };

    private List<ChatMessage> _chatMessages = new()
    {
        new ChatMessage { PlayerName = "Player1", Message = "Hey everyone!", Color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f) },
        new ChatMessage { PlayerName = "Player2", Message = "Ready to race?", Color = new Vector4(0.2f, 0.5f, 1.0f, 1.0f) },
        new ChatMessage { PlayerName = "Player3", Message = "Let's go!", Color = new Vector4(0.2f, 1.0f, 0.2f, 1.0f) },
    };

    private string _chatInput = "";
    private float _sidebarWidth = 250f;
    private float _gameListHeight = 200f;

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
            ImGui.PushStyleColor(ImGuiCol.Text, player.Color);
            ImGui.Text($"• {player.Name}");
            ImGui.PopStyleColor();
            
            ImGui.Indent(20);
            ImGui.TextDisabled($"Vehicle: {player.Vehicle}");
            ImGui.Unindent(20);
            ImGui.Spacing();
        }
        
        ImGui.Separator();
        
        if (ImGui.Button("Change Vehicle", new Vector2(-1, 0)))
        {
            // TODO: Open vehicle selection dialog
        }
        
        if (ImGui.Button("Ready", new Vector2(-1, 0)))
        {
            // TODO: Toggle ready status
        }

        ImGui.EndChild();
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
            ImGui.PushStyleColor(ImGuiCol.Text, msg.Color);
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
                // TODO: Send chat message
                _chatMessages.Add(new ChatMessage 
                { 
                    PlayerName = "You", 
                    Message = _chatInput,
                    Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
                });
                _chatInput = "";
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Send", new Vector2(70, 0)))
        {
            if (!string.IsNullOrWhiteSpace(_chatInput))
            {
                // TODO: Send chat message
                _chatMessages.Add(new ChatMessage 
                { 
                    PlayerName = "You", 
                    Message = _chatInput,
                    Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
                });
                _chatInput = "";
            }
        }
        
        ImGui.EndChild();
    }
}