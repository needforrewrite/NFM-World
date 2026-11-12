using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

[TestClass]
public class TimeTrialRecordingTests
{
    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), "nfmw-tt-recording", Guid.NewGuid().ToString("N"));

    static TimeTrialRecordingTests()
    {
        Directory.CreateDirectory(Root);
        TheVFS.VFS.MountWriteDestination(Root);
    }

    // Other test classes re-mount the process-wide write root.
    [TestInitialize]
    public void UseIsolatedRoot() => TheVFS.VFS.MountWriteDestination(Root);

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

    [TestMethod]
    public void RecordingCarriesCameraAnglesForPlayback()
    {
        var car = new BackendCar(TestCar(), 0, 0, 0, false);
        car.CarPhysics.Cxz = (Fixed64)123.5;
        car.CarPhysics.StaticCameraXz = (Fixed64)(-45.25);

        var recording = new SavedTimeTrial(
            "test/car",
            "test/stage",
            new StageLoader(),
            new Rad3d([], castsShadow: false, fileName: "test/car")
        );
        recording.RecordTick(car);
        recording.Save();

        var loaded = SavedTimeTrial.Load("test/car", "test/stage");
        Assert.IsNotNull(loaded);
        Assert.AreEqual(SavedTimeTrial.CURRENT_VERSION, loaded.Version);
        Assert.AreEqual(1, loaded.DemoData.Ticks.Count);

        // A replay drives the cameras off the recorded headings, so both have to survive the round
        // trip and land on the car when the frame is applied.
        var restored = new BackendCar(TestCar(), 0, 0, 0, false);
        loaded.DemoData.Ticks[0].ApplyToCar(restored);

        Assert.AreEqual(car.CarPhysics.Cxz, restored.CarPhysics.Cxz);
        Assert.AreEqual(car.CarPhysics.StaticCameraXz, restored.CarPhysics.StaticCameraXz);
    }

    private static Rad3d TestCar()
        => new([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], "test/car");

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
