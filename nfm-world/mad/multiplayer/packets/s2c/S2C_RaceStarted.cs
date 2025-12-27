using System.Runtime.InteropServices;
using MessagePack;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct S2C_RaceStarted : IPacketServerToClient<S2C_RaceStarted>
{
    [StructLayout(LayoutKind.Sequential)]
    [MessagePackObject]
    public struct GameSession
    {
        [Key(0)] public required string StageName { get; set; }
        
        /// <summary>
        /// Key: player car index
        /// Value: client ID
        /// </summary>
        [Key(1)] public required IDictionary<byte, PlayerInfo> Players { get; set; }
        [Key(2)] public required SessionState State { get; set; } = SessionState.NotStarted;
        [Key(3)] public required GameModes Gamemode { get; set; } = GameModes.Sandbox;
        
        public GameSession()
        {
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    [MessagePackObject]
    public struct PlayerInfo
    {
        [Key(0)] public required uint Id { get; set; }
        [Key(1)] public required string Name { get; set; }
        [Key(2)] public required string Vehicle { get; set; }
        [Key(3)] public required Color3 Color { get; set; }
    }
    
    [Key(0)] public required GameSession Session { get; set; }
}