using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorld.Library.Test;

/// <summary>
/// When a session stops waiting to load. Its load deadline was arm-on-start and never cleared,
/// so every race was declared timed out - and its players told it had failed - 20 seconds in,
/// which is exactly when a race is being played rather than started.
/// </summary>
[TestClass]
public class LobbySessionTimeoutTests
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

    [TestMethod]
    public void Bots_ReserveLobbySlotsAndRejectJoinsWhenFull()
    {
        var transport = new StubTransport();
        var players = new PlayerRegistry();
        var creator = players.GetOrAdd(1, ClientState.Connected);
        players.GetOrAdd(2, ClientState.Connected);
        var sessions = new SessionManager(transport, players);
        var session = sessions.CreateSession(1, "nfmm/road", 16, DefaultGamemodes.PvP, bots: 15)!;

        Assert.HasCount(15, session.Bots);
        Assert.HasCount(1, session.Players);
        var snapshot = new LobbyStateBroadcaster(transport, players, sessions).BuildSnapshot(creator.Id);
        Assert.AreEqual(16, snapshot.ActiveSessions.Single().PlayerCount);
        Assert.IsNull(sessions.JoinSession(2, session.Id).Joined);
    }

    [TestMethod]
    public void Humans_JoinOnlyUnreservedSlots_AndStartingPreservesBots()
    {
        var players = new PlayerRegistry();
        players.GetOrAdd(1, ClientState.Connected);
        players.GetOrAdd(2, ClientState.Connected);
        players.GetOrAdd(3, ClientState.Connected);
        var sessions = new SessionManager(new StubTransport(), players);
        var session = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP, bots: 2)!;
        var botIds = session.Bots.Values.Select(bot => bot.Id).ToArray();

        Assert.IsNotNull(sessions.JoinSession(2, session.Id).Joined);
        Assert.IsFalse(session.Players.Keys.Intersect(session.Bots.Keys).Any());
        Assert.IsNull(sessions.JoinSession(3, session.Id).Joined);
        Assert.IsTrue(sessions.StartRace(1, session.Id));
        Assert.AreEqual(4, session.Players.Count + session.Bots.Count);
        CollectionAssert.AreEquivalent(botIds, session.Bots.Values.Select(bot => bot.Id).ToArray());
    }

    [TestMethod]
    public void BotReservation_ClampsToTotalCapacityAndTimeTrialHasNoBots()
    {
        var players = new PlayerRegistry();
        players.GetOrAdd(1, ClientState.Connected);
        players.GetOrAdd(2, ClientState.Connected);
        var sessions = new SessionManager(new StubTransport(), players);
        var session = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP, bots: 15)!;
        Assert.AreEqual(3, session.BotCount);
        Assert.HasCount(3, session.Bots);
        Assert.IsTrue(session.Bots.Keys.All(slot => slot < 4));
        var trial = sessions.CreateSession(2, "nfmm/road", 4, DefaultGamemodes.TimeTrial, bots: 15)!;
        Assert.AreEqual(0, trial.BotCount);
        Assert.IsEmpty(trial.Bots);
    }

    [TestMethod]
    public void CreatorLeavingBeforeStart_HandsReservedBotsToTheStartingHuman()
    {
        var players = new PlayerRegistry();
        players.GetOrAdd(1, ClientState.Connected);
        var remaining = players.GetOrAdd(2, ClientState.Connected);
        var sessions = new SessionManager(new StubTransport(), players);
        var session = sessions.CreateSession(1, "nfmm/road", 4, DefaultGamemodes.PvP, bots: 2)!;
        sessions.JoinSession(2, session.Id);
        sessions.LeaveSession(1, session.Id);
        Assert.IsTrue(sessions.StartRace(2, session.Id));
        Assert.IsTrue(session.Bots.Values.All(bot => bot.OwnerId == remaining.Id));
    }

    private static (SessionManager Sessions, PlayerRegistry Players, uint ClientIndex) CreateSession()
    {
        var players = new PlayerRegistry();
        var creator = players.GetOrAdd(1, ClientState.Connected);
        creator.Name = "creator";

        var sessions = new SessionManager(new StubTransport(), players);
        var session = sessions.CreateSession(1, "nfmm/road", maxPlayers: 4, DefaultGamemodes.PvP)!;

        return (sessions, players, session.Id);
    }

    [TestMethod]
    public void StartedRace_IsNeverDeclaredTimedOut()
    {
        var (sessions, _, sessionId) = CreateSession();

        Assert.IsTrue(sessions.StartRace(1, sessionId));
        Assert.AreEqual(SessionState.Started, sessions.Get(sessionId)!.State);
        Assert.IsNull(
            sessions.Get(sessionId)!.StartTime,
            "the load deadline has to be cleared, or the race is timed out mid-play"
        );

        Assert.IsEmpty(sessions.CheckTimeouts());
    }

    [TestMethod]
    public void SessionWaitingToLoad_StillTimesOut()
    {
        var (sessions, _, sessionId) = CreateSession();

        // A session that never starts is what the timeout is for.
        sessions.Get(sessionId)!.State = SessionState.WaitingToLoad;
        sessions.Get(sessionId)!.StartTime = DateTimeOffset.UtcNow.AddSeconds(-1);

        Assert.HasCount(1, sessions.CheckTimeouts());
    }

    /// <summary>
    /// A timed-out player has to be released, or it stays flagged in-game and can never start
    /// another race. The release looked players up by id through the connection-index overload,
    /// so it silently found nothing; this asserts the flag actually clears.
    /// </summary>
    [TestMethod]
    public void TimedOutSession_ReleasesItsPlayers()
    {
        var (sessions, players, sessionId) = CreateSession();
        var creator = players.Get(1)!;
        creator.IsInGame = true;

        sessions.Get(sessionId)!.State = SessionState.WaitingToLoad;
        sessions.Get(sessionId)!.StartTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        sessions.CheckTimeouts();

        Assert.IsFalse(creator.IsInGame, "a timed-out player must be free to start another race");
        Assert.IsNull(creator.InSession);
    }

    /// <summary>
    /// A race whose players all disconnect can never start, so it must not sit in the list at
    /// 0/16. Aborting marks it finished, which the pruner then removes.
    /// </summary>
    [TestMethod]
    public void AbortedSession_IsPruned()
    {
        var (sessions, _, sessionId) = CreateSession();
        var session = sessions.Get(sessionId)!;
        session.State = SessionState.WaitingToLoad;
        session.Players.Clear();

        sessions.AbortSession(sessionId);
        sessions.PruneFinished();

        Assert.IsNull(sessions.Get(sessionId), "an empty, unfinished race is not a race anyone can join");
    }
}
