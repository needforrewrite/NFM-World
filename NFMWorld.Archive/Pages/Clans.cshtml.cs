using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class ClansModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    [BindProperty] public string? ClanName { get; set; }
    [BindProperty] public string? Tag { get; set; }
    [BindProperty] public int JoinClanId { get; set; }
    public string? Error { get; private set; }

    public IReadOnlyList<Clan> Clans { get; private set; } = [];
    public Clan? MyClan { get; private set; }
    public bool IsOwner { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await ResolveAccountAsync();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();

        await LoadAsync();
        if (MyClan is not null)
            return await Fail("You are already in a clan.");

        if (!UsernamePolicy.TryValidate(ClanName, out var nameError))
            return await Fail(nameError);

        var normalized = UsernamePolicy.Normalize(ClanName!);
        if (await Db.Clans.AnyAsync(c => c.NameNormalized == normalized))
            return await Fail("That clan name is taken.");

        var clan = new Clan { Name = ClanName!, NameNormalized = normalized, Tag = Tag, OwnerId = Account!.Id };
        Db.Clans.Add(clan);
        await Db.SaveChangesAsync();

        Db.ClanMembers.Add(new ClanMember { ClanId = clan.Id, UserId = Account!.Id });
        await Db.SaveChangesAsync();

        return RedirectToPage("/Clans");
    }

    public async Task<IActionResult> OnPostJoinAsync()
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();

        await LoadAsync();
        if (MyClan is not null)
            return await Fail("Leave your current clan first.");

        if (!await Db.Clans.AnyAsync(c => c.Id == JoinClanId))
            return await Fail("That clan does not exist.");

        Db.ClanMembers.Add(new ClanMember { ClanId = JoinClanId, UserId = Account!.Id });
        await Db.SaveChangesAsync();
        return RedirectToPage("/Clans");
    }

    public async Task<IActionResult> OnPostLeaveAsync()
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();

        await LoadAsync();
        if (MyClan is null)
            return RedirectToPage("/Clans");

        Db.ClanMembers.RemoveRange(Db.ClanMembers.Where(m => m.UserId == Account!.Id));
        await Db.SaveChangesAsync();
        return RedirectToPage("/Clans");
    }

    private async Task<IActionResult> Fail(string message)
    {
        Error = message;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Clans = await Db.Clans.Include(c => c.Members).OrderBy(c => c.Name).Take(100).ToListAsync();
        if (Account is null)
            return;

        MyClan = await Db.ClanMembers.Where(m => m.UserId == Account.Id)
            .Select(m => m.Clan)
            .FirstOrDefaultAsync();
        IsOwner = MyClan?.OwnerId == Account.Id;
    }
}
