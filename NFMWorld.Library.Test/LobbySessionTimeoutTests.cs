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
}
