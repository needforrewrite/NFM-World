using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Test;

[TestClass]
public class AssetTagsMigrationTests
{
    [TestMethod]
    public async Task Upgrade_BackfillsExistingDescriptionsAndIsResumable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ArchiveDbContext>().UseSqlite(connection).Options;
        await using var db = new ArchiveDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261123235000_AssetVerification");
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Assets (Type, Name, Description, ContentHash, StoragePath, SizeBytes,
                IsPublic, IsTakedown, IsValidated, ReviewStatus, CreatedAt, DownloadCount)
            VALUES (1, 'Old stage', 'A #ПРИВЕТ stage', 'hash', 'path', 1,
                1, 0, 1, 0, '2026-01-01T00:00:00+00:00', 0)
            """
        );
        await migrator.MigrateAsync();
        await AssetTags.BackfillAsync(db, CancellationToken.None);
        await AssetTags.BackfillAsync(db, CancellationToken.None);
        var asset = await db.Assets.SingleAsync();
        Assert.AreEqual("#ПРИВЕТ", asset.Tags);
        Assert.AreEqual(
            1,
            await db.Assets.CountAsync(a => EF.Functions.Like(a.TagsSearch!, "%" + AssetTags.SearchText("привет") + "%"))
        );
    }
}
