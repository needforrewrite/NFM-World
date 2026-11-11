using NFMWorld.PhysicsFS;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary.Radpack;

/// <summary>
/// Reads radpacks by expected kind, for both local files and canonical archive identities.
/// </summary>
/// <remarks>
/// A radpack's kind lives in its header byte, and one payload type (Rad3d) is shared by cars,
/// stage pieces and wheels. Reading without an expectation therefore lets a car be used as a
/// stage piece, or a track as a car. Everything that loads content goes through here so the
/// kind is checked once, in one place, and corrupt input fails with a message rather than
/// silently becoming a different asset.
/// </remarks>
public static class RadpackReader
{
    /// <summary>Canonical prefix for an archive asset reference, e.g. <c>archive/&lt;sha256&gt;</c>.</summary>
    public const string ArchivePrefix = "archive/";

    /// <summary>Whether <paramref name="reference"/> names an archive asset by content hash.</summary>
    public static bool IsArchiveId(string reference)
        => reference.StartsWith(ArchivePrefix, StringComparison.OrdinalIgnoreCase)
           && reference.Length == ArchivePrefix.Length + 64
           && reference.AsSpan(ArchivePrefix.Length).IndexOfAnyExcept("0123456789abcdefABCDEF".AsSpan()) < 0;

    /// <summary>The cache key for a reference: the canonical hash for archive assets, else the path.</summary>
    public static string Identity(string reference)
        => IsArchiveId(reference) ? ArchivePrefix + reference[ArchivePrefix.Length..].ToLowerInvariant() : reference;

    /// <summary>Reads and validates a radpack of exactly <paramref name="expected"/> kind.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a well-formed, matching radpack.</exception>
    public static RadpackAsset Read(string path, RadpackType expected)
        => Read(VFS, path, expected, RadpackLimits.MaxCompressedBytes, RadpackLimits.MaxDecompressedBytes);

    public static RadpackAsset Read(
        VirtualFS fs,
        string path,
        RadpackType expected,
        int maxCompressedBytes,
        int maxDecompressedBytes
    )
        => Read(fs.ReadAllBytes(path), path, expected, maxCompressedBytes, maxDecompressedBytes);

    public static RadpackAsset Read(
        ReadOnlySpan<byte> data,
        string description,
        RadpackType expected,
        int maxCompressedBytes,
        int maxDecompressedBytes
    )
    {
        RadpackAsset asset;
        try
        {
            asset = RadpackSerializer.Deserialize(data, maxCompressedBytes, maxDecompressedBytes);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"'{description}': {ex.Message}", ex);
        }

        if (asset.Metadata.Type != expected)
            throw new InvalidDataException($"'{description}' is a {asset.Metadata.Type}, not a {expected}.");

        return asset;
    }

    /// <summary>As <see cref="Read(string, RadpackType)"/>, but returns null and the reason instead of throwing.</summary>
    public static RadpackAsset? TryRead(string path, RadpackType expected, out string? error)
    {
        try
        {
            error = null;
            return Read(path, expected);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Loads a car or stage-piece model by local name or archive identity. The local lookup order
    /// is the archive identity itself, then <c>data/models/&lt;name&gt;.radpack</c>, then the legacy
    /// <c>data/models/&lt;name&gt;.rad</c> text. A file of the wrong kind is rejected, never returned.
    /// </summary>
    public static Rad3d? TryReadModel(string reference, RadpackType expected, out string? error)
    {
        error = null;

        if (IsArchiveId(reference))
            return ReadModelAt(reference, reference, expected, out error);

        var radpackPath = $"data/models/{reference}.radpack";
        if (VFS.FileExists(radpackPath))
            return ReadModelAt(radpackPath, reference, expected, out error);

        var radPath = $"data/models/{reference}.rad";
        if (VFS.FileExists(radPath))
        {
            try
            {
                return RadParser.ParseRad(VFS.ReadAllText(radPath), reference);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
            {
                error = $"'{radPath}': {ex.Message}";
                return null;
            }
        }

        return null;
    }

    /// <summary>Loads a stage by local name, accepting only a track radpack.</summary>
    public static StageLoader? TryReadTrack(string reference, out string? error)
    {
        error = null;
        var path = IsArchiveId(reference) ? reference : $"data/stages/{reference}.radpack";
        if (!VFS.FileExists(path))
            return null;

        try
        {
            return Read(path, RadpackType.Track) is RadpackTrack track
                ? track.Stage
                : throw new InvalidDataException($"'{path}' is not a track.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }

    private static Rad3d? ReadModelAt(string path, string description, RadpackType expected, out string? error)
    {
        try
        {
            error = null;
            return Read(path, expected) is RadpackRad3d rad
                ? rad.Rad
                : throw new InvalidDataException($"'{description}' does not contain a model.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }
}
