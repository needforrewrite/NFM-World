using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Plugins;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Filling a session's free slots with bots, and reading a gamemode's config schema for the
/// create dialog. Both were new ground: the dialog hardcoded its only option, and nothing ever
/// created a bot in a lobby.
/// </summary>
[TestClass]
public class LobbyCreateTests
{
    private const string AnyCar = "nfmm/radicalone";

    private static ServerSidePlayerInfo Human(string name) => new()
    {
        Id = Guid.NewGuid(), PlayerName = name, CarName = AnyCar, Color = default
    };

    /// <summary>
    /// Bots take the slots humans did not, at race start rather than at creation, so a human
    /// who joins while the lobby is still open takes one of those slots instead.
    /// </summary>
    [TestMethod]
    public void PlaceBots_FillsOnlyTheSlotsHumansDidNotTake()
    {
        var creator = Human("creator");

        var bots = SessionBots.Place([0, 1], count: 3, ownerId: creator.Id);

        CollectionAssert.AreEquivalent(
            new byte[] { 2, 3, 4 },
            bots.Keys.ToArray(),
            "bots fill the first free slots and never displace a human"
        );
        Assert.IsTrue(bots.Values.All(b => b.IsBot));
        Assert.IsTrue(
            bots.Values.All(b => b.OwnerId == creator.Id),
            "the client that fielded them simulates them"
        );
    }

    [TestMethod]
    public void PlaceBots_StopsAtTheSlotLimit()
    {
        var bots = SessionBots.Place([], count: 100, ownerId: Guid.NewGuid());

        Assert.HasCount(MatchGameplayInfo.MaxPlayers, bots);
    }

    /// <summary>
    /// The create dialog renders whatever a gamemode declares. It used to hardcode
    /// <c>constraint=both</c>, so a plugin's own options could never be chosen.
    /// </summary>
    [TestMethod]
    public void GamemodeCatalog_ReadsThePvpConfigOptions()
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

        var properties = GamemodeCatalog.ConfigProperties(DefaultGamemodes.PvP);

        var constraint = properties.Single(p => p.Name == "constraint");
        CollectionAssert.AreEquivalent(
            new[] { "racing", "wasting", "both" },
            constraint.Options.Select(o => o.Value).ToArray()
        );
    }

    [TestMethod]
    public void GamemodeCatalog_MissingPluginYieldsNoOptions()
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

        Assert.IsEmpty(GamemodeCatalog.ConfigProperties("nfmm/does-not-exist"));
    }

    /// <summary>
    /// The field size and the bot count are separate on the wire: "max players" is how many
    /// humans may join, and the bots fill whatever is left at the start.
    /// </summary>
    [TestMethod]
    public void CreateSession_RoundTripsTheBotCount()
    {
        var sent = new C2S_CreateSession
        {
            StageName = "nfmm/road",
            MaxPlayers = 4,
            GameMode = DefaultGamemodes.PvP,
            GameModeParameters = new() { ["constraint"] = "wasting" },
            Bots = 2
        };

        using var buffer = new ArrayPoolBufferWriter<byte>();
        ((IReadableWritable<C2S_CreateSession>)sent).Write(buffer);
        var received = (C2S_CreateSession)MultiplayerUtils.TryDeserializeC2SPacket(1, buffer.WrittenMemory)!;

        Assert.AreEqual(4, received.MaxPlayers);
        Assert.AreEqual(2, received.Bots);
        Assert.AreEqual("wasting", received.GameModeParameters["constraint"]);
    }
}
