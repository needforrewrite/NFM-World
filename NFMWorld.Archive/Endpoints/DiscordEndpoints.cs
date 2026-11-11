using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Endpoints;

/// <summary>
/// Discord OAuth2 with PKCE.
///
/// The account is keyed on Discord's immutable user id, never on the email claim: Discord lets a
/// user change their email, and an email can be reassigned by whoever controls the address. The
/// <c>state</c> parameter is single-use and stored hashed, so a leaked database cannot be used to
/// complete someone's pending login.
/// </summary>
public static class DiscordEndpoints
{
    private const string AuthorizeUrl = "https://discord.com/oauth2/authorize";
    private const string TokenUrl = "https://discord.com/api/oauth2/token";
    private const string UserUrl = "https://discord.com/api/users/@me";

    private sealed record DiscordTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")]
        string? AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")]
        string? TokenType
    );

    private sealed record DiscordUser(string? Id, string? Username);

    public static void MapDiscordEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/discord/start", () => Results.Redirect("/account/login?flow=discord"));
        app.MapPost(
            "/auth/discord/start",
            async (
                ArchiveOptions options, ArchiveDbContext db, TurnstileVerifier turnstile, LoginThrottle throttle,
                Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext http, CancellationToken ct
            ) =>
            {
                if (string.IsNullOrEmpty(options.DiscordClientId) || string.IsNullOrEmpty(options.DiscordRedirectUri))
                    return Results.NotFound();

                try
                {
                    await antiforgery.ValidateRequestAsync(http);
                }
                catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
                {
                    return Results.BadRequest();
                }

                var ipKey = "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                if (throttle.IsBlocked(ipKey, out _))
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                var form = await http.Request.ReadFormAsync(ct);
                if (!await turnstile.VerifyAsync(
                        form["cf-turnstile-response"],
                        "discord",
                        ct,
                        http.Connection.RemoteIpAddress?.ToString()
                    ))
                    return Results.BadRequest(new { error = TurnstileVerifier.Error });
                throttle.RecordFailure(ipKey);

                // The prefix excludes pending flows created before challenge enforcement.
                var state = "turnstile." + Tokens.Create();
                var verifier = Tokens.Create();
                var challenge = Tokens.Base64UrlEncode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));

                db.OauthPending.Add(
                    new OauthPending
                    {
                        StateHash = Tokens.Hash(state),
                        CodeVerifier = verifier,
                        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
                    }
                );
                await db.SaveChangesAsync(ct);

                // Bind the state to this browser. Without it anyone could start a login as
                // themselves and send the callback URL to a victim, silently signing them into the
                // attacker's account (login CSRF).
                http.Response.Cookies.Append(
                    "nfmw_oauth_state",
                    state,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = http.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddMinutes(10)
                    }
                );

                var url = $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(options.DiscordClientId)}"
                          + $"&redirect_uri={Uri.EscapeDataString(options.DiscordRedirectUri)}"
                          + "&response_type=code&scope=identify"
                          + $"&state={Uri.EscapeDataString(state)}"
                          + $"&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256";

                return Results.Redirect(url);
            }
        );

        app.MapGet(
            "/auth/discord/callback",
            async (
                string? code, string? state, ArchiveOptions options, ArchiveDbContext db,
                SessionService sessions, IHttpClientFactory clients, HttpContext http, CancellationToken ct
            ) =>
            {
                if (string.IsNullOrEmpty(code)
                    || string.IsNullOrEmpty(state)
                    || !state.StartsWith("turnstile.", StringComparison.Ordinal))
                    return Results.BadRequest(new { error = "Missing code or state." });

                // The state must be the one this browser started with, not merely a live one: a
                // state handed to a victim would otherwise sign them into the attacker's account.
                if (http.Request.Cookies["nfmw_oauth_state"] is not { Length: > 0 } browserState
                    || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(browserState),
                        System.Text.Encoding.UTF8.GetBytes(state)
                    ))
                    return Results.BadRequest(new { error = "This login attempt was started in a different browser." });

                http.Response.Cookies.Delete("nfmw_oauth_state");

                var stateHash = Tokens.Hash(state);
                var pending = await db.OauthPending.FirstOrDefaultAsync(p => p.StateHash == stateHash, ct);
                if (pending is null)
                    return Results.BadRequest(new { error = "Unknown or already-used login attempt." });

                // Consume it whether or not the exchange succeeds: a state is valid for exactly one attempt.
                db.OauthPending.Remove(pending);
                await db.SaveChangesAsync(ct);

                if (pending.ExpiresAt <= DateTimeOffset.UtcNow)
                    return Results.BadRequest(new { error = "Login attempt expired." });

                var client = clients.CreateClient("discord");
                using var tokenResponse = await client.PostAsync(
                    TokenUrl,
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["client_id"] = options.DiscordClientId,
                            ["client_secret"] = options.DiscordClientSecret,
                            ["grant_type"] = "authorization_code",
                            ["code"] = code,
                            ["redirect_uri"] = options.DiscordRedirectUri,
                            ["code_verifier"] = pending.CodeVerifier
                        }
                    ),
                    ct
                );

                if (!tokenResponse.IsSuccessStatusCode)
                    return Results.BadRequest(new { error = "Discord rejected the authorization code." });

                var token = await tokenResponse.Content.ReadFromJsonAsync<DiscordTokenResponse>(ct);
                if (string.IsNullOrEmpty(token?.AccessToken))
                    return Results.BadRequest(new { error = "Discord returned no access token." });

                using var userRequest = new HttpRequestMessage(HttpMethod.Get, UserUrl);
                userRequest.Headers.Authorization = new("Bearer", token.AccessToken);
                using var userResponse = await client.SendAsync(userRequest, ct);
                if (!userResponse.IsSuccessStatusCode)
                    return Results.BadRequest(new { error = "Could not read the Discord profile." });

                var discordUser = await userResponse.Content.ReadFromJsonAsync<DiscordUser>(ct);
                if (string.IsNullOrEmpty(discordUser?.Id))
                    return Results.BadRequest(new { error = "Discord returned no user id." });

                var login = await db.ExternalLogins
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Provider == "discord" && l.ProviderUserId == discordUser.Id, ct);

                User user;
                if (login is not null)
                {
                    user = login.User;
                }
                else
                {
                    if (options.ReadOnly)
                        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                    var baseName = SanitizeUsername(discordUser.Username) ?? $"discord{discordUser.Id}";
                    user = new User
                    {
                        Username = baseName,
                        UsernameNormalized = UsernamePolicy.Normalize(baseName)
                    };

                    // Discord names collide freely, so append a suffix until the folded name is free.
                    var suffix = 0;
                    while (await db.Users.AnyAsync(u => u.UsernameNormalized == user.UsernameNormalized, ct))
                        user.UsernameNormalized = UsernamePolicy.Normalize($"{baseName}#{++suffix}");

                    db.Users.Add(user);
                    await db.SaveChangesAsync(ct);
                    db.ExternalLogins.Add(
                        new ExternalLogin
                        {
                            UserId = user.Id,
                            Provider = "discord",
                            ProviderUserId = discordUser.Id
                        }
                    );
                    await db.SaveChangesAsync(ct);
                    AuthEndpoints.Audit(db, user.Id, "register_discord", null, http);
                }

                var sessionToken = await sessions.CreateAsync(user, null, http.Connection.RemoteIpAddress?.ToString(), ct);

                // Handing the token back through a redirect URL would put it in browser history and
                // referrer headers, so it is set as an HttpOnly cookie instead.
                http.Response.Cookies.Append(
                    "nfmw_session",
                    sessionToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = http.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow + options.SessionLifetime
                    }
                );

                return Results.Redirect("/");
            }
        );
    }

    /// <summary>Discord names allow spaces and symbols; keep only what our username policy admits.</summary>
    private static string? SanitizeUsername(string? discordUsername)
    {
        if (string.IsNullOrWhiteSpace(discordUsername))
            return null;

        var cleaned = new string(
            discordUsername
                .Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c))
                .ToArray()
        );
        if (cleaned.Length > UsernamePolicy.MaxLength)
            cleaned = cleaned[..UsernamePolicy.MaxLength];
        return UsernamePolicy.TryValidate(cleaned, out _) ? cleaned : null;
    }
}
