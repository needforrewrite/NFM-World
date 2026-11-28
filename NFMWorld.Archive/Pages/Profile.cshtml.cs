using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed record PublicUser(int Id, string Username, bool IsVerifiedUploader);

public sealed class ProfileModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    public PublicUser Profile { get; private set; } = null!;
    public IReadOnlyList<Asset> Assets { get; private set; } = [];
    public int? NextCursor { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, int? after)
    {
        await ResolveAccountAsync();
        var ct = HttpContext.RequestAborted;
        var profile = await Db.Users.Where(user => user.Id == id && user.DeletedAt == null)
            .Select(user => new PublicUser(user.Id, user.Username, user.IsVerifiedUploader))
            .FirstOrDefaultAsync(ct);

        if (profile is null)

            return NotFound();

        Profile = profile;
        const int pageSize = 50;
        var query = Db.Assets.Listed().Where(asset => asset.OwnerId == id);
        if (after is { } cursor)
            query = query.Where(asset => asset.Id < cursor);
        var rows = await query.OrderByDescending(asset => asset.Id).Take(pageSize + 1).ToListAsync(ct);
        Assets = rows.Take(pageSize).ToArray();
        NextCursor = rows.Count > pageSize ? Assets[^1].Id : null;

        return Page();
    }
}
