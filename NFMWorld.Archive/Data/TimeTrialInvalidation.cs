using Microsoft.EntityFrameworkCore;

namespace NFMWorld.Archive.Data;

/// <summary>
/// A replay is only meaningful against the exact bytes it was simulated on. When the stage or car
/// it pinned is deleted or replaced, the recording can no longer be checked, so it stops ranking
/// and stops being served; the row is kept so the player's history does not silently disappear.
/// </summary>
public static class TimeTrialInvalidation
{
    public static async Task<int> InvalidateForAssetAsync(ArchiveDbContext db, string contentHash, CancellationToken ct)
    {
        var rows = await db.TimeTrials
            .Where(t => t.IsValid && (t.StageHash == contentHash || t.CarHash == contentHash))
            .ToListAsync(ct);

        if (rows.Count == 0)

            return 0;

        foreach (var row in rows)
            row.IsValid = false;

        await db.SaveChangesAsync(ct);

        return rows.Count;
    }
}
