using System.Runtime.InteropServices;
using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-2)]
public partial struct S2C_LobbyState : IPacketServerToClient<S2C_LobbyState>
{
    [TightPackOrder(0)] public required Guid ClientId { get; set; }
    [TightPackOrder(1)] public required IList<ServerSidePlayerInfo> Players { get; set; }
    [TightPackOrder(2)] public required IList<GameSession> ActiveSessions { get; set; }

    [StructLayout(LayoutKind.Sequential)]
    [TightPackable]
    public partial struct GameSession
    {
        [TightPackOrder(0)] public required uint Id { get; set; }
        [TightPackOrder(1)] public required Guid CreatorId { get; set; }
        [TightPackOrder(2)] public required string CreatorName { get; set; }
        [TightPackOrder(3)] public required string StageName { get; set; }
        [TightPackOrder(6)] public required int MaxPlayers { get; set; }

        /// <summary>
        /// Key: player car index
        /// Value: client ID
        /// </summary>
        [TightPackOrder(4)]
        public required IDictionary<byte, Guid> Players { get; set; }

        [TightPackOrder(5)] public required SessionState State { get; set; } = SessionState.NotStarted;

        [TightPackIgnore] public int PlayerCount => Players.Count;

        public GameSession()
        {
        }
    }
}
