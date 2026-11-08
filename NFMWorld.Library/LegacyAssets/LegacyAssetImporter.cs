using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.LegacyAssets;

/// <summary>
/// Reads assets from the pre-NFMW multiplayer servers, for the stages and cars that only exist
/// there. Read-only: NFMW's own archive is always tried first, a local <c>data/legacy</c> copy
/// next, and the network last, from the handful of hosts in <see cref="AllowedHosts"/> only.
/// Fetched payloads are kept in memory for the session and never written to disk.
/// </summary>
/// <remarks>
/// A <c>.radq</c> is a zip of one <c>.rad</c> file, but the legacy HTTP path read the response in
/// 20-byte chunks that skipped the first byte of each, so the zip starts at byte 20 up to offset
/// 500 and byte 40 after that (phy-addons' <c>SharedInfoService.getOnlineAsset</c>). Raw,
/// undamaged zips are accepted too, so a re-packaged archive still imports.
/// </remarks>
[PluginVisible]
public static class LegacyAssetImporter
{
    /// <summary>The original download host. NFMW ships from its own archive first.</summary>
    public const string CarsHost = "http://multiplayer.needformadness.com/cars/";

    public const string TracksHost = "http://multiplayer.needformadness.com/tracks/";

    /// <summary>phy-addons' model source: plain <c>.rad</c> text.</summary>
    private const string PhyModelHost = "https://raw.githubusercontent.com/JaroPaska/phy-addons-data/master/models/";

    /// <summary>Dragshot's model mirror: a zip of one <c>.rad</c>.</summary>
    private const string DsModelHost = "http://ds-serv.tk/nfm-models/";

    /// <summary>
    /// The only hosts a stage or car may make the game contact. A stage file is untrusted input,
    /// so the URL is never taken from it: labels only select a name within a fixed template.
    /// </summary>
    private static readonly string[] AllowedHosts =
    [
        "multiplayer.needformadness.com",
        "raw.githubusercontent.com",
        "ds-serv.tk",
    ];

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly ConcurrentDictionary<string, byte[]?> Cache = new();

    /// <summary>
    /// Decodes an embedded stage model (<c>amodel</c>/<c>amodel2</c>): base64 of a compressed
    /// <c>.rad</c>. Returns null when the payload is not a model.
    /// </summary>
    public static string? DecodeEmbeddedRad(string base64)
    {
        try
        {
            return Encoding.UTF8.GetString(Inflate(Convert.FromBase64String(base64)));
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException)
        {
            Logging.Error($"Could not decode an embedded stage model: {ex.Message}");
            return null;
        }
    }

    /// <summary>Extracts the <c>.rad</c> text from a <c>.radq</c> payload, or null if it is not one.</summary>
    public static string? DecodeRadq(ReadOnlySpan<byte> payload)
    {
        if (TryReadZip(payload, out var rad)) return rad;

        // Legacy windowed skip: the zip body starts at 20, then every further byte at 40.
        foreach (var skip in (ReadOnlySpan<int>)[20, 40])
        {
            if (payload.Length > skip && TryReadZip(payload[skip..], out rad)) return rad;
        }

        return null;
    }

    /// <summary>
    /// Loads a <c>data/legacy/{cars|tracks}/{name}.radq</c> the user placed there. Returns the
    /// <c>.rad</c> text, or null when there is no such file.
    /// </summary>
    public static string? TryReadLocal(string kind, string name)
    {
        if (SafeName(name) is not { } safe) return null;

        var path = $"data/legacy/{kind}/{safe}.radq";
        if (!TheVFS.VFS.FileExists(path)) return null;

        try
        {
            return DecodeRadq(TheVFS.VFS.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            Logging.Error($"Could not read legacy asset '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads a car or track <c>.radq</c> from the legacy host. Returns the <c>.rad</c> text,
    /// or null when the name is unusable or the download fails.
    /// </summary>
    public static string? TryFetch(string kind, string name)
    {
        // The legacy host stored names with underscores.
        if (SafeName(name.Replace(' ', '_')) is not { } safe) return null;

        var payload = Download((kind == "cars" ? CarsHost : TracksHost) + safe + ".radq");
        return payload is null ? null : DecodeRadq(payload);
    }

    /// <summary>
    /// Resolves a phy-addons model label (<c>phy:</c>, <c>ds:</c> or <c>nfm:</c> prefixed) into
    /// <c>.rad</c> text. Bare labels are handled locally and return null here.
    /// </summary>
    public static string? TryFetchModel(string label)
    {
        if (label.Length < 5 || label[3] != ':') return null;
        if (SafeName(label[4..]) is not { } safe) return null;

        switch (label[..3])
        {
            case "phy":
                // Plain .rad text, no zip.
                return Download(PhyModelHost + safe + ".rad") is { } rad ? Encoding.UTF8.GetString(rad) : null;
            case "ds":
                return Download(DsModelHost + safe + ".rad.zip") is { } zip ? DecodeRadq(zip) : null;
            case "nfm":
                return TryFetch("cars", safe);
            default:
                return null;
        }
    }

    private static byte[]? Download(string url)
    {
        // No synchronous HTTP on the browser's main thread; a missing asset simply stays missing.
        if (OperatingSystem.IsBrowser()) return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            Logging.Error($"Refusing to download legacy asset from untrusted host: {url}");
            return null;
        }

        return Cache.GetOrAdd(
            url,
            static u =>
            {
                try
                {
                    // Sync from the caller's perspective (stages load synchronously), but the stage
                    // loading thread is not the UI thread, so this cannot block a frame.
                    using var response = Http.GetAsync(u).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        Logging.Info($"Legacy asset {u} answered {(int)response.StatusCode}.");
                        return null;
                    }

                    return ReadCapped(response.Content, MaxAssetBytes, u);
                }
                catch (Exception ex)
                {
                    Logging.Info($"Could not download legacy asset '{u}': {ex.Message}");
                    return null;
                }
            }
        );
    }

    /// <summary>The largest asset accepted, so an allowlisted host cannot exhaust memory.</summary>
    private const int MaxAssetBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Reads a response body, refusing anything past <paramref name="limit"/>. A legacy car or
    /// track is tens of KB, so 2 MB is already generous.
    /// </summary>
    private static byte[]? ReadCapped(HttpContent content, int limit, string url)
    {
        if (content.Headers.ContentLength is { } declared && declared > limit)
        {
            Logging.Info($"Refusing legacy asset {url}: {declared} bytes over the {limit}-byte limit.");
            return null;
        }

        using var stream = content.ReadAsStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                Logging.Info($"Refusing legacy asset {url}: it exceeded the {limit}-byte limit.");
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// phy-addons compresses embedded models with Java's default <c>Deflater</c>, which writes a
    /// zlib wrapper. Written into a plain deflate stream it would fail to decompress, so the
    /// two-byte zlib header is checked for and skipped; raw deflate is accepted as well.
    /// </summary>
    private static byte[] Inflate(byte[] payload)
    {
        using var input = new MemoryStream(payload, writable: false);
        var header = new byte[2];
        var wrapped = input.Read(header) == 2
                      && (header[0] & 0x0F) == 8
                      && ((header[0] << 8) | header[1]) % 31 == 0;
        if (!wrapped) input.Position = 0;

        using var inflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        // A small payload can inflate to gigabytes. A .rad is far under this cap, so stopping
        // here refuses a decompression bomb rather than letting it exhaust memory.
        while ((read = inflate.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (output.Length + read > MaxAssetBytes)
                throw new InvalidDataException($"Embedded model inflates past {MaxAssetBytes} bytes.");
            output.Write(chunk, 0, read);
        }

        return output.ToArray();
    }

    /// <summary>Rejects anything that could escape the fixed URL template.</summary>
    private static string? SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (name.Length > 128) return null;
        if (name.AsSpan().IndexOfAny("\\/:*?\"<>|") >= 0) return null;
        if (name.Contains("..", StringComparison.Ordinal)) return null;
        return name;
    }

    private static bool TryReadZip(ReadOnlySpan<byte> payload, out string? rad)
    {
        rad = null;
        // PK\x03\x04
        if (payload.Length < 4 || payload[0] != 0x50 || payload[1] != 0x4B || payload[2] != 0x03)
            return false;

        try
        {
            using var archive = new ZipArchive(new MemoryStream(payload.ToArray()), ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault();
            if (entry is null) return false;

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            rad = reader.ReadToEnd();
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
