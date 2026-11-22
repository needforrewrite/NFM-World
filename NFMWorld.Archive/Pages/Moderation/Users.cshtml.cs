using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages.Moderation;

/// <summary>
/// Staff search over accounts, newest first. A tombstone still appears here so staff can find
/// and take down its content by id.
/// </summary>
public sealed class UsersModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public string Query { get; private set; } = "";
    public int? After { get; private set; }
    public int? NextCursor { get; private set; }

    public sealed record Row(
        int Id,
        string Username,
        NFMWorldLibrary.Multiplayer.UserRole Role,
        bool Banned,
        bool Muted,
        DateTimeOffset? KickedUntil,
        DateTimeOffset? DeletionEraseAt,
        bool Deleted,
        int AssetCount,
        DateTimeOffset? LastSeenAt
    );

    public async Task<IActionResult> OnGetAsync(string? query, int? after)
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();
        if (!IsModerator)
            return NotFound();

        Query = (query ?? "").Trim();
        After = after;
        const int page = 50;

        IQueryable<Data.User> q = Db.Users;
        if (Query.Length > 0)
            q = q.Where(u => EF.Functions.Like(u.Username, "%" + Sql.LikeEscape(Query) + "%", "\\"));
        if (after is { } cursor)
            q = q.Where(u => u.Id < cursor);

        var rows = await q
            .OrderByDescending(u => u.Id)
            .Take(page + 1)
            .Select(u => new Row(
                u.Id,
                u.Username,
                u.Role,
                u.BannedUntil != null && u.BannedUntil > DateTimeOffset.UtcNow,
                u.MutedUntil != null && u.MutedUntil > DateTimeOffset.UtcNow,
                u.KickedUntil,
                u.DeletionRequestedAt,
                u.DeletedAt != null,
                Db.Assets.Count(a => a.OwnerId == u.Id),
                u.LastSeenAt
            ))
            .ToListAsync();

        NextCursor = rows.Count > page ? rows[page - 1].Id : null;
        Rows = rows.Take(page).ToList();
        return Page();
    }
}
