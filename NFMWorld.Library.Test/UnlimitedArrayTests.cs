using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class UnlimitedArrayTests
{
    [TestMethod]
    public void Insert_WhenFull_KeepsTheInsertedItem()
    {
        // Regression: Insert took the grow path without incrementing _size, so inserting
        // into a full array silently dropped the last element instead.
        var array = new UnlimitedArray<int>();
        for (var i = 0; i < 4; i++)
            array.Add(i);

        array.Insert(2, 99);

        CollectionAssert.AreEqual(new[] { 0, 1, 99, 2, 3 }, array.ToArray());
        Assert.AreEqual(5, array.Count);
    }

    [TestMethod]
    public void Insert_WhenNotFull_ShiftsAndGrows()
    {
        var array = new UnlimitedArray<int>(8) { 0, 1, 3 };

        array.Insert(2, 2);

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, array.ToArray());
    }

    [TestMethod]
    public void Insert_AtEnd_Appends()
    {
        var array = new UnlimitedArray<int> { 0, 1 };

        array.Insert(2, 2);

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, array.ToArray());
    }
}
