using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class MultiplayerContentPolicyTests
{
    private static readonly string ArchiveCar = "archive/" + new string('a', 64);
    private static readonly string ArchiveOtherCar = "archive/" + new string('c', 64);
    private static readonly string ArchiveStage = "archive/" + new string('d', 64);

    [TestMethod]
    [DataRow("nfmm/2000tornados")]
    [DataRow("world/icytower")]
    [DataRow("elo/whatever")]
    public void ShippedCars_AreAccepted(string id)
        => Assert.IsTrue(MultiplayerContentPolicy.IsAcceptedCar(id));

    [TestMethod]
    public void CanonicalArchiveCar_IsAccepted()
        => Assert.IsTrue(MultiplayerContentPolicy.IsAcceptedCar(ArchiveCar));

    [TestMethod]
    [DataRow("user/mycar")]
    [DataRow("mycar")]
    [DataRow("content/me/car")]
    [DataRow("https://example.com/car.radpack")]
    [DataRow(@"C:\cars\mine.rad")]
    [DataRow("../nfmm/2000tornados")]
    [DataRow("nfmm/../user/mine")]
    [DataRow("nfmm/2000tornados.rad")]
    [DataRow("archive/not-a-hash")]
    public void LocalCustomAndUnsafeIds_AreRefused(string id)
        => Assert.IsFalse(MultiplayerContentPolicy.IsAcceptedCar(id));

    [TestMethod]
    public void CoerceCar_FallsBackToAStockCar()
        => Assert.AreEqual(MultiplayerContentPolicy.DefaultCar, MultiplayerContentPolicy.CoerceCar("user/mine"));

    [TestMethod]
    public void Stages_RejectTheUserFolder()
    {
        Assert.IsTrue(MultiplayerContentPolicy.IsAcceptedStage("nfm2/1_introductory"));
        Assert.IsFalse(MultiplayerContentPolicy.IsAcceptedStage("user/my_stage"));
        Assert.IsTrue(MultiplayerContentPolicy.IsAcceptedStage("archive/" + new string('b', 64)));
    }

    [TestMethod]
    public void ValidateMatch_RefusesACustomCarAnywhereInTheRoster()
    {
        var info = new MatchGameplayInfo
        {
            StageName = "nfm2/1_introductory",
            Gamemode = DefaultGamemodes.PvP,
            Players = new Dictionary<byte, ServerSidePlayerInfo>
            {
                [0] = Player("nfmm/radicalone"),
                [1] = Player("user/mine")
            }
        };

        Assert.IsNotNull(MultiplayerContentPolicy.ValidateMatch(info));
    }

    [TestMethod]
    public void ValidateMatch_AcceptsStockAndArchiveContent()
    {
        var info = new MatchGameplayInfo
        {
            StageName = "nfm2/1_introductory",
            Gamemode = DefaultGamemodes.PvP,
            Players = new Dictionary<byte, ServerSidePlayerInfo>
            {
                [0] = Player("nfmm/radicalone"),
                [1] = Player(ArchiveCar)
            }
        };

        Assert.IsNull(MultiplayerContentPolicy.ValidateMatch(info));
    }

    [TestMethod]
    public void RequiredArchiveContent_CoversTheStageAndDistinctCars()
    {
        var info = new MatchGameplayInfo
        {
            StageName = ArchiveStage,
            Gamemode = DefaultGamemodes.PvP,
            Players = new Dictionary<byte, ServerSidePlayerInfo>
            {
                [0] = Player(ArchiveCar),
                [1] = Player(ArchiveCar),
                [2] = Player("nfmm/radicalone"),
                [3] = Player(ArchiveOtherCar)
            }
        };

        var required = MultiplayerContentPolicy.RequiredArchiveContent(info);

        Assert.AreEqual(3, required.Length);
        Assert.AreEqual(ArchiveStage, required[0].Id);
        Assert.AreEqual((byte)AssetKind.Stage, required[0].Kind);
        CollectionAssert.AreEquivalent(
            new[] { ArchiveCar, ArchiveOtherCar },
            required.Skip(1).Select(item => item.Id).ToArray()
        );
        Assert.IsTrue(required.Skip(1).All(item => item.Kind == (byte)AssetKind.Car));
    }

    [TestMethod]
    public void RequiredArchiveContent_IsEmptyForShippedContent()
    {
        var info = new MatchGameplayInfo
        {
            StageName = "nfm2/1_introductory",
            Gamemode = DefaultGamemodes.PvP,
            Players = new Dictionary<byte, ServerSidePlayerInfo> { [0] = Player("nfmm/radicalone") }
        };

        Assert.AreEqual(0, MultiplayerContentPolicy.RequiredArchiveContent(info).Length);
    }

    private static ServerSidePlayerInfo Player(string car)
        => new()
        {
            Id = Guid.NewGuid(),
            PlayerName = "racer",
            CarName = car,
            Color = new Color3(255, 0, 0)
        };
}
