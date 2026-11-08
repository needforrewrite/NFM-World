using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Auth;

/// <summary>
/// Issues and validates opaque session tokens. Lookup is by the token's selector, so validating
/// a token is one indexed read rather than a scan over all sessions.
/// </summary>
public sealed class SessionService(ArchiveDbContext db, ArchiveOptions options)
{
    /// <summary>Creates a session and returns the plaintext token, which is never stored.</summary>
    public async Task<string> CreateAsync(User user, string? userAgent, string? ipAddress, CancellationToken ct)
    {
        var token = Tokens.CreateSessionToken();
        var (selector, verifierHash) = Tokens.Split(token);
        var now = DateTimeOffset.UtcNow;

        db.Sessions.Add(
            new Session
            {
                UserId = user.Id,
                TokenHash = $"{selector}.{verifierHash}",
                SecurityStamp = user.SecurityStamp,
                ExpiresAt = now + options.SessionLifetime,
                CreatedAt = now,
                LastUsedAt = now,
                UserAgent = Truncate(userAgent, 256),
                IpAddress = Truncate(ipAddress, 64)
            }
        );
        user.LastSeenAt = now;
        await db.SaveChangesAsync(ct);

        return token;
    }

    public async Task<User?> ValidateAsync(string token, CancellationToken ct)
    {
        var (selector, verifierHash) = Tokens.Split(token);
        var now = DateTimeOffset.UtcNow;

        var session = await db.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == $"{selector}.{verifierHash}", ct);

        if (session is null)
            return null;

        if (session.ExpiresAt <= now || session.SecurityStamp != session.User.SecurityStamp)
        {
            db.Sessions.Remove(session);
            await db.SaveChangesAsync(ct);
            return null;
        }

        session.LastUsedAt = now;
        session.User.LastSeenAt = now;
        await db.SaveChangesAsync(ct);

        return session.User;
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var (selector, verifierHash) = Tokens.Split(token);
        var stored = $"{selector}.{verifierHash}";
        await db.Sessions.Where(s => s.TokenHash == stored).ExecuteDeleteAsync(ct);
    }

    /// <summary>Invalidates every session for a user, e.g. after a password change.</summary>
    public async Task RevokeAllAsync(int userId, CancellationToken ct)
        => await db.Sessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);

    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}
