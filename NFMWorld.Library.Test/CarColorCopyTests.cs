using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class CarColorCopyTests
{
    [TestMethod]
    public void Match_FourChannelsToOne_CopiesOnlyTheDefinedChannel()
    {
        var source = Enumerable.Range(0, 4)
            .Select(slot => new CarColorSlot(slot, new Color3((short)(40 + slot), 80, 120))).ToArray();
        var result = CarColorCopy.Match([new CarColorSlot(0, default)], source);

        CollectionAssert.AreEqual(new[] { source[0] }, result.ToArray());
    }

    [TestMethod]
    public void Match_SparseChannels_MatchesSlotNumbersAndPreservesMissingChannels()
    {
        var result = CarColorCopy.Match(
            [new CarColorSlot(1, default), new CarColorSlot(3, default)],
            [new CarColorSlot(0, new Color3(40, 50, 60)), new CarColorSlot(3, new Color3(90, 100, 110))]
        );

        CollectionAssert.AreEqual(new[] { new CarColorSlot(3, new Color3(90, 100, 110)) }, result.ToArray());
    }

    [TestMethod]
    public void Match_EmptyOrInvalidChannels_ReturnsNoColors()
    {
        Assert.AreEqual(0, CarColorCopy.Match([], [new CarColorSlot(0, default)]).Count);
        Assert.AreEqual(0, CarColorCopy.Match([new CarColorSlot(0, default)], []).Count);
        Assert.AreEqual(
            0,
            CarColorCopy.Match(
                [new CarColorSlot(-1, default), new CarColorSlot(4, default)],
                [new CarColorSlot(-1, default), new CarColorSlot(4, default)]
            ).Count
        );
    }

    [TestMethod]
    public void Match_ClampsIncomingColors()
    {
        var result = CarColorCopy.Match(
            [new CarColorSlot(0, default)],
            [new CarColorSlot(0, new Color3(-10, 300, 20))]
        );

        Assert.AreEqual(new Color3(15, 255, 20), result[0].Color);
    }
}
