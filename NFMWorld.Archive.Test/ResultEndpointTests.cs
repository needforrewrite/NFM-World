using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Test;

/// <summary>Same app, but with a trusted-server key configured so results can be signed.</summary>
public sealed class TrustedArchiveAppFactory : ArchiveAppFactory
{
    public static readonly byte[] Secret = Encoding.UTF8.GetBytes("test-secret-key");
    public const string KeyId = "lobby";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(
            "Archive:TrustedServerKeys",
            $"{KeyId}={Convert.ToBase64String(Secret)}"
        );
    }
}

[TestClass]
public class ResultEndpointTests
{
    private static string Sign(string json, string nonce) =>
        HmacAuth.SignWithNonce(
            "POST",
            "/results/match",
            Encoding.UTF8.GetBytes(json),
            TrustedArchiveAppFactory.KeyId,
            TrustedArchiveAppFactory.Secret,
            nonce
        );

    private static HttpRequestMessage Request(string json, string? authHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/results/match")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (authHeader is not null)
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
        return request;
    }

    private static async Task<(int AliceId, int BobId)> RegisterTwo(ArchiveAppFactory factory)
    {
        using var client = factory.CreateClient();

        async Task<int> Register(string name)
        {
            var response = await client.PostAsJsonAsync("/auth/register", ArchiveAppFactory.Registration(name));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            return db.Users.Single(u => u.UsernameNormalized == name).Id;
        }

        return (await Register("alice"), await Register("bob"));
    }

    [TestMethod]
    public async Task Match_RejectsAnUnsignedRequest()
    {
        using var factory = new TrustedArchiveAppFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(
            Request("""{"matchKey":"m1","gamemode":"nfmm/pvp","stageName":"s","durationTicks":1,"players":[]}""", null)
        );
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Match_RejectsAReplayedRequest()
    {
        using var factory = new TrustedArchiveAppFactory();
        using var client = factory.CreateClient();
        var (alice, bob) = await RegisterTwo(factory);

        var json = $$"""
                     {"matchKey":"m1","gamemode":"nfmm/pvp","stageName":"s","durationTicks":1,
                      "players":[{"accountId":{{alice}},"finishPosition":0,"finishTimeTicks":null,"isBot":false},
                                 {"accountId":{{bob}},"finishPosition":1,"finishTimeTicks":null,"isBot":false}]}
                     """;
        var auth = Sign(json, "fixed-nonce");

        Assert.AreEqual(HttpStatusCode.OK, (await client.SendAsync(Request(json, auth))).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.SendAsync(Request(json, auth))).StatusCode);
    }

    [TestMethod]
    public async Task Match_RatesTheWinnerAndIsIdempotentOnTheMatchKey()
    {
        using var factory = new TrustedArchiveAppFactory();
        using var client = factory.CreateClient();
        var (alice, bob) = await RegisterTwo(factory);

        var json = $$"""
                     {"matchKey":"m1","gamemode":"nfmm/pvp","stageName":"s","durationTicks":1,
                      "players":[{"accountId":{{alice}},"finishPosition":0,"finishTimeTicks":null,"isBot":false},
                                 {"accountId":{{bob}},"finishPosition":1,"finishTimeTicks":null,"isBot":false}]}
                     """;

        Assert.AreEqual(HttpStatusCode.OK, (await client.SendAsync(Request(json, Sign(json, "n1")))).StatusCode);
        // Same match, fresh nonce: accepted but not rated twice.
        Assert.AreEqual(HttpStatusCode.OK, (await client.SendAsync(Request(json, Sign(json, "n2")))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var aliceRating = db.Ratings.Single(r => r.UserId == alice && r.Gamemode == "nfmm/pvp");
        var bobRating = db.Ratings.Single(r => r.UserId == bob && r.Gamemode == "nfmm/pvp");

        Assert.IsGreaterThan(1500, aliceRating.Elo);
        Assert.IsLessThan(1500, bobRating.Elo);
        Assert.AreEqual(1, aliceRating.Wins);
        Assert.AreEqual(1, bobRating.Losses);
        Assert.AreEqual(1, aliceRating.Matches);
    }

    [TestMethod]
    public async Task Match_DoesNotRateBots()
    {
        using var factory = new TrustedArchiveAppFactory();
        using var client = factory.CreateClient();
        var (alice, _) = await RegisterTwo(factory);

        var json = $$"""
                     {"matchKey":"m2","gamemode":"nfmm/pvp","stageName":"s","durationTicks":1,
                      "players":[{"accountId":{{alice}},"finishPosition":0,"finishTimeTicks":null,"isBot":false},
                                 {"accountId":null,"finishPosition":1,"finishTimeTicks":null,"isBot":true}]}
                     """;

        Assert.AreEqual(HttpStatusCode.OK, (await client.SendAsync(Request(json, Sign(json, "n3")))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        Assert.IsFalse(db.Ratings.Any(r => r.UserId == alice));
    }
}
