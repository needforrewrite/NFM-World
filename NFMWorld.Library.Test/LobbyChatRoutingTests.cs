using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Covers room chat, match channels before and during a race, and the lobby's match snapshots.
/// </summary>
[TestClass]
public class LobbyChatRoutingTests
{
    private sealed class RecordingTransport : IMultiplayerServerTransport
    {
        public List<(uint ClientIndex, S2C_LobbyChatMessage Message)> Chat { get; } = [];
        public IReadOnlyCollection<uint> Connections => [];
        public event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
        public event EventHandler<uint>? ClientConnecting;
        public event EventHandler<uint>? ClientConnected;
        public event EventHandler<uint>? ClientDisconnected;

        public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true)
            where T : IPacketServerToClient<T>
        {
            if (packet is S2C_LobbyChatMessage chat) Chat.Add((clientIndex, chat));
        }

        public void SendPacketToClients<T>(
            ReadOnlySpan<uint> clientIndices,
            T packet,
            bool reliable = true
        ) where T : IPacketServerToClient<T>
        {
            if (packet is not S2C_LobbyChatMessage chat) return;
            foreach (var index in clientIndices)
                Chat.Add((index, chat));
        }

        public void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>
        {
        }

        public void DisconnectClient(uint clientIndex)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private static (PlayerRegistry Players, RecordingTransport Transport, ChatManager Chat) Setup()
    {
        var players = new PlayerRegistry();
        var transport = new RecordingTransport();
        return (players, transport, new ChatManager(transport, players));
    }

    private static PlayerRegistry.ClientInfo Join(
        PlayerRegistry players,
        uint index,
        string name,
        uint? session = null
    )
    {
        var player = players.GetOrAdd(index);
        player.IsAuthenticated = true;
        player.AccountId = (int)index + 1;
        player.Name = name;
        player.Color = new Color3(10, 20, 30);
        player.InSession = session is { } id ? ((byte)0, id) : null;
        return player;
    }

    [TestMethod]
    public void RoomChat_ReachesEveryAuthenticatedPlayer()
    {
        var (players, transport, chat) = Setup();
        var alice = Join(players, 1, "alice");
        var bob = Join(players, 2, "bob", 10);
        var carol = Join(players, 3, "carol");
        var guest = players.GetOrAdd(4);

        chat.SendChatMessage(alice.Id, "hello room", 0);

        CollectionAssert.AreEquivalent(
            new[] { alice.ClientIndex, bob.ClientIndex, carol.ClientIndex },
            transport.Chat.Select(entry => entry.ClientIndex).ToArray()
        );
        Assert.AreEqual(0u, transport.Chat[0].Message.SessionId);
        Assert.AreEqual("hello room", transport.Chat[0].Message.Message);
        Assert.AreEqual(alice.Color, transport.Chat[0].Message.Color);
    }

    [TestMethod]
    public void MatchChat_ReachesOnlyThatGamesMembers()
    {
        var (players, transport, chat) = Setup();
        var alice = Join(players, 1, "alice", 10);
        Join(players, 2, "bob", 11);
        Join(players, 3, "carol");

        chat.SendChatMessage(alice.Id, "we are waiting", 10);

        Assert.AreEqual(1, transport.Chat.Count);
        Assert.AreEqual(alice.ClientIndex, transport.Chat[0].ClientIndex);
        Assert.AreEqual(10u, transport.Chat[0].Message.SessionId);
    }

    [TestMethod]
    public void MatchChat_RefusesASessionTheSenderIsNotIn()
    {
        var (players, transport, chat) = Setup();
        var alice = Join(players, 1, "alice", 10);

        chat.SendChatMessage(alice.Id, "not mine", 11);

        Assert.AreEqual(0, transport.Chat.Count);
    }

    [TestMethod]
    public void RunningMatchChat_AcceptsViewersAndKeepsTheMatchChannel()
    {
        var (players, transport, chat) = Setup();
        var racer = Join(players, 1, "racer", 10);
        var viewer = Join(players, 2, "viewer");
        var memberOfAnotherGame = Join(players, 3, "another viewer", 11);
        players.GetOrAdd(4);

        chat.SendChatMessage(viewer.Id, "nice recovery", 10, openToLobby: true);

        CollectionAssert.AreEquivalent(
            new[] { racer.ClientIndex, viewer.ClientIndex, memberOfAnotherGame.ClientIndex },
            transport.Chat.Select(entry => entry.ClientIndex).ToArray()
        );
        Assert.IsTrue(transport.Chat.All(entry => entry.Message.SessionId == 10
                                                  && entry.Message.SenderId == viewer.Id && entry.Message.Message
                                                  == "nice recovery"
        )
        );
    }

    [TestMethod]
    public void ForwardedRaceChat_KeepsItsSenderNameAndColour()
    {
        var (players, transport, chat) = Setup();
        var alice = Join(players, 1, "alice", 10);
        var viewer = Join(players, 2, "bob");

        chat.SendToSession(
            10,
            new S2C_LobbyChatMessage
            {
                SenderId = Guid.NewGuid(),
                Sender = "(SPEC) watcher",
                Message = "nice jump",
                Color = new Color3(200, 60, 60)
            },
            openToLobby: true
        );

        CollectionAssert.AreEquivalent(
            new[] { alice.ClientIndex, viewer.ClientIndex },
            transport.Chat.Select(entry => entry.ClientIndex).ToArray()
        );
        Assert.AreEqual(10u, transport.Chat[0].Message.SessionId);
        Assert.AreEqual("(SPEC) watcher", transport.Chat[0].Message.Sender);
        Assert.AreEqual(new Color3(200, 60, 60), transport.Chat[0].Message.Color);
    }

    [TestMethod]
    public void SessionSystemLine_ReachesOnlyThatGamesMembers()
    {
        var (players, transport, chat) = Setup();
        var alice = Join(players, 1, "alice", 10);
        Join(players, 2, "bob", 11);

        chat.SendSystemToSession(10, "alice joined the game.");

        Assert.AreEqual(1, transport.Chat.Count);
        Assert.AreEqual(alice.ClientIndex, transport.Chat[0].ClientIndex);
        Assert.AreEqual(10u, transport.Chat[0].Message.SessionId);
        Assert.AreEqual(Guid.Empty, transport.Chat[0].Message.SenderId);
        Assert.AreEqual("alice joined the game.", transport.Chat[0].Message.Message);
    }

    [TestMethod]
    public void Snapshot_CarriesSessionOptionsAndBotSlots()
    {
        var players = new PlayerRegistry();
        var transport = new RecordingTransport();
        var sessions = new SessionManager(transport, players);
        var creator = Join(players, 1, "alice");

        var session = sessions.CreateSession(
            creator.ClientIndex,
            "nfmm/road",
            4,
            DefaultGamemodes.PvP,
            new Dictionary<string, string> { ["constraint"] = "both", ["walls"] = "3" },
            bots: 2
        );
        Assert.IsNotNull(session);

        var snapshot = new LobbyStateBroadcaster(transport, players, sessions).BuildSnapshot(creator.Id);
        var published = snapshot.ActiveSessions[0];

        Assert.AreEqual(DefaultGamemodes.PvP, published.Gamemode);
        Assert.AreEqual("both", published.GamemodeParameters["constraint"]);
        Assert.AreEqual("3", published.GamemodeParameters["walls"]);
        Assert.AreEqual(1, published.Players.Count);
        Assert.AreEqual(2, published.Bots.Count);
        Assert.AreEqual(3, published.PlayerCount);
    }

    [TestMethod]
    public void Snapshot_KeepsFinalPlacingsAfterTheLastPlayerLeaves()
    {
        var players = new PlayerRegistry();
        var transport = new RecordingTransport();
        var sessions = new SessionManager(transport, players);
        var creator = Join(players, 1, "alice");

        var session = sessions.CreateSession(creator.ClientIndex, "nfmm/road", 4, DefaultGamemodes.PvP)!;
        session.State = SessionState.Finished;
        session.ClosesAtUnixMs = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();
        session.FinalStandings =
        [
            new SessionStanding
            {
                PlayerId = creator.Id,
                PlayerName = "alice",
                CarName = "nfmm/radicalone",
                Color = new Color3(1, 2, 3),
                Position = 0,
                Dnf = false
            }
        ];

        sessions.LeaveSession(creator.ClientIndex, session.Id);
        sessions.PruneFinished();

        var published = new LobbyStateBroadcaster(transport, players, sessions)
            .BuildSnapshot(creator.Id)
            .ActiveSessions[0];

        Assert.AreEqual(SessionState.Finished, published.State);
        Assert.AreEqual(session.ClosesAtUnixMs, published.ClosesAtUnixMs);
        Assert.AreEqual(0, published.Players.Count);
        Assert.AreEqual(1, published.FinalStandings.Count);
        Assert.AreEqual("alice", published.FinalStandings[0].PlayerName);
    }

    [TestMethod]
    public void PruneKeepsEmptyRunningMatchWhileResultsMayStillArrive()
    {
        var (players, transport, _) = Setup();
        var sessions = new SessionManager(transport, players);
        var creator = Join(players, 1, "alice");
        var session = sessions.CreateSession(creator.ClientIndex, "nfmm/road", 4, DefaultGamemodes.PvP)!;
        Assert.IsTrue(sessions.StartRace(creator.ClientIndex, session.Id));

        sessions.LeaveSession(creator.ClientIndex, session.Id);
        Assert.IsFalse(sessions.PruneFinished());
        Assert.AreEqual(SessionState.Started, sessions.Get(session.Id)!.State);

        session.ClosesAtUnixMs = 1;
        Assert.IsTrue(sessions.PruneFinished());
        Assert.IsNull(sessions.Get(session.Id));
    }
}
