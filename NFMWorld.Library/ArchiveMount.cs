using System.Net.Http;
using NFMWorld.PhysicsFS;

namespace NFMWorldLibrary;

/// <summary>
/// Reads archive assets on demand at archive/&lt;sha256&gt;, without fetching a remote listing.
/// Existence checks and enumeration only know about assets already fetched through this mount.
/// </summary>
public static class ArchiveMount
{
    public static void MountIfConfigured(string? archiveUrl)
    {
        if (string.IsNullOrWhiteSpace(archiveUrl))
            return;

        var baseUri = new Uri(archiveUrl.EndsWith('/') ? archiveUrl : archiveUrl + "/");
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        TheVFS.VFS.Mount(Create(baseUri, http));
        Logging.Info($"[Archive] Mounted asset store from {baseUri}");
    }

    internal static FetchFileSystem Create(Uri baseUri, HttpClient http)
    {
        byte[]? ReadEntry(string path)
        {
            const string prefix = "archive/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;
            var hash = path[prefix.Length..];
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                return null;

            try
            {
                using var response = http.GetAsync(new Uri(baseUri, $"assets/{hash.ToLowerInvariant()}")).GetAwaiter()
                    .GetResult();
                return response.IsSuccessStatusCode
                    ? response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                    : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Logging.Warning($"[Archive] Could not fetch {path}: {ex.Message}");
                return null;
            }
        }

        return new FetchFileSystem(ReadEntry);
    }
}
