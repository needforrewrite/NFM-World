using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace TightPack.Tests;

[TightPackable]
public partial class ExtendedMembers
{
    public (int A, float B) Pair { get; set; }
    public (int A, float B, int C) Triple { get; set; }
    public (int A, int B, int C, int D) Quad { get; set; }
    public (byte A, long B) Padded { get; set; }
    public Vector2 V2 { get; set; }
    public Vector3 V3 { get; set; }
    public Vector4 V4 { get; set; }
    public ImmutableArray<uint> Indices { get; set; }
    public ReadOnlyMemory<byte> Payload { get; set; }
    public IList<string>? Names { get; set; }
    public IDictionary<byte, string>? Map { get; set; }
    public IReadOnlyList<int>? ReadOnly { get; set; }
    public int? Maybe { get; set; }
}

[TightPackable]
public partial class InlineArrayHolder
{
    public InlineArray4<int> Values { get; set; }
}

[TightPackable]
public partial class SinglePaddedTuple
{
    public (byte A, long B) Value { get; set; }
}

[TestClass]
public sealed class ExtendedMemberTests
{
    [TestMethod]
    public void Tuples_RoundTrip()
    {
        var value = new ExtendedMembers
        {
            Pair = (7, 1.5f),
            Triple = (1, 2.5f, 3),
            Quad = (4, 5, 6, 7),
        };

        ExtendedMembers result = RoundTrip(value);

        Assert.AreEqual(value.Pair, result.Pair);
        Assert.AreEqual(value.Triple, result.Triple);
        Assert.AreEqual(value.Quad, result.Quad);
    }

    [TestMethod]
    public void PaddedTuple_WritesNoPaddingBytes()
    {
        var value = new SinglePaddedTuple { Value = (1, 2) };

        byte[] bytes = TightPackSerializer.Serialize(in value);

        // object header + byte + long. The tuple is 16 bytes in memory; 7 of them are padding.
        Assert.HasCount(10, bytes);
    }

    [TestMethod]
    public void Vectors_RoundTrip()
    {
        var value = new ExtendedMembers
        {
            V2 = new Vector2(1.5f, -2.5f),
            V3 = new Vector3(1.5f, -2.5f, 3.5f),
            V4 = new Vector4(1.5f, -2.5f, 3.5f, -4.5f),
        };

        ExtendedMembers result = RoundTrip(value);

        Assert.AreEqual(value.V2, result.V2);
        Assert.AreEqual(value.V3, result.V3);
        Assert.AreEqual(value.V4, result.V4);
    }

    [TestMethod]
    public void ImmutableArray_RoundTrips()
    {
        var value = new ExtendedMembers { Indices = ImmutableArray.Create(1u, 2u, 3u) };

        ExtendedMembers result = RoundTrip(value);

        CollectionAssert.AreEqual(new[] { 1u, 2u, 3u }, result.Indices.ToArray());
    }

    [TestMethod]
    public void ImmutableArray_DefaultAndEmptyStayDistinct()
    {
        Assert.IsTrue(RoundTrip(new ExtendedMembers { Indices = default }).Indices.IsDefault);
        Assert.IsFalse(RoundTrip(new ExtendedMembers { Indices = ImmutableArray<uint>.Empty }).Indices.IsDefault);
    }

    [TestMethod]
    public void ReadOnlyMemoryByte_RoundTrips()
    {
        var value = new ExtendedMembers { Payload = new byte[] { 9, 8, 7 } };

        ExtendedMembers result = RoundTrip(value);

        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, result.Payload.ToArray());
    }

    [TestMethod]
    public void InterfaceCollections_RoundTrip()
    {
        var value = new ExtendedMembers
        {
            Names = new List<string> { "a", "b" },
            Map = new Dictionary<byte, string> { [1] = "one", [2] = "two" },
            ReadOnly = new List<int> { 10, 20, 30 },
        };

        ExtendedMembers result = RoundTrip(value);

        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Names!.ToArray());
        Assert.AreEqual("two", result.Map![2]);
        CollectionAssert.AreEqual(new[] { 10, 20, 30 }, result.ReadOnly!.ToArray());
    }

    [TestMethod]
    public void InterfaceCollections_NullStaysNull()
    {
        ExtendedMembers result = RoundTrip(new ExtendedMembers { Names = null, Map = null });

        Assert.IsNull(result.Names);
        Assert.IsNull(result.Map);
    }

    [TestMethod]
    public void NullableInt_RoundTripsBothWays()
    {
        Assert.AreEqual(5, RoundTrip(new ExtendedMembers { Maybe = 5 }).Maybe);
        Assert.IsNull(RoundTrip(new ExtendedMembers { Maybe = null }).Maybe);
    }

    [TestMethod]
    public void InlineArray_RoundTrips()
    {
        var value = new InlineArrayHolder();
        InlineArray4<int> array = default;
        array[0] = 11;
        array[3] = 44;
        value.Values = array;

        InlineArrayHolder result = RoundTrip(value);

        Assert.AreEqual(11, result.Values[0]);
        Assert.AreEqual(0, result.Values[1]);
        Assert.AreEqual(44, result.Values[3]);
    }

    private static T RoundTrip<T>(T value)
        where T : ITightPackable<T> =>
        TightPackSerializer.Deserialize<T>(TightPackSerializer.Serialize(in value))!;
}

/// <summary>Stands in for a type from an assembly that cannot reference TightPack.</summary>
public struct ForeignStruct
{
    public int Value;
}

public sealed class ForeignStructFormatter : TightPackFormatter<ForeignStruct>
{
    public static readonly ForeignStructFormatter Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ForeignStruct value
    ) => writer.WriteInt32(value.Value);

    public override void Deserialize(ref TightPackReader reader, ref ForeignStruct value) =>
        value.Value = reader.ReadInt32();
}

[TightPackable]
[TightPackAllowSerialize]
public partial class ForeignHolder
{
    public ForeignStruct Foreign { get; set; }
    public int Plain { get; set; }
}

internal static class TestRegistrations
{
    [ModuleInitializer]
    internal static void Initialize() => TightPackFormatterProvider.Register(new ForeignStructFormatter());
}

[TestClass]
public sealed class AllowSerializeTests
{
    [TestMethod]
    public void TypeLevelAllowSerialize_UsesTheRegisteredFormatter()
    {
        var value = new ForeignHolder { Foreign = new ForeignStruct { Value = 77 }, Plain = 3 };

        ForeignHolder result = TightPackSerializer.Deserialize<ForeignHolder>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.AreEqual(77, result.Foreign.Value);
        Assert.AreEqual(3, result.Plain);
    }
}

public abstract partial class BaseAsset
{
    public required string Name { get; init; }
}

[TightPackable]
public partial class DerivedAsset : BaseAsset
{
    public int Size { get; init; }
}

[TightPackable]
[TightPackUnion(0, typeof(MeshAsset))]
public abstract partial class AssetRoot
{
    public required int Version { get; init; }
}

[TightPackable]
public partial class MeshAsset : AssetRoot
{
    public required string Name { get; init; }
    public int Size { get; init; }
}

[TestClass]
public sealed class InheritanceTests
{
    [TestMethod]
    public void BaseMembers_SerializeBeforeDerivedOnes()
    {
        var value = new DerivedAsset { Name = "car", Size = 9 };

        DerivedAsset result = TightPackSerializer.Deserialize<DerivedAsset>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.AreEqual("car", result.Name);
        Assert.AreEqual(9, result.Size);
    }

    [TestMethod]
    public void UnionCase_CarriesItsInheritedMembers()
    {
        AssetRoot value = new MeshAsset { Version = 7, Name = "piece", Size = 4 };

        AssetRoot result = TightPackSerializer.Deserialize<AssetRoot>(
            TightPackSerializer.Serialize(in value)
        );

        var derived = result as MeshAsset;
        Assert.IsNotNull(derived);
        Assert.AreEqual(7, derived.Version);
        Assert.AreEqual("piece", derived.Name);
        Assert.AreEqual(4, derived.Size);
    }
}

[TightPackable]
public readonly partial struct InnerResult
{
    public required int Value { get; init; }
}

[TightPackable]
public readonly partial struct SnapshotWithNullableStruct
{
    public required InnerResult? Results { get; init; }
    public bool Finished { get; init; }
}

/// <summary>
/// A packable struct reached only through a wrapper has no generated code of its own to register
/// it, so the type that wraps it has to. Without that the provider falls back to building the
/// formatter by reflection, which works in a JIT test run and is invisible to the AOT compiler - so
/// the failure only appears in a published build. That makes the emitted registration the thing to
/// assert, not a runtime lookup: a static constructor runs once per type per process, so no test
/// can re-observe whether it registered.
/// </summary>
[TestClass]
public sealed class DependentRegistrationTests
{
    [TestMethod]
    public void NullableStruct_RoundTrips()
    {
        var value = new SnapshotWithNullableStruct
        {
            Results = new InnerResult { Value = 42 },
            Finished = true,
        };

        SnapshotWithNullableStruct result = TightPackSerializer.Deserialize<SnapshotWithNullableStruct>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.IsTrue(result.Finished);
        Assert.AreEqual(42, result.Results!.Value.Value);
    }

    [TestMethod]
    public void NullableStruct_NullStaysNull()
    {
        var result = TightPackSerializer.Deserialize<SnapshotWithNullableStruct>(
            TightPackSerializer.Serialize(new SnapshotWithNullableStruct { Results = null })
        );

        Assert.IsNull(result.Results);
    }
}
