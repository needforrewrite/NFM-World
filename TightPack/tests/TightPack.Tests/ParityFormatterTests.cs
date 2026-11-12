using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO.Compression;
using TightPack.Formatters;

namespace TightPack.Tests;

/// <summary>
/// One <c>[TightPackable]</c> member per formatter added for MemoryPack parity, so the generator
/// resolves each through the provider the way a real payload does.
/// </summary>
[TightPackable]
public partial class ParityCollections
{
    public Stack<int>? Stack { get; set; }
    public Queue<int>? Queue { get; set; }
    public LinkedList<int>? Linked { get; set; }
    public SortedSet<int>? Sorted { get; set; }
    public Collection<int>? Owned { get; set; }
    public ObservableCollection<int>? Observable { get; set; }
    public ConcurrentQueue<int>? ConcurrentQueue { get; set; }
    public ConcurrentStack<int>? ConcurrentStack { get; set; }
    public ConcurrentBag<int>? ConcurrentBag { get; set; }
    public ConcurrentDictionary<int, string>? ConcurrentMap { get; set; }
    public SortedDictionary<int, string>? SortedMap { get; set; }
    public SortedList<int, string>? SortedList { get; set; }
    public PriorityQueue<int, int>? Priorities { get; set; }
    public ReadOnlyCollection<int>? ReadOnly { get; set; }
    public ReadOnlyObservableCollection<int>? ReadOnlyObservable { get; set; }
    public BlockingCollection<int>? Blocking { get; set; }
    public ArraySegment<int> Segment { get; set; }
}

[TightPackable]
public partial class ParityInterfaces
{
    public ISet<int>? MutableSet { get; set; }
    public IReadOnlySet<int>? ReadOnlySet { get; set; }
    public int[,]? Grid { get; set; }
    public int[,,]? Cube { get; set; }
    public int[,,,]? Hyper { get; set; }
}

[TightPackable]
public partial class ParityDynamic
{
    public Type? PayloadType { get; set; }
}

[TestClass]
public sealed class ParityFormatterTests
{
    [TestMethod]
    public void OrderedCollections_RoundTripInOrder()
    {
        var value = new ParityCollections
        {
            Stack = new Stack<int>(new[] { 1, 2, 3 }),
            Queue = new Queue<int>(new[] { 4, 5, 6 }),
            Linked = new LinkedList<int>(new[] { 7, 8, 9 }),
            Sorted = new SortedSet<int> { 12, 10, 11 },
            Owned = new Collection<int> { 13, 14 },
            Observable = new ObservableCollection<int> { 15, 16 },
            ConcurrentQueue = new ConcurrentQueue<int>(new[] { 17, 18 }),
            ConcurrentStack = new ConcurrentStack<int>(new[] { 19, 20 }),
            ConcurrentBag = new ConcurrentBag<int>(new[] { 21, 22 }),
            ConcurrentMap = new ConcurrentDictionary<int, string>(new Dictionary<int, string> { [1] = "a", [2] = "b" }),
            SortedMap = new SortedDictionary<int, string> { [3] = "c", [4] = "d" },
            SortedList = new SortedList<int, string> { [5] = "e", [6] = "f" },
            Priorities = new PriorityQueue<int, int>([(30, 3), (20, 2), (10, 1)]),
            ReadOnly = new ReadOnlyCollection<int>(new[] { 23, 24 }),
            ReadOnlyObservable = new ReadOnlyObservableCollection<int>(new ObservableCollection<int> { 25 }),
            Blocking = new BlockingCollection<int>(new ConcurrentQueue<int>(new[] { 26, 27 })),
            Segment = new ArraySegment<int>(new[] { 28, 29, 30 }, 1, 2),
        };

        ParityCollections result = TightPackSerializer.Deserialize<ParityCollections>(
            TightPackSerializer.Serialize(in value)
        );

        CollectionAssert.AreEqual(value.Stack!.ToArray(), result.Stack!.ToArray());
        CollectionAssert.AreEqual(value.Queue!.ToArray(), result.Queue!.ToArray());
        CollectionAssert.AreEqual(value.Linked!.ToArray(), result.Linked!.ToArray());
        CollectionAssert.AreEqual(value.Sorted!.ToArray(), result.Sorted!.ToArray());
        CollectionAssert.AreEqual(value.Owned!.ToArray(), result.Owned!.ToArray());
        CollectionAssert.AreEqual(value.Observable!.ToArray(), result.Observable!.ToArray());
        CollectionAssert.AreEqual(value.ConcurrentQueue!.ToArray(), result.ConcurrentQueue!.ToArray());
        CollectionAssert.AreEqual(value.ConcurrentStack!.ToArray(), result.ConcurrentStack!.ToArray());
        CollectionAssert.AreEquivalent(value.ConcurrentBag!.ToArray(), result.ConcurrentBag!.ToArray());
        CollectionAssert.AreEquivalent(
            value.ConcurrentMap!.ToArray(),
            result.ConcurrentMap!.ToArray()
        );
        CollectionAssert.AreEqual(value.SortedMap!.ToArray(), result.SortedMap!.ToArray());
        CollectionAssert.AreEqual(value.SortedList!.ToArray(), result.SortedList!.ToArray());
        CollectionAssert.AreEqual(
            value.Priorities!.UnorderedItems.OrderBy(i => i.Priority).ToArray(),
            result.Priorities!.UnorderedItems.OrderBy(i => i.Priority).ToArray()
        );
        CollectionAssert.AreEqual(value.ReadOnly!.ToArray(), result.ReadOnly!.ToArray());
        CollectionAssert.AreEqual(value.ReadOnlyObservable!.ToArray(), result.ReadOnlyObservable!.ToArray());
        CollectionAssert.AreEqual(value.Blocking!.ToArray(), result.Blocking!.ToArray());
        CollectionAssert.AreEqual(new[] { 29, 30 }, result.Segment.ToArray());
    }

    [TestMethod]
    public void Stack_RoundTripsTopFirst()
    {
        var value = new ParityCollections { Stack = new Stack<int>(new[] { 1, 2, 3 }) };

        ParityCollections result = TightPackSerializer.Deserialize<ParityCollections>(
            TightPackSerializer.Serialize(in value)
        );

        // Peek is the top of the stack, so this catches a payload written in the wrong direction.
        Assert.AreEqual(3, result.Stack!.Peek());
        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, result.Stack.ToArray());
    }

    [TestMethod]
    public void NullCollections_RoundTripAsNull()
    {
        var value = new ParityCollections();

        ParityCollections result = TightPackSerializer.Deserialize<ParityCollections>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.IsNull(result.Stack);
        Assert.IsNull(result.ConcurrentBag);
        Assert.IsNull(result.Blocking);
    }

    [TestMethod]
    public void SetInterfaces_RoundTripThroughHashSet()
    {
        var value = new ParityInterfaces
        {
            MutableSet = new HashSet<int> { 1, 2, 3 },
            ReadOnlySet = new HashSet<int> { 4, 5 },
        };

        ParityInterfaces result = TightPackSerializer.Deserialize<ParityInterfaces>(
            TightPackSerializer.Serialize(in value)
        );

        CollectionAssert.AreEquivalent(value.MutableSet!.ToArray(), result.MutableSet!.ToArray());
        CollectionAssert.AreEquivalent(value.ReadOnlySet!.ToArray(), result.ReadOnlySet!.ToArray());
    }

    [TestMethod]
    public void MultiDimensionalArrays_KeepTheirDynShape()
    {
        var value = new ParityInterfaces
        {
            Grid = new int[2, 3] { { 1, 2, 3 }, { 4, 5, 6 } },
            Cube = new int[2, 2, 2] { { { 1, 2 }, { 3, 4 } }, { { 5, 6 }, { 7, 8 } } },
            Hyper = new int[1, 1, 1, 2] { { { { 9, 10 } } } },
        };

        ParityInterfaces result = TightPackSerializer.Deserialize<ParityInterfaces>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.AreEqual(2, result.Grid!.GetLength(0));
        Assert.AreEqual(3, result.Grid.GetLength(1));
        Assert.AreEqual(6, result.Grid[1, 2]);
        Assert.AreEqual(8, result.Cube![1, 1, 1]);
        Assert.AreEqual(10, result.Hyper![0, 0, 0, 1]);
    }

    [TestMethod]
    public void Type_RoundTripsByName()
    {
        var value = new ParityDynamic { PayloadType = typeof(Dictionary<string, List<int>>) };

        ParityDynamic result = TightPackSerializer.Deserialize<ParityDynamic>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.AreEqual(value.PayloadType, result.PayloadType);
    }

    [TestMethod]
    public void Type_NullRoundTrips()
    {
        var value = new ParityDynamic();

        ParityDynamic result = TightPackSerializer.Deserialize<ParityDynamic>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.IsNull(result.PayloadType);
    }

    [TestMethod]
    public void BitPack_WritesOneBytePerEightBits()
    {
        bool[] flags = new bool[32];
        flags[0] = true;
        flags[31] = true;

        byte[] bytes = TightPackSerializer.Serialize(flags, BitPackFormatter.Default);
        bool[]? result = TightPackSerializer.Deserialize(bytes, BitPackFormatter.Default);

        // 4-byte count + 4 bytes of bits, against ArrayFormatter's 4 + 32.
        Assert.AreEqual(8, bytes.Length);
        CollectionAssert.AreEqual(flags, result!);
    }

    [TestMethod]
    public void BitPack_RoundTripsALengthThatIsNotAMultipleOf32()
    {
        bool[] flags = [true, false, true, false, true];

        byte[] bytes = TightPackSerializer.Serialize(flags, BitPackFormatter.Default);
        bool[]? result = TightPackSerializer.Deserialize(bytes, BitPackFormatter.Default);

        Assert.AreEqual(8, bytes.Length);
        CollectionAssert.AreEqual(flags, result!);
    }

    [TestMethod]
    public void BitPack_EmptyAndNullRoundTrip()
    {
        Assert.AreEqual(0, TightPackSerializer.Deserialize(
            TightPackSerializer.Serialize(Array.Empty<bool>(), BitPackFormatter.Default),
            BitPackFormatter.Default
        )!.Length
        );

        Assert.IsNull(TightPackSerializer.Deserialize(
            TightPackSerializer.Serialize((bool[]?)null, BitPackFormatter.Default),
            BitPackFormatter.Default
        )
        );
    }

    [TestMethod]
    public void Utf16String_RoundTripsALoneSurrogate()
    {
        // A lone surrogate cannot be encoded as UTF-8, which is the reason this formatter exists.
        string value = "ab\uD800cd";

        byte[] bytes = TightPackSerializer.Serialize(value, Utf16StringFormatter.Default);
        string? result = TightPackSerializer.Deserialize(bytes, Utf16StringFormatter.Default);

        Assert.AreEqual(value, result);
    }

    [TestMethod]
    public void Utf8String_RejectsALoneSurrogate()
    {
        Assert.ThrowsExactly<TightPackSerializationException>(() =>
            TightPackSerializer.Serialize("ab\uD800cd", Utf8StringFormatter.Default));
    }

    [TestMethod]
    public void InternString_ReturnsThePooledInstance()
    {
        string value = new string('x', 8) + Guid.NewGuid();

        string? result = TightPackSerializer.Deserialize(
            TightPackSerializer.Serialize(value, InternStringFormatter.Default),
            InternStringFormatter.Default
        );

        Assert.AreEqual(value, result);
        Assert.IsTrue(ReferenceEquals(string.Intern(value), result));
    }

    [TestMethod]
    public void Brotli_RoundTripsBytes()
    {
        byte[] value = System.Text.Encoding.UTF8.GetBytes(new string('a', 4096));

        byte[] bytes = TightPackSerializer.Serialize(value, BrotliFormatter.Default);
        byte[]? result = TightPackSerializer.Deserialize(bytes, BrotliFormatter.Default);

        CollectionAssert.AreEqual(value, result!);
        Assert.IsTrue(bytes.Length < value.Length / 4, "repetitive input should compress hard");
    }

    [TestMethod]
    public void Brotli_EmptyAndNullRoundTrip()
    {
        var formatter = BrotliFormatter.Default;

        Assert.AreEqual(0, TightPackSerializer.Deserialize(
            TightPackSerializer.Serialize(Array.Empty<byte>(), formatter),
            formatter
        )!.Length
        );

        Assert.IsNull(TightPackSerializer.Deserialize(
            TightPackSerializer.Serialize((byte[]?)null, formatter),
            formatter
        )
        );
    }

    [TestMethod]
    public void Brotli_RefusesAPayloadOverTheLimit()
    {
        var limited = new BrotliFormatter(CompressionLevel.Fastest, decompressionSizeLimit: 16);
        byte[] value = new byte[64];

        byte[] bytes = TightPackSerializer.Serialize(value, BrotliFormatter.Default);

        Assert.ThrowsExactly<TightPackSerializationException>(() =>
            TightPackSerializer.Deserialize(bytes, limited));
    }

    [TestMethod]
    public void BrotliString_RoundTrips()
    {
        string value = string.Concat(Enumerable.Repeat("nfm-world ", 128));

        byte[] bytes = TightPackSerializer.Serialize(value, BrotliStringFormatter.Default);
        string? result = TightPackSerializer.Deserialize(bytes, BrotliStringFormatter.Default);

        Assert.AreEqual(value, result);
        Assert.IsTrue(bytes.Length < value.Length, "the payload should be smaller than the text");
    }

    [TestMethod]
    public void BrotliGeneric_RoundTripsAStruct()
    {
        var formatter = new BrotliFormatter<TightStruct>();
        var value = new TightStruct { X = 7, Y = 1.5f };

        byte[] bytes = TightPackSerializer.Serialize(value, formatter);
        TightStruct result = TightPackSerializer.Deserialize(bytes, formatter);

        Assert.AreEqual(value, result);
    }

    [TestMethod]
    public void DynamicUnion_RoundTripsEachCase()
    {
        var formatter = new DynamicUnionFormatter<DynShape>(
            (1, typeof(DynCircle)),
            (2, typeof(DynSquare))
        );

        DynShape circle = new DynCircle { Radius = 3 };
        DynShape square = new DynSquare { Side = 4 };

        Assert.AreEqual(3, ((DynCircle)TightPackSerializer.Deserialize(
            TightPackSerializer.Serialize(circle, formatter),
            formatter
        )!).Radius
        );

        Assert.AreEqual(4, ((DynSquare)TightPackSerializer.Deserialize(
            TightPackSerializer.Serialize(square, formatter),
            formatter
        )!).Side
        );
    }

    [TestMethod]
    public void DynamicUnion_RefusesAnUnmappedType()
    {
        var formatter = new DynamicUnionFormatter<DynShape>((1, typeof(DynCircle)));

        Assert.ThrowsExactly<TightPackSerializationException>(() =>
            TightPackSerializer.Serialize<DynShape>(new DynSquare { Side = 1 }, formatter));
    }

    [TestMethod]
    public void DynamicUnion_RefusesADuplicateTag()
    {
        Assert.ThrowsExactly<TightPackSerializationException>(() =>
            _ = new DynamicUnionFormatter<DynShape>((1, typeof(DynCircle)), (1, typeof(DynSquare))));
    }
}

[TightPackUnion(1, typeof(DynCircle))]
[TightPackUnion(2, typeof(DynSquare))]
[TightPackable(GenerateType.Union)]
public partial interface DynShape;

[TightPackable]
public partial class DynCircle : DynShape
{
    public int Radius { get; set; }
}

[TightPackable]
public partial class DynSquare : DynShape
{
    public int Side { get; set; }
}
