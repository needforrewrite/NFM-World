using Microsoft.EntityFrameworkCore;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Data;

/// <summary>
/// Seeds the archive with the canonical packages for the build whose data directory is configured.
/// </summary>
/// <remarks>
/// Stock cars and stages are identified by hash, not by name, so they have to be inserted from the
/// build's own content rather than uploaded by a player. Re-running is safe: an asset whose hash is
/// already catalogued is left alone.
/// </remarks>
public static class StockSeeder
{
    public static async Task<int> SeedAsync(
        ArchiveDbContext db,
        FileStorage storage,
        string shippedRoot,
        ILogger logger,
        CancellationToken ct
    )
    {
        var packages = StockCatalog.Build(shippedRoot);
        var added = 0;

        foreach (var package in packages)
        {
            if (await db.Assets.AnyAsync(a => a.ContentHash == package.Hash, ct))
                continue;

            var (hash, relative, size) = await storage.PutAsync(package.Bytes, ct);
            if (!string.Equals(hash, package.Hash, StringComparison.Ordinal))
            {
                logger.LogWarning("[Stock] Stored hash {Stored} differs from package hash {Expected}", hash, package.Hash);
                continue;
            }

            db.Assets.Add(
                new Asset
                {
                    OwnerId = null,
                    Type = (AssetType)package.Kind,
                    Name = package.Id,
                    Description = "Base game content",
                    ContentHash = hash,
                    StoragePath = relative,
                    SizeBytes = size,
                    IsPublic = true,
                    IsValidated = true
                }
            );
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation("[Stock] Seeded {Count} package(s) from {Root}", added, shippedRoot);
        return added;
    }
}
