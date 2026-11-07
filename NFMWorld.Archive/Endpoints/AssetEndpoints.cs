using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Endpoints;

public sealed record AssetInfo(
    int Id, string Name, string Type, string ContentHash, long SizeBytes, int DownloadCount);

/// <summary>
/// Radpack asset upload and download.
///
/// Uploads are validated out of process before being indexed, are addressed by their own hash,
/// and are served with a download disposition from a separate origin: an SVG or HTML payload
/// served inline from the app's own origin under a session cookie would be stored XSS.
/// </summary>
public static class AssetEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".radpack", ".rad"
    };

    public static void MapAssetEndpoints(this WebApplication app)
    {
        var assets = app.MapGroup("/assets");

        assets.MapPost("/upload", async (
            HttpRequest request, ArchiveDbContext db, ArchiveOptions options, ValidatorRunner validator,
            FileStorage storage, HttpContext http, CancellationToken ct) =>
        {
            var user = await http.GetUserAsync(db, ct);
            if (user is null)
                return Results.Unauthorized();

            if (options.ReadOnly)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected a multipart upload." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            var name = form["name"].ToString();
            var description = form["description"].ToString();
            var typeText = form["type"].ToString();

            if (file is null || string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Missing file or name." });

            if (!Enum.TryParse<AssetType>(typeText, ignoreCase: true, out var type))
                return Results.BadRequest(new { error = "Unknown asset type." });

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                return Results.BadRequest(new { error = "Only .radpack and .rad files are accepted." });

            if (file.Length > RadpackLimits.MaxCompressedBytes)
                return Results.BadRequest(new { error = "Asset exceeds the size limit." });

            await using var source = file.OpenReadStream();
            var (hash, relative, size) = await storage.PutAsync(source, RadpackLimits.MaxCompressedBytes, ct);
            var path = Path.Combine(storage.Root, relative);

            // Radpacks are validated; a bare .rad is a legacy format the validator cannot
            // inspect, so it is stored unlisted rather than rejected.
            if (extension.Equals(".radpack", StringComparison.OrdinalIgnoreCase))
            {
                var result = await validator.RunAsync("radpack", [path], ct);
                if (!result.Ok)
                {
                    await storage.DeleteIfUnreferencedAsync(relative, db, ct);
                    return Results.BadRequest(new { error = result.Error ?? "The asset is not a readable radpack." });
                }

                // The type byte inside the file has to agree with what the uploader claimed;
                // otherwise a car could be listed as a stage and downloaded into the wrong slot.
                if (result.Output is { } output
                    && output.TryGetProperty("type", out var validated)
                    && !string.Equals(validated.GetString(), type.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    await storage.DeleteIfUnreferencedAsync(relative, db, ct);
                    return Results.BadRequest(new { error = $"The asset is a {validated.GetString()}, not a {type}." });
                }
            }

            var asset = await db.Assets.FirstOrDefaultAsync(a => a.ContentHash == hash && a.Name == name, ct);
            if (asset is not null)
            {
                return Results.Ok(new { status = "already present", asset.Id });
            }

            asset = new Asset
            {
                OwnerId = user.Id,
                Type = type,
                Name = name,
                Description = description,
                ContentHash = hash,
                StoragePath = relative,
                SizeBytes = size,
                IsPublic = true
            };
            db.Assets.Add(asset);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { status = "stored", asset.Id });
        });

        assets.MapGet("/search", async (
            ArchiveDbContext db, string? query, string? type, int? limit, CancellationToken ct) =>
        {
            var q = db.Assets.Where(a => a.IsPublic && !a.IsTakedown);

            if (!string.IsNullOrWhiteSpace(query))
                q = q.Where(a => a.Name.Contains(query));

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<AssetType>(type, ignoreCase: true, out var parsed))
                q = q.Where(a => a.Type == parsed);

            // By id: SQLite cannot ORDER BY a DateTimeOffset, and ids follow creation order.
            return Results.Ok(await q
                .OrderByDescending(a => a.Id)
                .Take(Math.Clamp(limit ?? 50, 1, 100))
                .Select(a => new AssetInfo(a.Id, a.Name, a.Type.ToString(), a.ContentHash, a.SizeBytes, a.DownloadCount))
                .ToListAsync(ct));
        });

        // Fetched by hash so a client can verify what it got and cache immutably.
        assets.MapGet("/{hash}", async (
            string hash, ArchiveDbContext db, FileStorage storage, CancellationToken ct) =>
        {
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                return Results.BadRequest();

            var asset = await db.Assets.FirstOrDefaultAsync(
                a => a.ContentHash == hash && (a.IsPublic || a.OwnerId != null) && !a.IsTakedown, ct);
            if (asset is null)
                return Results.NotFound();

            var stream = storage.OpenRead(asset.StoragePath);
            if (stream is null)
                return Results.NotFound();

            asset.DownloadCount++;
            await db.SaveChangesAsync(ct);

            // Content-addressed, so it never changes: cache it hard.
            var extension = Path.GetExtension(asset.StoragePath) is { Length: > 0 } ext ? ext : ".radpack";
            return Results.File(
                stream,
                contentType: "application/octet-stream",
                fileDownloadName: $"{asset.Name}{extension}",
                enableRangeProcessing: true);
        });
    }
}
