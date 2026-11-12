using System.Buffers;

namespace TightPack.Tests;

[TestClass]
public sealed class SafetyTests
{
    [TestMethod]
    public void TruncatedPayload_Throws()
    {
        var value = new SimpleObject { Id = 1, Name = "abc" };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<SimpleObject>(bytes[..^4])
        );
    }

    [TestMethod]
    public void EmptyPayload_Throws()
    {
        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<SimpleObject>([])
        );
    }

    [TestMethod]
    public void HugeCollectionLength_ThrowsInsteadOfAllocating()
    {
        // header for Parent, then a collection header claiming int.MaxValue elements
        byte[] bytes =
        [
            4,
            0xFF, 0xFF, 0xFF, 0x7F,
        ];

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<Parent>(bytes)
        );
    }

    [TestMethod]
    public void NegativeCollectionLength_Throws()
    {
        byte[] bytes = [4, 0xFE, 0xFF, 0xFF, 0xFF];

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<Parent>(bytes)
        );
    }

    [TestMethod]
    public void CorruptStringLength_Throws()
    {
        var value = new SimpleObject { Name = "abc" };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        // Overstate the string length so the reader has to reject it.
        bytes[6] = 0xFF;

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<SimpleObject>(bytes)
        );
    }

    [TestMethod]
    public void UnpairedSurrogate_IsRejectedRatherThanReplaced()
    {
        var value = new SimpleObject { Name = "\uD800" };

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Serialize(in value)
        );
    }

    [TestMethod]
    public void SelfNestingAtDepthLimit_Throws()
    {
        var root = new Node { Value = 0 };
        Node current = root;
        for (int i = 0; i < 2000; i++)
        {
            current.Next = new Node { Value = i };
            current = current.Next;
        }

        // A chain this long must fail loudly instead of overflowing the stack.
        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Serialize(in root)
        );
    }

    [TestMethod]
    public void BufferWriterTooSmall_Throws()
    {
        var buffer = new TinyBufferWriter(1);
        var value = new SimpleObject { Id = 1 };

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Serialize(ref buffer, in value)
        );
    }

    [TestMethod]
    public void MemberCountAboveTheWireLimit_IsACompileTimeError()
    {
        // The generator and the writer must agree on the cap, so pin the shared constant. Boxing
        // keeps the analyzer from folding it away as a compile-time tautology.
        object cap = TightPackCode.ReferenceId - 1;
        object nullSentinel = TightPackCode.NullObject;
        Assert.AreEqual(249, (int)cap, "member-count cap changed");
        Assert.AreEqual(byte.MaxValue, (byte)nullSentinel, "null sentinel changed");
    }

    [TestMethod]
    public void BlittableArray_RejectsALengthLargerThanTheBufferAllows()
    {
        // header + an array header claiming 1000 elements when only a few bytes follow
        byte[] bytes = [1, 0xE8, 0x03, 0x00, 0x00, 1, 2, 3];

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<BigArray>(bytes)
        );
    }

    /// <summary>A writer that never provides more than it was told to.</summary>
    private sealed class TinyBufferWriter(int capacity) : IBufferWriter<byte>
    {
        private byte[] _buffer = new byte[capacity];

        public void Advance(int count)
        {
        }

        public Memory<byte> GetMemory(int sizeHint = 0) => new byte[Math.Min(sizeHint, capacity)];

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            if (sizeHint > capacity)
            {
                return Array.Empty<byte>();
            }

            return _buffer.AsSpan(0, capacity);
        }
    }
}
