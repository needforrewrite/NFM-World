using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Moderation;

/// <summary>
/// Takes down or restores one asset. The user-scoped <c>Unlist</c>/<c>Relist</c> actions hit
/// every asset an account owns; this is the single-asset form the moderation pages use.
/// </summary>
public static class AssetModeration
{
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
        return true;
    }
}
