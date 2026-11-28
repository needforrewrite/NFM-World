using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Cache;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Moderation;

/// <summary>
/// Takes down or restores one asset. The user-scoped <c>Unlist</c>/<c>Relist</c> actions hit
/// every asset an account owns; this is the single-asset form the moderation pages use.
/// </summary>
public static class AssetModeration
{
    public static async Task<bool> ReviewAsync(
        ArchiveDbContext db,
        int assetId,
        string contentHash,
        AssetReviewStatus status,
        User actor,
        HttpContext http,
        CancellationToken ct
    )
    {
        if (!actor.IsModerator || status is not (AssetReviewStatus.Verified or AssetReviewStatus.Rejected))

            return false;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId && a.ContentHash == contentHash, ct);

        if (asset is null)

            return false;
        if (asset.ReviewStatus == status)

            return true;

        asset.ReviewStatus = status;
        db.ModerationActions.Add(new ModerationAction
        {
            ActorUserId = actor.Id,
            ActorName = actor.Username,
            TargetUserId = asset.OwnerId,
            TargetName = asset.Name,
            Action = status == AssetReviewStatus.Verified ? ModerationActionKind.VerifyAsset : ModerationActionKind.RejectAsset,
            Reason = $"Asset #{asset.Id} ({contentHash})"
        });
        await db.SaveChangesAsync(ct);
        if (status == AssetReviewStatus.Rejected)
            await TimeTrialInvalidation.InvalidateForAssetAsync(db, contentHash, ct);
        await transaction.CommitAsync(ct);
        await http.PurgeAssetsAsync([contentHash]);

        return true;
    }

    public static async Task<bool> SetTakedownAsync(
        ArchiveDbContext db,
        int assetId,
        bool isTakedown,
        User actor,
        HttpContext http,
        CancellationToken ct
    )
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);

        if (asset is null)

            return false;

        asset.IsTakedown = isTakedown;

        db.ModerationActions.Add(
            new ModerationAction
            {
                ActorUserId = actor.Id,
                ActorName = actor.Username,
                TargetUserId = asset.OwnerId,
                TargetName = asset.Name,
                Action = isTakedown ? ModerationActionKind.Unlist : ModerationActionKind.Relist
            }
        );

        db.AuthAudit.Add(
            new AuthAudit
            {
                UserId = actor.Id,
                Event = "moderation",
                Detail = isTakedown ? "unlist" : "relist",
                IpAddress = http.Connection.RemoteIpAddress?.ToString()
            }
        );

        await db.SaveChangesAsync(ct);

        // The edge copy does not know the row was hidden: its 200 would outlive the takedown.
        await http.PurgeAssetsAsync([asset.ContentHash]);

        return true;
    }
}
