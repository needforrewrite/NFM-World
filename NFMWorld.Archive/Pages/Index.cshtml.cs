using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class IndexModel(ArchiveDbContext db) : ArchivePageModel(db)
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
        Assets = await Db.Assets.Where(a => a.OwnerId == Account!.Id).OrderByDescending(a => a.Id).Take(100).ToListAsync();
        TimeTrialCount = await Db.TimeTrials.CountAsync(t => t.UserId == Account!.Id);
        return Page();
    }
}
