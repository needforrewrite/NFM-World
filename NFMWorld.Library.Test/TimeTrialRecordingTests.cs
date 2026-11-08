using NFMWorldLibrary;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

[TestClass]
public class TimeTrialRecordingTests
{
    [TestMethod]
    [DataRow(10, 9, true)]
    [DataRow(10, 10, false)]
    [DataRow(10, 11, false)]
    [DataRow(10, 0, false)]
    [DataRow(0, 1, true)]
    [DataRow(0, 0, false)]
    public void FasterRecordingCanReplaceAGhostWithoutSplits(int bestTicks, int currentTicks, bool faster)
    {
        var best = Recording(bestTicks);
        var current = Recording(currentTicks);

        Assert.IsEmpty(best.Splits.SplitTimes);
        Assert.IsEmpty(current.Splits.SplitTimes);
        Assert.AreEqual(faster, current.IsFasterThan(best));
    }

    [TestMethod]
    public void ComparisonUsesGhostPlaybackLengthRatherThanWallClockSplits()
    {
        var best = Recording(10);
        best.RecordSplit(1000);
        var current = Recording(9);
        current.RecordSplit(1100);

        Assert.IsTrue(current.IsFasterThan(best));
        Assert.IsFalse(best.IsFasterThan(current));
    }

    private static SavedTimeTrial Recording(int ticks)
    {
        var recording = new SavedTimeTrial(
            "test/car",
            "test/stage",
            new StageLoader(),
            new Rad3d([], castsShadow: false, fileName: "test/car")
        );
        for (var i = 0; i < ticks; i++)
            recording.DemoData.Ticks.Add(default);
        return recording;
    }
}
