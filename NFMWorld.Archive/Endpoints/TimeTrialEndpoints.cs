using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Endpoints;

public sealed record TimeTrialUpload(string StageHash, string CarHash, string StageName, string CarName);

public sealed record TimeTrialEntry(
    string Username, int TotalTicks, DateTimeOffset CreatedAt, string ReplayUrl);

/// <summary>
/// Time-trial upload and leaderboards.
///
/// A submitted replay is ranked only by the tick count the validator's simulation produces, never
/// by a number the client sends, and only against the stage and car hashes it claims. Both are
/// content hashes, so editing the stage or the car afterwards cannot silently make an old time
/// faster or a forged one valid.
/// </summary>
public static class TimeTrialEndpoints
{
    public static void MapTimeTrialEndpoints(this WebApplication app)
    {
        var tt = app.MapGroup("/tt");

        tt.MapPost("/upload", async (
            HttpRequest request, ArchiveDbContext db, ArchiveOptions options, ValidatorRunner validator,
            FileStorage storage, HttpContext http, CancellationToken ct) =>
        {
            var user = await http.GetUserAsync(db, ct);
            if (user is null)
                return Results.Unauthorized();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected a multipart upload." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            var stageHash = form["stageHash"].ToString();
            var carHash = form["carHash"].ToString();
            var stageName = form["stageName"].ToString();
            var carName = form["carName"].ToString();

            if (file is null || string.IsNullOrEmpty(stageHash) || string.IsNullOrEmpty(carHash))
                return Results.BadRequest(new { error = "Missing replay, stage hash or car hash." });

            // Both hashes have to name content the archive actually holds, of the right kind.
            // They are the leaderboard's key, so an invented hash would rank a time against
            // content that does not exist. This binds the claim to real assets; the validator
            // still resolves the stage by name, so it is not yet proof of what was simulated.
            // shortcut: hashes are not used to load the stage for simulation, upgrade when the
            // validator can be pointed at an asset by hash.
            if (!await db.Assets.AnyAsync(a => a.ContentHash == stageHash && a.Type == AssetType.Stage, ct)
                || !await db.Assets.AnyAsync(a => a.ContentHash == carHash && a.Type == AssetType.Car, ct))
                return Results.BadRequest(new { error = "The stage or car is not in the archive." });

            if (file.Length > RadpackLimits.MaxTimeTrialBytes)
                return Results.BadRequest(new { error = "Replay exceeds the size limit." });

            // The validator simulates against the archive's stored stage, not the one the replay
            // carries, so the replay cannot choose the course it is verified on.
            var stageAsset = await db.Assets.FirstAsync(a => a.ContentHash == stageHash && a.Type == AssetType.Stage, ct);
            var stagePath = Path.Combine(storage.Root, stageAsset.StoragePath);

            await using var received = file.OpenReadStream();
            var stored = await StoreAndValidateAsync(
                db, validator, storage, received, stageName, carName, stagePath, ct);

            // The simulation is the authority on the time; a replay that does not reproduce its
            // own tick count is refused, not stored.
            if (stored.ValidatorOutput is not { } output
                || !output.TryGetProperty("elapsedTicks", out var elapsed)
                || !output.TryGetProperty("expectedTicks", out var expected)
                || elapsed.GetInt32() != expected.GetInt32())
                return Results.BadRequest(new { error = "The replay did not reproduce its own time." });

            // Replays from another backend version tick different physics, so their time is not
            // comparable with the leaderboard's.
            if (!output.TryGetProperty("replayVersion", out var replayVersion))
                return Results.BadRequest(new { error = "The replay did not report its version." });

            var totalTicks = elapsed.GetInt32();
            var existing = await db.TimeTrials
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.StageHash == stageHash && t.CarHash == carHash, ct);

            // One entry per (user, stage, car): a slower retry keeps the old time rather than
            // replacing it, so a player cannot lose a good time by retrying.
            if (existing is not null && existing.TotalTicks <= totalTicks)
            {
                await storage.DeleteIfUnreferencedAsync(stored.StoragePath, db, ct);
                return Results.Ok(new { status = "kept existing faster time", existing.TotalTicks });
            }

            if (existing is not null)
            {
                await storage.DeleteIfUnreferencedAsync(existing.StoragePath, db, ct);
                existing.TotalTicks = totalTicks;
                existing.StoragePath = stored.StoragePath;
                existing.CreatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                db.TimeTrials.Add(new TimeTrial
                {
                    UserId = user.Id,
                    StageHash = stageHash,
                    CarHash = carHash,
                    StageName = stageName,
                    CarName = carName,
                    TotalTicks = totalTicks,
                    StoragePath = stored.StoragePath,
                    ReplayVersion = replayVersion.GetInt32()
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { status = "recorded", totalTicks });
        });

        // Public: a leaderboard is the point of a time trial, and it carries no account data
        // beyond the username.
        tt.MapGet("/leaderboard", async (
            ArchiveDbContext db, string stageHash, string carHash, int? limit, CancellationToken ct) =>
            Results.Ok(await db.TimeTrials
                .Where(t => t.StageHash == stageHash && t.CarHash == carHash && t.IsValid)
                .OrderBy(t => t.TotalTicks)
                .Take(Math.Clamp(limit ?? 100, 1, 100))
                .Select(t => new TimeTrialEntry(t.User.Username, t.TotalTicks, t.CreatedAt, $"/tt/replay/{t.Id}"))
                .ToListAsync(ct)));

        tt.MapGet("/replay/{id:int}", async (
            int id, ArchiveDbContext db, FileStorage storage, CancellationToken ct) =>
        {
            var entry = await db.TimeTrials.FirstOrDefaultAsync(t => t.Id == id && t.IsValid, ct);
            if (entry is null)
                return Results.NotFound();

            var stream = storage.OpenRead(entry.StoragePath);
            return stream is null
                ? Results.NotFound()
                : Results.File(stream, "application/octet-stream", $"replay-{id}.timetrial");
        });
    }

    private sealed record StoredReplay(string StoragePath, System.Text.Json.JsonElement? ValidatorOutput);

    /// <summary>
    /// Stores the upload first, then validates the stored bytes, so the file that is simulated is
    /// byte-for-byte the file that later gets served. The name is content-addressed, so a
    /// rejected replay leaves nothing behind once it is deleted.
    /// </summary>
    private static async Task<StoredReplay> StoreAndValidateAsync(
        ArchiveDbContext db, ValidatorRunner validator, FileStorage storage, Stream source,
        string stageName, string carName, string stageAssetPath, CancellationToken ct)
    {
        var (_, relative, _) = await storage.PutAsync(source, RadpackLimits.MaxTimeTrialBytes, ct);
        var path = Path.Combine(storage.Root, relative);

        var result = await validator.RunAsync("tt-sim", [path, stageName, carName, stageAssetPath], ct);
        // The path is content-addressed: a replay or asset already stored under the same bytes
        // must not lose its file because a second, identical upload failed to validate.
        if (!result.Ok)
            await storage.DeleteIfUnreferencedAsync(relative, db, ct);

        return new StoredReplay(relative, result.Output);
    }
}
