using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Library.Test;

[TestClass]
public class SoundSpatializationTests
{
    [TestMethod]
    [DataRow(0f, 1f)]
    [DataRow(1200f, 1f)]
    [DataRow(2400f, 0.5f)]
    [DataRow(12000f, 0.1f)]
    [DataRow(15000f, 0.04f)]
    [DataRow(18000f, 0f)]
    [DataRow(20000f, 0f)]
    public void DistanceGainKeepsNearbyCarsAudibleAndFadesDistantCars(float distance, float expected)
        => Assert.AreEqual(expected, SoundSpatialization.DistanceGain(distance), 0.00001f);

    [TestMethod]
    public void DistanceGainIsBoundedAndMonotonicallyDecreasing()
    {
        var previous = 1f;
        for (var distance = 0; distance <= SoundSpatialization.MaximumDistance * 2; distance++)
        {
            var gain = SoundSpatialization.DistanceGain(distance);
            Assert.IsTrue(gain >= 0 && gain <= previous);
            previous = gain;
        }

        Assert.AreEqual(0f, SoundSpatialization.DistanceGain(float.NaN));
        Assert.AreEqual(0f, SoundSpatialization.DistanceGain(float.PositiveInfinity));
    }
}
