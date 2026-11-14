using Microsoft.EntityFrameworkCore;

namespace NFMWorld.Archive.Data;

public static class StockAssetCleanup
{
    public const string LegacyDescription = "Base game content";

    public static async Task<int> RunAsync(ArchiveDbContext db, FileStorage storage, ILogger logger, CancellationToken ct)
    {
        var seeded = await db.Assets.Where(asset => asset.OwnerId == null && asset.Description == LegacyDescription)
            .ToListAsync(ct);
        if (seeded.Count == 0)
            return 0;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Assets.RemoveRange(seeded);
        await db.SaveChangesAsync(ct);
        foreach (var path in seeded.Select(asset => asset.StoragePath).Distinct())
            await storage.DeleteIfUnreferencedAsync(path, db, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Removed {Count} previously seeded stock assets", seeded.Count);
        return seeded.Count;
    }
}
