using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

/// <summary>
/// The public page for one uploaded asset. The bytes are still served by
/// <c>/assets/{hash}</c>, which is what the game fetches; this page is the human-readable half.
/// </summary>
public sealed class AssetDetailModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    public Asset? Asset { get; private set; }

    /// <summary>Other public uploads by the same creator, newest first.</summary>
    public IReadOnlyList<(int Id, string Name, string Type)> More { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await ResolveAccountAsync();

        // Same visibility rule as the download endpoint: a hidden or taken-down row does not exist.
        Asset = await Db.Assets
            .Include(a => a.Owner)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsPublic && !a.IsTakedown);
        if (Asset is null)
            return NotFound();

        ViewData["Title"] = Asset.Name;

        if (Asset.OwnerId is { } ownerId)
        {
            // Newest first by id: SQLite cannot ORDER BY a DateTimeOffset, and ids follow creation order.
            var rows = await Db.Assets
                .Where(a => a.OwnerId == ownerId && a.Id != Asset.Id && a.IsPublic && !a.IsTakedown)
                .OrderByDescending(a => a.Id)
                .Take(6)
                .Select(a => new { a.Id, a.Name, a.Type })
                .ToListAsync();

            More = rows.Select(a => (a.Id, a.Name, a.Type.ToString())).ToList();
        }

        return Page();
    }

    public static string FormatSize(long bytes)
        => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
        };
}
