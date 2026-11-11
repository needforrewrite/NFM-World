using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary;
using NFMWorldLibrary.Radpack;

namespace NFMWorldLibrary.Content;

/// <summary>Display metadata for a public archive asset.</summary>
public sealed record RemoteAsset(
    int Id,
    string Name,
    string Type,
    string ContentHash,
    long SizeBytes,
    string? Description,
    string? Author,
    bool Eligible
);

public sealed record RemoteAssetPage(IReadOnlyList<RemoteAsset> Items, int? NextCursor);

public sealed record ContentPrepareResult(bool Ok, string? Error)
{
    public static ContentPrepareResult Success { get; } = new(true, null);
    public static ContentPrepareResult Failure(string error) => new(false, error);
}

/// <summary>
/// The game's read-only client for public archive content: search, metadata and download.
/// </summary>
/// <remarks>
/// This deliberately does not carry the HMAC result-signing key or the lobby's server key; those
/// belong to trusted servers, not to a client that renders a list. A signed-in upload uses
/// <c>AccountManager</c>'s bearer session through its own client.
/// </remarks>
public sealed class ArchiveContentClient : IDisposable
{
    private readonly Uri _baseUri;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public ArchiveContentClient(string baseUrl, HttpClient? http = null)
    {
        _baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<RemoteAssetPage> SearchAsync(
        string? query,
        string? type,
        int limit,
        int? after,
        CancellationToken ct
    )
    {
        var url = $"assets/search?limit={Math.Clamp(limit, 1, 100)}";
        if (!string.IsNullOrWhiteSpace(query))
            url += "&query=" + Uri.EscapeDataString(query.Trim());
        if (!string.IsNullOrWhiteSpace(type))
            url += "&type=" + Uri.EscapeDataString(type.Trim());
        if (after is { } cursor)
            url += "&after=" + cursor;

        using var response = await _http.GetAsync(new Uri(_baseUri, url), ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var items = new List<RemoteAsset>();
        foreach (var item in body.GetProperty("items").EnumerateArray())
        {
            items.Add(
                new RemoteAsset(
                    item.GetProperty("id").GetInt32(),
                    item.GetProperty("name").GetString() ?? "",
                    item.GetProperty("type").GetString() ?? "",
                    item.GetProperty("contentHash").GetString() ?? "",
                    item.GetProperty("sizeBytes").GetInt64(),
                    item.TryGetProperty("description", out var description) ? description.GetString() : null,
                    item.TryGetProperty("author", out var author) ? author.GetString() : null,
                    item.TryGetProperty("eligible", out var eligible) && eligible.GetBoolean()
                )
            );
        }

        int? next = body.TryGetProperty("nextCursor", out var nextCursor) && nextCursor.ValueKind == JsonValueKind.Number
            ? nextCursor.GetInt32()
            : null;

        return new RemoteAssetPage(items, next);
    }

    /// <summary>Metadata for one public asset, used to decide eligibility before downloading.</summary>
    public async Task<RemoteAsset?> MetadataAsync(string hash, CancellationToken ct)
    {
        if (!ContentHashes.IsCanonical(hash))
            return null;

        using var response = await _http.GetAsync(new Uri(_baseUri, $"assets/meta/{hash.ToLowerInvariant()}"), ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var item = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return new RemoteAsset(
            item.GetProperty("id").GetInt32(),
            item.GetProperty("name").GetString() ?? "",
            item.GetProperty("type").GetString() ?? "",
            item.GetProperty("contentHash").GetString() ?? "",
            item.GetProperty("sizeBytes").GetInt64(),
            item.TryGetProperty("description", out var description) ? description.GetString() : null,
            item.TryGetProperty("author", out var author) ? author.GetString() : null,
            item.TryGetProperty("eligible", out var eligible) && eligible.GetBoolean()
        );
    }

    /// <summary>
    /// Downloads one asset, refusing more than <paramref name="maxBytes"/> and anything whose
    /// bytes do not hash to <paramref name="hash"/>.
    /// </summary>
    public async Task<byte[]> DownloadAsync(string hash, int maxBytes, CancellationToken ct)
    {
        if (!ContentHashes.IsCanonical(hash))
            throw new InvalidDataException("Not a content hash.");

        var normalized = hash.ToLowerInvariant();
        using var response = await _http.GetAsync(
            new Uri(_baseUri, $"assets/{normalized}"),
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } length && length > maxBytes)
            throw new InvalidDataException($"Asset is larger than {maxBytes} bytes.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidDataException($"Asset is larger than {maxBytes} bytes.");
            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();
        var actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actual, normalized, StringComparison.Ordinal))
            throw new InvalidDataException("The downloaded asset's hash does not match the requested one.");

        return bytes;
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

public static class ContentHashes
{
    public static bool IsCanonical(string? hash)
        => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);

    public static string Normalize(string hash)
        => hash.Trim().ToLowerInvariant();

    public static string Of(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>
/// Prepared archive content, mounted at the canonical <c>archive/&lt;sha256&gt;</c> paths.
/// </summary>
/// <remarks>
/// A file only appears here after its bytes have verified against the requested hash and decoded
/// as the expected kind, so a synchronous loader can read it without a network call and never sees
/// a partial or corrupt copy. Desktop keeps the cache on disk across restarts; the browser's write
/// root is MEMFS, so there it lasts for the session.
/// </remarks>
public static class ContentCache
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    private static string? _root;
    private static ArchiveCacheFileSystem? _mounted;

    /// <summary>The cache directory, or null before <see cref="Mount"/>.</summary>
    public static string? Root => _root;

    /// <summary>Mounts the cache so cached assets win over the network mount. Idempotent.</summary>
    public static void Mount()
    {
        if (_mounted is not null || TheVFS.VFS.WriteRoot is null)
            return;

        MountAt(Path.Combine(TheVFS.VFS.WriteRoot, "data", "cache", "archive"));
    }

    /// <summary>Mounts the cache at an explicit root; used by tests and alternative heads.</summary>
    public static void MountAt(string root)
    {
        if (_mounted is not null)
            return;

        Directory.CreateDirectory(root);
        _root = Path.GetFullPath(root);
        _mounted = new ArchiveCacheFileSystem(_root);
        TheVFS.VFS.Mount(_mounted);
    }

    public static bool IsPrepared(string hash)
        => _root is not null && ContentHashes.IsCanonical(hash) && File.Exists(PathFor(ContentHashes.Normalize(hash)));

    /// <summary>The VFS path a prepared asset is read from.</summary>
    public static string PathForHash(string hash) => RadpackReader.ArchivePrefix + ContentHashes.Normalize(hash);

    /// <summary>
    /// Downloads and prepares one asset for the given kind, coalescing concurrent requests for the
    /// same hash. Cancellation belongs to the caller: a cancelled waiter does not cancel the work
    /// another caller is waiting on.
    /// </summary>
    public static async Task<ContentPrepareResult> PrepareAsync(
        ArchiveContentClient client,
        string hash,
        AssetKind kind,
        CancellationToken ct
    )
    {
        if (_root is null)
            return ContentPrepareResult.Failure("The content cache is not mounted.");

        if (!ContentHashes.IsCanonical(hash))
            return ContentPrepareResult.Failure("Not a content hash.");

        var normalized = ContentHashes.Normalize(hash);

        var radpackKind = AssetKinds.ToRadpackType(kind);
        if (radpackKind is null)
            return ContentPrepareResult.Failure($"{AssetKinds.Name((byte)kind)} is not downloadable content.");

        if (ReadPrepared(normalized, radpackKind.Value, out _))
            return ContentPrepareResult.Success;

        var gate = Gates.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (ReadPrepared(normalized, radpackKind.Value, out _))
                return ContentPrepareResult.Success;

            byte[] bytes;
            try
            {
                bytes = await client.DownloadAsync(normalized, RadpackLimits.MaxCompressedBytes, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
            {
                return ContentPrepareResult.Failure($"Could not download the asset: {ex.Message}");
            }

            if (!TryDecode(bytes, normalized, radpackKind.Value, out var error))
                return ContentPrepareResult.Failure(error!);

            try
            {
                Publish(normalized, bytes);
            }
            catch (IOException ex)
            {
                return ContentPrepareResult.Failure($"Could not cache the asset: {ex.Message}");
            }

            return ContentPrepareResult.Success;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Removes a cached asset so the next prepare fetches it again.</summary>
    public static void Invalidate(string hash)
    {
        if (_root is null || !ContentHashes.IsCanonical(hash))
            return;

        var path = PathFor(ContentHashes.Normalize(hash));
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>True when <paramref name="bytes"/> are a well-formed radpack of the expected kind.</summary>
    public static bool TryDecode(byte[] bytes, string hash, RadpackType kind, out string? error)
    {
        if (!string.Equals(ContentHashes.Of(bytes), ContentHashes.Normalize(hash), StringComparison.Ordinal))
        {
            error = "The asset's hash does not match the requested one.";
            return false;
        }

        try
        {
            RadpackReader.Read(
                bytes,
                RadpackReader.ArchivePrefix + ContentHashes.Normalize(hash),
                kind,
                RadpackLimits.MaxCompressedBytes,
                RadpackLimits.MaxDecompressedBytes
            );
            error = null;
            return true;
        }
        catch (InvalidDataException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string PathFor(string normalizedHash) => Path.Combine(_root!, normalizedHash);

    /// <summary>
    /// Reads a prepared asset and checks it again. A file left half-written by an older build, or
    /// damaged on disk, is deleted so it is fetched and republished rather than handed to a
    /// loader that would report the whole asset as corrupt.
    /// </summary>
    private static bool ReadPrepared(string normalizedHash, RadpackType kind, out string? error)
    {
        error = null;
        var path = PathFor(normalizedHash);
        if (!File.Exists(path))
            return false;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return false;
        }

        if (TryDecode(bytes, normalizedHash, kind, out error))
            return true;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }

        return false;
    }

    /// <summary>
    /// Writes through a temporary file in the cache directory and renames over the final name, so
    /// a reader never observes a partially written asset.
    /// </summary>
    private static void Publish(string normalizedHash, byte[] bytes)
    {
        var destination = PathFor(normalizedHash);
        var temporary = destination + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, destination, overwrite: true);
    }
}

/// <summary>
/// Serves prepared assets at <c>archive/&lt;sha256&gt;</c>. Enumeration only knows what has been
/// fetched, matching the archive mount's fetched-only semantics.
/// </summary>
internal sealed class ArchiveCacheFileSystem(string root) : IVirtualFileSystem
{
    private const string Prefix = RadpackReader.ArchivePrefix;

    public Stream? OpenRead(string path)
    {
        var file = FileFor(path);
        return file is not null ? File.OpenRead(file) : null;
    }

    public Stream? OpenWrite(string path) => null;

    public bool FileExists(string path) => FileFor(path) is not null;

    public bool DirectoryExists(string path)
        => path.Replace('\\', '/').Trim('/') is "archive" or "" && Directory.Exists(root);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        if (!Directory.Exists(root))
            yield break;

        foreach (var file in Directory.EnumerateFiles(root, "*"))
        {
            var name = Path.GetFileName(file);
            if (ContentHashes.IsCanonical(name))
                yield return Prefix + name.ToLowerInvariant();
        }
    }

    private string? FileFor(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];

        if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var hash = normalized[Prefix.Length..];
        if (!ContentHashes.IsCanonical(hash))
            return null;

        var file = Path.Combine(root, ContentHashes.Normalize(hash));
        return File.Exists(file) ? file : null;
    }
}
