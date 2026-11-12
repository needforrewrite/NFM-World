using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Archive;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorld.Library.Test;

/// <summary>
/// The account handshake over the real WebSocket transports, loopback.
///
/// The transports used to fire and forget every send, so a packet written while another was
/// mid-flight threw inside an unobserved task and was dropped. A lost reply is what stranded
/// players on "Verifying account...", which no in-process stub can reproduce.
/// </summary>
[TestClass]
public class LobbyHandshakeTests
{
    private sealed class ValidArchive : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ArchiveAccount(42, "account-name"))
                }
            );
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly HttpListener _listener;
        private readonly WebSocketMultiplayerServerTransport _server;

        public ushort Port { get; }
        public WebSocketMultiplayerClientTransport Client { get; }
        public GameOrchestrator Orchestrator { get; }
        public WebSocketMultiplayerServerTransport Server => _server;

        /// <summary>The connection index the server accepted <see cref="Client"/> as.</summary>
        public uint ClientIndex { get; private set; }

        private Rig(ushort port, HttpListener listener, WebSocketMultiplayerServerTransport server, GameOrchestrator orchestrator)
        {
            Port = port;
            _listener = listener;
            _server = server;
            Orchestrator = orchestrator;
            Client = new WebSocketMultiplayerClientTransport("localhost", port);
        }

        public static async Task<Rig> Start()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = (ushort)((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            var server = new WebSocketMultiplayerServerTransport();
            var orchestrator = new GameOrchestrator(
                server,
                new ArchiveClient("https://archive.invalid/", http: new HttpClient(new ValidArchive()))
            );
            orchestrator.Start();

            var rig = new Rig(port, listener, server, orchestrator);
            _ = Task.Run(rig.AcceptLoopAsync);

            Assert.IsTrue(await rig.Client.WaitForStateAsync(ClientState.Connected), "the client never connected");
            rig.ClientIndex = await rig.WaitForClientIndexAsync();
            return rig;
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                if (ctx.Request.IsWebSocketRequest)
                    _ = _server.AcceptWebSocketRequest(ctx, _cts.Token);
            }
        }

        public WebSocketMultiplayerClientTransport ConnectAnother()
            => new("localhost", Port);

        private async Task<uint> WaitForClientIndexAsync()
        {
            var deadline = Environment.TickCount64 + 5000;
            while (Environment.TickCount64 < deadline)
            {
                if (_server.Connections.Count > 0)
                    return _server.Connections.First();
                await Task.Delay(10);
            }

            throw new AssertFailedException("the server never registered the connection");
        }

        public async ValueTask DisposeAsync()
        {
            Client.Stop();
            _cts.Cancel();
            Orchestrator.Stop();
            _listener.Stop();
            await Task.Yield();
        }
    }

    private static async Task<T> WaitForAsync<T>(IMultiplayerClientTransport client, int milliseconds = 5000)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            foreach (var packet in client.GetNewPackets())
            {
                if (packet is T typed)
                    return typed;
            }

            await Task.Delay(10);
        }

        throw new AssertFailedException($"{typeof(T).Name} never arrived");
    }

    [TestMethod]
    public async Task Authenticate_IsAnsweredOverTheRealTransport()
    {
        await using var rig = await Rig.Start();
        rig.Client.SendPacketToServer(new C2S_Authenticate { Token = "saved-token" });

        var reply = await WaitForAsync<S2C_Authenticated>(rig.Client);
        Assert.IsTrue(reply.Accepted);
        Assert.AreEqual(42, reply.AccountId);
    }

    /// <summary>
    /// Back-to-back sends are what a client does on connect: authenticate, then the identity
    /// packet as soon as the reply lands. Each has to reach the server.
    /// </summary>
    [TestMethod]
    public async Task BackToBackSends_AllReachTheServer()
    {
        await using var rig = await Rig.Start();
        rig.Client.SendPacketToServer(new C2S_Authenticate { Token = "saved-token" });
        Assert.IsTrue((await WaitForAsync<S2C_Authenticated>(rig.Client)).Accepted);
        rig.Client.SendPacketToServer(new C2S_PlayerIdentity
        {
            PlayerName = "account-name", SelectedVehicle = "nfmm/other-car", Color = default
        });

        const int count = 200;
        for (var i = 0; i < count; i++)
            rig.Client.SendPacketToServer(new C2S_LobbyChatMessage { Message = $"line {i}" });

        // The last chat line coming back proves the server read the whole burst, including the
        // identity packet that preceded it.
        var seen = 0;
        var deadline = Environment.TickCount64 + 8000;
        while (Environment.TickCount64 < deadline && seen < count)
        {
            foreach (var packet in rig.Client.GetNewPackets())
            {
                if (packet is S2C_LobbyChatMessage chat && chat.Message.StartsWith("line "))
                    seen++;
            }

            await Task.Delay(10);
        }

        Assert.AreEqual(count, seen, $"{count - seen} of {count} packets were dropped");

        // The identity survived the burst: the roster carries the claimed name and car.
        var state = await WaitForAsync<S2C_LobbyState>(rig.Client);
        Assert.AreEqual("nfmm/other-car", state.Players.Single(p => p.PlayerName == "account-name").CarName);
    }

    /// <summary>A refusal is followed by a disconnect, so it has to be delivered before the close.</summary>
    [TestMethod]
    public async Task Refusal_ReachesTheClientBeforeTheSocketCloses()
    {
        await using var rig = await Rig.Start();
        rig.Client.SendPacketToServer(new C2S_Authenticate { Token = "saved-token" });
        Assert.IsTrue((await WaitForAsync<S2C_Authenticated>(rig.Client)).Accepted);

        var second = rig.ConnectAnother();
        try
        {
            Assert.IsTrue(await second.WaitForStateAsync(ClientState.Connected), "the second client never connected");
            second.SendPacketToServer(new C2S_Authenticate { Token = "saved-token" });

            var reply = await WaitForAsync<S2C_Authenticated>(second);
            Assert.IsFalse(reply.Accepted);
            Assert.IsFalse(reply.SessionRejected);
            Assert.IsNotEmpty(reply.Error);
        }
        finally
        {
            second.Stop();
        }
    }

    /// <summary>
    /// The lobby answers from the connection's own thread while its one-second state broadcast
    /// runs on another, so one client is written to from two threads at once. A WebSocket allows
    /// a single outstanding send and throws on the second, which used to lose that packet - the
    /// handshake reply among them, leaving the player on "Verifying account...".
    /// </summary>
    [TestMethod]
    public async Task ConcurrentWrites_DoNotDropPackets()
    {
        await using var rig = await Rig.Start();
        rig.Client.SendPacketToServer(new C2S_Authenticate { Token = "saved-token" });
        Assert.IsTrue((await WaitForAsync<S2C_Authenticated>(rig.Client)).Accepted);

        const int perWriter = 25;
        const int writers = 8;
        var total = perWriter * writers;

        await Task.WhenAll(
            Enumerable.Range(0, writers).Select(w => Task.Run(() =>
                {
                    for (var i = 0; i < perWriter; i++)
                    {
                        rig.Server.SendPacketToClient(
                        rig.ClientIndex,
                        new S2C_LobbyChatMessage { SenderId = Guid.Empty, Sender = "w", Message = $"{w}:{i}" }
                    );
                    }
                }
            )
            )
        );

        var seen = 0;
        var deadline = Environment.TickCount64 + 8000;
        while (Environment.TickCount64 < deadline && seen < total)
        {
            foreach (var packet in rig.Client.GetNewPackets())
            {
                if (packet is S2C_LobbyChatMessage chat && chat.Sender == "w")
                    seen++;
            }

            await Task.Delay(10);
        }

        Assert.AreEqual(total, seen, $"{total - seen} of {total} packets were dropped by concurrent writes");
    }
}

internal static class TransportWaitExtensions
{
    public static async Task<bool> WaitForStateAsync(
        this IMultiplayerClientTransport client,
        ClientState state,
        int milliseconds = 5000
    )
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (client.State == state)
                return true;
            await Task.Delay(10);
        }

        return client.State == state;
    }
}
