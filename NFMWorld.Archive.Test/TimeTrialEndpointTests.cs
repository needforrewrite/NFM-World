using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

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
        var register = await client.PostAsJsonAsync("/auth/register", new { Username = "racer", Password = "password123" });
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
    public async Task Leaderboard_IsEmptyForAnUnknownStage()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/tt/leaderboard?stageHash=nothing&carHash=nothing");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var entries = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(0, entries.GetArrayLength());
    }
}
