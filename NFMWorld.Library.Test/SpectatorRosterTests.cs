using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorld.Library.Test;

/// <summary>
/// A spectator joins a running race without taking a slot, so nobody in the roster is the
/// client player. The client gamemode used to require exactly one and threw without it.
/// </summary>
[TestClass]
public class SpectatorRosterTests
{
    private static ClientSidePlayerInfo Player(string name, bool isClient) => new()
    {
        PlayerName = name,
        CarName = "nfmm/radicalone",
        Color = default,
        IsBot = false,
        IsClientPlayer = isClient
    };

    [TestMethod]
    public void AClientsIdIsTheClientPlayer()
    {
        var players = new[] { Player("alice", isClient: true), Player("bob", isClient: false) };

        var clientPlayer = BaseClientGamemode.FirstClientPlayer(
            players.Select((p, i) => new ClientSidePlayer(p, i)).ToArray()
        );

        Assert.AreEqual("alice", clientPlayer?.Info.PlayerName);
    }

    /// <summary>The case that used to throw: every slot belongs to someone else.</summary>
    [TestMethod]
    public void ASpectatorHasNoClientPlayer()
    {
        var players = new[] { Player("alice", isClient: false), Player("bob", isClient: false) };

        var clientPlayer = BaseClientGamemode.FirstClientPlayer(
            players.Select((p, i) => new ClientSidePlayer(p, i)).ToArray()
        );

        Assert.IsNull(clientPlayer, "a spectator is in none of the slots");
    }
}
