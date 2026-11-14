using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

[TestClass]
public class StockCatalogTests
{
    [TestMethod]
    public void Build_ProducesTheSameHashesEveryRun()
    {
        var first = StockCatalog.Build(AppContext.BaseDirectory);
        var second = StockCatalog.Build(AppContext.BaseDirectory);

        Assert.IsTrue(first.Count > 0);
        Assert.AreEqual(first.Count, second.Count);
        CollectionAssert.AreEqual(
            first.Select(p => p.Hash).ToArray(),
            second.Select(p => p.Hash).ToArray()
        );
    }

    [TestMethod]
    public void Build_IncludesCarsAndStagePiecesUnderTheirGameplayIds()
    {
        var packages = StockCatalog.Build(AppContext.BaseDirectory);

        Assert.IsTrue(packages.Any(p => p.Id == "nfmm/2000tornados" && p.Kind == AssetKind.Car));
        Assert.IsTrue(packages.Any(p => p.Id == "nfmm/road" && p.Kind == AssetKind.StagePiece));
        Assert.AreEqual(packages.Count, packages.Select(p => p.Hash).Distinct().Count());
    }

    [TestMethod]
    public void Build_PackedStockRetainsTheHashesUsedByReplays()
    {
        var root = Path.Combine(Path.GetTempPath(), "nfmw-stock-export", Guid.NewGuid().ToString("N"));
        var files = new NFMWorld.PhysicsFS.VirtualFS();
        files.MountWriteDestination(root);
        var stage = StockCatalog.PackageStage("nfm2/test", new StageLoader { Path = "nfm2/test" });
        var car = StockCatalog.PackageModel(AssetKind.Car, "nfmm/test", new Rad3d([], false, "nfmm/test"));
        using (var stream = files.OpenWrite("data/stages/nfm2/test.radpack"))
            stream.Write(stage);
        using (var stream = files.OpenWrite("data/models/nfmm/cars/test.radpack"))
            stream.Write(car);

        var packages = StockCatalog.Build(root);
        Assert.HasCount(2, packages);
        Assert.AreEqual(StockCatalog.HashOf(stage), packages.Single(p => p.Kind == AssetKind.Stage).Hash);
        Assert.AreEqual(StockCatalog.HashOf(car), packages.Single(p => p.Kind == AssetKind.Car).Hash);
    }

    [TestMethod]
    public void Find_UsesShippedFilesWithoutCreatingAnArchiveCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "nfmw-local-stock", Guid.NewGuid().ToString("N"));
        var files = new NFMWorld.PhysicsFS.VirtualFS();
        files.MountWriteDestination(root);
        var stage = StockCatalog.PackageStage("nfm2/test", new StageLoader { Path = "nfm2/test" });
        var car = StockCatalog.PackageModel(AssetKind.Car, "nfmm/test", new Rad3d([], false, "nfmm/test"));
        using (var output = files.OpenWrite("data/stages/nfm2/test.radpack"))
            output.Write(stage);
        using (var output = files.OpenWrite("data/models/nfmm/cars/test.radpack"))
            output.Write(car);

        Assert.AreEqual(StockCatalog.HashOf(stage), StockCatalog.Find(root, AssetKind.Stage, "nfm2/test")?.Hash);
        Assert.AreEqual(StockCatalog.HashOf(car), StockCatalog.Find(root, AssetKind.Car, "nfmm/test")?.Hash);
        Assert.IsNull(StockCatalog.Find(root, AssetKind.Stage, "../../outside"));
        Assert.IsNull(StockCatalog.Find(root, AssetKind.Car, "../../outside"));
        Assert.AreEqual(2, files.EnumerateWriteFiles("data", "*", SearchOption.AllDirectories).Count());
    }

    [TestMethod]
    public void Package_IsAReadableRadpackOfTheRightKind()
    {
        var packages = StockCatalog.Build(AppContext.BaseDirectory);
        var car = packages.First(p => p.Id == "nfmm/2000tornados");

        Assert.AreEqual(car.Size, car.Bytes.Length);
        Assert.AreEqual(StockCatalog.HashOf(car.Bytes), car.Hash);
        Assert.IsInstanceOfType<RadpackRad3d>(
            RadpackReader.Read(
                car.Bytes,
                car.Id,
                RadpackType.Car,
                RadpackLimits.MaxCompressedBytes,
                RadpackLimits.MaxDecompressedBytes
            )
        );
    }
}
