using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Endpoints;

public sealed record RegisterRequest(
    string? Username,
    string? Password,
    [property: System.Text.Json.Serialization.JsonPropertyName("cf-turnstile-response")]
    string? TurnstileResponse
);

public sealed record LoginRequest(string? Username, string? Password);

public sealed record PasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record PasswordRemoveRequest(string? CurrentPassword);

public sealed record AccountResponse(
    string Username,
    bool IsAdmin,
    UserRole Role,
    DateTimeOffset? DeletionPendingUntil,
    double Elo,
    int Wins,
    int Losses,
    int Matches);

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
        // Reached from the browser build's own origin, so the game client can sign in and confirm
        // its session without a page reload.
        var auth = app.MapGroup("/auth").RequireCors(CorsPolicies.CrossOrigin);

        auth.MapPost(
            "/register",
            async (
                RegisterRequest request, ArchiveDbContext db, ArchiveOptions options, LoginThrottle throttle,
                SessionService sessions, TurnstileVerifier turnstile, HttpContext http, CancellationToken ct
            ) =>
            {
                if (options.ReadOnly)

                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                // Each attempt costs a full PBKDF2 hash and a row, so registration is throttled by
                // the same per-IP limiter as login.

                if (throttle.IsBlocked("ip:" + (Ip(http) ?? "unknown"), out _))

                    return Results.Json(
                        new { error = "Too many attempts. Try again later." },
                        statusCode: StatusCodes.Status429TooManyRequests
                    );

                var challenge = await turnstile.VerifyAsync(request.TurnstileResponse, "register", ct, Ip(http));

                if (!challenge.Accepted)

                    return Results.BadRequest(new { error = challenge.Message });

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

                throttle.RecordFailure("ip:" + (Ip(http) ?? "unknown"));
                Audit(db, user.Id, "register", null, http);
                var token = await sessions.CreateAsync(user, UserAgent(http), Ip(http), ct);
                await db.SaveChangesAsync(ct);
                SetSessionCookie(http, token, options);

                return Results.Ok(
                    new TokenResponse(
                        token,
                        new AccountResponse(user.Username, user.IsAdmin, user.Role, null, 1500, 0, 0, 0)
                    )
                );
            }
        );

        auth.MapPost(
            "/login",
            async (
                LoginRequest request, ArchiveDbContext db, ArchiveOptions options, LoginThrottle throttle,
                SessionService sessions, HttpContext http, CancellationToken ct
            ) =>
            {
                var normalized = request.Username is null ? "" : UsernamePolicy.Normalize(request.Username);
                var ipKey = "ip:" + (Ip(http) ?? "unknown");
                var nameKey = "name:" + normalized;

                // Both keys are checked so a distributed spray against one account and a spread across
                // many accounts both slow down.

                if (throttle.IsBlocked(ipKey, out var retry) || throttle.IsBlocked(nameKey, out retry))
                {
                    http.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();

                    return Results.Json(
                        new { error = "Too many attempts. Try again later." },
                        statusCode: StatusCodes.Status429TooManyRequests
                    );
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

                return Results.Ok(
                    new TokenResponse(
                        token,
                        new AccountResponse(
                            user.Username,
                            user.IsAdmin,
                            user.Role,
                            DeletionPendingUntil(user, options),
                            rating?.Elo ?? 1500,
                            rating?.Wins ?? 0,
                            rating?.Losses ?? 0,
                            rating?.Matches ?? 0
                        )
                    )
                );
            }
        );

        auth.MapPost(
            "/logout",
            async (
                HttpContext http, ArchiveDbContext db, SessionService sessions, CancellationToken ct
            ) =>
            {
                var token = BearerToken(http);
                if (token is not null)
                    await sessions.RevokeAsync(token, ct);

                return Results.NoContent();
            }
        );

        auth.MapGet(
            "/me",
            async (
                HttpContext http, ArchiveDbContext db, ArchiveOptions options, CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();

                var rating = await db.Ratings.FirstOrDefaultAsync(r => r.UserId == user.Id && r.Gamemode == "nfmm/pvp", ct);

                return Results.Ok(
                    new AccountResponse(
                        user.Username,
                        user.IsAdmin,
                        user.Role,
                        DeletionPendingUntil(user, options),
                        rating?.Elo ?? 1500,
                        rating?.Wins ?? 0,
                        rating?.Losses ?? 0,
                        rating?.Matches ?? 0
                    )
                );
            }
        );

        // Called by the lobby with a player's token. The lobby is not the game master: it holds
        // no archive key, and this returns only what a live lobby connection needs to enforce
        // bans, mutes and moderation.
        auth.MapGet(
            "/verify",
            async (
                HttpContext http, ArchiveDbContext db, CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);

                return user is null
                    ? Results.Unauthorized()
                    : Results.Ok(VerifyShape(user));
            }
        );

        // Trusted-server only: lets the lobby re-read moderation state without holding a player
        // token. Signed, because an account's ban/mute state is not public.
        auth.MapGet(
            "/account/{id:int}",
            async (int id, HttpRequest request, ArchiveDbContext db, TrustedServerAuth auth, CancellationToken ct) =>
            {
                var error = auth.Verify(
                    "GET",
                    $"/auth/account/{id}",
                    ReadOnlySpan<byte>.Empty,
                    request.Headers.Authorization,
                    out _
                );

                if (error is not null)

                    return Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);

                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

                return user is null ? Results.NotFound() : Results.Ok(VerifyShape(user));
            }
        );

        auth.MapPost(
            "/logout-all",
            async (
                HttpContext http, ArchiveDbContext db, SessionService sessions, CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();

                // Bumping the stamp invalidates every session, including ones we cannot see here.
                user.SecurityStamp++;
                await db.SaveChangesAsync(ct);
                await sessions.RevokeAllAsync(user.Id, ct);

                return Results.NoContent();
            }
        );

        // Adds or changes the password. Bumps the stamp, revokes every session, and re-issues
        // one so the caller is not signed out by their own change.
        auth.MapPost(
            "/password",
            async (
                PasswordRequest request, HttpContext http, ArchiveDbContext db, ArchiveOptions options,
                SessionService sessions, CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();

                // A Discord-only account has no hash to verify; anything else must prove the
                // current password.

                if (user.PasswordHash is { } hash && !PasswordHasher.Verify(request.CurrentPassword ?? "", hash))

                    return Results.BadRequest(new { error = "The current password is wrong." });

                if (!PasswordPolicy.TryValidate(request.NewPassword, out var error))

                    return Results.BadRequest(new { error });

                user.PasswordHash = PasswordHasher.Hash(request.NewPassword!);
                user.SecurityStamp++;
                await db.SaveChangesAsync(ct);
                await sessions.RevokeAllAsync(user.Id, ct);

                var token = await sessions.CreateAsync(user, null, http.Connection.RemoteIpAddress?.ToString(), ct);
                SetSessionCookie(http, token, options);

                return Results.Ok(new TokenResponse(
                    token,
                    new AccountResponse(
                        user.Username,
                        user.IsAdmin,
                        user.Role,
                        DeletionPendingUntil(user, options),
                        1500,
                        0,
                        0,
                        0
                    )
                )
                );
            }
        );

        // POST, not DELETE: a JSON body on DELETE is not inferred by minimal APIs, and the
        // password must not travel in a URL.
        auth.MapPost(
            "/password/remove",
            async (
                PasswordRemoveRequest request, HttpContext http, ArchiveDbContext db, SessionService sessions,
                CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();

                // Removing the only sign-in method would make the account unreachable.
                var hasDiscord = await db.ExternalLogins.AnyAsync(l => l.UserId == user.Id, ct);

                if (!hasDiscord)

                    return Results.BadRequest(new { error = "Link another sign-in method before removing the password." });

                if (user.PasswordHash is { } hash && !PasswordHasher.Verify(request.CurrentPassword ?? "", hash))

                    return Results.BadRequest(new { error = "The current password is wrong." });

                user.PasswordHash = null;
                user.SecurityStamp++;
                await db.SaveChangesAsync(ct);
                await sessions.RevokeAllAsync(user.Id, ct);

                return Results.NoContent();
            }
        );

        auth.MapPost(
            "/discord/unlink",
            async (HttpContext http, ArchiveDbContext db, SessionService sessions, CancellationToken ct) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();

                if (user.PasswordHash is null)

                    return Results.BadRequest(new { error = "Add a password before unlinking Discord." });

                await db.ExternalLogins
                .Where(l => l.UserId == user.Id && l.Provider == "discord")
                .ExecuteDeleteAsync(ct);
                await db.SaveChangesAsync(ct);

                return Results.NoContent();
            }
        );
    }

    private static object VerifyShape(User user) => new
    {
        AccountId = user.Id,
        user.Username,
        Role = user.Role,
        ModerationRevision = user.ModerationRevision,
        Banned = user.IsBanned(DateTimeOffset.UtcNow),
        user.BannedUntil,
        user.BanReason,
        user.KickedUntil,
        Muted = user.IsMuted(DateTimeOffset.UtcNow),
        user.MutedUntil,
        user.MuteReason
    };

    private static DateTimeOffset? DeletionPendingUntil(User user, ArchiveOptions options)
        => user.DeletionRequestedAt is { } requested ? requested + options.DeletionGracePeriod : null;

    /// <summary>
    /// Issues the session as an HttpOnly cookie as well as returning it in the body. The game
    /// client uses the body token; the website uses the cookie, which keeps the token out of
    /// script and out of URLs.
    /// </summary>
    internal static void SetSessionCookie(HttpContext http, string token, ArchiveOptions options)
    {
        http.Response.Cookies.Append(
            "nfmw_session",
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow + options.SessionLifetime
            }
        );
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
        db.AuthAudit.Add(
            new AuthAudit
            {
                UserId = userId,
                Event = @event,
                Detail = detail,
                IpAddress = Ip(http)
            }
        );
    }

    private static string? Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static string? UserAgent(HttpContext http)
        => http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
