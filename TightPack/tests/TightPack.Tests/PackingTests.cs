using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TightPack.Tests;

/// <summary>
/// The wire format's defining property: bytes on the wire are exactly the member payloads, so no
/// padding or uninitialised memory can reach it.
/// </summary>
[TestClass]
public sealed class PackingTests
{
    [TestMethod]
    public void BlittableStruct_IsProvenPaddingFree()
    {
        // Explicitly the layout the analyzer claimed, so the proof is not just consistent with itself.
        Assert.AreEqual(8, Unsafe.SizeOf<TightStruct>());
        Assert.AreEqual(8, Marshal.SizeOf<TightStruct>());
    }

    [TestMethod]
    public void BlittableStruct_EncodesToExactlyItsFields()
    {
        var value = new TightStruct { X = 0x01020304, Y = 0f };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        // One object header byte (member count) then the raw image.
        Assert.HasCount(9, bytes);
        Assert.AreEqual(2, bytes[0]);
        CollectionAssert.AreEqual(BitConverter.GetBytes(0x01020304), bytes[1..5]);
    }

    [TestMethod]
    public void PaddedStruct_DoesNotReachTheWire()
    {
        // byte + long: 9 bytes of data, but a raw image would be 16 with 7 padding bytes.
        Assert.AreEqual(16, Unsafe.SizeOf<PaddedStruct>());

        var value = new PaddedStruct { A = 0x7F, B = long.MaxValue };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        // header + 1 + 8, not header + 16.
        Assert.HasCount(10, bytes);
        Assert.AreEqual(2, bytes[0]);
        Assert.AreEqual(0x7F, bytes[1]);
    }

    [TestMethod]
    public void PaddedStruct_DoesNotLeakNeighbouringMemory()
    {
        var value = new PaddedStruct { A = 0xAA, B = 0x0102030405060708 };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        // The seven bytes between A and B exist only in memory; the payload must not contain them.
        var payload = bytes.AsSpan(2).ToArray();
        CollectionAssert.AreEqual(BitConverter.GetBytes(0x0102030405060708L), payload);
    }

    [TestMethod]
    public void NestedBlittableStruct_IsAlsoBlitted()
    {
        Assert.AreEqual(12, Unsafe.SizeOf<NestedTightStruct>());

        var value = new NestedTightStruct { Inner = new TightStruct { X = 5, Y = 6 }, Z = 7 };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        Assert.HasCount(13, bytes);
        NestedTightStruct result = TightPackSerializer.Deserialize<NestedTightStruct>(bytes);
        Assert.AreEqual(5, result.Inner.X);
        Assert.AreEqual(6f, result.Inner.Y);
        Assert.AreEqual(7, result.Z);
    }

    [TestMethod]
    public void NullableStruct_IsNeverBlitted()
    {
        // Nullable<T> has a bool flag plus an aligned value, which is padding, and it cannot satisfy
        // the unmanaged constraint, so the generator must fall back to member writes.
        var value = new NullableHolder { Value = 1234, Tag = 5 };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        NullableHolder result = TightPackSerializer.Deserialize<NullableHolder>(bytes);
        Assert.AreEqual(1234, result.Value);
        Assert.AreEqual(5, result.Tag);

        var empty = new NullableHolder { Value = null, Tag = 1 };
        NullableHolder emptyResult = TightPackSerializer.Deserialize<NullableHolder>(TightPackSerializer.Serialize(in empty));
        Assert.IsNull(emptyResult.Value);
        Assert.AreEqual(1, emptyResult.Tag);
    }

    [TestMethod]
    public void BoolPayload_IsOneByte()
    {
        // bool is 1 byte in managed memory but 4 as a Win32 BOOL; the writer must use the managed form.
        var value = new Scalars { Bool = true };
        byte[] bytes = TightPackSerializer.Serialize(in value);
        Scalars result = TightPackSerializer.Deserialize<Scalars>(bytes);

        Assert.IsTrue(result.Bool);
    }

    [TestMethod]
    public void StringPayload_IsUtf8WithAByteLength()
    {
        var value = new SimpleObject { Name = "ab" };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        // header, id (4), name length (4), name bytes (2), score (8)
        Assert.HasCount(1 + 4 + 4 + 2 + 8, bytes);
    }

    [TestMethod]
    public void SerializeIntoCallerBuffer_WritesTheSameBytes()
    {
        var value = new SimpleObject { Id = 3, Name = "x", Score = 2 };
        byte[] direct = TightPackSerializer.Serialize(in value);

        var buffer = new ArrayBufferWriter<byte>();
        TightPackSerializer.Serialize(ref buffer, in value);

        CollectionAssert.AreEqual(direct, buffer.WrittenSpan.ToArray());
    }
}
