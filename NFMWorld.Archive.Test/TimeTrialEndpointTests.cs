using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Content;

namespace NFMWorld.Archive.Test;

/// <summary>
/// The validator is a separate executable that is not built for the test run, so these cover the
/// archive's own behaviour around it: refusing to rank without a validator rather than trusting
/// the client, and the leaderboard's shape.
/// </summary>
[TestClass]
public class TimeTrialEndpointTests
{
    private static async Task<HttpClient> SignedInClient(ArchiveAppFactory factory)
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/auth/register", ArchiveAppFactory.Registration("racer"));
        var token = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MultipartFormDataContent Upload(byte[] replay, string stageHash, string carHash)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(replay);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "replay.timetrial");
        form.Add(new StringContent(stageHash), "stageHash");
        form.Add(new StringContent(carHash), "carHash");
        form.Add(new StringContent("nfmm/road"), "stageName");
        form.Add(new StringContent("nfmm/radicalone"), "carName");
        return form;
    }

    [TestMethod]
    public async Task Upload_RequiresSignIn()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/tt/upload", Upload([1, 2, 3], "s", "c"));
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Upload_WithoutAValidator_RefusesRatherThanTrustingTheClient()
    {
        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        var response = await client.PostAsync("/tt/upload", Upload(new byte[64], "stagehash", "carhash"));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Upload_RefusesHashesThatAreNotPublicValidatedContent()
    {
        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        // Nothing is catalogued under these hashes.
        var response = await client.PostAsync("/tt/upload", Upload(new byte[64], Hash('a'), Hash('b')));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string Hash(char c) => new(c, 64);

    [TestMethod]
    public async Task Leaderboard_IsEmptyForAnUnknownStage()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/tt/leaderboard?stageHash=nothing&carHash=nothing");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var entries = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(0, entries.GetArrayLength());
    }

    [TestMethod]
    public async Task ReplayBrowserClient_ReadsTheArchiveContractIncludingPublicationDate()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        var published = new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            db.TimeTrials.Add(new TimeTrial
            {
                User = new User { Username = "racer", UsernameNormalized = "racer" },
                StageHash = Hash('a'), CarHash = Hash('b'), StageName = "nfmm/road", CarName = "nfmm/radicalone",
                StoragePath = "test/replay", TotalTicks = 1234, ReplayVersion = 7, CreatedAt = published
            });
            await db.SaveChangesAsync();
        }

        using var browser = new ArchiveReplayClient(client.BaseAddress!.ToString(), client);
        var page = await browser.BrowseAsync("nfmm/road", 10, null, CancellationToken.None);
        var replay = page.Items.Single();
        Assert.AreEqual("racer", replay.Username);
        Assert.AreEqual("nfmm/road", replay.StageName);
        Assert.AreEqual("nfmm/radicalone", replay.CarName);
        Assert.AreEqual(1234, replay.TotalTicks);
        Assert.AreEqual(7, replay.ReplayVersion);
        Assert.AreEqual(published, replay.CreatedAt);
    }

    [TestMethod]
    public async Task Replays_ArePublicAndPaged()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        // Public like the leaderboard: browsing needs no account, and an anonymous client must
        // not be refused a list that carries nothing but a username and a time.
        var response = await client.GetAsync("/tt/replays?limit=10");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(0, page.GetProperty("items").GetArrayLength());
        Assert.AreEqual(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }
}
