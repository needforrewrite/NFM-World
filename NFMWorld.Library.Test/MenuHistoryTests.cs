using NFMWorld.Gameplay;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class MenuHistoryTests
{
    private const string SavePath = "data/cfg/menu-history.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NFMWorld-menu-tests", Guid.NewGuid().ToString("N"));
    private Dictionary<Collection, UnlimitedArray<Rad3d>> _previousCars = null!;
    private VirtualFS _fs = null!;

    [TestInitialize]
    public void Initialize()
    {
        _previousCars = BackendGameSparker.cars;
        BackendGameSparker.cars = new()
        {
            [Collection.NFMM] = [Car(MenuHistory.DefaultCar), Car("nfmm/other")]
        };
        _fs = new VirtualFS();
        _fs.MountWriteDestination(_root);
        MenuHistory.Load(_fs);
    }

    [TestCleanup]
    public void Cleanup()
    {
        BackendGameSparker.cars = _previousCars;
        MenuHistory.Load(new VirtualFS());
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void ConfirmedCarSurvivesReloadAndStageChanges()
    {
        MenuHistory.Remember(car: "nfmm/other", fileSystem: _fs);
        MenuHistory.Remember(stage: "nfm2/another", fileSystem: _fs);

        MenuHistory.Load(new VirtualFS());
        Assert.AreEqual(MenuHistory.DefaultCar, MenuHistory.LastPlayed.Car);
        var reloaded = new VirtualFS();
        reloaded.MountDirectory(_root);
        MenuHistory.Load(reloaded);

        Assert.AreEqual("nfmm/other", MenuHistory.LastPlayed.Car);
        Assert.AreEqual("nfm2/another", MenuHistory.LastPlayed.Stage);
        Assert.AreEqual("nfmm/other", MenuHistory.Car?.FileName);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("null")]
    public void InvalidHistoryResetsToDefaults(string content)
    {
        MenuHistory.Remember(car: "nfmm/other", fileSystem: _fs);
        using (var writer = new StreamWriter(_fs.OpenWrite(SavePath)))
            writer.Write(content);

        MenuHistory.Load(_fs);

        Assert.AreEqual(MenuHistory.DefaultCar, MenuHistory.LastPlayed.Car);
        Assert.AreEqual(MenuHistory.DefaultStage, MenuHistory.LastPlayed.Stage);
    }

    [TestMethod]
    public void CanonicalArchiveStage_IsNotDiscardedForBeingUncachedLocally()
    {
        var hash = new string('a', 64);
        MenuHistory.Remember(stage: "archive/" + hash, fileSystem: _fs);

        MenuHistory.Load(_fs);

        Assert.AreEqual("archive/" + hash, MenuHistory.Stage);
    }

    [TestMethod]
    public void MissingHistoryResetsToDefaults()
    {
        MenuHistory.Remember(car: "nfmm/other", fileSystem: _fs);

        MenuHistory.Load(new VirtualFS());

        Assert.AreEqual(MenuHistory.DefaultCar, MenuHistory.LastPlayed.Car);
    }

    [TestMethod]
    public void SavedCarPathUsesVfsSeparatorsAndCaseInsensitiveLookup()
    {
        MenuHistory.Remember(car: "NFMM\\OTHER", fileSystem: _fs);

        Assert.AreEqual("nfmm/other", MenuHistory.Car?.FileName);
    }

    [TestMethod]
    public void RemovedCarFallsBackToInstalledDefault()
    {
        MenuHistory.Remember(car: "missing/car", fileSystem: _fs);

        Assert.AreEqual(MenuHistory.DefaultCar, MenuHistory.Car?.FileName);
    }

    [TestMethod]
    public void MissingDefaultFallsBackToAnInstalledCar()
    {
        BackendGameSparker.cars[Collection.NFMM] = [Car("nfmm/other")];

        Assert.AreEqual("nfmm/other", MenuHistory.Car?.FileName);
    }

    private static Rad3d Car(string name)
        => new([], CarStats.Default, [], null, [], [], false, [], name);
}
