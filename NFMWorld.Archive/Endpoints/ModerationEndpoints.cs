using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Moderation;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Endpoints;

/// <summary>The lobby's signed event body; the actor is resolved by account id, not token.</summary>
public sealed record ModerationEventRequest(
    int ActorAccountId,
    ModerationActionKind Action,
    int TargetAccountId,
    string TargetUsername,
    string? Reason,
    long? DurationSeconds,
    string? Argument
);

/// <summary>
/// Staff-only moderation endpoints. The game client never calls these: chat commands arrive
/// through the lobby's signed <c>/moderation/events</c>, and the Razor pages call
/// <see cref="ModerationService"/> directly.
/// </summary>
public static class ModerationEndpoints
{
    private const int MaxBodyBytes = 64 * 1024;
    private const int MaxListLimit = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapModerationEndpoints(this WebApplication app)
    {
        var moderation = app.MapGroup("/moderation");

        // The lobby applies a chat command by asking the archive, signed with the trusted key.
        // The actor is still resolved and authorised here, so a compromised lobby cannot exceed
        // the actor's own standing.
        moderation.MapPost(
            "/events",
            async (HttpRequest request, ArchiveDbContext db, TrustedServerAuth auth, ModerationService service,
                CancellationToken ct) =>
            {
                byte[] body;

                try
                {
                    body = await TrustedServerAuth.ReadBodyAsync(request, MaxBodyBytes, ct);
                }
                catch (InvalidDataException)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                var error = auth.Verify("POST", "/moderation/events", body, request.Headers.Authorization, out _);

                if (error is not null)

                    return Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);

                ModerationEventRequest? payload;

                try
                {
                    payload = JsonSerializer.Deserialize<ModerationEventRequest>(body, JsonOptions);
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = "Malformed moderation event." });
                }

                if (payload is null)

                    return Results.BadRequest(new { error = "Malformed moderation event." });

                var actor = await db.Users.FirstOrDefaultAsync(u => u.Id == payload.ActorAccountId, ct);

                if (actor is null)

                    return Results.Json(new { error = "Unknown actor." }, statusCode: StatusCodes.Status403Forbidden);

                var duration = payload.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;
                var outcome = await service.ApplyAsync(
                    actor,
                    new ModerationRequest(payload.Action, payload.TargetAccountId, payload.Reason, duration, payload.Argument),
                    request.HttpContext,
                    ct
                );

                return Results.Json(new { ok = outcome.Ok, error = outcome.Error });
            }
        );

        // Staff action from the website; never the game client. A refused action is a 403 (or a
        // 404 for an unknown target), so a caller can branch on the status.
        moderation.MapPost(
            "/actions",
            async (ModerationActionRequest request, ArchiveDbContext db, ModerationService service, HttpContext http,
                CancellationToken ct) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();
                if (!user.IsModerator)

                    return Results.Json(new { error = "Staff only." }, statusCode: StatusCodes.Status403Forbidden);

                var outcome = await service.ApplyAsync(
                    user,
                    new ModerationRequest(
                        request.Action,
                        request.TargetUserId,
                        request.Reason,
                        request.Duration,
                        request.Argument
                    ),
                    http,
                    ct
                );

                if (!outcome.Ok)

                    return Refusal(outcome.Error);

                return Results.Json(new { ok = true, error = (string?)null });
            }
        );

        // Keyset-paged, newest first, so staff can find a tombstone by its id.
        moderation.MapGet(
            "/users",
            async (string? query, int? after, int? limit, ArchiveDbContext db, HttpContext http, CancellationToken ct) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();
                if (!user.IsModerator)

                    return Results.Json(new { error = "Staff only." }, statusCode: StatusCodes.Status403Forbidden);

                var take = Math.Clamp(limit ?? 50, 1, MaxListLimit);

                IQueryable<User> q = db.Users;
                if (!string.IsNullOrWhiteSpace(query))
                    q = q.Where(u => EF.Functions.Like(u.Username, "%" + Sql.LikeEscape(query.Trim()) + "%", "\\"));
                if (after is { } cursor)
                    q = q.Where(u => u.Id < cursor);

                var rows = await q
                    .OrderByDescending(u => u.Id)
                    .Take(take + 1)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Role,
                        Banned = u.BannedUntil != null && u.BannedUntil > DateTimeOffset.UtcNow,
                        u.BannedUntil,
                        Muted = u.MutedUntil != null && u.MutedUntil > DateTimeOffset.UtcNow,
                        u.MutedUntil,
                        u.KickedUntil,
                        DeletionPendingUntil = u.DeletionRequestedAt,
                        u.DeletedAt,
                        AssetCount = db.Assets.Count(a => a.OwnerId == u.Id),
                        u.LastSeenAt
                    })
                    .ToListAsync(ct);

                var hasMore = rows.Count > take;
                var items = rows.Take(take).Select(r => new
                {
                    r.Id,
                    r.Username,
                    role = (byte)r.Role,
                    r.Banned,
                    r.BannedUntil,
                    r.Muted,
                    r.MutedUntil,
                    r.KickedUntil,
                    r.DeletionPendingUntil,
                    deleted = r.DeletedAt != null,
                    r.AssetCount,
                    r.LastSeenAt
                }).ToArray();

                return Results.Ok(new { items, nextCursor = hasMore ? items[^1].Id : (int?)null });
            }
        );

        moderation.MapGet(
            "/actions",
            async (int? targetUserId, int? after, int? limit, ArchiveDbContext db, HttpContext http, CancellationToken ct) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();
                if (!user.IsModerator)

                    return Results.Json(new { error = "Staff only." }, statusCode: StatusCodes.Status403Forbidden);

                var take = Math.Clamp(limit ?? 50, 1, MaxListLimit);

                IQueryable<ModerationAction> q = db.ModerationActions;
                if (targetUserId is { } target)
                    q = q.Where(a => a.TargetUserId == target);
                if (after is { } cursor)
                    q = q.Where(a => a.Id < cursor);

                var rows = await q
                    .OrderByDescending(a => a.Id)
                    .Take(take + 1)
                    .Select(a => new
                    {
                        a.Id,
                        a.ActorUserId,
                        a.ActorName,
                        a.TargetUserId,
                        a.TargetName,
                        a.Action,
                        a.Reason,
                        a.ExpiresAt,
                        a.CreatedAt
                    })
                    .ToListAsync(ct);

                var hasMore = rows.Count > take;
                var items = rows.Take(take).Select(a => new
                {
                    a.Id,
                    a.ActorUserId,
                    a.ActorName,
                    a.TargetUserId,
                    a.TargetName,
                    action = (byte)a.Action,
                    a.Reason,
                    a.ExpiresAt,
                    a.CreatedAt
                }).ToArray();

                return Results.Ok(new { items, nextCursor = hasMore ? items[^1].Id : (long?)null });
            }
        );

        moderation.MapPost(
            "/assets/{id:int}/takedown",
            async (int id, ArchiveDbContext db, HttpContext http, CancellationToken ct) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();
                if (!user.IsModerator)

                    return Results.Json(new { error = "Staff only." }, statusCode: StatusCodes.Status403Forbidden);

                var found = await AssetModeration.SetTakedownAsync(db, id, isTakedown: true, user, http, ct);

                return found ? Results.Ok(new { ok = true }) : Results.NotFound();
            }
        );

        moderation.MapPost(
            "/assets/{id:int}/restore",
            async (int id, ArchiveDbContext db, HttpContext http, CancellationToken ct) =>
            {
                var user = await http.GetUserAsync(db, ct);

                if (user is null)

                    return Results.Unauthorized();
                if (!user.IsModerator)

                    return Results.Json(new { error = "Staff only." }, statusCode: StatusCodes.Status403Forbidden);

                var found = await AssetModeration.SetTakedownAsync(db, id, isTakedown: false, user, http, ct);

                return found ? Results.Ok(new { ok = true }) : Results.NotFound();
            }
        );
    }

    /// <summary>Maps a refused outcome to a status a caller can branch on.</summary>
    private static IResult Refusal(string? error)
    {
        if (error is not { Length: > 0 })

            return Results.Json(new { ok = false, error = "The action was refused." }, statusCode: StatusCodes
                .Status403Forbidden
            );

        if (error.Contains("No such account"))

            return Results.Json(new { ok = false, error }, statusCode: StatusCodes.Status404NotFound);

        return Results.Json(new { ok = false, error }, statusCode: StatusCodes.Status403Forbidden);
    }
}
