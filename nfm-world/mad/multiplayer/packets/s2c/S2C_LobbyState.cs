using System.Buffers;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using Microsoft.Xna.Framework;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct S2C_LobbyState : IPacketServerToClient<S2C_LobbyState>
{
    public required uint PlayerClientId { get; set; }
    public required IList<PlayerInfo> Players { get; set; }
    public required IList<GameSession> ActiveSessions { get; set; }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct PlayerInfo
    {
        public required uint Id { get; set; }
        public required string Name { get; set; }
        public required string Vehicle { get; set; }
        public Color3 Color { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GameSession
    {
        public required uint Id { get; set; }
        public required uint CreatorId { get; set; }
        public required string CreatorName { get; set; }
        public required string StageName { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
    }
    
    public void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        writer.Write(PlayerClientId);
        writer.Write(Players.Count);
        foreach (var player in Players)
        {
            writer.Write(player.Id);
            writer.WriteString(player.Name);
            writer.WriteString(player.Vehicle);
            writer.Write(player.Color);
        }

        writer.Write(ActiveSessions.Count);
        foreach (var session in ActiveSessions)
        {
            writer.Write(session.Id);
            writer.Write(session.CreatorId);
            writer.WriteString(session.CreatorName);
            writer.WriteString(session.StageName);
            writer.Write(session.PlayerCount);
            writer.Write(session.MaxPlayers);
        }
    }

    public static S2C_LobbyState Read(SpanReader data)
    {
        var playerClientId = data.ReadUInt32();
        
        var playerCount = data.ReadInt32();
        var players = new PlayerInfo[playerCount];
        for (var i = 0; i < playerCount; i++)
        {
            players[i] = new PlayerInfo
            {
                Id = data.ReadUInt32(),
                Name = data.ReadString(),
                Vehicle = data.ReadString(),
                Color = data.ReadMemory<Color3>()
            };
        }

        var sessionCount = data.ReadInt32();
        var sessions = new GameSession[sessionCount];
        for (var i = 0; i < sessionCount; i++)
        {
            sessions[i] = new GameSession
            {
                Id = data.ReadUInt32(),
                CreatorId = data.ReadUInt32(),
                CreatorName = data.ReadString(),
                StageName = data.ReadString(),
                PlayerCount = data.ReadInt32(),
                MaxPlayers = data.ReadInt32(),
            };
        }

        return new S2C_LobbyState
        {
            PlayerClientId = playerClientId,
            Players = players,
            ActiveSessions = sessions
        };
    }
}