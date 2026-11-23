using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Moderation;

namespace NFMWorld.Archive.Pages.Moderation;

/// <summary>
/// The same list as <c>/browse</c> without the visibility filter, so a moderator can find a
/// taken-down row, with per-row take-down/restore.
/// </summary>
public sealed class AssetsModel(ArchiveDbContext db, FileStorage storage) : ArchivePageModel(db)
{
    public IReadOnlyList<Asset> Rows { get; private set; } = [];
    public int? After { get; private set; }
    public int? NextCursor { get; private set; }
    public bool PendingOnly { get; private set; }
    [TempData] public string? ReviewMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? after, bool pending = false)
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();
        if (!IsModerator)
            return NotFound();

        After = after;
        PendingOnly = pending;
        const int page = 50;

        var q = Db.Assets.Include(a => a.Owner).AsQueryable();
        if (pending)
            q = q.Where(a => a.ReviewStatus == AssetReviewStatus.Pending);
        if (after is { } cursor)
            q = q.Where(a => a.Id < cursor);

        var rows = await q.OrderByDescending(a => a.Id).Take(page + 1).ToListAsync();
        NextCursor = rows.Count > page ? rows[page - 1].Id : null;
        Rows = rows.Take(page).ToList();
        return Page();
    }

    public async Task<IActionResult> OnGetReviewFileAsync(int id, string hash)
    {
        if (await ResolveAccountAsync() is null) return RedirectToLogin();
        if (!IsModerator) return NotFound();

        var asset = await Db.Assets.FirstOrDefaultAsync(a => a.Id == id && a.ContentHash == hash, HttpContext.RequestAborted);
        if (asset is null || storage.OpenRead(asset.StoragePath) is not { } stream) return NotFound();

        var header = new byte[3];
        int length;
        try
        {
            length = await stream.ReadAtLeastAsync(header, header.Length, false, HttpContext.RequestAborted);
            stream.Position = 0;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }

        var extension = length == 3 && header.AsSpan().SequenceEqual("RAD"u8) ? ".radpack" : ".rad";
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, "application/octet-stream", $"{asset.Name}{extension}");
    }

    public Task<IActionResult> OnPostVerifyAsync(int id, string hash, bool pending)
        => ReviewAsync(id, hash, pending, AssetReviewStatus.Verified);

    public Task<IActionResult> OnPostRejectAsync(int id, string hash, bool pending)
        => ReviewAsync(id, hash, pending, AssetReviewStatus.Rejected);

    private async Task<IActionResult> ReviewAsync(int id, string hash, bool pending, AssetReviewStatus status)
    {
        if (await ResolveAccountAsync() is null) return RedirectToLogin();
        if (!IsModerator) return NotFound();

        ReviewMessage = await AssetModeration.ReviewAsync(Db, id, hash, status, Account!, HttpContext, HttpContext.RequestAborted)
            ? $"Asset {status.ToString().ToLowerInvariant()}."
            : "The file changed or was removed. Refresh the queue before reviewing it again.";
        return RedirectToPage(new { pending });
    }

    public async Task<IActionResult> OnPostTakedownAsync(int id)
        => await SetAsync(id, isTakedown: true);

    public async Task<IActionResult> OnPostRestoreAsync(int id)
        => await SetAsync(id, isTakedown: false);

    private async Task<IActionResult> SetAsync(int id, bool isTakedown)
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();
        if (!IsModerator)
            return NotFound();

        var asset = await Db.Assets.FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
            return NotFound();

        await NFMWorld.Archive.Moderation.AssetModeration.SetTakedownAsync(
            Db,
            id,
            isTakedown,
            Account!,
            HttpContext,
            HttpContext.RequestAborted
        );
        return RedirectToPage(new { after = After });
    }
}
