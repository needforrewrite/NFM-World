using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NFMWorld.Archive.Cache;

/// <summary>
/// Purges the edge copy of content-addressed downloads after a row's bytes, visibility or display
/// name changed.
/// </summary>
/// <remarks>
/// <para>
/// Downloads are served <c>immutable</c> for a year, so an edge would otherwise keep answering a
/// taken-down asset for everyone who had not fetched it yet: the origin's 404 never gets a chance
/// to run. The address is the content hash, so the purge is per URL.
/// </para>
/// <para>
/// Opt-in. With no zone id and token the archive makes no outbound request at all. The token only
/// needs <c>Zone → Cache Purge → Purge</c> on that zone. A failure is logged and swallowed: a
/// moderation action or a delete must not fail because Cloudflare is unreachable.
/// </para>
/// </remarks>
public sealed class CloudflareCache(HttpClient http, ArchiveOptions options, ILogger<CloudflareCache> logger)
{
    /// <summary>Cloudflare takes at most 30 URLs in one purge request.</summary>
    private const int BatchSize = 30;

    /// <summary>Whether a zone and token are configured and the public origin is usable.</summary>
    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(options.CloudflareZoneId)
           && !string.IsNullOrWhiteSpace(options.CloudflareApiToken)
           && Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out _);

    /// <summary>Purges these content hashes from the edge. No-op when purging is not configured.</summary>
    public async Task PurgeAssetsAsync(IEnumerable<string> contentHashes)
    {
        if (!IsConfigured)
            return;

        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        var files = contentHashes
            .Where(hash => hash.Length == 64 && hash.All(Uri.IsHexDigit))
            .Select(hash => $"{baseUrl}/assets/{hash.ToLowerInvariant()}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        for (var start = 0; start < files.Length; start += BatchSize)
        {
            var batch = files[start..Math.Min(start + BatchSize, files.Length)];
            // A refused or unreachable API fails every batch; don't make a bulk unlist wait 5s per
            // 30 URLs. A transient failure is followed by the next moderation action anyway.
            if (!await PurgeAsync(batch))
                return;
        }
    }

    private async Task<bool> PurgeAsync(string[] files)
    {
        // Not the request's token: a player closing the page must not cancel a moderation purge.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.cloudflare.com/client/v4/zones/{options.CloudflareZoneId}/purge_cache"
            )
            {
                Content = JsonContent.Create(new { files })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.CloudflareApiToken);

            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "[Cache] Cloudflare refused a purge of {Count} asset(s): {Status} {Body}",
                    files.Length,
                    (int)response.StatusCode,
                    Truncate(await response.Content.ReadAsStringAsync(timeout.Token), 400)
                );
                return false;
            }

            logger.LogInformation("[Cache] Purged {Count} asset(s) from the edge.", files.Length);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "[Cache] Could not purge {Count} asset(s) from Cloudflare.", files.Length);
            return false;
        }
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
}

public static class CloudflareCacheExtensions
{
    /// <summary>Purges these hashes at the edge; a no-op when Cloudflare purging is not configured.</summary>
    public static Task PurgeAssetsAsync(this HttpContext http, IEnumerable<string> contentHashes)
        => http.RequestServices.GetRequiredService<CloudflareCache>().PurgeAssetsAsync(contentHashes);
}
