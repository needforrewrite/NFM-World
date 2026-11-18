using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages.Moderation;

/// <summary>
/// The same list as <c>/browse</c> without the visibility filter, so a moderator can find a
/// taken-down row, with per-row take-down/restore.
/// </summary>
public sealed class AssetsModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    public IReadOnlyList<Asset> Rows { get; private set; } = [];
    public int? After { get; private set; }
    public int? NextCursor { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? after)
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();
        if (!IsModerator)
            return NotFound();

        After = after;
        const int page = 50;

        var q = Db.Assets.Include(a => a.Owner).AsQueryable();
        if (after is { } cursor)
            q = q.Where(a => a.Id < cursor);

        var rows = await q.OrderByDescending(a => a.Id).Take(page + 1).ToListAsync();
        NextCursor = rows.Count > page ? rows[page - 1].Id : null;
        Rows = rows.Take(page).ToList();
        return Page();
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
