using System.Net.Http;
using NFMWorld.PhysicsFS;

namespace NFMWorldLibrary;

/// <summary>
/// Mounts the archive's asset store as a read-only VFS layer.
///
/// Assets are content-addressed on the server, so the client lists what it wants through the
/// archive's search endpoint and then reads each file by hash. The listing is fetched once and
/// cached as the mount's known-path set, keeping existence checks synchronous while the bytes
/// still arrive on demand.
///
/// Off by default: an offline game and a server deployment must not depend on a reachable
/// archive, so this only runs when an archive URL is configured.
/// </summary>
public static class ArchiveMount
{
    public static void MountIfConfigured(string? archiveUrl)
    {
        if (string.IsNullOrWhiteSpace(archiveUrl))
            return;

        var baseUri = new Uri(archiveUrl.EndsWith('/') ? archiveUrl : archiveUrl + "/");
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // Fetch a file's bytes by the path the mount knows it as. A path is
        // "archive/<type>/<name>.<ext>"; the hash rides in the manifest, so the mount translates
        // through a cached manifest rather than accepting a hash-shaped path from callers.
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        List<string> ListPaths()
        {
            try
            {
                // One listing covers every asset type; the page size is generous because the
                // client only needs paths, not bodies.
                using var response = http.GetAsync(new Uri(baseUri, "assets/search?limit=100")).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return [];

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var document = System.Text.Json.JsonDocument.Parse(json);
                var paths = new List<string>();
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    var hash = entry.GetProperty("contentHash").GetString();
                    var name = entry.GetProperty("name").GetString();
                    var type = entry.GetProperty("type").GetString();
                    if (hash is null || name is null || type is null)
                        continue;

                    var path = $"archive/{type}/{name}";
                    manifest[path] = hash;
                    paths.Add(path);
                }

                return paths;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                Logging.Warning($"[Archive] Could not list archive assets: {ex.Message}");
                return [];
            }
        }

        byte[]? ReadEntry(string path)
        {
            if (!manifest.TryGetValue(path, out var hash))
                return null;

            try
            {
                using var response = http.GetAsync(new Uri(baseUri, $"assets/{hash}")).GetAwaiter().GetResult();
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

        TheVFS.VFS.Mount(new FetchFileSystem(ReadEntry, listPaths: ListPaths));
        Logging.Info($"[Archive] Mounted asset store from {baseUri}");
    }
}
