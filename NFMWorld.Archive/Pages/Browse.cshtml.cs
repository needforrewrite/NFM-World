using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class BrowseModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public int? After { get; set; }

    public IReadOnlyList<Asset> Assets { get; private set; } = [];
    public int? NextCursor { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await ResolveAccountAsync();

        var q = Db.Assets.Where(a => a.IsPublic && !a.IsTakedown);
        if (!string.IsNullOrWhiteSpace(Query))
            q = q.Where(a => a.Name.Contains(Query));
        if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse<AssetType>(Type, true, out var parsed))
            q = q.Where(a => a.Type == parsed);
        if (After is { } after)
            q = q.Where(a => a.Id < after);

        // By id, not CreatedAt: SQLite cannot ORDER BY a DateTimeOffset, and insertion order is
        // creation order for assets. Ordering it in LINQ to Objects would load the whole table.
        var rows = await q.OrderByDescending(a => a.Id).Take(101).ToListAsync();
        Assets = rows.Take(100).ToArray();
        NextCursor = rows.Count > 100 ? Assets[^1].Id : null;
        return Page();
    }
}
