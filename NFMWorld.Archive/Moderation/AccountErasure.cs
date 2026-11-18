using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Moderation;

/// <summary>
/// Erases one account: scrubs its identity, keeps its content orphaned under the tombstone.
/// No file is deleted except the user's own replay recordings, because uploads stay published.
/// </summary>
public static class AccountErasure
{
    /// <summary>
    /// Erases one account. Returns 1 when the account was erased, 0 when it was already gone.
    /// </summary>
    public static async Task<int> EraseAsync(
        ArchiveDbContext db,
        FileStorage storage,
        User target,
        ILogger logger,
        CancellationToken ct
    )
    {
        if (target.IsDeleted)
            return 0;

        var now = DateTimeOffset.UtcNow;
        var replayPaths = await db.TimeTrials
            .Where(t => t.UserId == target.Id)
            .Select(t => t.StoragePath)
            .ToListAsync(ct);

        // One transaction: the scrub and the row deletes commit together, or the account is left
        // exactly as it was. The username overwrite satisfies the unique index and makes the
        // tombstone self-describing.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        target.Username = $"deleted-{target.Id}";
        target.UsernameNormalized = UsernamePolicy.Normalize(target.Username);
        target.PasswordHash = null;
        target.Role = NFMWorldLibrary.Multiplayer.UserRole.User;
        target.BannedUntil = null;
        target.BanReason = null;
        target.KickedUntil = null;
        target.MutedUntil = null;
        target.MuteReason = null;
        target.SecurityStamp++;
        target.DeletionRequestedAt = null;
        target.DeletedAt = now;
        target.ModerationRevision++;

        // The User row survives, so the cascade no longer fires; delete explicitly.
        await db.Sessions.Where(s => s.UserId == target.Id).ExecuteDeleteAsync(ct);
        await db.ExternalLogins.Where(l => l.UserId == target.Id).ExecuteDeleteAsync(ct);
        await db.Ratings.Where(r => r.UserId == target.Id).ExecuteDeleteAsync(ct);
        await db.ClanMembers.Where(m => m.UserId == target.Id).ExecuteDeleteAsync(ct);
        await db.Clans.Where(c => c.OwnerId == target.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.OwnerId, (int?)null), ct);
        await db.TimeTrials.Where(t => t.UserId == target.Id).ExecuteDeleteAsync(ct);

        // Match participants stay: the row still names the tombstone, so history reads
        // "deleted-42 finished 3rd" instead of losing a participant.

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Only after the rows are gone does a replay file become unreferenced, and it is the
        // user's own recording (personal data), not published content.
        foreach (var path in replayPaths)
            await storage.DeleteIfUnreferencedAsync(path, db, ct);

        logger.LogInformation("Erased account {AccountId}", target.Id);
        return 1;
    }
}
