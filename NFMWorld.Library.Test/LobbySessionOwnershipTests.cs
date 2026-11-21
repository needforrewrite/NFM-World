using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorld.Library.Test;

/// <summary>
/// Who owns a session when its creator goes away. Only the creator can start a race and the
/// client hides START from everyone else, so a session that keeps a departed creator is a dead
/// end: it stays listed, joins succeed, and nothing can ever start it.
/// </summary>
[TestClass]
public class LobbySessionOwnershipTests
{
    private sealed class StubTransport : IMultiplayerServerTransport
    {
        public IReadOnlyCollection<uint> Connections => [];
        public event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
        public event EventHandler<uint>? ClientConnecting;
        public event EventHandler<uint>? ClientConnected;
        public event EventHandler<uint>? ClientDisconnected;

        public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true)
            where T : IPacketServerToClient<T>
        {
        }

        public void DisconnectClient(uint clientIndex)
        {
        }

        public void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true)
            where T : IPacketServerToClient<T>
        {
        }

        public void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private static (SessionManager Sessions, PlayerRegistry Players, PlayerRegistry.ClientInfo Creator) Create()
    {
        var players = new PlayerRegistry();
        var creator = players.GetOrAdd(1, ClientState.Connected);
        creator.Name = "creator";
        var sessions = new SessionManager(new StubTransport(), players);
        return (sessions, players, creator);
    }

    /// <summary>
    /// Closing the game while in the lobby. A solo session is over; a session with friends in
    /// it goes to the next player, who can then start the race.
    /// </summary>
    [TestMethod]
    public void CreatorDisconnecting_AloneEndsTheSession_WithOthersHandsItOver()
    {
        var (sessions, players, creator) = Create();
        var alone = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP)!;

        sessions.RemovePlayer(creator);

        Assert.AreEqual(SessionState.Finished, alone.State, "a session with nobody in it is not a race");
        sessions.PruneFinished();
        Assert.IsNull(sessions.Get(alone.Id));

        var remaining = players.GetOrAdd(1, ClientState.Connected);
        remaining.Name = "remaining";
        var friend = players.GetOrAdd(2, ClientState.Connected);
        friend.Name = "friend";
        var shared = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP, bots: 2)!;
        Assert.IsNotNull(sessions.JoinSession(2, shared.Id).Joined);

        sessions.RemovePlayer(remaining);

        Assert.AreEqual(SessionState.NotStarted, shared.State, "the race is still there for its players");
        Assert.AreEqual(friend.Id, shared.CreatorId, "whoever is left owns it, or nobody can start it");
        Assert.AreEqual("friend", shared.CreatorName);
        Assert.IsTrue(shared.Bots.Values.All(bot => bot.OwnerId == friend.Id), "bots are simulated by their owner");
        Assert.IsTrue(sessions.StartRace(2, shared.Id), "the remaining player has to be able to start");
    }

    [TestMethod]
    public void OnlyTheCreatorCanChangeGameRulesBeforeStarting()
    {
        var (sessions, players, creator) = Create();
        var guest = players.GetOrAdd(2, ClientState.Connected);
        var session = sessions.CreateSession(
            creator.ClientIndex,
            "nfmm/road",
            4,
            DefaultGamemodes.PvP,
            new Dictionary<string, string> { ["constraint"] = "both", ["walls"] = "off" }
        )!;
        Assert.IsNotNull(sessions.JoinSession(guest.ClientIndex, session.Id).Joined);

        Assert.IsFalse(sessions.SetGameRule(guest.ClientIndex, session.Id, "constraint", "wasting"));
        Assert.IsFalse(sessions.SetGameRule(creator.ClientIndex, session.Id, "unknown", "on"));
        Assert.IsTrue(sessions.SetGameRule(creator.ClientIndex, session.Id, "constraint", "racing"));
        Assert.AreEqual("racing", session.GamemodeParameters["constraint"]);
        Assert.AreEqual("off", session.GamemodeParameters["walls"]);
        Assert.AreEqual(2, session.Players.Count);

        Assert.IsTrue(sessions.StartRace(creator.ClientIndex, session.Id));
        Assert.IsFalse(sessions.SetGameRule(creator.ClientIndex, session.Id, "constraint", "wasting"));
        Assert.AreEqual("racing", session.GamemodeParameters["constraint"]);
    }

    [TestMethod]
    public void RemovedAccountNeedsAValidInviteEvenAfterReconnecting()
    {
        var (sessions, players, creator) = Create();
        var guest = players.GetOrAdd(2, ClientState.Connected);
        guest.AccountId = 42;
        guest.IsAuthenticated = true;
        var game = sessions.CreateSession(creator.ClientIndex, "nfmm/road", 4, DefaultGamemodes.PvP)!;
        Assert.IsNotNull(sessions.JoinSession(guest.ClientIndex, game.Id).Joined);
        Assert.IsFalse(sessions.KickGamePlayer(guest.ClientIndex, game.Id, guest.Id));
        Assert.IsFalse(sessions.KickGamePlayer(creator.ClientIndex, game.Id, creator.Id));
        Assert.IsTrue(sessions.KickGamePlayer(creator.ClientIndex, game.Id, guest.Id));
        Assert.IsNull(guest.InSession);

        Assert.IsTrue(players.TryRemove(guest.ClientIndex, out _));
        var reconnected = players.GetOrAdd(3, ClientState.Connected);
        reconnected.AccountId = guest.AccountId;
        reconnected.IsAuthenticated = true;
        var otherGame = sessions.CreateSession(reconnected.ClientIndex, "nfmm/other", 4, DefaultGamemodes.PvP)!;
        var broadcaster = new LobbyStateBroadcaster(new StubTransport(), players, sessions);
        Assert.IsTrue(
            broadcaster.BuildSnapshot(reconnected.Id).ActiveSessions.Single(session => session.Id == game.Id).RequiresInvite
        );
        Assert.IsNull(sessions.JoinSession(reconnected.ClientIndex, game.Id).Joined);
        Assert.AreEqual(otherGame.Id, reconnected.InSession!.Value.SessionIndex);

        var invitation = sessions.InviteGamePlayer(creator.ClientIndex, game.Id, reconnected.Id);
        Assert.IsNotNull(invitation);
        Assert.IsNull(sessions.JoinSession(reconnected.ClientIndex, game.Id).Joined);
        var otherPlayer = players.GetOrAdd(4, ClientState.Connected);
        otherPlayer.AccountId = 43;
        Assert.IsNull(sessions.JoinSession(otherPlayer.ClientIndex, game.Id, invitation.Value.Id).Joined);

        Assert.AreSame(game, sessions.JoinSession(reconnected.ClientIndex, game.Id, invitation.Value.Id).Joined);
        Assert.IsFalse(
            broadcaster.BuildSnapshot(reconnected.Id).ActiveSessions.Single(session => session.Id == game.Id).RequiresInvite
        );
        sessions.LeaveSession(reconnected.ClientIndex, game.Id);
        Assert.IsNull(sessions.JoinSession(reconnected.ClientIndex, game.Id, invitation.Value.Id).Joined);
        Assert.AreSame(game, sessions.JoinSession(reconnected.ClientIndex, game.Id).Joined);
    }

    [TestMethod]
    public void CreatorCanRemoveBotsAndInviteOnlyWhileGameIsOpen()
    {
        var (sessions, players, creator) = Create();
        var guest = players.GetOrAdd(2, ClientState.Connected);
        guest.AccountId = 42;
        guest.IsAuthenticated = true;
        var game = sessions.CreateSession(creator.ClientIndex, "nfmm/road", 2, DefaultGamemodes.PvP, bots: 1)!;
        var bot = game.Bots.Values.Single();
        Assert.IsNull(sessions.InviteGamePlayer(creator.ClientIndex, game.Id, guest.Id));
        Assert.IsFalse(sessions.KickGamePlayer(guest.ClientIndex, game.Id, bot.Id));
        Assert.IsTrue(sessions.KickGamePlayer(creator.ClientIndex, game.Id, bot.Id));
        Assert.IsEmpty(game.Bots);
        Assert.AreEqual(0, game.BotCount);

        var invitation = sessions.InviteGamePlayer(creator.ClientIndex, game.Id, guest.Id);
        Assert.IsNotNull(invitation);
        var otherPlayer = players.GetOrAdd(3, ClientState.Connected);
        Assert.AreSame(game, sessions.JoinSession(otherPlayer.ClientIndex, game.Id).Joined);
        Assert.IsNull(sessions.JoinSession(guest.ClientIndex, game.Id, invitation.Value.Id).Joined);
        sessions.LeaveSession(otherPlayer.ClientIndex, game.Id);

        Assert.IsTrue(sessions.StartRace(creator.ClientIndex, game.Id));
        Assert.IsNull(sessions.InviteGamePlayer(creator.ClientIndex, game.Id, guest.Id));
        Assert.IsNull(sessions.JoinSession(guest.ClientIndex, game.Id, invitation.Value.Id).Joined);
        Assert.IsFalse(sessions.KickGamePlayer(creator.ClientIndex, game.Id, guest.Id));
    }

    /// <summary>
    /// Creating a second game without leaving the first. The first kept the creator's slot and
    /// their id, so after a restart it was still listed and joinable while nobody could start it.
    /// </summary>
    [TestMethod]
    public void CreatingASecondSession_EndsTheOneBeingLeft()
    {
        var (sessions, _, creator) = Create();
        var first = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP)!;

        var second = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP)!;

        Assert.AreNotEqual(first.Id, second.Id);
        Assert.IsEmpty(first.Players, "the creator left the first session when the second was created");
        Assert.AreEqual(SessionState.Finished, first.State);
        Assert.AreSame(second, sessions.Get((byte)creator.InSession!.Value.SessionIndex));

        sessions.PruneFinished();
        Assert.IsNull(sessions.Get(first.Id));
        Assert.IsNotNull(sessions.Get(second.Id));
    }

    /// <summary>
    /// A finished race is still worth looking at: it stays listed until its close deadline,
    /// then the pruner drops it.
    /// </summary>
    [TestMethod]
    public void FinishedSession_LingersUntilItsCloseDeadline()
    {
        var (sessions, _, _) = Create();
        var session = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP)!;
        session.State = SessionState.Finished;
        session.ClosesAtUnixMs = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds();

        sessions.PruneFinished();
        Assert.IsNotNull(sessions.Get(session.Id), "a finished race with placings must stay viewable");

        session.ClosesAtUnixMs = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeMilliseconds();
        sessions.PruneFinished();
        Assert.IsNull(sessions.Get(session.Id), "an expired finished race is dropped");
    }

    /// <summary>
    /// A repeated join for the session a player is already in. Leaving first and re-joining ends
    /// a session only they were in, so the repeat would destroy the session it just named.
    /// </summary>
    [TestMethod]
    public void RejoiningOwnSession_KeepsIt()
    {
        var (sessions, _, _) = Create();
        var session = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP)!;

        var (joined, left) = sessions.JoinSession(1, session.Id);

        Assert.AreSame(session, joined);
        Assert.IsNull(left);
        Assert.AreEqual(SessionState.NotStarted, session.State);
        Assert.IsTrue(sessions.StartRace(1, session.Id));
    }
}
