using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Endpoints;

/// <summary>AccountId is null for guests and always null for bots; it is the archive's user id.</summary>
public sealed record ResultPlayer(int? AccountId, int FinishPosition, long? FinishTimeTicks, bool IsBot);
public sealed record MatchResultRequest(
    string MatchKey,
    string Gamemode,
    string StageName,
    string? StageHash,
    long DurationTicks,
    ResultPlayer[] Players);

/// <summary>
/// Match result reporting. Only a server holding a trusted key may call this, and only the lobby
/// does: a game master is untrusted, so it must never be able to write ratings directly.
/// </summary>
public static class ResultEndpoints
{
    private const int MaxBodyBytes = 256 * 1024;
    private const double KFactor = 32;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapResultEndpoints(this WebApplication app)
    {
        app.MapPost("/results/match", async (
            HttpRequest request, ArchiveDbContext db, TrustedServerAuth auth, CancellationToken ct) =>
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

            var error = auth.Verify("POST", "/results/match", body, request.Headers.Authorization, out var keyId);
            if (error is not null)
                return Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);

            MatchResultRequest? result;
            try
            {
                result = JsonSerializer.Deserialize<MatchResultRequest>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Malformed result." });
            }

            if (result is null || string.IsNullOrEmpty(result.MatchKey) || result.Players is null || result.Players.Length == 0)
                return Results.BadRequest(new { error = "Malformed result." });

            // The match key is the idempotency key: a retried report is acknowledged and ignored,
            // rather than rating the same match twice.
            if (await db.Matches.AnyAsync(m => m.MatchKey == result.MatchKey, ct))
                return Results.Ok(new { status = "already recorded" });

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var match = new Match
            {
                MatchKey = result.MatchKey,
                ResultServerId = keyId!,
                Gamemode = result.Gamemode,
                StageName = result.StageName,
                StageHash = result.StageHash,
                Duration = TimeSpan.FromTicks(Math.Max(0, result.DurationTicks))
            };
            db.Matches.Add(match);

            var rated = new List<(User User, Rating Rating, int Position)>();
            var humanCount = result.Players.Count(p => !p.IsBot);
            foreach (var player in result.Players)
            {
                // A bot or an anonymous player has no account to attribute the result to; the
                // match is still recorded, it just cannot move a rating.
                var user = player.AccountId is { } accountId && !player.IsBot
                    ? await db.Users.FirstOrDefaultAsync(u => u.Id == accountId, ct)
                    : null;

                db.MatchParticipants.Add(new MatchParticipant
                {
                    Match = match,
                    UserId = user?.Id,
                    FinishPosition = player.FinishPosition,
                    FinishTime = player.FinishTimeTicks is { } ticks ? TimeSpan.FromTicks(Math.Max(0, ticks)) : null,
                    IsBot = player.IsBot
                });

                // Elo between humans only: a lobby with one human and bots has no opponent to rate
                // against, and rating it would farm points for playing alone.
                if (user is null || humanCount < 2)
                    continue;

                var rating = await db.Ratings
                    .FirstOrDefaultAsync(r => r.UserId == user.Id && r.Gamemode == result.Gamemode, ct);
                if (rating is null)
                {
                    rating = new Rating { UserId = user.Id, User = user, Gamemode = result.Gamemode };
                    db.Ratings.Add(rating);
                }

                rated.Add((user, rating, player.FinishPosition));
            }

            ApplyElo(rated);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Results.Ok(new { status = "recorded" });
        });
    }

    /// <summary>
    /// Team-free Elo: every participant is compared with every other, and the winner of each
    /// pairing takes the points. Only players ranked by the server are rated; a match with one
    /// account and the rest bots moves nothing.
    /// </summary>
    private static void ApplyElo(List<(User User, Rating Rating, int Position)> participants)
    {
        if (participants.Count < 2)
            return;

        var deltas = new Dictionary<int, double>();
        foreach (var (user, _, _) in participants)
            deltas[user.Id] = 0;

        foreach (var a in participants)
        {
            foreach (var b in participants)
            {
                if (a.User.Id == b.User.Id)
                    continue;

                var expected = 1 / (1 + Math.Pow(10, (b.Rating.Elo - a.Rating.Elo) / 400.0));
                var score = a.Position < b.Position ? 1.0 : a.Position == b.Position ? 0.5 : 0.0;
                deltas[a.User.Id] += KFactor * (score - expected);
            }
        }

        foreach (var (user, rating, position) in participants)
        {
            // Split the pairwise total so the per-match movement stays comparable to a straight
            // win, regardless of lobby size.
            rating.Elo = Math.Max(100, rating.Elo + deltas[user.Id] / (participants.Count - 1));
            rating.Matches++;
            if (position == 0)
                rating.Wins++;
            else
                rating.Losses++;
            rating.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
