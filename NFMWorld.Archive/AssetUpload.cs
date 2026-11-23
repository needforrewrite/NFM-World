using NFMWorld.Archive.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive;

/// <summary>
/// The upload rules every surface applies: the JSON API, the website's publish form and the
/// edit page's replacement file. They used to be written out three times, and the website
/// silently lost its extension check because of it.
/// </summary>
public static class AssetUpload
{
    /// <summary>The file formats an upload may carry.</summary>
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".radpack", ".rad"
    };

    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MultipartOverheadBytes = 64 * 1024;
    public const int MaxRequestBytes = RadpackLimits.MaxCompressedBytes + MultipartOverheadBytes;

    public static string SizeLimitMessage(int maxBytes)
        => $"The file exceeds this Archive's {maxBytes / ArchiveSettings.BytesPerMiB} MiB upload limit.";

    public static async Task<AssetReviewStatus> ReviewStatusForUploadAsync(
        ArchiveDbContext db,
        int uploaderId,
        CancellationToken ct
    )
        => await db.Users.AnyAsync(user => user.Id == uploaderId && user.IsVerifiedUploader && user.DeletedAt == null, ct)
            ? AssetReviewStatus.Verified
            : AssetReviewStatus.Pending;

    public static void SetReplacementReview(ArchiveDbContext db, Asset asset, AssetReviewStatus status)
    {
        asset.ReviewStatus = status;
        // A moderator may have reviewed the previous file while the replacement was validating.
        db.Entry(asset).Property(row => row.ReviewStatus).IsModified = true;
    }

    public static async Task<IFormCollection?> ReadFormWithinLimitAsync(HttpRequest request, int maxBytes, CancellationToken ct)
    {
        var requestLimit = maxBytes + MultipartOverheadBytes;
        if (request.ContentLength > requestLimit) return null;

        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
            feature.MaxRequestBodySize = requestLimit;

        try
        {
            return await request.ReadFormAsync(ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks the claim before a byte is stored: the kind may be published, the extension is one
    /// this archive accepts, and that extension may claim that kind.
    /// Returns the message to show, or null when the upload may proceed.
    /// </summary>
    public static string? CheckClaim(byte kind, string? fileName)
    {
        if (!AssetKinds.IsPublishable(kind))
            return $"{AssetKinds.Name(kind)} cannot be published as an asset.";

        var extension = Path.GetExtension(fileName ?? "");
        if (!AllowedExtensions.Contains(extension))
            return "Only .radpack and .rad files are accepted.";

        if (!AssetKinds.IsUploadExtensionAllowed(kind, extension))
            return "A legacy .rad file can only be a Car, StagePiece or Wheel.";

        return null;
    }

    /// <summary>Trims and bounds a display name. Null when it is empty or too long.</summary>
    public static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxNameLength ? null : trimmed;
    }

    /// <summary>Trims a description; null means "none". Too long is reported by the caller.</summary>
    public static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static bool DescriptionTooLong(string? description)
        => description is { Length: > MaxDescriptionLength };

    /// <summary>
    /// Stores the bytes and then validates the stored file, so what is checked is byte-for-byte
    /// what later gets served. A rejected upload deletes its file only when no other row shares
    /// it: storage is content-addressed, so a second upload of the same bytes resolves to one file.
    /// </summary>
    public static async Task<StoredUpload> StoreAndValidateAsync(
        FileStorage storage,
        ValidatorRunner validator,
        ArchiveDbContext db,
        Stream source,
        string? fileName,
        byte kind,
        int maxBytes,
        CancellationToken ct
    )
    {
        var (hash, relative, size) = await storage.PutAsync(source, maxBytes, ct);
        var extension = Path.GetExtension(fileName ?? "");
        var path = Path.Combine(storage.Root, relative);

        var error = await ValidateAsync(validator, path, extension, kind, ct);
        if (error is not null)
            await storage.DeleteIfUnreferencedAsync(relative, db, ct);

        return new StoredUpload(hash, relative, size, error);
    }

    /// <summary>
    /// Stores a replacement for an existing row and validates the stored file. A replacement is
    /// not an upload: the row already has a kind, and the author may switch between a radpack and a
    /// legacy <c>.rad</c> model, so the format is decided by what the bytes actually are rather
    /// than by the file name the browser or the game happened to send.
    /// </summary>
    public static async Task<StoredUpload> StoreAndValidateReplacementAsync(
        FileStorage storage,
        ValidatorRunner validator,
        ArchiveDbContext db,
        Stream source,
        byte kind,
        int maxBytes,
        CancellationToken ct
    )
    {
        var (hash, relative, size) = await storage.PutAsync(source, maxBytes, ct);
        var path = Path.Combine(storage.Root, relative);

        // The format is decided by the bytes, not by the name: a radpack that fails as a radpack
        // must not be retried as text, or a wrong-kind package would be read as a legacy model.
        var radpack = await IsRadpackAsync(path, ct);
        var error = await ValidateAsync(validator, path, radpack ? ".radpack" : ".rad", kind, ct);

        if (error is not null)
            await storage.DeleteIfUnreferencedAsync(relative, db, ct);

        return new StoredUpload(hash, relative, size, error);
    }

    /// <summary>Whether the stored file begins with the radpack magic (<c>RAD&lt;type&gt;</c>).</summary>
    private static async Task<bool> IsRadpackAsync(string path, CancellationToken ct)
    {
        var head = new byte[3];
        await using var stream = File.OpenRead(path);
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        return read == head.Length && head[0] == 'R' && head[1] == 'A' && head[2] == 'D';
    }

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

        // The kind is passed through so the validator knows a car is being published and can
        // check the values the game will drive it with.
        var result = await validator.RunAsync("radpack", [path, AssetKinds.Name(kind)], ct);
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

/// <summary>Where a stored upload landed, and why it was refused when <see cref="Error"/> is set.</summary>
public sealed record StoredUpload(string Hash, string RelativePath, long SizeBytes, string? Error);
