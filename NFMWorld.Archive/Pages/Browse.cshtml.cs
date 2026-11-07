using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class BrowseModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }

    public IReadOnlyList<Asset> Assets { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await ResolveAccountAsync();

        var q = Db.Assets.Where(a => a.IsPublic && !a.IsTakedown);
        if (!string.IsNullOrWhiteSpace(Query))
            q = q.Where(a => a.Name.Contains(Query));
        if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse<AssetType>(Type, true, out var parsed))
            q = q.Where(a => a.Type == parsed);

        Assets = await q.OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync();
        return Page();
    }
}
