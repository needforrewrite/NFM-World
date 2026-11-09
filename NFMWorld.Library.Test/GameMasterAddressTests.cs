using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Library.Test;

/// <summary>
/// Where players are told to reach a game master. The lobby reaches it over the compose
/// network or a tunnel; players reach it at a public address, and only the public one is
/// resolved - a name put on the wire would still have to be resolved by the player.
/// </summary>
[TestClass]
public class GameMasterAddressTests
{
    [TestMethod]
    public void ParseAddressMap_SplitsPrivateAndPublicNames()
    {
        var map = GameMasterRegistry.ParseAddressMap(
            "game-master=game-nfmw.puppykitty.racing,lobby-gm=other.example:7005"
        );

        Assert.AreEqual(("game-nfmw.puppykitty.racing", (ushort?)null), map["game-master"]);
        Assert.AreEqual(("other.example", (ushort?)7005), map["lobby-gm"]);
    }

    [TestMethod]
    public void ParseAddressMap_IgnoresMalformedEntries()
    {
        var map = GameMasterRegistry.ParseAddressMap("nonsense,=empty,a=");

        Assert.IsEmpty(map, "an entry without both halves must not invent one");
    }

    [TestMethod]
    public void ParseAddressMap_KeepsAnIpv6LiteralIntact()
    {
        var map = GameMasterRegistry.ParseAddressMap("gm=[2001:db8::1]");

        Assert.AreEqual(
            "[2001:db8::1]",
            map["gm"].Host,
            "only a numeric tail is a port, or an IPv6 address loses its last group"
        );
    }

    /// <summary>
    /// The player-facing address is the public one, and the HTTP endpoint the lobby uses is
    /// still the private one - they go to different places.
    /// </summary>
    [TestMethod]
    public void PublicAddress_IsResolvedEvenWhenTheLobbyUsesAnotherHost()
    {
        var registry = new GameMasterRegistry(
            ["localhost:7002+7003"],
            publicAddresses: GameMasterRegistry.ParseAddressMap("localhost=localhost")
        );

        var gm = registry.SelectGameMaster();

        // Domain is the whole entry: RefreshAll re-resolves from it, so it has to round-trip.
        Assert.AreEqual("localhost:7002+7003", gm.Domain);
        Assert.AreEqual(7003, gm.HttpEndpoint.Port, "the lobby still talks to the private HTTP endpoint");
        Assert.AreEqual(7002, gm.GameAddress.Port);
    }
}
