using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages.Account;

/// <summary>
/// The account's own sign-in surface: linked providers, the password, and the deletion link.
/// Every credential change happens here, never in the game client.
/// </summary>
public sealed class SecurityModel(
    ArchiveDbContext db,
    SessionService sessions,
    ArchiveOptions options
)
    : ArchivePageModel(db)
{
    [BindProperty] public string? CurrentPassword { get; set; }
    [BindProperty] public string? NewPassword { get; set; }
    [BindProperty] public string? ConfirmPassword { get; set; }
    public IReadOnlyList<ExternalLogin> Providers { get; private set; } = [];
    public bool HasPassword { get; private set; }
    public string? Status { get; private set; }
    public bool StatusIsError { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        Providers = await Db.ExternalLogins.Where(l => l.UserId == Account!.Id).ToListAsync();
        HasPassword = Account!.PasswordHash is not null;
        Status = TempData["Status"] as string;
        StatusIsError = TempData["StatusError"] as bool?== true;

        return Page();
    }


    /// <summary>Adds a password to a Discord-only account, or changes the existing one.</summary>
    public async Task<IActionResult> OnPostSavePasswordAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        var user = await Db.Users.Include(u => u.ExternalLogins).FirstAsync(u => u.Id == Account!.Id);
        var hasPassword = user.PasswordHash is not null;

        if (hasPassword)
        {
            if (string.IsNullOrEmpty(CurrentPassword) || !PasswordHasher.Verify(CurrentPassword, user.PasswordHash))

                return Fail("The current password is wrong.");
        }

        if (NewPassword != ConfirmPassword)

            return Fail("The new passwords do not match.");

        if (!PasswordPolicy.TryValidate(NewPassword, out var error))

            return Fail(error);

        user.PasswordHash = PasswordHasher.Hash(NewPassword!);
        user.SecurityStamp++;
        await Db.SaveChangesAsync();
        await sessions.RevokeAllAsync(user.Id, HttpContext.RequestAborted);

        TempData["Status"] = hasPassword ? "Password changed." : "Password added.";

        return Redirect("/account/security");
    }

    /// <summary>Removes the password; refused when Discord is not linked, which would lock the account.</summary>
    public async Task<IActionResult> OnPostRemovePasswordAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        var user = await Db.Users.Include(u => u.ExternalLogins).FirstAsync(u => u.Id == Account!.Id);

        if (!user.ExternalLogins.Any())

            return Fail("Link another sign-in method before removing the password.");

        if (string.IsNullOrEmpty(CurrentPassword)
            || user.PasswordHash is not { } hash
            || !PasswordHasher.Verify(CurrentPassword, hash))

            return Fail("The current password is wrong.");

        user.PasswordHash = null;
        user.SecurityStamp++;
        await Db.SaveChangesAsync();
        await sessions.RevokeAllAsync(user.Id, HttpContext.RequestAborted);

        TempData["Status"] = "Password removed.";

        return Redirect("/account/security");
    }

    public async Task<IActionResult> OnPostUnlinkDiscordAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        var user = await Db.Users.Include(u => u.ExternalLogins).FirstAsync(u => u.Id == Account!.Id);

        if (user.PasswordHash is null)

            return Fail("Add a password before unlinking Discord.");

        await Db.ExternalLogins.Where(l => l.UserId == user.Id && l.Provider == "discord").ExecuteDeleteAsync();
        TempData["Status"] = "Discord unlinked.";

        return Redirect("/account/security");
    }

    private IActionResult Fail(string message)
    {
        TempData["Status"] = message;
        TempData["StatusError"] = true;

        return Redirect("/account/security");
    }
}
