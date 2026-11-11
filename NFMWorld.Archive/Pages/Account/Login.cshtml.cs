using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Endpoints;

namespace NFMWorld.Archive.Pages.Account;

public sealed class LoginModel(
    ArchiveDbContext db,
    SessionService sessions,
    ArchiveOptions options,
    LoginThrottle throttle,
    TurnstileVerifier turnstile
)
    : ArchivePageModel(db)
{
    [BindProperty] public string? Username { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? RegisterPassword { get; set; }
    [BindProperty] public string? ReturnUrl { get; set; }

    [BindProperty(Name = "cf-turnstile-response")]
    public string? TurnstileResponse { get; set; }

    public string? Error { get; private set; }

    public IActionResult OnGet(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostSignInAsync()
    {
        var normalized = Username is null ? "" : UsernamePolicy.Normalize(Username);
        var ipKey = "ip:" + (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var nameKey = "name:" + normalized;

        // The same limiter the JSON endpoint uses, so the form cannot bypass it.
        if (throttle.IsBlocked(ipKey, out _) || throttle.IsBlocked(nameKey, out _))
        {
            Error = "Too many attempts. Try again in a few minutes.";
            return Page();
        }

        if (!await VerifyTurnstileAsync("signin"))
            return Page();

        var user = await Db.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized);
        var hash = user?.PasswordHash ?? PasswordHasher.DummyHash;
        var valid = user is not null && PasswordHasher.Verify(Password ?? "", hash);

        if (!valid)
        {
            throttle.RecordFailure(ipKey);
            throttle.RecordFailure(nameKey);
            AuthEndpoints.Audit(Db, user?.Id, "web_login_failed", null, HttpContext);
            await Db.SaveChangesAsync();
            Error = "Wrong username or password.";
            return Page();
        }

        var token = await sessions.CreateAsync(
            user!,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.RequestAborted
        );
        AuthEndpoints.Audit(Db, user!.Id, "web_login", null, HttpContext);
        await Db.SaveChangesAsync();
        AuthEndpoints.SetSessionCookie(HttpContext, token, options);

        return Redirect(SafeReturnUrl(ReturnUrl));
    }

    public async Task<IActionResult> OnPostRegisterAsync()
    {
        if (options.ReadOnly)
        {
            Error = "This archive is not accepting new accounts.";
            return Page();
        }

        var ipKey = "ip:" + (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (throttle.IsBlocked(ipKey, out _))
        {
            Error = "Too many attempts. Try again in a few minutes.";
            return Page();
        }

        if (!await VerifyTurnstileAsync("register"))
            return Page();

        if (!UsernamePolicy.TryValidate(Username, out var nameError))
        {
            Error = nameError;
            return Page();
        }

        if (!PasswordPolicy.TryValidate(RegisterPassword, out var passwordError))
        {
            Error = passwordError;
            return Page();
        }

        var normalized = UsernamePolicy.Normalize(Username!);
        if (await Db.Users.AnyAsync(u => u.UsernameNormalized == normalized))
        {
            Error = "That username is taken.";
            return Page();
        }

        var user = new User
        {
            Username = Username!,
            UsernameNormalized = normalized,
            PasswordHash = PasswordHasher.Hash(RegisterPassword!)
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var token = await sessions.CreateAsync(
            user,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.RequestAborted
        );
        throttle.RecordFailure(ipKey);
        AuthEndpoints.Audit(Db, user.Id, "web_register", null, HttpContext);
        await Db.SaveChangesAsync();
        AuthEndpoints.SetSessionCookie(HttpContext, token, options);

        return Redirect("/");
    }

    private async Task<bool> VerifyTurnstileAsync(string action)
    {
        if (await turnstile.VerifyAsync(
                TurnstileResponse,
                action,
                HttpContext.RequestAborted,
                HttpContext.Connection.RemoteIpAddress?.ToString()
            ))
            return true;
        Error = TurnstileVerifier.Error;
        return false;
    }

    /// <summary>Only same-site paths: an open redirect here would send a freshly signed-in user anywhere.</summary>
    private string SafeReturnUrl(string? url)
        => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : "/";
}
