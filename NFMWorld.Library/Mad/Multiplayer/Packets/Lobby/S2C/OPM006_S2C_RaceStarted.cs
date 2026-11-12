using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-6)]
public partial struct S2C_RaceStarted() : IPacketServerToClient<S2C_RaceStarted>
{
    [TightPackable]
    public partial struct GameJoinInfo()
    {
        /// <summary>
        /// ENet UDP IP address and port of the race server.
        /// </summary>
        [TightPackOrder(0)]
        public required IpAndPort RaceServerIpAddress { get; set; }

        /// <summary>
        /// Unique 128-bit single-use token to send to the race server to join the race.
        /// </summary>
        [TightPackOrder(1)]
        public required Guid JoinToken { get; set; }
    }

    [TightPackOrder(0)] public required MatchGameplayInfo MatchGameplayInfo { get; set; }
    [TightPackOrder(1)] public required SessionState State { get; set; } = SessionState.NotStarted;
    [TightPackOrder(2)] public required GameJoinInfo JoinInfo { get; set; }

    /// <summary>
    /// True when this client was given a way into the race without taking a slot. It sees every
    /// car but sits in none of them, so its race has no client player.
    /// </summary>
    [TightPackOrder(3)]
    public bool Spectator { get; set; }

    /// <summary>
    /// The lobby session this race was started from. The client leaves it when the race ends:
    /// returning over the same socket produces no disconnect, so without this the lobby keeps
    /// the player in a started session and never frees them.
    /// </summary>
    [TightPackOrder(4)]
    public uint SessionId { get; set; }
}
