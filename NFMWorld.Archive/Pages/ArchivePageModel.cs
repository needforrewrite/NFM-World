using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Endpoints;

namespace NFMWorld.Archive.Pages;

/// <summary>
/// Shared page base: resolves the signed-in account from the session cookie once per request.
/// </summary>
public abstract class ArchivePageModel(ArchiveDbContext db) : PageModel
{
    protected ArchiveDbContext Db { get; } = db;

    /// <summary>Public because Razor views read it; the setter stays internal to the page.</summary>
    public User? Account { get; private set; }

    public bool IsSignedIn => Account is not null;

    /// <summary>Resolves the cookie session. Pages that need an account call this and redirect.</summary>
    protected async Task<User?> ResolveAccountAsync() => Account ??= await HttpContext.GetUserAsync(Db, HttpContext.RequestAborted);

    protected IActionResult RedirectToLogin()
        => Redirect("/account/login?returnUrl=" + Uri.EscapeDataString(Request.Path + Request.QueryString));
}
