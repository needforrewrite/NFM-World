using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// The version gate over a real WebSocket, not the in-process stubs in
/// <see cref="ProtocolVersionTests"/>. The stubs prove the decision; these prove the refusals
/// reach the socket before it closes and that a foreign build really is unable to send anything
/// the server will act on.
/// </summary>
[TestClass]
public class ProtocolVersionTransportTests
{
    private static byte[] Frame<T>(T packet)
        where T : IReadableWritable<T>, TightPack.ITightPackable<T>, IPacket
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        writer.Write(packet.Opcode);
        packet.Write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private sealed class Wire : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly HttpListener _listener;

        public ushort Port { get; }
        public WebSocketMultiplayerServerTransport Server { get; } = new();
        public List<(uint Client, IPacketClientToServer Packet)> Received { get; } = [];

        private Wire(ushort port, HttpListener listener)
        {
            Port = port;
            _listener = listener;
            Server.PacketReceived += (_, e) =>
            {
                lock (Received)
                    Received.Add(e);
            };
        }

        public static Wire Start()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = (ushort)((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            var wire = new Wire(port, listener);
            wire.Server.Start();
            _ = Task.Run(wire.AcceptLoopAsync);
            return wire;
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
                    _ = Server.AcceptWebSocketRequest(ctx, _cts.Token);
            }
        }

        /// <summary>Opens a socket that speaks the framing by hand, as a build of another revision does.</summary>
        public static async Task<ClientWebSocket> ConnectRawAsync(ushort port)
        {
            var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri($"ws://localhost:{port}/game"), CancellationToken.None);
            return socket;
        }

        public async ValueTask DisposeAsync()
        {
            Server.Stop();
            _cts.Cancel();
            _listener.Stop();
            await Task.Yield();
        }
    }

    /// <summary>Waits for the server to answer, then decodes the packet the answer carries.</summary>
    private static async Task<IPacketServerToClient> ReceiveOneAsync(ClientWebSocket socket, int milliseconds = 5000)
    {
        using var timeout = new CancellationTokenSource(milliseconds);
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new AssertFailedException("the socket closed before the answer arrived");

                ms.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                var frame = ms.ToArray();
                return MultiplayerUtils.TryDeserializeS2CPacket((sbyte)frame[0], frame[1..])
                       ?? throw new AssertFailedException($"unknown opcode {frame[0]} in the server's answer");
            }
        }
        catch (OperationCanceledException)
        {
            throw new AssertFailedException("the server never answered over the wire");
        }
    }

    [TestMethod]
    public async Task Server_RefusesAnOlderBuildOverTheWire()
    {
        await using var wire = Wire.Start();
        using var socket = await Wire.ConnectRawAsync(wire.Port);

        await socket.SendAsync(
            Frame(new C2S_Handshake { Version = Protocol.Version - 1 }),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None
        );

        var reply = await ReceiveOneAsync(socket);
        Assert.IsInstanceOfType<S2C_Handshake>(reply);
        var handshake = (S2C_Handshake)reply;
        Assert.IsFalse(handshake.Accepted);
        Assert.AreEqual(Protocol.Version, handshake.Version, "the refusal names the server's version");
        StringAssert.Contains(handshake.Error, (Protocol.Version - 1).ToString(), "the refusal names the client's version");
    }

    /// <summary>
    /// A build from before the handshake exists sends its first packet without one. Nothing it
    /// sends may reach a handler, whatever the socket delivered first.
    /// </summary>
    [TestMethod]
    public async Task Server_RefusesAPacketSentWithoutAVersionOverTheWire()
    {
        await using var wire = Wire.Start();
        using var socket = await Wire.ConnectRawAsync(wire.Port);

        await socket.SendAsync(
            Frame(new C2S_Authenticate { Token = "saved-token" }),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None
        );

        var reply = await ReceiveOneAsync(socket);
        Assert.IsFalse(((S2C_Handshake)reply).Accepted);
        Assert.IsEmpty(wire.Received, "an unversioned packet must not reach a handler");
    }

    [TestMethod]
    public async Task Client_HandshakesOverTheWireAndIsAccepted()
    {
        await using var wire = Wire.Start();
        var client = new WebSocketMultiplayerClientTransport("localhost", wire.Port);

        try
        {
            Assert.IsTrue(await client.WaitForStateAsync(ClientState.Connected), "the client never connected");
            Assert.IsTrue(
                await WaitForAsync(() => client.HandshakeAccepted),
                client.HandshakeError ?? "the server never accepted the client's version"
            );
            Assert.IsNull(client.HandshakeError);

            // The connection is usable only once the version was settled.
            client.SendPacketToServer(new C2S_Authenticate { Token = "saved-token" });

            Assert.IsTrue(
                await WaitForAsync(() =>
                    {
                        lock (wire.Received)
                            return wire.Received.Exists(e => e.Packet is C2S_Authenticate);
                    }
                ),
                "the packet sent after the handshake never reached the server"
            );
        }
        finally
        {
            client.Stop();
        }
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, int milliseconds = 5000)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(10);
        }

        return condition();
    }
}
