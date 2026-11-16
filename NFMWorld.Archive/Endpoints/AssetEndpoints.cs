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
/// One of the signed-in account's own uploads. Unlike <see cref="AssetInfo"/> it carries the
/// row's visibility, because the owner has to be able to see and edit a hidden or taken-down row.
/// </summary>
public sealed record OwnedAssetInfo(
    int Id,
    string Name,
    string Type,
    string ContentHash,
    long SizeBytes,
    int DownloadCount,
    string? Description,
    DateTimeOffset CreatedAt,
    bool Eligible,
    bool IsPublic,
    bool IsTakedown
);

public sealed record OwnedAssetPage(IReadOnlyList<OwnedAssetInfo> Items, int? NextCursor);

/// <summary>The fields an owner may change without replacing the bytes.</summary>
public sealed record AssetUpdate(string? Name, string? Description, bool? IsPublic, bool? IsTakedown);

/// <summary>
/// Radpack asset upload, search, metadata, download and the owner's edit/delete surface.
///
/// Uploads are validated out of process before being indexed, are addressed by their own hash,
/// and are served with a download disposition from a separate origin: an SVG or HTML payload
/// served inline from the app's own origin under a session cookie would be stored XSS.
/// </summary>
public static class AssetEndpoints
{
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
                var name = AssetUpload.NormalizeName(form["name"].ToString());
                var description = AssetUpload.NormalizeDescription(form["description"].ToString());
                var typeText = form["type"].ToString();

                if (file is null || name is null)
                    return Results.BadRequest(new { error = "Missing file or name." });

                if (AssetUpload.DescriptionTooLong(description))
                    return Results.BadRequest(new { error = "The description is too long." });

                if (!AssetKinds.TryParseName(typeText, out var kind))
                    return Results.BadRequest(new { error = "Unknown asset type." });

                if (AssetUpload.CheckClaim(kind, file.FileName, file.Length) is { } claimError)
                    return Results.BadRequest(new { error = claimError });

                await using var source = file.OpenReadStream();
                var stored = await AssetUpload.StoreAndValidateAsync(
                    storage,
                    validator,
                    db,
                    source,
                    file.FileName,
                    kind,
                    ct
                );
                if (stored.Error is { } error)
                    return Results.BadRequest(new { error });

                // Identical bytes are one asset. If it is already catalogued, the upload is a
                // success that reports the canonical hash and metadata rather than a second row.
                var existing = await db.Assets
                    .Include(a => a.Owner)
                    .FirstOrDefaultAsync(a => a.ContentHash == stored.Hash, ct);
                if (existing is not null)
                {
                    if ((byte)existing.Type != kind)
                        return Results.BadRequest(
                            new
                            {
                                error = $"Those bytes are already catalogued as a {AssetKinds.Name((byte)existing.Type)}."
                            }
                        );

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
                    ContentHash = stored.Hash,
                    StoragePath = stored.RelativePath,
                    SizeBytes = stored.SizeBytes,
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

        // The signed-in account's own uploads, including hidden and taken-down rows. Editing needs
        // to find a row that the public surfaces deliberately do not list.
        assets.MapGet(
            "/mine",
            async (
                ArchiveDbContext db, string? type, int? limit, int? after, HttpContext http,
                CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);
                if (user is null)
                    return Results.Unauthorized();

                byte? kind = null;
                if (!string.IsNullOrWhiteSpace(type))
                {
                    if (!AssetKinds.TryParseName(type, out var parsed))
                        return Results.BadRequest(new { error = "Unknown asset type." });
                    kind = parsed;
                }

                var pageSize = Math.Clamp(limit ?? 50, 1, MaxSearchLimit);
                var q = db.Assets.Where(a => a.OwnerId == user.Id);
                if (kind is { } filter)
                    q = q.Where(a => (byte)a.Type == filter);
                if (after is { } cursor)
                    q = q.Where(a => a.Id < cursor);

                var rows = await q
                    .OrderByDescending(a => a.Id)
                    .Take(pageSize + 1)
                    .Select(
                        a => new
                        {
                            a.Id,
                            a.Name,
                            Type = (byte)a.Type,
                            a.ContentHash,
                            a.SizeBytes,
                            a.DownloadCount,
                            a.Description,
                            a.CreatedAt,
                            a.IsValidated,
                            a.IsPublic,
                            a.IsTakedown
                        }
                    )
                    .ToListAsync(ct);

                var hasMore = rows.Count > pageSize;
                var items = rows.Take(pageSize).Select(
                    a => new OwnedAssetInfo(
                        a.Id,
                        a.Name,
                        AssetKinds.Name(a.Type),
                        a.ContentHash,
                        a.SizeBytes,
                        a.DownloadCount,
                        a.Description,
                        a.CreatedAt,
                        a.IsValidated && AssetKinds.IsDataAsset(a.Type),
                        a.IsPublic,
                        a.IsTakedown
                    )
                ).ToList();

                return Results.Ok(new OwnedAssetPage(items, hasMore ? items[^1].Id : null));
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

        // Metadata, visibility and takedown. An owner may hide or re-list their own upload; only
        // an administrator may take one down or restore it.
        assets.MapPatch(
            "/{id:int}",
            async (
                int id, AssetUpdate update, ArchiveDbContext db, ArchiveOptions options, HttpContext http,
                CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);
                if (user is null)
                    return Results.Unauthorized();

                if (options.ReadOnly)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                var asset = await db.Assets.Include(a => a.Owner).FirstOrDefaultAsync(a => a.Id == id, ct);
                if (asset is null)
                    return Results.NotFound();

                var isOwner = asset.OwnerId == user.Id;
                if (!isOwner && !user.IsAdmin)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                if (update.IsTakedown is not null && !user.IsAdmin)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                var changed = false;
                if (update.Name is not null)
                {
                    var name = AssetUpload.NormalizeName(update.Name);
                    if (name is null)
                        return Results.BadRequest(new { error = "A name is required and must be short." });
                    asset.Name = name;
                    changed = true;
                }

                if (update.Description is not null)
                {
                    var description = AssetUpload.NormalizeDescription(update.Description);
                    if (AssetUpload.DescriptionTooLong(description))
                        return Results.BadRequest(new { error = "The description is too long." });
                    asset.Description = description;
                    changed = true;
                }

                if (update.IsPublic is { } isPublic)
                {
                    asset.IsPublic = isPublic;
                    changed = true;
                }

                if (update.IsTakedown is { } isTakedown)
                {
                    asset.IsTakedown = isTakedown;
                    changed = true;
                }

                if (changed)
                    await db.SaveChangesAsync(ct);

                return Results.Ok(new { asset = ToOwnedInfo(asset) });
            }
        );

        // Replacing the bytes. The row keeps its id and its owner, so a published link and the
        // author's inventory entry survive an update; the hash is what changes, and every replay
        // that ran against the old bytes stops ranking.
        assets.MapPut(
            "/{id:int}/content",
            async (
                int id, HttpRequest request, ArchiveDbContext db, ArchiveOptions options,
                ValidatorRunner validator, FileStorage storage, HttpContext http, CancellationToken ct
            ) =>
            {
                var user = await http.GetUserAsync(db, ct);
                if (user is null)
                    return Results.Unauthorized();

                if (options.ReadOnly)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                var asset = await db.Assets.Include(a => a.Owner).FirstOrDefaultAsync(a => a.Id == id, ct);
                if (asset is null)
                    return Results.NotFound();

                // Replacing content is the author's act; a moderator deletes instead of
                // re-publishing someone else's work under their name.
                if (asset.OwnerId != user.Id)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                if (!request.HasFormContentType)
                    return Results.BadRequest(new { error = "Expected a multipart upload." });

                var form = await request.ReadFormAsync(ct);
                var file = form.Files["file"];
                if (file is null)
                    return Results.BadRequest(new { error = "Missing file." });

                var kind = (byte)asset.Type;
                if (AssetUpload.CheckClaim(kind, file.FileName, file.Length) is { } claimError)
                    return Results.BadRequest(new { error = claimError });

                await using var source = file.OpenReadStream();
                var stored = await AssetUpload.StoreAndValidateReplacementAsync(
                    storage,
                    validator,
                    db,
                    source,
                    kind,
                    ct
                );
                if (stored.Error is { } error)
                    return Results.BadRequest(new { error });

                if (string.Equals(stored.Hash, asset.ContentHash, StringComparison.Ordinal))
                    return Results.Ok(new { status = "unchanged", asset = ToOwnedInfo(asset) });

                // One row per distinct file is the archive's invariant: a second row holding the
                // same bytes would compete with this one for /assets/{hash}.
                var other = await db.Assets.FirstOrDefaultAsync(
                    a => a.ContentHash == stored.Hash && a.Id != asset.Id,
                    ct
                );
                if (other is not null)
                    return Results.BadRequest(
                        new { error = $"Those bytes are already published as \"{other.Name}\"." }
                    );

                var previous = asset.StoragePath;
                var previousHash = asset.ContentHash;
                asset.ContentHash = stored.Hash;
                asset.StoragePath = stored.RelativePath;
                asset.SizeBytes = stored.SizeBytes;
                asset.IsValidated = true;
                await db.SaveChangesAsync(ct);

                // Saved first, so the delete sees the row's new path and leaves the replacement alone.
                await storage.DeleteIfUnreferencedAsync(previous, db, ct);

                // Every replay pinned the bytes that just stopped being served.
                await TimeTrialInvalidation.InvalidateForAssetAsync(db, previousHash, ct);

                return Results.Ok(new { status = "replaced", asset = ToOwnedInfo(asset) });
            }
        );

        assets.MapDelete(
            "/{id:int}",
            async (int id, ArchiveDbContext db, ArchiveOptions options, FileStorage storage, HttpContext http,
                CancellationToken ct) =>
            {
                var user = await http.GetUserAsync(db, ct);
                if (user is null)
                    return Results.Unauthorized();

                if (options.ReadOnly)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct);
                if (asset is null)
                    return Results.NotFound();

                if (asset.OwnerId != user.Id && !user.IsAdmin)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                var path = asset.StoragePath;
                var hash = asset.ContentHash;
                db.Assets.Remove(asset);
                await db.SaveChangesAsync(ct);

                await storage.DeleteIfUnreferencedAsync(path, db, ct);
                await TimeTrialInvalidation.InvalidateForAssetAsync(db, hash, ct);

                return Results.NoContent();
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

    private static AssetRow Projection(Asset a) => new(
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
    );

    private static AssetInfo ToInfo(Asset asset) => ToInfo(Projection(asset));

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

    private static OwnedAssetInfo ToOwnedInfo(Asset asset) => new(
        asset.Id,
        asset.Name,
        AssetKinds.Name((byte)asset.Type),
        asset.ContentHash,
        asset.SizeBytes,
        asset.DownloadCount,
        asset.Description,
        asset.CreatedAt,
        asset.IsValidated && AssetKinds.IsDataAsset((byte)asset.Type),
        asset.IsPublic,
        asset.IsTakedown
    );

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
