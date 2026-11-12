using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary;
using TightPack;
using NFMWorldLibrary.Util;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

/// <summary>
/// The two packet-level types that carry every per-frame car state. They are the highest-traffic
/// types on the wire and the ones whose memory layout would leak padding if the format were ever
/// allowed to blit them, so they get an explicit round-trip rather than only indirect coverage
/// through a race.
/// </summary>
[TestClass]
public class PlayerStateSerializationTests
{
    private static CarFrame SampleFrame()
    {
        var flags = default(CarFrame.BitFlags);
        flags.Wasted = true;
        flags.Up = true;

        return new CarFrame
        {
            Power = (fix64)1.5f,
            TheBitFlags = flags,
            CarPosition = ((fix64)10, (fix64)20, (fix64)30),
            CarRotation = ((fix64)1, (fix64)2, (fix64)3),
            RacePosition = (2, 7),
            StuntState = ((sbyte)-1, (fix64)0.5f, (fix64)0.25f, (fix64)0.125f),
            XzReadings = ((fix64)100, (fix64)(-100)),
            CameraAngles = ((fix64)45, (fix64)(-45)),
            AngularVelocities = ((fix64)1, (fix64)2, (fix64)3, (fix64)4),
        };
    }

    [TestMethod]
    public void PlayerState_RoundTrips()
    {
        var value = new PlayerState
        {
            CarFrame = SampleFrame(),
            Ticks = 12345,
            CurrentTime = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000),
        };

        PlayerState result = TightPackSerializer.Deserialize<PlayerState>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.AreEqual(value.Ticks, result.Ticks);
        Assert.AreEqual(value.CurrentTime, result.CurrentTime);
        Assert.AreEqual(value.CarFrame.Power, result.CarFrame.Power);
        Assert.AreEqual(value.CarFrame.CarPosition, result.CarFrame.CarPosition);
        Assert.AreEqual(value.CarFrame.RacePosition, result.CarFrame.RacePosition);
        Assert.AreEqual(value.CarFrame.StuntState, result.CarFrame.StuntState);
        Assert.AreEqual(value.CarFrame.CameraAngles, result.CarFrame.CameraAngles);
        Assert.IsTrue(result.CarFrame.TheBitFlags.Wasted);
        Assert.IsTrue(result.CarFrame.TheBitFlags.Up);
        Assert.IsFalse(result.CarFrame.TheBitFlags.Down);
    }

    [TestMethod]
    public void PlayerStatePacket_RoundTripsThroughTheSharedPacketPath()
    {
        var state = new PlayerState
        {
            CarFrame = SampleFrame(),
            Ticks = 7,
            CurrentTime = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000),
        };

        var packet = new C2S_PlayerState { State = state };
        // Read is a static virtual member, so it only binds through a type parameter - the same
        // path the opcode dispatcher takes.
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        ((IReadableWritable<C2S_PlayerState>)packet).Write(writer);
        C2S_PlayerState result = ReadPacket<C2S_PlayerState>(writer.WrittenMemory);

        Assert.AreEqual(7u, result.State.Ticks);
        Assert.AreEqual(state.CarFrame.CarPosition, result.State.CarFrame.CarPosition);
        Assert.IsTrue(result.State.CarFrame.TheBitFlags.Wasted);
    }

    private static T ReadPacket<T>(ReadOnlyMemory<byte> data)
        where T : IReadableWritable<T>, ITightPackable<T> => T.Read(data);

    /// <summary>
    /// A per-frame packet has to be the same size for every car state: a variable-length encoding
    /// here would make the wire cost depend on the values rather than on the layout.
    /// </summary>
    [TestMethod]
    public void PlayerState_WireSizeDoesNotDependOnTheValues()
    {
        var neutral = new PlayerState
        {
            CarFrame = default,
            Ticks = 0,
            CurrentTime = DateTimeOffset.FromUnixTimeMilliseconds(0),
        };

        var extreme = new PlayerState
        {
            CarFrame = SampleFrame(),
            Ticks = uint.MaxValue,
            CurrentTime = DateTimeOffset.FromUnixTimeSeconds(4_102_444_800),
        };

        Assert.AreEqual(
            TightPackSerializer.Serialize(in neutral).Length,
            TightPackSerializer.Serialize(in extreme).Length
        );
    }
}
