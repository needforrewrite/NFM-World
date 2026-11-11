using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class MultiplayerContentPolicyTests
{
    private static readonly string ArchiveCar = "archive/" + new string('a', 64);

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

    private static ServerSidePlayerInfo Player(string car)
        => new()
        {
            Id = Guid.NewGuid(),
            PlayerName = "racer",
            CarName = car,
            Color = new Color3(255, 0, 0)
        };
}
