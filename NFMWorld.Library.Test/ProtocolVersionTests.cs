using System.Buffers;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// The wire version handshake. Both sides declare <see cref="Protocol.Version"/> on connect and
/// refuse the other on a mismatch, because a packet layout from another revision does not fail
/// to parse - it parses into fields that mean something else.
/// </summary>
[TestClass]
public class ProtocolVersionTests
{
    /// <summary>Builds the bytes a client would put on the wire for one packet.</summary>
    private static byte[] Frame<T>(T packet) where T : IReadableWritable<T>, TightPack.ITightPackable<T>, IPacket
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        writer.Write(packet.Opcode);
        packet.Write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private sealed class TestServer : BaseMultiplayerServerTransport
    {
        public List<(uint Client, object Packet)> Received { get; } = [];
        public List<(uint Client, object Packet)> Sent { get; } = [];
        public List<uint> Disconnected { get; } = [];

        public override IReadOnlyCollection<uint> Connections => [];

        public override event EventHandler<uint>? ClientConnecting;
        public override event EventHandler<uint>? ClientConnected;
        public override event EventHandler<uint>? ClientDisconnected;

        public TestServer()
        {
            PacketReceived += (_, e) =>
            {
                lock (Received)
                    Received.Add(e);
            };
        }

        public void Connect(uint client) => ClientConnecting?.Invoke(this, client);

        public void Disconnect(uint client)
        {
            ClientDisconnected?.Invoke(this, client);
            ForgetConnection(client);
        }

        public void Deliver(uint client, byte[] frame) => ReceivePacket(client, frame);

        public override void SendRawPacketToClients(ReadOnlySpan<uint> clientIndices, ReadOnlySpan<byte> span, bool reliable)
        {
            // Unpack the frame the base wrote, so a test can assert on the packet itself.
            var opcode = (sbyte)span[0];
            var packet = MultiplayerUtils.TryDeserializeS2CPacket(opcode, span[1..].ToArray());
            lock (Sent)
                Sent.Add((clientIndices[0], packet!));
        }

        public override void DisconnectClient(uint clientIndex)
        {
            lock (Disconnected)
                Disconnected.Add(clientIndex);
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }
    }

    private sealed class TestClient : BaseMultiplayerClientTransport
    {
        public List<byte[]> Frames { get; } = [];

        public void Connect() => State = ClientState.Connected;

        public void Deliver(byte[] frame) => ReceivePacket(frame);

        protected override void SendRawPacketToServer(ReadOnlySpan<byte> span, bool reliable)
        {
            lock (Frames)
                Frames.Add(span.ToArray());
        }
    }

    [TestMethod]
    public void Client_DeclaresItsVersionAsTheFirstPacket()
    {
        var client = new TestClient();
        client.Connect();

        Assert.HasCount(1, client.Frames);
        Assert.AreEqual(
            Protocol.ClientHandshakeOpcode,
            (sbyte)client.Frames[0][0],
            "the version has to precede anything whose layout it describes"
        );

        var sent = MultiplayerUtils.TryDeserializeC2SPacket(
            Protocol.ClientHandshakeOpcode,
            client.Frames[0][1..]
        ) as C2S_Handshake?;
        Assert.AreEqual(Protocol.Version, sent?.Version);
    }

    [TestMethod]
    public void Server_AcceptsMatchingVersion()
    {
        var server = new TestServer();
        server.Connect(1);

        server.Deliver(1, Frame(new C2S_Handshake { Version = Protocol.Version }));

        Assert.HasCount(1, server.Sent);
        var reply = (S2C_Handshake)server.Sent[0].Packet;
        Assert.IsTrue(reply.Accepted);
        Assert.AreEqual(Protocol.Version, reply.Version);
        Assert.IsEmpty(server.Disconnected);
    }

    [TestMethod]
    public void Server_RefusesOldVersionAndDisconnects()
    {
        var server = new TestServer();
        server.Connect(1);

        server.Deliver(1, Frame(new C2S_Handshake { Version = Protocol.Version - 1 }));

        var reply = (S2C_Handshake)server.Sent[0].Packet;
        Assert.IsFalse(reply.Accepted);
        Assert.AreEqual(Protocol.Version, reply.Version, "the refusal names the server's version");
        Assert.IsNotEmpty(reply.Error);
        CollectionAssert.Contains(server.Disconnected, 1u);
    }

    /// <summary>
    /// A build from before the handshake sends its packet straight away. Nothing it sends can be
    /// read safely, so it is refused rather than handed to a handler.
    /// </summary>
    [TestMethod]
    public void Server_RefusesAPacketSentBeforeTheHandshake()
    {
        var server = new TestServer();
        server.Connect(1);

        server.Deliver(
            1,
            Frame(new C2S_Authenticate { Token = "saved-token" })
        );

        Assert.IsEmpty(server.Received, "an unversioned packet must not reach a handler");
        Assert.IsFalse(((S2C_Handshake)server.Sent[0].Packet).Accepted);
        CollectionAssert.Contains(server.Disconnected, 1u);
    }

    [TestMethod]
    public void Server_AcceptsPacketsAfterAMatchingHandshake()
    {
        var server = new TestServer();
        server.Connect(1);
        server.Deliver(1, Frame(new C2S_Handshake { Version = Protocol.Version }));

        server.Deliver(1, Frame(new C2S_Authenticate { Token = "saved-token" }));

        Assert.HasCount(1, server.Received);
        Assert.IsInstanceOfType<C2S_Authenticate>(server.Received[0].Packet);
    }

    [TestMethod]
    public void Client_MarksTheHandshakeAccepted()
    {
        var client = new TestClient();
        client.Connect();

        client.Deliver(
            Frame(new S2C_Handshake { Version = Protocol.Version, Accepted = true })
        );

        Assert.IsTrue(client.HandshakeAccepted);
        Assert.IsNull(client.HandshakeError);
    }

    [TestMethod]
    public void Client_SurfacesARefusalAndStopsRetrying()
    {
        var client = new TestClient();
        client.Connect();

        client.Deliver(
            Frame(
                new S2C_Handshake
                {
                    Version = Protocol.Version + 1,
                    Accepted = false,
                    Error = "This server runs game version 2 and your build is version 1."
                }
            )
        );

        Assert.IsFalse(client.HandshakeAccepted);
        Assert.AreEqual("This server runs game version 2 and your build is version 1.", client.HandshakeError);
        Assert.AreEqual(ClientState.Disconnected, client.State);
        Assert.IsFalse(client.IsReconnecting, "the same server would refuse a retry the same way");
    }

    /// <summary>
    /// A connection index is reused after a disconnect on some transports. The next connection
    /// has said nothing, so it must start over rather than inherit the previous handshake.
    /// </summary>
    [TestMethod]
    public void Server_ForgettingAConnection_RequiresAFreshHandshake()
    {
        var server = new TestServer();
        server.Connect(1);
        server.Deliver(1, Frame(new C2S_Handshake { Version = Protocol.Version }));

        server.Disconnect(1);
        server.Connect(1);
        server.Deliver(1, Frame(new C2S_Authenticate { Token = "saved-token" }));

        Assert.IsEmpty(server.Received);
        CollectionAssert.Contains(server.Disconnected, 1u);
    }
}
