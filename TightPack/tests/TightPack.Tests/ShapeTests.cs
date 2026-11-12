namespace TightPack.Tests;

[TightPackable]
public partial record struct RecordPoint(int X, int Y);

[TightPackable]
public partial record Person(string Name, int Age)
{
    public string? Nickname { get; init; }
}

[TightPackable]
public partial class Generic<T>
{
    public T? Value { get; set; }
    public int Count { get; set; }
}

[TightPackable]
public partial class Outer
{
    [TightPackable]
    public partial class Inner
    {
        public int Value { get; set; }
    }

    public Inner? Nested { get; set; }
}

[TightPackable(SerializeLayout.Sequential)]
public partial class OrderedMembers
{
    [TightPackOrder(2)] public int Third { get; set; }

    [TightPackOrder(1)] public int Second { get; set; }

    [TightPackOrder(0)] public int First { get; set; }
}

[TightPackable]
public partial class WithIgnored
{
    public int Kept { get; set; }

    [TightPackIgnore] public int Dropped { get; set; }

    [TightPackInclude] private int _hidden = 0;

    public void SetHidden(int value) => _hidden = value;

    public int GetHidden() => _hidden;
}

[TestClass]
public sealed class ShapeTests
{
    [TestMethod]
    public void RecordStruct_RoundTrips()
    {
        var value = new RecordPoint(3, 4);
        RecordPoint result = TightPackSerializer.Deserialize<RecordPoint>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(3, result.X);
        Assert.AreEqual(4, result.Y);
    }

    [TestMethod]
    public void PositionalRecord_RoundTrips()
    {
        var value = new Person("Ada", 36) { Nickname = "A" };
        Person result = TightPackSerializer.Deserialize<Person>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual("Ada", result.Name);
        Assert.AreEqual(36, result.Age);
        Assert.AreEqual("A", result.Nickname);
    }

    [TestMethod]
    public void PositionalRecord_WithoutOptionalMember_RoundTrips()
    {
        var value = new Person("Bob", 20);
        Person result = TightPackSerializer.Deserialize<Person>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual("Bob", result.Name);
        Assert.IsNull(result.Nickname);
    }

    [TestMethod]
    public void GenericType_RoundTrips()
    {
        var value = new Generic<string> { Value = "v", Count = 2 };
        Generic<string> result = TightPackSerializer.Deserialize<Generic<string>>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.AreEqual("v", result.Value);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void NestedType_RoundTrips()
    {
        var value = new Outer { Nested = new Outer.Inner { Value = 8 } };
        Outer result = TightPackSerializer.Deserialize<Outer>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(8, result.Nested!.Value);
    }

    [TestMethod]
    public void ExplicitOrderOverridesDeclarationOrder()
    {
        var value = new OrderedMembers { First = 1, Second = 2, Third = 3 };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        // header, then First(1), Second(2), Third(3) as little-endian int32s
        Assert.AreEqual(1, BitConverter.ToInt32(bytes, 1));
        Assert.AreEqual(2, BitConverter.ToInt32(bytes, 5));
        Assert.AreEqual(3, BitConverter.ToInt32(bytes, 9));

        OrderedMembers result = TightPackSerializer.Deserialize<OrderedMembers>(bytes);
        Assert.AreEqual(1, result.First);
        Assert.AreEqual(2, result.Second);
        Assert.AreEqual(3, result.Third);
    }

    [TestMethod]
    public void IgnoredMember_IsNotSerialized()
    {
        var value = new WithIgnored { Kept = 1, Dropped = 99 };
        value.SetHidden(42);

        byte[] bytes = TightPackSerializer.Serialize(in value);

        // header + Kept + hidden, but never Dropped
        Assert.HasCount(1 + 4 + 4, bytes);

        WithIgnored result = TightPackSerializer.Deserialize<WithIgnored>(bytes);
        Assert.AreEqual(1, result.Kept);
        Assert.AreEqual(0, result.Dropped);
        Assert.AreEqual(42, result.GetHidden());
    }
}
