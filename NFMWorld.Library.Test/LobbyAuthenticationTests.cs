using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Archive;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorld.Library.Test;

[TestClass]
public class LobbyAuthenticationTests
{
    private sealed class StubTransport : IMultiplayerServerTransport
    {
        public IReadOnlyCollection<uint> Connections => [];
        public event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
        public event EventHandler<uint>? ClientConnecting;
        public event EventHandler<uint>? ClientConnected;
        public event EventHandler<uint>? ClientDisconnected;
        public ConcurrentQueue<(uint Client, object Packet)> Sent { get; } = new();
        public ConcurrentQueue<uint> Disconnected { get; } = new();

        public TaskCompletionSource<S2C_Authenticated> Authentication { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Connect(uint client)
        {
            ClientConnecting?.Invoke(this, client);
            ClientConnected?.Invoke(this, client);
        }

        public void Receive(uint client, IPacketClientToServer packet) => PacketReceived?.Invoke(this, (client, packet));

        public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true) where T : IPacketServerToClient<T>
        {
            Sent.Enqueue((clientIndex, packet));
            if (packet is S2C_Authenticated authenticated)
                Authentication.TrySetResult(authenticated);
        }

        public void DisconnectClient(uint clientIndex)
        {
            Disconnected.Enqueue(clientIndex);
            ClientDisconnected?.Invoke(this, clientIndex);
        }

        public void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true)
            where T : IPacketServerToClient<T>
        {
            foreach (var client in clientIndices)
                SendPacketToClient(client, packet, reliable);
        }

        public void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>
            => Sent.Enqueue((0, packet));

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class StubArchive(Func<Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return respond();
        }
    }

    private static HttpResponseMessage Account() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new ArchiveAccount(42, "account-name"))
    };

    private static ArchiveClient Archive(StubArchive handler) => new("https://archive.invalid/", http: new HttpClient(handler));

    private static C2S_CreateSession Create() => new()
    {
        StageName = "nfmm/road", MaxPlayers = 4, GameMode = DefaultGamemodes.PvP, GameModeParameters = []
    };

    private static C2S_PlayerIdentity Identity() => new()
    {
        PlayerName = "claimed-name", SelectedVehicle = "nfmm/other-car", Color = default
    };

    private static S2C_Authenticated Reply(StubTransport transport, uint client = 1) =>
        transport.Sent.Where(p => p.Client == client).Select(p => p.Packet).OfType<S2C_Authenticated>().Single();

    private static S2C_LobbyState Snapshot(StubTransport transport, uint client = 1) =>
        transport.Sent.Where(p => p.Client == client).Select(p => p.Packet).OfType<S2C_LobbyState>().Last();

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void MissingToken_IsRefusedAndDisconnected(string? token)
    {
        var transport = new StubTransport();
        var archive = new StubArchive(() => Task.FromResult(Account()));
        var lobby = new GameOrchestrator(transport, Archive(archive));
        transport.Connect(1);
        transport.Receive(1, new C2S_Authenticate { Token = token! });

        Assert.IsFalse(Reply(transport).Accepted);
        Assert.IsFalse(Reply(transport).SessionRejected);
        CollectionAssert.Contains(transport.Disconnected.ToArray(), 1u);
        Assert.AreEqual(0, archive.Requests);
        Assert.IsFalse(transport.Sent.Any(p => p.Packet is S2C_LobbyState or S2C_LobbyChatMessage));
        lobby.Stop();
    }

    [TestMethod]
    public void MissingArchiveConfiguration_IsRefusedWithoutInvalidatingToken()
    {
        var transport = new StubTransport();
        var lobby = new GameOrchestrator(transport, archive: null);
        transport.Connect(1);
        transport.Receive(1, new C2S_Authenticate { Token = "saved-token" });

        Assert.IsFalse(Reply(transport).Accepted);
        Assert.IsFalse(Reply(transport).SessionRejected);
        CollectionAssert.Contains(transport.Disconnected.ToArray(), 1u);
        lobby.Stop();
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized, true)]
    [DataRow(HttpStatusCode.ServiceUnavailable, false)]
    [DataRow(HttpStatusCode.TooManyRequests, false)]
    [DataRow(HttpStatusCode.NotFound, false)]
    public void ArchiveRefusal_DistinguishesExpiredSessionFromOutage(HttpStatusCode status, bool sessionRejected)
    {
        var transport = new StubTransport();
        var archive = new StubArchive(() => Task.FromResult(new HttpResponseMessage(status)));
        var lobby = new GameOrchestrator(transport, Archive(archive));
        transport.Connect(1);
        transport.Receive(1, new C2S_Authenticate { Token = "saved-token" });

        Assert.IsFalse(Reply(transport).Accepted);
        Assert.AreEqual(sessionRejected, Reply(transport).SessionRejected);
        CollectionAssert.Contains(transport.Disconnected.ToArray(), 1u);
        lobby.Stop();
    }

    [TestMethod]
    public void UnauthenticatedPackets_CannotSetIdentityCreateJoinChatStartOrSpectate()
    {
        var transport = new StubTransport();
        var lobby = new GameOrchestrator(transport, Archive(new StubArchive(() => Task.FromResult(Account()))));
        transport.Connect(1);
        transport.Receive(1, Identity());
        transport.Receive(1, Create());
        transport.Receive(1, new C2S_JoinSession { SessionId = 1 });
        transport.Receive(1, new C2S_LobbyChatMessage { Message = "guest chat" });
        transport.Receive(1, new C2S_LobbyStartRace { SessionId = 1 });
        transport.Receive(1, new C2S_SpectateSession { SessionId = 1 });
        transport.Receive(1, new C2S_LobbyPlayerReadyState { SessionId = 1, IsReady = true });
        Assert.IsEmpty(transport.Sent);

        transport.Receive(1, new C2S_Authenticate { Token = "saved-token" });
        Assert.IsTrue(Reply(transport).Accepted);
        Assert.AreEqual(42, Reply(transport).AccountId);
        Assert.IsEmpty(Snapshot(transport).ActiveSessions);
        Assert.AreEqual("account-name", Snapshot(transport).Players.Single().PlayerName);
        Assert.AreEqual("nfmm/radicalone", Snapshot(transport).Players.Single().CarName);

        transport.Receive(1, Identity());
        transport.Receive(1, Create());
        Assert.AreEqual("account-name", Snapshot(transport).Players.Single().PlayerName);
        Assert.AreEqual("nfmm/other-car", Snapshot(transport).Players.Single().CarName);
        Assert.HasCount(1, Snapshot(transport).ActiveSessions);
        lobby.Stop();
    }

    [TestMethod]
    public void DuplicateAccount_IsRefusedWithoutInvalidatingToken()
    {
        var transport = new StubTransport();
        var archive = new StubArchive(() => Task.FromResult(Account()));
        var lobby = new GameOrchestrator(transport, Archive(archive));
        transport.Connect(1);
        transport.Receive(1, new C2S_Authenticate { Token = "first-token" });
        transport.Receive(1, new C2S_Authenticate { Token = "first-token" });
        transport.Connect(2);
        transport.Receive(1, new C2S_LobbyChatMessage { Message = "account chat" });
        transport.Receive(2, Identity());
        transport.Receive(2, new C2S_JoinSession { SessionId = 1 });
        Assert.HasCount(1, Snapshot(transport).Players);
        Assert.IsFalse(transport.Sent.Any(p => p.Client == 2 && p.Packet is S2C_LobbyState or S2C_LobbyChatMessage));
        transport.Receive(2, new C2S_Authenticate { Token = "second-token" });

        Assert.IsTrue(Reply(transport).Accepted);
        Assert.IsFalse(Reply(transport, 2).Accepted);
        Assert.IsFalse(Reply(transport, 2).SessionRejected);
        CollectionAssert.AreEqual(new uint[] { 2 }, transport.Disconnected.ToArray());
        Assert.HasCount(1, Snapshot(transport).Players);
        Assert.AreEqual(2, archive.Requests, "repeated tokens must not trigger more archive requests");
        lobby.Stop();
    }

    [TestMethod]
    public async Task PendingAuthentication_CannotActOrReverifyTheSameToken()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var archive = new StubArchive(() => response.Task);
        var transport = new StubTransport();
        var lobby = new GameOrchestrator(transport, Archive(archive));
        transport.Connect(1);
        transport.Receive(1, new C2S_Authenticate { Token = "saved-token" });
        transport.Receive(1, new C2S_Authenticate { Token = "saved-token" });
        transport.Receive(1, Create());
        transport.Receive(1, Identity());
        Assert.IsEmpty(transport.Sent);
        Assert.AreEqual(1, archive.Requests);

        response.SetResult(Account());
        Assert.IsTrue((await transport.Authentication.Task.WaitAsync(TimeSpan.FromSeconds(5))).Accepted);
        lobby.Stop();
    }
}
