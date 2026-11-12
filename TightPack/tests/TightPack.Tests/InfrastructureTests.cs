using System.Buffers;

namespace TightPack.Tests;

[TestClass]
public sealed class CallbackTests
{
    [TestMethod]
    public void Callbacks_FireInWireOrder()
    {
        var value = new WithCallbacks { Value = 1 };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        CollectionAssert.AreEqual(new[] { "serializing", "serialized" }, value.Log);

        WithCallbacks result = TightPackSerializer.Deserialize<WithCallbacks>(bytes);
        CollectionAssert.AreEqual(new[] { "deserializing", "deserialized" }, result.Log);
        Assert.AreEqual(1, result.Value);
    }
}

[TestClass]
public sealed class FormatterProviderTests
{
    [TestMethod]
    public void Provider_ResolvesAFormatterForAKnownCollection()
    {
        Assert.IsTrue(TightPackFormatterProvider.TryGetFormatter<List<int>>(out _));
    }

    [TestMethod]
    public void Provider_ResolvesNullableOfAKnownScalar()
    {
        Assert.IsTrue(TightPackFormatterProvider.TryGetFormatter<int?>(out _));
    }

    [TestMethod]
    public void Provider_ResolvesAnArrayElement()
    {
        Assert.IsTrue(TightPackFormatterProvider.TryGetFormatter<int[]>(out _));
    }

    [TestMethod]
    public void Provider_ResolvesAGeneratedType()
    {
        Assert.IsTrue(TightPackFormatterProvider.TryGetFormatter<Child>(out _));
    }

    [TestMethod]
    public void Provider_ResolvesThroughAFormatterOverload()
    {
        var value = new Child { Value = 5 };
        var formatter = TightPackFormatterProvider.GetFormatter<Child>();
        byte[] bytes = TightPackSerializer.Serialize(in value, formatter);
        Child? result = TightPackSerializer.Deserialize<Child>(bytes, formatter);

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result!.Value);
    }

    [TestMethod]
    public void Provider_UnregisteredTypeThrows()
    {
        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackFormatterProvider.GetFormatter<Unregistered>()
        );
    }

    /// <summary>
    /// A type with no registration has its formatter built reflectively, and it must be built once:
    /// the collection formatters ask the provider for their element formatter on every call.
    /// </summary>
    [TestMethod]
    public void Provider_BuildsAFallbackFormatterOnlyOnce()
    {
        Assert.AreSame(
            TightPackFormatterProvider.GetFormatter<List<int>>(),
            TightPackFormatterProvider.GetFormatter<List<int>>()
        );
    }

    [TestMethod]
    public void Provider_ResolvesAgainAfterTheRegistrationChanges()
    {
        TightPackFormatterProvider.GetFormatter<List<ulong>>();
        var replacement = new Formatters.ListFormatter<ulong>();

        try
        {
            TightPackFormatterProvider.Register(replacement);
            Assert.AreSame(replacement, TightPackFormatterProvider.GetFormatter<List<ulong>>());

            TightPackFormatterProvider.Unregister(typeof(List<ulong>));
            Assert.AreNotSame(replacement, TightPackFormatterProvider.GetFormatter<List<ulong>>());
        }
        finally
        {
            TightPackFormatterProvider.Unregister(typeof(List<ulong>));
        }
    }

    private sealed class Unregistered
    {
    }
}

[TestClass]
public sealed class StreamAndSequenceTests
{
    [TestMethod]
    public void Stream_RoundTrips()
    {
        var value = new SimpleObject { Id = 11, Name = "stream" };
        using var stream = new MemoryStream();
        TightPackSerializer.Serialize(stream, in value);
        stream.Position = 0;

        SimpleObject result = TightPackSerializer.Deserialize<SimpleObject>(stream);
        Assert.AreEqual(11, result.Id);
        Assert.AreEqual("stream", result.Name);
    }

    [TestMethod]
    public void MultiSegmentSequence_RoundTrips()
    {
        var value = new SimpleObject { Id = 3, Name = "seg" };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        // Split the payload across two segments so the reader has to join them.
        var first = new Segment(bytes.AsMemory(0, 3));
        var last = first.Append(bytes.AsMemory(3, bytes.Length - 3));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        // Deserializes as one payload despite living in two segments.

        SimpleObject result = TightPackSerializer.Deserialize<SimpleObject>(in sequence);
        Assert.AreEqual(3, result.Id);
        Assert.AreEqual("seg", result.Name);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    [TestMethod]
    public void DeserializeIntoExistingValue_ReportsBytesConsumed()
    {
        var value = new SimpleObject { Id = 4 };
        byte[] bytes = TightPackSerializer.Serialize(in value);
        SimpleObject? target = new();
        int consumed = TightPackSerializer.Deserialize(bytes, ref target);

        Assert.AreEqual(bytes.Length, consumed);
        Assert.AreEqual(4, target!.Id);
    }
}
