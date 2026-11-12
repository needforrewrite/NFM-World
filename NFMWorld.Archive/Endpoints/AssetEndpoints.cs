using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Endpoints;

/// <summary>
/// One public, non-takedown asset. <see cref="Type"/> is the shared asset-kind name, not the
/// radpack header name, so a stage is always a Stage on every surface.
/// </summary>
public sealed record AssetInfo(
    int Id,
    string Name,
    string Type,
    string ContentHash,
    long SizeBytes,
    int DownloadCount,
    string? Description,
    string? Author,
    DateTimeOffset CreatedAt,
    bool Eligible
);

public sealed record AssetSearchPage(IReadOnlyList<AssetInfo> Items, int? NextCursor);

/// <summary>
/// Radpack asset upload, search, metadata and download.
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

    private const int MaxSearchLimit = 100;

    public static void MapAssetEndpoints(this WebApplication app)
    {
        var assets = app.MapGroup("/assets");

        assets.MapPost(
            "/upload",
            async (
                HttpRequest request, ArchiveDbContext db, ArchiveOptions options, ValidatorRunner validator,
                FileStorage storage, HttpContext http, CancellationToken ct
            ) =>
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

                if (!AssetKinds.TryParseName(typeText, out var kind))
                    return Results.BadRequest(new { error = "Unknown asset type." });

                // Executable content and replays are not published through the asset API: a
                // downloaded gamemode/AI package is code, and replay submission has its own
                // authoritative path.
                if (!AssetKinds.IsPublishable(kind))
                    return Results.BadRequest(new { error = $"{AssetKinds.Name(kind)} cannot be published as an asset." });

                var extension = Path.GetExtension(file.FileName);
                if (!AllowedExtensions.Contains(extension))
                    return Results.BadRequest(new { error = "Only .radpack and .rad files are accepted." });

                if (!AssetKinds.IsUploadExtensionAllowed(kind, extension))
                    return Results.BadRequest(new { error = "A legacy .rad file can only be a Car, StagePiece or Wheel." });

                if (file.Length > RadpackLimits.MaxCompressedBytes)
                    return Results.BadRequest(new { error = "Asset exceeds the size limit." });

                await using var source = file.OpenReadStream();
                var (hash, relative, size) = await storage.PutAsync(source, RadpackLimits.MaxCompressedBytes, ct);
                var path = Path.Combine(storage.Root, relative);

                var failure = await ValidateAsync(validator, path, extension, kind, ct);
                if (failure is { } error)
                {
                    await storage.DeleteIfUnreferencedAsync(relative, db, ct);
                    return Results.BadRequest(new { error });
                }

                // Identical bytes are one asset. If it is already catalogued, the upload is a
                // success that reports the canonical hash and metadata rather than a second row.
                var existing = await db.Assets
                    .Include(a => a.Owner)
                    .FirstOrDefaultAsync(a => a.ContentHash == hash, ct);
                if (existing is not null)
                {
                    if ((byte)existing.Type != kind)
                    {
                        await storage.DeleteIfUnreferencedAsync(relative, db, ct);
                        return Results.BadRequest(
                            new
                            {
                                error = $"Those bytes are already catalogued as a {AssetKinds.Name((byte)existing.Type)}."
                            }
                        );
                    }

                    if (!existing.IsValidated)
                    {
                        existing.IsValidated = true;
                        await db.SaveChangesAsync(ct);
                    }

                    return Results.Ok(new { status = "already present", asset = ToInfo(existing) });
                }

                var asset = new Asset
                {
                    OwnerId = user.Id,
                    Type = (AssetType)kind,
                    Name = name,
                    Description = description,
                    ContentHash = hash,
                    StoragePath = relative,
                    SizeBytes = size,
                    IsPublic = true,
                    IsValidated = true
                };
                db.Assets.Add(asset);
                await db.SaveChangesAsync(ct);

                asset.Owner = user;
                return Results.Ok(new { status = "stored", asset = ToInfo(asset) });
            }
        );

        assets.MapGet(
            "/search",
            async (
                ArchiveDbContext db, string? query, string? type, int? limit, int? after,
                CancellationToken ct
            ) =>
            {
                byte? kind = null;
                if (!string.IsNullOrWhiteSpace(type))
                {
                    if (!AssetKinds.TryParseName(type, out var parsed))
                        return Results.BadRequest(new { error = "Unknown asset type." });
                    kind = parsed;
                }

                var pageSize = Math.Clamp(limit ?? 50, 1, MaxSearchLimit);

                var q = Visible(db.Assets);
                if (kind is { } filter)
                    q = q.Where(a => (byte)a.Type == filter);

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var escaped = EscapeLike(query.Trim());
                    q = q.Where(a => EF.Functions.Like(a.Name, "%" + escaped + "%", "\\"));
                }

                // Newest first by id: SQLite cannot ORDER BY a DateTimeOffset, and ids follow
                // creation order. The cursor is the last id of this page, so paging stays stable
                // even while new assets are being uploaded.
                if (after is { } cursor)
                    q = q.Where(a => a.Id < cursor);

                var rows = await q
                    .OrderByDescending(a => a.Id)
                    .Take(pageSize + 1)
                    .Select(
                        a => new AssetRow(
                            a.Id,
                            a.Name,
                            (byte)a.Type,
                            a.ContentHash,
                            a.SizeBytes,
                            a.DownloadCount,
                            a.Description,
                            a.Owner != null ? a.Owner.Username : null,
                            a.CreatedAt,
                            a.IsValidated
                        )
                    )
                    .ToListAsync(ct);

                var hasMore = rows.Count > pageSize;
                var items = rows.Take(pageSize).Select(ToInfo).ToList();
                return Results.Ok(new AssetSearchPage(items, hasMore ? items[^1].Id : null));
            }
        );

        // Metadata by hash, for eligibility checks before a download.
        assets.MapGet(
            "/meta/{hash}",
            async (string hash, ArchiveDbContext db, CancellationToken ct) =>
            {
                if (!IsHash(hash))
                    return Results.BadRequest(new { error = "Not a content hash." });

                var asset = await Visible(db.Assets).Include(a => a.Owner)
                    .FirstOrDefaultAsync(a => a.ContentHash == hash, ct);
                return asset is null ? Results.NotFound() : Results.Ok(ToInfo(asset));
            }
        );

        // Fetched by hash so a client can verify what it got and cache immutably.
        assets.MapGet(
            "/{hash}",
            async (
                string hash, ArchiveDbContext db, FileStorage storage, HttpContext http, CancellationToken ct
            ) =>
            {
                if (!IsHash(hash))
                    return Results.BadRequest();

                var asset = await Visible(db.Assets).FirstOrDefaultAsync(a => a.ContentHash == hash, ct);
                if (asset is null)
                    return Results.NotFound();

                var stream = storage.OpenRead(asset.StoragePath);
                if (stream is null)
                    return Results.NotFound();

                asset.DownloadCount++;
                await db.SaveChangesAsync(ct);

                // Content-addressed, so it never changes: cache it hard.
                http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.File(
                    stream,
                    contentType: "application/octet-stream",
                    fileDownloadName: $"{asset.Name}{DefaultExtension(asset)}",
                    enableRangeProcessing: true
                );
            }
        );
    }

    /// <summary>The one visibility rule every read surface applies.</summary>
    private static IQueryable<Asset> Visible(IQueryable<Asset> assets)
        => assets.Where(a => a.IsPublic && !a.IsTakedown);

    private static bool IsHash(string hash) => hash.Length == 64 && hash.All(Uri.IsHexDigit);

    // Stored bytes are extensionless, so the download name gets the format's own extension.
    private static string DefaultExtension(Asset asset) => ".radpack";

    private sealed record AssetRow(
        int Id,
        string Name,
        byte Type,
        string ContentHash,
        long SizeBytes,
        int DownloadCount,
        string? Description,
        string? Author,
        DateTimeOffset CreatedAt,
        bool IsValidated
    );

    private static AssetInfo ToInfo(Asset asset) => ToInfo(
        new AssetRow(
            asset.Id,
            asset.Name,
            (byte)asset.Type,
            asset.ContentHash,
            asset.SizeBytes,
            asset.DownloadCount,
            asset.Description,
            asset.Owner?.Username,
            asset.CreatedAt,
            asset.IsValidated
        )
    );

    private static AssetInfo ToInfo(AssetRow row) => new(
        row.Id,
        row.Name,
        AssetKinds.Name(row.Type),
        row.ContentHash,
        row.SizeBytes,
        row.DownloadCount,
        row.Description,
        row.Author,
        row.CreatedAt,
        row.IsValidated && AssetKinds.IsDataAsset(row.Type)
    );

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// Reads the stored bytes as the claimed kind. Radpacks are checked against their own header;
    /// a legacy model has no kind, so the validator only proves it parses as a model of a kind it
    /// could be, which is why legacy content is not treated as an authority for anything else.
    /// </summary>
    private static async Task<string?> ValidateAsync(
        ValidatorRunner validator,
        string path,
        string extension,
        byte kind,
        CancellationToken ct
    )
    {
        if (!extension.Equals(".radpack", StringComparison.OrdinalIgnoreCase))
        {
            var legacy = await validator.RunAsync("rad", [path, AssetKinds.Name(kind)], ct);
            return legacy.Ok ? null : legacy.Error ?? "The asset is not a readable model.";
        }

        var result = await validator.RunAsync("radpack", [path], ct);
        if (!result.Ok)
            return result.Error ?? "The asset is not a readable radpack.";

        // The kind inside the file has to agree with what the uploader claimed; otherwise a car
        // could be listed as a stage and downloaded into the wrong slot.
        if (result.Output is { } output
            && output.TryGetProperty("kind", out var validated)
            && validated.GetString() is { } validatedKind
            && !string.Equals(validatedKind, AssetKinds.Name(kind), StringComparison.OrdinalIgnoreCase))
            return $"The asset is a {validatedKind}, not a {AssetKinds.Name(kind)}.";

        return null;
    }
}
