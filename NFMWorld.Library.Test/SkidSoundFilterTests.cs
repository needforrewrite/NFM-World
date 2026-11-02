using NFMWorldLibrary;

namespace NFMWorld.Library.Test;

[TestClass]
public class SkidSoundFilterTests
{
    [TestMethod]
    [DataRow(0f, 250f, 0f)]
    [DataRow(0f, -250f, 0f)]
    [DataRow(-250f, 0f, 90f)]
    [DataRow(250f, 0f, -90f)]
    [DataRow(0f, 250f, 360f)]
    [DataRow(5f, 250f, 0f)]
    [DataRow(0f, 250f, 1f)]
    [DataRow(0f, 250f, 359.9f)]
    [DataRow(5f, 0f, 0f)]
    public void StraightMotionSmallCorrectionsAndSlowMovementDoNotSqueal(float x, float z, float heading)
        => Assert.IsFalse(SkidSoundFilter.HasLateralSlip(x, z, heading));

    [TestMethod]
    [DataRow(80f, 250f, 0f)]
    [DataRow(-80f, 250f, 0f)]
    [DataRow(80f, -250f, 0f)]
    [DataRow(-250f, 80f, 90f)]
    [DataRow(250f, 80f, -90f)]
    public void SignificantSidewaysMotionRemainsAudible(float x, float z, float heading)
        => Assert.IsTrue(SkidSoundFilter.HasLateralSlip(x, z, heading));

    [TestMethod]
    [DataRow(0f, 250f, 0f)]
    [DataRow(0f, -250f, 0f)]
    [DataRow(80f, 250f, 0f)]
    [DataRow(-80f, 250f, 0f)]
    [DataRow(-250f, 80f, 90f)]
    [DataRow(0f, 250f, 18f)]
    [DataRow(0f, 250f, 342f)]
    [DataRow(20f, 0f, 0f)]
    public void TurnsAndSmallSlidesDoNotTriggerDriftAir(float x, float z, float heading)
        => Assert.IsFalse(SkidSoundFilter.HasDriftSlip(x, z, heading));

    [TestMethod]
    [DataRow(120f, 250f, 0f)]
    [DataRow(-120f, 250f, 0f)]
    [DataRow(120f, -250f, 0f)]
    [DataRow(-250f, 120f, 90f)]
    [DataRow(250f, 120f, -90f)]
    public void PronouncedSidewaysMotionCanTriggerDriftAir(float x, float z, float heading)
        => Assert.IsTrue(SkidSoundFilter.HasDriftSlip(x, z, heading));

    [TestMethod]
    public void NonFiniteInputsDoNotTriggerSkidSounds()
    {
        Assert.IsFalse(SkidSoundFilter.HasLateralSlip(float.NaN, 250, 0));
        Assert.IsFalse(SkidSoundFilter.HasLateralSlip(0, float.PositiveInfinity, 0));
        Assert.IsFalse(SkidSoundFilter.HasLateralSlip(0, 250, float.NaN));
        Assert.IsFalse(SkidSoundFilter.HasDriftSlip(float.NaN, 250, 0));
        Assert.IsFalse(SkidSoundFilter.HasDriftSlip(0, float.PositiveInfinity, 0));
        Assert.IsFalse(SkidSoundFilter.HasDriftSlip(0, 250, float.NaN));
    }
}
