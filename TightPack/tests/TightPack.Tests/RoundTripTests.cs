namespace TightPack.Tests;

[TestClass]
public sealed class RoundTripTests
{
    [TestMethod]
    public void SimpleObject_RoundTrips()
    {
        var value = new SimpleObject { Id = 42, Name = "hello", Score = 1.5 };
        SimpleObject result = TightPackSerializer.Deserialize<SimpleObject>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(42, result.Id);
        Assert.AreEqual("hello", result.Name);
        Assert.AreEqual(1.5, result.Score);
    }

    [TestMethod]
    public void SimpleObject_NullString_RoundTripsAsNull()
    {
        var value = new SimpleObject { Id = 1, Name = null };
        SimpleObject result = TightPackSerializer.Deserialize<SimpleObject>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(1, result.Id);
        Assert.IsNull(result.Name);
    }

    [TestMethod]
    public void ScalarMembers_RoundTripEveryClrType()
    {
        var value = new Scalars
        {
            Bool = true,
            Byte = 200,
            SByte = -100,
            Char = 'x',
            Int16 = -12345,
            UInt16 = 54321,
            Int32 = -123456789,
            UInt32 = 3000000000,
            Int64 = -1234567890123456789,
            UInt64 = 18000000000000000000,
            Single = 1.25f,
            Double = -2.5,
            Half = (Half)0.5,
            Int128 = Int128.MinValue,
            UInt128 = UInt128.MaxValue,
            Decimal = -1234.5678m,
            Guid = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),
            DateTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            DateTimeOffset = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromMinutes(-330)),
            TimeSpan = TimeSpan.FromTicks(123456789),
            DateOnly = new DateOnly(1999, 12, 31),
            TimeOnly = new TimeOnly(23, 59, 58, 987),
            Enum = DayOfWeek.Wednesday,
        };

        Scalars result = TightPackSerializer.Deserialize<Scalars>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(value.Bool, result.Bool);
        Assert.AreEqual(value.Byte, result.Byte);
        Assert.AreEqual(value.SByte, result.SByte);
        Assert.AreEqual(value.Char, result.Char);
        Assert.AreEqual(value.Int16, result.Int16);
        Assert.AreEqual(value.UInt16, result.UInt16);
        Assert.AreEqual(value.Int32, result.Int32);
        Assert.AreEqual(value.UInt32, result.UInt32);
        Assert.AreEqual(value.Int64, result.Int64);
        Assert.AreEqual(value.UInt64, result.UInt64);
        Assert.AreEqual(value.Single, result.Single);
        Assert.AreEqual(value.Double, result.Double);
        Assert.AreEqual(value.Half, result.Half);
        Assert.AreEqual(value.Int128, result.Int128);
        Assert.AreEqual(value.UInt128, result.UInt128);
        Assert.AreEqual(value.Decimal, result.Decimal);
        Assert.AreEqual(value.Guid, result.Guid);
        Assert.AreEqual(value.DateTime, result.DateTime);
        Assert.AreEqual(value.DateTime.Kind, result.DateTime.Kind);
        Assert.AreEqual(value.DateTimeOffset, result.DateTimeOffset);
        Assert.AreEqual(value.DateTimeOffset.Offset, result.DateTimeOffset.Offset);
        Assert.AreEqual(value.TimeSpan, result.TimeSpan);
        Assert.AreEqual(value.DateOnly, result.DateOnly);
        Assert.AreEqual(value.TimeOnly, result.TimeOnly);
        Assert.AreEqual(value.Enum, result.Enum);
    }

    [TestMethod]
    public void DateTimeOffset_KeepsItsOffset()
    {
        var value = new Scalars { DateTimeOffset = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(9)) };
        Scalars result = TightPackSerializer.Deserialize<Scalars>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(TimeSpan.FromHours(9), result.DateTimeOffset.Offset);
        Assert.AreEqual(value.DateTimeOffset.UtcTicks, result.DateTimeOffset.UtcTicks);
    }

    [TestMethod]
    public void Immutable_IsRebuiltThroughItsConstructor()
    {
        var value = new Immutable(7, "seven");
        Immutable result = TightPackSerializer.Deserialize<Immutable>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(7, result.Id);
        Assert.AreEqual("seven", result.Name);
    }

    [TestMethod]
    public void Collections_RoundTrip()
    {
        var value = new Parent
        {
            First = new Child { Value = 1 },
            Children = [new Child { Value = 2 }, new Child { Value = 3 }],
            Numbers = [4, 5, 6],
            Map = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 },
        };

        Parent result = TightPackSerializer.Deserialize<Parent>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(1, result.First!.Value);
        CollectionAssert.AreEqual(new[] { 2, 3 }, result.Children!.Select(c => c.Value).ToArray());
        CollectionAssert.AreEqual(new[] { 4, 5, 6 }, result.Numbers!);
        Assert.AreEqual(1, result.Map!["a"]);
        Assert.AreEqual(2, result.Map["b"]);
    }

    [TestMethod]
    public void NullCollections_StayNull()
    {
        var value = new Parent();
        Parent result = TightPackSerializer.Deserialize<Parent>(TightPackSerializer.Serialize(in value));

        Assert.IsNull(result.First);
        Assert.IsNull(result.Children);
        Assert.IsNull(result.Numbers);
        Assert.IsNull(result.Map);
    }

    [TestMethod]
    public void ListsOfPackables_RoundTrip()
    {
        var value = new ListsOfPackables
        {
            Structs = [new TightStruct { X = 1, Y = 2 }, new TightStruct { X = 3, Y = 4 }],
            Children = [new Child { Value = 9 }],
            Primitives = [7, 8],
            Strings = ["a", null, "c"],
        };

        ListsOfPackables result = TightPackSerializer.Deserialize<ListsOfPackables>(TightPackSerializer.Serialize(in value));

        var structs = result.Structs!;
        Assert.HasCount(2, structs);
        Assert.AreEqual(1, structs[0].X);
        Assert.AreEqual(4f, structs[1].Y);
        Assert.AreEqual(9, result.Children![0].Value);
        CollectionAssert.AreEqual(new[] { 7, 8 }, result.Primitives!);
        CollectionAssert.AreEqual(new string?[] { "a", null, "c" }, result.Strings!);
    }

    [TestMethod]
    public void EmptyString_IsDistinctFromNull()
    {
        var value = new SimpleObject { Name = "" };
        SimpleObject result = TightPackSerializer.Deserialize<SimpleObject>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual("", result.Name);
    }

    [TestMethod]
    public void UnicodeString_RoundTrips()
    {
        var value = new SimpleObject { Name = "héllo \U0001F600 世界" };
        SimpleObject result = TightPackSerializer.Deserialize<SimpleObject>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(value.Name, result.Name);
    }
}
