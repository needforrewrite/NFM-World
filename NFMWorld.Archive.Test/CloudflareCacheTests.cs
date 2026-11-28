using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NFMWorld.Archive;
using NFMWorld.Archive.Cache;

namespace NFMWorld.Archive.Test;

[TestClass]
public class CloudflareCacheTests
{
    private sealed class RecordingHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(string Uri, string? Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(
                (
                    request.RequestUri!.ToString(),
                    request.Headers.Authorization?.ToString(),
                    request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)
                )
            );

            return new HttpResponseMessage(status);
        }
    }

    private static CloudflareCache Cache(RecordingHandler handler, string? zone = "zone123", string? token = "token")
        => new(
            new HttpClient(handler),
            new ArchiveOptions
            {
                PublicBaseUrl = "https://archive.example",
                CloudflareZoneId = zone,
                CloudflareApiToken = token
            },
            NullLogger<CloudflareCache>.Instance
        );

    [TestMethod]
    public async Task Purge_SendsEachDistinctAssetUrlOnce()
    {
        var handler = new RecordingHandler();
        await Cache(handler).PurgeAssetsAsync([new string('A', 64), new string('a', 64), "not-a-hash"]);

        var (uri, authorization, body) = handler.Requests.Single();
        Assert.AreEqual("https://api.cloudflare.com/client/v4/zones/zone123/purge_cache", uri);
        Assert.AreEqual("Bearer token", authorization);

        using var document = JsonDocument.Parse(body);
        CollectionAssert.AreEqual(
            new[] { "https://archive.example/assets/" + new string('a', 64) },
            document.RootElement.GetProperty("files").EnumerateArray().Select(file => file.GetString()!).ToArray()
        );
    }

    [TestMethod]
    public async Task Purge_SplitsAtTheApiBatchLimit()
    {
        // Cloudflare refuses more than 30 URLs in one request.
        var handler = new RecordingHandler();
        var hashes = Enumerable.Range(0, 31).Select(i => i.ToString("x2").PadLeft(64, '0')).ToArray();

        await Cache(handler).PurgeAssetsAsync(hashes);

        Assert.AreEqual(2, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Purge_DoesNothingWhenUnconfigured()
    {
        var handler = new RecordingHandler();
        await Cache(handler, zone: "", token: "").PurgeAssetsAsync([new string('a', 64)]);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Purge_FailureIsSwallowed()
    {
        // A Cloudflare outage must never turn a takedown or a delete into an error for the user.
        var handler = new RecordingHandler(HttpStatusCode.Forbidden);
        await Cache(handler).PurgeAssetsAsync([new string('a', 64)]);
        Assert.AreEqual(1, handler.Requests.Count);
    }
}
