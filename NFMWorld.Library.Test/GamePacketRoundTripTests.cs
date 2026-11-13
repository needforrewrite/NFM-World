using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Round-trips the in-race game-master packets that carry a payload, which the lobby
/// round-trip tests do not cover. A regression here drops the server's countdown-go and
/// finish events while the countdown the client animates locally still runs to zero.
/// </summary>
[TestClass]
public class GamePacketRoundTripTests
{
    private static void WritePacket<T>(T packet, IBufferWriter<byte> writer)
        where T : IReadableWritable<T>, TightPack.ITightPackable<T> => packet.Write(writer);

    [TestMethod]
    public void ServerEvent_RoundTripsItsPayload()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var sent = new S2C_ServerEvent { Payload = payload };

        using var writer = new ArrayPoolBufferWriter<byte>();
        WritePacket(sent, writer);

        var received = (S2C_ServerEvent)MultiplayerUtils.TryDeserializeS2CPacket(-8, writer.WrittenMemory)!;

        CollectionAssert.AreEqual(payload, received.Payload.ToArray());
        received.Dispose();
    }

    [TestMethod]
    public void ServerState_RoundTripsItsPayload()
    {
        var payload = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 };
        var sent = new S2C_ServerState { Payload = payload };

        using var writer = new ArrayPoolBufferWriter<byte>();
        WritePacket(sent, writer);

        var received = (S2C_ServerState)MultiplayerUtils.TryDeserializeS2CPacket(-10, writer.WrittenMemory)!;

        CollectionAssert.AreEqual(payload, received.Payload.ToArray());
        received.Dispose();
    }

    [TestMethod]
    public void ClientEvent_RoundTripsItsPayload()
    {
        var payload = new byte[] { 4, 5, 6, 7, 8, 9 };
        var sent = new C2S_ClientEvent { Payload = payload };

        using var writer = new ArrayPoolBufferWriter<byte>();
        WritePacket(sent, writer);

        var received = (C2S_ClientEvent)MultiplayerUtils.TryDeserializeC2SPacket(10, writer.WrittenMemory)!;

        CollectionAssert.AreEqual(payload, received.Payload.ToArray());
        received.Dispose();
    }
}
