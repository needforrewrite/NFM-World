using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class BrowseModel(ArchiveDbContext db, ArchiveOptions options) : ArchivePageModel(db)
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public int? After { get; set; }

    public IReadOnlyList<Asset> Assets { get; private set; } = [];
    public int? NextCursor { get; private set; }

    /// <summary>Where the browser build shows an upload, or null when it has no 3D view.</summary>
    public string? PreviewUrl(Asset asset) => AssetPreview.Url(options, asset.Type, asset.ContentHash);

    public async Task<IActionResult> OnGetAsync()
    {
        await ResolveAccountAsync();

        var q = Db.Assets.Listed().Include(asset => asset.Owner).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var pattern = "%" + Sql.LikeEscape(Query.Trim()) + "%";
            var tagPattern = "%" + Sql.LikeEscape(AssetTags.SearchText(Query.Trim())) + "%";
            q = q.Where(a => EF.Functions.Like(a.Name, pattern, "\\") ||
                             EF.Functions.Like(a.TagsSearch!, tagPattern, "\\"));
        }

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
