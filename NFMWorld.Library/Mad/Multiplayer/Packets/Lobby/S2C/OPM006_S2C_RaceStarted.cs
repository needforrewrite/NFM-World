using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[MemoryPackable]
[PacketServerToClient(-6)]
public partial struct S2C_RaceStarted() : IPacketServerToClient<S2C_RaceStarted>
{
    public partial struct GameJoinInfo()
    {
        /// <summary>
        /// ENet UDP IP address and port of the race server.
        /// </summary>
        [MemoryPackOrder(0)]
        public required IpAndPort RaceServerIpAddress { get; set; }

        /// <summary>
        /// Unique 128-bit single-use token to send to the race server to join the race.
        /// </summary>
        [MemoryPackOrder(1)]
        public required Guid JoinToken { get; set; }
    }

    [MemoryPackOrder(0)] public required MatchGameplayInfo MatchGameplayInfo { get; set; }
    [MemoryPackOrder(1)] public required SessionState State { get; set; } = SessionState.NotStarted;
    [MemoryPackOrder(2)] public required GameJoinInfo JoinInfo { get; set; }

    /// <summary>
    /// True when this client was given a way into the race without taking a slot. It sees every
    /// car but sits in none of them, so its race has no client player.
    /// </summary>
    [MemoryPackOrder(3)]
    public bool Spectator { get; set; }
}
