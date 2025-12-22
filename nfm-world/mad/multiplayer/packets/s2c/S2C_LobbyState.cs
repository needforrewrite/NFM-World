using System.Buffers;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using MessagePack;
using Microsoft.Xna.Framework;
using NFMWorld.Util;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct S2C_LobbyState : IPacketServerToClient<S2C_LobbyState>
{
    [Key(0)] public required uint PlayerClientId { get; set; }
    [Key(1)] public required IList<PlayerInfo> Players { get; set; }
    [Key(2)] public required IList<GameSession> ActiveSessions { get; set; }
    
    [StructLayout(LayoutKind.Sequential)]
    [MessagePackObject]
    public struct PlayerInfo
    {
        [Key(0)] public required uint Id { get; set; }
        [Key(1)] public required string Name { get; set; }
        [Key(2)] public required string Vehicle { get; set; }
        [Key(3)] public Color3 Color { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    [MessagePackObject]
    public struct GameSession
    {
        [Key(0)] public required uint Id { get; set; }
        [Key(1)] public required uint CreatorId { get; set; }
        [Key(2)] public required string CreatorName { get; set; }
        [Key(3)] public required string StageName { get; set; }
        [Key(4)] public required int PlayerCount { get; set; }
        [Key(5)] public required int MaxPlayers { get; set; }
        [Key(6)] public required IDictionary<byte, uint> PlayerClientIds { get; set; }
        [Key(7)] public required SessionState State { get; set; } = SessionState.NotStarted;
        
        public GameSession()
        {
        }
    }
}