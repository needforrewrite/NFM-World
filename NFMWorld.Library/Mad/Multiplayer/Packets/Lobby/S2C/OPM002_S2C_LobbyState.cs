using System.Runtime.InteropServices;
using TightPack;
using NFMWorldLibrary.Gamemodes;

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
        /// Value: player ID (human or bot)
        /// </summary>
        [TightPackOrder(4)]
        public required IDictionary<byte, Guid> Players { get; set; }

        [TightPackOrder(5)] public required SessionState State { get; set; } = SessionState.NotStarted;

        /// <summary>The gamemode the host picked, e.g. <c>nfmm/pvp</c>.</summary>
        [TightPackOrder(7)]
        public required string Gamemode { get; set; } = DefaultGamemodes.PvP;

        /// <summary>The host's gamemode options, as declared by the plugin.</summary>
        [TightPackOrder(8)]
        public required IDictionary<string, string> GamemodeParameters { get; set; } =
            new Dictionary<string, string>();

        /// <summary>
        /// Bot slots, keyed by slot. Kept out of <see cref="Players"/> because a bot has no
        /// connection; the match view shows its name and car like a human slot.
        /// </summary>
        [TightPackOrder(9)]
        public required IDictionary<byte, ServerSidePlayerInfo> Bots { get; set; } =
            new Dictionary<byte, ServerSidePlayerInfo>();

        /// <summary>The final order, kept after the race so a match lobby can show placings.</summary>
        [TightPackOrder(10)]
        public required IList<SessionStanding> FinalStandings { get; set; } = [];

        /// <summary>Unix ms at which this finished race is dropped; zero when it is not lingering.</summary>
        [TightPackOrder(11)]
        public long ClosesAtUnixMs { get; set; }

        [TightPackOrder(12)] public bool RequiresInvite { get; set; }

        [TightPackIgnore] public int PlayerCount => Players.Count + Bots.Count;

        public GameSession()
        {
        }
    }
}
