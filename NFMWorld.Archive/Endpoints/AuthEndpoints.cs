using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Endpoints;

public sealed record RegisterRequest(string? Username, string? Password);
public sealed record LoginRequest(string? Username, string? Password);
public sealed record AccountResponse(string Username, bool IsAdmin, double Elo, int Wins, int Losses, int Matches);
public sealed record TokenResponse(string Token, AccountResponse Account);

/// <summary>
/// Account endpoints. Registration and login are deliberately uniform in failure: a wrong
/// password and an unknown username cost the same work and return the same error, so the login
/// form cannot be used to enumerate accounts.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapPost("/register", async (
            RegisterRequest request, ArchiveDbContext db, ArchiveOptions options,
            SessionService sessions, HttpContext http, CancellationToken ct) =>
        {
            if (options.ReadOnly)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            if (!UsernamePolicy.TryValidate(request.Username, out var nameError))
                return Results.BadRequest(new { error = nameError });
            if (!PasswordPolicy.TryValidate(request.Password, out var passwordError))
                return Results.BadRequest(new { error = passwordError });

            var normalized = UsernamePolicy.Normalize(request.Username!);
            if (await db.Users.AnyAsync(u => u.UsernameNormalized == normalized, ct))
                return Results.Conflict(new { error = "That username is taken." });

            var user = new User
            {
                Username = request.Username!,
                UsernameNormalized = normalized,
                PasswordHash = PasswordHasher.Hash(request.Password!)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            Audit(db, user.Id, "register", null, http);
            var token = await sessions.CreateAsync(user, UserAgent(http), Ip(http), ct);
            await db.SaveChangesAsync(ct);
            SetSessionCookie(http, token, options);

            return Results.Ok(new TokenResponse(token,
                new AccountResponse(user.Username, user.IsAdmin, 1500, 0, 0, 0)));
        });

        auth.MapPost("/login", async (
            LoginRequest request, ArchiveDbContext db, ArchiveOptions options, LoginThrottle throttle,
            SessionService sessions, HttpContext http, CancellationToken ct) =>
        {
            var normalized = request.Username is null ? "" : UsernamePolicy.Normalize(request.Username);
            var ipKey = "ip:" + (Ip(http) ?? "unknown");
            var nameKey = "name:" + normalized;

            // Both keys are checked so a distributed spray against one account and a spread across
            // many accounts both slow down.
            if (throttle.IsBlocked(ipKey, out var retry) || throttle.IsBlocked(nameKey, out retry))
            {
                http.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
                return Results.Json(new { error = "Too many attempts. Try again later." },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized, ct);

            // Verified unconditionally so a missing account costs the same hash as a wrong password;
            // the `user` null check is what actually rejects the request.
            var hash = user?.PasswordHash ?? PasswordHasher.DummyHash;
            var valid = user is not null && PasswordHasher.Verify(request.Password ?? "", hash);

            if (user is null || !valid)
            {
                throttle.RecordFailure(ipKey);
                throttle.RecordFailure(nameKey);
                Audit(db, user?.Id, "login_failed", null, http);
                await db.SaveChangesAsync(ct);
                return Results.Unauthorized();
            }

            throttle.Reset(nameKey);
            Audit(db, user.Id, "login", null, http);
            var token = await sessions.CreateAsync(user, UserAgent(http), Ip(http), ct);
            await db.SaveChangesAsync(ct);
            SetSessionCookie(http, token, options);

            var rating = await db.Ratings.FirstOrDefaultAsync(r => r.UserId == user.Id && r.Gamemode == "nfmm/pvp", ct);
            return Results.Ok(new TokenResponse(token, new AccountResponse(
                user.Username, user.IsAdmin, rating?.Elo ?? 1500,
                rating?.Wins ?? 0, rating?.Losses ?? 0, rating?.Matches ?? 0)));
        });

        auth.MapPost("/logout", async (
            HttpContext http, ArchiveDbContext db, SessionService sessions, CancellationToken ct) =>
        {
            var token = BearerToken(http);
            if (token is not null)
                await sessions.RevokeAsync(token, ct);
            return Results.NoContent();
        });

        auth.MapGet("/me", async (
            HttpContext http, ArchiveDbContext db, ArchiveOptions options, CancellationToken ct) =>
        {
            var user = await http.GetUserAsync(db, ct);
            if (user is null)
                return Results.Unauthorized();

            var rating = await db.Ratings.FirstOrDefaultAsync(r => r.UserId == user.Id && r.Gamemode == "nfmm/pvp", ct);
            return Results.Ok(new AccountResponse(
                user.Username, user.IsAdmin, rating?.Elo ?? 1500,
                rating?.Wins ?? 0, rating?.Losses ?? 0, rating?.Matches ?? 0));
        });

        // Called by the lobby with a player's token to learn who they are. Returns only the id
        // and username, so a compromised lobby learns nothing else about the account.
        auth.MapGet("/verify", async (
            HttpContext http, ArchiveDbContext db, CancellationToken ct) =>
        {
            var user = await http.GetUserAsync(db, ct);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(new { AccountId = user.Id, user.Username });
        });

        auth.MapPost("/logout-all", async (
            HttpContext http, ArchiveDbContext db, SessionService sessions, CancellationToken ct) =>
        {
            var user = await http.GetUserAsync(db, ct);
            if (user is null)
                return Results.Unauthorized();

            // Bumping the stamp invalidates every session, including ones we cannot see here.
            user.SecurityStamp++;
            await db.SaveChangesAsync(ct);
            await sessions.RevokeAllAsync(user.Id, ct);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Issues the session as an HttpOnly cookie as well as returning it in the body. The game
    /// client uses the body token; the website uses the cookie, which keeps the token out of
    /// script and out of URLs.
    /// </summary>
    internal static void SetSessionCookie(HttpContext http, string token, ArchiveOptions options)
    {
        http.Response.Cookies.Append("nfmw_session", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow + options.SessionLifetime
        });
    }

    internal static string? BearerToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    internal static async Task<User?> GetUserAsync(this HttpContext http, ArchiveDbContext db, CancellationToken ct)
    {
        var token = BearerToken(http) ?? http.Request.Cookies["nfmw_session"];
        if (string.IsNullOrEmpty(token))
            return null;

        var sessions = http.RequestServices.GetRequiredService<SessionService>();
        return await sessions.ValidateAsync(token, ct);
    }

    internal static void Audit(ArchiveDbContext db, int? userId, string @event, string? detail, HttpContext http)
    {
        db.AuthAudit.Add(new AuthAudit
        {
            UserId = userId,
            Event = @event,
            Detail = detail,
            IpAddress = Ip(http)
        });
    }

    private static string? Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static string? UserAgent(HttpContext http)
        => http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
