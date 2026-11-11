using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;

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
