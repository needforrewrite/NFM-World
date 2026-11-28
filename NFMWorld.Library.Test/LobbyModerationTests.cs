using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Archive;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorld.Library.Test;

/// <summary>
/// Lobby moderation: staff-only chat commands, mute refusals, and kick disconnects. The archive
/// is a stub that always verifies and always accepts a moderation event.
/// </summary>
[TestClass]
public class LobbyModerationTests
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

        public void Connect(uint client)
        {
            ClientConnecting?.Invoke(this, client);
            ClientConnected?.Invoke(this, client);
        }

        public void Receive(uint client, IPacketClientToServer packet) => PacketReceived?.Invoke(this, (client, packet));

        public void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true)
            where T : IPacketServerToClient<T>
            => Sent.Enqueue((clientIndex, packet));

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

        public void BroadcastPacket<T>(T packet, bool reliable = true)
            where T : IPacketServerToClient<T>
            => Sent.Enqueue((0, packet));

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class StubArchive : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            var path = request.RequestUri?.AbsolutePath ?? "";

            if (path == "/auth/verify")

                return Task.FromResult(VerifyResponse(request.Headers.Authorization?.ToString()));

            return Task.FromResult(Accepted());
        }
    }

    private static HttpResponseMessage Accepted()
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { ok = true, error = (string?)null }) };

    private static ArchiveClient Archive(StubArchive handler) => new(
        "https://archive.invalid/",
        keyId: "test",
        secretBase64: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
        http: new HttpClient(handler)
    );

    private static bool MutedUser;

    /// <summary>Resolves the account by the token the request carried.</summary>
    private static HttpResponseMessage VerifyResponse(string? bearer)
    {
        var token = bearer?.Split(' ')[^1] ?? "";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                token == "token-1"
                    ? new ArchiveAccount(10, "mod", UserRole.Moderator, 0, false, null, null, null, false, null, null)
                    : new ArchiveAccount(20, "user2", UserRole.User, 0, false, null, null, null, MutedUser, null, null)
            )
        };
    }

    /// <summary>Connects and authenticates two clients through the archive stub.</summary>
    private static async Task<(StubTransport Transport, GameOrchestrator Lobby)> ConnectTwoAsync(
        StubArchive archive
    )
    {
        var transport = new StubTransport();
        var lobby = new GameOrchestrator(transport, Archive(archive));
        transport.Connect(1);
        transport.Receive(1, new C2S_Authenticate { Token = "token-1" });
        await Task.Delay(50);

        transport.Connect(2);
        transport.Receive(2, new C2S_Authenticate { Token = "token-2" });
        await Task.Delay(50);

        return (transport, lobby);
    }

    /// <summary>
    /// Answers /auth/verify by token: t1 is the moderator, t2 a plain user. Distinct ids, so the
    /// one-account-one-seat rule never fires.
    /// </summary>
    private static HttpResponseMessage Verify(string token) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(
            token == "token-1"
                ? new ArchiveAccount(10, "mod", UserRole.Moderator, 0, false, null, null, null, false, null, null)
                : new ArchiveAccount(20, "user2", UserRole.User, 0, false, null, null, null, false, null, null)
        )
    };

    private static T Last<T>(StubTransport transport, uint client)
        where T : class
        => transport.Sent.Where(p => p.Client == client).Select(p => p.Packet).OfType<T>().Last();

    [TestMethod]
    public async Task ChatCommand_ByNonStaff_IsNotRelayedAndAnswersUnknownCommand()
    {
        var archive = new StubArchive();
        var (transport, lobby) = await ConnectTwoAsync(archive);

        transport.Receive(2, new C2S_LobbyChatMessage { Message = "/ban someone" });
        await Task.Delay(50);

        // Nothing was relayed, and the sender learned it is not a command.
        Assert.IsFalse(transport.Sent.Any(p => p.Packet is S2C_LobbyChatMessage chat && chat.SenderId != Guid.Empty));
        Assert.IsTrue(
            transport.Sent.Any(p => p.Client == 2 && p.Packet is S2C_ModerationNotice n && n.Message == "Unknown command.")
        );
        Assert.AreEqual(2, archive.Requests);
        lobby.Stop();
    }

    [TestMethod]
    public async Task MutedClient_CannotChatAndGetsANotice()
    {
        MutedUser = true;
        var archive = new StubArchive();
        var (transport, lobby) = await ConnectTwoAsync(archive);

        transport.Receive(2, new C2S_LobbyChatMessage { Message = "any chat" });
        await Task.Delay(50);

        Assert.IsFalse(transport.Sent.Any(p => p.Packet is S2C_LobbyChatMessage chat && chat.SenderId != Guid.Empty));
        Assert.IsTrue(
            transport.Sent.Any(p => p.Client == 2 && p.Packet is S2C_ModerationNotice m && m.Message == "You are muted.")
        );
        lobby.Stop();
    }

    [TestMethod]
    public async Task StaffKick_AppliesAndDisconnectsTheTarget()
    {
        MutedUser = false;
        var archive = new StubArchive();
        var (transport, lobby) = await ConnectTwoAsync(archive);

        transport.Receive(1, new C2S_LobbyChatMessage { Message = "/kick user2" });
        await Task.Delay(150);

        Assert.AreEqual(3, archive.Requests); // two verifies + one moderation event
        Assert.IsTrue(transport.Sent.Any(p => p.Client == 2 && p.Packet is S2C_ModerationNotice));
        Assert.IsTrue(transport.Sent.Any(p => p.Client == 1 && p.Packet is S2C_ModerationResult r && r.Ok));
        CollectionAssert.Contains(transport.Disconnected.ToArray(), 2u);
        lobby.Stop();
    }
}
