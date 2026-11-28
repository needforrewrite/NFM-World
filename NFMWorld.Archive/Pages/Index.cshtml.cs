using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Cache;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class IndexModel(
    ArchiveDbContext db,
    FileStorage storage,
    ArchiveOptions options
)
    : ArchivePageModel(db)
{
    public IReadOnlyList<Rating> Ratings { get; private set; } = [];
    public int AssetCount { get; private set; }
    public int TimeTrialCount { get; private set; }
    public IReadOnlyList<Asset> Assets { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        Ratings = await Db.Ratings.Where(r => r.UserId == Account!.Id).OrderBy(r => r.Gamemode).ToListAsync();
        AssetCount = await Db.Assets.CountAsync(a => a.OwnerId == Account!.Id);
        Assets = await Db.Assets
            .Where(a => a.OwnerId == Account!.Id)
            .OrderByDescending(a => a.Id)
            .Take(100)
            .ToListAsync();
        TimeTrialCount = await Db.TimeTrials.CountAsync(t => t.UserId == Account!.Id);

        return Page();
    }

    /// <summary>
    /// The account page's inline delete, so a row does not need a trip to its edit page. It
    /// applies the same rules as <c>DELETE /assets/{id}</c>.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        if (options.ReadOnly)

            return Forbid();

        var asset = await Db.Assets.FirstOrDefaultAsync(a => a.Id == id);

        if (asset is null || asset.OwnerId != Account!.Id)

            return NotFound();

        var path = asset.StoragePath;
        var hash = asset.ContentHash;
        Db.Assets.Remove(asset);
        await Db.SaveChangesAsync();

        await storage.DeleteIfUnreferencedAsync(path, Db, HttpContext.RequestAborted);
        await TimeTrialInvalidation.InvalidateForAssetAsync(Db, hash, HttpContext.RequestAborted);
        await HttpContext.PurgeAssetsAsync([hash]);

        return Redirect("/");
    }
}
