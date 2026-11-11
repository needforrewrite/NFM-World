using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Round-trips the packets that carry a gamemode's config.
///
/// These were <c>Dictionary&lt;string, object&gt;</c>, which MemoryPack cannot serialize at all -
/// it has no formatter for <c>object</c>, and resolving the value formatter fails even for an
/// empty dictionary. Creating a session crashed on the client; starting one would have crashed
/// the same way, because <see cref="MatchGameplayInfo"/> is on that wire too.
/// </summary>
[TestClass]
public class LobbyPacketTests
{
    private static T RoundTrip<T>(T packet, sbyte opcode) where T : IReadableWritable<T>, MemoryPack.IMemoryPackable<T>
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        packet.Write(writer);

        // C2S and S2C live in separate opcode spaces, so try the one the packet belongs to.
        object? deserialized = opcode > 0
            ? MultiplayerUtils.TryDeserializeC2SPacket(opcode, writer.WrittenMemory)
            : MultiplayerUtils.TryDeserializeS2CPacket(opcode, writer.WrittenMemory);
        Assert.IsNotNull(deserialized, $"opcode {opcode} did not deserialize");
        return (T)deserialized;
    }

    [TestMethod]
    public void CreateSession_CarriesGamemodeParameters()
    {
        var sent = new C2S_CreateSession
        {
            StageName = "nfmm/road",
            MaxPlayers = 6,
            GameMode = DefaultGamemodes.PvP,
            GameModeParameters = new Dictionary<string, string> { ["constraint"] = "both" }
        };

        var received = RoundTrip(sent, 1);

        Assert.AreEqual("nfmm/road", received.StageName);
        Assert.AreEqual(6, received.MaxPlayers);
        Assert.AreEqual(DefaultGamemodes.PvP, received.GameMode);
        Assert.AreEqual("both", received.GameModeParameters["constraint"]);
    }

    [TestMethod]
    public void CreateSession_SerializesWithNoParameters()
    {
        // The old field crashed on this case too: the value formatter is resolved before the
        // dictionary is even read, so "there are no entries" did not save it.
        var sent = new C2S_CreateSession
        {
            StageName = "nfmm/road",
            MaxPlayers = 4,
            GameMode = DefaultGamemodes.PvP,
            GameModeParameters = []
        };

        Assert.AreEqual(0, RoundTrip(sent, 1).GameModeParameters.Count);
    }

    [TestMethod]
    public void RaceStarted_CarriesMatchInfo()
    {
        var info = new MatchGameplayInfo
        {
            StageName = "nfmm/road",
            Gamemode = DefaultGamemodes.PvP,
            Parameters = new Dictionary<string, string> { ["constraint"] = "racing" },
            Players = new Dictionary<byte, ServerSidePlayerInfo>
            {
                [0] = new()
                {
                    Id = Guid.NewGuid(),
                    PlayerName = "alice",
                    CarName = "nfmm/radicalone",
                    Color = new Color3(1, 2, 3)
                }
            }
        };

        var sent = new S2C_RaceStarted
        {
            MatchGameplayInfo = info,
            State = SessionState.WaitingToLoad,
            JoinInfo = new S2C_RaceStarted.GameJoinInfo
            {
                RaceServerIpAddress = default,
                JoinToken = Guid.NewGuid()
            }
        };

        var received = RoundTrip(sent, -6);

        Assert.AreEqual("nfmm/road", received.MatchGameplayInfo.StageName);
        Assert.AreEqual("racing", received.MatchGameplayInfo.Parameters["constraint"]);
        Assert.AreEqual("alice", received.MatchGameplayInfo.Players[0].PlayerName);
    }

    [TestMethod]
    public void PrepareContent_RoundTripsTheAssetList()
    {
        var content = new[]
        {
            new PreparedContent { Id = "archive/" + new string('a', 64), Kind = 1 },
            new PreparedContent { Id = "archive/" + new string('b', 64), Kind = 0 }
        };

        var sent = new S2C_PrepareContent
        {
            SessionId = 42,
            PreparationId = Guid.NewGuid(),
            Content = content
        };

        var received = RoundTrip(sent, -11);

        Assert.AreEqual(42u, received.SessionId);
        Assert.AreEqual(sent.PreparationId, received.PreparationId);
        Assert.AreEqual(2, received.Content.Length);
        Assert.AreEqual(content[0].Id, received.Content[0].Id);
        Assert.AreEqual((byte)1, received.Content[0].Kind);
    }

    [TestMethod]
    public void ContentReady_RoundTripsTheReason()
    {
        var sent = new C2S_ContentReady
        {
            SessionId = 7,
            PreparationId = Guid.NewGuid(),
            Ready = false,
            Reason = "Could not download the asset"
        };

        var received = RoundTrip(sent, 13);

        Assert.AreEqual(7u, received.SessionId);
        Assert.AreEqual(sent.PreparationId, received.PreparationId);
        Assert.IsFalse(received.Ready);
        Assert.AreEqual("Could not download the asset", received.Reason);
    }

    /// <summary>
    /// The client gamemode indexes players by array position while relayed car state is
    /// addressed by id, so the roster has to be laid out in slot order. Enumerated as-is, a
    /// dictionary built by the lobby put each player in another player's slot and the cars
    /// never moved - and what order a dictionary enumerates in is not something to rely on.
    /// </summary>
    [TestMethod]
    public void InSlotOrder_LaysPlayersOutBySlotNotEnumerationOrder()
    {
        var info = new MatchGameplayInfo
        {
            StageName = "nfmm/road",
            Gamemode = "nfmm/pvp",
            Players = new Dictionary<byte, ServerSidePlayerInfo>
            {
                [2] = Player("carol"),
                [0] = Player("alice"),
                [1] = Player("bob")
            }
        };

        Assert.AreEqual(
            "alice,bob,carol",
            string.Join(",", info.InSlotOrder().Select(p => p.PlayerName))
        );

        static ServerSidePlayerInfo Player(string name) => new()
        {
            Id = Guid.NewGuid(), PlayerName = name, CarName = "nfmm/radicalone", Color = default
        };
    }
}
