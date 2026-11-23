using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class ProfilesModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public int? After { get; set; }
    public IReadOnlyList<PublicUser> Profiles { get; private set; } = [];
    public int? NextCursor { get; private set; }

    public async Task OnGetAsync()
    {
        await ResolveAccountAsync();
        const int pageSize = 50;
        var users = Db.Users.Where(user => user.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var normalized = UsernamePolicy.Normalize(Query.Trim());
            users = users.Where(user => user.UsernameNormalized.Contains(normalized));
        }

        if (After is { } cursor) users = users.Where(user => user.Id < cursor);

        var rows = await users.OrderByDescending(user => user.Id).Take(pageSize + 1)
            .Select(user => new PublicUser(user.Id, user.Username, user.IsVerifiedUploader))
            .ToListAsync(HttpContext.RequestAborted);
        Profiles = rows.Take(pageSize).ToArray();
        NextCursor = rows.Count > pageSize ? Profiles[^1].Id : null;
    }
}
