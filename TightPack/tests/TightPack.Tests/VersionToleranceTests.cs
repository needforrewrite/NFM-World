namespace TightPack.Tests;

[TestClass]
public sealed class VersionToleranceTests
{
    [TestMethod]
    public void VersionTolerant_RoundTrips()
    {
        var value = new Evolvable { First = 1, Second = "two", Third = 3 };
        Evolvable result = TightPackSerializer.Deserialize<Evolvable>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(1, result.First);
        Assert.AreEqual("two", result.Second);
        Assert.AreEqual(3, result.Third);
    }

    [TestMethod]
    public void VersionTolerant_TrailingMemberCanBeOmitted()
    {
        // Simulates an older writer that only knew First and Second: write a three-member payload by
        // hand with the same layout so the reader must accept the shorter count.
        var value = new Evolvable { First = 10, Second = "s", Third = 30 };
        byte[] full = TightPackSerializer.Serialize(in value);

        // Drop the last member, keeping each remaining length prefix intact.
        var older = new List<byte> { 2 };
        int index = 1;
        for (int i = 0; i < 2; i++)
        {
            int start = index;
            int length = ReadVarUInt(full, ref index);
            older.AddRange(full[start..(index + length)]);
            index += length;
        }

        Evolvable result = TightPackSerializer.Deserialize<Evolvable>(older.ToArray());

        Assert.AreEqual(10, result.First);
        Assert.AreEqual("s", result.Second);
        Assert.AreEqual(0, result.Third);
    }

    [TestMethod]
    public void VersionTolerant_UnknownTrailingMemberIsSkipped()
    {
        var value = new Evolvable { First = 10, Second = "s", Third = 30 };
        byte[] full = TightPackSerializer.Serialize(in value);

        // Append an unknown fourth member and bump the header, as a newer writer would.
        var newer = new List<byte> { 4 };
        newer.AddRange(full[1..]);
        newer.Add(1);
        newer.Add(0xAB);

        Evolvable result = TightPackSerializer.Deserialize<Evolvable>(newer.ToArray());

        Assert.AreEqual(10, result.First);
        Assert.AreEqual("s", result.Second);
        Assert.AreEqual(30, result.Third);
    }

    [TestMethod]
    public void VersionTolerant_TruncatedMemberThrows()
    {
        var value = new Evolvable { First = 1 };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        var corrupt = bytes.ToArray();
        corrupt[^1] ^= 0xFF;
        corrupt = corrupt[..^1];

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<Evolvable>(corrupt)
        );
    }

    [TestMethod]
    public void Object_RejectsAMemberCountMismatch()
    {
        var value = new SimpleObject { Id = 1 };
        byte[] bytes = TightPackSerializer.Serialize(in value);

        var wrong = bytes.ToArray();
        wrong[0] = 9;

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<SimpleObject>(wrong)
        );
    }

    private static int ReadVarUInt(byte[] buffer, ref int index)
    {
        int result = 0;
        int shift = 0;
        while (true)
        {
            byte b = buffer[index++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }
    }
}
